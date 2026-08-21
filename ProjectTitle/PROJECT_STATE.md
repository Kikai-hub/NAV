# NAV --- Project State

## Current Version

`v0.1 — Pre-production`

## Current Phase

**Design / Architecture / MVP Planning**

## Project Status

The project concept and high-level design are defined.

Unity implementation has not yet been finalized.

------------------------------------------------------------------------

## Confirmed

-   Project name: NAV
-   3D survival
-   PC
-   Third-person camera
-   Low-poly visual direction
-   Fairy-tale Ancient Rus
-   Large procedural world
-   Seed-based generation
-   Islands and large land masses
-   Initial biomes: ocean, plains, dense forest, birch grove, ordinary
    forest
-   Day/night cycle
-   Clear weather
-   Rain
-   Heavy rain
-   Thunderstorm
-   Gathering
-   Crafting
-   Building
-   Workbench progression
-   Equipment
-   Food system
-   Skills inspired by Valheim
-   Combat based on hit detection
-   Shields
-   Blocking/parrying
-   Neutral and hostile creatures
-   Basic AI
-   Boats/rafts planned
-   Character customization planned
-   Death creates a gravestone containing the player's carried loot
-   Resources are not infinitely regenerated
-   1--4 player cooperative multiplayer is the long-term target
-   MVP is single-player
-   NPCs are not part of the initial version
-   7--10 bosses planned for the full game
-   Bosses are intentionally not finalized
-   Unity Editor version locked: 6000.5.2f1 (Unity 6)
-   Render pipeline locked: Universal Render Pipeline (URP) 17.5.0, using the
    default 3D (URP) template's PC_RPAsset/PC_Renderer as the active pipeline
    asset
-   Input: Unity Input System (com.unity.inputsystem 1.19.0); reusing the
    template's InputSystem_Actions.inputactions "Player" action map rather
    than creating a new input asset; no generated C# wrapper class (actions
    are looked up by name at runtime via PlayerInputHandler)
-   Assets/NAV folder structure created per ARCHITECTURE_v0.1.md, with two
    additions: Scripts/Presentation/Camera (camera code needed a home; the
    original tree only named "Presentation" as a conceptual layer, not a
    folder) and ScriptableObjects/Player (no category existed for
    character/movement stat data)
-   Player Foundation increment 1 complete: third-person camera-relative
    movement, jump, gravity, sprint (speed multiplier only, no stamina
    cost/regen yet), body rotation toward movement direction; driven by
    PlayerMovementStats ScriptableObject; verified via PlayerDebugHud
    on-screen readout (Grounded/Speed/Sprinting/Move Input) in SampleScene

------------------------------------------------------------------------

## Current MVP Target

1.  Unity project foundation
2.  Player movement
3.  Camera
4.  Character controller
5.  Interaction
6.  Basic item system
7.  Gathering
8.  Inventory
9.  Basic crafting
10. Workbench
11. Basic building
12. Basic equipment
13. Basic combat
14. One neutral creature
15. One hostile creature
16. Basic day/night
17. Basic weather
18. Save/load
19. Basic death/gravestone

------------------------------------------------------------------------

## Not Yet Decided

-   Networking solution
-   Asset strategy
-   Final biome list
-   Final resource list
-   Final technology tiers
-   Final weapons and armor balance
-   Final creature roster
-   Final boss roster
-   Final boss mechanics
-   Final recipes
-   Final UI
-   Final audio/music
-   Final narrative

------------------------------------------------------------------------

## Boss Status

**NOT FINALIZED**

Do not create final boss content yet.

Use placeholders only.

Future target: - approximately 7--10 bosses; - some recognizable Slavic
mythology figures; - some original creatures; - boss summoning through
altar + ritual items; - bosses tied to progression.

------------------------------------------------------------------------

## Known Risks

1.  Procedural world generation can become too complex too early.
2.  Building can consume excessive development time.
3.  Multiplayer can dramatically increase architectural complexity.
4.  Save/load must be designed carefully from the beginning.
5.  Inventory/item architecture will affect almost every future system.
6.  World resource persistence needs careful optimization.
7.  Large numbers of AI agents can create performance issues.

------------------------------------------------------------------------

## Current Next Step

