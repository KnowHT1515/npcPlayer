using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.NpcPlayer.Input;

internal sealed class NpcInputDevice
{
    private sealed class ButtonNode : VirtualButton.Node
    {
        private bool current;
        private bool previous;

        public override bool Check => current;
        public override bool Pressed => current && !previous;
        public override bool Released => !current && previous;

        public void Set(bool value)
        {
            previous = current;
            current = value;
        }

        public void Clear()
        {
            current = false;
            previous = false;
        }
    }

    private sealed class AxisNode : VirtualAxis.Node
    {
        public float Current;
        public override float Value => Current;
    }

    private sealed class JoystickNode : VirtualJoystick.Node
    {
        public Vector2 Current;
        public override Vector2 Value => Current;
    }

    private readonly ButtonNode jumpNode = new();
    private readonly ButtonNode dashNode = new();
    private readonly ButtonNode grabNode = new();
    private readonly ButtonNode crouchDashNode = new();
    private readonly AxisNode moveXNode = new();
    private readonly AxisNode moveYNode = new();
    private readonly JoystickNode aimNode = new();
    private ulong lastAdvanceFrame = ulong.MaxValue;

    public VirtualIntegerAxis MoveX { get; }
    public VirtualIntegerAxis MoveY { get; }
    public VirtualIntegerAxis GliderMoveY { get; }
    public VirtualJoystick Aim { get; }
    public VirtualJoystick Feather { get; }
    public VirtualButton Jump { get; }
    public VirtualButton Dash { get; }
    public VirtualButton Grab { get; }
    public VirtualButton CrouchDash { get; }
    public Vector2 LastAim { get; set; } = Vector2.UnitX;
    public bool GrabCheck => grabNode.Check;
    public bool GrabPressed => grabNode.Pressed;
    public bool GrabReleased => grabNode.Released;
    public bool CrouchDashPressed => crouchDashNode.Pressed;

    public NpcInputDevice()
    {
        MoveX = new VirtualIntegerAxis(moveXNode);
        MoveY = new VirtualIntegerAxis(moveYNode);
        GliderMoveY = new VirtualIntegerAxis(moveYNode);
        Aim = new VirtualJoystick(true, aimNode);
        Feather = new VirtualJoystick(true, aimNode);

        // Copy the initialized vanilla buttons' buffering configuration.
        // In the current game Grab.BufferTime is zero; copying it explicitly
        // keeps the NPC input semantics tied to the installed Celeste build.
        Jump = CreateButton(jumpNode, global::Celeste.Input.Jump);
        Dash = CreateButton(dashNode, global::Celeste.Input.Dash);
        Grab = CreateButton(grabNode, global::Celeste.Input.Grab);
        CrouchDash = CreateButton(crouchDashNode, global::Celeste.Input.CrouchDash);

        MoveX.Deregister();
        MoveY.Deregister();
        GliderMoveY.Deregister();
        Aim.Deregister();
        Feather.Deregister();
        Jump.Deregister();
        Dash.Deregister();
        Grab.Deregister();
        CrouchDash.Deregister();
    }

    public void Advance(NpcInputFrame frame)
    {
        ulong frameCounter = Engine.FrameCounter;
        if (lastAdvanceFrame == frameCounter)
            return;
        lastAdvanceFrame = frameCounter;

        moveXNode.Current = frame.Aim.X;
        moveYNode.Current = frame.Aim.Y;
        aimNode.Current = frame.Aim;
        jumpNode.Set(frame.Jump);
        dashNode.Set(frame.Dash);
        grabNode.Set(frame.Grab);
        crouchDashNode.Set(frame.CrouchDash);

        MoveX.Update();
        MoveY.Update();
        GliderMoveY.Update();
        Aim.Update();
        Feather.Update();
        Jump.Update();
        Dash.Update();
        Grab.Update();
        CrouchDash.Update();
    }

    public void Reset()
    {
        moveXNode.Current = 0f;
        moveYNode.Current = 0f;
        aimNode.Current = Vector2.Zero;
        jumpNode.Clear();
        dashNode.Clear();
        grabNode.Clear();
        crouchDashNode.Clear();
        LastAim = Vector2.UnitX;
        lastAdvanceFrame = ulong.MaxValue;
        Advance(NpcInputFrame.Empty);
    }

    private static VirtualButton CreateButton(ButtonNode node, VirtualButton template)
    {
        return new VirtualButton(template.BufferTime, node)
        {
            AutoConsumeBuffer = template.AutoConsumeBuffer
        };
    }
}