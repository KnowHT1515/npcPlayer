local loadedState = require("loaded_state")
local mods = require("mods")
local utils = require("utils")

local fields = {}

local npcObjectNames = {
    ["npcPlayer/npcPlayerSpawnPoint"] = true,
    ["npcPlayer/activateNpcPlayer"] = true,
    ["npcPlayer/changeNpcPlayerRespawn"] = true
}
local isWindows = string.lower(utils.getOS()) == "windows"

local function sortedKeys(lookup)
    local result = {}

    for value, _ in pairs(lookup) do
        table.insert(result, value)
    end

    table.sort(result, function(a, b)
        local lowerA = string.lower(a)
        local lowerB = string.lower(b)

        if lowerA == lowerB then
            return a < b
        end

        return lowerA < lowerB
    end)

    return result
end

local function normalizeTasPath(value)
    return utils.trim(tostring(value or "")):gsub("\\", "/")
end

local function displayTasPath(value)
    local normalized = normalizeTasPath(value)

    if isWindows then
        return normalized:gsub("/", "\\")
    end

    return normalized
end

function fields.getNpcIds()
    local ids = {}
    local map = loadedState.map

    if map and map.rooms then
        for _, room in pairs(map.rooms) do
            for _, items in ipairs({room.entities or {}, room.triggers or {}}) do
                for _, item in pairs(items) do
                    if npcObjectNames[item._name] then
                        local npcId = utils.trim(tostring(item.npcId or ""))

                        if npcId ~= "" then
                            ids[npcId] = true
                        end
                    end
                end
            end
        end
    end

    return sortedKeys(ids)
end

function fields.getTasPaths()
    local filename = loadedState.filename
    local modRoot = mods.getFilenameModPath(filename)
    local modMetadata = modRoot and mods.getModMetadataFromPath(modRoot)
    local modFolderName = modMetadata and modMetadata._folderName
    local mountPoint = modMetadata and modMetadata._mountPoint

    if not filename or not modFolderName or not mountPoint then
        return {}
    end

    local unixFilename = utils.convertToUnixPath(filename)
    local mapName = utils.filename(utils.stripExtension(unixFilename), "/")

    if not mapName or mapName == "" then
        return {}
    end

    -- Lönn mounts each mod into its virtual filesystem. Its recursive file
    -- enumeration cannot traverse the real absolute path returned for a map.
    local filenames = mods.findModFolderFiletype(modFolderName, {}, "Tas") or {}
    local unixMountPoint = utils.convertToUnixPath(mountPoint):gsub("/+$", "")
    local rootPrefix = string.lower(unixMountPoint .. "/")
    local wantedDirectory = string.lower(mapName)
    local added = {}
    local paths = {}

    for _, candidate in ipairs(filenames) do
        local unixCandidate = utils.convertToUnixPath(candidate)
        local extension = string.lower(utils.fileExtension(unixCandidate) or "")

        if extension == "tas" and
            string.sub(string.lower(unixCandidate), 1, #rootPrefix) == rootPrefix then
            local relative = string.sub(unixCandidate, #rootPrefix + 1)
            local directory = utils.dirname(relative, "/")
            local directoryName = directory and utils.filename(directory:gsub("/$", ""), "/")

            if utils.startsWith(string.lower(relative), "tas/") and
                directoryName and string.lower(directoryName) == wantedDirectory and
                not added[relative] then
                added[relative] = true
                table.insert(paths, relative)
            end
        end
    end

    table.sort(paths, function(a, b)
        local lowerA = string.lower(a)
        local lowerB = string.lower(b)

        if lowerA == lowerB then
            return a < b
        end

        return lowerA < lowerB
    end)

    local options = {}
    for _, path in ipairs(paths) do
        table.insert(options, {displayTasPath(path), path})
    end

    return options
end

function fields.npcIdField()
    return {
        fieldType = "string",
        options = fields.getNpcIds,
        editable = true,
        searchable = true,
        allowEmpty = true
    }
end

function fields.tasPathField()
    return {
        fieldType = "string",
        options = fields.getTasPaths,
        editable = true,
        searchable = true,
        allowEmpty = true,
        displayTransformer = displayTasPath,
        valueTransformer = normalizeTasPath
    }
end

return fields
