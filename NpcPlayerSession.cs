using System;
using System.Collections.Generic;

namespace Celeste.Mod.NpcPlayer;

public sealed class NpcPlayerSession : EverestModuleSession
{
    public string ActiveRoom { get; set; } = "";
    public Dictionary<string, string> RespawnPoints { get; set; } = new(StringComparer.Ordinal);
}