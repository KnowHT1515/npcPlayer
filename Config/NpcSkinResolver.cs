using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.NpcPlayer.Config;

internal static class NpcSkinResolver
{
    private const string ModuleName = "SkinModHelperPlus";
    private static readonly object sync = new();
    private static readonly HashSet<string> warnedSkins = new(StringComparer.Ordinal);
    private static readonly HashSet<string> warnedInterop = new(StringComparer.Ordinal);
    private static bool bindingAttempted;
    private static bool smhInstalled;
    private static string smhVersion = "unknown";
    private static SmhBridge? bridge;

    private sealed class WeakCache
    {
        private readonly object table;
        private readonly MethodInfo remove;
        private readonly Type keyType;

        private WeakCache(object table, MethodInfo remove, Type keyType)
        {
            this.table = table;
            this.remove = remove;
            this.keyType = keyType;
        }

        public static WeakCache? Bind(Type owner, string fieldName)
        {
            object? table = owner.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (table is null)
                return null;

            foreach (MethodInfo method in table.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == "Remove" && method.ReturnType == typeof(bool) && parameters.Length == 1)
                    return new WeakCache(table, method, parameters[0].ParameterType);
            }
            return null;
        }

        public void Remove(object key)
        {
            if (!keyType.IsInstanceOfType(key))
                throw new InvalidOperationException(
                    $"SMH+ cache expected '{keyType.FullName}', not '{key.GetType().FullName}'.");
            remove.Invoke(table, new[] { key });
        }
    }

    private sealed class SmhBridge
    {
        private readonly FieldInfo skinConfigs;
        private readonly FieldInfo hashValues;
        private readonly PropertyInfo skinName;
        private readonly WeakCache? spriteCache;
        private readonly WeakCache? characterCache;
        private readonly WeakCache? hairCache;

        public bool CanResetMetadata =>
            spriteCache is not null && characterCache is not null && hairCache is not null;

        public SmhBridge(Assembly assembly)
        {
            Type skins = RequireType(assembly, "Celeste.Mod.SkinModHelper.SkinsSystem");
            Type config = RequireType(assembly, "Celeste.Mod.SkinModHelper.SkinModHelperConfig");
            Type character = RequireType(assembly, "Celeste.Mod.SkinModHelper.CharacterConfig");
            Type hair = RequireType(assembly, "Celeste.Mod.SkinModHelper.HairConfig");

            skinConfigs = RequireField(skins, "skinConfigs", typeof(IDictionary), BindingFlags.Public | BindingFlags.Static);
            hashValues = RequireField(config, "hashValues", typeof(int), BindingFlags.Public | BindingFlags.Instance);
            skinName = config.GetProperty("SkinName", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(config.FullName, "SkinName");
            if (skinName.PropertyType != typeof(string) || skinName.GetMethod is null)
                throw new InvalidOperationException("SMH+ SkinName is not a readable string property.");

            spriteCache = WeakCache.Bind(skins, "SpriteDataCache");
            characterCache = WeakCache.Bind(character, "_Instance");
            hairCache = WeakCache.Bind(hair, "_Instance");
        }

        public bool TryResolve(string requestedName, out PlayerSpriteMode mode)
        {
            mode = PlayerSpriteMode.MadelineAsBadeline;
            if (skinConfigs.GetValue(null) is not IDictionary configs || !configs.Contains(requestedName))
                return false;

            object? config = configs[requestedName];
            if (config is null ||
                skinName.GetValue(config) is not string actualName ||
                !string.Equals(actualName, requestedName, StringComparison.Ordinal) ||
                hashValues.GetValue(config) is not int hash)
                return false;

            mode = (PlayerSpriteMode) hash;
            return true;
        }

        public void ResetMetadata(PlayerSprite sprite, PlayerHair hair)
        {
            if (!CanResetMetadata)
                throw new MissingMemberException("SMH+ metadata caches do not match the expected 0.17.x contract.");

            spriteCache!.Remove(sprite);
            characterCache!.Remove(sprite);
            hairCache!.Remove(hair);
        }

        private static Type RequireType(Assembly assembly, string name) =>
            assembly.GetType(name, false) ?? throw new TypeLoadException($"SMH+ type '{name}' was not found.");

        private static FieldInfo RequireField(Type owner, string name, Type expectedType, BindingFlags flags)
        {
            FieldInfo field = owner.GetField(name, flags) ?? throw new MissingFieldException(owner.FullName, name);
            if (!expectedType.IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException(
                    $"SMH+ field '{owner.FullName}.{name}' is '{field.FieldType.FullName}', expected '{expectedType.FullName}'.");
            return field;
        }
    }

    public static PlayerSpriteMode ResolveSpriteMode(string variant)
    {
        if (string.Equals(variant, "madeline", StringComparison.OrdinalIgnoreCase))
            return PlayerSpriteMode.Madeline;
        if (string.Equals(variant, "badeline", StringComparison.OrdinalIgnoreCase))
            return PlayerSpriteMode.MadelineAsBadeline;

        SmhBridge? current = GetBridge();
        if (current is not null)
        {
            try
            {
                if (current.TryResolve(variant, out PlayerSpriteMode mode))
                    return mode;
            }
            catch (Exception error)
            {
                WarnOnce("resolve", $"Failed to resolve SMH+ SkinName '{variant}'", error);
            }
        }

        if (warnedSkins.Add(variant))
            Logger.Warn("npcPlayer", $"SMH+ SkinName '{variant}' was not found. Falling back to badeline.");
        return PlayerSpriteMode.MadelineAsBadeline;
    }

    public static bool TryResetBuiltInSkinMetadata(PlayerSprite sprite, PlayerHair hair)
    {
        SmhBridge? current = GetBridge();
        if (!smhInstalled)
            return true;
        if (current is null || !current.CanResetMetadata)
        {
            WarnOnce(
                "metadata-contract",
                "SMH+ metadata cleanup is unavailable; keeping its managed sprite instead of partially applying a vanilla sprite",
                null);
            return false;
        }

        try
        {
            current.ResetMetadata(sprite, hair);
            return true;
        }
        catch (Exception error)
        {
            WarnOnce(
                "metadata-reset",
                "SMH+ metadata cleanup failed; keeping its managed sprite instead of partially applying a vanilla sprite",
                error);
            return false;
        }
    }

    public static void Clear()
    {
        lock (sync)
        {
            bindingAttempted = false;
            smhInstalled = false;
            smhVersion = "unknown";
            bridge = null;
            warnedSkins.Clear();
            warnedInterop.Clear();
        }
    }

    private static SmhBridge? GetBridge()
    {
        lock (sync)
        {
            if (bindingAttempted)
                return bridge;

            bindingAttempted = true;
            EverestModule? module = null;
            foreach (EverestModule candidate in Everest.Modules)
            {
                if (string.Equals(candidate.Metadata?.Name, ModuleName, StringComparison.Ordinal))
                {
                    module = candidate;
                    break;
                }
            }

            if (module is null)
                return null;

            smhInstalled = true;
            smhVersion = module.Metadata?.VersionString ?? module.Metadata?.Version?.ToString() ?? "unknown";
            try
            {
                bridge = new SmhBridge(module.GetType().Assembly);
                if (!bridge.CanResetMetadata)
                    WarnOnce("metadata-bind", "SMH+ skin lookup is compatible, but its private metadata cache contract changed", null);
            }
            catch (Exception error)
            {
                bridge = null;
                WarnOnce("binding", "SMH+ compatibility binding failed", error);
            }
            return bridge;
        }
    }

    private static void WarnOnce(string key, string message, Exception? error)
    {
        lock (sync)
        {
            if (!warnedInterop.Add(key))
                return;
        }

        Exception? actual = error is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException
            : error;
        string suffix = actual is null ? "" : $": {actual.Message}";
        Logger.Warn("npcPlayer", $"{message} (SkinModHelperPlus {smhVersion}){suffix}");
    }
}