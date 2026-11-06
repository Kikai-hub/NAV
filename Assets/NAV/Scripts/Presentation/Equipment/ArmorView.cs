using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;

namespace NAV.Presentation.Equipment
{
    /// <summary>
    /// Shows worn armour on the character: whenever PlayerEquipment changes, every worn piece's
    /// ArmorDefinition.WornParts are spawned under their bones and the old ones removed. Purely
    /// visual, like HeldItemView - observes PlayerEquipment, owns no state, strips colliders.
    ///
    /// Worn models are authored in character space (+Y up, +Z forward, origin at the bone's
    /// pivot), so they don't depend on how each bone's axes happen to point. Each bone's
    /// rotation relative to the character is recorded in Awake, while the rig is still in its
    /// bind pose (before the Animator's first update), and applied to the spawned model.
    /// </summary>
    public class ArmorView : MonoBehaviour
    {
        [Tooltip("What the character wears: the player's PlayerEquipment, or a network avatar's mirror (any IEquipmentView).")]
        [SerializeField] private MonoBehaviour _equipment;
        [Tooltip("Rig bones the worn models attach to - names on the Kevin Iglesias rig.")]
        [SerializeField] private string _headBone = "B-head";
        [SerializeField] private string _chestBone = "B-chest";
        [SerializeField] private string _thighLeftBone = "B-thigh.L";
        [SerializeField] private string _thighRightBone = "B-thigh.R";
        [SerializeField] private string _shinLeftBone = "B-shin.L";
        [SerializeField] private string _shinRightBone = "B-shin.R";

        private readonly Dictionary<ArmorBone, Transform> _bones = new();
        private readonly Dictionary<ArmorBone, Quaternion> _bindRotations = new();
        private readonly List<GameObject> _spawned = new();
        private IEquipmentView _view;

        private void Awake()
        {
            _view = _equipment as IEquipmentView;
            if (_view == null)
            {
                Debug.LogError($"{nameof(ArmorView)} on '{name}' is missing its equipment (an IEquipmentView).", this);
                enabled = false;
                return;
            }

            var byName = new Dictionary<string, Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                byName.TryAdd(child.name, child);
            }

            MapBone(byName, ArmorBone.Head, _headBone);
            MapBone(byName, ArmorBone.Chest, _chestBone);
            MapBone(byName, ArmorBone.ThighLeft, _thighLeftBone);
            MapBone(byName, ArmorBone.ThighRight, _thighRightBone);
            MapBone(byName, ArmorBone.ShinLeft, _shinLeftBone);
            MapBone(byName, ArmorBone.ShinRight, _shinRightBone);
        }

        private void OnEnable()
        {
            if (_view != null)
            {
                _view.Changed += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_view != null)
            {
                _view.Changed -= Refresh;
            }
        }

        private void MapBone(Dictionary<string, Transform> byName, ArmorBone bone, string boneName)
        {
            if (!byName.TryGetValue(boneName, out Transform found))
            {
                Debug.LogError($"{nameof(ArmorView)} on '{name}': bone '{boneName}' not found - armour on {bone} won't show.", this);
                return;
            }

            _bones[bone] = found;
            _bindRotations[bone] = Quaternion.Inverse(found.rotation) * transform.rotation;
        }

        private void Refresh()
        {
            foreach (GameObject instance in _spawned)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _spawned.Clear();
            foreach (ItemDefinition item in _view.Worn.Values)
            {
                foreach (ArmorWornPart part in item.Armor.WornParts)
                {
                    if (part.Prefab == null || !_bones.TryGetValue(part.Bone, out Transform bone))
                    {
                        continue;
                    }

                    GameObject instance = Instantiate(part.Prefab, bone, false);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = _bindRotations[part.Bone];
                    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    {
                        Destroy(collider);
                    }

                    _spawned.Add(instance);
                }
            }
        }
    }
}
