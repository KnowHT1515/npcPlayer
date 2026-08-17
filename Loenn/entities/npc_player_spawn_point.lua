local spawnPoint = {}
local npcPlayerFields = require("mods").requireFromPlugin("libraries.npc_player_fields")

spawnPoint.name = "npcPlayer/npcPlayerSpawnPoint"
spawnPoint.depth = 0
spawnPoint.justification = {0.5, 1.0}
spawnPoint.texture = "characters/player_badeline/sleep00"
spawnPoint.fieldOrder = {"x", "y", "npcId", "default"}
spawnPoint.fieldInformation = {
    npcId = npcPlayerFields.npcIdField()
}

spawnPoint.placements = {
    {
        name = "spawn_point",
        data = {
            npcId = "",
            default = false
        }
    }
}

return spawnPoint
