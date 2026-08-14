using Celeste.Mod.NpcPlayer.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.NpcPlayer.Config;

internal static class NpcVariantConfig
{
    private const string ConfigPath = "config/npcPlayer/npcPlayer.yaml";
    private static ConditionalWeakTable<ModContent, Dictionary<string, string>> cache = new();

    public static string Resolve(ModContent? sourceMod, string npcId)
    {
        if (sourceMod is null)
            return "badeline";
        return Load(sourceMod).TryGetValue(npcId, out string? variant) ? variant : "badeline";
    }

    public static void Clear() => cache = new ConditionalWeakTable<ModContent, Dictionary<string, string>>();

    public static void OnContentUpdate(ModAsset? from, ModAsset? to)
    {
        ModAsset? asset = to ?? from;
        if (asset?.Source is null || !IsConfigAsset(asset.PathVirtual))
            return;

        cache.Remove(asset.Source);
        string modName = asset.Source.Mod?.Name ?? asset.Source.Name ?? "<unknown mod>";
        Logger.Info("npcPlayer", $"Reloaded npcPlayer variant config for mod '{modName}'. Changes apply when the room is next loaded.");
    }

    private static Dictionary<string, string> Load(ModContent sourceMod)
    {
        if (cache.TryGetValue(sourceMod, out Dictionary<string, string>? variants))
            return variants;

        variants = new Dictionary<string, string>(StringComparer.Ordinal);
        cache.Add(sourceMod, variants);
        if (!MapModContent.TryFindAsset(sourceMod, ConfigPath, out ModAsset? asset) || asset is null)
            return variants;

        string modName = sourceMod.Mod?.Name ?? sourceMod.Name ?? "<unknown mod>";
        if (!asset.TryDeserialize<List<Dictionary<object, object>>>(out List<Dictionary<object, object>>? root) || root is null)
        {
            Logger.Error("npcPlayer", $"Failed to parse '{ConfigPath}' from mod '{modName}'. All npcPlayers will use badeline.");
            return variants;
        }

        for (int i = 0; i < root.Count; i++)
        {
            if (!TryGet(root[i], "npcPlayer", out object? entryNode) || !TryMap(entryNode, out IDictionary<object, object>? entry) ||
                !TryGetString(entry, "npcId", out string npcId) || !TryGetString(entry, "variant", out string variant))
            {
                Logger.Warn("npcPlayer", $"{modName}/{ConfigPath}: entry {i + 1} is invalid and was skipped.");
                continue;
            }

            if (variants.ContainsKey(npcId))
            {
                Logger.Warn("npcPlayer", $"{modName}/{ConfigPath}: duplicate npcId '{npcId}' was ignored; the first entry wins.");
                continue;
            }
            variants[npcId] = variant;
        }

        return variants;
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