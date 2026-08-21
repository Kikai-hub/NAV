# NAV --- Technical Architecture v0.1

## Goal

Create a modular Unity architecture that supports: - rapid MVP
development; - future content expansion; - save/load; - procedural world
generation; - 1--4 player co-op later; - large amounts of data-driven
content.

------------------------------------------------------------------------

## Proposed High-Level Layers

### Core

Low-level reusable systems: - service/bootstrap; - event system; -
utilities; - save interfaces; - time; - configuration.

### Gameplay

Core gameplay: - player; - health; - stamina; - skills; - interaction; -
combat; - AI; - items; - inventory; - equipment; - crafting; - building.

### World

-   world generation;
-   seed;
-   terrain;
-   biomes;
-   resources;
-   creatures;
-   weather;
-   day/night;
-   water;
-   points of interest.

### Presentation

-   UI;
-   animation;
-   VFX;
-   audio;
-   camera;
-   feedback.

### Data

ScriptableObject definitions and serialized runtime state.

------------------------------------------------------------------------

## Suggested Project Structure

``` text
Assets/
  NAV/
    Art/
    Audio/
    Materials/
    Prefabs/
    Scenes/
    Scripts/
      Core/
      Gameplay/
        Player/
        Combat/
        Inventory/
        Items/
        Crafting/
        Building/
        Equipment/
        Skills/
        AI/
      World/
        Generation/
        Biomes/
        Resources/
        Weather/
        Time/
        Water/
      Save/
      UI/
      Multiplayer/
      Editor/
    ScriptableObjects/
      Items/
      Recipes/
      Weapons/
      Armor/
      Food/
      Creatures/
      Biomes/
      Workbenches/
      Building/
      Skills/
      Weather/
    Tests/
```

The exact folder structure may evolve after implementation experience.

------------------------------------------------------------------------

## Data vs Runtime State

### Static data

Use ScriptableObjects for: - item definitions; - recipes; - weapons; -
armor; - food; - creatures; - biomes; - workbenches; - building
pieces; - weather types; - skills.

### Runtime state

Use serializable runtime models for: - inventory contents; -
equipment; - player stats; - skills; - world changes; - container
contents; - building instances; - resource depletion; - time; - weather
state.

Do not serialize arbitrary MonoBehaviour references as the primary save
format.

------------------------------------------------------------------------

## Item Architecture

A likely direction:

``` text
ItemDefinition
  ├── identity
  ├── display data
  ├── stack rules
  ├── weight
  ├── category
  └── gameplay properties
```

Runtime inventory stores item instances/IDs and quantities, not complete
copies of ScriptableObjects.

------------------------------------------------------------------------

## Recipe Architecture

``` text
RecipeDefinition
  ├── ingredients
  ├── output
  ├── required workbench
  ├── required level/tier
  └── unlock condition
```

------------------------------------------------------------------------

## Character Architecture

Separate: - input; - movement; - camera; - stats; - health; - stamina; -
skills; - equipment; - animation; - interaction.

Do not create one `PlayerController` that owns everything.

------------------------------------------------------------------------

## Combat Architecture

Separate: - input; - attack definitions; - attack execution; - hit
detection; - damage; - health; - blocking; - parrying; - stamina; -
weapon data; - animation feedback.

Combat should support melee first and ranged weapons later.

------------------------------------------------------------------------

## AI Architecture

Use modular components.

Possible responsibilities: - perception; - target selection; -
movement; - state machine/behavior; - attack; - damage reaction; -
death; - drop/loot.

Do not build one universal enemy script with hundreds of conditionals.

------------------------------------------------------------------------

## World Generation

World generation should be deterministic from: - seed; - generation
version; - generation settings.

Recommended conceptual pipeline:

``` text
Seed
 ↓
Noise / deterministic random
 ↓
Heightmap
 ↓
Land / Water
 ↓
Biome Assignment
 ↓
Terrain Features
 ↓
Resources
 ↓
Vegetation
 ↓
Creatures / Spawn Rules
 ↓
Points of Interest
```

Generation should be split into testable stages.

------------------------------------------------------------------------

## Persistence

Save system should use: - explicit save models; - version numbers; -
stable IDs; - migration strategy.

Never rely on scene object instance IDs as persistent identity.

------------------------------------------------------------------------

## Future Multiplayer

The single-player architecture should not assume local-only ownership
forever.

However, networking should not be implemented prematurely.

When multiplayer begins, identify authoritative state for: - world; -
players; - inventory; - combat; - creatures; - construction; -
containers; - boats.

------------------------------------------------------------------------

## Boss Architecture

Bosses are not finalized.

Create interfaces/extensible architecture only when required by the
current milestone.

Do not build a giant boss framework during the MVP.

Future boss content must support: - custom AI; - multiple phases; -
unique attacks; - summon mechanics; - rewards; - progression unlocks.

------------------------------------------------------------------------

## Architectural Principle

When uncertain, prefer the simplest architecture that: 1. works; 2. is
testable; 3. can be extended; 4. does not prematurely solve future
problems.
