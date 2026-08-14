using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Globalization;

namespace Celeste.Mod.NpcPlayer.Entities;

[Tracked]
[CustomEntity("npcPlayer/npcPlayerSpawnPoint")]
public sealed class NpcPlayerSpawnPoint : Entity
{
    public string NpcId { get; }
    public bool IsDefault { get; }
    public EntityID EntityId { get; }
    public string StableId => EntityId.ID.ToString(CultureInfo.InvariantCulture);

    public NpcPlayerSpawnPoint(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset)
    {
        NpcId = data.Attr("npcId", "npc").Trim();
        if (NpcId.Length == 0)
            NpcId = "npc";

        IsDefault = data.Bool("default", false);
        EntityId = entityId;
        Active = false;
        Visible = false;
        Collidable = false;
    }
}