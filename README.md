# npcPlayer

Everest mapping helper that creates Player-compatible NPC actors from map spawn points and drives them with packaged direct-input TAS files. CelesteTAS is a development tool only and is not a runtime dependency.

> **Technical preview:** behavior and compatibility interfaces may change before a stable release. Please report reproducible problems with `log.txt`, environment versions, and a minimal map or TAS through the repository bug report form.

Detailed documentation: [English usage guide](docs/en-US/usage.md) | [简体中文使用手册](docs/zh-CN/usage.md)

## Lönn installation

Place the release ZIP directly in `Celeste/Mods/`, then fully restart Lönn. Search the entity list for `npcPlayer (Spawn Point)` and the trigger list for `Activate npcPlayer` or `Change npcPlayer Respawn`. If Lönn is filtering by map dependencies, add `npcPlayer` to the map mod's `everest.yaml` dependencies.

## Map objects

### npcPlayer (Spawn Point)

Entity ID: `npcPlayer/npcPlayerSpawnPoint`

- `npcId`: exact, case-sensitive logical NPC identity.
- `default`: whether this point should be preferred as the initial respawn point for this `npcId` in this room.

Lönn provides one placement named `npcPlayer (Spawn Point)`, rendered with `characters/player_badeline/sleep00`; newly placed points use `default=false`.

A room containing Spawn Points creates one runtime `npcPlayer` actor per unique `npcId`. For each ID, runtime uses the first `default=true` Spawn Point in map-data order. If none are marked default, it uses the first Spawn Point in map-data order. Multiple defaults are left unchanged and only the first is used.

### Activate npcPlayer

Trigger ID: `npcPlayer/activateNpcPlayer`

- `npcId`: exact target NPC ID.
- `tas`: TAS path relative to the current map mod root, for example `Tas/Author/Map/action.tas`.
- `once`: consume the trigger after one successful activation.
- `playerOnly`: when true only the real Player activates it; when false both the real Player and npcPlayers can activate it.

A successful activation interrupts any current TAS and starts the new file from frame one.

### Change npcPlayer Respawn

Trigger ID: `npcPlayer/changeNpcPlayerRespawn`

- `npcId`: exact target NPC ID.
- One required Node.

The trigger filters Spawn Points by exact `npcId`, then selects the matching point nearest its Node. This persisted same-room selection takes priority over the initial `default` rule on the next reload. It does not teleport a living NPC.

## Map-side NPC config

Optional file in the map mod:

```text
config/npcPlayer/npcPlayer.yaml
```

```yaml
npcPlayer:
  - npcId: "partner"
    variant: "madeline"
    death_link:
      lead_by_player: true
      lead_to_player: false
  - npcId: "rival"
    variant: "MySmhPlusSkinName"
```

IDs are case-sensitive. Missing entries use `badeline`; `death_link` values default to `true`. `lead_by_player` controls the `Player -> NPC` death edge, while `lead_to_player` controls the `NPC -> Player` edge. NPC-to-NPC propagation only occurs through the real Player: an NPC that kills the Player can kill another NPC only when that second NPC has `lead_by_player: true`.

Built-ins are `madeline` and `badeline`; other `variant` values are raw `SkinName` values from `SkinModHelperConfig.yaml`. Missing SMH+ skins log a warning and fall back to Badeline. Duplicate config IDs use the first valid entry. Missing or invalid death-link booleans individually fall back to `true` without discarding a valid variant. The v0.2.0 layout remains accepted and gives every legacy entry both default links:

```yaml
- npcPlayer:
    npcId: "partner"
    variant: "madeline"
```

SMH+ remains optional. Its compatibility contract is bound and validated once per npcPlayer module lifetime; if a future SMH+ release changes the private sprite metadata caches, npcPlayer logs one versioned warning and keeps the fully SMH+-managed sprite instead of producing a partially converted sprite.

## Compatibility

> **Known conflict — PandorasBox:** PandorasBox and npcPlayer may have numerous additional conflicts. Compatibility testing and fixes are still in progress. Unless a map specifically requires both mods, do not enable PandorasBox and npcPlayer at the same time.

## Supported TAS input

Only positive direct frame-input lines are accepted:

