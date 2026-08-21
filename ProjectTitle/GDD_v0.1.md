# NAV --- Game Design Document v0.1

## 1. Project Overview

**Project name:** NAV\
**Genre:** 3D survival\
**Platform:** PC\
**Camera:** Third-person, free camera, inspired by Valheim\
**Visual style:** Stylized low-poly, fairy-tale Ancient Rus\
**Players:** 1--4 player cooperative multiplayer\
**Initial development target:** Single-player MVP, followed by
cooperative multiplayer\
**World:** Large procedural world generated from a seed

### High concept

NAV is an open-world survival game inspired structurally by Valheim,
with a distinct Ancient Rus fantasy identity.

The player controls a bogatyr summoned by Perun to cleanse the lands of
evil creatures. The player explores a large procedurally generated
world, gathers resources, builds a settlement, develops workbenches,
crafts equipment, improves character skills, discovers new biomes and
eventually confronts powerful bosses.

The intended tone is **fairy-tale Rus**, not grimdark horror.

### Core gameplay loop

`Explore → Gather → Craft → Build → Improve → Explore a new biome → Prepare for a boss → Defeat boss → Unlock the next progression tier`

The game should remain open-ended rather than becoming a linear
corridor.

------------------------------------------------------------------------

## 2. Design Pillars

1.  **Exploration has purpose.** New areas should contain meaningful
    resources, creatures, discoveries, and progression opportunities.
2.  **Progression is systemic.** Workbenches, resources, equipment,
    food, skills, and biomes should reinforce one another.
3.  **The world feels alive.** Day/night, weather, animals, creatures,
    water, forests, and environmental audio contribute to atmosphere.
4.  **Construction matters.** The player's settlement is a functional
    home and progression hub.
5.  **Ancient Rus is the identity.** Mythological inspiration must
    influence creatures, materials, architecture, names, atmosphere, and
    gameplay.
6.  **Simple combat, strong feedback.** Combat is based on attacks, hit
    detection, blocking/parrying, positioning, stamina, and readable
    enemy behavior.
7.  **Co-op is part of the long-term design, but not an MVP blocker.**
8.  **Architecture must support expansion.** New content should be added
    without rewriting core systems.

------------------------------------------------------------------------

## 3. Player Experience

The intended early experience:

1.  Spawn into the world.
2.  Learn movement, camera, stamina, interaction, gathering, and
    inventory.
3.  Gather wood and stone.
4.  Create basic tools.
5.  Establish the first shelter.
6.  Build the first workbench.
7.  Craft basic equipment and food.
8.  Explore the surrounding region.
9.  Encounter neutral and hostile wildlife/creatures.
10. Discover the first progression objective.

The player should quickly understand: - what can be gathered; - what can
be crafted; - what can be built; - where the player is safe; - what the
next meaningful objective is.

------------------------------------------------------------------------

## 4. World

### World generation

The world is procedurally generated when a new world is created.

A world has: - world name; - seed; - deterministic generation
parameters; - generated terrain; - biome distribution; - resource
placement; - creature spawning rules; - points of interest.

The same seed must produce the same world when generation settings are
identical.

### Geography

The world contains: - large land masses; - islands; - oceans and smaller
bodies of water; - coastlines; - forests; - plains; - groves; - other
future biome types.

The final world should be large enough to support long-term exploration,
while remaining technically manageable for an indie PC game.

### Biomes

Confirmed initial/basic biomes: - Ocean - Plains - Dense Forest - Birch
Grove - Ordinary Forest

More biomes will be added during production.

Each biome should eventually define: - terrain characteristics; -
vegetation; - resources; - ambient audio; - weather presentation; -
wildlife; - hostile creatures; - progression tier; - points of
interest; - boss relationship.

------------------------------------------------------------------------

## 5. Time and Weather

### Day/night

The game has a complete day/night cycle.

The system should support: - time progression; - lighting changes; - sky
changes; - creature behavior changes; - ambient sound changes; -
gameplay changes based on time.

### Weather

Initial weather types: - Clear - Rain - Heavy rain - Thunderstorm

Weather should be data-driven and extensible.

Future weather types may be added without rewriting the weather
architecture.

------------------------------------------------------------------------

## 6. Player Character

The player creates a character from the main menu.

Character customization should support: - body/sex selection; - skin
color; - body shape; - height; - voice; - hair style; - hair color; -
beard; - beard color.

The system must be designed so additional customization options can be
added later.

### Base attributes

The exact numbers are not finalized. The design is inspired by Valheim's
general philosophy.

