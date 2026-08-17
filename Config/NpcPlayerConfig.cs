using Celeste.Mod.NpcPlayer.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.NpcPlayer.Config;

internal readonly record struct NpcDeathLink(bool LeadByPlayer, bool LeadToPlayer)
{
    public static NpcDeathLink Linked { get; } = new(true, true);
}

internal sealed record NpcPlayerConfig(string Variant, NpcDeathLink DeathLink)
{
    public static NpcPlayerConfig Default { get; } = new("badeline", NpcDeathLink.Linked);
}

internal static class NpcPlayerConfigLoader
{
    private const string ConfigPath = "config/npcPlayer/npcPlayer.yaml";
    private static ConditionalWeakTable<ModContent, Dictionary<string, NpcPlayerConfig>> cache = new();

    public static NpcPlayerConfig Resolve(ModContent? sourceMod, string npcId)
    {
        if (sourceMod is null)
            return NpcPlayerConfig.Default;
        return Load(sourceMod).TryGetValue(npcId, out NpcPlayerConfig? config)
            ? config
            : NpcPlayerConfig.Default;
    }

    public static void Clear()
        => cache = new ConditionalWeakTable<ModContent, Dictionary<string, NpcPlayerConfig>>();

    public static void OnContentUpdate(ModAsset? from, ModAsset? to)
    {
        ModAsset? asset = to ?? from;
        if (asset?.Source is null || !IsConfigAsset(asset.PathVirtual))
            return;

        cache.Remove(asset.Source);
        string modName = asset.Source.Mod?.Name ?? asset.Source.Name ?? "<unknown mod>";
        Logger.Info("npcPlayer", $"Reloaded npcPlayer config for mod '{modName}'. Changes apply when the room is next loaded.");
    }

    private static Dictionary<string, NpcPlayerConfig> Load(ModContent sourceMod)
    {
        if (cache.TryGetValue(sourceMod, out Dictionary<string, NpcPlayerConfig>? configs))
            return configs;

        configs = new Dictionary<string, NpcPlayerConfig>(StringComparer.Ordinal);
        cache.Add(sourceMod, configs);
        if (!MapModContent.TryFindAsset(sourceMod, ConfigPath, out ModAsset? asset) || asset is null)
            return configs;

        string modName = sourceMod.Mod?.Name ?? sourceMod.Name ?? "<unknown mod>";
        if (TryLoadCurrentFormat(asset, out List<Dictionary<object, object>>? currentEntries))
        {
            ParseEntries(currentEntries, configs, modName);
            return configs;
        }

        if (asset.TryDeserialize<List<Dictionary<object, object>>>(out List<Dictionary<object, object>>? legacyRoot) &&
            legacyRoot is not null)
        {
            List<Dictionary<object, object>> legacyEntries = new(legacyRoot.Count);
            for (int i = 0; i < legacyRoot.Count; i++)
            {
                if (!TryGet(legacyRoot[i], "npcPlayer", out object? entryNode) ||
                    !TryMap(entryNode, out IDictionary<object, object>? entry))
                {
                    legacyEntries.Add(new Dictionary<object, object>());
                    continue;
                }

                legacyEntries.Add(new Dictionary<object, object>(entry));
            }

            ParseEntries(legacyEntries, configs, modName);
            return configs;
        }

        Logger.Error("npcPlayer", $"Failed to parse '{ConfigPath}' from mod '{modName}'. All npcPlayers will use default settings.");
        return configs;
    }

    private static bool TryLoadCurrentFormat(
        ModAsset asset,
        [NotNullWhen(true)] out List<Dictionary<object, object>>? entries)
    {
        entries = null;
        if (!asset.TryDeserialize<Dictionary<object, object>>(out Dictionary<object, object>? root) ||
            root is null ||
            !TryGet(root, "npcPlayer", out object? entriesNode) ||
            entriesNode is not IEnumerable<object> sequence)
            return false;

        entries = new List<Dictionary<object, object>>();
        foreach (object? entryNode in sequence)
        {
            if (TryMap(entryNode, out IDictionary<object, object>? entry))
                entries.Add(new Dictionary<object, object>(entry));
            else
                entries.Add(new Dictionary<object, object>());
        }
        return true;
    }

    private static void ParseEntries(
        List<Dictionary<object, object>> entries,
        Dictionary<string, NpcPlayerConfig> configs,
        string modName)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            IDictionary<object, object> entry = entries[i];
            if (!TryGetString(entry, "npcId", out string npcId) ||
                !TryGetString(entry, "variant", out string variant))
            {
                Logger.Warn("npcPlayer", $"{modName}/{ConfigPath}: entry {i + 1} is invalid and was skipped.");
                continue;
            }

            if (configs.ContainsKey(npcId))
            {
                Logger.Warn("npcPlayer", $"{modName}/{ConfigPath}: duplicate npcId '{npcId}' was ignored; the first entry wins.");
                continue;
            }

            NpcDeathLink deathLink = ParseDeathLink(entry, modName, npcId);
            configs[npcId] = new NpcPlayerConfig(variant, deathLink);
        }
    }

    private static NpcDeathLink ParseDeathLink(
        IDictionary<object, object> entry,
        string modName,
        string npcId)
    {
        if (!TryGet(entry, "death_link", out object? deathLinkNode))
            return NpcDeathLink.Linked;

        if (!TryMap(deathLinkNode, out IDictionary<object, object>? deathLink))
        {
            Logger.Warn(
                "npcPlayer",
                $"{modName}/{ConfigPath}: npcId '{npcId}' has an invalid death_link; both links default to true.");
            return NpcDeathLink.Linked;
        }

        bool leadByPlayer = ReadBoolean(deathLink, "lead_by_player", modName, npcId);
        bool leadToPlayer = ReadBoolean(deathLink, "lead_to_player", modName, npcId);
        return new NpcDeathLink(leadByPlayer, leadToPlayer);
    }

    private static bool ReadBoolean(
        IDictionary<object, object> map,
        string key,
        string modName,
        string npcId)
    {
        if (!TryGet(map, key, out object? node))
            return true;
        if (node is bool value)
            return value;
        if (node is string text && bool.TryParse(text.Trim(), out value))
            return value;

        Logger.Warn(
            "npcPlayer",
            $"{modName}/{ConfigPath}: npcId '{npcId}' has an invalid {key}; it defaults to true.");
        return true;
    }

    private static bool TryGet(IDictionary<object, object> map, string key, out object? value)
    {
        foreach (KeyValuePair<object, object> pair in map)
        {
            if (pair.Key is string text && string.Equals(text, key, StringComparison.Ordinal))
            {
                value = pair.Value;
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool TryGetString(IDictionary<object, object> map, string key, out string value)
    {
        value = "";
        if (!TryGet(map, key, out object? node) || node is null)
            return false;
        value = node.ToString()?.Trim() ?? "";
        return value.Length > 0;
    }

    private static bool TryMap(object? node, [NotNullWhen(true)] out IDictionary<object, object>? map)
    {
        map = node as IDictionary<object, object>;
        return map is not null;
    }

    private static bool IsConfigAsset(string path)
    {
        path = path.Replace('\\', '/').Trim().TrimStart('/');
        return string.Equals(path, ConfigPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, ConfigPath.Substring(0, ConfigPath.Length - ".yaml".Length), StringComparison.OrdinalIgnoreCase);
    }
}
