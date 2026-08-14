using Celeste.Mod.Entities;
using Celeste.Mod.NpcPlayer.Runtime;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.NpcPlayer.Triggers;

[CustomEntity("npcPlayer/changeNpcPlayerRespawn")]
public sealed class NpcChangeRespawnTrigger : Trigger
{
    private readonly string npcId;
    private readonly Vector2 node;

    public NpcChangeRespawnTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        npcId = data.Attr("npcId", "npc").Trim();
        if (npcId.Length == 0)
            npcId = "npc";
        Vector2[] nodes = data.NodesOffset(offset);
        node = nodes.Length > 0 ? nodes[0] : Position;
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        if (Scene is not Level level)
            return;
        if (!NpcPlayerRegistry.ChangeRespawn(level, npcId, node))
            Logger.Warn("npcPlayer", $"Change npcPlayer Respawn found no exact Spawn Point match for npcId '{npcId}'.");
    }
}