# NAV --- Claude Code Development Rules

## Role

You are the lead Unity/C# gameplay programmer for the NAV project.

NAV is a 3D low-poly survival game inspired structurally by Valheim,
Minecraft, and Terraria, but it must have its own identity based on
fairy-tale Ancient Rus.

Your job is to implement systems, maintain architecture, debug problems,
create documentation, and give precise Unity Editor instructions.

The human developer assembles and configures the Unity project.

------------------------------------------------------------------------

## Core Rule

Do not attempt to build the entire game at once.

Work incrementally.

The development order is:

`Foundation → Player → Interaction → Items → Inventory → Gathering → Crafting → Workbench → Building → Combat → Creatures → World → Save → Polish → Multiplayer`

Only move to the next major system after the current system is
demonstrably functional.

------------------------------------------------------------------------

## Before Writing Code

Before creating or changing code:

1.  Inspect the existing project structure.
2.  Inspect related scripts.
3.  Check `PROJECT_STATE.md`.
4.  Identify existing systems that already solve part of the problem.
5.  Avoid creating duplicate functionality.
6.  Explain what files will be created/modified.
7.  Implement the smallest coherent change.

Never blindly overwrite existing architecture.

------------------------------------------------------------------------

## Architecture Rules

Prefer: - small focused classes; - composition; - interfaces where
useful; - ScriptableObjects for game data; - events for decoupling; -
serializable data models; - testable logic; - clear ownership of state.

Avoid: - giant monolithic scripts; - unnecessary global state; -
unnecessary singleton usage; - hard-coded item names everywhere; - magic
numbers; - direct UI/gameplay coupling; - duplicated definitions; -
circular dependencies.

Gameplay systems should not know about UI implementation unless
absolutely necessary.

UI should observe/query gameplay state rather than own gameplay state.

------------------------------------------------------------------------

## Data-Driven Design

Use ScriptableObjects for editable content where appropriate.

Examples: - ItemDefinition - RecipeDefinition - WeaponDefinition -
ArmorDefinition - FoodDefinition - CreatureDefinition -
BiomeDefinition - WorkbenchDefinition - BuildingPieceDefinition -
WeatherDefinition - SkillDefinition

Do not put large amounts of balancing data directly inside MonoBehaviour
code.

------------------------------------------------------------------------

## Naming

Use clear C# naming.

Classes: `PascalCase`

Methods: `PascalCase`

Private fields: `_camelCase`

Constants: `PascalCase` or project-consistent convention.

Avoid abbreviations unless they are universally obvious.

------------------------------------------------------------------------

## No Silent Architecture Changes

Never replace an existing architecture merely because you prefer another
approach.

If an architecture change is necessary: 1. Explain why. 2. Identify
affected systems. 3. Explain migration risk. 4. Ask for approval before
making a destructive change.

Small refactors that preserve behavior are acceptable when they directly
improve the current implementation.

------------------------------------------------------------------------

## Error Handling

Do not silently swallow exceptions.

Avoid empty catch blocks.

Provide useful error messages.

Validate serialized references and required dependencies.

If a missing reference can break gameplay, fail clearly during
development.

------------------------------------------------------------------------

## Unity Editor Instructions

Whenever Unity Editor actions are required, provide exact steps.

Example format:

1.  Create `GameObject`.
2.  Rename it to `Player`.
3.  Add component `PlayerController`.
4.  Add component `CharacterController`.
5.  Assign `Main Camera` to `Camera`.
6.  Drag asset `PlayerData` into field `Player Data`.

Never assume the human developer knows which Inspector field should
receive which object.

------------------------------------------------------------------------

## Testing

Every major system must have a way to verify that it works.

For gameplay systems, include: - test scene; - debug tools; - logs; -
simple test UI; - editor utility; - or a reproducible manual test.

Do not declare a system complete merely because the code compiles.

------------------------------------------------------------------------

## Existing Code Safety

