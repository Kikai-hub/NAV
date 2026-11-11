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
-   [x] Storage container (2026-10-21: wooden chest building piece, 10 slots - StorageContainer)
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
-   [x] Basic gathering feedback (node shrinks per hit - rocks only since 2026-10-21; gathering by tool hits only, no E - 2026-10-21)
-   [x] Valheim-style tree felling (2026-10-21, spruce): tree doesn't shrink; felled ->
    stump stays + bare trunk topples away from the player -> chopped into 2 logs ->
    each log drops the wood. Built by menu NAV/World/Build Spruce Felling.
    Later: falling trunk damaging creatures/player, saving fallen trunks/logs,
    other tree types, Valheim-style downward chop animation for logs.

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
-   [x] Basic armor (Stage C, 2026-10-24)

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
-   [x] Gable (фронтон) piece, real 45-degree roof slope with an eave on the wall logs, directional snapping (2026-10-24)
-   [ ] Wood steps / porch (крыльцо) at tier 1 (2026-10-24): on a slope the floor can stand 0.5-1.5 m above the ground at the door and the player (step offset 0.3 m) can't get in - the only stairs are the oak ones (joiner's bench). Needs a cheap wood piece snapping to a foundation edge, ~0.25 m per step

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
-   [x] Terrain generation (2026-10-25: hybrid world, radius 15 km - square terrain tiles 512 m computed on worker threads around the player, see PROJECT_STATE.md "Hybrid world in the game"; before that one Unity Terrain 2560 m)
-   [x] Ocean (2026-10-25: real waves - Gerstner waves shared by the shader and gameplay (NAV.Core.WaveField), LOD ocean mesh around the camera (OceanSurface), sea state from the weather, boats/swimming/floating items ride the waves (WaterBuoyancy); no underwater view yet)
-   [x] Plains
-   [x] Forest (ground texture only - no distinct vegetation/trees yet, see PROJECT_STATE.md)
-   [x] Birch grove (2026-10-22 - see Stage B below)
-   [x] Dense forest (2026-10-24 - the wettest heart of each forest: ancient trees 18-27 m with closed crowns, old oaks, copper; see Stage C)
-   [x] **Hybrid world, radius 15 km (2026-10-25, in the game).** `World/Generation/Hybrid/` + `TerrainTiles`: two-layer noise (continents + detail), mask 0-3% solid core / 3-80% archipelago (land threshold rising outwards, big islands with mountains near the core, islets near the rim) / 80-100% deep ocean, radial climate bands with noisy borders, polar caps north (permafrost) and south (desert), height biomes (mountains on the peaks of the core and the big islands, swamps in island lowlands), POIs per chunk. Preview: NAV > World > Hybrid World > Render Preview Map. Mountains, Swamp, Permafrost, Desert are placeholder biomes (ground texture only) until their tiers.
-   [ ] Resource distribution
-   [ ] Vegetation distribution
-   [x] Spawn regions (dry/flat player spawn point per seed + respawn there; creatures per biome, 30 m away from it - 2026-10-15)
-   [~] Points of interest (2026-10-25: PoiDefinition + PoiPlacer, spawned with their chunk, trees kept clear; kinds and prefabs are placeholders from primitives - Ruins, Shrine, Shipwreck, Boss arena slot (empty, no boss))

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
-   [x] Container save (2026-10-21: chest contents saved with the building piece)
-   [x] Time/weather save (day, time of day, current weather + its remaining time)
-   [x] Save versioning (2026-10-21: FormatVersion + WorldGenerationVersion in every save, migration step, Load list warns about older-world saves; bump `SaveSystem.WorldGenerationVersion` on every generation change)
-   [x] Backup/recovery (2026-10-21: temp-file write + atomic replace, previous save kept as `.bak`, load falls back to it)

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
    (Done 2026-10-21 - HitFeedback + Creature stagger, checked in Play Mode
    by scripted swings; block/parry sounds and stone hits not seen yet. Tick
    after the developer's playtest.)
    Needed before creature work - creatures can't be judged without it.
-   [x] Storage chest + container save (Phase 2 / Phase 11) - done 2026-10-21
-   [x] Save versioning (Phase 11) - old saves must not silently break
    when world generation changes (done 2026-10-21, with crash-safe
    write + backup)
-   [ ] Full manual Phase 12 playthrough by the developer + fixes from it

### Stage B --- Creatures (right after the slice)

Combat (contact hits, 155 weapons) is ready; there is almost nothing to
fight yet. Phase 8 is formally done with the minimum (Wild Boar, Forest
Wolf).

-   [ ] Real models + animations for the boar and the wolf (2026-10-21: the
    boar is now a Deer and the wolf a Tiger stand-in from the ithappy Animals
    pack - idle/walk/run clips, attack lunge procedural. Since 2026-11-06 every
    creature and the player die into a physics ragdoll (CLAUDE.md "Death Rule",
    NAV > Animation > Build Ragdolls). Still to do: a real wolf model,
    attack/hit animations, creature sounds)
-   [x] Line-of-sight perception; wolf packs; more dangerous night creatures
    (Done 2026-10-22 - field of view 220 deg + raycast, hears within 3 m,
    wolves alert packmates within 15 m; NightCount per biome spawn entry.
    Still to do: genuinely new night-only creatures)
-   [x] Creature spawning by biome and time of day (Done 2026-10-22 -
    CreaturePopulation: groups/packs, day/night counts, respawn every
    RespawnSeconds 50-120 m from the player, extras removed far away)
-   [x] Creature loot: hide, bone, fangs - these are resources the pack
    weapon recipes need (feeds Stage C) (Done 2026-10-22 - Hide/Bone/Fang
    items with placeholder primitive models; wolf: hide, bone 60%, 2 fangs
    50%; deer: hide, bone 75%. Still to do: real models, recipes that use
    them - Stage C)
-   [ ] A couple of new creatures from the Slavic setting (Phase 13
    "Expand creatures")

**Early visuals - in parallel with Stages A--B** (what makes the slice
*feel like NAV*):

-   [x] Hit and gathering VFX (shared with Stage A damage feedback) (Done
    2026-10-24 - ImpactEffects: wood chips + sawdust, stone shards + dust +
    sparks, blood droplets leaving ground splats, impact flash, parry sparks,
    falling leaves/needles and dust clouds for felled trees)
-   [x] Baseline post-processing / colour grading (Done 2026-10-22 - Day
    base look + Golden Hour / Night / Overcast volumes blended by
    DayNightGrading, aerial haze in clear weather, sand on the shore, softer
    meadow grass. Still to do: water material - flat saturated blue that
    glows at night)
-   [x] Trees and vegetation in the forest; Birch grove (Phase 9) (Done
    2026-10-22 - Birch Grove sub-biome inside forests with placeholder
    procedural birches; Nature Starter Kit 2 bushes, broadleaf trees and
    waving grass; chamomile/cornflower flowers; mushrooms, stumps, logs,
    pebbles as decor; olive forest floor; Hand Painted Nature Kit larches,
    cedars, ferns, ceps, dandelions, plantain. Still to do: a real birch model,
    birch-specific felled trunk, a grass density graphics setting)

### Stage C --- Resources and Progression Tiers 1--2

The full game has **9 progression tiers, each with its own biome
(developer, 2026-10-24)**. Stage C builds the first ones; the rest come with
later biomes and bosses.

-   [x] Plan all 9 tiers: biome, material, station and station levels,
    tools/weapons/armour per tier, which boss unlocks it (Approved
    2026-10-24 - `PROGRESSION_TIERS_v0.1.md` v1.0: meadows & birch groves,
    dense forest + oak, sea islands, swamp, mountains, black forest + bog
    oak, wild steppe, gem caverns, Nav)
-   [x] Bronze tier (3): tin, bronze bars, casting mould = forge 2, shift the
    iron recipes/upgrades up one forge level, iron armour 12 -> 15, bronze
    tools/weapons/armour from the pack (Done 2026-10-24 - tin on shores until
    the sea-islands biome exists; bog iron needs a bronze pickaxe)
-   [x] Oak (tier 2): old oaks in the dense forest (copper axe), joiner's
    bench = workbench 3, oak building set (beams, posts, upper floors,
    stairs, gates, porch, carved window frames) (Done 2026-10-24 - oaks in the
    ordinary forest until the dense forest exists; gate opens; no porch
    piece - stairs + floor)
-   [ ] New biome per tier (World Generation): dense forest separated from
    the ordinary forest, sea islands (+ boat), swamp, mountains, black
    forest, steppe, gem caverns, Nav - each with its creatures and boss
    -   [x] Dense forest (tier 2) (Done 2026-10-24 - `DenseForestSetup`;
        ~10% of the world; ancient spruce/larch/cedar 18-27 m, copper axe;
        old oaks and copper ore moved here only; many wolves. Also forest and
        birch-grove trees x2.5. Still to do: gloom under the crowns
        (per-biome fog/light), its own creatures, boss slot)
    -   [x] Sea islands (tier 3) + boat (Done 2026-10-25 - round world in an endless sea, islands in the outer coastal band, tin only there, driftwood, shellfish, the ladya boat; see PROJECT_STATE.md)
    -   [x] World ~20x: hybrid world radius 15 km, terrain tiles around the player, biome by per-point noise (Done 2026-10-25 - see Phase 9 and PROJECT_STATE.md)
    -   [x] Swamp (tier 4) - first part done 2026-10-25; bare dead trees (primitives) (NAV > World > Build Swamp (Tier 4)): dead trees, sunken logs -> Bog Wood, reeds, hummocks, fog and sound, bog iron only here, Bog Lurker with a poisonous bite (PlayerPoison, green health bar). Bow and arrows done 2026-10-25 (Hunting Bow + wood/flint/iron arrows, Human Archer Animations FREE clips and models, PlayerArchery). Part 2 done 2026-10-25 (NAV > Build > Build Swamp Iron Age (Tier 4)): iron hinges, Iron-Bound Gate, Stake Trap, wild rosemary -> Antidote, boss 4 arena slot (empty). Still to do: real models (MODELS_FOR_3D_ARTIST 7.7), the boss itself (not designed)
    -   [x] Mountains (tier 5) (Done 2026-10-25 - NAV > World > Build Mountains (Tier 5)): cold (PlayerCold, HeatSource, armour warmth), Snowy Peaks above 175 m, silver ore -> Smelting Furnace -> silver tools/weapons/armour (x1.5 vs the unclean), Snow Leopard -> Fur -> Loom -> Fur Hood / Fur Cloak (Cloak slot), boss 5 arena slot (empty), spawn only in the starting biome. Still to do: rockfalls, the boss itself, real models (MODELS_FOR_3D_ARTIST 7.8)
    -   [x] Black Forest (tier 6) (Done 2026-11-06 - NAV > World > Build Black Forest (Tier 6), then Build Armour + Build Pack Weapon Recipes): own climate band 0.5-0.6 of the radius, eternal dusk (BiomeDefinition.Twilight dims the daylight), bog oaks (black steel axe) + deadfall (silver axe) -> bog oak charcoal, black ore -> black iron -> black steel bar at the Forge level 6 (Quench Tub), Carved Table = Workbench 5 with the bog-oak set (wall, carved pillar, carved arch, tent roof, black gate), black steel tools / 9 weapons / armour 7-12-7, Volkolak (нечисть, packs) + Volkolak Cloak, boss 6 arena slot (empty). Still to do: watchtowers, more terem pieces (multi-storey walls, carved porches), the boss itself, real models (MODELS_FOR_3D_ARTIST 7.9)
    -   [ ] Then the rest (tier 7+)

-   [x] Ores, smelter, bars; leather/bone processing (Phase 13 "Expand
    resources") (Done 2026-10-24 -
    copper ore + bog iron in the world, charcoal kiln, bloomery, tanning
    rack, bone glue on the campfire. Still to do: tools/armour that use
    bars and leather - next items)
-   [x] Workbench tiers / upgrade path (Phase 4) (Done 2026-10-24 - station
    kinds + levels from nearby upgrade pieces: Workbench (+Tanning Rack),
    Forge (+Anvil, +Bellows); copper/iron axes and pickaxes; bog iron needs
    a copper pickaxe; better tools gather faster)
-   [x] Armor (Phase 4 / 13) (Done 2026-10-24 - head/chest/legs slots,
    Valheim armour curve, leather/copper/iron sets with recipes at the
    Workbench level 2 and Forge levels 1-2, placeholder looks on the body,
    saved. Still to do: real models, weight/speed penalty, durability, cape)
-   [x] Workbench recipes for the pack weapons + real stats; then delete the
    TEMPORARY weapon racks (Phase 13 "Expand weapons") (Done 2026-10-24 -
    20 pack weapons in wood/bone/copper/iron tiers with recipes at the
    Workbench and Forge, stats = class base x tier; racks removed from the
    scene. The other ~135 models wait for later tiers)

Bosses need this stage: without tiers a boss has nothing to reward or
unlock.

### Stage D --- Bosses (Phase 14)

-   [ ] Boss framework built and proven on **one placeholder boss**: altar,
    ritual items, arena, phases, reward, unlock of the next tier
-   [~] Boss 1 - the **Leshy** (developer's design, 2026-11-06): done - no altar, comes by the wrath of the forest + chance (ForestWrath), peaceful until provoked while the forest isn't angry, Slam/Sweep/rage roar, foot IK, ragdoll, Leshy Heartwood gates the Stone Pickaxe, HUD bar, saved wrath. Left: sounds and slam dust/shake, multiplayer sync, balance after playtests (NAV > Bosses > Build Leshy Boss)
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
-   [ ] Add final audio (started 2026-10-21: weapon swing whooshes from the Whooshes SFX Pack; hits, footsteps, ambience, UI still placeholder or missing)
-   [ ] Improve world generation

------------------------------------------------------------------------

## Phase 14 --- Boss System

Only after the underlying progression is stable.

-   [ ] Boss framework
-   [ ] Summoning altar
-   [ ] Ritual items
-   [ ] Boss arenas
-   [~] Boss AI extensions (ICreatureAttack, BossActor, Creature.SpeedMultiplier/TurnSpeed - 2026-11-06, Leshy)
-   [~] Boss phases (Leshy: rage at 50% HP)
-   [ ] Boss rewards
-   [ ] Progression unlocks
-   [ ] 7--10 final bosses over the full production cycle

**Boss identities and mechanics are intentionally undefined at v0.1.**

------------------------------------------------------------------------

## Phase 15 --- Multiplayer

Target: 1--4 player cooperative multiplayer.

Started 2026-11-06 at the developer's request (see PROJECT_STATE.md Change Log "Multiplayer").

-   [x] Choose networking technology (Photon PUN 2, fixed region "eu")
-   [x] Define authority model (each player owns their character - hits go to the owner; each machine runs the creatures around its player, nearby players share the lower actor's set; world changes as cached events)
-   [x] Network player state (proxy avatars: position, animator, equipment, health, death ragdoll, name tag)
-   [x] Network inventory (each player keeps their own; since 2026-11-10 items on the ground are shared - the master client gives an item to whoever picks it first; hand-over = drop and pick up)
-   [x] Network combat (PvP rooms: hits sent to the owner's PlayerCombat with block/parry/armour; PvE: no player damage)
-   [x] Network creatures (copies of the owner's creatures; hits to the owner; loot on the killer's machine)
-   [x] Network construction (build / demolish; doors since 2026-11-10; stations process on the master client only)
-   [x] Network containers (2026-11-10: chest and station contents, whole after each change, last change wins)
-   [x] Network gathering (2026-11-10: one shared hit count per tree/rock on the master client; trunks and logs owned by the feller, streamed, hits on copies sent to the owner)
-   [x] Network events (2026-11-10: the Leshy copied with health, rage and actions; encounter lines, omen and forest silence for players nearby)
-   [ ] Network boats
-   [ ] Network save/load (the host saves the world incl. guests' builds; guests' characters not saved; online worlds are new-only - no "load save online" yet)
-   [x] Host/join flow (Multiplayer window: server list, room + password, double click to join; Create World: Online, room, password, PvE/PvP)
-   [ ] Disconnect/reconnect handling (leaving returns to... the game keeps running offline; host migration keeps the room)
-   [x] Chat (Enter; since 2026-11-10 also in solo, dark half-transparent panel)

------------------------------------------------------------------------

## Phase 16 --- Polish and Release

**First Steam demo (planned 2026-11-12):** step-by-step plan in `STEAM_DEMO_PLAN.md`
(audit check, decisions needed, stages 0-6). Urgent first: make the GitHub repo private (paid
asset packs + Photon AppId are public). Scope decided 2026-11-12: tiers 1-2 + the Leshy,
single-player.

-   [x] Demo limits (2026-11-12): `DemoConfig`/`DemoMode`, NAV > Demo menu, end-of-demo screen,
    multiplayer hidden, dev console only in development builds - see PROJECT_STATE Change Log
-   [ ] Play the whole demo in a build with demo mode on

-   [ ] Optimization
    -   [x] Dynamic fog, chunk streaming, far-plane tree impostors, local NavMesh (2026-10-25)
    -   [ ] Check the remaining rare 36-90 ms spikes in a build (NavMesh rebuild integration, GC); decor in far chunks; split the terrain into tiles if the world grows further
-   [ ] UI polish
    -   [x] Minimap (top right) + explored world map on M, Valheim-style fog of war (2026-11-10)
    -   [x] Developer console (` key, Settings > Controls > Developer) (2026-11-10)
    -   [x] Multiplayer menu controls in the kit's style (2026-11-10)
    -   [ ] Map markers/pins (the player's own notes, boss places) - not requested yet
    -   [ ] Check the fog in a build after the Fog Modes = Custom fix (2026-11-10)
-   [x] Input rebinding (2026-10-22: keyboard/mouse, Settings window; gamepad not yet)
-   [ ] Accessibility
-   [ ] Audio polish
-   [ ] VFX polish
-   [ ] Animation polish
-   [ ] Tutorial/onboarding
-   [x] Settings (2026-10-22: Settings window in main menu + pause - audio, controls, graphics; settings.json)
-   [x] Graphics settings (2026-10-22: preset, display mode, resolution, VSync, FPS limit, FOV, object distance, render scale, shadows, MSAA, textures)
-   [ ] Save recovery
-   [ ] Save the terrain heightmap in the save file (deferred by the developer
    2026-10-21 until closer to game-ready): today only the seed is saved and the
    terrain is regenerated, so a world-generation change still leaves old saves'
    buildings under/above ground - versioning only warns about it. Plan: store
    the 513x513 heights (~0.5-0.7 MB per save), keep resources from the seed;
    also the base for terrain flattening under buildings
-   [ ] Crash/error reporting
-   [ ] Final balancing
-   [ ] QA
-   [ ] Release build

------------------------------------------------------------------------

# Golden Rule

Do not build the whole game before proving the core loop.

NAV should grow as a sequence of working milestones, not as one enormous
unfinished system.
