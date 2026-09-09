# NAV --- Development Roadmap v0.1

## Phase 0 --- Pre-production

Goal: establish the project before writing large amounts of gameplay
code.

-   [ ] Lock Unity version
-   [ ] Lock render pipeline
-   [ ] Create Unity project
-   [ ] Create repository/version control
-   [ ] Create initial folder structure
-   [ ] Add CLAUDE.md
-   [ ] Add PROJECT_STATE.md
-   [ ] Add architecture documentation
-   [ ] Create bootstrap scene
-   [ ] Create test scene

------------------------------------------------------------------------

## Phase 1 --- Player Foundation

Goal: make the character feel good.

-   [x] Third-person movement
-   [x] Camera
-   [x] Rotation
-   [x] Sprint
-   [x] Stamina
-   [x] Jump
-   [x] Basic gravity
-   [x] Character stats
-   [ ] Animation placeholders
-   [x] Interaction raycast/system

------------------------------------------------------------------------

## Phase 2 --- Items and Inventory

Goal: establish the data architecture used by almost every future
system.

-   [x] ItemDefinition
-   [x] Item IDs
-   [x] Item instances/runtime data
-   [x] Stack rules
-   [x] Inventory
-   [ ] Inventory UI
-   [x] Item pickup
-   [ ] Item dropping
-   [ ] Storage container
-   [ ] Equipment slots

------------------------------------------------------------------------

## Phase 3 --- Gathering

-   [ ] Resource nodes
-   [ ] Resource health/state
-   [ ] Axe/tool interaction
-   [ ] Mining interaction
-   [ ] Drops
-   [ ] Pickup
-   [ ] Resource persistence
-   [ ] Basic gathering feedback

------------------------------------------------------------------------

## Phase 4 --- Crafting and Workbench

-   [ ] RecipeDefinition
-   [ ] Recipe UI
-   [ ] Crafting requirements
-   [ ] Workbench
-   [ ] Workbench levels
-   [ ] Workbench placement
-   [ ] Recipe unlock structure
-   [ ] Basic tools
-   [ ] Basic weapons
-   [ ] Basic armor

------------------------------------------------------------------------

## Phase 5 --- Building

-   [x] Building piece data
-   [x] Placement preview
-   [x] Snapping (basic: nearest-point position snap only, see PROJECT_STATE.md)
-   [x] Rotation
-   [x] Placement validation (basic: range-only, see PROJECT_STATE.md)
-   [x] Build
-   [x] Remove/demolish
-   [ ] Save building state
-   [ ] Basic structural validation

------------------------------------------------------------------------

## Phase 6 --- Survival Systems

