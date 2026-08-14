using Celeste.Mod.NpcPlayer.Input;
using System.Collections.Generic;

namespace Celeste.Mod.NpcPlayer.Tas;

internal sealed class NpcTasRunner
{
    private IReadOnlyList<NpcTasProgram.Segment>? segments;
    private int segmentIndex;
    private int remainingFrames;

    public bool IsPlaying => segments is not null;

    public void Play(NpcTasProgram program)
    {
        segments = program.Segments;
        segmentIndex = 0;
        remainingFrames = 0;
    }

    public NpcInputFrame Advance()
    {
        if (segments is null || segmentIndex >= segments.Count)
        {
            Stop();
            return NpcInputFrame.Empty;
        }

        NpcTasProgram.Segment segment = segments[segmentIndex];
        if (remainingFrames == 0)
            remainingFrames = segment.FrameCount;

        remainingFrames--;
        NpcInputFrame input = segment.Input;
        if (remainingFrames == 0)
        {
            segmentIndex++;
            if (segmentIndex >= segments.Count)
                segments = null;
        }
        return input;
    }

    public void Stop()
    {
        segments = null;
        segmentIndex = 0;
        remainingFrames = 0;
    }
}