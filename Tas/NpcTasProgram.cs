using Celeste.Mod.NpcPlayer.Input;
using Celeste.Mod.NpcPlayer.Runtime;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Celeste.Mod.NpcPlayer.Tas;

internal sealed class NpcTasProgram
{
    // These limits protect the game from a typo such as "1000000000,R" and
    // from pathological files while still allowing scripts many hours long.
    private const int MaxSegments = 100_000;
    private const long MaxTotalFrames = 10_000_000;

    internal readonly struct Segment
    {
        public int FrameCount { get; }
        public NpcInputFrame Input { get; }

        public Segment(int frameCount, NpcInputFrame input)
        {
            FrameCount = frameCount;
            Input = input;
        }
    }

    public IReadOnlyList<Segment> Segments { get; }
    public long TotalFrames { get; }

    private NpcTasProgram(IReadOnlyList<Segment> segments, long totalFrames)
    {
        Segments = segments;
        TotalFrames = totalFrames;
    }

    public static bool TryLoad(ModContent sourceMod, string path, out NpcTasProgram? program)
    {
        program = null;
        string modName = sourceMod.Mod?.Name ?? sourceMod.Name ?? "<unknown mod>";
        if (!MapModContent.TryNormalizeSafeRelativePath(path, ".tas", out string virtualPath))
        {
            Logger.Error("npcPlayer", $"Mod '{modName}' supplied unsafe or empty TAS path '{path}'.");
            return false;
        }

        if (!MapModContent.TryFindAsset(sourceMod, virtualPath, out ModAsset? asset) || asset is null)
        {
            Logger.Error("npcPlayer", $"TAS asset '{virtualPath}' was not found in mod '{modName}'.");
            return false;
        }

        try
        {
            using Stream stream = asset.Stream;
            using StreamReader reader = new(stream);
            program = Parse(reader, modName + "/" + virtualPath);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("npcPlayer", $"Failed to parse TAS asset '{virtualPath}' from mod '{modName}': {e.Message}");
            return false;
        }
    }

    private static NpcTasProgram Parse(TextReader reader, string source)
    {
        List<Segment> segments = new();
        long totalFrames = 0;
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#npcPlayer", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"{source}:{lineNumber}: segmented npcPlayer TAS syntax is no longer supported.");
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = trimmed.Split(',');
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
                throw new FormatException($"{source}:{lineNumber}: only positive direct TAS input lines are supported.");

            NpcInputFrame frame = ParseFrame(parts, source, lineNumber);
            if (segments.Count >= MaxSegments)
                throw new FormatException($"{source}:{lineNumber}: TAS exceeds the {MaxSegments:N0} segment limit.");
            if (totalFrames > MaxTotalFrames - count)
                throw new FormatException($"{source}:{lineNumber}: TAS exceeds the {MaxTotalFrames:N0} total-frame limit.");

            segments.Add(new Segment(count, frame));
            totalFrames += count;
        }

        return new NpcTasProgram(segments, totalFrames);
    }

    private static NpcInputFrame ParseFrame(string[] parts, string source, int lineNumber)
    {
        bool left = false;
        bool right = false;
        bool up = false;
        bool down = false;
        bool jump = false;
        bool dash = false;
        bool grab = false;
        bool crouchDash = false;

        for (int i = 1; i < parts.Length; i++)
        {
            string action = parts[i].Trim().ToUpperInvariant();
            switch (action)
            {
                case "": break;
                case "L": left = true; break;
                case "R": right = true; break;
                case "U": up = true; break;
                case "D": down = true; break;
                case "J":
                case "K": jump = true; break;
                case "X":
                case "C": dash = true; break;
                case "G":
                case "H": grab = true; break;
                case "Z":
                case "V": crouchDash = true; break;
                default:
                    throw new FormatException($"{source}:{lineNumber}: unsupported TAS action '{action}'.");
            }
        }

        Vector2 aim = new((right ? 1f : 0f) - (left ? 1f : 0f), (down ? 1f : 0f) - (up ? 1f : 0f));
        if (aim != Vector2.Zero)
            aim.Normalize();
        return new NpcInputFrame(aim, jump, dash, grab, crouchDash);
    }
}