using System;
using System.Collections.Generic;

namespace Celeste.Mod.NpcPlayer.Runtime;

internal static class MapModContent
{
    public static ModContent? Find(MapData map)
    {
        string wanted = Normalize("Maps/" + map.Filename);
        foreach (ModContent content in Everest.Content.Mods)
        {
            foreach (KeyValuePair<string, ModAsset> pair in content.Map)
            {
                if (string.Equals(WithoutExtension(Normalize(pair.Key)), WithoutExtension(wanted), StringComparison.OrdinalIgnoreCase))
                    return content;
            }
        }

        Logger.Error("npcPlayer", $"Could not locate the source mod for map '{map.Filename}'.");
        return null;
    }

    public static bool TryFindAsset(ModContent content, string relativePath, out ModAsset? asset)
    {
        string wanted = Normalize(relativePath);
        string wantedWithoutExtension = WithoutExtension(wanted);

        if (content.Map.TryGetValue(wanted, out asset) && asset is not null)
            return true;
        if (content.Map.TryGetValue(wantedWithoutExtension, out asset) && asset is not null)
            return true;

        foreach (KeyValuePair<string, ModAsset> pair in content.Map)
        {
            string key = Normalize(pair.Key);
            if (string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(WithoutExtension(key), wantedWithoutExtension, StringComparison.OrdinalIgnoreCase))
            {
                asset = pair.Value;
                return asset is not null;
            }
        }

        asset = null;
        return false;
    }

    public static bool TryNormalizeSafeRelativePath(string path, string requiredExtension, out string normalized)
    {
        string raw = path.Replace('\\', '/').Trim();
        normalized = Normalize(raw);
        if (raw.Length == 0 || raw.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains(":", StringComparison.Ordinal))
            return false;

        string[] parts = normalized.Split('/');
        foreach (string part in parts)
        {
            if (part.Length == 0 || part == "." || part == "..")
                return false;
        }

        if (!normalized.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            normalized += requiredExtension;
        return true;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim().TrimStart('/');

    private static string WithoutExtension(string path)
    {
        int slash = path.LastIndexOf('/');
        int dot = path.LastIndexOf('.');
        return dot > slash ? path.Substring(0, dot) : path;
    }
}