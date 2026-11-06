# NAV --- Video capture

Editor-only tools for devlog footage. They never ship: `Scripts/Dev/VideoCapture.cs` is wrapped
in `#if UNITY_EDITOR`, `Scripts/Editor/NavVideoRecorder.cs` lives in an Editor folder, and no game
code references either. Requires `com.unity.recorder` (added 2026-10-31).

## Running a queue

Open `Assets/Scenes/WorldGenTest.unity`, stop Play Mode, then from Unity MCP `execute_code` (or any
editor script):

```csharp
NAV.Editor.NavVideoRecorder.Start("id=wolf;biome=Plains;minR=60;hour=10;spawn=ForestWolf:1:10;cam=orbit;recLen=8||id=sea;...");
```

Clips are separated by `||`. Each clip enters Play Mode, starts a new throwaway world, waits for
the terrain, records, leaves Play Mode and deletes that world's save. The developer's saves are not
touched. Files land in `F:/Dev/RebonkVideo/public/footage/nav/<id>.mp4`.

- `NavVideoRecorder.LogText` - what happened; `NavVideoRecorder.Abort()` - stop after the current clip.
- Recording is frame-locked at 60 fps: a slow PC takes longer, the clip stays smooth.
- Script changes wait until Play ends while a queue runs (the setting is restored after).
- Do not use the editor while a queue runs: a clip takes over Play Mode.

## Clip keys

| Key | Default | Meaning |
|---|---|---|
| `id` | `clip` | File name |
| `seed` | `1337` | World seed |
| `at` | | Exact spot `x,z` |
| `biome` | | Nearest land of this biome: Plains, BirchGrove, Forest, DenseForest, SeaIslands, Swamp, Mountains, SnowPeaks, Permafrost, Desert. `Water` = open water |
| `maxH` | | With `biome`: lowest ground only, e.g. `maxH=2` for a beach |
| `arsenal` | `0` | `1` = every weapon of the pack laid out ahead; `arsenalScale` sets their size |
| `minR` | `0` | Search at least this far from the spawn. Use 60+ with creatures: there is a safe zone around the spawn |
| `face` | `0` | Player's heading, degrees |
| `hour` | | Holds the clock at this hour (0-24) |
| `weather` | | `Clear`, `Rain`, `HeavyRain`, `Thunderstorm` |
| `hold` | | Item asset in hand, e.g. `StoneSwordItem`, `StoneAxe`, `WoodHammer` |
| `spawn` | | `Prefab:count:metres in front`: ForestWolf, Deer, BogLurker, SnowLeopard |
| `cam` | `orbit` | `orbit` or `follow` (the game's own camera) |
| `dist`, `dist2` | `6` | Camera distance, start and end |
| `h`, `h2` | `2` | Camera height, start and end |
| `yaw` | `0` | Camera direction |
| `orbit` | `0` | Degrees per second around the player |
| `look` | `1.2` | Height of the look-at point |
| `pitch` | `12` | `follow` only |
| `fov` | | Field of view |
| `fog` | `1` | `0` = no view-distance fog, needed for high shots |
| `far` | | Camera far plane in metres, for high shots with `fog=0` |
| `track` | `1` | `0` = the camera stays on the starting place, the player leaves it |
| `walk` | | Walks in this world heading, degrees |
| `sprint` | `0` | `1` = sprint |
| `swing` | | Seconds between attacks |
| `fight` | | Metres. With `spawn` and `swing`: the hero stays turned to the first creature, steps up to this distance and swings only from there. 1.6 suits the sword and the ForestWolf. Each hit goes into `NavVideoRecorder.LogText` with its time |
| `lift` | | Metres. With `walk` and a `sphere` prop: stops this far from the ball, squats, lifts it overhead. A staged reconstruction: the game has no pick-up animation. 0.55 with `ballSize=0.6` and height `-0.7` |
| `ballSize` | `1` | Diameter of the `sphere` prop, with `lift` |
| `hourSpeed` | `0` | Game hours per second during the clip, with `hour` |
| `near` | | `Name:gap` stands the player by the nearest resource node, facing it |
| `props` | | Set dressing: `Build/Forge:right:ahead:yaw:height`, separated by `\|`. Prefab under Assets/NAV/Prefabs, or `capsule`, `sphere` |
| `level` | `0` | `1` = all props on one height, the highest ground under them. Needed for a building: its pieces stay aligned |
| `calm` | `0` | `1` = spawned creatures stand still |
| `hp` | | Player's health at the clip's start, with `mortal=1` |
| `hide` | `0` | `1` = the player is invisible |
| `pink` | `0` | `1` = reconstruction of the pink rain |
| `hud` | `0` | `1` = keep the interface |
| `mortal` | `0` | `1` = the player can die |
| `recLen` | `8` | Seconds |
| `settle` | `6` | Real seconds to wait after the terrain is ready |
| `audio` | `1` | `0` = no game sound |
| `width`, `height` | `3840`, `2160` | Output size |

Keep the action in the centre of the frame: Shorts are cropped from these clips.
