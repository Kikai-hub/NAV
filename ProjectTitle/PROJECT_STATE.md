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

## Technical Debt

-   **Building (Phase 5) is a basic/first-pass implementation, not a
    finished system.** It works end-to-end (placement, rotation,
    piece cycling, resource cost, demolish, ground-anchored
    positioning, snap-point-based position+rotation snapping between
    pieces) and is confirmed by the developer, but it should be
    revisited before being considered done - do not build further
    content or systems on top of it assuming it's final. Known gaps:
    -   Snapping has no socket-type/compatibility system - any
        `BuildingSnapPoint` can pull any other into alignment. Point
        placement/rotation is entirely manual (hand-set per prefab in
        the Editor, per-axis), with nothing validating that a
        developer set them up correctly - a wrong local Rotation
        silently produces a wrong orientation, not an error.
    -   No structural validation: pieces can overlap each other or
        float unsupported in mid-air. "Placement validation" in this
        increment means only "the aim ray hit something within
        range" - see `DEVELOPMENT_ROADMAP_v0.1.md` Phase 5's separate
        unchecked "Basic structural validation" item.
    -   No save/load of building state - blocked on the Save system
        (Phase 11), which hasn't started yet.
    -   No resource refund on demolish.
    -   No dedicated Building UI - piece selection is
        Next/Previous-key cycling plus a `PlayerDebugHud` line only,
        unlike Inventory/Crafting's real panels.
    -   Ground-anchoring (resting a piece's bottom on the raycast hit
        point) assumes pieces only ever yaw; it was not designed for
        pitch/roll placement.

    Re-read this list before doing any further Building work (e.g.
    when Save/Load lands, or when new piece types/categories are
    added), rather than assuming the Phase 5 checkboxes being ticked
    in the roadmap means the system is production-ready.

-   **`PlayerHealth`/`PlayerHealthStats` (added to back the new HP HUD
    bar) is not the Phase 6 "Health" system** - it's current/max HP
    and `TakeDamage`/`Heal` only. No regeneration, no food interaction,
    no death handling (reaching 0 HP just stays at 0). Do not treat
    `DEVELOPMENT_ROADMAP_v0.1.md` Phase 6's "Health" checkbox as done
    because of this - it isn't checked, and shouldn't be until
    regen/death/food are actually built.

------------------------------------------------------------------------

## Current Next Step

Player Foundation is complete and verified end-to-end, including the
interaction system and its range fix. "Animation placeholders" is the
one Phase 1 checklist item left undone --- deferred, since there is no
character model/Animator in the project yet; revisit once a placeholder
mesh exists.