Player Foundation increment 1 (movement + camera) is complete and manually
verified in SampleScene. Next: continue Phase 1 Player Foundation with
stamina drain/regen for sprint, then move to Phase 2 Interaction
(raycast-based interact system) before touching items/inventory.

------------------------------------------------------------------------

## Change Log

### v0.1

-   Initial project state created.
-   High-level game concept documented.
-   MVP scope defined.
-   Bosses explicitly left unfinished.

### v0.1 --- 2026-08-21

-   Locked Unity version (6000.5.2f1) and render pipeline (URP 17.5.0,
    PC_RPAsset) in PROJECT_STATE.md.
-   Created Assets/NAV folder skeleton (Scripts/{Core,Gameplay,World,
    Presentation,Save,UI,Multiplayer,Editor}, ScriptableObjects/{...}, plus
    Art/Audio/Materials/Prefabs/Scenes/Tests).
-   Implemented Player Foundation increment 1: PlayerInputHandler,
    PlayerMotor, ThirdPersonCameraController, PlayerMovementStats SO,
    PlayerDebugHud. Reuses the existing InputSystem_Actions.inputactions
    "Player" action map (Move/Look/Jump/Sprint) --- no new input asset, no
    generated C# wrapper class.
-   Architecture notes: added Scripts/Presentation/Camera/ (not in the
    original ARCHITECTURE_v0.1.md tree) and ScriptableObjects/Player/ (same);
    both are small, low-risk extensions of the existing layer concepts.
-   Stamina cost for sprint, the interaction raycast system, animation, and
    inventory remain explicitly out of scope for this milestone.
-   Manual Unity Editor wiring (Player GameObject, CharacterController,
    Ground plane, CameraTarget, Main Camera assignment) still needs to be
    performed by the human developer in SampleScene --- see instructions
    given alongside this milestone.
-   Fix (playtest feedback): PlayerMotor no longer rotates the body toward
    the raw camera-relative move-input direction (was causing the character
    to snap-turn on strafe, read by the developer as "the camera is tied to
    movement"). Body yaw now tracks the camera's yaw directly every frame;
    the camera itself remains fully independent (mouse-controlled, follows
    only the player's position). Strafing (A/D) now moves sideways without
    reorienting the character.
-   Investigated a reported "camera pitch doesn't work" issue at length.
    Root cause was twofold: (1) the initial Min/Max Pitch range (-30/70) was
    easy to pin at its "look up" limit during repeated one-directional
    testing, making further input look like a no-op; widened defaults to
    -60/80 in ThirdPersonCameraController. (2) The developer
    was checking CameraTarget's own rotation as if it were the camera --- it
    isn't a Camera, it's a position-only pivot child of Player, and by
    design only yaws (matches the player body's yaw), never pitches. The
    actual Main Camera (holding ThirdPersonCameraController) was confirmed
    by the developer to rotate correctly on all axes. No code bug; milestone
    verified working end-to-end.
-   Actual root cause (found after the above): a Camera + AudioListener had
    accidentally been added directly to CameraTarget (child of Player) while
    following the setup steps. Since CameraTarget inherits Player's yaw-only
    body rotation, that stray camera only ever appeared to pitch
    horizontally, and it was apparently the one being rendered to the Game
    view instead of the real Main Camera. Fixed by removing the stray
    Camera/AudioListener from CameraTarget (it is now a plain Transform
    again, position-only, as designed). Developer confirmed vertical look
    now works correctly in the Game view. Player Foundation increment 1 is
    fully verified end-to-end.
-   Player Foundation increment 2: sprint stamina. Added PlayerStaminaStats
    (max stamina, drain/sec while sprinting, regen/sec, regen delay after
    sprint stops, minimum stamina required to resume sprinting after
    hitting zero) and PlayerStamina (drives current stamina + CanSprint
    hysteresis). PlayerMotor now asks PlayerStamina.TickSprint() each frame
    instead of just checking the Sprint button, so sprint automatically
    cuts out at 0 stamina and can't restart until stamina recovers past the
    minimum threshold. Exposed on PlayerDebugHud for verification. Not yet
    done: no stamina cost for anything besides sprinting (jump/attack
    stamina costs are future work), no UI stamina bar (still OnGUI debug
    only --- real UI is Inventory-phase work).
