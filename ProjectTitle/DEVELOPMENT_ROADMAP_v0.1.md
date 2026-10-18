# NAV --- Development Roadmap v0.1

## Phase 0 --- Pre-production

Goal: establish the project before writing large amounts of gameplay
code.

-   [x] Lock Unity version
-   [x] Lock render pipeline
-   [x] Create Unity project
-   [x] Create repository/version control
-   [x] Create initial folder structure
-   [x] Add CLAUDE.md
-   [x] Add PROJECT_STATE.md
-   [x] Add architecture documentation
-   [x] Create bootstrap scene (MainMenu)
-   [x] Create test scene (SampleScene - sandbox; WorldGenTest is the real game scene)

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
-   [x] Animation placeholders (locomotion only - attack/block/gather clips missing from the imported pack, see PROJECT_STATE.md)
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
-   [x] Inventory UI
-   [x] Item pickup
-   [x] Item dropping (drag out of the Inventory panel -> PlayerInventory.DropItem)
-   [ ] Storage container
-   [x] Equipment slots (main hand only - weapon or tool; armor slots come with armor; 2026-10-15)

------------------------------------------------------------------------

## Phase 3 --- Gathering

-   [x] Resource nodes (ResourceNode + ResourceNodeDefinition; trees, stone, flint)
-   [x] Resource health/state (hits-to-deplete)
-   [x] Axe/tool interaction (trees need an Axe - 2026-10-15)
-   [x] Mining interaction (stone/flint nodes need a Pickaxe - 2026-10-15)
-   [x] Drops (depletion scatters item pickups)
-   [x] Pickup
-   [x] Resource persistence (gathered nodes are saved and stay gone after load - 2026-10-15; no regrowth)
-   [x] Basic gathering feedback (node shrinks per hit)

------------------------------------------------------------------------

## Phase 4 --- Crafting and Workbench

-   [x] RecipeDefinition
-   [x] Recipe UI
-   [x] Crafting requirements (ingredients + RequiredWorkbenchTier)
-   [x] Workbench
-   [ ] Workbench levels (WorkbenchDefinition has a Tier, but only Tier 1 exists - no upgrade path)
-   [x] Workbench placement (WoodWorkbenchPiece - built like any other piece, 2026-10-15)
-   [ ] Recipe unlock structure
-   [x] Basic tools (Stone Axe, Stone Pickaxe, Wood Hammer - no tool gating yet)
-   [x] Basic weapons (Stone Sword item + recipe; axe/pickaxe also have weapon stats; fists fallback)
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
-   [x] Save building state (placed pieces - part of Save Increment 1)
-   [x] Building requires the hammer (2026-10-18): hold the Wood Hammer, RMB enters/leaves build mode (B no longer does), LMB places with a hammer-strike animation
-   [x] Build menu + hammer demolish (2026-10-18): inventory key in build mode opens a Valheim-style piece grid with a Demolish tool; demolish drops the full cost on the ground as pickups (E no longer demolishes)
-   [x] Workbench build area + roof (2026-10-18): pieces need a placed workbench within 20 m (workbench/campfire go anywhere); crafting at a workbench needs a roof over it
-   [x] Show the workbench build radius while building - dashed circle on the ground (2026-10-18)
-   [ ] Basic structural validation

------------------------------------------------------------------------

## Phase 6 --- Survival Systems

