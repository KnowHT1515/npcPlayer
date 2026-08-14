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
    private static bool loaded;
    private static bool coordinatingDeath;
    private static ConditionalWeakTable<PlayerDeadBody, object> visualNpcDeathBodies = new();
    private static readonly object VisualNpcDeathBodyMarker = new();
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
        visualNpcDeathBodies = new ConditionalWeakTable<PlayerDeadBody, object>();
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
            List<NpcPlayerEntity>? synchronizedNpcs = level is not null && !coordinatingDeath
                ? NpcPlayerRegistry.SnapshotLivePlayers(level)
                : null;
            PlayerDeadBody? body = orig(self, direction, evenIfInvincible, registerDeathInStats);
            if (body is not null && level is not null && synchronizedNpcs is not null)
            {
                Logger.Info(
                    "npcPlayer",
                    $"Death sync: captured {synchronizedNpcs.Count} live npcPlayer instance(s) before the real Player died.");
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

        if (coordinatingDeath)
        {
            MarkVisualNpcDeathBody(npcBody);
            return npcBody;
        }

        Player? realPlayer = level is null ? null : FindRealPlayer(level);
        if (realPlayer is null)
            return npcBody;
        if (realPlayer.Dead)
        {
            // A real-player death is already in flight. This NPC body belongs
            // to that same reload even if another hook killed it slightly later.
            MarkVisualNpcDeathBody(npcBody);
            return npcBody;
        }

        MarkVisualNpcDeathBody(npcBody);

        // Match TheoCrystal's ownership model: let the initiating entity finish
        // its own death call, then request an ordinary Player death outside the
        // nested Player.Die hook stack. The real body remains entirely owned by
        // Celeste/SMH+ and therefore gets exactly their normal animation and
        // particle sequence.
        level!.OnEndOfFrame += () =>
        {
            if (realPlayer.Dead || !ReferenceEquals(realPlayer.Scene, level))
                return;

            coordinatingDeath = true;
            try
            {
                Vector2 playerDeathDirection = GetSynchronizedDeathDirection(direction);
                PlayerDeadBody? realBody = realPlayer.Die(playerDeathDirection, false, registerDeathInStats);
                if (realBody is not null)
                {
                    Logger.Info("npcPlayer", $"Death sync: npcPlayer '{npc.NpcId}' created the real Player death body at end of frame.");
                }
                else
                    Logger.Warn("npcPlayer", $"Death sync: npcPlayer '{npc.NpcId}' died, but the end-of-frame real Player death was rejected.");
            }
            finally
            {
                coordinatingDeath = false;
            }
        };
        return npcBody;
    }

    private static void OnPlayerDeadBodyEnd(On.Celeste.PlayerDeadBody.orig_End orig, PlayerDeadBody self)
    {
        // NPC bodies are visual companions to the real Player's body. Let the
        // latter own the screen wipe and room reload so multiple bodies cannot
        // race Level.Reload or count more than one death.
        if (visualNpcDeathBodies.TryGetValue(self, out _))
            return;
        orig(self);
    }

    private static void MarkVisualNpcDeathBody(PlayerDeadBody body)
    {
        if (!visualNpcDeathBodies.TryGetValue(body, out _))
            visualNpcDeathBodies.Add(body, VisualNpcDeathBodyMarker);
    }

    private static void KillNpcSnapshot(
        List<NpcPlayerEntity> synchronizedNpcs,
        Vector2 direction)
    {
        // The snapshot was captured before the real Player's original death
        // path could mutate scene tracking. Dispatch immediately after that
        // path succeeds, while every captured NPC instance is still usable.
        bool wasCoordinating = coordinatingDeath;
        coordinatingDeath = true;
        try
        {
            int killed = 0;
            foreach (NpcPlayerEntity npc in synchronizedNpcs)
            {
                if (npc.Dead || npc.Scene is null)
                    continue;

                // A synchronized companion death must not re-enter the public
                // Player.Die hook chain. Some compatibility hooks consume the
                // nested call after the real Player is already dead and return
                // no body, leaving the NPC visually alive.
                PlayerDeadBody? npcBody = npc.orig_Die(direction, true, false);
                if (npcBody is null)
                {
                    Logger.Warn(
                        "npcPlayer",
                        $"Death sync: original death rejected npcPlayer '{npc.NpcId}' " +
                        $"(dead={npc.Dead}, state={npc.StateMachine.State}, scene={npc.Scene?.GetType().Name ?? "null"}).");
                    continue;
                }

                MarkVisualNpcDeathBody(npcBody);
                killed++;
            }
            if (killed == 0)
                Logger.Warn(
                    "npcPlayer",
                    $"Death sync: captured {synchronizedNpcs.Count} live npcPlayer instance(s), but no death body was created.");
            else
                Logger.Info("npcPlayer", $"Death sync: the real Player created {killed} npcPlayer death body/bodies.");
        }
        finally
        {
            coordinatingDeath = wasCoordinating;
        }
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
