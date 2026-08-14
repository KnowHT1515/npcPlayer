using Celeste.Mod.NpcPlayer.Config;
using Celeste.Mod.NpcPlayer.Runtime;
using System;

namespace Celeste.Mod.NpcPlayer;

public sealed class NpcPlayerModule : EverestModule
{
    public static NpcPlayerModule Instance { get; private set; } = null!;

    public override Type SessionType => typeof(NpcPlayerSession);

    public static NpcPlayerSession Session => (NpcPlayerSession) Instance._Session;

    public NpcPlayerModule()
    {
        Instance = this;
    }

    public override void Load()
    {
        NpcPlayerHooks.Load();
        Everest.Content.OnUpdate += NpcVariantConfig.OnContentUpdate;
    }

    public override void Unload()
    {
        Everest.Content.OnUpdate -= NpcVariantConfig.OnContentUpdate;
        NpcPlayerHooks.Unload();
        NpcPlayerRegistry.Clear();
    }
}