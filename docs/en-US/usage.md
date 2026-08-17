# npcPlayer 0.2.2 Usage Guide

This guide is intended for Celeste map authors using Lönn. It explains how to create `Player`-type NPCs driven by TAS input files packaged with a map mod.

npcPlayer is currently a technical preview. Test the entities and other Helpers required by your map in an isolated room before using them in a production sequence.

## 1. What npcPlayer Does

npcPlayer creates NPCs that genuinely inherit from `Celeste.Player` and drives them with direct-input TAS files from the map mod. As a result, NPCs can use most vanilla Player movement abilities, including walking, jumping, dashing, climbing, and grabbing.

It is not a story NPC framework, a pathfinding system, or a CelesteTAS playback engine. CelesteTAS is not a runtime dependency; it is only useful during authoring for recording, observing, and adjusting input.

Current main features:

- A room can contain multiple NPCs with different `npcId` values.
- One NPC can have multiple spawn points and switch respawn points after a death reload.
- TAS input is stored and executed as compressed segments instead of one object per frame.
- TAS and skin configuration can be hot-reloaded when the map mod is installed as an unpacked directory.
- Built-in Madeline and Badeline appearances are available, with optional SkinModHelperPlus `SkinName` support.
- NPCs can interact with common terrain, moving platforms, holdables, and selected vanilla `PlayerCollider` entities.

## 2. Installation and Dependencies

Place the npcPlayer release ZIP directly in:

```text
Celeste/Mods/
```

Do not unpack the npcPlayer release itself. After restarting Celeste and Lönn, the following objects should be available in Lönn:

- Entity: `npcPlayer (Spawn Point)`
- Trigger: `Activate npcPlayer`
- Trigger: `Change npcPlayer Respawn`

At minimum, the map mod's `everest.yaml` should contain:

```yaml
- Name: YourMap
  Version: 1.0.0
  Dependencies:
    - Name: Everest
      Version: 1.0.0
    - Name: npcPlayer
      Version: 0.2.2
```

The map mod only needs an additional SkinModHelperPlus dependency when it uses a custom SMH+ skin. SkinModHelperPlus is not required for the built-in `madeline` or `badeline` appearances.

## 3. Minimal Working Example

The following example creates an NPC named `partner`. When the real Player enters the trigger, the NPC moves right for 30 frames, jumps, and then dashes.

### 3.1 Place a Spawn Point

Place `npcPlayer (Spawn Point)` in the room with:

```text
npcId: partner
default: true
```

### 3.2 Create a TAS File

Create the following file inside the map mod:

```text
Tas/YourName/YourMap/partner_intro.tas
```

Contents:

```tas
30,R
1,R,J
12,R
1,R,X
20,R
```

### 3.3 Place an Activation Trigger

Place `Activate npcPlayer` with:

```text
npcId: partner
tas: Tas/YourName/YourMap/partner_intro.tas
once: true
playerOnly: true
```

When the room loads, it creates `partner`. The TAS starts the first time the real Player enters the trigger.

## 4. Recommended Map Mod Layout

```text
YourMod/
  Maps/
    Author/
      Map.bin
  Tas/
    Author/
      Map/
        partner_intro.tas
        partner_escape.tas
  config/
    npcPlayer/
      npcPlayer.yaml
  everest.yaml
  Dialog/
  Graphics/
```

The TAS files and configuration belong to the map mod that uses npcPlayer; they should not be placed in npcPlayer's own release ZIP.

Use namespaced paths such as `Tas/<Author>/<Map>/<Action>.tas` to avoid file-name collisions in larger mods.

## 5. NPC Spawn Points

Lönn entity ID:

```text
npcPlayer/npcPlayerSpawnPoint
```

Fields:

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `npcId` | string | empty | Case-sensitive logical NPC identity |
| `default` | boolean | `false` | Whether this point is preferred as the NPC's initial spawn point in this room |

Only one runtime NPC is created for each unique `npcId` when the room loads. One `npcId` can have multiple spawn points:

- If any point has `default=true`, the first default point in map-data order is used.
- If no point is marked as default, the first point in map-data order is used.
- Multiple defaults for one ID are allowed, but only the first is used.
- IDs with different capitalization are different NPCs; for example, `Partner` and `partner` do not match.
- A blank `npcId` is normalized to `npc`.

In Lönn, every npcPlayer `npcId` field provides a searchable list of IDs collected from all npcPlayer entities and triggers across the currently open map. The list is only an editing aid: the field remains editable and accepts new or intentionally unmatched values.

Runtime NPCs belong only to the current room. Cross-room movement and following are not implemented; entering another room recreates NPCs from that room's spawn points.

## 6. NPC Configuration

Appearance and death-link configuration use an optional file in the map mod:

```text
config/npcPlayer/npcPlayer.yaml
```

Example:

