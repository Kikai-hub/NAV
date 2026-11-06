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

        /// <summary>The forest's wrath (ForestWrath), how many times the Leshy was killed and the
        /// seconds before he can come again. 0 in saves from before 2026-11-06.</summary>
        public float ForestWrath;
        public int LeshyDefeats;
        public float LeshyCooldownSeconds;
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

        /// <summary>ItemDefinition.Ids of the worn armour (PlayerEquipment), restored the same way.</summary>
        public List<string> WornArmorItemIds = new();

        /// <summary>Recipe discovery (PlayerCrafting): every item id the player has had, and
        /// the highest level reached at each station kind. Empty in older saves - recipes are
        /// then rediscovered from the inventory and the next station use.</summary>
        public List<string> DiscoveredItemIds = new();
        public List<StationLevelSaveData> StationLevels = new();
    }

    /// <summary>Highest level the player has reached at a station kind (WorkbenchDefinition asset name).</summary>
    [Serializable]
    public class StationLevelSaveData
    {
        public string StationId;
        public int Level;
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

        /// <summary>Occupied slots of the piece's IStoredContents (a chest, a processing
        /// station's loaded inputs and fuel); empty for every
        /// other piece and for saves from before chests existed.</summary>
        public List<InventorySlotSaveData> ContainerSlots = new();
    }
}