```tas
30,R
1,R,J
20,R
1,R,X
15,R,G
```

Supported tokens: `L R U D`, `J K` (jump), `X C` (dash), `G H` (grab), `Z V` (crouch dash). Blank lines and ordinary comments beginning with `#` or `//` are ignored. Legacy `#npcPlayer...` segment headers are rejected instead of being silently concatenated. CelesteTAS commands, `Read`, `Repeat`, `RecordCount`, segment labels, analog input and savestate commands are unsupported.

Use namespaced paths such as `Tas/<Author>/<Map>/<Action>.tas`. Absolute paths, drive paths, `.` and `..` components are rejected.

TAS input is stored and executed as run-length encoded segments rather than expanded into one object per frame. A file may contain at most 100,000 direct-input segments and 10,000,000 total frames.

## Development hot reload

For live TAS and NPC-config editing, install the map mod as an unpacked directory such as `Celeste/Mods/YourMod/`. Everest watches unpacked mod files for additions, edits, renames, and deletions; ZIP contents are not live-watched.

- Each successful `Activate npcPlayer` reads and parses the current TAS asset. A TAS already in progress keeps the snapshot with which it started; activate it again to run the edited file.
- Editing `config/npcPlayer/npcPlayer.yaml` invalidates that map mod's cached NPC config. New variants and death links are applied the next time the room loads, including a death/retry or leave and re-enter, without restarting Celeste. Existing runtime NPCs are not rebuilt in place because doing so would discard their movement, held object, and TAS state.

## Runtime behavior

- NPC input uses a separate unbound virtual input device.
- npcPlayers use an independent trigger state machine. Ordinary Celeste and third-party triggers ignore NPCs by default, so they cannot steal the real Player's shared `Triggered` / `PlayerIsInside` state.
- `Activate npcPlayer` explicitly opts into NPC callbacks when `playerOnly` is false. Third-party triggers can opt in by implementing `INpcPlayerTrigger`; callback-visible vanilla trigger flags are virtualized and restored after each callback.
- Common Player physics and PlayerCollider interactions run through the original Player update.
- PlayerCollider callbacks are filtered: vanilla hazards, springs, boosters, bumpers, feathers and refills work; collectibles, doors, story and progression objects ignore NPCs. Third-party entities must implement `INpcPlayerCollider` to opt in.
- NPC updates cannot move the camera, change the global underwater music state, or produce controller rumble. PlayerDeadBody animation feedback runs later and is intentionally unaffected by this update-only suppression.
- Freeze calls made synchronously during NPC update are discarded. Global freezes still pause the scene and TAS cursor.
- Real Player death runs the original death animation and radial `DeathEffect` only for live npcPlayers whose `lead_by_player` link is enabled.
- An NPC whose `lead_to_player` link is enabled kills the real Player at end of frame. The Player then propagates death only to other NPCs whose `lead_by_player` link is enabled; NPCs never link directly to one another.
- An NPC whose `lead_to_player` link is disabled dies independently and is not automatically respawned. NPC death bodies are visual-only and never own the screen wipe, reload, or real death statistics.
- Respawn selection persists across same-room death/reload, then resets to the new room's initial Spawn Point selection on room change.
- Same-room use only; NPC room transitions are not implemented.

## Map package layout

```text
YourMod/
  Maps/<Author>/<Map>.bin
  Tas/<Author>/<Map>/<Action>.tas
  config/npcPlayer/npcPlayer.yaml
  everest.yaml
  Dialog/
  Graphics/
```

The map mod should depend on `npcPlayer`. It only needs a dependency on `SkinModHelperPlus` when it uses a custom SMH+ `SkinName`.

## Build

```powershell
.\build.ps1 -CelesteDir "C:\Path\To\Celeste"
```

The build script also synchronizes `build/npcPlayer.dll` and its PDB into `Code/` for packaging.

Create an installable archive (with `everest.yaml` at ZIP root):

```powershell
.\package.ps1 -CelesteDir "C:\Path\To\Celeste"
```

The archive version is read from `everest.yaml` and written to `dist/npcPlayer-<version>.zip`; source and local build output are excluded.

## License

npcPlayer is available under the [MIT License](LICENSE).
