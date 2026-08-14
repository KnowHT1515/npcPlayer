using Celeste.Mod.NpcPlayer.Config;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;
using SpawnPoint = Celeste.Mod.NpcPlayer.Entities.NpcPlayerSpawnPoint;

namespace Celeste.Mod.NpcPlayer.Runtime;

internal static class NpcPlayerRegistry
{
    private const string SpawnPointEntityName = "npcPlayer/npcPlayerSpawnPoint";

    private sealed class RoomState
    {
        public readonly Dictionary<string, NpcPlayerEntity> Players = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<SpawnPoint>> SpawnPoints = new(StringComparer.Ordinal);
        public ModContent? SourceMod;
    }

    private static ConditionalWeakTable<Scene, RoomState> byScene = new();

    public static void Add(NpcPlayerEntity npc)
    {
        if (npc.Scene is null)
            return;
        RoomState state = State(npc.Scene);
        if (state.Players.TryGetValue(npc.NpcId, out NpcPlayerEntity? existing) && !ReferenceEquals(existing, npc))
            Logger.Warn("npcPlayer", $"Duplicate runtime npcPlayer id '{npc.NpcId}' in one room; the newest instance is used by triggers.");
        state.Players[npc.NpcId] = npc;
    }

    public static void Remove(NpcPlayerEntity npc, Scene scene)
    {
        RoomState state = State(scene);
        if (state.Players.TryGetValue(npc.NpcId, out NpcPlayerEntity? existing) && ReferenceEquals(existing, npc))
            state.Players.Remove(npc.NpcId);
    }

    public static NpcPlayerEntity? Find(Scene scene, string npcId)
        => State(scene).Players.TryGetValue(npcId, out NpcPlayerEntity? npc) ? npc : null;

    public static ModContent? GetSourceMod(Scene scene) => State(scene).SourceMod;
    public static NpcPlayerEntity? FindRider(Solid solid)
    {
        if (solid.Scene is null)
            return null;

        foreach (NpcPlayerEntity npc in State(solid.Scene).Players.Values)
        {
            if (ReferenceEquals(npc.Scene, solid.Scene) && !npc.Dead && npc.IsRiding(solid))
                return npc;
        }
        return null;
    }

    public static NpcPlayerEntity? FindOnTop(Solid solid)
    {
        if (solid.Scene is null)
            return null;

        Vector2 checkAt = solid.Position - Vector2.UnitY;
        foreach (NpcPlayerEntity npc in State(solid.Scene).Players.Values)
        {
            if (ReferenceEquals(npc.Scene, solid.Scene) && !npc.Dead && solid.CollideCheck(npc, checkAt))
                return npc;
        }
        return null;
    }

    public static NpcPlayerEntity? FindClimbing(Solid solid)
    {
        if (solid.Scene is null)
            return null;

        foreach (NpcPlayerEntity npc in State(solid.Scene).Players.Values)
        {
            if (!ReferenceEquals(npc.Scene, solid.Scene) || npc.Dead || npc.StateMachine.State != Player.StClimb)
                continue;

            Vector2 checkAt = solid.Position + Vector2.UnitX * (npc.Facing == Facings.Left ? 1f : -1f);
            if (solid.CollideCheck(npc, checkAt))
                return npc;
        }
        return null;
    }

    public static NpcPlayerEntity? FindRider(JumpThru jumpThru)
    {
        if (jumpThru.Scene is null)
            return null;

        foreach (NpcPlayerEntity npc in State(jumpThru.Scene).Players.Values)
        {
            if (ReferenceEquals(npc.Scene, jumpThru.Scene) && !npc.Dead && npc.IsRiding(jumpThru))
                return npc;
        }
        return null;
    }

    public static void RemoveAll(Level level)
    {
        // Copy first: RemoveSelf mutates the scene's entity lists.
        List<NpcPlayerEntity> players = new();
        // Player is [Tracked(false)], so subclasses such as NpcPlayer are not
        // present in Tracker<Player>. Actor is inherited-tracked and is the
        // correct fallback for recovering every runtime NPC, including orphans.
        foreach (Entity entity in level.Tracker.GetEntities<Actor>())
        {
            if (entity is NpcPlayerEntity npc)
                players.Add(npc);
        }

        foreach (NpcPlayerEntity npc in players)
        {
            npc.StopTas();
            // Also handles instances created by older builds, whose Player
            // constructor left the Persistent tag enabled.
            npc.Tag &= ~Tags.Persistent;
            npc.RemoveSelf();
        }

        State(level).Players.Clear();
    }

    public static List<NpcPlayerEntity> SnapshotLivePlayers(Level level)
    {
        // Combine the explicit registry with Actor tracking. The registry is
        // authoritative during normal play; Actor tracking also recovers an
        // NPC that was added by another mod or was not registered before the
        // real Player died.
        HashSet<NpcPlayerEntity> players = new();
        foreach (NpcPlayerEntity npc in State(level).Players.Values)
        {
            if (ReferenceEquals(npc.Scene, level) && !npc.Dead)
                players.Add(npc);
        }

        foreach (Entity entity in level.Tracker.GetEntities<Actor>())
        {
            if (entity is NpcPlayerEntity npc && ReferenceEquals(npc.Scene, level) && !npc.Dead)
                players.Add(npc);
        }

        return new List<NpcPlayerEntity>(players);
    }

