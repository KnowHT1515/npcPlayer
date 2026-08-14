using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

/// <summary>
/// Opt-in contract for triggers that can safely receive npcPlayer callbacks.
/// Implementations must not retain Triggered or PlayerIsInside after a callback;
/// those vanilla single-player fields are virtualized by NpcTriggerRouter.
/// </summary>
public interface INpcPlayerTrigger
{
    bool AllowsNpcPlayer(NpcPlayerEntity player);
}
