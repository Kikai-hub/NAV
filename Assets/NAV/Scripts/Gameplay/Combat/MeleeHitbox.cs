using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// The striking part of a held weapon model (put it on the item's HeldPrefab root): a
    /// capsule from Start to End in this transform's local space - e.g. from just above the
    /// guard to the sword's tip, or across an axe head. It is not a Collider (held models have
    /// none) - PlayerCombat sweeps it with physics queries during the swing's hit window, so
    /// damage lands at the moment of real contact. Shown as a gizmo when selected.
    /// </summary>
    public class MeleeHitbox : MonoBehaviour
    {
        [SerializeField] private Vector3 _start = new(0f, 0.1f, 0f);
        [SerializeField] private Vector3 _end = new(0f, 0.9f, 0f);
        [SerializeField, Min(0.01f)] private float _radius = 0.08f;

        public void GetWorldCapsule(out Vector3 start, out Vector3 end, out float radius)
        {
            start = transform.TransformPoint(_start);
            end = transform.TransformPoint(_end);
            Vector3 scale = transform.lossyScale;
            radius = _radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private void OnDrawGizmosSelected()
        {
            GetWorldCapsule(out Vector3 start, out Vector3 end, out float radius);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(start, radius);
            Gizmos.DrawWireSphere(end, radius);
            Gizmos.DrawLine(start, end);
        }
    }
}
