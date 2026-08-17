local trigger = {}
local npcPlayerFields = require("mods").requireFromPlugin("libraries.npc_player_fields")

trigger.name = "npcPlayer/activateNpcPlayer"
trigger.fieldInformation = {
    npcId = npcPlayerFields.npcIdField(),
    tas = npcPlayerFields.tasPathField()
}
trigger.placements = {
    {
        name = "activate_npc_player",
        data = {
            npcId = "",
            tas = "",
            once = true,
            playerOnly = true
        }
    }
}

return trigger
