using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;

namespace NAV.Core.Save
{
    /// <summary>
    /// Hand-maintained list of every BuildingPieceDefinition that can end up in a save file - the
    /// Building equivalent of ItemDatabase, same reasoning (resolves a BuildingSaveData.PieceId
    /// string back to a real asset on load).
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingPieceDatabase", menuName = "NAV/Core/Save/Building Piece Database")]
    public class BuildingPieceDatabase : ScriptableObject
    {
        [SerializeField] private List<BuildingPieceDefinition> _pieces = new();

        private Dictionary<string, BuildingPieceDefinition> _lookup;

        public BuildingPieceDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_lookup == null)
            {
                BuildLookup();
            }

            if (_lookup.TryGetValue(id, out BuildingPieceDefinition definition))
            {
                return definition;
            }

            Debug.LogError($"{nameof(BuildingPieceDatabase)}: no BuildingPieceDefinition with id '{id}' - add it to this database's Pieces list.", this);
            return null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, BuildingPieceDefinition>();
            foreach (BuildingPieceDefinition piece in _pieces)
            {
                if (piece != null && !string.IsNullOrEmpty(piece.Id))
                {
                    _lookup[piece.Id] = piece;
                }
            }
        }
    }
}