-   [x] Health (max HP + food bonus, regen with post-damage delay, death - 2026-10-15)
-   [x] Stamina (max stamina + food bonus)
-   [x] Food (Cooked Meat via Campfire cooking spit; PlayerFood, up to 3 active)
-   [x] Food effects (max HP, max stamina, HP regen for a duration - FoodDefinition)
-   [x] Regeneration (health from food only by default; stamina regen unchanged)
-   [ ] Environmental effects
-   [x] Death (shared with Phase 7's own Death item - same PlayerHealth/PlayerDeath)
-   [x] Gravestone
-   [x] Respawn (respawn-in-place only; see PROJECT_STATE.md for the exact scope)

------------------------------------------------------------------------

## Phase 7 --- Combat

-   [x] Damage system
-   [x] Hit detection (2026-10-17: contact-based - the weapon/fist capsule is swept during the swing's hit window; trees/rocks take gather-hits on contact too)
-   [x] Melee attacks (2026-10-17: per-style attack animations - fists, one-handed L/R combo, two-handed chop, polearm thrust; weapon model in hand)
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
-   [x] Spawn regions (dry/flat player spawn point per seed + respawn there; creatures per biome, 30 m away from it - 2026-10-15)
-   [ ] Points of interest

------------------------------------------------------------------------

## Phase 10 --- Time and Weather

-   [x] Day/night (WorldClock - 24 real minutes per day, sunrise 06:00, sunset 20:00; 2026-10-15)
-   [x] Lighting (one directional light as sun by day / moon by night, trilight ambient)
-   [x] Sky (2026-10-17: Fantasy Skybox FREE FS003 painted skies cross-faded Night/Sunrise/Day/Sunset + overcast sky in rain; sun and moon discs on the light's arc; 8 moon phases, one per night; moving procedural cloud layer driven by wind/weather - first pass, see next item)
-   [ ] **Sky polish (later - developer asked to revisit the sky).** First pass works, known gaps (details in `PROJECT_STATE.md` Technical Debt):
    -   moving clouds are a flat 2D deck that fades out before the horizon; the painted horizon clouds only drift by slow sky rotation
    -   only one sky set (FS003) is wired; no snowy sky (no snow weather yet)
    -   sun/moon are procedural (no moon texture); stars only come from the painted night texture (no twinkle, they drift with the sky rotation)
    -   lightning doesn't light up the clouds; no cloud shadows on the ground
    -   environment reflections are still baked from the static skybox
    -   no quality setting for the sky shader (6 extra texture samples per sky pixel for clouds)
-   [x] Weather state machine (WeatherSystem - weighted transitions, random durations, 30 s blends; 2026-10-15)
-   [x] Clear weather
-   [x] Rain
-   [x] Heavy rain
-   [x] Thunderstorm (lightning flash + delayed thunder)
-   [x] Ambient audio changes (procedural placeholder rain loop + thunder - no real audio assets yet)

------------------------------------------------------------------------

## Phase 11 --- Save/Load

-   [x] Player save (position/rotation, health, inventory contents)
-   [x] Inventory save (part of Player save above - same JSON, same slot indices)
-   [x] Equipment save (held main-hand item)
-   [ ] Skill save (no Skills system exists yet)
-   [x] World seed save (world is regenerated from the saved seed on load, not stored as terrain data - see PROJECT_STATE.md's "known limitation" note on generation-settings changes invalidating old saves)
-   [x] World changes (gathered/depleted resource nodes saved as spawn-index ids - 2026-10-15; partly hit nodes, items lying on the ground and killed creatures are still not saved)
-   [x] Building save (placed pieces only - position/rotation/definition; no structural/snap state beyond that)
-   [ ] Container save (no storage containers/chests exist yet)
-   [x] Time/weather save (day, time of day, current weather + its remaining time)
-   [ ] Save versioning (known gap - a WorldGenerationSettings change after a save was made silently desyncs old saves' terrain from their stored building/player coordinates; see PROJECT_STATE.md)
-   [ ] Backup/recovery

------------------------------------------------------------------------

## Production Order from here (agreed 2026-10-18)

Phases 0--8 are essentially done; the open items of Phases 2--11 are
listed in their phases above. The phases below are no longer worked
strictly top to bottom - this is the agreed order of work:

`Stage A: close the slice → Stage B: creatures (+ early visuals) → Stage C: resources & tiers → Stage D: bosses`

Final visuals run alongside content production (Phase 13) and polish
(Phase 16), after Stage C.

### Stage A --- Close the Vertical Slice (next)

-   [ ] Damage feedback (Phase 7): hit reaction/stagger on the target,
    hit particles on creatures/trees/rocks, placeholder hit sounds.
    Needed before creature work - creatures can't be judged without it.
-   [ ] Storage chest + container save (Phase 2 / Phase 11)
-   [ ] Save versioning (Phase 11) - old saves must not silently break
    when world generation changes
-   [ ] Full manual Phase 12 playthrough by the developer + fixes from it

### Stage B --- Creatures (right after the slice)

Combat (contact hits, 155 weapons) is ready; there is almost nothing to
fight yet. Phase 8 is formally done with the minimum (Wild Boar, Forest
Wolf).

-   [ ] Real models + animations for the boar and the wolf (currently
    placeholders)
-   [ ] Line-of-sight perception; wolf packs; more dangerous night creatures
-   [ ] Creature spawning by biome and time of day
-   [ ] Creature loot: hide, bone, fangs - these are resources the pack
    weapon recipes need (feeds Stage C)
-   [ ] A couple of new creatures from the Slavic setting (Phase 13
    "Expand creatures")

**Early visuals - in parallel with Stages A--B** (what makes the slice
*feel like NAV*):

-   [ ] Hit and gathering VFX (shared with Stage A damage feedback)
-   [ ] Baseline post-processing / colour grading
-   [ ] Trees and vegetation in the forest; Birch grove (Phase 9)

### Stage C --- Resources and Progression Tiers 1--2

-   [ ] Ores, smelter, bars; leather/bone processing (Phase 13 "Expand
    resources")
-   [ ] Workbench tiers / upgrade path (Phase 4)
-   [ ] Armor (Phase 4 / 13)
-   [ ] Workbench recipes for the pack weapons + real stats; then delete the
    TEMPORARY weapon racks (Phase 13 "Expand weapons")

Bosses need this stage: without tiers a boss has nothing to reward or
unlock.

### Stage D --- Bosses (Phase 14)

-   [ ] Boss framework built and proven on **one placeholder boss**: altar,
    ritual items, arena, phases, reward, unlock of the next tier
-   [ ] Real bosses (7--10, some from Slavic mythology, some original) -
    only once the developer designs them (bosses are NOT FINALIZED); then
    roughly one per progression tier

### Final Visuals (Phase 13 / Phase 16, after Stage C)

-   [ ] Decide the asset strategy first ("Not Yet Decided" in
    PROJECT_STATE.md): Asset Store packs vs own Blender models (like the
    hammer) vs a mix
-   [ ] Sky polish (Phase 10), atmospheric content, final models/VFX,
    animation polish

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
    -   [ ] **Resources for the LowPoly pack weapons (TODO, 2026-10-17).** The 155 pack weapons exist in the game (9 classes) but only Wood/Rock/Flint/Resin exist as materials. Needed: metals/ores and their processing (e.g. copper/bronze/iron ore -> smelter -> bars), leather/hide, bone, hardwood, rope/sinew, gems - per GDD tier progression. Details: `PROJECT_STATE.md` Technical Debt "LowPoly pack weapons".
-   [ ] Expand weapons
    -   [x] All LowPoly Weapons Pack models as weapons (2026-10-17): 9 classes (Dagger, Sword, Greatsword, Hand Axe, Battle Axe, Mace, Warhammer, Spear, Shield), each with its attack animation; generated by NAV > Items > Generate Weapons From LowPoly Pack
    -   [ ] **Workbench recipes for every pack weapon, each class/tier from different ingredients (TODO)** - blocked on the resources above. Until then they are handed out by TEMPORARY weapon racks at the spawn (`WeaponArsenal`/`WeaponArsenalSpawner`/`WeaponRack` - delete when recipes exist)
    -   [ ] Real per-weapon stats (tiers/materials) - currently placeholder stats per class
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
