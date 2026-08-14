local spawnPoint = {}

spawnPoint.name = "npcPlayer/npcPlayerSpawnPoint"
spawnPoint.depth = 0
spawnPoint.justification = {0.5, 1.0}
spawnPoint.texture = "characters/player_badeline/sleep00"
spawnPoint.fieldOrder = {"x", "y", "npcId", "default"}

spawnPoint.placements = {
    {
        name = "spawn_point",
        data = {
            npcId = "npc",
            default = false
        }
    }
}

return spawnPoint