-   [ ] Health
-   [ ] Stamina
-   [ ] Food
-   [ ] Food effects
-   [ ] Regeneration
-   [ ] Environmental effects
-   [x] Death (shared with Phase 7's own Death item - same PlayerHealth/PlayerDeath)
-   [x] Gravestone
-   [x] Respawn (respawn-in-place only; see PROJECT_STATE.md for the exact scope)

------------------------------------------------------------------------

## Phase 7 --- Combat

-   [x] Damage system
-   [x] Hit detection
-   [x] Melee attacks
-   [x] Weapon definitions
-   [x] Blocking
-   [x] Parrying
-   [x] Stamina interaction
-   [ ] Damage feedback (console/HUD only - no VFX/audio/hit reaction; see PROJECT_STATE.md)
-   [x] Death (player: freeze + respawn-in-place + Gravestone, see Phase 6; CombatDummy: stops attacking/taking damage)
-   [x] Loot/drop (CombatDummy only - real creature loot is Phase 8)

------------------------------------------------------------------------

## Phase 8 --- Creatures and AI

-   [x] Creature data
-   [x] Health
-   [x] Perception (trigger-radius detection only, no line-of-sight raycast; see PROJECT_STATE.md)
-   [x] Target selection (single nearest-in-trigger target, no multi-target prioritization)
-   [x] Patrol
-   [x] Chase
-   [x] Attack
-   [x] Search
-   [x] Return
-   [x] Death
-   [x] Loot
-   [x] One neutral animal (Wild Boar - flees when attacked)
-   [x] One hostile creature (Forest Wolf - chases/attacks on sight)

------------------------------------------------------------------------

## Phase 9 --- World

-   [x] Seed system
-   [x] Deterministic random
-   [x] Terrain generation (single bounded Unity Terrain, no chunk/streaming; see PROJECT_STATE.md Known Risk #1)
-   [x] Ocean (sea-level water plane placeholder, no waves/shader)
-   [x] Plains
-   [x] Forest (ground texture only - no distinct vegetation/trees yet, see PROJECT_STATE.md)
-   [ ] Birch grove
-   [ ] Dense forest
-   [ ] Resource distribution
-   [ ] Vegetation distribution
-   [ ] Spawn regions (no guaranteed-dry player spawn point yet; see PROJECT_STATE.md)
-   [ ] Points of interest

------------------------------------------------------------------------

## Phase 10 --- Time and Weather

-   [ ] Day/night
-   [ ] Lighting
-   [ ] Sky
-   [ ] Weather state machine
-   [ ] Clear weather
-   [ ] Rain
-   [ ] Heavy rain
-   [ ] Thunderstorm
-   [ ] Ambient audio changes

------------------------------------------------------------------------

## Phase 11 --- Save/Load

-   [x] Player save (position/rotation, health, inventory contents)
-   [x] Inventory save (part of Player save above - same JSON, same slot indices)
-   [ ] Equipment save (no Equipment system exists yet - Phase 2/4 slots still unchecked)
-   [ ] Skill save (no Skills system exists yet)
-   [x] World seed save (world is regenerated from the saved seed on load, not stored as terrain data - see PROJECT_STATE.md's "known limitation" note on generation-settings changes invalidating old saves)
-   [ ] World changes (which resource nodes were gathered/depleted is NOT saved - every load respawns all resources fresh from the seed; deliberate Increment 1 scope cut, see PROJECT_STATE.md)
-   [x] Building save (placed pieces only - position/rotation/definition; no structural/snap state beyond that)
-   [ ] Container save (no storage containers/chests exist yet)
-   [ ] Time/weather save (Phase 10 not started)
-   [ ] Save versioning (known gap - a WorldGenerationSettings change after a save was made silently desyncs old saves' terrain from their stored building/player coordinates; see PROJECT_STATE.md)
-   [ ] Backup/recovery

------------------------------------------------------------------------

## Phase 12 --- Vertical Slice

Goal:

A small but complete piece of NAV that feels like the actual game.

The player should be able to:

`Spawn → Gather → Build → Craft → Explore → Fight → Eat → Die → Recover → Save → Reload`

Do not proceed to massive content production until this loop is stable.

------------------------------------------------------------------------

## Phase 13 --- Content Production

After the vertical slice is proven:

-   [ ] Expand biomes
-   [ ] Expand resources
-   [ ] Expand weapons
-   [ ] Expand armor
-   [ ] Expand food
-   [ ] Expand creatures
-   [ ] Expand building pieces
-   [ ] Expand workbench tiers
-   [ ] Add boats
-   [ ] Add atmospheric content
-   [ ] Add VFX
-   [ ] Add final audio
-   [ ] Improve world generation

------------------------------------------------------------------------

## Phase 14 --- Boss System

Only after the underlying progression is stable.

-   [ ] Boss framework
-   [ ] Summoning altar
-   [ ] Ritual items
-   [ ] Boss arenas
-   [ ] Boss AI extensions
-   [ ] Boss phases
-   [ ] Boss rewards
-   [ ] Progression unlocks
-   [ ] 7--10 final bosses over the full production cycle

**Boss identities and mechanics are intentionally undefined at v0.1.**

------------------------------------------------------------------------

## Phase 15 --- Multiplayer

Target: 1--4 player cooperative multiplayer.

-   [ ] Choose networking technology
-   [ ] Define authority model
-   [ ] Network player state
-   [ ] Network inventory
-   [ ] Network combat
-   [ ] Network creatures
-   [ ] Network construction
-   [ ] Network containers
-   [ ] Network boats
-   [ ] Network save/load
-   [ ] Host/join flow
-   [ ] Disconnect/reconnect handling

------------------------------------------------------------------------

## Phase 16 --- Polish and Release

-   [ ] Optimization
-   [ ] UI polish
-   [ ] Input rebinding
-   [ ] Accessibility
-   [ ] Audio polish
-   [ ] VFX polish
-   [ ] Animation polish
-   [ ] Tutorial/onboarding
-   [ ] Settings
-   [ ] Graphics settings
-   [ ] Save recovery
-   [ ] Crash/error reporting
-   [ ] Final balancing
-   [ ] QA
-   [ ] Release build

------------------------------------------------------------------------

# Golden Rule

Do not build the whole game before proving the core loop.

NAV should grow as a sequence of working milestones, not as one enormous
unfinished system.
