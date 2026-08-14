using Monocle;
using System;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

internal static class NpcPlayerColliderRouter
{
    public static bool Check(
        On.Celeste.PlayerCollider.orig_Check orig,
        PlayerCollider self,
        Player player)
    {
        if (player is not NpcPlayerEntity npc)
            return orig(self, player);

        Entity? owner = self.Entity;
        if (owner is INpcPlayerCollider compatible)
            return compatible.AllowsNpcPlayer(npc) && orig(self, player);
        if (owner is null)
            return false;

        Type ownerType = owner.GetType();
        if (ownerType == typeof(CrystalStaticSpinner) || ownerType == typeof(DustStaticSpinner))
            return CheckDistanceGatedSpinner(orig, self, npc, owner);
        if (!IsVanillaNpcInteraction(ownerType))
            return false;

        return orig(self, player);
    }

    private static bool CheckDistanceGatedSpinner(
        On.Celeste.PlayerCollider.orig_Check orig,
        PlayerCollider collider,
        NpcPlayerEntity npc,
        Entity spinner)
    {
        bool wasCollidable = spinner.Collidable;
        if (!wasCollidable &&
            (Math.Abs(npc.X - spinner.X) >= 128f || Math.Abs(npc.Y - spinner.Y) >= 128f))
            return false;

        try
        {
            // Vanilla static spinners gate Collidable by distance to the one
            // tracked real Player. Apply the same range to this NPC only while
            // the original collider and callback perform their normal check.
            spinner.Collidable = true;
            return orig(collider, npc);
        }
        finally
        {
            spinner.Collidable = wasCollidable;
        }
    }

    private static bool IsVanillaNpcInteraction(Type type)
    {
        // Exact types only: a third-party subclass can add session or story
        // side effects and must explicitly opt in through INpcPlayerCollider.
        return type == typeof(AngryOshiro) ||
            type == typeof(Booster) ||
            type == typeof(Bumper) ||
            type == typeof(CrystalStaticSpinner) ||
            type == typeof(DustStaticSpinner) ||
            type == typeof(FinalBoss) ||
            type == typeof(FinalBossShot) ||
            type == typeof(FireBall) ||
            type == typeof(FireBarrier) ||
            type == typeof(FlyFeather) ||
            type == typeof(IceBlock) ||
            type == typeof(Killbox) ||
            type == typeof(Lightning) ||
            type == typeof(PlayerSeeker) ||
            type == typeof(Puffer) ||
            type == typeof(Refill) ||
            type == typeof(RisingLava) ||
            type == typeof(RotateSpinner) ||
            type == typeof(SandwichLava) ||
            type == typeof(Seeker) ||
            type == typeof(Snowball) ||
            type == typeof(Spikes) ||
            type == typeof(Spring) ||
            type == typeof(TempleBigEyeballShockwave) ||
            type == typeof(TrackSpinner) ||
            type == typeof(TriggerSpikes) ||
            type.FullName == "Celeste.Mod.Entities.TriggerSpikesOriginal";
    }
}