```yaml
npcPlayer:
  - npcId: "partner"
    variant: "madeline"
    death_link:
      lead_by_player: true
      lead_to_player: false
  - npcId: "rival"
    variant: "badeline"
    death_link:
      lead_by_player: false
      lead_to_player: false
  - npcId: "guide"
    variant: "MySmhPlusSkinName"
```

The two death links are independent directed edges centered on the real Player:

- `lead_by_player` controls whether a real Player death causes this NPC to die.
- `lead_to_player` controls whether this NPC's death causes the real Player to die.
- NPCs never link directly to one another. If NPC A kills the Player, the Player propagates death only to NPCs whose own `lead_by_player` value is `true`.

Rules:

- `npcId` must exactly match the spawn point, including capitalization.
- `madeline` and `badeline` are built-in npcPlayer appearance names and are case-insensitive.
- Any other `variant` is looked up as a raw `SkinName` from SkinModHelperPlus's `SkinModHelperConfig.yaml`.
- NPCs without an entry use Badeline and both death links default to `true`.
- Missing or invalid `death_link` values fall back to `true` per field without discarding a valid variant.
- A missing custom `SkinName` produces a warning and falls back to Badeline.
- If an `npcId` appears more than once, the first valid entry wins and later entries are ignored with a warning.
- Invalid entries are skipped. If the entire file cannot be parsed, all NPCs from that map mod use the default appearance and death links.

The v0.2.0 layout remains supported. Legacy entries receive `lead_by_player: true` and `lead_to_player: true`:

```yaml
- npcPlayer:
    npcId: "partner"
    variant: "madeline"
```

SkinModHelperPlus is optional. npcPlayer reads its skin information through a validated compatibility layer. If a future version changes the internal metadata contract, npcPlayer logs a versioned warning and preserves the fully SMH+-managed state instead of producing a partially applied skin.

## 7. TAS File Format

npcPlayer accepts direct frame-input lines:

> **Recording recommendation:** When recording scripts with CelesteTAS, use Extended Variants to disable freeze-frame effects. npcPlayer does not reproduce every freeze frame exactly as the real Player does. Recording against a real-Player flow with freeze frames enabled can shift playback timing and make the NPC behave differently from the recording.

```text
frame count,input,input,...
```

For example:

```tas
# Move right
30,R

# Jump right
1,R,J
20,R

# Dash right
1,R,X
15,R

# Grab the wall on the right
10,R,G
```

Supported inputs:

| Input | Alias | Meaning |
| --- | --- | --- |
| `L` | — | Left |
| `R` | — | Right |
| `U` | — | Up |
| `D` | — | Down |
| `J` | `K` | Jump |
| `X` | `C` | Dash |
| `G` | `H` | Grab |
| `Z` | `V` | Crouch dash |

Directional input is combined into a normalized direction. For example, `U,R` produces a diagonal up-right direction rather than two independent full-strength axes.

Parsing rules:

- The frame count must be a positive integer.
- Input names are case-insensitive.
- Blank lines are ignored.
- Ordinary comments beginning with `#` or `//` are ignored.
- An unknown input rejects the entire TAS and reports the file and line number in `log.txt`.
- When the TAS ends, NPC input returns to an empty state and the NPC remains at its current position.
- Starting a new TAS immediately interrupts the old TAS and begins the new file from its first frame.

Currently unsupported:

- CelesteTAS commands.
- `Read`, `Repeat`, and `RecordCount`.
- Savestate commands.
- Precise analog-stick values.
- TAS segment labels.
- The legacy `#npcPlayer...` segmented syntax. It is explicitly rejected instead of silently concatenating multiple segments.

Safety limits:

- One file can contain at most 100,000 input segments.
- One file can contain at most 10,000,000 total frames.
- The TAS path must remain inside the current map mod.
- Absolute paths, drive paths, empty paths, and `.` or `..` path components are rejected.
- The `.tas` extension is added when omitted, although specifying it explicitly is recommended.

## 8. Activate npcPlayer

Lönn trigger ID:

```text
npcPlayer/activateNpcPlayer
```

Fields:

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `npcId` | string | empty | Case-sensitive target NPC ID |
| `tas` | string | empty | TAS path relative to the map mod root |
| `once` | boolean | `true` | Whether to disable the trigger after one successful activation |
| `playerOnly` | boolean | `true` | Whether only the real Player may activate it |

