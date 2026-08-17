using Celeste.Mod.NpcPlayer.Input;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

internal static class NpcPlayerHooks
{
    private sealed class NpcDeathBodyState
    {
        public bool CleanupScheduled;
    }

    private static bool loaded;
    private static ConditionalWeakTable<PlayerDeadBody, NpcDeathBodyState> npcDeathBodies = new();
    private static Hook? grabCheckHook;
    private static Hook? crouchDashPressedHook;
    private static Hook? musicUnderwaterHook;

    private delegate bool OrigInputCheck();
    private delegate bool HookInputCheck(OrigInputCheck orig);
    private delegate void OrigSetBool(bool value);
    private delegate void HookSetBool(OrigSetBool orig, bool value);

    public static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        On.Celeste.Celeste.Freeze += OnFreeze;
        On.Celeste.PlayerCollider.Check += NpcPlayerColliderRouter.Check;
        On.Celeste.Input.Rumble += OnRumble;
        On.Celeste.Input.RumbleSpecific += OnRumbleSpecific;
        On.Celeste.Player.Die += OnPlayerDie;
        On.Celeste.PlayerDeadBody.End += OnPlayerDeadBodyEnd;
        On.Celeste.Level.LoadLevel += OnLoadLevel;
        On.Celeste.Holdable.Pickup += OnHoldablePickup;
        On.Celeste.Solid.GetPlayerRider += OnSolidGetPlayerRider;
        On.Celeste.Solid.GetPlayerOnTop += OnSolidGetPlayerOnTop;
        On.Celeste.Solid.GetPlayerClimbing += OnSolidGetPlayerClimbing;
        On.Celeste.JumpThru.GetPlayerRider += OnJumpThruGetPlayerRider;

