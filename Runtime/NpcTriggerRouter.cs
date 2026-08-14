using Monocle;
using System;
using System.Collections.Generic;
using System.Reflection;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

/// <summary>
/// Runs an NPC-local trigger state machine without claiming Trigger.Triggered,
/// which is a single shared flag in vanilla Celeste.
/// </summary>
internal static class NpcTriggerRouter
{
    internal readonly struct VanillaTriggerState
    {
        public Trigger Trigger { get; }
        public bool Collidable { get; }
        public bool Triggered { get; }

        public VanillaTriggerState(Trigger trigger)
        {
            Trigger = trigger;
            Collidable = trigger.Collidable;
            Triggered = trigger.Triggered;
        }
    }

    private enum Callback
    {
        Enter,
        Stay,
        Leave
    }

    private static readonly FieldInfo PlayerIsInsideField = typeof(Trigger).GetField(
        "<PlayerIsInside>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(Trigger).FullName, "<PlayerIsInside>k__BackingField");

    public static void SuppressVanilla(NpcPlayerEntity npc)
    {
        List<VanillaTriggerState> states = npc.VanillaTriggerStates;
        states.Clear();
        if (npc.Scene is null)
            return;

        foreach (Entity entity in npc.Scene.Tracker.GetEntities<Trigger>())
        {
            if (entity is not Trigger trigger)
                continue;

            states.Add(new VanillaTriggerState(trigger));
            // Player.Update treats Collidable as the spatial gate and Triggered
            // as its shared transition state. Hide both while an NPC executes
            // the inherited loop so it cannot enter or leave real-player state.
            trigger.Collidable = false;
            trigger.Triggered = false;
        }
    }

    public static void RestoreVanilla(NpcPlayerEntity npc)
    {
        List<VanillaTriggerState> states = npc.VanillaTriggerStates;
        foreach (VanillaTriggerState state in states)
        {
            state.Trigger.Collidable = state.Collidable;
            state.Trigger.Triggered = state.Triggered;
        }
        states.Clear();
    }

    public static void Update(NpcPlayerEntity npc)
    {
        // Vanilla skips its trigger loop in state 18. Preserve that behavior
        // without allowing it to touch the shared trigger flags.
        if (npc.StateMachine.State == 18 || npc.Scene is null)
            return;

        foreach (Entity entity in npc.Scene.Tracker.GetEntities<Trigger>())
        {
            if (entity is not Trigger trigger)
                continue;

            bool wasInside = npc.NpcTriggersInside.Contains(trigger);
            bool allowed = trigger is INpcPlayerTrigger compatible && compatible.AllowsNpcPlayer(npc);
            bool isInside = allowed && npc.CollideCheck(trigger);

            if (isInside)
            {
                if (!wasInside)
                {
                    npc.NpcTriggersInside.Add(trigger);
                    Invoke(trigger, npc, Callback.Enter);
                }
                Invoke(trigger, npc, Callback.Stay);
            }
            else if (wasInside)
            {
                npc.NpcTriggersInside.Remove(trigger);
                Invoke(trigger, npc, Callback.Leave);
            }
        }
    }

    public static void Remove(NpcPlayerEntity npc)
    {
        if (npc.NpcTriggersInside.Count == 0)
            return;

        List<Trigger> active = new(npc.NpcTriggersInside);
        npc.NpcTriggersInside.Clear();
        foreach (Trigger trigger in active)
            Invoke(trigger, npc, Callback.Leave);
    }

    private static void Invoke(Trigger trigger, NpcPlayerEntity npc, Callback callback)
    {
        bool previousTriggered = trigger.Triggered;
        bool previousPlayerInside = (bool) (PlayerIsInsideField.GetValue(trigger) ?? false);
        try
        {
            // Match the values observed during vanilla callbacks. OnLeave is
            // called after Triggered becomes false but before base.OnLeave
            // clears PlayerIsInside.
            trigger.Triggered = callback != Callback.Leave;
            PlayerIsInsideField.SetValue(trigger, true);

            switch (callback)
            {
                case Callback.Enter:
                    trigger.OnEnter(npc);
                    break;
                case Callback.Stay:
                    trigger.OnStay(npc);
                    break;
                case Callback.Leave:
                    trigger.OnLeave(npc);
                    break;
            }
        }
        finally
        {
            trigger.Triggered = previousTriggered;
            PlayerIsInsideField.SetValue(trigger, previousPlayerInside);
        }
    }
}
