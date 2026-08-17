using Celeste.Mod.NpcPlayer.Input;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

namespace Celeste.Mod.NpcPlayer.Runtime;

/// <summary>
/// Mixes audio owned by npcPlayers without changing the real Player, global
/// music, or sounds that cannot be attributed to one NPC.
/// </summary>
internal static class NpcAudioRouter
{
    private const float FullVolumeDistance = 64f;
    private const float SilentDistance = 320f;
    private const float SameEventVolumeBudget = 1f;
    private const float TotalNpcVolumeBudget = 1.2f;
    private const int MaxNpcOwnersPerEvent = 4;
    private const float GainRecoveryPerSecond = 12.5f;

    private sealed class NpcSoundSourceState
    {
        public NpcPlayerEntity Owner { get; }
        public EventInstance? Instance { get; set; }

        public NpcSoundSourceState(NpcPlayerEntity owner)
        {
            Owner = owner;
        }
    }

    private sealed class NpcVoice
    {
        public EventInstance Instance { get; }
        public NpcPlayerEntity Owner { get; set; }
        public string Path { get; set; }
        public ulong StartFrame { get; }
        public bool IsSoundSource { get; set; }
        public bool SuppressedDuplicate { get; set; }
        public float DistanceGain { get; set; }
        public float CurrentGain { get; set; }
        public float TargetGain { get; set; }

        public NpcVoice(EventInstance instance, NpcPlayerEntity owner, string path)
        {
            Instance = instance;
            Owner = owner;
            Path = path;
            StartFrame = Engine.FrameCounter;
            DistanceGain = GetDistanceGain(owner);
            CurrentGain = DistanceGain;
            TargetGain = DistanceGain;
        }
    }

    private static readonly FieldInfo SoundSourceInstanceField = typeof(SoundSource).GetField(
        "instance",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(SoundSource).FullName, "instance");

    private static ConditionalWeakTable<SoundSource, NpcSoundSourceState> soundSources = new();
    private static Dictionary<EventInstance, NpcVoice> voices = new();

    public static void Load()
    {
        On.Celeste.Audio.CreateInstance += OnCreateInstance;
        On.Celeste.Player.Play += OnPlayerPlay;
        On.Celeste.SoundSource.Play += OnSoundSourcePlay;
        On.Celeste.SoundSource.Update += OnSoundSourceUpdate;
        On.Celeste.Level.Update += OnLevelUpdate;
    }

    public static void Unload()
    {
        On.Celeste.Audio.CreateInstance -= OnCreateInstance;
        On.Celeste.Player.Play -= OnPlayerPlay;
        On.Celeste.SoundSource.Play -= OnSoundSourcePlay;
        On.Celeste.SoundSource.Update -= OnSoundSourceUpdate;
        On.Celeste.Level.Update -= OnLevelUpdate;

        foreach (NpcVoice voice in voices.Values)
        {
            if (!voice.Instance.isValid())
                continue;

            if (voice.SuppressedDuplicate)
                voice.Instance.stop(STOP_MODE.IMMEDIATE);
            else
                voice.Instance.setVolume(1f);
        }

        foreach (var pair in soundSources)
            ApplyVolume(pair.Key, 1f);

        voices = new Dictionary<EventInstance, NpcVoice>();
        soundSources = new ConditionalWeakTable<SoundSource, NpcSoundSourceState>();
    }

    private static EventInstance OnCreateInstance(
        On.Celeste.Audio.orig_CreateInstance orig,
        string path,
        Vector2? position)
    {
        EventInstance instance = orig(path, position);
        // Positionless events are normally music, ambience, snapshots, or UI.
        // They are not spatial feedback owned by the active npcPlayer update.
        if (position.HasValue && NpcInputContext.Current is NpcPlayerEntity npc)
            RegisterVoice(instance, npc, path, isSoundSource: false, checkSameFrameDuplicate: false);
        return instance;
    }

