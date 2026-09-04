using UnityEngine;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// A marker on a building piece prefab (a child Transform, placed by hand in the Editor at
    /// an edge/corner - e.g. a wall's two bottom corners, a foundation's four edge midpoints)
    /// that PlayerBuilding's placement snapping can align to. Still deliberately minimal - no
    /// full socket-compatibility system - but Kind is a small category (Edge vs Corner) so,
    /// for example, a Palisade's corner-post points only pull toward a Foundation's actual
    /// corners, not its edge midpoints (which Walls/Doors/Roof use instead). See
    /// PlayerBuilding.TryApplySnap, which only matches points that share the same Kind.
    /// Rotation is not auto-aligned to a target socket; the player rotates the ghost manually
    /// (RotatePiece) and only position snaps once close enough.
    /// </summary>
    public class BuildingSnapPoint : MonoBehaviour
    {
        public enum SnapKind
        {
            Edge,
            Corner
        }

        [SerializeField] private SnapKind _kind = SnapKind.Edge;

        public SnapKind Kind => _kind;

        private void OnDrawGizmos()
        {
            Gizmos.color = _kind == SnapKind.Corner ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}
