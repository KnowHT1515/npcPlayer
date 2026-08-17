local trigger = {}
local npcPlayerFields = require("mods").requireFromPlugin("libraries.npc_player_fields")

trigger.name = "npcPlayer/changeNpcPlayerRespawn"
trigger.nodeLimits = {1, 1}
trigger.nodeLineRenderType = "line"
trigger.fieldInformation = {
    npcId = npcPlayerFields.npcIdField()
}
trigger.placements = {
    {
        name = "change_npc_player_respawn",
        data = {
            npcId = ""
        }
    }
}

return trigger
