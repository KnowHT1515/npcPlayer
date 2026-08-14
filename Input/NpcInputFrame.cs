using Microsoft.Xna.Framework;

namespace Celeste.Mod.NpcPlayer.Input;

internal readonly struct NpcInputFrame
{
    public static readonly NpcInputFrame Empty = new(Vector2.Zero, false, false, false, false);

    public Vector2 Aim { get; }
    public bool Jump { get; }
    public bool Dash { get; }
    public bool Grab { get; }
    public bool CrouchDash { get; }

    public NpcInputFrame(Vector2 aim, bool jump, bool dash, bool grab, bool crouchDash)
    {
        Aim = aim;
        Jump = jump;
        Dash = dash;
        Grab = grab;
        CrouchDash = crouchDash;
    }
}