    private static EventInstance OnPlayerPlay(
        On.Celeste.Player.orig_Play orig,
        Player self,
        string sound,
        string? parameter,
        float value)
    {
        EventInstance instance = orig(self, sound, parameter, value);
        if (self is NpcPlayerEntity npc)
            RegisterVoice(instance, npc, sound, isSoundSource: false, checkSameFrameDuplicate: true);
        return instance;
    }

    private static SoundSource OnSoundSourcePlay(
        On.Celeste.SoundSource.orig_Play orig,
        SoundSource self,
        string path,
        string? parameter,
        float value)
    {
        // A reusable SoundSource can later be started by the real Player or an
        // unrelated entity, so every Play call replaces its previous owner.
        if (soundSources.TryGetValue(self, out NpcSoundSourceState? previousState) &&
            previousState.Instance is not null)
            RemoveVoice(previousState.Instance, restoreVolume: false);
        soundSources.Remove(self);

        NpcPlayerEntity? owner = self.Entity as NpcPlayerEntity ?? NpcInputContext.Current;
        SoundSource result = orig(self, path, parameter, value);
        if (owner is not null && TryGetSoundSourceInstance(self, out EventInstance instance))
        {
            var state = new NpcSoundSourceState(owner) { Instance = instance };
            soundSources.Add(self, state);
            RegisterVoice(instance, owner, path, isSoundSource: true, checkSameFrameDuplicate: false);
        }
        return result;
    }

    private static void OnSoundSourceUpdate(On.Celeste.SoundSource.orig_Update orig, SoundSource self)
    {
        orig(self);
        if (!soundSources.TryGetValue(self, out NpcSoundSourceState? state))
            return;

        if (state.Owner.Scene is not Level level ||
            self.Entity?.Scene is not null && !ReferenceEquals(self.Entity.Scene, level))
        {
            if (state.Instance is not null)
                RemoveVoice(state.Instance, restoreVolume: true);
            soundSources.Remove(self);
            return;
        }

        if (!TryGetSoundSourceInstance(self, out EventInstance instance))
        {
            if (state.Instance is not null)
                RemoveVoice(state.Instance, restoreVolume: false);
            state.Instance = null;
            return;
        }

        if (state.Instance is null || !state.Instance.Equals(instance) || !voices.ContainsKey(instance))
        {
            if (state.Instance is not null && !state.Instance.Equals(instance))
                RemoveVoice(state.Instance, restoreVolume: false);
            state.Instance = instance;
            RegisterVoice(instance, state.Owner, self.EventName, isSoundSource: true, checkSameFrameDuplicate: false);
        }
    }

    private static void OnLevelUpdate(On.Celeste.Level.orig_Update orig, Level self)
    {
        orig(self);
        UpdateMixer(self, cleanupStoppedVoices: true, smoothRecovery: true);
    }

    private static void RegisterVoice(
        EventInstance instance,
        NpcPlayerEntity owner,
        string path,
        bool isSoundSource,
        bool checkSameFrameDuplicate)
    {
        if (instance is null || !instance.isValid())
            return;

        if (!voices.TryGetValue(instance, out NpcVoice? voice))
        {
            voice = new NpcVoice(instance, owner, path);
            voices.Add(instance, voice);
        }
        else
        {
            voice.Owner = owner;
            voice.Path = path;
        }

        if (isSoundSource)
        {
            voice.IsSoundSource = true;
            voice.SuppressedDuplicate = false;
        }
        else if (checkSameFrameDuplicate && !voice.IsSoundSource)
        {
            voice.SuppressedDuplicate = HasSameFrameDuplicate(voice);
        }

        if (owner.Scene is Level level)
            UpdateMixer(level, cleanupStoppedVoices: false, smoothRecovery: false);
    }

