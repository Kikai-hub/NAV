using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.Core.Save
{
    /// <summary>
    /// Everything one save slot holds. Deliberately NOT the whole world (per the developer's own
    /// scope call, see PROJECT_STATE.md) - the terrain itself is regenerated deterministically
    /// from WorldSeed on load (see WorldGenerator.Generate(int)); only the state that generation
    /// can't reproduce on its own is stored here. Increment 1: player + placed buildings. NOT
    /// saved: which resource nodes were already gathered/depleted (they respawn fresh every
    /// load) - a known, deliberate limitation, same category as WorldGenTest's other deferred
    /// items.
    /// </summary>
    [Serializable]
    public class SaveGameData
    {
        public string SaveId;
        public string WorldName;
        public long SavedAtUnixSeconds;
        public int WorldSeed;
        public PlayerSaveData Player = new();
        public List<BuildingSaveData> Buildings = new();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float CurrentHealth;
        public List<InventorySlotSaveData> InventorySlots = new();
    }

    /// <summary>One occupied inventory slot. ItemId (ItemDefinition.Id), not a direct asset
    /// reference - JSON can't hold a Unity object reference, so ItemDatabase resolves this back
    /// to the real ItemDefinition on load.</summary>
    [Serializable]
    public class InventorySlotSaveData
    {
        public int SlotIndex;
        public string ItemId;
        public int Quantity;
    }

    /// <summary>One placed building piece. PieceId (BuildingPieceDefinition.Id), resolved back
    /// via BuildingPieceDatabase on load, same reasoning as InventorySlotSaveData.ItemId.</summary>
    [Serializable]
    public class BuildingSaveData
    {
        public string PieceId;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