Items Foundation increment 1 (ItemDefinition, ItemCategory, ItemStack) is
confirmed working (developer verified "[ItemStackSanityChecks] All checks
passed." with no compiler errors).

Inventory (Scripts/Gameplay/Inventory: `Inventory` + `PlayerInventory`) and
Item pickup (`ItemPickup` + the placeholder Wood.asset) have both been
added on top of it. Inventory is confirmed correct by its own editor
utility (InventorySanityChecks). ItemPickup is now CONFIRMED as well ---
developer wired `PlayerInventory` onto Player and a `TestPickup_Wood`
sphere (`ItemPickup` + Wood.asset) in SampleScene per the Step 7
instructions and confirmed the pickup playtest works end-to-end.

Next: Inventory UI. This needs a decision this project hasn't made yet
--- uGUI (Canvas) vs. UI Toolkit --- so it should be raised with the
developer rather than picked unilaterally before starting that
increment.

Developer chose **UI Toolkit**. `InventoryUIController` (Scripts/UI, the
folder ARCHITECTURE_v0.1.md already names) has been added: it observes
`PlayerInventory`/`Inventory.Changed` (UI observes gameplay state, per
ARCHITECTURE_v0.1.md) and renders a fixed grid of slot elements (icon +
quantity) built from `InventoryPanel.uxml`/`.uss` (same folder). Panel
visibility is driven by a new `ToggleInventory` input action (Player
map, bound to `I` / gamepad Select) added to
`InputSystem_Actions.inputactions`, exposed as
`PlayerInputHandler.ToggleInventoryPerformed`. Opening the panel also
unlocks/shows the cursor (and re-locks/hides it on close) directly via
`Cursor.lockState`/`Cursor.visible` --- safe because
`ThirdPersonCameraController` only touches those in its own
OnEnable/OnDisable, not every frame, so there's no per-frame fight over
cursor state.

CONFIRMED --- developer created the Panel Settings asset and the
InventoryUI GameObject (UIDocument + InventoryUIController) and
verified the toggle/pickup/close flow described in
UNITY_SETUP_NEXT_STEPS.md Step 8.

Playtest feedback (round 1): with input not suspended, the camera kept
reacting to mouse movement while the panel was open (the developer's
mouse naturally moves toward the panel, which
`ThirdPersonCameraController` was still reading as look input every
frame). Fixed by adding `PlayerInputHandler.MenuOpen`
(+`SetMenuOpen`) --- initially this suspended Move/Look/Sprint/Jump/
Interact all together.

Playtest feedback (round 2): developer wants to keep walking around
while the inventory panel is open (only the camera should freeze).
Narrowed `MenuOpen`'s effect: Move keeps reading normally always;
Look reads zero while `MenuOpen` (camera stays still); Sprint/Jump/
Interact stay suppressed while `MenuOpen` (sprint stays off deliberately
--- untested/unrequested to allow running with the panel open, revisit
if asked). `InventoryUIController.SetVisible` calls
`SetMenuOpen(visible)` alongside the cursor lock toggle. This is the
funnel all player input already passes through, so
`ThirdPersonCameraController`/`PlayerMotor`/`PlayerInteractor` needed
no changes and still don't know the inventory UI exists.

Items Foundation / Inventory / Inventory UI together close out the
"Items" and "Inventory" stages of the Core Rule's development order.
Next stage: **Gathering**.

Gathering increment 1 has been added: `ResourceNodeDefinition`
(ScriptableObject: drop item, amount per hit, hits to deplete) and
`ResourceNode` (Scripts/Gameplay/Items, IInteractable, same pattern as
ItemPickup) --- each Interact() call is one "hit" that adds
`AmountPerHit` of the drop item straight to the interactor's
PlayerInventory and decrements a hit counter; the node shrinks a little
each hit as cheap placeholder feedback and destroys itself when
depleted. Deliberately out of scope for this increment (explicit
Gathering roadmap items, deferred): tool/axe/pickaxe requirements (no
Equipment system exists yet - that's a later roadmap phase), resource
persistence/respawn (needs the Save system), and real gathering
VFX/audio (see "Not Yet Decided" - final audio/music).

CONFIRMED --- developer created `TreeWoodNode.asset` (Drop Item: Wood,
Amount Per Hit: 1, Hits To Deplete: 3) and `TestResourceNode_Tree` in
SampleScene (`ResourceNode` wired to `TreeWoodNode`), saved the scene,
and verified the gather/deplete/pickup flow from
UNITY_SETUP_NEXT_STEPS.md Step 9.

Developer asked for more resource node content on the same system
(content, not a new architecture). Added a second resource: an
ItemDefinition and a ResourceNodeDefinition, hand-authored directly
(same approach used for Wood.asset originally) reusing the
already-known script GUIDs, so no new code/logic.

CONFIRMED --- developer built this step themselves rather than
following the Step 10 instructions literally, and renamed the assets
along the way (an improvement: the node you hit is now named after
the material you're mining, and the item it drops is named after the
mineable chunk you carry away). Current names: item
`ScriptableObjects/Items/Rock.asset` (id `Rock`, stack size 20, weight
5) dropped by node definition `Scripts/Gameplay/Items/StoneRockNode.asset`
(display name "Stone", Amount Per Hit 1, Hits To Deplete 10), placed
in SampleScene as `TestResourceNode_Stone`. Gathering increment 1 is
now fully confirmed end-to-end with two working resources (Wood, Stone/Rock).

Gathering (per the Core Rule's development order) is demonstrably
functional; moved on to the next stage: **Crafting**.

Crafting increment 1: added `RecipeDefinition` (ScriptableObject:
ingredient list of item+amount pairs, output item + amount, plus
`CanCraft`/`TryCraft` logic that checks/consumes ingredients from an
`Inventory` and adds the output) and `RecipeIngredient` (a plain
serializable item+amount pair nested inside a recipe's ingredient
list) in a new `Scripts/Gameplay/Crafting/` folder (already existed as
an empty placeholder from the original ARCHITECTURE_v0.1.md folder
skeleton). Added `PlayerCrafting` (Scripts/Gameplay/Crafting), a thin
MonoBehaviour mirroring PlayerInventory: holds a fixed serialized list
of known `RecipeDefinition`s (no unlock/discovery system yet --- see
"Recipe unlock structure" in DEVELOPMENT_ROADMAP_v0.1.md Phase 4,
explicitly deferred, same reasoning as ResourceNode's deferred tool
requirement) and a `TryCraft(recipe)` method. No workbench requirement
yet either (Workbench is its own later roadmap stage) --- this
increment is "craft anywhere" by design, matching the Core Rule's
"only move to the next system after the current one is functional"
philosophy (Crafting itself needs to work before gating it behind a
Workbench makes sense to build).

Added a UI Toolkit crafting panel, following the same pattern as the
Inventory UI: `Scripts/UI/CraftingUIController.cs` +
`CraftingPanel.uxml`/`.uss`, toggled by a new `ToggleCrafting` input
action (`K` key / gamepad West button) added to
`InputSystem_Actions.inputactions`. Lists each known recipe with its
ingredient cost (shown as "have/need" per ingredient) and a Craft
button, disabled when the player can't currently afford it; refreshes
automatically off `Inventory.Changed` (same observation pattern as
InventoryUIController) so affordability updates live as items are
gathered/consumed.

Since Inventory and Crafting are both full-screen modal panels drawn
the same way, opening either now closes the other (each controller
holds an optional reference to the other and calls its new `Hide()`
method). This required one small preserving refactor:
`PlayerInputHandler.MenuOpen` changed from a single bool to an
internal request count (`SetMenuOpen(bool)` keeps its exact same
signature/behavior for existing callers) so that two panels closing
each other in sequence can't clobber the "is any modal open" state ---
documented in the property's own doc comment.

Placeholder test content (final recipes are explicitly "Not Yet
Decided"): `ScriptableObjects/Items/StoneAxe.asset` (ItemDefinition,
Tool category) and `ScriptableObjects/Recipes/StoneAxeRecipe.asset`
(RecipeDefinition: 3x Wood + 2x Rock -> 1x Stone Axe), both
hand-authored directly the same way Wood/Rock were. Added
`Scripts/Editor/CraftingSanityChecks.cs` (same editor-utility pattern
as ItemStackSanityChecks/InventorySanityChecks) to verify
RecipeDefinition's CanCraft/TryCraft math on every recompile.

CONFIRMED --- developer added `PlayerCrafting` to Player (with
StoneAxeRecipe assigned as a known recipe), created the Panel Settings
+ UIDocument for the crafting panel, cross-wired the two panel
controllers, and verified the Step 11 playtest (craft Stone Axe, panels
closing each other, live affordability). Crafting increment 1 is now
confirmed end-to-end.

Developer feedback (5-part request, delivered as one increment since
all five touch the same inventory/UI/player-movement surface):
1. Inventory and Crafting panels should no longer be full-screen modals
that close each other --- both should be able to stay open at once,
anchored to opposite sides of the screen, without covering the player.
2. Drag-and-drop (LMB) to move/swap items between inventory slots.
3. Per-item weight (kg) with a total carry-weight cap on the inventory,
shown in the panel; going over the cap should slow movement and drain
stamina. 4. Dragging an item out of the inventory panel onto the game
world should drop it. 5. Dropped/world items should have physics (fall
under gravity, collide) rather than floating in place.

Implemented:
- `Inventory` (Scripts/Gameplay/Inventory): added `MaxWeight`/
  `TotalWeight`/`IsOverloaded`, a weight-aware `AddItem` (amount is
  clamped to whatever still fits under MaxWeight before the existing
  stack-filling logic runs; weightless items or an unlimited inventory
  --- MaxWeight <= 0, the default for the two- and five-slot
  inventories the sanity-check editor utilities construct --- are
  never blocked this way, so the pre-existing tests kept working
  unchanged), plus two new slot-index-based operations for drag/drop:
  `MoveSlot` (move into an empty slot, merge onto a same-item stack, or
  swap with a different-item stack) and `RemoveFromSlot` (remove from
  one specific slot, unlike `RemoveItem`'s by-definition search across
  every slot). `ItemStack` gained a `Swap` helper backing the swap
  case.
- `PlayerInventory`: new `_maxWeight` (default 400) passed into its
  `Inventory`; new `DropItem(slotIndex, amount)` that removes from a
  slot and spawns the item into the world via a new
  `ItemDefinition.WorldPrefab` field (a prefab carrying an
  `ItemPickup`) --- fails loudly with a clear Debug.LogError (per
  CLAUDE.md's Error Handling rule) if a definition has no WorldPrefab
  assigned, rather than silently doing nothing. This is a gameplay
  action, so it lives on `PlayerInventory`, not in the UI layer that
  triggers it.
- `ItemPickup`: now `[RequireComponent(typeof(Rigidbody))]` --- every
  pickup, hand-placed or spawned, has real physics; mass comes from
  `ItemDefinition.Weight`. Added `Configure(definition, quantity)` so a
  freshly-instantiated generic WorldPrefab can be assigned its item at
  runtime (hand-placed pickups in the scene still just use the
  Inspector fields directly and never call this).
- `PlayerMovementStats`/`PlayerStaminaStats`: added
  `OverloadSpeedMultiplier` (default 0.5) and `OverloadDrainPerSecond`
  (default 5/sec). `PlayerStamina.TickSprint` gained an `isOverloaded`
  parameter. `PlayerMotor` now takes a required `PlayerInventory`
  reference, reads `Inventory.IsOverloaded` once per frame, and applies
  `OverloadSpeedMultiplier` on top of walk speed; exposed as
  `PlayerMotor.IsOverloaded` and added to `PlayerDebugHud`'s Weight line
  for verification.
- Refinement (playtest feedback): overloaded stamina drain originally
  applied even standing still, and sprint speed still stacked with the
  overload penalty instead of being disabled outright. `TickSprint` now
  forces `sprinting` false whenever `isOverloaded` (Shift no longer
  speeds up an overloaded character at all - overload movement is
  walk-only, matching `OverloadSpeedMultiplier` at its face value
  instead of a sprint speed on top of it) and only applies
  `OverloadDrainPerSecond` while `isMoving` is also true; standing still
  overloaded now regenerates stamina normally instead of draining it.
- UI repositioning: `InventoryPanel.uss`/`CraftingPanel.uss` changed
  from full-screen dimmed overlays (centered, mutually exclusive) to
  small anchored panels (inventory left, crafting right) sized to their
  content, so both can be visible together without covering the
  player. Vertically centered on the screen (not pinned near the top) -
  developer's first screenshot showed the inventory panel overlapping
  `PlayerDebugHud`'s fixed top-left readout box; each root now spans
  the full viewport height with `justify-content: center` instead of a
  fixed `top` offset, well clear of the HUD regardless of resolution.
  Since both can now be open simultaneously,
  `InventoryUIController`/`CraftingUIController` no longer hide each
  other on open (the `_craftingPanel`/`_inventoryPanel` cross-reference
  fields and their `Hide()` calls were removed) --- `Hide()` itself
  stays as a public method for possible future callers (e.g. a future
  "close all UI" action).
- Fix required by the above: both panels used to set
  `Cursor.lockState`/`Cursor.visible` directly in their own
  `SetVisible`, which was safe only because at most one panel could
  ever be open. With both open at once, whichever panel happened to
  close *first* would incorrectly re-lock the cursor while the other
  panel was still open and needed it free. Moved cursor lock/visibility
  into `PlayerInputHandler.SetMenuOpen` itself, driven by transitions
  of its existing open-panel request count (0 -> >0 unlocks, >0 -> 0
  locks) --- the two UI controllers now just call `SetMenuOpen`, no
  longer touch `Cursor` at all.
- `InventoryUIController`: added LMB drag-and-drop using UI Toolkit
  pointer events (`PointerDownEvent`/`PointerMoveEvent`/
  `PointerUpEvent` with `CapturePointer`, matching the standard UI
  Toolkit runtime drag pattern) --- a floating icon ("ghost") follows
  the cursor during a drag; on release, `panel.Pick(...)` finds the
  real element under the cursor (independent of pointer capture) to
  decide the outcome: onto another slot -> `Inventory.MoveSlot`,
  outside the panel bounds entirely -> `PlayerInventory.DropItem` for
  the whole stack (partial-stack drop is a possible future refinement,
  not implemented), anywhere else inside the panel -> cancels. Added a
  weight readout (`Weight: X/Y kg`, `(OVERLOADED)` when applicable) to
  `InventoryPanel.uxml`/`.uss`.
- Added `Prefabs/` content (developer-authored in the Editor, not
  hand-written like the ScriptableObject assets): a single generic
  `ItemPickup_Generic` prefab (any Collider + the now-required
  Rigidbody + `ItemPickup`) reused as the `WorldPrefab` for every
  existing item (Wood/Rock/StoneAxe) --- items don't have distinct 3D
  meshes yet (only a 2D `Icon` sprite for UI), so one shared placeholder
  visual is the right amount of investment for this increment; distinct
  per-item WorldPrefabs are a drop-in future upgrade (swap the
  reference, no code change) once real meshes exist.
- Extended `InventorySanityChecks.cs` with new checks for `MoveSlot`
  (move into empty, swap different items, self no-op, exact-fit merge),
  `RemoveFromSlot` (returns definition/removed count, no-op on an empty
  slot), and weight-limited `AddItem` (partial block, exact-cap
  overload detection) --- same editor-utility pattern as before, still
  logs `[InventorySanityChecks] All checks passed.`.

CONFIRMED --- developer wired `PlayerMotor.Inventory`, created the
`ItemPickup_Generic` prefab and assigned it as `WorldPrefab` on
Wood/Rock/StoneAxe, repositioned both panels to stay clear of
`PlayerDebugHud` (screenshot feedback, see below), and verified the
full Step 12 playtest end-to-end (simultaneous panels, cursor behavior
across independent open/close, drag-to-move, drag-to-drop, overload
speed/stamina behavior including the movement-gated drain and
sprint-block refinement). This closes out the 5-part Inventory/Crafting
UX request.

Inventory/Crafting UX (panels + drag-and-drop + weight + item dropping
+ item physics) is now demonstrably functional end-to-end. Per the Core
Rule's development order
(Foundation -> Player -> Interaction -> Items -> Inventory -> Gathering
-> Crafting -> Workbench -> ...), the next stage is **Workbench**
(`DEVELOPMENT_ROADMAP_v0.1.md` Phase 4: Workbench object/placement,
Workbench levels, and gating existing recipes behind proximity to one -
recipe unlock structure is explicitly deferred further, same as
before).

Workbench increment 1: added `WorkbenchDefinition`
(Scripts/Gameplay/Crafting, ScriptableObject: display name, `Tier`,
`Range`) and `Workbench` (same folder - a workbench is crafting-gating
data/behavior, not a distinct architectural layer, same reasoning
ResourceNode/ItemPickup shared the Items folder). Unlike
ItemPickup/ResourceNode, `Workbench` is proximity-based, not a raycast
`IInteractable`: it drives a trigger `SphereCollider` sized from
`WorkbenchDefinition.Range` and registers/unregisters itself with
`PlayerCrafting` on `OnTriggerEnter`/`OnTriggerExit` - cheap (physics-
driven, not a per-frame distance scan) and correct even though the
Player has a `CharacterController` rather than a `Rigidbody`, since
`CharacterController` itself derives from `Collider` and raises trigger
events like any other collider.

`RecipeDefinition` gained `RequiredWorkbenchTier` (default 0 -
craftable anywhere, so every pre-existing recipe's behavior is
unchanged unless explicitly set otherwise) and `CanCraft`/`TryCraft`
both now take an `availableWorkbenchTier` parameter, checked before
ingredients. `PlayerCrafting` tracks the highest tier among all
currently-overlapping Workbenches as `NearbyWorkbenchTier` (0 if none),
exposes a `NearbyWorkbenchChanged` event, and passes its tier into
`TryCraft`. `CraftingUIController` passes the same tier into
`CanCraft` for the Craft button's enabled state, and - for any recipe
with `RequiredWorkbenchTier > 0` - shows a `Requires Workbench
(Tier N)` line per row (green when satisfied, red when not), refreshed
both on `Inventory.Changed` (already wired) and the new
`NearbyWorkbenchChanged`.

Placeholder content (mirrors the Wood/Rock/StoneAxeRecipe pattern -
final workbench tiers/recipes are still "Not Yet Decided"):
`ScriptableObjects/Workbenches/BasicWorkbench.asset` (Tier 1, Range 4).
`StoneAxeRecipe.asset` now sets `_requiredWorkbenchTier: 1`, so
crafting a Stone Axe demonstrates the gating directly instead of
needing brand-new unverified content to prove the feature works.

Extended `CraftingSanityChecks.cs` with tier-gating coverage (blocked
below the required tier, allowed at/above it, independent of whether
ingredients are otherwise satisfied) and updated every existing
`CanCraft`/`TryCraft` call site (editor checks, `PlayerCrafting`,
`CraftingUIController`) for the new parameter. Added an optional
`PlayerCrafting` reference to `PlayerDebugHud` showing `Workbench:
Tier N nearby` / `Workbench: none nearby` for verification.

CONFIRMED --- developer created `TestWorkbench_Basic` in SampleScene
and verified the full Step 13 playtest (Craft button/requirement line
toggling correctly with proximity to the workbench).

Developer feedback (screenshot reference, a Valheim-style crafting
screen): the crafting panel's flat list-of-rows layout should become a
two-column layout instead - a scrollable list of known recipes on the
left, and the selected recipe's icon/name/description/stats/ingredients
/Craft button on the right.

Reworked `Scripts/UI/CraftingPanel.uxml`/`.uss`/`CraftingUIController.cs`
accordingly (pure UI reskin, no gameplay/data changes - `RecipeDefinition`
/`Inventory`/`PlayerCrafting` are untouched). Left column: a
`ui:ScrollView` of clickable recipe entries (icon + name), the first
known recipe auto-selected on open, selected entry highlighted, entries
whose recipe currently can't be crafted dimmed. Right column: large
icon, title, the output `ItemDefinition.Description`, a small stats
block, the `Requires Workbench (Tier N)` line (moved from its old
per-row spot, same green/red logic), an ingredients row (icon + "have
/need" per ingredient, red when short), and one large Craft button
acting on whichever recipe is currently selected. Panel widened
(560px) and given a fixed-height two-column body to fit the layout.

The stats block intentionally only shows **Weight** - the only stat
`ItemDefinition` actually has right now. Valheim-style
Durability/Slash/Block/Parry/Knockback/etc from the reference
screenshot are not fabricated as placeholder numbers; they belong to
the future Equipment/Combat systems, and "Final weapons and armor
balance" is explicitly listed as Not Yet Decided. The stats block is
written to extend easily (`AddStatRow(label, value)`) once real
combat/tool stats exist on `ItemDefinition`.

CONFIRMED --- developer verified the two-column crafting UI in Play
mode (also asked for, and got, a visual divider border between the
recipe list and detail columns - `.crafting-sidebar` gained a
`border-right`/`padding-right`, pure USS, no code change).

Workbench increment 1 (data, proximity gating, UI) is now fully
confirmed end-to-end. Per the Core Rule's development order, the next
stage would be **Building** (`DEVELOPMENT_ROADMAP_v0.1.md` Phase 5) -
but the developer explicitly asked to pause here rather than start it
now. Wait for the developer to say go before beginning Building work.

Developer said go. Building increment 1: added `BuildingPieceDefinition`
(Scripts/Gameplay/Building, ScriptableObject: display name, icon, cost
- reuses `RecipeIngredient` from Crafting rather than a new item+amount
type, since it's the same concept - and a `Prefab` used for both the
ghost preview and the real placed piece) and `BuildingPiece` (same
folder, `IInteractable` marker on a placed piece: E demolishes it, no
resource refund yet - explicitly deferred, same style as ResourceNode's
deferred tool requirement). Added `PlayerBuilding` (same folder,
mirrors PlayerCrafting: fixed serialized list of known pieces, no
unlock system yet) which owns build mode: on `ToggleBuild` it spawns a
ghost (an inactive-collider, kinematic-Rigidbody clone of the selected
piece's own `Prefab` - not a separate ghost asset) that follows the
camera's aim raycast each frame, tinted with a shared valid/invalid
material based on whether the raycast hit within range (`IsPlacementValid`)
and whether the piece is affordable (`CanAffordSelected`).
`RotatePiece` adds a fixed yaw step to the ghost; `Next`/`Previous`
(pre-existing unused actions from the input template) cycle the
selected known piece and respawn the ghost for it. Placing
(`Attack`/LMB, also a pre-existing unused action) pays the cost via
`Inventory.RemoveItem` and instantiates a fresh real piece at the
ghost's transform, `Configure()`d with its definition; build mode stays
active afterward so multiple pieces can be placed in a row (matches
survival-game convention, e.g. Valheim) - ToggleBuild again exits.

Reused three previously-unused actions from the input system template
instead of adding redundant new ones: `Attack` (LMB) now doubles as
"place ghost" while build mode is active - the same button Combat will
later use for melee attacks, matching how a game's primary click does
whatever the currently equipped tool/mode says it does; `Next`/`Previous`
(`2`/`1` keys) cycle the selected building piece. Two new actions were
added to `InputSystem_Actions.inputactions`: `ToggleBuild` (`B` key /
gamepad right shoulder) and `RotatePiece` (`R` key / gamepad left
shoulder). `PlayerInputHandler` exposes all five as events
(`AttackPerformed`, `ToggleBuildPerformed`, `RotatePiecePerformed`,
`CycleNextPerformed`, `CyclePreviousPerformed`); `Attack`/`RotatePiece`/
`CycleNext`/`CyclePrevious` are suppressed while `MenuOpen` (same
treatment as Jump/Interact); `ToggleBuild` is not suppressed (same
treatment as ToggleInventory/ToggleCrafting - a toggle should always be
able to fire).

Deliberately out of scope for this increment (explicit Roadmap Phase 5
items, deferred): **Snapping** (pieces don't snap to each other/a grid
yet - placement is a free raycast hit point), **Save building state**
(needs the Save system, not started), and **Basic structural
validation** (no overlap-with-other-objects check, no support/stability
check - "Placement validation" in this increment means only "the aim
ray hit something within `_maxPlacementDistance`"). No dedicated
Building UI panel either - piece selection/cost/validity is shown via
`PlayerDebugHud`'s new `Build: ...` line only, matching how Gathering
increment 1 shipped without its own UI.

Placeholder content (final building pieces are "Not Yet Decided", same
status as recipes/resources): developer created two
`BuildingPieceDefinition` assets and their Cube-based prefabs
(`Prefabs/Build/WoodWall.prefab`, `WoodFoundation.prefab`), two ghost
materials, and added `PlayerBuilding` to Player - Step 14 confirmed.

Building increment 2 (developer feedback from playtesting increment 1):
pieces were sinking halfway into the ground (position anchored on a
piece's center, not its bottom) and reach felt short (8m). Fixed the
ground anchor and raised reach to 15m. Added basic Valheim-style
snapping - `BuildingSnapPoint` marker children (hand-placed per prefab)
plus `PlayerBuilding.TryApplySnap` pull a nearby ghost's position *and*
rotation onto a placed piece's matching point (e.g. a wall lies flush
along whichever foundation edge it's brought close to, two foundations
end up parallel). One real bug hit along the way: the ground-anchor fix
initially silently no-offed itself because its bounds were read from an
already-disabled `Collider` (collapses to zero) - fixed by reading
`Renderer.bounds` before disabling anything. Step 15 (snap point
authoring) and Step 16 (per-point rotation for the two side-facing
foundation points) are both developer-confirmed; snapping now aligns
position and rotation correctly.

Building increment 2 works end-to-end, but per the developer's explicit
request it is **not** being marked "done" the way Workbench was - see
the "Technical Debt" section near the top of this file for the full
list of known gaps (snapping has no socket-type system and is entirely
hand-authored, no structural validation, no save/load, no demolish
refund, no dedicated UI). Per the Core Rule's development order, the
next stage would be **Combat** (Phase 7) - not started, waiting for the
developer to say go, same pause pattern as before Building itself
started.

Developer asked for a real gameplay HUD (UI Toolkit, same "HTML-like"
approach as Inventory/Crafting) rather than continuing to rely on
`PlayerDebugHud`'s `OnGUI` readout for this: an HP bar, a 6-slot
hotbar, and a stamina bar that only appears while stamina is actually
being spent. This isn't a roadmap stage on its own - it's presentation
work layered on existing (Stamina, Inventory) and new-but-minimal
(Health) gameplay state, done because the developer asked for it now
rather than waiting for a later "Polish" phase.

Added `PlayerHealthStats` + `PlayerHealth` (Scripts/Gameplay/Player) -
intentionally minimal (current/max HP, `TakeDamage`/`Heal`, a `Changed`
event) purely to give the new HP bar something real to bind to; see the
"Technical Debt" note above - this is *not* Phase 6's "Health" system,
no regen/food/death yet. `PlayerStamina` gained `IsDraining` (true only
on a frame that's actually spending stamina - sprinting or the passive
overload drain - false while idle/walking/regenerating), computed
alongside its existing sprinting bool in `TickSprint`.

Added `PlayerHudController` + `PlayerHud.uxml`/`.uss` (Scripts/UI),
following the same UIDocument-per-controller pattern as
InventoryUIController/CraftingUIController, but always-visible (no
`ToggleX` action, no `MenuOpen` interaction - unlike Inventory/Crafting
this isn't a togglable panel). Layout is absolutely positioned within
one `hud-root`, each anchored 24px off its edge (matching the
Inventory/Crafting panels' existing 24px-from-edge convention, i.e.
"near the corner, not flush against it"): hotbar top-left (horizontal,
6 slots - matches `InventoryPanel`'s per-row column count, so the
hotbar's width lines up with the inventory grid's, at the developer's
request - read-only mirror of `PlayerInventory.Inventory`'s first 6
slots; reuses the existing slot data/rendering rather than a separate
hotbar container, so dragging an item into one of those 6 slots in the
main Inventory panel makes it appear here automatically), HP bottom-left
(vertical fill bar, bottom-anchored), stamina bottom-center (horizontal
fill bar, same 24px bottom offset as HP - "the same level" - centered
via `left: 50%` + a fixed negative `margin-left`). Health/hotbar refresh
on their respective `Changed` events; stamina has no such event (it
drains/regens continuously) so it's polled once per `Update`, same
approach `PlayerDebugHud` already uses for the same field - display
toggles `Flex`/`None` off `PlayerStamina.IsDraining` directly, so the
bar is invisible whenever nothing is being spent, exactly as requested.
No hotbar slot selection/quick-use yet - that needs an Equipment/item-
use system that doesn't exist (Phase 2's "Equipment slots" and Phase
4's "Basic tools" are both still unchecked); this increment is display-
only, matching the request ("быстрый инвентарь" as a visible bar, not
an activation system).

NOT YET CONFIRMED --- needs a `PlayerHealthStats` asset, `PlayerHealth`
added to Player, and a Panel Settings + UIDocument for the new HUD
(reusing the existing `NAV_PanelSettings.asset`); see
`UNITY_SETUP_NEXT_STEPS.md` Step 17.

Developer confirmed Step 17 works. Follow-up feedback: the stamina bar
should stay visible until stamina is *fully* restored (not just while
`IsDraining`), and should fade rather than snap away. Changed
`PlayerHudController.RefreshStamina` to base visibility on "not at max"
instead of `IsDraining`, and switched from toggling `style.display`
(binary, can't be animated) to animating `style.opacity` - the actual
fade is a USS `transition-property: opacity` on `.stamina-bar`
(`PlayerHud.uss`), not code-driven interpolation. No Editor
reconfiguration needed, code/USS only; see the addendum to
UNITY_SETUP_NEXT_STEPS.md Step 17.

Developer asked for a loading screen and main menu. Same category as the
HUD work above - presentation layered on top of what exists, not a
roadmap stage - done now because asked for, not deferred to a future
"Polish" phase.

Added `Assets/NAV/Scripts/Core/SceneLoader.cs`: the project's first
Core-layer script. Drives `SceneManager.LoadSceneAsync` for a scene by
name, reports `0..1` progress via a plain event (no UI dependency,
matches ARCHITECTURE_v0.1.md's "UI observes state" rule generalized to
Core), and enforces a small minimum display duration (default 0.5s)
purely so the loading screen doesn't flash for a single frame when
loading today's near-empty `SampleScene`. Single-scene load (not
additive) - no `DontDestroyOnLoad` needed since the loader's own scene
is replaced as part of the same transition once activation is allowed.

Added `Scripts/UI/MainMenuUIController.cs` + `MainMenuPanel.uxml`/`.uss`
(title, Play/Quit buttons) and `Scripts/UI/LoadingScreenUIController.cs`
+ `LoadingScreenPanel.uxml`/`.uss` (progress bar), following the same
UIDocument-per-controller pattern as every other UI panel, reusing
`NAV_PanelSettings.asset` and the existing dark-wood/gold palette
(`rgb(43,33,24)` panel, `rgb(139,105,20)` gold border/accent,
`rgb(230,210,160)` cream text). Play hides the menu, shows the loading
screen, and calls `SceneLoader.LoadScene("SampleScene")`. Quit calls
`Application.Quit()` (stops Play Mode in the Editor instead, via
`#if UNITY_EDITOR`).

Deliberately no Continue/Settings buttons - there's no save system or
settings menu yet (both explicitly future work; see "Not Yet Decided"),
and a button that does nothing would be dishonest UI, not a minimal
increment.

This introduces the project's first second scene: a new `MainMenu`
scene (alongside the existing `SampleScene`, both in `Assets/Scenes/` -
matching where `SampleScene` actually lives, not the aspirational
`Assets/NAV/Scenes/` path in ARCHITECTURE_v0.1.md, which has stayed
unused). `MainMenu` becomes Build Settings scene index 0, `SampleScene`
index 1. `SampleScene` itself is untouched - still fully playable by
opening it directly and pressing Play, bypassing the menu, for regular
gameplay-system development/testing.

CONFIRMED --- developer created the `MainMenu` scene, wired
`SceneLoader`/`MainMenuUI`/`LoadingScreenUI`, added both scenes to Build
Settings (`MainMenu` index 0, `SampleScene` index 1), and verified the
Step 18 playtest end-to-end (menu -> Play -> loading screen ->
gameplay).

Developer confirmed Step 18 works, then asked for ESC pause. Added a
new `Pause` input action (Escape / gamepad Start, Player map) and
`PlayerInputHandler.PausePerformed` (fires unconditionally, same as
ToggleInventory/ToggleCrafting - not suppressed by `MenuOpen`). Added
`Scripts/UI/PauseUIController.cs` + `PausePanel.uxml`/`.uss`: a
center-screen overlay (Resume / Leave to Main Menu) toggled by
`PausePerformed`, living only in `SampleScene` (pause has no meaning in
the menu scene itself). While open it sets `Time.timeScale = 0` -
actually halts gameplay simulation (movement, stamina drain, physics),
not just input - and calls the same `PlayerInputHandler.SetMenuOpen`
request-counted cursor/camera-freeze mechanism every other modal panel
uses. Resume sets `timeScale` back to 1 and closes; Leave to Main Menu
does the same then `SceneManager.LoadScene("MainMenu")` directly - no
async loading screen for this transition, since `SampleScene` has no
`SceneLoader`/loading-screen instance of its own and duplicating
`MainMenu`'s just for the reverse hop isn't warranted yet.

Deliberately out of scope for this increment: Escape does not close
Inventory/Crafting first if they're open - the pause overlay just draws
on top of them (its `UIDocument` needs a higher Sort Order, see
UNITY_SETUP_NEXT_STEPS.md Step 19). No Settings from the pause menu
either, same reasoning as the main menu (no settings system exists).

CONFIRMED --- developer created the `PauseUI` GameObject in
`SampleScene` (UIDocument + PauseUIController, Sort Order 10) wired to
Player's `PlayerInputHandler`, and verified the Step 19 playtest
(Escape toggles pause, timeScale/cursor behave correctly, Resume and
Leave to Main Menu both work).

Developer feedback (screenshot): the loading screen's progress bar read
as completely static - Unity's real `AsyncOperation.progress` is coarse
and jumps straight to 1 for today's near-empty `SampleScene`. Changed
`LoadingScreenUIController`: the fill now eases toward the real target
each frame instead of snapping (`Mathf.MoveTowards`, unscaled time), a
semi-transparent shimmer sweeps back and forth across the track
continuously (independent of real progress - purely a "something is
happening" signal), and the "Loading" label cycles an animated ellipsis
(`Loading` -> `Loading...`). No new serialized fields/Editor wiring -
same `LoadingScreenUI` GameObject as Step 18, code/UXML/USS only; see
the addendum to UNITY_SETUP_NEXT_STEPS.md Step 18.

CONFIRMED --- developer verified the loading screen now visibly
animates (fill eases in, shimmer sweeps, dots cycle). Main menu, loading
screen, and ESC pause are all confirmed end-to-end. This closes out the
menu/pause presentation work; per the Core Rule the next unstarted
system is still **Combat** (Phase 7) - waiting for the developer to say
go, same pause pattern as before Building started.

Developer said go. Combat increment 1: implemented the whole Phase 7
checklist in one increment (Damage system, Hit detection, Melee
attacks, Weapon definitions, Blocking, Parrying, Stamina interaction) -
they're tightly coupled (blocking/parrying can't be verified without
something dealing damage back, which itself needs hit detection/damage
to exist first), matching how prior systems (Gathering, Building,
Workbench) each shipped as one coherent first pass rather than being
split further.

Added `IDamageable` (Scripts/Gameplay/Combat) - a small interface
(`CurrentHealth`/`MaxHealth`/`IsAlive`/`TakeDamage`) that creatures
(Phase 8) will implement too once they exist. `PlayerCombat` implements
it itself (not `PlayerHealth`) so block/parry mitigation has exactly one
place to live - `PlayerHealth` stays the dumb HP store it already was
(see the existing Technical Debt note on it), `PlayerCombat.TakeDamage`
decides how much of an incoming hit actually reaches it before calling
`PlayerHealth.TakeDamage`. Added `WeaponDefinition` (ScriptableObject:
Damage, Range, AttackCooldown, AttackStaminaCost,
BlockDamageReduction, BlockStaminaCostPerHit, ParryWindowSeconds) - no
Equipment system exists yet (Phase 2/4 both still unchecked), so
`PlayerCombat.EquippedWeapon` is a single fixed serialized reference,
the same "no unlock/equip system yet" deferral used by
`PlayerCrafting`'s known recipes and `PlayerBuilding`'s known pieces;
block/parry stats live on the weapon itself rather than a separate
shield type since there's no dual-wield/offhand system to hang a shield
off of yet.

`PlayerCombat`'s attack is a camera-forward raycast at the weapon's
Range (same idiom `PlayerInteractor`/`PlayerBuilding` already use),
gated by cooldown and stamina, hitting whatever `IDamageable` the ray
finds via `GetComponentInParent` (same pattern `PlayerInteractor` uses
for `IInteractable`). Blocking is a held button (`PlayerInputHandler`
gained `BlockHeld`, mirroring `SprintHeld`'s "read every frame, zeroed
while a modal panel has focus" treatment, plus a new `Block` input
action - RMB / gamepad left trigger). A hit landing within the weapon's
`ParryWindowSeconds` of Block starting to be held is a full-negation
parry (no stamina cost); otherwise a held block reduces damage by
`BlockDamageReduction` and costs `BlockStaminaCostPerHit`. Attack
explicitly no-ops while `PlayerBuilding.IsBuildModeActive` (via an
optional cross-reference), preserving the existing "LMB does whatever
the current tool/mode says" rule now that both Building and Combat want
it. `PlayerStamina` gained a small `Spend(amount)` method (flat one-time
deduction, unlike `TickSprint`'s continuous per-second drain) for the
attack/block stamina costs, with no changes to `TickSprint` itself.

Added `CombatDummy` (Scripts/Gameplay/Combat) as the placeholder test
target - the same role `DebugInteractable` played for
`PlayerInteractor` before real interactables existed. It has its own
health and, via a trigger radius (same proximity pattern `Workbench`
already uses), periodically attacks whatever `IDamageable` is standing
nearby - specifically so Block/Parry have something to verify against.
Explicitly **not** a Creature system: no perception, target selection,
or pathing; real enemy AI stays Phase 8 (Creatures), out of scope here.
Requires its own separate non-trigger Collider (the placeholder mesh's
own Capsule Collider) for the player's attack raycast to hit, since the
auto-added SphereCollider is trigger-only (detection range) and ignored
by that raycast.

Deliberately out of scope for this increment: Equipment (weapon
swapping from inventory), real creature AI, player death/respawn
(`PlayerHealth` still just stops at 0 HP, per its existing Technical
Debt note), attack/block animations (no character model/Animator in the
project yet), and hit VFX/audio (final audio/music is "Not Yet
Decided").

Placeholder content (final weapon balance is "Not Yet Decided", same
status as recipes/resources/building pieces):
`ScriptableObjects/Weapons/StoneSword.asset` (Damage 15, Range 2.5,
AttackCooldown 0.6s, AttackStaminaCost 10, BlockDamageReduction 50%,
BlockStaminaCostPerHit 10, ParryWindowSeconds 0.25s), hand-authored the
same way Wood/Rock/StoneAxe/BasicWorkbench were. `PlayerDebugHud` gained
a `Combat: <weapon> (blocking: True/False)` line. NOT YET CONFIRMED ---
needs `PlayerCombat` added to Player (with StoneSword assigned) and a
`CombatDummy_Basic` test object in SampleScene; see
`UNITY_SETUP_NEXT_STEPS.md` Step 20.

Playtest feedback: attacking hit nothing even standing right next to
`CombatDummy_Basic`. Root cause was the exact same bug already fixed
once for `PlayerInteractor` (see the 2026-08-21 change log entry) -
`PlayerCombat`'s raycast used the weapon's `Range` as the ray length
itself, starting from the camera. In third person the camera sits well
behind/above the player, so most of a short range (2.5m) was consumed
just reaching the character before the ray could reach anything beside
them. Fixed the same way: the ray still starts at the camera (avoids
the player's own collider blocking it) and now casts out to a new,
more generous `_maxAimDistance` (default 15, matching
`PlayerInteractor`/`PlayerBuilding`'s existing convention), but a hit
only counts as in range if `Vector3.Distance(transform.position,
hit.point) <= _equippedWeapon.Range`. Also added Debug.Log lines for
the miss/no-IDamageable cases (previously silent), so a playtester can
immediately tell from the Console whether the ray hit nothing, hit
something without IDamageable, or landed a real hit. No new manual
Editor step - `_maxAimDistance` is a brand-new serialized field, so an
already-placed `PlayerCombat` picks up its code default (15)
automatically on recompile, unlike the old Building distance fix which
needed a manual Inspector update because that field already existed
with a stale serialized value.

Developer asked for a content design pass: describe all plannable
craftable items (weapons, resources, etc.), with a Hammer as the main
building tool upgrading Wood -> Titanium, Valheim-style tiers each
gated behind a matching Workbench tier. Added
`ITEMS_AND_CRAFTING_v0.1.md` (new doc, same versioned-filename
convention as GDD/ARCHITECTURE/ROADMAP) - a 6-tier draft (Wood/Stone ->
Copper/Bronze -> Iron -> Silver -> Bulat -> Skymetal/"Titanium") mapping
each tier to a Workbench tier, listing resources/tools/weapons/armor/
building pieces per tier, plus a section separating what already exists
in code from what needs a separate architecture decision before
implementation (in-place Workbench tier upgrades, tool-gated gathering,
new crafting stations, new biomes for Tier 3+ ores, a Hide/meat source
once Creatures exists). Explicitly framed as a draft, not final content
- matches the existing "Not Yet Decided" status of the final resource/
recipe/tier lists.

Developer said go on Tier 1 implementation. Added the first missing
slice of Tier 1 content (Wood/Rock/StoneAxe/StoneSword/WoodWall/
WoodFoundation already existed) - pure content on top of already-built
systems, no code changes: `Flint`/`Resin` (ItemDefinition, new Tier 1
resources) with `FlintDeposit`/`ResinNode` (ResourceNodeDefinition,
same folder/pattern as `TreeWoodNode`/`StoneRockNode`); `WoodHammer`
(ItemDefinition, Tool) with `WoodHammerRecipe` (5x Wood,
RequiredWorkbenchTier 0 - craftable anywhere, since per the design doc
the Hammer is what places the very first Workbench) and `StonePickaxe`
(ItemDefinition, Tool) with `StonePickaxeRecipe` (3x Wood + 3x Rock,
RequiredWorkbenchTier 1, mirrors the existing `StoneAxeRecipe` exactly).
All hand-authored the same way as Wood/Rock/StoneAxe/BasicWorkbench.
Neither Hammer nor Pickaxe gate anything mechanically yet (same as
StoneAxe before them) - tier-gating Building pieces by hammer tier is
explicitly deferred, per the design doc's own "needs a separate
architecture decision" list, since only Tier 1 exists so far and there
is nothing yet for a tier check to differentiate.

Noted while authoring this content: none of the four new items have a
`WorldPrefab` assigned - the project no longer has a single generic
`ItemPickup_Generic` placeholder (Wood/Rock now use real meshes from
the imported art packs, e.g. `Broken log_1.prefab`/
`SM_Rocks_01_item.prefab`), so a world-drop visual has to be picked per
item by the developer rather than defaulted. Until assigned, dropping
these items fails loudly via the existing `PlayerInventory.DropItem`
error path (same as `StoneAxe` today) - pickup/crafting/inventory are
unaffected.

Per the developer's request, Unity setup steps for this content live in
a **separate** file, not appended to `UNITY_SETUP_NEXT_STEPS.md`:
`UNITY_SETUP_TIER1_ITEMS.md` (test resource nodes for Flint/Resin,
adding the two new recipes to `PlayerCrafting.Known Recipes`, full
playtest). NOT YET CONFIRMED.

Developer feedback on the above: Resin having its own dedicated
resource node felt wrong when it's conceptually "the same place" as
Wood (a tree) - asked for one resource node to be able to yield
multiple different items with a per-item chance, rather than one node
per item. This is a real code change (`ResourceNodeDefinition`/
`ResourceNode`), not just content: `ResourceNodeDefinition._dropItem`/
`_amountPerHit` (single item) replaced with `_drops` (`List<
ResourceNodeDrop>`) - a new small serializable type
(Scripts/Gameplay/Items/ResourceNodeDrop.cs, same "plain data class
nested in a list" shape as `RecipeIngredient`, but with its own
`Chance` field, which `RecipeIngredient` has no use for, hence a
separate type rather than reusing it). `ResourceNode.Interact()` now
rolls each drop entry independently (`Random.value <= drop.Chance`)
per hit, so a single hit can yield several different items at once.
Preserves the original "a hit that fits nothing in the inventory
doesn't consume the node's hit counter" behavior, generalized: a hit
is only "wasted" (no counter decrement, no depletion feedback) if at
least one drop rolled true but every rolled drop failed to fit -
a drop simply not rolling due to its own chance is normal, not a
failure, and still consumes a hit.

Migrated the existing node assets to the new schema (same values, no
behavior change) - `StoneRockNode`/`FlintDeposit` each keep their single
drop at 100% chance. `TreeWoodNode` gained a second drop entry: `Resin`
at 25% chance (alongside the existing 100%-chance `Wood`) - this is
exactly the developer's requested change, folding Resin into the tree
node instead of a separate one. The standalone `ResinNode.asset`
(created earlier this same session, nothing else referenced it yet) was
deleted rather than left dangling. Updated `UNITY_SETUP_TIER1_ITEMS.md`
accordingly (dropped the now-unnecessary "create a Resin test node"
step; the existing `TestResourceNode_Tree` picks up the new Resin drop
automatically once Unity recompiles, since it already points at the
same `TreeWoodNode.asset`). NOT YET CONFIRMED.

Further developer correction, same session: per-hit direct-to-inventory
gathering (even with the new multi-drop chance roll above) wasn't what
was wanted either - a felled tree should disappear and scatter all its
yield into the world as physical pickups, not silently fill the
inventory on every swing. Reworked `ResourceNode.Interact()`: interim
hits (before the node is fully depleted) now give no reward at all,
only the existing shrink depletion-feedback; the hit that empties
`_remainingHits` destroys the node and calls a new `SpawnDrops()`,
which rolls each `Definition.Drops` entry independently and spawns the
ones that hit as physical `ItemPickup` objects scattered around the
node's position (small random horizontal offset + an outward/upward
physics impulse) - the player then has to walk over and Interact (E)
with each one individually, same as any other world item.

This needed a shared "put an item into the world as a physical pickup"
code path, since that logic previously only existed inside
`PlayerInventory.DropItem` (used when the player throws an item out of
their inventory) and `ResourceNode` now needs the same thing for a
different reason. Extracted it as a new static `ItemPickup.
SpawnInWorld(definition, quantity, position, rotation, impulse)`
- `PlayerInventory.SpawnWorldItem` now just computes its
player-relative spawn position/impulse and calls this; its externally
observable behavior (including the "no WorldPrefab assigned" error) is
unchanged. `ResourceNodeDrop._amountPerHit` renamed to `_amount`/
`Amount` (public API rename, safe since this feature isn't confirmed
yet) - it now means the total quantity scattered on depletion, not a
per-hit amount, since hits before depletion no longer yield anything.
`HitsToDeplete` is now a fully independent "toughness" stat, no longer
implicitly tied 1:1 to total reward. Migrated the three existing node
assets to preserve the same total yield as before this change
(`TreeWoodNode`: Wood 3, Resin 1 at 25% - unchanged from the multi-drop
step above; `StoneRockNode`: Rock 10; `FlintDeposit`: Flint 2).

Important consequence flagged in `UNITY_SETUP_TIER1_ITEMS.md`: gathering
itself now requires `ItemDefinition.WorldPrefab` to be set (previously
only *dropping from inventory* needed it - direct-to-inventory gathering
didn't). `Wood`/`Rock` already have one; `Flint`/`Resin` do not (no
generic placeholder prefab exists in the project anymore - see the
2026-08-31 Tier 1 entry above), so depleting `FlintDeposit` or rolling
Resin will currently fail loudly with a "no WorldPrefab assigned"
Console error until the developer assigns one. Added a new required
"Шаг 0" to `UNITY_SETUP_TIER1_ITEMS.md`: create one small reusable
placeholder pickup prefab (`ItemPickup_Placeholder`, a scaled-down
Sphere + ItemPickup, the same recipe the old `ItemPickup_Generic` used)
and assign it as `Flint`/`Resin`'s `WorldPrefab` before testing. NOT YET
CONFIRMED.

Developer imported a character model + animation pack ("Kevin Iglesias
- Human Character Dummy" and "Human Animations", both under
`Assets/Kevin Iglesias/`, both Humanoid-rigged - confirmed via their
.fbx.meta `animationType: 3`) and asked for animation setup: smooth
locomotion, the character always facing the camera's direction, and
all movement animations blended together so changing direction reads
naturally. Checked the scene first - `Player` has no model/Animator
wired yet; the developer had only created one color-variant prefab
(`Assets/NAV/Prefabs/Player/HumanDummy_F Red.prefab`) without placing
it under Player. Also confirmed `PlayerMotor` already locks the body's
yaw to the camera's yaw every frame (see the 2026-08-21 change log
entry) - "character always faces camera direction" is already true and
needed no code change; what was actually missing was the animation
side. Noted in `UNITY_SETUP_ANIMATION.md` that the developer's
parenthetical about an "opposite direction" exception doesn't map to
anything the camera system currently does (`ThirdPersonCameraController`
has no wall-collision/front-facing mode) - flagged for the developer to
clarify or confirm current behavior is what they meant, rather than
guessing at an exception with no obvious code home.

Added `PlayerAnimator` (Scripts/Presentation/Animation - new subfolder,
same reasoning as the pre-existing Presentation/Camera one): reads
`PlayerInputHandler.MoveInput` directly as the Animator's MoveX/MoveY
(no transform needed, since body yaw already equals camera yaw - raw
WASD input is already character-local) via the damped
`Animator.SetFloat(name, value, dampTime, deltaTime)` overload for
smoothing, plus `IsSprinting`/`Grounded` from `PlayerMotor`. This is
the entire code side - the rest (Animator Controller, blend trees) is
Editor-graph authoring that can't be safely hand-authored the way
ScriptableObject content has been (FBX sub-clip references inside an
Animator Controller aren't something to guess blind).

Unity setup steps in a new separate file (same "separate topic, own
file" precedent as `UNITY_SETUP_TIER1_ITEMS.md`):
`UNITY_SETUP_ANIMATION.md` - place the model under Player, build one 2D
Freeform Directional "Locomotion" blend tree (Idle center + 8 walk
directions, all in one continuous blend space - this is what makes
direction changes read as natural blending instead of discrete
animation swaps), a second "Sprint Locomotion" blend tree (the anim
pack has no backward/diagonal-backward sprint clips, so those three
positions borrow the Run pack's backward clips as a stand-in, flagged
as a known compromise) crossfaded in via `IsSprinting`, and a minimal
`Grounded`-gated Jump state (single Fall pose, not full begin/land
phases - explicitly out of scope, this wasn't what was asked). Also
flagged: use the non-`[RM]` (non-root-motion) clip variants and turn
off the Animator's Apply Root Motion, since `PlayerMotor`/
`CharacterController` already fully own player movement - baked root
motion on top would fight it. NOT YET CONFIRMED.

Developer asked to continue with attack/block/gathering/building
animations next. Investigated the imported animation pack first (per
CLAUDE.md's "inspect before implementing" rule) and found a hard
blocker: `Assets/Kevin Iglesias/Human Animations/` only contains
locomotion (Idle/Walk/Run/Sprint/Turn/Jump) and Conversation clips,
plus two static hand-grip poses (`Masked Poses/`) - there is no attack
swing, block, mining, or hammering clip anywhere in the project's
imported assets (checked every other pack too - PolyOne, Innerverse
Interactive, Static Soul Studio, ADG_Textures are all
environment/texture content, nothing rigged). Flagged this to the
developer rather than guessing at an Animator graph with nothing to
put in its states (same reasoning already on record for why Locomotion
itself needed real clips, not placeholders). Waiting on the developer
for how to proceed (source more clips e.g. via Mixamo against the
existing Humanoid rig, vs. a placeholder-motion version of the
Animator graph now) - not started.

While that was pending, developer redirected to a smaller, immediately
useful request: a center-screen aim crosshair, since without the F1
debug HUD it's hard to tell what the player is aiming at (for
gathering/interacting or attacking). Added to the existing always-on
`PlayerHud` (not a new panel) - a ring + dot (`PlayerHud.uxml`/`.uss`)
that changes color based on aim state, computed in
`PlayerHudController.RefreshCrosshair` (polled every frame, same
pattern as its existing stamina refresh) purely from state other
systems already expose: build mode's placement validity
(`PlayerBuilding.CanPlace`/`IsBuildModeActive`) takes priority when
active, then `PlayerInteractor.CurrentInteractable != null`
(interact/gather/pickup), then a new small read-only
`PlayerCombat.HasTargetInSight` (a non-mutating copy of the attack
raycast's aim/range logic - camera-forward ray, range measured from
the player's position, same split already used for the real attack -
that finds a live `IDamageable` without triggering damage/cooldown/
stamina), then a neutral fallback. Hidden entirely while
`PlayerInputHandler.MenuOpen` (Inventory/Crafting open, cursor free) -
nothing to aim at. `PlayerHudController` gained three new required
serialized fields (`InputHandler`/`Interactor`/`Combat`) and one
optional one (`Building`, only used to tint the crosshair during
building) - NOT YET CONFIRMED, needs the existing `PlayerHud` object's
Inspector updated; see the new addendum to
`UNITY_SETUP_NEXT_STEPS.md` Step 17.

Developer confirmed Tier 1 fully done, then asked to continue items/
crafting - offered a choice of the remaining Tier 1 items
(`ITEMS_AND_CRAFTING_v0.1.md` section 4) rather than assuming; they
picked Door/Roof/Palisade. Pure content, zero code changes -
`BuildingPieceDefinition`/`BuildingPiece`/`BuildingSnapPoint` are
already fully generic (same classes `WoodWall`/`WoodFoundation` use),
so this is Editor-authored content exactly like Step 14. `WoodDoor`
matches `WoodWall`'s exact size/snap layout (same opening, same
Foundation-edge attachment) - explicitly **not** an openable door,
just a wall-slot filler; real open/close is a new interaction
mechanic, not content, and wasn't started (flagged rather than
silently added, per CLAUDE.md's "No Silent Architecture Changes").
`WoodRoof` mirrors `WoodFoundation`'s flat-plate shape/cost but with
its 4 edge-midpoint snap points on the underside instead of the top,
so it attaches downward onto whatever it's resting on. To let a Roof
actually attach to a Wall's top (not just a Foundation's top), added
two more snap points to the existing `WoodWall` prefab
(`SnapPoint_TopLeft`/`SnapPoint_TopRight`) alongside its Step 15
bottom-corner pair - a small natural extension of already-placed
content, not a new mechanic. `WoodPalisade` is a new standalone
tall/thin shape (bottom-corner snap points only, for chaining palisades
edge-to-edge in a row). NOT YET CONFIRMED - see the new
`UNITY_SETUP_NEXT_STEPS.md` Step 21.

CONFIRMED - developer wired the new `PlayerHudController` fields
(InputHandler/Interactor/Combat/Building) and the `ItemPickup_Placeholder`
prefab (assigned as `Flint`/`Resin`'s WorldPrefab per
`UNITY_SETUP_TIER1_ITEMS.md`'s Step 0), then completed the rest of that
file's playtest end-to-end. Tier 1 content
(Flint/Resin/WoodHammer/StonePickaxe + their recipes, the multi-drop
resource node rework, and the aim crosshair) is now fully confirmed.
Per `ITEMS_AND_CRAFTING_v0.1.md` section 4's own "left for next
increment" list, still open within Tier 1: a second/third weapon (Wood
Club, Wood Bow + Stone Arrow - the latter needs a ranged/projectile
system that doesn't exist yet, ranged combat has never been
implemented, only melee), Hide Armor (blocked on Creatures/Phase 8 for
a Hide source), and Door/Roof/Palisade building pieces. Final
models/icons for the Tier 1 placeholders are explicitly out of scope
(asset strategy is "Not Yet Decided"). Developer asked to continue
items/crafting work next - which specific piece to pick up is being
confirmed with them rather than assumed, since Wood Bow in particular
implies new architecture (projectiles) that CLAUDE.md's "No Silent
Architecture Changes" rule says shouldn't be started without a
decision.

Developer asked to close out Combat's two remaining Phase 7 checklist
items instead - Death and Loot/drop. Scope explicitly excludes Phase 6's
Gravestone/Respawn (dropping the player's inventory into a recoverable
gravestone per GDD_v0.1.md section 9) - that stays a separate future
item; this increment only makes reaching 0 HP do something coherent
instead of softlocking there forever.

`PlayerHealth` gained `IsAlive`, a `Died` event (fires once, guarded, the
instant CurrentHealth reaches 0), and `Revive()` (resets to full,
unconditional unlike Heal/TakeDamage which now both no-op once dead).
`PlayerCombat.IsAlive` now delegates to it instead of its own
`CurrentHealth > 0f` copy; `PlayerCombat.TakeDamage` also early-returns
once `_health.IsAlive` is false, so a hit landing after death can't spend
block/parry stamina or double-log.

Added `PlayerDeath` (Scripts/Gameplay/Player, new) - subscribes to
`PlayerHealth.Died`, disables `PlayerMotor`/`PlayerCombat`/
`PlayerInteractor`/(optionally) `PlayerBuilding` and frees the cursor via
the existing `SetMenuOpen` request-count mechanism (same pattern every
other modal panel uses), then raises its own `Died` event for UI.
Respawn (called from the new death screen's button) re-enables those
components, calls `PlayerHealth.Revive()`, and teleports the player back
to wherever they started this scene (position/rotation captured once in
`Awake` - no world/save system yet to pick a smarter point). Teleporting
needed a new `PlayerMotor.Teleport(position, rotation)` - briefly
disables the `CharacterController` around the transform set (it fights a
direct position assignment while enabled) and zeroes vertical velocity so
the player doesn't fall through/launch off the respawn point using
whatever gravity speed they had at death.

Added `DeathUIController` + `DeathPanel.uxml`/`.uss` (Scripts/UI) - a
centered "You Died" + Respawn overlay, same UIDocument-per-controller
pattern and dark-wood/gold palette as `PauseUIController`/`PausePanel`
(title tinted red to read as distinct from Pause). Respawn button calls
`PlayerDeath.Respawn()` directly.

`CombatDummy` gained a **Loot Drops** list (reusing `ResourceNodeDrop`
from `NAV.Gameplay.Items` as-is - "item + amount + chance" means the same
thing for a defeated combat target as a depleted resource node, no new
type needed) and a `SpawnLoot()` that rolls each entry independently and
scatters hits as physical `ItemPickup`s via the existing
`ItemPickup.SpawnInWorld` path, mirroring `ResourceNode.SpawnDrops`
exactly. Guarded by `_hasDroppedLoot` so it only fires once. Unlike a
resource node (or a future real creature), the dummy deliberately does
**not** despawn on death - it's reusable test infrastructure, not
content; once `IsAlive` is false it just permanently stops attacking and
stops accepting further damage, staying in the scene for repeat manual
testing. Placeholder loot content (2x Rock guaranteed + 1x Wood at 50%,
both already have `WorldPrefab` set) is Unity-side setup, not code.

`PlayerDebugHud`'s existing `Health:` line now appends `(DEAD)` when
`!PlayerHealth.IsAlive`, so death is visible for verification even before
the death-screen UI is wired up.

This closes every Phase 7 checklist item except **Damage feedback**
(still console log + HUD text only - no hit VFX/audio/reaction; final
audio/VFX is "Not Yet Decided", not addressed here, not requested this
time). NOT YET CONFIRMED - needs `PlayerDeath` added to Player, a
`DeathUI` UIDocument object created and wired, and `CombatDummy_Basic`'s
new Loot Drops list populated; see `UNITY_SETUP_NEXT_STEPS.md` Step 22.

CONFIRMED - developer completed Step 22.1-22.4 end-to-end (PlayerDeath
wired onto Player, DeathUI created/wired at Sort Order 10, CombatDummy's
Loot Drops populated with placeholder Rock/Wood, and the full playtest:
CombatDummy defeated -> loot scatters and is pickupable, dummy stops
attacking/taking damage but stays in scene; player reduced to 0 HP ->
input/camera/build freeze, cursor frees, You Died screen with Respawn
appears, `PlayerDebugHud`'s Health line shows `(DEAD)`; Respawn ->
screen closes, player returns to the scene's start position at full HP
and responds to input again). This closes Phase 7 (Combat) except the
already-deferred **Damage feedback** item (VFX/audio - "Not Yet
Decided").

Per the Core Rule's development order
(Foundation -> Player -> Interaction -> Items -> Inventory -> Gathering
-> Crafting -> Workbench -> Building -> Combat -> **Creatures** -> World
-> Save -> Polish -> Multiplayer), the next unstarted stage is
**Creatures and AI** (`DEVELOPMENT_ROADMAP_v0.1.md` Phase 8: creature
data, health, perception, target selection, patrol, chase, attack,
search, return, death, loot, one neutral animal, one hostile creature).
Same pause-and-wait-for-go pattern as before Building and Combat -
waiting for the developer's explicit go-ahead before starting it.

Developer said go. Creatures increment 1: implemented the whole Phase 8
checklist in one increment (creature data, health, perception, target
selection, patrol, chase, attack, search, return, death, loot, one
neutral + one hostile creature) - same reasoning as Combat increment 1:
these states are tightly coupled (Search/Return can't be verified
without Chase losing a target first, Flee needs TakeDamage already
wired) and every prior stage shipped its first pass as one coherent
increment rather than being split further.

Added `Assets/NAV/Scripts/Gameplay/AI/` (new folder - `ScriptableObjects
/Creatures` already existed as an empty skeleton from the original
ARCHITECTURE_v0.1.md folder plan): `CreatureDefinition` (ScriptableObject
- identity/IsHostile, stats, detection radius, attack numbers, patrol/
search/flee tuning, a `LootDrops` list reusing `ResourceNodeDrop` as-is,
same "item + amount + chance" type `CombatDummy`/`ResourceNode` already
use) and `Creature` (MonoBehaviour, implements `IDamageable` like
`PlayerCombat`/`CombatDummy` do). This is the project's first use of
`com.unity.ai.navigation` (`NavMeshAgent`/`NavMeshSurface`, already
present in Packages but never wired into a scene before).

`Creature` runs a small explicit state machine (`CreatureState`: Patrol,
Chase, Attack, Search, Return, Flee, Dead) on top of a `NavMeshAgent`.
Perception is a trigger `SphereCollider` sized from
`CreatureDefinition.DetectionRadius` - the same "physics event, not a
per-frame distance scan" idiom `Workbench`/`CombatDummy` already use,
not a new pattern. Hostile creatures (`IsHostile = true`) chase/attack
anything `IDamageable` that enters the trigger (GDD 17's `Idle/Patrol ->
Detect Player -> Chase -> Attack -> Search/Return`); neutral creatures
never act on the trigger for aggro purposes and only react to actually
being hit (GDD 17: "Neutral creatures may flee when threatened") by
entering Flee. `IDamageable.TakeDamage(float)` carries no attacker
reference (a pre-existing interface shape, unchanged by this increment -
extending it would be a cross-cutting change touching every existing
implementor, out of scope for one increment per CLAUDE.md's "No Silent
Architecture Changes" rule), so Flee's direction is read from whatever
is currently sitting in the creature's own detection trigger at the
moment it takes damage - correct for the only damage source that exists
today (melee, which requires standing within a few meters, well inside
any sane DetectionRadius) but would need a real attacker parameter if
ranged or creature-vs-creature damage is ever added.

On death, `Creature` disables its `NavMeshAgent`, scatters
`LootDrops` via the existing `ItemPickup.SpawnInWorld` path (same
scatter shape and independent-chance roll as `CombatDummy.SpawnLoot`/
`ResourceNode.SpawnDrops`), and - unlike `CombatDummy`, which is reusable
test infrastructure - despawns itself after a short delay, since a real
creature is content, not a fixture.

Deliberately out of scope for this increment (explicit Roadmap Phase 8
refinements, same style as every other stage's first pass): no
animations (no rig/Animator for creatures yet, same gap flagged for the
player's own attack/block/gather animations), no line-of-sight raycast
(detection is radius-only, so a creature can "sense" something through a
thin wall), no pack/group behavior, no creature persistence/respawn
(needs the Save system, Phase 11 - not started).

Placeholder content (final creature roster is explicitly "Not Yet
Decided", same status as every other content category):
`ScriptableObjects/Items/RawMeat.asset` (new item, Food category, stack
10, weight 1 - no WorldPrefab assigned yet, same "needs a Unity-side
prefab before it can drop" situation Flint/Resin had before their own
Step 0), `ScriptableObjects/Creatures/WildBoarCreature.asset` (neutral,
40 HP, flees when hit, 75% chance to drop 1x Raw Meat) and
`ScriptableObjects/Creatures/ForestWolfCreature.asset` (hostile, 35 HP,
10m detection, 12 damage, guaranteed 1x Raw Meat on death) - hand-
authored the same way Wood/Rock/StoneAxe/StoneSword/BasicWorkbench were.

NOT YET CONFIRMED - needs a NavMesh baked in SampleScene (first time
this project uses one), two test Capsule GameObjects
(`CreatureTest_WildBoar`/`CreatureTest_ForestWolf`, each with
`NavMeshAgent` + `Creature` + its Definition assigned), and a World
Prefab assigned to `RawMeat.asset` (duplicating an existing item prefab,
e.g. `Flint_item.prefab`) before loot can be picked up; see
`UNITY_SETUP_NEXT_STEPS.md` Step 23.

CONFIRMED - developer completed Step 23 end-to-end (NavMesh baked,
`CreatureTest_WildBoar`/`CreatureTest_ForestWolf` created and wired,
`RawMeat_item` World Prefab assigned) and verified the playtest: Wild
Boar patrols and flees when hit instead of fighting back, Forest Wolf
detects/chases/attacks on cooldown and returns to patrol after losing
the player, both drop Raw Meat on death and despawn. Creatures
increment 1 (Phase 8's full checklist) is now fully confirmed
end-to-end.

Per the Core Rule's development order
(... -> Combat -> Creatures -> **World** -> Save -> Polish ->
Multiplayer), the next unstarted stage is **World** (Phase 9: seed
system, deterministic random, terrain generation, biomes, resource/
vegetation distribution, spawn regions, points of interest) - by far
the largest and riskiest stage so far (see Known Risk #1: "Procedural
world generation can become too complex too early"), including the
runtime NavMesh-baking-per-chunk question already flagged during Step
23's discussion. Same pause-and-wait-for-go pattern as every prior
stage transition - waiting for the developer's explicit go-ahead, and
given the size/risk here, likely worth scoping down to a small first
slice (e.g. one seeded flat-ish biome with basic height variation)
rather than attempting the whole Phase 9 checklist at once the way
Combat/Creatures did.

Discussed scope with the developer before writing any code (per
CLAUDE.md's Development Protocol - Plan before Implement, and given
this is Known Risk #1). Agreed first-slice scope: **Unity Terrain**
(not a hand-rolled mesh - faster to a playable result, chunking for a
truly large/streamed world is explicitly future work), **one bounded,
non-streaming Terrain** (fixed size, no chunk system yet), **three
biomes** - Ocean, Plains, Forest (developer initially said "ocean
(river)" but on the complexity trade-off being spelled out - a real
carved river is its own algorithm, source-to-sea pathfinding through
the heightmap - chose ocean-only for this slice, river deferred).

Added `Assets/NAV/Scripts/World/` (new top-level area, matching
`ARCHITECTURE_v0.1.md`'s World layer, which was planned from the start
but unused until now): `BiomeDefinition` (`World/Biomes/` -
ScriptableObject: display name, a `TerrainLayer` reference for ground
texture, `IsWater`), `WorldGenerationSettings` (`World/Generation/` -
ScriptableObject holding every tunable: seed, terrain width/length/max
height/heightmap resolution, height-noise scale + a smaller detail-noise
layer blended in, sea level as a 0-1 fraction of max height, a second
independent moisture-noise channel + threshold that splits land into
Plains vs Forest, and references to the three `BiomeDefinition`
assets), and `WorldGenerator` (`World/Generation/` - MonoBehaviour,
runs the actual pipeline).

`WorldGenerator` follows `ARCHITECTURE_v0.1.md`'s documented pipeline
almost verbatim for this slice: `Seed -> Noise -> Heightmap -> Land/
Water -> Biome Assignment` (Terrain Features, Resources, Vegetation,
Creatures/Spawn Rules, and Points of Interest are the pipeline's later
stages - "Generation should be split into testable stages" - explicitly
not attempted here). On `Start()` (also exposed as a `[ContextMenu(
"Regenerate")]` for manual re-runs without entering Play), it seeds a
`System.Random` from `WorldGenerationSettings.Seed` - deliberately not
`UnityEngine.Random`, whose global state depends on unrelated call
order elsewhere in the game - and derives per-channel noise offsets
from it, so `Mathf.PerlinNoise` sampling is both seed-dependent and
reproducible (GDD 4: "The same seed must produce the same world when
generation settings are identical"). Per heightmap cell: height comes
from two blended Perlin layers (broad shape + a smaller weighted detail
pass), moisture from an independent third Perlin channel. Cells at or
below `SeaLevel` become Ocean; land cells split into Plains/Forest by
`ForestMoistureThreshold` - a hard per-cell choice, no blending between
biome edges yet. Heights feed `TerrainData.SetHeights`; the three
biomes' `TerrainLayer`s become `TerrainData.terrainLayers`, painted via
`SetAlphamaps` (each cell fully weighted onto its chosen layer). A
placeholder water plane (flat quad, no shader/waves) is repositioned/
rescaled to sea level and the terrain's footprint. Finally, an optional
`NavMeshSurface.BuildNavMesh()` runs once against the freshly generated
terrain - the same package/API Creatures increment 1 introduced for
Step 23, now driven at runtime instead of an Editor-time Bake button,
directly answering the runtime-NavMesh question raised during that
step's discussion.

Deliberately out of scope for this increment (explicit Roadmap Phase 9
items, deferred - same style as every stage's first pass): chunk/
streaming support for a truly large world (this generates exactly one
fixed-size Terrain - see Known Risk #1, still open), rivers (see the
scope discussion above), Birch grove/Dense forest as distinct biomes
(only one generic "Forest"), Resource/Vegetation distribution (no
trees/resource nodes placed procedurally - `SampleScene`'s hand-placed
`TestResourceNode_*` etc. are untouched and still the only way to test
Gathering), Spawn regions (no logic picks a guaranteed-dry player spawn
point - an unlucky seed can spawn the fixed scene position underwater;
flagged, not solved), Points of interest, and saving a generated world
(needs Phase 11).

Placeholder content: `ScriptableObjects/World/
DefaultWorldGenerationSettings.asset` (seed 12345, 500x500 terrain, 60m
max height) and `ScriptableObjects/Biomes/OceanBiome.asset`/
`PlainsBiome.asset`/`ForestBiome.asset` - all three ship with **no**
`TerrainLayer` assigned (final art/texture strategy is "Not Yet
Decided" and I can't author texture assets) - Unity-side setup
(`UNITY_SETUP_NEXT_STEPS.md` Step 24) has the developer create three
placeholder `TerrainLayer`s and assign them.

Testing this needed a new scene rather than reusing `SampleScene`
directly - `WorldGenerator` replaces the ground `SampleScene`'s flat
plane and every hand-placed test object (resource nodes, workbench,
combat dummy, building pieces, the two Creatures test objects) already
assumes; regenerating terrain under all of that would silently break
every previously-confirmed system's test setup. Setup instructions have
the developer duplicate `SampleScene` into a new `WorldGenTest` scene,
strip the old ground/test objects, and keep `Player`/HUD - `SampleScene`
itself stays untouched, exactly the same "duplicate, don't disturb"
precedent `MainMenu` already established.

NOT YET CONFIRMED - needs the new `WorldGenTest` scene, three
placeholder `TerrainLayer` assets, a `Terrain` + `WaterPlane` +
`NavMeshSurface`, and the `WorldGenerator` GameObject wired up; see
`UNITY_SETUP_NEXT_STEPS.md` Step 24.

Developer confirmed generation works (screenshot: seeded terrain,
Ocean/Plains/Forest textures all visible, HUD/creatures/existing
systems unaffected) - **CONFIRMED**, generation pipeline itself
closes out Creatures increment 1's earlier NavMesh-question and the
core of Phase 9's first slice. Developer feedback from the same
screenshot: the terrain read as "one giant mountain range" despite the
biome textures being correct - height and biome were being computed
from independent noise passes, so the same high-frequency detail noise
was added at full strength everywhere, including Plains.

Root-cause fix (`WorldGenerator`/`BiomeDefinition`, no scene changes
needed): height computation is now biome-first, not independent of it.
The existing low-frequency base noise alone decides Land/Water and
Plains/Forest (`GetBiome`, unchanged in spirit, now factored into its
own method reused by painting and resource spawning too); only once a
cell's biome is known does the high-frequency detail layer get added,
scaled by that biome's new `HeightVariation` (0-1). `BiomeDefinition`
gained this field - `OceanBiome`/`PlainsBiome` set to 0.15 (smooth
seabed / flat fields), `ForestBiome` to 0.65 (rolling hills) - so
Plains actually reads as flat ground instead of the same "everywhere is
a hill" texture Forest gets. `WorldGenerationSettings` defaults also
adjusted for a gentler overall silhouette: `MaxHeight` 60 -> 40,
`HeightNoiseScale` 180 -> 260 (smoother/larger landmass shapes),
`DetailNoiseWeight` 0.2 -> 0.35 (now a ceiling further multiplied by
each cell's biome `HeightVariation`, not the same effective strength as
before). `PaintBiomes` was refactored to read a shared `BiomeDefinition[,]`
map built once during the height pass (instead of re-deriving biome
from height/moisture arrays with its own separate threshold checks) -
one source of truth for "which biome is this cell", removing a latent
risk of painting and height shaping silently disagreeing near
thresholds.

Same conversation, developer asked for a second thing: per-biome
control over which resource nodes spawn where (explicitly "как с
ресурс нодов" - matching `ResourceNodeDefinition`'s existing
item+amount+chance pattern). Added `BiomeResourceSpawn` (`Scripts/World/
Biomes/BiomeDefinition.cs`, a small `[Serializable]` class - prefab +
`Density` 0-1 + `MinSpacing`) and a `ResourceSpawns` list on
`BiomeDefinition`. `WorldGenerator.SpawnResources` (new pass, runs
after `PaintBiomes`, before `PlaceWater`/`BakeNavMesh` so newly spawned
colliders are included in the NavMesh bake) walks a `MinSpacing`-sized
grid per resource-spawn entry, jitters one candidate point per cell
from a seed-derived `System.Random` (`Settings.Seed + 1` - independent
of the terrain-shape RNG, still fully deterministic), keeps only points
that land in the entry's own biome (via the same shared biome map
`PaintBiomes` uses), rolls `Density`, and instantiates on a hit -
positioned via `Terrain.SampleHeight` so nodes sit on the actual
generated surface. Regenerating destroys and re-spawns every tracked
instance first (`Application.isPlaying` ? `Destroy` : `DestroyImmediate`,
since Regenerate is also usable from the Editor context menu outside
Play), so repeated Regenerate calls don't accumulate duplicates.

Wired to content that already existed but was unused anywhere:
`Assets/NAV/Prefabs/World/SM_Rocks_03.prefab` and `Flint_Ore_Rock_01.prefab`
(both already-configured `ResourceNode` prefabs) on Plains, `UNS_Spruce_
WoodNode.prefab` (same) on Forest, denser there (Density 0.35 vs Plains'
0.12/0.06) to actually read as a forest. Ocean's list is empty. All
three are plain data on the `BiomeDefinition` assets - fully
adjustable by the developer without touching code, per the request.

Separately clarified (no code change - already exactly matches the
request): Creature loot was asked to be made configurable "like
resource nodes" - it already is, unchanged since Creatures increment 1.
`CreatureDefinition.LootDrops` is the same `ResourceNodeDrop`
(item + amount + chance) list `ResourceNodeDefinition.Drops` uses; it
just lives on the `WildBoarCreature.asset`/`ForestWolfCreature.asset`
ScriptableObject, not on the `Creature` component in the scene (which
only shows loot-scatter *physics* fields - radius/force/upward force -
not the loot list itself, which is likely why it read as missing).
Flagged this distinction to the developer rather than adding a
duplicate per-instance field, which would violate the project's
existing "shared data lives on the Definition asset" convention used
by every other content type (recipes, weapons, resource nodes, building
pieces).

NOT YET CONFIRMED (this addendum only - the base generation above is
already confirmed) - needs a Regenerate/Play retest for the flatter
Plains/hillier Forest terrain and the new resource scatter; no new
Unity Editor setup required, see `UNITY_SETUP_NEXT_STEPS.md` Step 24.6.

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
-   Developer confirmed: compiles clean, "[ItemStackSanityChecks] All
    checks passed." in Console, and the interaction range fix (camera vs.
    player origin, see below) also verified working.
-   Inventory increment: added `Inventory` (Scripts/Gameplay/Inventory) ---
    a fixed-size array of ItemStack slots with AddItem (fills matching
    stacks first, then empty slots, returns unfitted leftover),
    RemoveItem (returns amount actually removed), GetTotalQuantity, and a
    `Changed` event for future UI to observe (gameplay owns state, UI
    observes it, per ARCHITECTURE_v0.1.md). Added `PlayerInventory`
    MonoBehaviour wrapping an `Inventory` instance with a serialized
    capacity (default 20) --- not yet added to the Player GameObject in
    the scene (nothing needs it there yet; that's for the pickup/UI
    increments). Verified via InventorySanityChecks
    (Scripts/Editor), same editor-utility approach as ItemStackSanityChecks.
    NOT YET CONFIRMED by the developer this session --- check Console for
    "[InventorySanityChecks] All checks passed." before continuing.
-   Item pickup increment: added `ItemPickup` (Scripts/Gameplay/Items), an
    IInteractable world object holding an ItemDefinition + quantity; on
    Interact() it calls `interactor.GetComponent<PlayerInventory>()` and
    adds to it, shrinking/self-destroying as it's consumed, logging the
    result to Console. Reuses the existing interaction system unchanged.
    Added one placeholder content asset,
    ScriptableObjects/Items/Wood.asset (id "wood", stack size 50, weight
    1) --- explicitly a placeholder for testing only; the final resource
    list is still "Not Yet Decided." PlayerDebugHud now also shows
    "Inventory: X/Y slots used" when given a PlayerInventory reference.
    CONFIRMED --- developer added PlayerInventory to Player and a
    TestPickup_Wood sphere (ItemPickup + Wood.asset) in SampleScene and
    playtested the pickup flow successfully.
-   Incident: developer hit `error CS0246: PlayerInventory could not be
    found` in PlayerDebugHud.cs despite correct code and a correct
    `using` directive. Confirmed via Logs/Editor.log that
    `PlayerInventory.cs` never appeared in the compiler's input file list
    at all (Inventory.cs and ItemPickup.cs, created around the same time,
    did) --- a one-off Unity AssetDatabase scan miss when several new
    files landed at once from outside the Editor, not a code or
    namespace-collision problem (separately verified with a standalone
    dotnet test that a class and its containing namespace sharing a name,
    e.g. NAV.Gameplay.Inventory.Inventory, compiles and resolves fine).
    Fix is Editor-side only: Ctrl+R (Assets → Refresh) to force a
    rescan, or Reimport the file directly. No code changed.
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
-   Inventory UI increment (UI Toolkit, developer's choice over uGUI):
    added `InventoryUIController` (Scripts/UI) plus `InventoryPanel.uxml`/
    `.uss` (same folder) --- a fixed grid of slot elements bound to
    `PlayerInventory`/`Inventory.Changed`. Added a `ToggleInventory`
    input action (Player map, `I` key / gamepad Select) to
    `InputSystem_Actions.inputactions` and
    `PlayerInputHandler.ToggleInventoryPerformed`. Toggling the panel
    also unlocks/relocks the cursor. Movement is intentionally NOT
    blocked while the panel is open (documented limitation, not a bug).
    NOT YET CONFIRMED --- needs Panel Settings asset + UIDocument
    GameObject creation and wiring in the Editor before it can be
    playtested; see the developer instructions.
-   Fix: `InventoryUIController` referenced the bare `Cursor` type, which
    is ambiguous between `UnityEngine.Cursor` and
    `UnityEngine.UIElements.Cursor` once the file imports
    `UnityEngine.UIElements` --- qualified as `UnityEngine.Cursor`.
-   Developer confirmed the Inventory UI increment (Panel Settings +
    InventoryUI GameObject created and wired, toggle/pickup/close
    playtest verified).
-   Gathering increment 1: added `ResourceNodeDefinition`
    (ScriptableObject: drop `ItemDefinition`, amount per hit, hits to
    deplete) and `ResourceNode` (Scripts/Gameplay/Items, `IInteractable`,
    same folder/pattern as `ItemPickup` since a resource node is
    conceptually still item-source data --- no new folder needed). Each
    Interact() is one gather "hit": adds `AmountPerHit` of the drop item
    to the interactor's `PlayerInventory`, decrements a remaining-hits
    counter, shrinks the node slightly as placeholder feedback, and
    destroys the node on depletion. No tool/axe/pickaxe gating (no
    Equipment system yet), no respawn/persistence (needs Save system),
    no real VFX/audio --- all explicitly deferred, not overlooked. NOT
    YET CONFIRMED --- needs a `ResourceNodeDefinition` asset and a test
    node placed in SampleScene; see the developer instructions.
-   Fix (playtest feedback): the camera kept turning while the
    inventory panel was open, since `ThirdPersonCameraController` read
    mouse-look input every frame regardless of any UI. Added
    `PlayerInputHandler.InputSuspended`/`SetInputSuspended` --- while
    true, Move/Look/Sprint read as zero/false and Jump/Interact stop
    firing (ToggleInventory itself is unaffected, so the panel can
    still be closed). `InventoryUIController.SetVisible` now calls this
    alongside the cursor lock toggle. No Editor wiring changes needed
    (no new serialized fields); code-only fix.
-   Refinement (playtest feedback): freezing the camera while the
    inventory panel is open shouldn't also freeze the character.
    Renamed `PlayerInputHandler.InputSuspended`/`SetInputSuspended` to
    `MenuOpen`/`SetMenuOpen` and narrowed its effect: Move always reads
    normally now; `MenuOpen` only zeroes Look and suppresses Sprint/
    Jump/Interact. Developer confirmed: camera stays still, character
    still walks, with the panel open.
-   Developer confirmed Gathering increment 1: created
    `TreeWoodNode.asset` (Drop Item: Wood, Amount Per Hit: 1, Hits To
    Deplete: 3) and `TestResourceNode_Tree` in SampleScene, verified
    gathering deposits Wood into the inventory and the node depletes
    after 3 hits.
-   Second resource node content (developer request, not a new
    system): added an ItemDefinition + ResourceNodeDefinition pair,
    hand-authored directly the same way Wood.asset originally was.
-   Developer built and confirmed the second resource themselves,
    renaming the assets along the way: item
    `ScriptableObjects/Items/Rock.asset` (id `Rock`, stack size 20,
    weight 5) dropped by node definition
    `Scripts/Gameplay/Items/StoneRockNode.asset` (display name
    "Stone", Amount Per Hit 1, Hits To Deplete 10), placed in
    SampleScene as `TestResourceNode_Stone`. Gathering increment 1 is
    now fully confirmed end-to-end with two working resources.
    Gathering is demonstrably functional per the Core Rule; moved on
    to the next development-order stage, Crafting.
-   Crafting increment 1: added `RecipeDefinition` + `RecipeIngredient`
    (Scripts/Gameplay/Crafting --- ingredient list, output item/amount,
    `CanCraft`/`TryCraft` against an `Inventory`) and `PlayerCrafting`
    (same folder, mirrors PlayerInventory: fixed serialized list of
    known recipes, no unlock system yet, no workbench gating yet ---
    both explicitly deferred to their own later roadmap items). Added
    `CraftingUIController` + `CraftingPanel.uxml`/`.uss` (Scripts/UI),
    a second UI Toolkit panel following the InventoryUI pattern,
    toggled by a new `ToggleCrafting` action (`K` / gamepad West) added
    to `InputSystem_Actions.inputactions`; lists known recipes with
    live "have/need" ingredient costs and a Craft button, disabled
    when unaffordable. Since both panels are full-screen modal
    overlays, opening one now closes the other (`Hide()` added to both
    controllers, wired via a new optional cross-reference on each).
    Refactored `PlayerInputHandler.MenuOpen` from a bool to an internal
    request count so two panels closing each other can't clobber the
    shared "is a modal open" state --- `SetMenuOpen(bool)`'s signature
    and behavior are unchanged for existing callers, this is
    internal-only. Added placeholder content (final recipes are "Not
    Yet Decided"): `ScriptableObjects/Items/StoneAxe.asset` (Tool) and
    `ScriptableObjects/Recipes/StoneAxeRecipe.asset` (3x Wood + 2x Rock
    -> 1x Stone Axe), hand-authored the same way Wood/Rock were. Added
    `Scripts/Editor/CraftingSanityChecks.cs` (same editor-utility
    pattern as ItemStackSanityChecks/InventorySanityChecks) covering
    CanCraft/TryCraft. NOT YET CONFIRMED --- needs `PlayerCrafting`
    added to Player, a Panel Settings + UIDocument for the crafting
    panel, and the two panel controllers cross-wired; see
    UNITY_SETUP_NEXT_STEPS.md Step 11.
-   Fix: `RecipeDefinition.CanCraft`/`TryCraft` failed to compile --
    `error CS0118: 'Inventory' is a namespace but is used like a type`.
    Same root cause as the incident logged for the original
    Inventory/PlayerInventory work (a class and its containing
    namespace sharing the name `Inventory`), but triggered differently
    this time: `RecipeDefinition.cs` lives in `NAV.Gameplay.Crafting`,
    a *sibling* of `NAV.Gameplay.Inventory` under the same parent
    (`NAV.Gameplay`), and C# resolves a bare `Inventory` type there to
    that sibling namespace before it considers the class imported via
    `using`. `ResourceNode`/`ItemPickup` never hit this because they
    only ever access `.Inventory` as a member
    (`playerInventory.Inventory`), never declare a parameter of bare
    type `Inventory`. Fixed by fully qualifying the two parameter
    types as `NAV.Gameplay.Inventory.Inventory` instead of importing
    the namespace; no behavior change.
-   Developer confirmed Crafting increment 1 end-to-end (Step 11
    playtest: craft Stone Axe, panels closing each other, live
    affordability).
-   Inventory/Crafting rework (developer's 5-part request): Inventory
    and Crafting panels are no longer full-screen mutually-exclusive
    modals - both are now small panels anchored left (Inventory) and
    right (Crafting), able to stay open together without covering the
    player. Added LMB drag-and-drop within the inventory grid
    (`Inventory.MoveSlot`) and drag-out-to-drop-in-world
    (`Inventory.RemoveFromSlot` + `PlayerInventory.DropItem`, spawning
    a new `ItemDefinition.WorldPrefab`). Added a real weight system
    (`Inventory.MaxWeight`/`TotalWeight`/`IsOverloaded`, weight-aware
    `AddItem`, shown in the panel) that slows movement
    (`PlayerMovementStats.OverloadSpeedMultiplier`) and passively
    drains stamina (`PlayerStaminaStats.OverloadDrainPerSecond`,
    `PlayerStamina.TickSprint` gained an `isOverloaded` parameter) once
    carried weight hits the cap. `ItemPickup` is now
    `[RequireComponent(typeof(Rigidbody))]` so every pickup (hand-placed
    or dropped) has real physics instead of floating in place. Fix
    required by panels coexisting: cursor lock/visibility moved out of
    each panel controller and into `PlayerInputHandler.SetMenuOpen`
    itself (driven by its open-panel request count transitioning to/from
    zero), since either panel closing independently used to re-lock the
    cursor even while the other was still open. Extended
    `InventorySanityChecks.cs` with coverage for `MoveSlot`,
    `RemoveFromSlot`, and weight-limited `AddItem`. NOT YET CONFIRMED
    --- needs `PlayerMotor.Inventory` wired to Player, a generic
    `ItemPickup_Generic` prefab created and assigned as `WorldPrefab` on
    Wood/Rock/StoneAxe, and the full playtest; see
    UNITY_SETUP_NEXT_STEPS.md Step 12.
-   Developer confirmed Step 12 end-to-end (including the screenshot-
    driven panel reposition and the movement-gated/sprint-blocked
    overload refinement). Inventory/Crafting UX is closed; moved on to
    the next development-order stage, Workbench.
-   Workbench increment 1: added `WorkbenchDefinition` + `Workbench`
    (Scripts/Gameplay/Crafting) - a trigger-based (not raycast)
    proximity system, since crafting near a bench shouldn't require
    looking at it. `RecipeDefinition` gained `RequiredWorkbenchTier`
    (default 0, backward compatible); `CanCraft`/`TryCraft` both take
    a new `availableWorkbenchTier` argument. `PlayerCrafting` tracks
    nearby Workbenches (registered via their own trigger events, no
    per-frame scan) as `NearbyWorkbenchTier` +
    `NearbyWorkbenchChanged`. `CraftingUIController` shows a
    `Requires Workbench (Tier N)` line per gated recipe, colored by
    whether it's currently satisfied. `StoneAxeRecipe.asset` now
    requires Tier 1 (via the new `ScriptableObjects/Workbenches/
    BasicWorkbench.asset`, Tier 1/Range 4) so the existing recipe
    demonstrates the gating directly. Extended
    `CraftingSanityChecks.cs` with tier-gating coverage. NOT YET
    CONFIRMED --- needs a `TestWorkbench_Basic` object in SampleScene;
    see UNITY_SETUP_NEXT_STEPS.md Step 13.
-   Developer confirmed Step 13 (Workbench) and the Valheim-style
    two-column crafting panel rework. Workbench increment 1 fully
    closed. Developer asked to pause before starting Building.

### v0.1 --- 2026-08-26

-   Developer said go on Building (Phase 5). Building increment 1:
    added `BuildingPieceDefinition` + `BuildingPiece` + `PlayerBuilding`
    (Scripts/Gameplay/Building) - ghost-preview placement (clones the
    selected piece's own prefab, disables its colliders, tints it via a
    shared valid/invalid material), rotation, cycling between known
    pieces, cost-gated building (reuses `RecipeIngredient` from
    Crafting), and demolish (`BuildingPiece` is `IInteractable`, E to
    remove, no refund yet). Reused three previously-unused input
    actions from the project template (`Attack`/LMB for place,
    `Next`/`Previous` for cycling) and added two new ones
    (`ToggleBuild` - `B`/gamepad right shoulder, `RotatePiece` -
    `R`/gamepad left shoulder) to `InputSystem_Actions.inputactions`;
    `PlayerInputHandler` exposes all five as events, with
    Attack/RotatePiece/CycleNext/CyclePrevious suppressed while
    `MenuOpen` (same treatment as Jump/Interact) and ToggleBuild not
    suppressed (same treatment as ToggleInventory/ToggleCrafting).
    Snapping, Save building state, and Basic structural validation are
    explicit Roadmap Phase 5 items left out of this increment -
    "Placement validation" here means only "the aim ray hit something
    within range." No dedicated Building UI panel; piece
    selection/cost/validity surfaces through a new `PlayerDebugHud`
    `Build: ...` line, same as Gathering increment 1 shipping without
    its own UI. NOT YET CONFIRMED --- needs two placeholder
    `BuildingPieceDefinition` assets + Cube-based prefabs, two ghost
    materials, and `PlayerBuilding` wired onto Player; see
    UNITY_SETUP_NEXT_STEPS.md Step 14.

Developer confirmed Step 14 works, but reported two problems from
playtesting: (1) placed pieces sink halfway into the ground - the
ghost/real piece was positioned with the raycast hit point as its
pivot, but a Cube's pivot is its *center*, so half the piece ends up
below the surface it's standing on; separately, `_maxPlacementDistance`
(8m) felt too short. (2) Explicit request for Valheim-style connection
between pieces (a wall attaching to a foundation's edge, pillars too).

Building increment 2 (fixes + basic snapping):
- Ground-anchor fix: `PlayerBuilding` now computes each selected
  piece's half-height once per ghost spawn (`ComputeHalfHeight`, from
  the union of its colliders' bounds, read right after `Instantiate`
  while the ghost still sits at the identity transform) and offsets
  the placement position upward by that amount, so a piece's *bottom*
  sits on the raycast hit point instead of its center. Assumes pieces
  only ever yaw (RotatePiece is Y-axis only), which holds for every
  piece so far.
- `_maxPlacementDistance` default raised 8m -> 15m, matching
  `PlayerInteractor._maxAimDistance`'s existing convention for "how far
  the player can reach by looking." NOTE: this is a serialized field -
  the developer's Player GameObject already has `8` baked in from Step
  14 and needs updating by hand in the Inspector; a code default change
  alone doesn't touch an already-serialized value.
- Basic snapping: added `BuildingSnapPoint` (Scripts/Gameplay/Building)
  - a plain marker component (with an editor-only gizmo) placed by hand
  as child Transforms on a piece's prefab at the points that should be
  able to connect (e.g. a wall's two bottom corners, a foundation's
  four top-edge midpoints). No "socket type"/compatibility rules - any
  snap point can pull any other snap point into alignment, matching the
  smallest-useful-version-first approach used everywhere else in this
  project. `BuildingPiece` now caches its own `SnapPoints` (children,
  read once in Awake) and maintains a static `AllPieces` registry
  (added in `Configure`/`OnEnable` when `Definition` is set, removed in
  `OnDisable`) so PlayerBuilding doesn't need a per-frame
  `FindObjectsByType` scan - only Configure()d/placed pieces register,
  so ghost previews (which are never Configure()d) never pollute it.
  `PlayerBuilding.TryApplySnap` runs every frame the ghost is active:
  for every placed piece within `_snapSearchRadius` (6m, a cheap
  broad-phase distance cull before touching any actual snap points), it
  finds the closest (ghost snap point, placed snap point) pair; if that
  distance is under `_snapRadius` (0.75m) the whole ghost is translated
  so the two points coincide exactly. Position-only - rotation is NOT
  auto-aligned to the target socket; the player still rotates manually
  with RotatePiece and only the position gets pulled into place once
  close enough. Exposed as `PlayerBuilding.IsSnapped`, shown in
  `PlayerDebugHud`'s `Build: ...` line (`snapped: True/False`) for
  verification.
- Auto-orienting rotation to match a socket's facing, and any real
  socket-type/compatibility system, are both explicitly deferred
  refinements on top of this basic version, not overlooked.

NOT YET CONFIRMED --- needs the Player Inspector's Max Placement
Distance updated, and BuildingSnapPoint children added to the two
existing test prefabs (WoodWall/WoodFoundation) at specific local
positions; see UNITY_SETUP_NEXT_STEPS.md Step 15.

Incident: developer playtested Step 15 with the ground-anchor fix
already in place and the wall was still sinking into the ground -
identical symptom to the original bug. Root cause: `_pieceHalfHeight`
was computed from `Collider.bounds` in `SpawnGhost`, but that
computation ran *after* the loop that disables the ghost's colliders
(`col.enabled = false`) a few lines above it - a disabled Collider's
`.bounds` collapses to zero in Unity, so the "lift the piece up by its
half-height" offset silently became a no-op and the ghost's pivot
landed back on the raycast hit point exactly as before the fix, with
no compile error or warning to flag it. Fixed by moving the bounds
computation to immediately after `Instantiate` (before anything touches
the colliders) and switching it from `Collider.bounds` to
`Renderer.bounds` - the visible mesh, not the (soon-to-be-disabled)
collider, is what actually needs to sit on the surface. No behavior
change to snapping or any other part of increment 2.

Developer confirmed snapping pulls position correctly, but reported the
rotation doesn't lock to match the piece it's snapping to - two
foundations would touch corner-to-corner but sit at an arbitrary angle
instead of parallel. Expected: `TryApplySnap` was position-only by
design (see the increment 2 entry below - rotation was explicitly left
manual). Extended it: on snap, the ghost's rotation is now set to the
*target snap point's own world rotation* (parent piece rotation
composed with that point's own local rotation), not just translated -
position is then translated using the snap points' post-rotation world
positions (rotating first, since it moves every child snap point).
Using the target *point's* rotation rather than the target *piece's*
rotation matters: it lets a single foundation orient an attaching wall
correctly regardless of which of its four edges (running along local X
vs local Z) the wall is snapping to, as long as each snap point's own
local rotation is authored to match its edge's direction. This needs
the developer to add a Rotation to the two side-facing foundation snap
points from Step 15 (`SnapPoint_East`/`SnapPoint_West`, both `(0, 90,
0)`) - the north/south ones and both wall points stay at the default
`(0, 0, 0)`; see UNITY_SETUP_NEXT_STEPS.md Step 16.

Developer confirmed Step 16: pieces now snap into position AND
rotation correctly (parallel foundations, walls lying flush along
whichever edge). Building increment 2 works end-to-end.

Per the developer's explicit request, Building is deliberately **not**
being marked "done"/closed the way Workbench was - see the new
"Technical Debt" section above for the full list of known gaps
(snapping has no socket-type system and is entirely hand-authored, no
structural validation, no save/load, no refund on demolish, no
dedicated UI). Treat Building as functional-but-basic; revisit that
list before/while doing further work on it. Not starting Combat (the
next Core Rule stage) automatically - wait for the developer to say go,
same as the pause before Building itself started.
-   Building increment 2: fixed pieces sinking into the ground
    (position now anchors the piece's bottom, not its center, to the
    raycast hit point) and raised placement reach 8m -> 15m. Added
    basic Valheim-style snapping: `BuildingSnapPoint` marker children +
    a `BuildingPiece.AllPieces` registry + `PlayerBuilding.TryApplySnap`
    pull the ghost's position onto the nearest matching snap point
    within radius. Position-only, no socket types, rotation still
    manual - explicitly a first version. NOT YET CONFIRMED - see
    UNITY_SETUP_NEXT_STEPS.md Step 15.
-   Fix: the ground-anchor fix from Building increment 2 didn't
    actually work - `_pieceHalfHeight` was read from `Collider.bounds`
    after the ghost's colliders were already disabled, and a disabled
    Collider's `.bounds` collapses to zero, so pieces kept sinking
    exactly as before. Moved the bounds read to immediately after
    `Instantiate` and switched to `Renderer.bounds`.
-   Building: `TryApplySnap` now also rotates the ghost to match the
    target snap point's own world rotation (not just translating
    position), so snapped pieces end up correctly oriented - e.g. two
    foundations parallel instead of touching at an arbitrary angle.
    Needs a Rotation added to the two side-facing foundation snap
    points (`SnapPoint_East`/`SnapPoint_West`, both `(0, 90, 0)`); see
    UNITY_SETUP_NEXT_STEPS.md Step 16.
-   Developer confirmed Step 16 - snapping now aligns both position
    and rotation correctly. Building increment 2 works end-to-end, but
    at the developer's explicit request it is *not* being marked
    "done" the way Workbench was - added a new "Technical Debt"
    section listing Building's known gaps (snapping has no
    socket-type system, no structural validation, no save/load, no
    demolish refund, no dedicated UI) to revisit before/while doing
    further Building work. Not auto-starting Combat (the next Core
    Rule stage) - waiting for the developer to say go.
-   Player HUD (developer request, UI Toolkit): added minimal
    `PlayerHealthStats`/`PlayerHealth` (current/max HP + TakeDamage/
    Heal only - explicitly not the Phase 6 Health system, see
    "Technical Debt") and `PlayerStamina.IsDraining`. Added
    `PlayerHudController` + `PlayerHud.uxml`/`.uss` (Scripts/UI):
    always-on (not toggled), 24px-from-edge anchored HP bar (bottom-
    left, vertical), stamina bar (bottom-center, horizontal, visible
    only while `IsDraining`), and a 7-slot hotbar (top-left,
    horizontal, read-only mirror of `PlayerInventory.Inventory`'s
    first 7 slots). NOT YET CONFIRMED - see UNITY_SETUP_NEXT_STEPS.md
    Step 17.
-   Developer confirmed Step 17. Refinement: stamina bar now stays
    visible until fully restored (was: only while `IsDraining`) and
    fades via an animated `opacity` USS transition instead of an
    instant `display` toggle.
-   Refinement: hotbar changed from 7 slots to 6, matching
    `InventoryPanel`'s per-row column count (its 400px grid max-width
    wraps at 6 slots of 56px+8px margin each) so the hotbar's width
    lines up with the inventory grid's, at the developer's request.
    `.hotbar-slot` margin changed from right-only to all sides to match
    `.inventory-slot` exactly.
-   Main menu + loading screen (developer request): added
    `Scripts/Core/SceneLoader.cs` (first Core-layer script - async
    scene load by name with progress reporting, minimum display
    duration so a fast load doesn't flash), `MainMenuUIController` +
    `MainMenuPanel.uxml`/`.uss` (Play/Quit, no Continue/Settings - no
    save/settings systems exist yet), and `LoadingScreenUIController` +
    `LoadingScreenPanel.uxml`/`.uss` (progress bar), all Scripts/UI,
    same dark-wood/gold palette and UIDocument-per-controller pattern
    as every other panel. Introduces the project's first second scene
    (`MainMenu`, alongside `SampleScene`, both in `Assets/Scenes/`) -
    Build Settings order `MainMenu` (0) then `SampleScene` (1).
    `SampleScene` itself unchanged; still directly playable for
    gameplay-system testing. NOT YET CONFIRMED - see
    UNITY_SETUP_NEXT_STEPS.md Step 18.
-   ESC pause (developer request): new `Pause` input action (Escape/
    gamepad Start) + `PlayerInputHandler.PausePerformed`. Added
    `PauseUIController` + `PausePanel.uxml`/`.uss` (Resume / Leave to
    Main Menu), lives in `SampleScene` only. Sets `Time.timeScale = 0`
    while open (real gameplay halt, not just input suppression) and
    reuses `SetMenuOpen`'s request-counted cursor/camera-freeze.
    Leave to Main Menu resets `timeScale` and loads `MainMenu` directly
    (no loading-screen hop for this direction). Escape does not force-
    close Inventory/Crafting first - pause just draws on top (needs
    higher UIDocument Sort Order). NOT YET CONFIRMED - see
    UNITY_SETUP_NEXT_STEPS.md Step 19.
-   Refinement: loading screen progress bar read as static (Unity's
    real `AsyncOperation.progress` jumps straight to 1 for today's
    near-empty `SampleScene`). Fill now eases toward the target instead
    of snapping, a shimmer sweeps the track continuously, and the
    "Loading" label cycles an animated ellipsis - all independent of
    real progress, purely so the screen reads as active. Code/UXML/USS
    only, no new Editor wiring.
-   Developer confirmed Steps 18 and 19, and the loading-screen
    animation refinement, all end-to-end. Main menu, loading screen,
    and ESC pause are done. Per the developer's request, pausing here -
    next unstarted Core Rule stage (**Combat**, Phase 7) waits for an
    explicit go-ahead.

### v0.1 --- 2026-08-31

-   Developer said go on Combat (Phase 7). Combat increment 1: added
    `IDamageable` + `WeaponDefinition` + `PlayerCombat` + `CombatDummy`
    (Scripts/Gameplay/Combat) - the full Phase 7 checklist (damage,
    hit detection, melee attacks, weapon definitions, blocking,
    parrying, stamina interaction) in one increment, since the pieces
    are too coupled to verify separately. `PlayerCombat` implements
    `IDamageable` itself (not `PlayerHealth`) so block/parry mitigation
    has one place to live before a hit reaches `PlayerHealth.TakeDamage`.
    Attack is a camera-forward raycast (same idiom as
    `PlayerInteractor`/`PlayerBuilding`), gated by weapon cooldown/range
    and stamina; Attack no-ops while `PlayerBuilding.IsBuildModeActive`
    so LMB stays exclusive to one tool/mode at a time. Blocking is a new
    held `Block` action (RMB/gamepad left trigger,
    `PlayerInputHandler.BlockHeld`); a hit within the weapon's
    `ParryWindowSeconds` of Block starting is a full-negation parry,
    otherwise a held block reduces damage and costs stamina.
    `PlayerStamina` gained `Spend(amount)` for one-time costs, no change
    to `TickSprint`. `CombatDummy` is a `DebugInteractable`-style
    placeholder target with its own HP that periodically counter-attacks
    anything `IDamageable` in its trigger radius - explicitly not a
    Creature/AI system (Phase 8 is still separate, later work). Added
    `ScriptableObjects/Weapons/StoneSword.asset` (placeholder weapon
    content, hand-authored the same way as Wood/Rock/StoneAxe/
    BasicWorkbench) and a `PlayerDebugHud` combat line. NOT YET
    CONFIRMED - needs `PlayerCombat` added to Player and a
    `CombatDummy_Basic` test object in SampleScene; see
    `UNITY_SETUP_NEXT_STEPS.md` Step 20.
-   Fix: `PlayerCombat`'s attack raycast measured range as raw ray
    length from the camera, the same bug already fixed once for
    `PlayerInteractor` - in third person most of a short range was
    consumed just reaching the player from the camera. Fixed the same
    way (range now checked as distance from the player's position to
    the hit point, ray itself casts out to a new `_maxAimDistance`).
    Also added Debug.Log lines for the miss/no-IDamageable cases,
    previously silent.
-   Content design: added `ITEMS_AND_CRAFTING_v0.1.md` - a draft
    6-tier item/resource/crafting-station plan (Wood/Stone -> Copper/
    Bronze -> Iron -> Silver -> Bulat -> Skymetal/"Titanium"), each
    tier gated behind a matching Workbench tier, Hammer as the main
    building-tool progression, plus an explicit list of what needs a
    separate architecture decision before implementation.
-   Tier 1 content increment: added `Flint`/`Resin` (resources) with
    `FlintDeposit` (gathering node), and `WoodHammer`/`StonePickaxe`
    (tools) with `WoodHammerRecipe` (no workbench required)/
    `StonePickaxeRecipe` (Workbench Tier 1, mirrors `StoneAxeRecipe`) -
    pure content, no code changes. Setup steps in a new separate file
    per the developer's request: `UNITY_SETUP_TIER1_ITEMS.md`. NOT YET
    CONFIRMED.
-   Multi-drop resource nodes: `ResourceNodeDefinition` now holds a
    list of `ResourceNodeDrop` (item + amount + independent chance)
    instead of one fixed item, so a single node/hit can yield several
    different items - requested so Resin didn't need its own dedicated
    node when it's conceptually part of the tree. `ResourceNode.
    Interact()` rolls each drop independently per hit.
    `StoneRockNode`/`FlintDeposit` migrated 1:1 (single 100%-chance
    drop, unchanged behavior); `TreeWoodNode` gained Resin as a 25%-
    chance second drop alongside Wood. Deleted the now-unnecessary
    standalone `ResinNode.asset`. NOT YET CONFIRMED.
-   Depletion-drop rework: gathering no longer adds items straight to
    inventory per hit. Interim hits only give shrink feedback; the hit
    that fully depletes a node destroys it and scatters its
    `Drops` into the world as physical `ItemPickup` objects (small
    random scatter + physics impulse) the player must walk over and
    pick up individually - matches "a felled tree drops its yield on
    the ground" instead of auto-filling the inventory. Extracted the
    spawn logic into a shared `ItemPickup.SpawnInWorld(...)`, reused by
    both `ResourceNode` and the pre-existing `PlayerInventory.DropItem`
    (unchanged behavior there). `ResourceNodeDrop.AmountPerHit` renamed
    to `Amount` (now a total-on-depletion quantity, not per-hit).
    Requires `Flint`/`Resin` to have a `WorldPrefab` assigned before
    gathering works at all now (previously only dropping needed it) -
    `UNITY_SETUP_TIER1_ITEMS.md` gained a required "Шаг 0" to create one
    and assign it. NOT YET CONFIRMED.
-   Character animation: developer imported a Humanoid character model
    + animation pack (`Assets/Kevin Iglesias/`). Confirmed
    `PlayerMotor` already locks body yaw to camera yaw (no code change
    needed for "always faces camera"). Added `PlayerAnimator`
    (Scripts/Presentation/Animation) feeding a to-be-built Animator
    Controller's MoveX/MoveY/IsSprinting/Grounded from existing
    movement state, damped for smooth blending. Animator Controller
    itself (2D Freeform Directional Locomotion + Sprint blend trees,
    Jump state) is Editor-graph work, documented step-by-step in a new
    separate file `UNITY_SETUP_ANIMATION.md` rather than hand-authored.
    NOT YET CONFIRMED.

### v0.1 --- 2026-09-01

-   Investigated attack/block/gather/build animations (developer's next
    request after locomotion). Blocked: the imported animation pack has
    no clips for any of these, only locomotion + conversation + two
    static hand poses - flagged to the developer, no Animator/code
    changes made, waiting on how to proceed (source clips vs.
    placeholder-motion graph now).
-   Aim crosshair (developer's redirect - more immediately useful than
    the animation work above): added a center-screen ring+dot reticle
    to the existing always-on `PlayerHud`, color-coded by aim state
    (build validity > interactable > attackable target > neutral),
    hidden while a modal panel has cursor focus. New
    `PlayerCombat.HasTargetInSight` (non-mutating aim/range query,
    mirrors the real attack raycast) is the only new gameplay logic;
    everything else reuses `PlayerInteractor`/`PlayerBuilding` state
    that already existed. `PlayerHudController` gained required
    InputHandler/Interactor/Combat fields and an optional Building
    field. NOT YET CONFIRMED - see the new addendum to
    `UNITY_SETUP_NEXT_STEPS.md` Step 17.
-   Developer confirmed Tier 1 content end-to-end (Flint/Resin
    gathering incl. the WorldPrefab placeholder fix, WoodHammer/
    StonePickaxe recipes, and the crosshair's new PlayerHudController
    fields). Tier 1 (per `ITEMS_AND_CRAFTING_v0.1.md`) is closed except
    for its explicitly-deferred remainder (Wood Club, Wood Bow + Stone
    Arrow, Hide Armor, Door/Roof/Palisade) - picking up items/crafting
    work again, direction to be confirmed with the developer.
-   Building content: added `WoodDoor`/`WoodRoof`/`WoodPalisade`
    (pure content, no code change - existing BuildingPiece/
    BuildingSnapPoint classes are already generic). Door is a same-size
    non-opening stand-in for `WoodWall` (opening is a future interaction
    mechanic, not content); Roof mirrors Foundation's shape with its
    snap points on the underside so it can attach on top of things;
    Palisade is a new tall/thin standalone shape for perimeter chains.
    `WoodWall` gained two more snap points (top corners) so Roof can
    attach directly to a wall, not just a foundation. NOT YET CONFIRMED
    - see `UNITY_SETUP_NEXT_STEPS.md` Step 21.

### v0.1 --- 2026-09-04

-   Closed out the remaining Building content from the 2026-09-01 session:
    `WoodDoorPiece` (Scripts/Gameplay/Building content, no code change --
    `BuildingPieceDefinition`/`BuildingPiece`/`BuildingSnapPoint` are
    already fully generic). Found while resuming this work that the
    2026-09-01 session's own plan (`UNITY_SETUP_NEXT_STEPS.md` Step 21)
    had been only partially carried out: `WoodRoofPiece`/`WoodPalisadePiece`
    existed and were already in `PlayerBuilding.Known Pieces`, but
    `WoodDoorPiece`/`WoodDoor` didn't exist at all, and `WoodWall` was
    still missing the `SnapPoint_TopLeft`/`SnapPoint_TopRight` points
    Step 21 called for (needed for Roof to attach to a Wall's top, not
    just a Foundation) -- both gaps are now fixed.
-   `WoodDoor` (`Assets/NAV/Prefabs/Build/WoodDoor.prefab`): a direct
    duplicate of `WoodWall` (same `(2, 2, 0.2)` scale, same BoxCollider,
    same three bottom snap points -- `SnapPoint_Bottom`/`BottomLeft`/
    `BottomRight`) so it occupies exactly a wall's opening and snaps to
    the same Foundation/Wall points. Per the existing design note (see
    `UNITY_SETUP_NEXT_STEPS.md` Step 21.1): this is **not** an openable
    door, just a same-size wall-slot filler -- real open/close is a
    separate interaction-mechanic decision, not started.
    `WoodDoorPiece.asset` (Display Name "Wood Door", Cost: Wood x4,
    Prefab: WoodDoor) added to `Assets/NAV/ScriptableObjects/Building/`.
-   `WoodWall.prefab` gained the two missing top snap points
    (`SnapPoint_TopLeft` at `(-0.5, 0.5, 0)`, `SnapPoint_TopRight` at
    `(0.5, 0.5, 0)`), matching the local positions Step 21 specified --
    `WoodRoof` can now actually snap onto a placed Wall's top edge, not
    only a Foundation's.
-   `PlayerBuilding.Known Pieces` on the `Player` GameObject in
    `SampleScene` extended from 4 to 5 entries, adding `WoodDoorPiece`
    alongside the existing `WoodFoundationPiece`/`WoodWallPiece`/
    `WoodPalisadePiece`/`WoodRoofPiece`. Scene saved. Compiles clean, no
    Console errors.
-   NOT YET CONFIRMED -- needs an in-Editor playtest (place a Foundation,
    a Wall on one edge, a Door on another edge of the same Foundation,
    a Roof snapped onto the Wall's new top points, and a couple of
    Palisades chained in a row; demolish a piece or two to confirm E
    still works on the new types). This closes
    `ITEMS_AND_CRAFTING_v0.1.md` section 4's Door/Roof/Palisade item --
    per the Core Rule, Building itself is still the functional-but-basic
    state described in the Technical Debt section above (no structural
    validation/save/refund/dedicated UI), unchanged by this increment.

-   Bug report (developer): after building a Roof, every other piece
    stopped snapping to anything. Investigated `WoodRoof.prefab` and
    found it was never actually in the clean state its own setup docs
    (Step 21.2) describe -- its root Transform had a baked-in 45-degree
    X rotation, a stray world position, and scale `(2, 0.1, 2.4)`
    instead of the documented `(2, 0.2, 2)`, and its four snap points
    were all literally named `SnapPoint` (not North/South/East/West)
    with garbled fractional local coordinates -- consistent with the
    cube having been tilted directly in the Scene view (to preview a
    sloped-roof look) and its snap points hand-dragged into place while
    the parent was already tilted, then the whole thing dragged into
    the project as a prefab without ever resetting its transform.
    `PlayerBuilding.SpawnGhost`/`ComputeHalfHeight` explicitly assumes a
    piece prefab's root sits at identity rotation (`RotatePiece` is
    Y-only, per its own doc comment) -- Roof's baked pitch broke that
    assumption, producing a badly wrong world-space bounds read and
    therefore a badly wrong ghost height offset for Roof specifically;
    separately, its snap points' local coordinates only made geometric
    sense combined with that same 45-degree tilt, so once the placement
    code forces any ghost level (yaw-only) they no longer lined up with
    anything. Not a code bug -- `WoodFoundation`/`WoodPalisade` both
    already sit at clean identity root transforms; `WoodWall`/`WoodDoor`
    carry a harmless stray root *position* (translation doesn't affect
    the bounds read, so it never caused a symptom) but Roof also had
    the rotation, which does. The three Roofs the developer built while
    diagnosing this didn't persist (Play-mode-only Instantiate calls,
    discarded on exiting Play) -- no scene cleanup was needed.
    Fix: rebuilt `WoodRoof.prefab` from scratch to match Step 21.2
    exactly -- identity position/rotation, scale `(2, 0.2, 2)`, and four
    distinctly-named snap points (`SnapPoint_North/South/East/West`) at
    the documented local positions. NOT YET CONFIRMED -- needs the same
    Step 21.5 playtest re-run (Foundation, Wall+Door on its edges, Roof
    snapped onto the Wall's new top points, Palisades chained).

-   Second bug report (developer): after placing a Foundation, neither
    Wall nor Palisade would snap to it at all -- and this was reported
    even for the pieces the Roof fix above didn't touch, so it wasn't
    the same issue. Verified `PlayerBuilding`'s snap-search algorithm
    itself is correct by replaying it directly (reflection, in Play
    mode) against the current (already-fixed) prefabs -- a Wall ghost
    aimed exactly at a Foundation edge point matched at distance 0, so
    the math and current prefab geometry are fine on their own.
    The real bug: every one of the five Building piece prefabs
    (`WoodFoundation`/`WoodWall`/`WoodDoor`/`WoodPalisade`/`WoodRoof`)
    had their `BuildingPiece._definition` field **pre-assigned in the
    prefab asset itself** (e.g. `WoodFoundation.prefab` shipped with
    `_definition: WoodFoundationPiece` already set), instead of being
    left empty and only set at runtime via `Configure()` for real
    placed pieces, per `BuildingPiece`'s own design ("Ghost previews
    never call Configure, so they never appear here"). Because
    `OnEnable` registers into the static `AllPieces` list whenever
    `_definition != null`, a baked-in definition meant every **ghost
    preview** (not just real placed pieces) self-registered the moment
    it was instantiated -- confirmed live: entering Build mode with
    nothing placed anywhere showed `AllPieces.Count == 1` and
    `IsSnapped == True` immediately, matching the developer's exact
    'snapped always True, even with nothing else built' report. Since
    `TryApplySnap`'s closest-pair search included the ghost's own
    (self-registered) points, it always found a same-object match at
    distance 0 -- which, being smaller than any real distance to an
    actual other piece, won every single time and silently "snapped to
    itself" with a zero-length move instead of ever reaching toward a
    real target. This affected every piece type identically, matching
    the developer's "neither Wall nor Palisade" report exactly.
    Not something introduced this session -- likely leaked in whenever
    the definitions were originally hand-authored onto these prefabs
    (same manual-authoring method used throughout this project's
    placeholder content), and apparently pre-dates even the Step 16
    snapping confirmation, meaning that confirmation may have relied
    on aim/timing that happened not to expose it, or regressed
    afterward via an Inspector "Apply to Prefab" on a placed instance.
    Fix: cleared `_definition` back to None on all five piece prefabs.
    Re-verified live (reflection-driven, in Play mode): with nothing
    placed, `AllPieces.Count == 0` and `IsSnapped == False`; after
    placing one real Foundation, `AllPieces.Count == 1` (the real piece
    only); a Wall ghost aimed at the Foundation's edge point correctly
    snapped (`IsSnapped == True`, final position matched the expected
    edge coordinate exactly). No Console errors. Both this fix and the
    Roof fix above are now confirmed working end-to-end via direct
    in-Editor testing (not yet re-confirmed by the developer's own
    manual playtest, but the underlying mechanism is verified sound).
    Worth flagging for future placeholder-content authoring: don't
    assign a BuildingPieceDefinition's own Prefab's `_definition` field
    directly in the prefab Inspector -- it should stay empty.

### v0.1 --- 2026-09-06

-   Developer confirmed `UNITY_SETUP_NEXT_STEPS.md` Step 22 end-to-end
    (Death/Loot). This closes Phase 7 (Combat) except the already-
    deferred Damage feedback item. Per the Core Rule's development
    order, the next unstarted stage is Creatures and AI (Phase 8) -
    waiting for the developer's go-ahead, same pattern as before
    Building/Combat.
-   Developer said go on Creatures (Phase 8). Creatures increment 1:
    added `CreatureDefinition` + `Creature` (new
    `Scripts/Gameplay/AI/` folder) - a `NavMeshAgent`-driven state
    machine (Patrol/Chase/Attack/Search/Return/Flee/Dead), trigger-
    radius perception (same idiom as `Workbench`/`CombatDummy`),
    `IDamageable` implemented like `PlayerCombat`/`CombatDummy`. First
    use of `com.unity.ai.navigation` in the project. Hostile creatures
    chase/attack on detection; neutral creatures only flee once hit
    (GDD 17). On death, scatters `LootDrops` (reusing
    `ResourceNodeDrop`) via the existing `ItemPickup.SpawnInWorld` path
    and despawns after a delay (unlike the reusable `CombatDummy`).
    Placeholder content: `RawMeat.asset` (new Food item, no WorldPrefab
    yet), `WildBoarCreature.asset` (neutral), `ForestWolfCreature.asset`
    (hostile). Implements every Phase 8 checklist item in one pass, same
    approach as Combat increment 1. NOT YET CONFIRMED - needs a baked
    NavMesh, two test creature GameObjects, and a World Prefab for
    RawMeat; see `UNITY_SETUP_NEXT_STEPS.md` Step 23.
-   Developer confirmed Step 23 end-to-end (NavMesh bake, both test
    creatures, RawMeat's World Prefab). Creatures increment 1 (all of
    Phase 8) is closed. Per the Core Rule, the next unstarted stage is
    World (Phase 9) - the largest/riskiest stage yet (procedural
    terrain/biomes/seed); waiting for the developer's go-ahead, likely
    scoped down to a small first slice rather than the full checklist.
-   Developer said go on World (Phase 9), then scope was discussed
    before writing code: Unity Terrain (not hand-rolled mesh), one
    bounded non-streaming Terrain, three biomes (Ocean/Plains/Forest -
    rivers explicitly deferred once the algorithmic cost was explained).
    World increment 1: added `Scripts/World/` (`BiomeDefinition`,
    `WorldGenerationSettings`, `WorldGenerator`) implementing
    `ARCHITECTURE_v0.1.md`'s pipeline (Seed -> Noise -> Heightmap ->
    Land/Water -> Biome Assignment) - seeded via `System.Random` (not
    `UnityEngine.Random`) for reproducibility, two blended Perlin
    layers for height + an independent moisture channel for Plains vs
    Forest, `TerrainData.SetHeights`/`SetAlphamaps`, a placeholder water
    plane at sea level, and a runtime `NavMeshSurface.BuildNavMesh()`
    call once generation finishes (answers the runtime-NavMesh question
    raised during Step 23). Terrain Features/Resources/Vegetation/
    Creatures/POI are later pipeline stages, out of scope here.
    Placeholder data ships with no `TerrainLayer` textures assigned (art
    strategy still "Not Yet Decided"). Setup has the developer build a
    new `WorldGenTest` scene (duplicated from `SampleScene`, old ground/
    test objects stripped, Player/HUD kept) rather than regenerating
    terrain under `SampleScene`'s existing confirmed test setup. NOT YET
    CONFIRMED; see `UNITY_SETUP_NEXT_STEPS.md` Step 24.
-   Developer confirmed generation works (screenshot). Follow-up
    feedback: terrain read as one uniform mountain range despite
    correct biome textures - fixed by making height computation
    biome-first (a shared biome map decides Land/Water/Plains/Forest
    before any detail noise is added, then that noise is scaled by a
    new per-biome `HeightVariation`: Ocean/Plains 0.15, Forest 0.65) and
    softening `WorldGenerationSettings` defaults (MaxHeight 60->40,
    HeightNoiseScale 180->260, DetailNoiseWeight 0.2->0.35 as a ceiling
    now further scaled per-biome). Also added `BiomeResourceSpawn`
    (prefab + Density + MinSpacing) and a `ResourceSpawns` list on
    `BiomeDefinition`, plus a `WorldGenerator.SpawnResources` pass
    (deterministic, seed-derived RNG, positioned via
    `Terrain.SampleHeight`) - wired to existing unused prefabs
    (`SM_Rocks_03`/`Flint_Ore_Rock_01` on Plains, `UNS_Spruce_WoodNode`
    on Forest). Separately clarified that Creature loot drops already
    matched the ResourceNode pattern exactly (no code change - a
    documentation/discoverability answer, not a bug). NOT YET CONFIRMED
    for this addendum; see `UNITY_SETUP_NEXT_STEPS.md` Step 24.6.

-   Third round of bug reports (developer, with screenshots): Wall/Door
    only ever attach in one position, Palisade attaches at an edge
    midpoint instead of a corner, Foundation sinks, Roof looks/behaves
    like a floor. Investigated live in the developer's own running Play
    session (read-only inspection plus small isolated far-away test
    instances, careful not to disturb their placed pieces).
-   Confirmed and fixed: `WoodPalisade.prefab`'s two snap points were on
    the Y axis (top-center/bottom-center - meant for stacking
    vertically), not the X axis (bottom-left/bottom-right corners,
    what Step 21.3 actually specified for chaining palisades
    side-by-side in a row). Rebuilt with correct
    `SnapPoint_BottomLeft`/`SnapPoint_BottomRight` at `(-0.5,-0.5,0)`/
    `(0.5,-0.5,0)`. Also re-cleared `_definition` on this prefab (the
    delete/recreate touched the same component).
-   Explained (not a new bug, a consequence of the previous Roof fix +
    an existing known limitation): the developer had placed a Roof
    directly on the ground with no Foundation underneath. Since Roof's
    snap points are (by design, matching Foundation's own edge-midpoint
    layout) on its underside, a Roof sitting on the ground is
    geometrically indistinguishable from a mini-Foundation once
    something else looks for a nearby snap point - confirmed live, a
    Door had snapped to that Roof's own edge point, not to any
    Foundation. This is the existing "no socket-type/compatibility
    system - any BuildingSnapPoint can pull any other into alignment"
    Technical Debt item, just newly visible now that Roof's shape
    matches Foundation's. Combined with the previous fix flattening
    Roof's *visual* to match its (necessarily flat, for correct height
    math) collision footprint, this made Roof read as "just another
    floor" both by eye and in practice.
-   Fix (developer chose "tilt the mesh only, keep the hitbox/points
    level"): `PlayerBuilding.ComputeHalfHeight` changed from reading
    `GetComponentsInChildren<Renderer>()` bounds to reading the piece's
    own `Collider` bounds instead (every piece already guarantees
    exactly one via `[RequireComponent(typeof(Collider))]`). Behavior-
    preserving for every existing piece (their Collider and Renderer
    bounds were always identical - none had a separate visual mesh) -
    this only matters once a piece's visual mesh diverges from its
    collision box, which Roof now does. `WoodRoof.prefab` reworked:
    root keeps only `BoxCollider` + `BuildingPiece` (no MeshFilter/
    MeshRenderer - root itself is invisible, flat, level, exactly like
    Foundation's collision footprint) plus its 4 (still-level, still
    correctly-positioned) snap points; a new non-colliding child
    `Visual` (a `-20` degree X-tilted cube, roughly 2.2 x 0.1 x 1.5 in
    world size) provides the sloped-roof look. Rough placeholder
    numbers, not final art - easy to retune by hand in the Inspector.
    Doesn't address the Roof-acts-like-Foundation ambiguity itself
    (that's the pre-existing no-socket-types limitation) - flagged, not
    fixed, since fixing it properly needs a real socket-type/compat
    system, an architecture change requiring its own separate
    conversation per CLAUDE.md.
-   Investigated but could NOT reproduce as a data/algorithm bug:
    Wall/Door "only one position" and Foundation "sinking". Replayed
    `PlayerBuilding`'s exact snap search (reflection, live) for a Wall
    ghost against all 4 Foundation edges independently - each matched
    correctly with the expected position AND rotation (0 or 90 degrees
    per edge). Verified `ComputeHalfHeight` returns the geometrically
    correct value for every piece type (Foundation/Roof 0.1, Wall/Door/
    Palisade 1.0). Suspect at least some of what looked like "wall/door
    only attach one way" was actually the Door-snapped-to-Roof mix-up
    above rather than a Wall/Foundation-specific bug. NOT YET
    CONFIRMED either way - needs a clean re-test (fresh Play session,
    for the ComputeHalfHeight fix to take effect; a real Foundation
    present, not a bare Roof) before concluding whether these two are
    real remaining bugs or were artifacts of the Roof/Definition issues
    fixed this round.

-   Fourth round (developer, with screenshots): Roof's new visual read
    as a small floating disconnected rectangle rather than the liked
    'full-length diagonal' look; Palisade still wasn't attaching at a
    Foundation corner. Root cause for the Palisade complaint was
    mis-diagnosed last round -- the fix needed wasn't on Palisade's own
    points at all. `WoodFoundation` never had any corner points, only
    the 4 edge-midpoints -- there was no corner target for anything to
    snap to, regardless of what points Palisade itself carried. Added 4
    new corner points to `WoodFoundation.prefab`
    (`SnapPoint_CornerNE/NW/SE/SW` at local `(±0.5, 0.5, ±0.5)`) --
    purely additive, doesn't touch the existing 4 edge points other
    pieces already rely on. Verified live: aiming a Palisade ghost
    genuinely near a corner (not exactly on top of the shared boundary
    between a corner and its neighboring edge-midpoint, which is a
    literal tie and resolves to whichever point the loop visits first)
    now correctly matches the new corner point instead of the edge
    midpoint.
-   `WoodRoof`'s `Visual` child reworked to match the shape the
    developer liked before (a single full-length diagonal slab) instead
    of the smaller offset lean-to panel from the previous round: local
    scale `(1, 0.5, 1.2)` / rotation `(45,0,0)` / position `(0, 0.5, 0)`
    against the root's `(2, 0.2, 2)` scale -- works out to the same
    `(2, 0.1, 2.4)` world size and 45-degree tilt the original
    (accidentally-broken) prefab had, just as a non-colliding child now
    instead of the root itself. Verified live: Roof-to-Foundation
    snapping still matches correctly (root/collider math untouched by
    this), and the visual's world bounds sit centered right at the
    footprint's top surface rather than offset to one side.
    NOT YET CONFIRMED by the developer visually (rough placeholder
    proportions, not final art) -- needs a fresh Play session (the
    ComputeHalfHeight code change from the previous round only takes
    effect after Play is restarted) and a normal playtest: Foundation,
    Wall/Door on edges, Roof on a Wall's top points, Palisade at a
    corner and chained along an edge.

-   Developer feedback: Palisade should attach ONLY at Foundation
    corners - the edge-midpoint attachment (still possible after the
    previous round's fix, since nothing stopped a Palisade's point from
    matching an edge-midpoint target too) needed to go away entirely.
    This needed a real (if minimal) architecture addition, not just more
    content: added `BuildingSnapPoint.Kind` (`enum SnapKind { Edge,
    Corner }`, defaults to `Edge`) and changed
    `PlayerBuilding.TryApplySnap`'s inner loop to skip any
    (ghost point, target point) pair whose `Kind` doesn't match. This is
    still not a full socket-compatibility system (no notion of e.g.
    'only a wall may plug into this'), just enough of a category so
    edge-hardware and corner-hardware stop being interchangeable.
    Set `Kind = Corner` on `WoodFoundation`'s 4 new corner points and
    `WoodPalisade`'s 2 points; everything else (Foundation's original 4
    edge points, all of Wall/Door/Roof's points) keeps the `Edge`
    default unchanged, so their existing behavior from prior rounds is
    untouched. Verified live (reflection): aiming a Palisade ghost
    exactly at a Foundation edge midpoint now finds NO match at all
    (previously it would snap there); aiming at/near a corner still
    matches correctly; Palisade-to-Palisade chaining (both ends are
    `Corner`) still matches correctly. No Console errors.
    Note for future content: any new snap point that should behave like
    a corner-post attachment needs `Kind` set to `Corner` explicitly in
    the Inspector (or via code, since the default is `Edge`).

-   Combat: Death and Loot/drop (closes every remaining Phase 7 checklist
    item except Damage feedback). `PlayerHealth` gained `IsAlive`/`Died`
    (fires once at 0 HP)/`Revive()`; `PlayerCombat` delegates `IsAlive` to
    it and stops applying damage/block/parry once dead. New `PlayerDeath`
    (Scripts/Gameplay/Player) disables Motor/Combat/Interactor/Building
    and frees the cursor on death, re-enables them and calls a new
    `PlayerMotor.Teleport(...)` back to the scene's starting
    position/rotation on Respawn. New `DeathUIController` +
    `DeathPanel.uxml`/`.uss` (Scripts/UI) - centered "You Died" + Respawn
    overlay, same pattern/palette as `PauseUIController`/`PausePanel`.
    `CombatDummy` gained a **Loot Drops** list (reuses `ResourceNodeDrop`
    from Items as-is) and scatters it as physical `ItemPickup`s via the
    existing `ItemPickup.SpawnInWorld` path on death (mirrors
    `ResourceNode.SpawnDrops`); guarded to fire once; the dummy itself
    does not despawn (reusable test target, not content).
    `PlayerDebugHud`'s Health line now appends `(DEAD)`. Explicitly out of
    scope: Phase 6's Gravestone/Respawn (inventory loss/recovery per
    GDD_v0.1.md section 9) and Damage feedback (VFX/audio). NOT YET
    CONFIRMED - see `UNITY_SETUP_NEXT_STEPS.md` Step 22.

### v0.1 --- 2026-09-07

-   Performance investigation (developer's own in-Editor profiler
    screenshot after Step 24.6's resource-spawn addendum: 82 FPS, 1157
    draw calls, 232 of them Non-SRP-Compatible, ~1.43M triangles). Read-
    only findings, no code changed yet at this point: `WorldGenerator`
    Instantiates every spawned resource as a fully separate GameObject
    (no pooling/batching), Forest/Plains density+spacing plausibly
    yields 1000+ instances map-wide, `SM_Rocks_03.prefab` has no
    `LODGroup` at all (unlike the tree/Flint rock prefabs, which have
    one), and `Assets/PolyOne/Rocks Stylized/Materials/Rocks Stylized_M.mat`
    had GPU Instancing disabled (`m_EnableInstancingVariants: 0`) while
    the tree/rock material sharing the same shader had it enabled - the
    likely source of the 232 non-SRP-compatible draw calls. Explained
    fixes to the developer (enable GPU Instancing on that material, add
    a `LODGroup`/Culled zone to `SM_Rocks_03`) as manual Editor steps -
    developer chose to do those two by hand rather than have them
    scripted.
-   Render Distance (Core layer, new): added
    `Assets/NAV/Scripts/Core/RenderDistanceSettings.cs` (ScriptableObject:
    Min/Max/Default distance, Fog Start Ratio, Resource Layer Name,
    Resource Cull Ratio) and `RenderDistanceController.cs` (applies one
    distance value to `RenderSettings` fog, `Camera.farClipPlane`, and
    `Camera.layerCullDistances` for a dedicated `WorldResource` layer -
    the actual performance win, since fog alone only hides pop-in
    visually without reducing draw calls; culling the thousands of
    small spawned resources at a shorter distance than the terrain is
    what does). `SetDistance(float)` is the single entry point - no
    Settings UI exists yet (see "Not Yet Decided" in this file), so this
    is the API a future slider will bind to; until then it's tested live
    by dragging the `Distance` field in the Inspector during Play Mode
    (`OnValidate` re-applies through the same code path) and persists
    across sessions via `PlayerPrefs`.
-   `WorldGenerationSettings` gained **Resource Layer Name** (default
    `WorldResource`, must match `RenderDistanceSettings`' own field of
    the same name - two independent Core/World-layer assets by design,
    not cross-referenced, per ARCHITECTURE_v0.1.md keeping Core
    unaware of World). `WorldGenerator.SpawnBiomeResources` now
    recursively assigns that layer (root + every child, so LOD
    sub-meshes are covered too) to each spawned resource instance,
    resolved once per `Generate()` call rather than per instance; logs
    a clear error (not a silent no-op) if the layer doesn't exist yet in
    the project.
-   NOT YET CONFIRMED - needs the `WorldResource` Layer created, a
    `RenderDistanceSettings` asset, and a `RenderDistanceController`
    GameObject wired up in `WorldGenTest`; see
    `UNITY_SETUP_NEXT_STEPS.md` Step 25.
-   Per developer's explicit instruction, World (Phase 9) work pauses
    here once Render Distance is confirmed - `DEVELOPMENT_ROADMAP_v0.1.md`
    Phase 9 stays intentionally partial (Birch grove, Dense forest,
    Vegetation distribution, Spawn regions, Points of interest are all
    still unchecked and not being picked up now - not forgotten, just
    deliberately deferred, same pause pattern used before
    Building/Combat/Creatures each started). Per the Core Rule, the next
    stage is **Save** (Phase 11).
-   Playtest of Render Distance surfaced a visual artifact: the flat
    `Fog Color` only matches the procedural Skybox's gradient at one
    point (its horizon color, roughly), so distant terrain - even fully
    fogged - still reads as a visible silhouette against the sky
    (`RenderSettings.fog` never touches the Skybox itself, which draws
    at infinite distance with its own gradient regardless of fog).
    Explained the fix (flat `Skybox/Color` material matching Fog Color,
    or `FogMode.ExponentialSquared` instead of `Linear`, or a shorter
    `RenderDistanceSettings.MaxDistance` so the terrain's own 500x500
    edge is never in view) but developer chose to defer tuning this
    until Polish rather than iterate on it now. Per developer request,
    Render Distance/Fog is **deactivated in the `WorldGenTest` scene** -
    developer removed the `RenderDistanceController` GameObject entirely
    (disabling it alone didn't visibly remove the fog, since
    `RenderSettings.fog` is a separate scene Environment setting the
    Step 25.5 instructions had them check by hand in the Lighting window
    - it isn't owned/reset by the component, so it needed unchecking
    there too). All the underlying code
    (`RenderDistanceSettings`/`RenderDistanceController` scripts, the
    `WorldResource` layer assignment in `WorldGenerator`/
    `WorldGenerationSettings`) stays in place untouched and ready to
    recreate/tune later - see `UNITY_SETUP_NEXT_STEPS.md` Step 25 for
    the setup to redo (GameObject + Fog checkbox) whenever this is
    revisited at Polish.
    World (Phase 9) is now fully paused; moving on to **Save** (Phase
    11) next, scoped down to Increment 1 = player state (position/HP/
    inventory) + world seed only - world state (buildings, depleted/
    gathered resource nodes) explicitly deferred, developer's own call.
-   Save, Increment 1 (developer expanded the scope from the plan above
    once discussion started: player + seed + **placed buildings**, plus
    a Main Menu Load screen and periodic autosave-into-its-own-slot).
    New `Assets/NAV/Scripts/Core/Save/` folder: `SaveGameData.cs` (plain
    serializable data - seed, player position/rotation/health/inventory
    slots, a list of placed buildings; deliberately NOT the terrain
    itself, which regenerates deterministically from the seed),
    `SaveSystem.cs` (static class - JSON file I/O under
    `Application.persistentDataPath/Saves/`, plus
    `PendingLoadSaveId`/`CurrentSaveId`/`CurrentSeed` static state that
    survives the MainMenu -> WorldGenTest scene load within one Play
    session without needing a DontDestroyOnLoad object),
    `SaveManager.cs` (the gameplay-scene bootstrapper: on Start, either
    begins a new game with a fresh random seed or loads a chosen save -
    regenerating the world from its seed and restoring player/building
    state - then runs a coroutine that autosaves on an interval,
    **always overwriting the same save id**, never creating a second
    file, per the developer's explicit "autosave replaces the world's
    own save" requirement), `ItemDatabase.cs`/`BuildingPieceDatabase.cs`
    (hand-maintained id->asset lookup lists, same "fixed serialized
    list" pattern as `PlayerCrafting.KnownRecipes`/
    `PlayerBuilding.KnownPieces` - needed because JSON can't hold a
    direct Unity asset reference).
-   Small supporting changes: `BuildingPieceDefinition` gained an
    **Id** field (auto-fills from the asset name, same pattern
    `ItemDefinition.Id` already had). `Inventory` gained `SetSlot(index,
    definition, quantity)` (bypasses `AddItem`'s stacking/weight-budget
    policy - a load restore of already-owned items must never be
    rejected). `PlayerHealth` gained `SetHealth(value)` (same "force
    state directly" precedent as the existing `Revive()`).
    `WorldGenerator.Generate()` split into a parameterless overload
    (uses `Settings.Seed` - manual Editor testing/Regenerate, unchanged
    behavior) and `Generate(int seed)` (what `SaveManager` actually
    calls - never mutates the `WorldGenerationSettings` asset itself,
    since editing a ScriptableObject asset's fields at runtime
    persists after stopping Play in the Editor, which would have
    silently corrupted the asset). New **Generate On Start** toggle
    (default on, preserves existing standalone-scene-testing behavior)
    lets `SaveManager` be the sole caller of `Generate()` once turned
    off, avoiding a double-generation race with `WorldGenerator`'s own
    `Start()`.
-   **Architecture decision (developer-confirmed, not made
    unilaterally):** `WorldGenTest` becomes the real game's scene going
    forward - `MainMenuUIController`'s `Game Scene Name` now defaults to
    `WorldGenTest` instead of `SampleScene` in code (existing scene data
    still needs the field updated by hand, see
    `UNITY_SETUP_NEXT_STEPS.md` Step 26.8). Reasoning: seed-based
    save/load only makes sense in the scene that actually has a
    `WorldGenerator` - `SampleScene` never had one. `SampleScene` stays
    in the project as a sandbox for testing systems outside world
    generation, but is no longer what Play/Load actually loads.
-   `MainMenuUIController`/`MainMenuPanel.uxml`/`.uss` reworked: Play
    now explicitly starts a new game (clears
    `SaveSystem.PendingLoadSaveId`); a new **Load** button switches the
    same panel to a second view listing every save from
    `SaveSystem.ListSaves()` (seed + saved-at timestamp per entry,
    newest first) - clicking one sets `PendingLoadSaveId` and goes
    through the identical loading-screen/SceneLoader flow Play already
    used. `PauseUIController` gained an optional **Save Manager**
    reference - if wired, "Leave to Main Menu" autosaves first so
    progress since the last periodic autosave isn't lost.
-   NOT YET CONFIRMED - needs `WorldGenTest` added to Build Settings, an
    `ItemDatabase`/`BuildingPieceDatabase` asset populated with existing
    content, `WorldGenerator.Generate On Start` turned off, a
    `SaveManager` GameObject wired up, `PauseUI`'s new field set, and
    `MainMenuUI`'s scene name field corrected by hand; see
    `UNITY_SETUP_NEXT_STEPS.md` Step 26 for the full walkthrough and its
    end-to-end playtest (new game -> autosave -> leave to menu -> Load
    -> same seed/player/buildings restored).
-   Explicitly deferred (developer's own scope call, same as the
    original Increment 1 plan): which resource nodes were already
    gathered/depleted is not saved - every load respawns all resources
    fresh from the seed. No delete-save UI, no manual Save button (only
    autosave + save-on-leave-to-menu), no multiple slots per world
    lineage (every "New Game" creates a distinct save file; Load lists
    all of them together).
-   **Known limitation, flagged to the developer (not fixed - this is
    Roadmap Phase 11's own separate unchecked "Save versioning" item):**
    a save only stores the world Seed, not the terrain itself - loading
    regenerates it from scratch via WorldGenerator.Generate(seed) using
    whatever WorldGenerationSettings values exist *at load time*. If
    those settings (or the generation algorithm) change after a save
    was made, the same seed produces a *different* terrain, and that
    save's stored building/player world-space coordinates silently stop
    matching the new ground (buildings floating/buried, player possibly
    spawning in water). No version/hash check exists yet to detect or
    warn about this mismatch. Practical implication right now: avoid
    tweaking WorldGenerationSettings if existing test saves need to stay
    valid. There is also no in-game "regenerate this save's world"
    feature - New Game always creates a brand new save file with a new
    seed rather than touching an existing one.
-   Load screen polish (developer request): each save card in
    `MainMenuUIController`'s Load list gained a delete (`✕`) button, and
    both loading and deleting now go through a shared confirmation
    dialog (new `confirm-dialog-root` overlay in `MainMenuPanel.uxml`/
    `.uss`, generic `ShowConfirm(message, onConfirm)` in the
    controller) instead of acting immediately on click.
    `SaveSystem.DeleteSave(saveId)` added (no-op if the file doesn't
    exist). No new Inspector wiring - same `MainMenuUI` GameObject/
    fields as Step 26. NOT YET CONFIRMED - see the addendum to
    `UNITY_SETUP_NEXT_STEPS.md` Step 26.
-   Create World screen (developer request): Play no longer starts a
    game immediately - it now opens a new `create-world-panel` (World
    Name text field, Seed text field pre-filled with a script-generated
    random number, a Randomize reroll button, Create World/Back
    buttons). `SaveGameData` gained `WorldName`; `SaveSystem` gained
    `PendingNewGameName`/`PendingNewGameSeed` (same
    set-before-scene-load, read-once-and-clear pattern as
    `PendingLoadSaveId`) and `CurrentWorldName`. `SaveManager.StartNewGame`
    reads them (falling back to a random seed + "New World" if empty -
    i.e. the gameplay scene was entered directly, not through this
    screen) and threads `CurrentWorldName` through to every
    `Autosave()`/`LoadFrom()`. The seed field accepts non-numeric text
    too (`MainMenuUIController.ResolveSeed`/`StableHash`) - hashed into a
    deterministic int with a custom FNV-1a-style hash rather than
    `string.GetHashCode()`, which is intentionally randomized per
    process by .NET and would make the "same word -> same world"
    guarantee false. Save list cards now show `<WorldName> (seed N)`
    instead of an anonymous `World (seed N)`; pre-existing saves from
    before this change display as `Unnamed World (seed N)` (no data
    loss, they just never had a name field). No new Inspector wiring.
    NOT YET CONFIRMED - see the second addendum to
    `UNITY_SETUP_NEXT_STEPS.md` Step 26.
-   CONFIRMED - developer verified Step 26 end-to-end (base Save
    Increment 1, delete/confirm dialog, and the Create World name/seed
    screen): new game -> autosave -> leave to menu -> Load -> same
    world/player/buildings restored correctly. Save (Phase 11) is now
    closed at Increment 1's scope - `DEVELOPMENT_ROADMAP_v0.1.md`
    checked off Player save/Inventory save/World seed save/Building
    save; World changes/Equipment save/Skill save/Container save/Time-
    weather save/Save versioning/Backup-recovery stay unchecked
    (systems they depend on don't exist yet, or explicitly deferred -
    see this file's own "known limitation" note above). Per the Core
    Rule, Save was the last explicitly-named stage before Polish: the
    remaining gaps blocking a real Phase 12 Vertical Slice loop (`Spawn
    -> Gather -> Build -> Craft -> Explore -> Fight -> Eat -> Die ->
    Recover -> Save -> Reload`) are Eat (no food/hunger mechanic yet)
    and Recover (Phase 6's Gravestone/loot-recovery on death, separate
    from the existing bare-bones `PlayerHealth`/`PlayerDeath`) - proposed
    as the next candidate, waiting for the developer's go-ahead, same
    pause pattern as every prior stage transition.