For a map named `MapName.bin`, Lönn scans the current map mod for TAS files directly inside directories matching `Tas/**/MapName/`. Matching `.tas` files appear in a searchable list, while arbitrary paths remain editable. Windows displays `\` separators for convenience; selected and manually entered paths are normalized to portable `/` separators before being stored in map data.

`once` is consumed only after the TAS has been found and parsed successfully. If the NPC is missing, the source map mod cannot be identified, the path is unsafe, or parsing fails, the trigger can be tried again and the reason is written to the log.

When `playerOnly=false`, both the real Player and npcPlayers can activate it. Ordinary Celeste triggers do not respond to npcPlayers by default; this is an explicit compatibility exception provided by npcPlayer.

Avoid creating a region where an NPC repeatedly enters its own `once=false` trigger, or the same TAS may continuously restart from its first frame.

## 9. Change npcPlayer Respawn

Lönn trigger ID:

```text
npcPlayer/changeNpcPlayerRespawn
```

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `npcId` | string | Case-sensitive target NPC ID |
| Node | coordinate | Exactly one Node is required |

When the real Player enters the trigger, npcPlayer:

1. Finds every spawn point in the room whose `npcId` is an exact match.
2. Selects the spawn point nearest the Node.
3. Saves that selection as the NPC's respawn position for this room.

It does not immediately teleport a living NPC. The new position takes effect the next time the room reloads, such as after a death retry. After leaving the room, another room starts with its own default spawn-point rules.

This trigger does not currently implement `INpcPlayerTrigger`, so only the real Player can activate it.

## 10. Hot-Reload Workflow

During development, install the map mod as an unpacked directory, for example:

```text
Celeste/Mods/YourMod/
```

Everest does not live-watch files inside ZIP archives, so a compressed map package is not suitable for hot-reload testing.

### TAS Hot Reload

Every successful entry into an `Activate npcPlayer` trigger reads and parses the current TAS asset again:

- Adding or editing a TAS does not require restarting Celeste.
- A TAS already in progress keeps the snapshot with which it started and does not change mid-playback.
- Activate the trigger again to run the edited file. If a `once=true` trigger has already succeeded in this room, reload the room first; use `once=false` temporarily during frequent iteration.
- Deleting or breaking a TAS does not affect the old snapshot already playing, but the next activation fails and writes an error to the log.

### NPC Configuration Hot Reload

Editing `config/npcPlayer/npcPlayer.yaml` invalidates the map mod's cached configuration:

- A full Celeste restart is not required.
- Existing NPCs do not change appearance or death links in place.
- The new configuration is applied on the next room load, death retry, or leave and re-entry.
- NPCs are not rebuilt in place because doing so would discard their current position, held object, and TAS state.

## 11. Interaction and Compatibility Boundaries

### Terrain and Holdables

npcPlayers use vanilla Player physics and can interact with `Solid`, `JumpThru`, and common moving platforms. The compatibility layer supplements selected vanilla behavior that finds only the real Player through `Tracker<Player>`.

If an NPC and the real Player compete for the same Holdable, the first successful holder owns it; the other Player cannot take it directly from that holder.

### Triggers

Ordinary vanilla and third-party `Trigger` entities ignore NPCs by default. Vanilla `Trigger.Triggered` and `PlayerIsInside` are single-Player state, so sharing them among multiple Player instances would produce incorrect `OnEnter`, `OnStay`, and `OnLeave` transitions.

npcPlayer runs an independent trigger state machine for NPCs. A third-party mod whose trigger is safe for NPCs can opt in by implementing:

```csharp
using Celeste;
using Celeste.Mod.Entities;
using Celeste.Mod.NpcPlayer.Runtime;
using Microsoft.Xna.Framework;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

public sealed class MyTrigger : Trigger, INpcPlayerTrigger
{
    public MyTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
    }

    public bool AllowsNpcPlayer(NpcPlayerEntity player) => true;
}
```

During each callback, npcPlayer temporarily exposes Trigger state matching vanilla callback semantics and restores it afterward. Implementations must not cache these shared fields or depend on them outside the callback.

### PlayerCollider

Selected vanilla interactions are explicitly allowed, including common hazards, springs, boosters, bumpers, feathers, and refills. Collectibles, doors, story entities, and progression entities ignore NPCs by default to prevent changes to Session or story state.

Third-party entities are not automatically allowed merely because they inherit from a vanilla type. Once the interaction has been confirmed safe, the entity should explicitly implement:

```csharp
using Celeste.Mod.NpcPlayer.Runtime;
using Monocle;
using NpcPlayerEntity = Celeste.Mod.NpcPlayer.Entities.NpcPlayer;

