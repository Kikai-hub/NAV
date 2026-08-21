using UnityEngine;

namespace NAV.Gameplay.Inventory
{
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int _capacity = 20;

        public Inventory Inventory { get; private set; }

        private void Awake()
        {
            Inventory = new Inventory(_capacity);
        }
    }
}