Candidate attributes: - Health - Stamina - Movement speed - Jump
height - Unarmed damage - Carry weight - Other future combat/defensive
values

Do not hard-code balancing values into gameplay scripts. Use centralized
data/configuration.

------------------------------------------------------------------------

## 7. Skills

The game uses skill progression inspired by Valheim.

Using an activity repeatedly improves the related skill.

Potential skills: - Swords - Axes - Clubs - Spears - Bows - Blocking -
Running - Jumping - Mining - Woodcutting - Farming - Other future skills

The exact list and formulas are not final.

Skills should be implemented as modular data-driven systems.

------------------------------------------------------------------------

## 8. Inventory

The player has an inventory system.

Requirements: - item stacking; - item quantities; - item weights; - item
categories; - equipment slots; - drag/drop UI; - item splitting; - item
transfer; - storage containers; - death inventory recovery.

Inventory data must be serializable for save/load.

------------------------------------------------------------------------

## 9. Death

When the player dies: 1. The character dies. 2. A gravestone is created
at the death location. 3. The player's carried inventory is stored
inside the gravestone. 4. The player respawns. 5. The player can return
to the gravestone and recover the inventory.

The exact respawn rules and any future death penalties are not
finalized.

------------------------------------------------------------------------

## 10. Gathering and Resources

Confirmed resource categories: - wood; - stone; - ores/minerals; -
food; - berries; - seeds; - crops; - future progression materials.

Natural resource nodes are **not infinitely regenerated**.

The world should therefore feel persistent.

Renewable systems such as: - planted trees; - farming; - crops; - animal
populations; may be used where appropriate.

Resource placement must be tied to procedural world generation.

------------------------------------------------------------------------

## 11. Crafting

Crafting is a major progression system.

The game should support: - crafting from inventory; - crafting at
workbenches; - workbench upgrades; - recipes; - recipe unlocks; -
material requirements; - future progression tiers.

Recipes should be data-driven.

Use ScriptableObjects or equivalent data assets for: - item
definitions; - recipes; - workbench definitions; - recipe unlock
conditions.

------------------------------------------------------------------------

## 12. Workbench Progression

The workbench is a major progression hub.

The intended progression pattern is:

`New biome/resource → new material → new workbench capability → new recipes → stronger equipment → access to harder content`

Exact tiers are intentionally **not finalized yet**.

Bosses will later be integrated into this progression.

------------------------------------------------------------------------

## 13. Building

Building is inspired by Valheim.

The system should support modular construction pieces such as: -
floors; - walls; - roofs; - doors; - windows; - beams; - stairs; -
structural pieces; - crafting stations; - furniture; - utility objects.

Building should use a snapping/grid-like modular placement system.

The player should be able to construct a functional settlement/base.

Future structural integrity mechanics may be added.

------------------------------------------------------------------------

## 14. Combat

Combat is intentionally simple and readable.

Initial philosophy: - attack; - hit/miss; - hitbox/hurtbox detection; -
damage; - stamina; - blocking; - parrying; - positioning.

Weapon categories: - swords; - axes; - clubs; - spears; - bows; -
shields; - future additional weapons.

Combat should avoid unnecessary complexity during the MVP.

------------------------------------------------------------------------

## 15. Armor

Armor is equipment-based.

Potential slots: - head; - chest; - legs; - hands; - feet; -
cloak/accessory where appropriate.

Armor sets can provide set bonuses.

Example design direction: `2/5 pieces → minor bonus`
`5/5 pieces → full set bonus`

Exact bonuses are not finalized.

------------------------------------------------------------------------

## 16. Food

Food is inspired by Valheim.

Food should temporarily affect player attributes.

Potential effects: - maximum health; - maximum stamina; - health
regeneration; - stamina regeneration; - temporary special effects.

Food can be created from: - meat; - berries; - crops; -
seeds/harvests; - other future ingredients.

The exact food formulas are not finalized.

------------------------------------------------------------------------

## 17. Creatures and AI

The world contains: - neutral wildlife; - aggressive wildlife; - hostile
supernatural creatures; - mini-bosses; - bosses.

Neutral creatures may flee when threatened.

Aggressive creatures may attack when appropriate.

Hostile creatures should support a basic AI loop:

`Idle/Patrol → Detect Player → Chase → Attack → Search/Return`

The AI architecture must be modular so different creatures can have
different behaviors.

------------------------------------------------------------------------

## 18. Boats

Initial transportation scope: - rafts; - small boats.

Large ships are not part of the initial scope.

