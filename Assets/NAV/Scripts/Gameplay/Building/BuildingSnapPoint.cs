using UnityEngine;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// A marker on a building piece prefab (a child Transform, placed by hand in the Editor at
    /// an edge/corner - e.g. a wall's two bottom corners, a foundation's four edge midpoints)
    /// that PlayerBuilding's placement snapping can align to. Deliberately just a position
    /// marker with no "socket type"/compatibility rules yet - any snap point can pull any other
    /// snap point into alignment. Rotation is not auto-aligned to a target socket; the player
    /// rotates the ghost manually (RotatePiece) and only position snaps once close enough.
    /// </summary>
    public class BuildingSnapPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}
