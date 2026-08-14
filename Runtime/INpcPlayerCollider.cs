using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

/// <summary>
/// Opt-in contract for third-party entities whose PlayerCollider callback is
/// safe and meaningful when the Player instance is an npcPlayer.
/// </summary>
public interface INpcPlayerCollider
{
    bool AllowsNpcPlayer(NpcPlayerEntity player);
}
