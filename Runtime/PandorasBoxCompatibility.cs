using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

internal static class PandorasBoxCompatibility
{
    private const string CloneSpawnerTypeName = "Celeste.Mod.PandorasBox.CloneSpawner";

    private static ILHook? playerDieHook;
    private static bool contractRejected;

    private delegate PlayerDeadBody? ForwardPlayerDie(
        On.Celeste.Player.orig_Die orig,
        Player self,
        Vector2 direction,
        bool evenIfInvincible,
        bool registerDeathsInStats);

    public static void EnsureInstalled()
    {
        if (playerDieHook is not null || contractRejected)
            return;

        Type? cloneSpawner = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(CloneSpawnerTypeName, false))
            .FirstOrDefault(type => type is not null);
        if (cloneSpawner is null)
            return;

        MethodInfo? playerOnDie = cloneSpawner.GetMethod(
            "Player_OnDie",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (!MatchesPlayerOnDieContract(playerOnDie))
        {
            RejectContract(cloneSpawner.Assembly, "Player_OnDie has an unexpected signature");
            return;
        }

        try
        {
            playerDieHook = new ILHook(playerOnDie!, PatchPlayerOnDie);
            Logger.Info(
                "npcPlayer",
                $"PandorasBox {FormatVersion(cloneSpawner.Assembly)} death compatibility enabled.");
        }
        catch (Exception error)
        {
            playerDieHook?.Dispose();
            playerDieHook = null;
            RejectContract(cloneSpawner.Assembly, error.Message);
        }
    }

    public static void Unload()
    {
        playerDieHook?.Dispose();
        playerDieHook = null;
        contractRejected = false;
    }

    private static void PatchPlayerOnDie(ILContext il)
    {
        ILCursor cursor = new(il);
        ILLabel continuePandorasBox = cursor.DefineLabel();

        // PandorasBox treats every tracked Player as one of its clones. An NPC
        // death must pass through to the next hook without killing the real
        // Player through CustomPlayerDeadBody.
        cursor.Emit(OpCodes.Ldarg, il.Method.Parameters[1]);
        cursor.EmitDelegate<Func<Player, bool>>(IsNpcPlayer);
        cursor.Emit(OpCodes.Brfalse, continuePandorasBox);
        foreach (ParameterDefinition parameter in il.Method.Parameters)
            cursor.Emit(OpCodes.Ldarg, parameter);
        cursor.EmitDelegate<ForwardPlayerDie>(ForwardNpcPlayerDeath);
        cursor.Emit(OpCodes.Ret);
        cursor.MarkLabel(continuePandorasBox);

        // Real-player deaths should still synchronize genuine PandorasBox
        // clones, but npcPlayers are owned by npcPlayer's death coordinator.
        if (!cursor.TryGotoNext(
                MoveType.After,
                instruction => instruction.Operand is GenericInstanceMethod generic &&
                    instruction.OpCode == OpCodes.Call &&
                    generic.ElementMethod.DeclaringType.FullName == typeof(Enumerable).FullName &&
                    generic.ElementMethod.Name == nameof(Enumerable.Cast) &&
                    generic.GenericArguments.Count == 1 &&
                    generic.GenericArguments[0].FullName == typeof(Player).FullName))
        {
            throw new InvalidOperationException(
                "Could not locate PandorasBox's tracked Player enumeration contract.");
        }

        cursor.EmitDelegate<Func<IEnumerable<Player>, IEnumerable<Player>>>(ExcludeNpcPlayers);
    }

    private static bool MatchesPlayerOnDieContract(MethodInfo? method)
    {
        if (method is null || method.ReturnType != typeof(PlayerDeadBody))
            return false;

        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length == 5 &&
            parameters[0].ParameterType == typeof(On.Celeste.Player.orig_Die) &&
            parameters[1].ParameterType == typeof(Player) &&
            parameters[2].ParameterType == typeof(Vector2) &&
            parameters[3].ParameterType == typeof(bool) &&
            parameters[4].ParameterType == typeof(bool);
    }

    private static bool IsNpcPlayer(Player player) => player is NpcPlayerEntity;

    private static PlayerDeadBody? ForwardNpcPlayerDeath(
        On.Celeste.Player.orig_Die orig,
        Player self,
        Vector2 direction,
        bool evenIfInvincible,
        bool registerDeathsInStats)
        => orig(self, direction, evenIfInvincible, registerDeathsInStats);

    private static IEnumerable<Player> ExcludeNpcPlayers(IEnumerable<Player> players)
        => players.Where(player => player is not NpcPlayerEntity);

    private static void RejectContract(Assembly assembly, string reason)
    {
        contractRejected = true;
        Logger.Warn(
            "npcPlayer",
            $"PandorasBox {FormatVersion(assembly)} death compatibility was not applied: {reason}. " +
            "PandorasBox and npcPlayer should not be used together in this session.");
    }

    private static string FormatVersion(Assembly assembly)
        => assembly.GetName().Version?.ToString() ?? "unknown";
}