        grabCheckHook = HookInputGetter("GrabCheck", OnGrabCheck);
        crouchDashPressedHook = HookInputGetter("CrouchDashPressed", OnCrouchDashPressed);
        musicUnderwaterHook = HookStaticBoolSetter(typeof(Audio), "MusicUnderwater", OnSetMusicUnderwater);
    }

    public static void Unload()
    {
        if (!loaded)
            return;
        loaded = false;
        On.Celeste.Celeste.Freeze -= OnFreeze;
        On.Celeste.PlayerCollider.Check -= NpcPlayerColliderRouter.Check;
        On.Celeste.Input.Rumble -= OnRumble;
        On.Celeste.Input.RumbleSpecific -= OnRumbleSpecific;
        On.Celeste.Player.Die -= OnPlayerDie;
        On.Celeste.PlayerDeadBody.End -= OnPlayerDeadBodyEnd;
        On.Celeste.Level.LoadLevel -= OnLoadLevel;
        On.Celeste.Holdable.Pickup -= OnHoldablePickup;
        On.Celeste.Solid.GetPlayerRider -= OnSolidGetPlayerRider;
        On.Celeste.Solid.GetPlayerOnTop -= OnSolidGetPlayerOnTop;
        On.Celeste.Solid.GetPlayerClimbing -= OnSolidGetPlayerClimbing;
        On.Celeste.JumpThru.GetPlayerRider -= OnJumpThruGetPlayerRider;
        grabCheckHook?.Dispose();
        grabCheckHook = null;
        crouchDashPressedHook?.Dispose();
        crouchDashPressedHook = null;
        musicUnderwaterHook?.Dispose();
        musicUnderwaterHook = null;
        PandorasBoxCompatibility.Unload();
        npcDeathBodies = new ConditionalWeakTable<PlayerDeadBody, NpcDeathBodyState>();
    }

    private static void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        PandorasBoxCompatibility.EnsureInstalled();
        // Clear the previous room before vanilla preserves persistent entities.
        // This also recovers orphaned npcPlayers left by older versions.
        NpcPlayerRegistry.RemoveAll(self);
        orig(self, playerIntro, isFromLoader);
        NpcPlayerRegistry.InitializeRoom(self);
    }

    private static void OnFreeze(On.Celeste.Celeste.orig_Freeze orig, float time)
    {
        if (NpcInputContext.Current is not null)
            return;
        orig(time);
    }

    private static void OnSetMusicUnderwater(OrigSetBool orig, bool value)
    {
        if (NpcInputContext.Current is null)
            orig(value);
    }

    private static void OnRumble(
        On.Celeste.Input.orig_Rumble orig,
        RumbleStrength strength,
        RumbleLength length)
    {
        if (NpcInputContext.Current is null)
            orig(strength, length);
    }

    private static void OnRumbleSpecific(On.Celeste.Input.orig_RumbleSpecific orig, float strength, float time)
    {
        if (NpcInputContext.Current is null)
            orig(strength, time);
    }

    private static bool OnGrabCheck(OrigInputCheck orig)
        => NpcInputContext.CurrentDevice?.GrabCheck ?? orig();

    private static bool OnCrouchDashPressed(OrigInputCheck orig)
        => NpcInputContext.CurrentDevice?.CrouchDashPressed ?? orig();

    private static Player? OnSolidGetPlayerRider(On.Celeste.Solid.orig_GetPlayerRider orig, Solid self)
    {
        Player? vanillaRider = orig(self);
        return vanillaRider ?? NpcPlayerRegistry.FindRider(self);
    }

    private static Player? OnSolidGetPlayerOnTop(On.Celeste.Solid.orig_GetPlayerOnTop orig, Solid self)
    {
        Player? vanillaPlayer = orig(self);
        return vanillaPlayer ?? NpcPlayerRegistry.FindOnTop(self);
    }

    private static Player? OnSolidGetPlayerClimbing(On.Celeste.Solid.orig_GetPlayerClimbing orig, Solid self)
    {
        Player? vanillaPlayer = orig(self);
        return vanillaPlayer ?? NpcPlayerRegistry.FindClimbing(self);
    }

    private static Player? OnJumpThruGetPlayerRider(On.Celeste.JumpThru.orig_GetPlayerRider orig, JumpThru self)
    {
        Player? vanillaRider = orig(self);
        return vanillaRider ?? NpcPlayerRegistry.FindRider(self);
    }

    private static bool OnHoldablePickup(On.Celeste.Holdable.orig_Pickup orig, Holdable self, Player player)
    {
        Player? holder = self.Holder;
        if (holder is not null && !ReferenceEquals(holder, player) &&
            (holder is NpcPlayerEntity || player is NpcPlayerEntity))
            return false;

        return orig(self, player);
    }

    private static PlayerDeadBody? OnPlayerDie(
        On.Celeste.Player.orig_Die orig,
        Player self,
        Vector2 direction,
        bool evenIfInvincible,
        bool registerDeathInStats)
    {
        Level? level = self.Scene as Level;
        if (self is not NpcPlayerEntity npc)
        {
            List<NpcPlayerEntity>? synchronizedNpcs = level is not null
                ? NpcPlayerRegistry.SnapshotPlayersLedByPlayer(level)
                : null;
            PlayerDeadBody? body = orig(self, direction, evenIfInvincible, registerDeathInStats);
            if (body is not null && level is not null && synchronizedNpcs is not null)
            {
                Logger.Info(
                    "npcPlayer",
                    $"Death link: captured {synchronizedNpcs.Count} live npcPlayer instance(s) led by the real Player's death.");
                if (synchronizedNpcs.Count > 0)
                    KillNpcSnapshot(synchronizedNpcs, GetSynchronizedDeathDirection(direction));
            }
            return body;
        }

        // npcPlayer is a synthetic Player actor. Its body must be created by
        // Celeste itself without allowing real-player multiplayer, networking,
        // or statistics hooks to treat the NPC as another local player.
        PlayerDeadBody? npcBody = npc.orig_Die(direction, evenIfInvincible, false);
        if (npcBody is null)
            return npcBody;

        // An NPC body never owns the room reload. When no Player death follows,
        // its completed visual effect is removed explicitly instead.
        MarkNpcDeathBody(npcBody);

        if (!npc.LeadToPlayer)
        {
            Logger.Info("npcPlayer", $"Death link: npcPlayer '{npc.NpcId}' died independently of the real Player.");
            return npcBody;
        }

        Player? realPlayer = level is null ? null : FindRealPlayer(level);
        if (realPlayer is null)
            return npcBody;
        if (realPlayer.Dead)
            return npcBody;

        // Match TheoCrystal's ownership model: let the initiating entity finish
        // its own death call, then request an ordinary Player death outside the
        // nested Player.Die hook stack. The real body remains entirely owned by
        // Celeste/SMH+ and therefore gets exactly their normal animation and
        // particle sequence.
        level!.OnEndOfFrame += () =>
        {
            if (realPlayer.Dead || !ReferenceEquals(realPlayer.Scene, level))
                return;

            Vector2 playerDeathDirection = GetSynchronizedDeathDirection(direction);
            PlayerDeadBody? realBody = realPlayer.Die(playerDeathDirection, false, registerDeathInStats);
            if (realBody is not null)
            {
                Logger.Info("npcPlayer", $"Death link: npcPlayer '{npc.NpcId}' created the real Player death body at end of frame.");
            }
            else
                Logger.Warn("npcPlayer", $"Death link: npcPlayer '{npc.NpcId}' died, but the end-of-frame real Player death was rejected.");
        };
        return npcBody;
    }

    private static void OnPlayerDeadBodyEnd(On.Celeste.PlayerDeadBody.orig_End orig, PlayerDeadBody self)
    {
        // NPC bodies never own the screen wipe or room reload. Preserve the
        // complete radial effect, then remove an independent body even when no
        // real Player body exists to reload the room.
        if (npcDeathBodies.TryGetValue(self, out NpcDeathBodyState? state))
        {
            ScheduleNpcDeathBodyCleanup(self, state);
            return;
        }
        orig(self);
    }

    private static void MarkNpcDeathBody(PlayerDeadBody body)
    {
        if (!npcDeathBodies.TryGetValue(body, out _))
            npcDeathBodies.Add(body, new NpcDeathBodyState());
    }

    private static void ScheduleNpcDeathBodyCleanup(PlayerDeadBody body, NpcDeathBodyState state)
    {
        if (state.CleanupScheduled)
            return;
        state.CleanupScheduled = true;

        DeathEffect? deathEffect = body.Get<DeathEffect>();
        if (deathEffect is null)
        {
            if (body.Scene is not null)
                body.RemoveSelf();
            return;
        }

        Action? previousOnEnd = deathEffect.OnEnd;
        deathEffect.OnEnd = () =>
        {
            try
            {
                previousOnEnd?.Invoke();
            }
            finally
            {
                if (body.Scene is not null)
                    body.RemoveSelf();
            }
        };
    }

    private static void KillNpcSnapshot(
        List<NpcPlayerEntity> synchronizedNpcs,
        Vector2 direction)
    {
        // The snapshot was captured before the real Player's original death
        // path could mutate scene tracking. Dispatch immediately after that
        // path succeeds, while every captured NPC instance is still usable.
        int killed = 0;
        foreach (NpcPlayerEntity npc in synchronizedNpcs)
        {
            if (npc.Dead || npc.Scene is null)
                continue;

            // Player-led propagation terminates here: linked NPCs use the
            // original death implementation so they cannot lead back to the
            // Player or establish a direct NPC-to-NPC death path.
            PlayerDeadBody? npcBody = npc.orig_Die(direction, true, false);
            if (npcBody is null)
            {
                Logger.Warn(
                    "npcPlayer",
                    $"Death link: original death rejected npcPlayer '{npc.NpcId}' " +
                    $"(dead={npc.Dead}, state={npc.StateMachine.State}, scene={npc.Scene?.GetType().Name ?? "null"}).");
                continue;
            }

            MarkNpcDeathBody(npcBody);
            killed++;
        }
        if (killed == 0)
            Logger.Warn(
                "npcPlayer",
                $"Death link: captured {synchronizedNpcs.Count} eligible npcPlayer instance(s), but no death body was created.");
        else
            Logger.Info("npcPlayer", $"Death link: the real Player created {killed} npcPlayer death body/bodies.");
    }

    private static Vector2 GetSynchronizedDeathDirection(Vector2 initiatingDirection)
        // Vector2.Zero is Celeste's immediate-death contract (for example,
        // falling out of bounds). Only the initiator may preserve that form;
        // its synchronized counterpart must run the normal death sequence.
        => initiatingDirection == Vector2.Zero ? -Vector2.UnitY : initiatingDirection;

    private static Hook HookInputGetter(string propertyName, HookInputCheck hook)
    {
        MethodInfo? getter = typeof(global::Celeste.Input).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
        if (getter is null)
            throw new MissingMethodException(typeof(global::Celeste.Input).FullName, "get_" + propertyName);
        return new Hook(getter, hook);
    }

    private static Hook HookStaticBoolSetter(Type owner, string propertyName, HookSetBool hook)
    {
        MethodInfo? setter = owner.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static)?.GetSetMethod();
        if (setter is null)
            throw new MissingMethodException(owner.FullName, "set_" + propertyName);
        return new Hook(setter, hook);
    }

    private static Player? FindRealPlayer(Level level)
    {
        foreach (Monocle.Entity entity in level.Tracker.GetEntities<Player>())
        {
            if (entity is Player player && player is not NpcPlayerEntity)
                return player;
        }
        return null;
    }
}
