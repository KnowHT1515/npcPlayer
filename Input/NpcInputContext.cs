using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Reflection;
using CelesteInput = global::Celeste.Input;

namespace Celeste.Mod.NpcPlayer.Input;

internal static class NpcInputContext
{
    private static readonly FieldInfo? ButtonBufferCounterField = FindButtonField("bufferCounter");
    private static readonly FieldInfo? ButtonConsumedField = FindButtonField("consumed");
    private static readonly FieldInfo? ButtonRepeatCounterField = FindButtonField("repeatCounter");
    private static readonly FieldInfo? ButtonRepeatingField = FindButtonField("<Repeating>k__BackingField");

    private readonly struct VirtualButtonRuntimeState
    {
        private readonly VirtualButton button;
        private readonly float bufferCounter;
        private readonly bool consumed;
        private readonly float repeatCounter;
        private readonly bool repeating;

        public VirtualButtonRuntimeState(VirtualButton button)
        {
            this.button = button;
            bufferCounter = ReadFloat(ButtonBufferCounterField, button);
            consumed = ReadBool(ButtonConsumedField, button);
            repeatCounter = ReadFloat(ButtonRepeatCounterField, button);
            repeating = ReadBool(ButtonRepeatingField, button);
        }

        public void Restore()
        {
            Write(ButtonBufferCounterField, button, bufferCounter);
            Write(ButtonConsumedField, button, consumed);
            Write(ButtonRepeatCounterField, button, repeatCounter);
            Write(ButtonRepeatingField, button, repeating);
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly VirtualIntegerAxis moveX;
        private readonly VirtualIntegerAxis moveY;
        private readonly VirtualIntegerAxis gliderMoveY;
        private readonly VirtualJoystick aim;
        private readonly VirtualJoystick feather;
        private readonly VirtualButton jump;
        private readonly VirtualButton dash;
        private readonly VirtualButton grab;
        private readonly VirtualButton crouchDash;
        private readonly VirtualButtonRuntimeState jumpState;
        private readonly VirtualButtonRuntimeState dashState;
        private readonly VirtualButtonRuntimeState grabState;
        private readonly VirtualButtonRuntimeState crouchDashState;
        private readonly Vector2 lastAim;
        private readonly NpcInputDevice device;
        private bool disposed;

        public Scope(NpcInputDevice device)
        {
            this.device = device;
            moveX = CelesteInput.MoveX;
            moveY = CelesteInput.MoveY;
            gliderMoveY = CelesteInput.GliderMoveY;
            aim = CelesteInput.Aim;
            feather = CelesteInput.Feather;
            jump = CelesteInput.Jump;
            dash = CelesteInput.Dash;
            grab = CelesteInput.Grab;
            crouchDash = CelesteInput.CrouchDash;
            jumpState = new VirtualButtonRuntimeState(jump);
            dashState = new VirtualButtonRuntimeState(dash);
            grabState = new VirtualButtonRuntimeState(grab);
            crouchDashState = new VirtualButtonRuntimeState(crouchDash);
            lastAim = CelesteInput.LastAim;

            CelesteInput.MoveX = device.MoveX;
            CelesteInput.MoveY = device.MoveY;
            CelesteInput.GliderMoveY = device.GliderMoveY;
            CelesteInput.Aim = device.Aim;
            CelesteInput.Feather = device.Feather;
            CelesteInput.Jump = device.Jump;
            CelesteInput.Dash = device.Dash;
            CelesteInput.Grab = device.Grab;
            CelesteInput.CrouchDash = device.CrouchDash;
            CelesteInput.LastAim = device.LastAim;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            device.LastAim = CelesteInput.LastAim;
            CelesteInput.MoveX = moveX;
            CelesteInput.MoveY = moveY;
            CelesteInput.GliderMoveY = gliderMoveY;
            CelesteInput.Aim = aim;
            CelesteInput.Feather = feather;
            CelesteInput.Jump = jump;
            CelesteInput.Dash = dash;
            CelesteInput.Grab = grab;
            CelesteInput.CrouchDash = crouchDash;
            CelesteInput.LastAim = lastAim;

            // Player hooks from multi-player mods can retain direct references to
            // the displaced real buttons and mutate them while the NPC is updating.
            // Restore their transient state so NPC updates remain input-isolated.
            jumpState.Restore();
            dashState.Restore();
            grabState.Restore();
            crouchDashState.Restore();
        }
    }

    private sealed class CompositeScope : IDisposable
    {
        private readonly Scope scope;
        private readonly Entities.NpcPlayer player;
        private readonly NpcInputDevice device;
        private bool disposed;

        public CompositeScope(Scope scope, Entities.NpcPlayer player, NpcInputDevice device)
        {
            this.scope = scope;
            this.player = player;
            this.device = device;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            try
            {
                scope.Dispose();
            }
            finally
            {
                NpcInputDevice poppedDevice = ActiveDevices.Pop();
                if (!ReferenceEquals(poppedDevice, device))
                    throw new InvalidOperationException("npcPlayer input device scopes were disposed out of order.");

                Entities.NpcPlayer poppedPlayer = ActivePlayers.Pop();
                if (!ReferenceEquals(poppedPlayer, player))
                    throw new InvalidOperationException("npcPlayer input scopes were disposed out of order.");
            }
        }
    }

    private static readonly Stack<Entities.NpcPlayer> ActivePlayers = new();
    private static readonly Stack<NpcInputDevice> ActiveDevices = new();

    public static Entities.NpcPlayer? Current => ActivePlayers.Count == 0 ? null : ActivePlayers.Peek();
    public static NpcInputDevice? CurrentDevice => ActiveDevices.Count == 0 ? null : ActiveDevices.Peek();

    public static IDisposable Push(Entities.NpcPlayer player, NpcInputDevice device)
    {
        ActivePlayers.Push(player);
        ActiveDevices.Push(device);
        return new CompositeScope(new Scope(device), player, device);
    }

    private static FieldInfo? FindButtonField(string name)
        => typeof(VirtualButton).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static float ReadFloat(FieldInfo? field, VirtualButton button)
        => field?.GetValue(button) is float value ? value : 0f;

    private static bool ReadBool(FieldInfo? field, VirtualButton button)
        => field?.GetValue(button) is bool value && value;

    private static void Write<T>(FieldInfo? field, VirtualButton button, T value)
        => field?.SetValue(button, value);
}