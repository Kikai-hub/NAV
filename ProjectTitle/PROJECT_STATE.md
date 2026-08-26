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