    private static bool HasSameFrameDuplicate(NpcVoice candidate)
    {
        foreach (NpcVoice voice in voices.Values)
        {
            if (ReferenceEquals(voice, candidate) || voice.IsSoundSource || voice.SuppressedDuplicate)
                continue;

            if (voice.StartFrame == candidate.StartFrame &&
                ReferenceEquals(voice.Owner, candidate.Owner) &&
                string.Equals(voice.Path, candidate.Path, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void UpdateMixer(Level level, bool cleanupStoppedVoices, bool smoothRecovery)
    {
        var staleVoices = new List<NpcVoice>();
        var applicableVoices = new List<NpcVoice>();
        var ownerDistanceGains = new Dictionary<NpcPlayerEntity, float>();
        var eventOwners = new Dictionary<string, Dictionary<NpcPlayerEntity, List<NpcVoice>>>(StringComparer.Ordinal);
        Player? realPlayer = FindRealPlayer(level);

        foreach (NpcVoice voice in voices.Values)
        {
            if (!voice.Instance.isValid())
            {
                staleVoices.Add(voice);
                continue;
            }

            if (!ReferenceEquals(voice.Owner.Scene, level))
            {
                if (cleanupStoppedVoices)
                {
                    voice.Instance.setVolume(0f);
                    staleVoices.Add(voice);
                }
                continue;
            }

            if (cleanupStoppedVoices && IsStopped(voice.Instance))
            {
                staleVoices.Add(voice);
                continue;
            }

            voice.DistanceGain = realPlayer is null ? 1f : GetDistanceGain(voice.Owner, realPlayer);
            voice.TargetGain = realPlayer is null && !voice.SuppressedDuplicate ? 1f : 0f;
            applicableVoices.Add(voice);

            if (realPlayer is null || voice.SuppressedDuplicate || voice.DistanceGain <= 0f)
                continue;

            ownerDistanceGains[voice.Owner] = voice.DistanceGain;
            if (!eventOwners.TryGetValue(
                voice.Path,
                out Dictionary<NpcPlayerEntity, List<NpcVoice>>? owners))
            {
                owners = new Dictionary<NpcPlayerEntity, List<NpcVoice>>();
                eventOwners.Add(voice.Path, owners);
            }

            if (!owners.TryGetValue(voice.Owner, out List<NpcVoice>? ownerVoices))
            {
                ownerVoices = new List<NpcVoice>();
                owners.Add(voice.Owner, ownerVoices);
            }
            ownerVoices.Add(voice);
        }

        foreach (NpcVoice stale in staleVoices)
            voices.Remove(stale.Instance);

        // Distance and crowd mixing need a real listener. Apart from explicit
        // same-frame duplicates, preserve the original volume during loads or
        // transitions where no real Player can be identified.
        if (realPlayer is null)
        {
            foreach (NpcVoice voice in applicableVoices)
                ApplyTargetGain(voice, smoothRecovery);
            return;
        }

        // One NPC keeps the same internal mix as a real Player. Crowd budgets
        // begin only when distinct npcPlayer owners contribute active voices.
        if (ownerDistanceGains.Count <= 1)
        {
            foreach (NpcVoice voice in applicableVoices)
            {
                if (!voice.SuppressedDuplicate)
                    voice.TargetGain = voice.DistanceGain;
                ApplyTargetGain(voice, smoothRecovery);
            }
            return;
        }

        float totalOwnerGain = 0f;
        foreach (float ownerGain in ownerDistanceGains.Values)
            totalOwnerGain += ownerGain;
        float crowdScale = GetBudgetScale(totalOwnerGain, TotalNpcVolumeBudget);

        foreach (NpcVoice voice in applicableVoices)
            if (!voice.SuppressedDuplicate)
                voice.TargetGain = voice.DistanceGain * crowdScale;

        foreach (Dictionary<NpcPlayerEntity, List<NpcVoice>> owners in eventOwners.Values)
        {
            var prioritizedOwners = new List<NpcPlayerEntity>(owners.Keys);
            prioritizedOwners.Sort((left, right) =>
                ownerDistanceGains[right].CompareTo(ownerDistanceGains[left]));

            int audibleOwnerCount = Math.Min(prioritizedOwners.Count, MaxNpcOwnersPerEvent);
            float eventOwnerGain = 0f;
            for (int i = 0; i < audibleOwnerCount; i++)
                eventOwnerGain += ownerDistanceGains[prioritizedOwners[i]];

            float finalScale = GetOwnerMixScale(totalOwnerGain, eventOwnerGain);
            for (int i = 0; i < prioritizedOwners.Count; i++)
            {
                NpcPlayerEntity owner = prioritizedOwners[i];
                float ownerScale = i < audibleOwnerCount ? finalScale : 0f;
                foreach (NpcVoice voice in owners[owner])
                    voice.TargetGain = voice.DistanceGain * ownerScale;
            }
        }

        foreach (NpcVoice voice in applicableVoices)
            ApplyTargetGain(voice, smoothRecovery);
    }

    private static float GetBudgetScale(float totalGain, float budget)
        => totalGain > budget && totalGain > 0f ? budget / totalGain : 1f;

    private static float GetOwnerMixScale(float totalOwnerGain, float eventOwnerGain)
        => Math.Min(
            GetBudgetScale(totalOwnerGain, TotalNpcVolumeBudget),
            GetBudgetScale(eventOwnerGain, SameEventVolumeBudget));

    private static void ApplyTargetGain(NpcVoice voice, bool smoothRecovery)
    {
        float target = Math.Clamp(voice.TargetGain, 0f, 1f);
        if (!smoothRecovery || target <= voice.CurrentGain)
        {
            voice.CurrentGain = target;
        }
        else
        {
            float recovery = GainRecoveryPerSecond * Math.Max(0f, Engine.RawDeltaTime);
            voice.CurrentGain = Math.Min(target, voice.CurrentGain + recovery);
        }

        if (voice.Instance.isValid())
            voice.Instance.setVolume(voice.CurrentGain);
    }

    private static bool IsStopped(EventInstance instance)
        => instance.getPlaybackState(out PLAYBACK_STATE state) != RESULT.OK ||
           state == PLAYBACK_STATE.STOPPED;

    private static void RemoveVoice(EventInstance instance, bool restoreVolume)
    {
        if (!voices.Remove(instance, out NpcVoice? voice))
            return;

        if (restoreVolume && voice.Instance.isValid())
            voice.Instance.setVolume(1f);
    }

    private static bool TryGetSoundSourceInstance(SoundSource source, out EventInstance instance)
    {
        instance = SoundSourceInstanceField.GetValue(source) as EventInstance ?? null!;
        return instance is not null && instance.isValid();
    }

    private static void ApplyVolume(SoundSource source, float gain)
    {
        if (TryGetSoundSourceInstance(source, out EventInstance instance))
            instance.setVolume(gain);
    }

    private static float GetDistanceGain(NpcPlayerEntity npc)
    {
        if (npc.Scene is not Level level)
            return 1f;

        Player? realPlayer = FindRealPlayer(level);
        if (realPlayer is null)
            return 1f;

        return GetDistanceGain(npc, realPlayer);
    }

    private static float GetDistanceGain(NpcPlayerEntity npc, Player realPlayer)
    {
        float distance = Vector2.Distance(npc.Center, realPlayer.Center);
        if (distance <= FullVolumeDistance)
            return 1f;
        if (distance >= SilentDistance)
            return 0f;

        float t = (distance - FullVolumeDistance) / (SilentDistance - FullVolumeDistance);
        float smoothStep = t * t * (3f - 2f * t);
        return 1f - smoothStep;
    }

    private static Player? FindRealPlayer(Level level)
    {
        foreach (Entity entity in level.Tracker.GetEntities<Player>())
        {
            if (entity is Player player && player is not NpcPlayerEntity)
                return player;
        }
        return null;
    }
}
