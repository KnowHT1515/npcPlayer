local trigger = {}

trigger.name = "npcPlayer/activateNpcPlayer"
trigger.placements = {
    {
        name = "activate_npc_player",
        data = {
            npcId = "npc",
            tas = "Tas/Author/Map/action.tas",
            once = true,
            playerOnly = true
        }
    }
}

return trigger