public sealed class MyEntity : Entity, INpcPlayerCollider
{
    public bool AllowsNpcPlayer(NpcPlayerEntity player) => true;
}
```

The interface only decides whether the entity's `PlayerCollider` callback may run. The entity remains responsible for the actual collision behavior.

## 12. Global State Isolation

While an NPC is updating:

- It uses a separate virtual input device with no real controller bindings.
- It cannot move the camera.
- It cannot change the global underwater music state.
- It does not produce controller rumble.
- Synchronous global Freeze requests are discarded.
- Spatial sound effects attributed to the NPC use its straight-line distance from the real Player: full volume through 64 pixels, then smooth attenuation to silence at 320 pixels.

Attached looping `SoundSource` components update this gain as the NPC moves. Positionless music, ambience, snapshots and UI audio are excluded, and the real Player's audio is never modified. If no real Player can be identified, npcPlayer leaves the original sound volume unchanged.

One NPC keeps its normal internal sound mix, even when several of its legitimate events overlap. Crowd limiting begins only when multiple distinct npcPlayer owners are audible: their distance gains share a budget equivalent to 1.2 full-volume owners. Owners playing the same event path additionally share one full-volume event budget, and only the four closest owners for that event remain audible. A repeated `Player.Play` call with the same NPC and event path in one frame is still muted as a duplicate. Reductions apply immediately; gain recovers over roughly 80 ms when competing NPCs end, avoiding an abrupt volume jump.

A global Freeze initiated by the game or the real Player still pauses the scene, including the NPC TAS cursor. Feedback created later by a `PlayerDeadBody` is outside ordinary NPC-update isolation.

## 13. Death and Reload

Death propagation is a directed star centered on the real Player:

- A real Player death kills each living NPC whose `lead_by_player` value is `true`.
- An NPC death kills the real Player only when that NPC's `lead_to_player` value is `true`.
- When an NPC kills the Player, that Player death can then reach another NPC only through the second NPC's `lead_by_player` link. There are no direct NPC-to-NPC death links.
- An NPC with `lead_to_player: false` dies independently and is not automatically respawned during the current room lifetime.
- Only a real Player death records statistics and owns the final screen wipe and room reload. NPC death bodies finish their visual effect without reloading the room.

If the real Player reloads the room, every NPC is recreated from room spawn-point data even when its `lead_by_player` link was disabled. The disabled link suppresses that NPC's death event and visual body; it does not preserve the runtime actor across a room reload.

For the technical preview, custom skins, simultaneous death of multiple NPCs, and combinations with third-party death hooks should still receive focused testing. Include the skin name and `log.txt` when reporting unexpected visuals.

## 14. Troubleshooting

### No NPC Appears in the Room

Check that:

1. The map mod depends on `npcPlayer 0.2.2`.
2. The room contains `npcPlayer (Spawn Point)`.
3. The Lönn entity ID is still `npcPlayer/npcPlayerSpawnPoint`.
4. `log.txt` does not report a duplicate ID, appearance configuration problem, or map-source error.

### A Trigger Cannot Find the NPC

`npcId` is case-sensitive. Confirm that the spawn point, appearance configuration, and trigger use the exact same value, and that the target NPC has a spawn point in the current room.

### The TAS Does Not Play

Check that:

- `tas` is relative to the map mod root.
- The file belongs to the same mod that contains the current map.
- The path uses forward slashes and a `.tas` extension.
- The file does not contain unsupported CelesteTAS commands, segment labels, or zero/negative frame counts.
- `log.txt` does not report a specific file and line-number error.

### Editing the TAS Has No Effect

A TAS already in progress is not replaced in place. Enter a `once=false` trigger again or start it from another trigger. If the original `once=true` trigger has already succeeded, reload the room first. The map mod must be installed as an unpacked directory because ZIP contents are not hot-reloaded.

### Editing the NPC Configuration Has No Effect

Appearance and death-link configuration is applied only when an NPC is created. Retry after death, re-enter the room, or reload it by another method before checking the result.

### An NPC Does Not Activate a Third-Party Entity

If the entity is a Trigger, the third party must implement `INpcPlayerTrigger`. If it relies on `PlayerCollider`, it must implement `INpcPlayerCollider`. This is normally a safety boundary, not evidence that the NPC did not overlap the entity.

### Can an NPC Change Story or Collection Progress?

Not by default. Collectibles, doors, story entities, and progression-related `PlayerCollider` entities are not on the allowlist, and ordinary triggers do not respond to NPCs. When a third party explicitly opts in, that implementation is responsible for the side effects of its callback.

## 15. Reporting Problems

Technical-preview reports should include at least:

- Celeste version.
- Everest version.
- npcPlayer version.
- SkinModHelperPlus version and `SkinName`, when applicable.
- Names and versions of relevant Helpers.
- Complete `log.txt`.
- A minimal reproduction room or map.
- The corresponding TAS and `config/npcPlayer/npcPlayer.yaml`.
- Expected behavior, actual behavior, and deterministic reproduction steps.

If a problem occurs only with one third-party entity, state whether it uses a Trigger, PlayerCollider, Solid, Holdable, or another custom interaction mechanism.

## 16. Copyable Example

The repository's `examples/YourMod/` directory provides a minimal directory layout, `everest.yaml`, appearance configuration, and TAS sample. After copying it, replace the mod name, map path, NPC IDs, and skin names. Do not publish it unchanged as `YourMap`.
