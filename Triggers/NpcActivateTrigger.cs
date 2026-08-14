using Celeste.Mod.Entities;
using Celeste.Mod.NpcPlayer.Runtime;
using Microsoft.Xna.Framework;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Triggers;

[CustomEntity("npcPlayer/activateNpcPlayer")]
public sealed class NpcActivateTrigger : Trigger, INpcPlayerTrigger
{
    private readonly string npcId;
    private readonly string tasPath;
    private readonly bool once;
    private readonly bool playerOnly;
    private bool used;

    public NpcActivateTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        npcId = data.Attr("npcId", "npc").Trim();
        if (npcId.Length == 0)
            npcId = "npc";
        tasPath = data.Attr("tas", "Tas/action.tas").Trim();
        once = data.Bool("once", true);
        playerOnly = data.Bool("playerOnly", true);
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        if (used && once || playerOnly && player is NpcPlayerEntity || Scene is null)
            return;

        NpcPlayerEntity? npc = NpcPlayerRegistry.Find(Scene, npcId);
        ModContent? sourceMod = NpcPlayerRegistry.GetSourceMod(Scene);
        if (npc is null)
        {
            Logger.Warn("npcPlayer", $"Activate npcPlayer could not find npcId '{npcId}'.");
            return;
        }
        if (sourceMod is null)
        {
            Logger.Warn("npcPlayer", $"Activate npcPlayer could not identify the map mod for npcId '{npcId}'.");
            return;
        }

        if (npc.PlayTas(sourceMod, tasPath))
            used = true;
    }

    public bool AllowsNpcPlayer(NpcPlayerEntity player) => !playerOnly;
}
