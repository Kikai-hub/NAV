using UnityEngine;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// GDD_v0.1 section 9 "Death": spawned by PlayerDeath at the death location, holding the
    /// Inventory extracted from the player at that moment (same capacity/slot layout - see
    /// Inventory.ExtractAll). Raycast-interactable (same idiom as ItemPickup/ResourceNode), but
    /// Interact() itself does nothing - it only exists so PlayerInteractor.Interacted fires,
    /// which GravestoneUIController listens for to open a grid panel (same look as the
    /// Inventory panel) the player drags items out of by hand, or empties in one click via its
    /// Move All button. Opening/moving items is deliberately a UI-layer job, not this class's -
    /// see ARCHITECTURE_v0.1.md's "gameplay shouldn't own UI" rule. Destroys itself once
    /// Contents is fully emptied, however that happened.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Gravestone : MonoBehaviour, IInteractable
    {
        // Fully-qualified everywhere in this file (rather than "using NAV.Gameplay.Inventory;"):
        // this namespace, NAV.Gameplay.Player, is a sibling of NAV.Gameplay.Inventory under the
        // shared NAV.Gameplay parent, so the bare name "Inventory" resolves to that sibling
        // *namespace* before a using-directive is even consulted (CS0118) - a using-alias at
        // the top of the file doesn't help either, since the same namespace-first lookup still
        // wins before reaching it.
        public NAV.Gameplay.Inventory.Inventory Contents { get; private set; }

        public string InteractionPrompt => "Open gravestone";

        /// <summary>Instantiates prefab at position, snapped to sit on top of it (position is
        /// assumed to be ground level - e.g. the dying player's own transform.position) rather
        /// than centered on it, then fills it with contents. Mirrors ItemPickup.SpawnInWorld's
        /// role as the shared spawn path.</summary>
        public static Gravestone Spawn(Gravestone prefab, Vector3 position, Quaternion rotation, NAV.Gameplay.Inventory.Inventory contents)
        {
            Gravestone instance = Instantiate(prefab, position, rotation);
            instance.SnapToGround(position);
            instance.Configure(contents);
            return instance;
        }

        private void OnDestroy()
        {
            if (Contents != null)
            {
                Contents.Changed -= HandleContentsChanged;
            }
        }

        private void Configure(NAV.Gameplay.Inventory.Inventory contents)
        {
            Contents = contents;
            Contents.Changed += HandleContentsChanged;
        }

        private void HandleContentsChanged()
        {
            if (Contents.IsEmpty)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Offsets this object upward by its own Collider's half-height so its
        /// bottom rests on groundPosition instead of the prefab's pivot (its visual/collider
        /// center) sinking halfway into the ground - same lesson PlayerBuilding's ground-anchor
        /// fix already learned: read Collider bounds, not Renderer bounds, since a disabled
        /// Renderer/Collider would collapse to zero (not the case here, just kept consistent).</summary>
        private void SnapToGround(Vector3 groundPosition)
        {
            float halfHeight = GetComponent<Collider>().bounds.extents.y;
            transform.position = new Vector3(groundPosition.x, groundPosition.y + halfHeight, groundPosition.z);
        }

        public bool CanInteract(GameObject interactor)
        {
            return Contents != null && !Contents.IsEmpty;
        }

        public void Interact(GameObject interactor)
        {
            // Intentionally empty - see class doc comment. PlayerInteractor fires Interacted
            // right after calling this, which is what GravestoneUIController actually reacts to.
        }
    }
}
