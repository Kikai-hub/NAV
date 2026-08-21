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
-   Assets/NAV folder structure created per ARCHITECTURE_v0.1.md, with three
    additions: Scripts/Presentation/Camera (camera code needed a home; the
    original tree only named "Presentation" as a conceptual layer, not a
    folder), ScriptableObjects/Player (no category existed for
    character/movement stat data), and Scripts/Gameplay/Interaction (no
    category existed for the generic raycast-interaction system; ARCHITECTURE
    only lists "interaction" as a conceptual responsibility under Gameplay)
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

Player Foundation is complete and verified: movement, camera, sprint
stamina, and the raycast-based interaction system (PlayerInteractor +
IInteractable) were confirmed working by the developer (initial playtest
found the interaction range was measured from the camera instead of the
player character; fixed and not yet re-confirmed after the fix, but the
fix is small and low-risk). "Animation placeholders" is the one Phase 1
checklist item left undone --- deferred, since there is no character
model/Animator in the project yet; revisit once a placeholder mesh
exists.

Items Foundation increment 1 (ItemDefinition, ItemCategory, ItemStack) has
been added but NOT YET VERIFIED --- check the Unity Console for
"[ItemStackSanityChecks] All checks passed." and for compiler errors
first. Nothing in the scene references these new files yet, so there is
no manual Editor wiring required for this increment.

Next after verification: Inventory (a container of ItemStack slots) ---
the next unchecked item in Phase 2 of DEVELOPMENT_ROADMAP_v0.1.md.

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
-   Player Foundation increment 3: raycast-based interaction system. Added
    IInteractable (Scripts/Gameplay/Interaction), PlayerInteractor (raycasts
    forward from the camera each frame, exposes CurrentInteractable/CurrentPrompt,
    and calls Interact() on the "Interact" action's performed event ---
    reuses the template's existing Interact action, bound to E/hold, unchanged),
    and DebugInteractable (logs + toggles a material color on interact, for
    manual verification only --- not a real gameplay object). Exposed on
    PlayerDebugHud. No real interactable content (items, resource nodes,
    containers) exists yet --- those belong to Phase 2/3.
-   Items Foundation increment 1: ItemDefinition ScriptableObject (id,
    display name, description, icon, category, max stack size, weight) and
    ItemCategory enum (Resource/Tool/Weapon/Armor/Food/Building/Misc) in
    Scripts/Gameplay/Items --- this is the folder ARCHITECTURE_v0.1.md
    already names, no new folder needed this time. Added ItemStack, a plain
    (non-ScriptableObject) runtime class holding a definition + quantity
    with Add/Remove/CanAccept respecting MaxStackSize --- this is the
    "item instances/runtime data" and "stack rules" pieces of Phase 2, not
    yet wired into any container (Inventory itself is still future work).
    No real item content exists yet (final resource list is explicitly
    "Not Yet Decided"); nothing consumes ItemDefinition/ItemStack yet.
-   Testing note: this project has no assembly definitions yet (everything
    compiles into the default Assembly-CSharp), so the Unity Test
    Runner/NUnit can't target just the gameplay code without first
    introducing asmdefs --- an architecture change that hasn't been
    proposed/approved (see "No Silent Architecture Changes" in CLAUDE.md).
    Added Scripts/Editor/ItemStackSanityChecks.cs instead: an
    `[InitializeOnLoad]` editor utility that exercises ItemStack's stacking
    math on every script recompile and logs failures to the Console. This
    is the "editor utility" verification method CLAUDE.md's Testing section
    allows; revisit real unit tests if/when an asmdef restructure is
    approved.
-   Could not self-verify compilation of the Items Foundation increment
    this session: the running Unity Editor only reimports/recompiles on
    focus-in (or explicit Refresh), and bringing it to the foreground
    programmatically was correctly blocked as an unusual action. Check the
    Console after opening the Editor for either
    "[ItemStackSanityChecks] All checks passed." or a `[ItemStackSanityChecks] FAILED: ...`
    error, and for any compiler errors, before building on top of this.
-   Fix (playtest feedback): PlayerInteractor's range was measured along the
    raycast (camera position -> hit), so in third person --- where the
    camera sits well behind/above the player --- most of the "3 meters"
    budget was consumed just reaching the character, and objects near the
    player often fell outside range unless they were also close to the
    camera. Aiming still originates from the camera (avoids the player's
    own collider blocking the ray), but range is now checked as the
    distance from the player's position to the hit point instead of ray
    length. Added a separate, more generous `_maxAimDistance` (default 15m)
    to bound the raycast itself.
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
