using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.Core.Save
{
    /// <summary>
    /// Everything one save slot holds. Deliberately NOT the whole world (per the developer's own
    /// scope call, see PROJECT_STATE.md) - the terrain itself is regenerated deterministically
    /// from WorldSeed on load (see WorldGenerator.Generate(int)); only the state that generation
    /// can't reproduce on its own is stored here: player, placed buildings, time, weather and
    /// which resource nodes are already gathered (as spawn-index ids). Still NOT saved: partly
    /// hit (not yet depleted) nodes, items lying on the ground, creatures.
    /// </summary>
    [Serializable]
    public class SaveGameData
    {
        /// <summary>SaveSystem.FormatVersion this file was written with. 0 = a save from before
        /// versioning existed (2026-10-21) - SaveSystem.Migrate upgrades it on read.</summary>
        public int FormatVersion;

        /// <summary>SaveSystem.WorldGenerationVersion at the time of saving. When it differs from
        /// the current one, the terrain regenerated from WorldSeed is not the terrain this save
        /// was made on (see SaveSystem.IsWorldGenerationOutdated).</summary>
        public int WorldGenerationVersion;

        public string SaveId;
        public string WorldName;
        public long SavedAtUnixSeconds;
        public int WorldSeed;

        /// <summary>WorldClock day (1+) and hour (0..24). Day 0 = a save made before the
        /// day/night cycle existed - the clock keeps its new-game start time.</summary>
        public int Day;
        public float Hour;

        /// <summary>WeatherDefinition.Id and seconds left before the next weather roll. Empty
        /// for saves from before weather existed - the initial weather is kept.</summary>
        public string WeatherId;
        public float WeatherRemainingSeconds;

        public PlayerSaveData Player = new();
        public List<BuildingSaveData> Buildings = new();

        /// <summary>Spawn-index ids of resource nodes gathered until depleted (see
        /// WorldGenerator.GetDepletedResourceIds) - skipped when the world is regenerated on
        /// load. Like the terrain itself, only valid while generation settings are unchanged.</summary>
        public List<int> DepletedResourceIds = new();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float CurrentHealth;
        public List<InventorySlotSaveData> InventorySlots = new();
        public List<ActiveFoodSaveData> ActiveFoods = new();

        /// <summary>ItemDefinition.Id held in the main hand (PlayerEquipment), or empty. Restored
        /// after the inventory, and only if that item is actually in it.</summary>
        public string MainHandItemId;
    }

    /// <summary>One active food (PlayerFood) and its remaining seconds. Restored before
    /// CurrentHealth so the food's max-health bonus exists when health is clamped on load.</summary>
    [Serializable]
    public class ActiveFoodSaveData
    {
        public string ItemId;
        public float RemainingSeconds;
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

        /// <summary>Occupied slots of the piece's StorageContainer (a chest); empty for every
        /// other piece and for saves from before chests existed.</summary>
        public List<InventorySlotSaveData> ContainerSlots = new();
    }
}