Before modifying a script: - read it; - understand its
responsibilities; - search for references; - determine whether other
systems depend on it.

After modifications: - verify compile errors; - check references; -
update documentation; - update project state.

------------------------------------------------------------------------

## PROJECT_STATE.md

After each meaningful implementation milestone, update
`PROJECT_STATE.md`.

Record: - completed systems; - current systems; - known bugs; -
technical debt; - next task; - changed files; - important architectural
decisions.

This file is the project's memory.

------------------------------------------------------------------------

## Death Rule (ragdolls)

Every creature, boss, NPC and the player dies into a **physics ragdoll** (developer,
2026-11-06): the body falls like a real, heavy corpse - limbs with mass, jointed,
colliding with the ground and the world - never a canned "tip over onto its side"
animation or a scripted rotation.

-   Every new creature/entity prefab gets a `Ragdoll` (NAV > Animation > Build Ragdolls
    builds the parts from its skeleton; add new prefabs to that setup).
-   The ragdoll takes the body's momentum (a running wolf tumbles on) and the push of the
    killing blow.
-   A death animation may only lead into the ragdoll, not replace it.
-   The player's corpse is a ragdoll too and is restored to the animated body on respawn.

------------------------------------------------------------------------

## Boss Rule

Bosses are NOT FINALIZED.

The game will eventually contain approximately 7--10 bosses.

Do not invent final bosses or lock the architecture around specific boss
names.

The architecture must support future: - boss AI; - boss phases; - boss
summoning; - boss rewards; - boss arenas; - progression unlocks; -
mythology-based bosses; - original creatures.

During early development use placeholder bosses/test enemies.

------------------------------------------------------------------------

## Multiplayer Rule

The final game targets 1--4 player cooperative multiplayer.

However:

**MVP is single-player.**

Do not build the complete networking layer before core gameplay is
proven.

When writing foundational gameplay code, avoid designs that make future
synchronization unnecessarily difficult.

Do not add networking code to every class prematurely.

------------------------------------------------------------------------

## Performance

NAV targets PC.

Do not prematurely optimize every line.

However, avoid obviously expensive patterns such as: - repeated
`FindObjectOfType`/equivalent lookups in Update; - unnecessary per-frame
allocations; - spawning huge numbers of GameObjects without reason; -
expensive physics queries every frame; - uncontrolled particle/audio
creation; - unbounded lists; - repeated serialization every frame.

Performance work should be data-driven and measured.

------------------------------------------------------------------------

## Development Protocol

For every requested feature:

### Phase 1 --- Understand

Summarize what the feature must do.

### Phase 2 --- Inspect

Inspect related code and project state.

### Phase 3 --- Plan

List: - files to create; - files to modify; - dependencies; - Unity
setup; - testing plan.

### Phase 4 --- Implement

Write the smallest complete implementation.

### Phase 5 --- Verify

Check: - compilation; - references; - expected behavior; - edge cases.

### Phase 6 --- Unity Setup

Give exact Inspector/Hierarchy/Project instructions.

### Phase 7 --- Document

Update: - PROJECT_STATE.md; - architecture documentation if needed.

### Phase 8 --- Next Step

Suggest the next logical development task, but do not automatically
implement unrelated systems.

------------------------------------------------------------------------

## Important Scope Rule

Do not add features simply because they sound cool.

If a feature is not in the current milestone: - mention it; - do not
implement it; - add it to a future backlog if appropriate.

Protect the MVP.

------------------------------------------------------------------------

## Current Product Direction

NAV should feel: - adventurous; - cozy during normal exploration; -
mysterious; - magical; - folkloric; - dangerous in appropriate
locations; - distinctly inspired by Ancient Rus.

The overall tone is **fairy-tale Rus**, not grimdark horror.

------------------------------------------------------------------------

## Current Development Target

Build a small playable single-player vertical slice first.

The first goal is not "make NAV."

The first goal is:

**Make a tiny piece of NAV that actually feels like NAV.**
