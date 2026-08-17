using Celeste.Mod.Entities;
using Celeste.Mod.NpcPlayer.Config;
using Celeste.Mod.NpcPlayer.Input;
using Celeste.Mod.NpcPlayer.Runtime;
using Celeste.Mod.NpcPlayer.Tas;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.NpcPlayer.Entities;

/// <summary>
/// Runtime Player actor. Maps should place npcPlayer/npcPlayerSpawnPoint instead.
/// </summary>
[CustomEntity("npcPlayer")]
public sealed class NpcPlayer : Player
{
    private static readonly FieldInfo? PlayerSpriteNameField = typeof(PlayerSprite).GetField(
        "spriteName",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? PlayerSpriteModeField = typeof(PlayerSprite).GetField(
        "<Mode>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly NpcInputDevice input = new();
    private readonly NpcTasRunner runner = new();

    public string NpcId { get; }
    public bool LeadByPlayer { get; }
    public bool LeadToPlayer { get; }
    internal NpcInputDevice InputDevice => input;
    internal HashSet<Trigger> NpcTriggersInside { get; } = new();
    internal List<NpcTriggerRouter.VanillaTriggerState> VanillaTriggerStates { get; } = new();

    public NpcPlayer(EntityData data, Vector2 offset)
        : this(
            data.Position + offset,
            NormalizeNpcId(data.Attr("npcId", "npc")),
            PlayerSpriteMode.MadelineAsBadeline,
            NpcDeathLink.Linked)
    {
    }

    internal NpcPlayer(
        Vector2 position,
        string npcId,
        PlayerSpriteMode spriteMode,
        NpcDeathLink deathLink)
        : base(position, spriteMode)
    {
        NpcId = NormalizeNpcId(npcId);
        LeadByPlayer = deathLink.LeadByPlayer;
        LeadToPlayer = deathLink.LeadToPlayer;
        if (spriteMode is PlayerSpriteMode.Madeline or PlayerSpriteMode.MadelineAsBadeline)
            ApplyBuiltInSpriteMode(spriteMode);

        // Player is persistent by default, but runtime npcPlayers belong to one
        // room and must be recreated from that room's spawn-point data.
        Tag &= ~Tags.Persistent;
        IntroType = IntroTypes.None;
        OverrideIntroType = IntroTypes.None;
        EnforceLevelBounds = false;
        ForceCameraUpdate = false;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        NpcPlayerRegistry.Add(this);
    }

    public override void Removed(Scene scene)
    {
        NpcTriggerRouter.Remove(this);
        StopTas();
        if (Holding is not null)
            Drop();
        NpcPlayerRegistry.Remove(this, scene);
        base.Removed(scene);
    }

    public override void Update()
    {
        input.Advance(runner.Advance());

        Level? level = Scene as Level;
        Vector2 cameraPosition = level?.Camera.Position ?? default;
        using (NpcInputContext.Push(this, input))
        {
            try
            {
                NpcTriggerRouter.SuppressVanilla(this);
                base.Update();
            }
            finally
            {
                // The inherited Player update owns global Trigger and camera state.
                // NPC simulation may observe those systems but must not retain them.
                NpcTriggerRouter.RestoreVanilla(this);
                if (level is not null)
                    level.Camera.Position = cameraPosition;
            }

            if (!Dead && Scene is not null)
                NpcTriggerRouter.Update(this);

            if (!Dead && level is not null && Top > level.Bounds.Bottom + 4f)
            {
                // NPCs cannot own room transitions, but falling below the room
                // must still participate in the shared player-death contract.
                Die(Vector2.Zero, false, true);
            }
        }
    }

    public bool PlayTas(ModContent sourceMod, string tasPath)
    {
        if (!NpcTasProgram.TryLoad(sourceMod, tasPath, out NpcTasProgram? program) || program is null)
        {
            StopTas();
            return false;
        }

        runner.Play(program);
        return true;
    }

    public void StopTas()
    {
        runner.Stop();
        input.Reset();
    }

    private static string NormalizeNpcId(string npcId)
    {
        npcId = npcId.Trim();
        return npcId.Length == 0 ? "npc" : npcId;
    }

    private void ApplyBuiltInSpriteMode(PlayerSpriteMode mode)
    {
        if (!NpcSkinResolver.TryResetBuiltInSkinMetadata(Sprite, Hair))
            return;

        string spriteName = mode == PlayerSpriteMode.Madeline ? "player" : "player_badeline";
        if (!GFX.SpriteBank.SpriteData.TryGetValue(spriteName, out SpriteData? spriteData))
        {
            Logger.Warn("npcPlayer", $"Built-in player sprite '{spriteName}' was not found.");
            return;
        }

        // SkinModHelper deliberately maps the vanilla Madeline modes to the
        // currently selected player skin. Built-in npcPlayer variants instead
        // mean the original Celeste sprites, so initialize from the underlying
        // vanilla SpriteData without going through SpriteBank.CreateOn hooks.
        spriteData.CreateOn(Sprite);
        PlayerSpriteModeField?.SetValue(Sprite, mode);
        PlayerSpriteNameField?.SetValue(Sprite, spriteName);
        DefaultSpriteMode = mode;
        Hair.Color = mode == PlayerSpriteMode.MadelineAsBadeline
            ? NormalBadelineHairColor
            : NormalHairColor;
    }
}