    public static void InitializeRoom(Level level)
    {
        RoomState state = State(level);
        state.Players.Clear();
        state.SpawnPoints.Clear();
        state.SourceMod = MapModContent.Find(level.Session.MapData);

        string roomKey = level.Session.Area.SID + "\n" + level.Session.Level;
        if (!string.Equals(NpcPlayerModule.Session.ActiveRoom, roomKey, StringComparison.Ordinal))
        {
            NpcPlayerModule.Session.ActiveRoom = roomKey;
            NpcPlayerModule.Session.RespawnPoints.Clear();
        }

        CollectSpawnPointsInMapOrder(level, state);
        foreach (KeyValuePair<string, List<SpawnPoint>> pair in state.SpawnPoints)
        {
            SpawnPoint selected = SelectInitialSpawn(pair.Key, pair.Value);
            string variant = NpcVariantConfig.Resolve(state.SourceMod, pair.Key);
            PlayerSpriteMode mode = NpcSkinResolver.ResolveSpriteMode(variant);
            level.Add(new NpcPlayerEntity(selected.Position, pair.Key, mode));
        }
    }

    public static bool ChangeRespawn(Level level, string npcId, Vector2 node)
    {
        if (!State(level).SpawnPoints.TryGetValue(npcId, out List<SpawnPoint>? points) || points.Count == 0)
            return false;

        SpawnPoint nearest = points[0];
        float nearestDistance = Vector2.DistanceSquared(node, nearest.Position);
        for (int i = 1; i < points.Count; i++)
        {
            float distance = Vector2.DistanceSquared(node, points[i].Position);
            if (distance < nearestDistance)
            {
                nearest = points[i];
                nearestDistance = distance;
            }
        }

        NpcPlayerModule.Session.RespawnPoints[npcId] = nearest.StableId;
        return true;
    }

    public static void Clear()
    {
        byScene = new ConditionalWeakTable<Scene, RoomState>();
        NpcVariantConfig.Clear();
        NpcSkinResolver.Clear();
    }

    private static void CollectSpawnPointsInMapOrder(Level level, RoomState state)
    {
        string currentRoom = level.Session.LevelData.Name;
        Dictionary<EntityID, SpawnPoint> trackedById = new();
        foreach (Entity entity in level.Tracker.GetEntities<SpawnPoint>())
        {
            if (entity is not SpawnPoint point ||
                !string.Equals(point.EntityId.Level, currentRoom, StringComparison.Ordinal))
                continue;

            // During a room transition, Celeste loads the destination before
            // unloading the source room's entity snapshot. Match its complete
            // EntityID contract so source-room points cannot seed the new room.
            trackedById[point.EntityId] = point;
        }

        foreach (EntityData data in level.Session.LevelData.Entities)
        {
            EntityID entityId = new(currentRoom, data.ID);
            if (!string.Equals(data.Name, SpawnPointEntityName, StringComparison.Ordinal) ||
                !trackedById.Remove(entityId, out SpawnPoint? point))
                continue;
            AddSpawnPoint(state, point);
        }

        if (trackedById.Count == 0)
            return;

        List<SpawnPoint> unlisted = new(trackedById.Values);
        unlisted.Sort(static (a, b) => a.EntityId.ID.CompareTo(b.EntityId.ID));
        foreach (SpawnPoint point in unlisted)
            AddSpawnPoint(state, point);
    }

    private static void AddSpawnPoint(RoomState state, SpawnPoint point)
    {
        if (!state.SpawnPoints.TryGetValue(point.NpcId, out List<SpawnPoint>? points))
        {
            points = new List<SpawnPoint>();
            state.SpawnPoints[point.NpcId] = points;
        }
        points.Add(point);
    }

    private static SpawnPoint SelectInitialSpawn(string npcId, List<SpawnPoint> points)
    {
        if (NpcPlayerModule.Session.RespawnPoints.TryGetValue(npcId, out string? selectedId))
        {
            SpawnPoint? persisted = points.Find(point => string.Equals(point.StableId, selectedId, StringComparison.Ordinal));
            if (persisted is not null)
                return persisted;
            NpcPlayerModule.Session.RespawnPoints.Remove(npcId);
        }

        SpawnPoint selected = points[0];
        foreach (SpawnPoint point in points)
        {
            if (point.IsDefault)
            {
                selected = point;
                break;
            }
        }

        NpcPlayerModule.Session.RespawnPoints[npcId] = selected.StableId;
        return selected;
    }

    private static RoomState State(Scene scene) => byScene.GetOrCreateValue(scene);
}
