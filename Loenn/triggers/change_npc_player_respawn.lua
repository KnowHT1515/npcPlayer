local trigger = {}

trigger.name = "npcPlayer/changeNpcPlayerRespawn"
trigger.nodeLimits = {1, 1}
trigger.nodeLineRenderType = "line"
trigger.placements = {
    {
        name = "change_npc_player_respawn",
        data = {
            npcId = "npc"
        }
    }
}

return trigger