Boat systems should eventually support: - water movement; - player
boarding/exiting; - basic steering; - persistence/save; - multiplayer
synchronization.

------------------------------------------------------------------------

## 19. NPCs

NPCs are **not part of the initial version**.

The architecture should not make future NPCs impossible, but NPC systems
should not be built during the MVP unless needed for technical
foundations.

------------------------------------------------------------------------

## 20. Quests and Story

There is no traditional quest-heavy story in the initial version.

The high-level premise is:

A bogatyr is summoned by Perun to cleanse the lands of evil.

The game should prioritize: - exploration; - survival; - progression; -
building; - discovery; - boss progression.

More narrative systems may be added later.

------------------------------------------------------------------------

## 21. Bosses --- NOT FINALIZED

**IMPORTANT FOR ALL DEVELOPMENT: Bosses are intentionally NOT DEFINED
yet.**

The full game is expected to contain approximately **7--10 bosses**.

Bosses will be designed later.

The architecture must therefore provide extension points for: - boss
entities; - boss arenas/locations; - boss summoning; - boss
health/state; - boss AI; - boss rewards; - progression unlocks; -
boss-specific loot; - boss-specific events.

Do NOT invent final bosses, boss names, boss mechanics, or boss rewards
unless explicitly requested.

For now, use: - placeholder boss; - mock boss data; - generic boss
interfaces; - test enemies.

The future boss system must be capable of supporting both: 1.
recognizable figures inspired by Slavic mythology; 2. original creatures
created specifically for NAV.

The intended summoning model is:

`Find/activate altar → collect required ritual items → perform summoning → boss encounter`

This is a future system and does not need to be fully implemented in the
first MVP.

------------------------------------------------------------------------

## 22. Multiplayer

Long-term target: - 1--4 player cooperative multiplayer.

Development strategy: 1. Build and validate core gameplay in
single-player. 2. Keep important gameplay systems deterministic and
network-friendly. 3. Introduce multiplayer after the core single-player
MVP is stable. 4. Synchronize world state, players, inventory, combat,
creatures, construction, containers, boats, and progression.

Do not allow multiplayer requirements to unnecessarily complicate the
first prototype.

------------------------------------------------------------------------

## 23. Save System

The game must support persistent saves.

Save data will eventually include: - world seed; - world generation
settings; - player data; - character customization; - inventory; -
equipment; - skills; - player position; - buildings; - containers; -
resources/state changes; - world progression; - defeated bosses; -
future multiplayer state.

The save system must be versioned so save formats can evolve.

------------------------------------------------------------------------

## 24. MVP Scope

The first playable prototype should be intentionally small.

### MVP target

-   One procedural test world
-   One or a few basic biomes
-   Third-person character
-   Camera
-   Movement
-   Jumping
-   Stamina
-   Interaction
-   Gathering wood
-   Gathering stone
-   Basic inventory
-   Basic item system
-   Basic crafting
-   First workbench
-   Basic building
-   Basic food
-   Basic equipment
-   One neutral animal
-   One hostile creature
-   Basic combat
-   Basic health/damage
-   Day/night
-   Basic weather
-   Save/load
-   Placeholder death/gravestone system

### Explicitly NOT required for MVP

-   Final bosses
-   Final boss progression
-   NPCs
-   Full multiplayer
-   Large final world
-   Complete biome roster
-   Final art
-   Final audio
-   Complete narrative
-   Final balance
-   Large boat system
-   Advanced quests

------------------------------------------------------------------------

## 25. Technical Philosophy

NAV should be built as a modular Unity project.

Prefer: - C#; - component-based architecture; - ScriptableObjects for
editable game data; - interfaces where useful; - events for
decoupling; - dependency injection only where it provides real value; -
small focused classes; - clear ownership of state; - centralized
configuration; - serialization-safe data structures.

Avoid: - giant manager classes; - duplicated systems; - hard-coded item
IDs scattered through code; - gameplay values hidden inside unrelated
scripts; - unnecessary singletons; - tight coupling between UI and
gameplay; - rewriting working systems without a concrete reason.

------------------------------------------------------------------------

## 26. Unresolved Design Areas

These are intentionally open: - exact Unity version; - render
pipeline; - networking solution; - exact asset stack; - final biome
list; - final resource list; - exact technology tiers; - exact boss
roster; - exact boss mechanics; - final enemy roster; - exact recipes; -
exact stats/balance; - final UI; - final art assets; - final
audio/music; - final story content.

These decisions should be made progressively rather than guessed.

------------------------------------------------------------------------

# End of GDD v0.1
