using System.Collections.Generic;
using UnityEngine;
using NAV.World.Biomes;

namespace NAV.World.Encounters
{
    /// <summary>
    /// Tuning for ForestWrath - how felling trees angers the forest and when its master, the
    /// Leshy, comes for the player (developer's choice 2026-11-06: "wrath of the forest + chance").
    /// </summary>
    [CreateAssetMenu(fileName = "ForestWrathSettings", menuName = "NAV/World/Forest Wrath Settings")]
    public class ForestWrathSettings : ScriptableObject
    {
        [Header("Boss")]
        [SerializeField] private GameObject _bossPrefab;

        [Header("Wrath")]
        [Tooltip("Felling a tree in one of these biomes angers the forest.")]
        [SerializeField] private List<BiomeDefinition> _wrathBiomes = new();
        [SerializeField, Min(0f)] private float _wrathPerTree = 1f;
        [SerializeField, Min(1f)] private float _maxWrath = 30f;
        [Tooltip("At this much wrath he comes for sure (in any of the wrath biomes).")]
        [SerializeField, Min(1f)] private float _guaranteedWrath = 24f;
        [Tooltip("Wrath forgotten per real minute without felling.")]
        [SerializeField, Min(0f)] private float _decayPerMinute = 0.15f;

        [Header("Chance")]
        [Tooltip("He may also come by chance - only in these biomes (the deep forest).")]
        [SerializeField] private List<BiomeDefinition> _chanceBiomes = new();
        [SerializeField, Min(1f)] private float _checkIntervalSeconds = 30f;
        [Tooltip("Chance per check in the deep forest with no wrath at all.")]
        [SerializeField, Range(0f, 1f)] private float _baseChance = 0.004f;
        [Tooltip("Extra chance per check just below the guaranteed wrath (scales with wrath).")]
        [SerializeField, Range(0f, 1f)] private float _wrathChance = 0.08f;
        [SerializeField, Min(1f)] private float _nightChanceMultiplier = 2f;

        [Header("Peaceful Leshy")]
        [Tooltip("Below this share of the guaranteed wrath the forest isn't angry: he comes peaceful - wanders and only fights when provoked (hit, or trees felled before his eyes).")]
        [SerializeField, Range(0f, 1f)] private float _angryWrathFraction = 0.35f;
        [Tooltip("Felling a tree this close (m) to a peaceful Leshy provokes him.")]
        [SerializeField, Min(0f)] private float _provokeTreeDistance = 30f;
        [Tooltip("Seconds a peaceful Leshy stays before he wanders back into the thicket (once out of the player's way).")]
        [SerializeField, Min(10f)] private float _peacefulStaySeconds = 240f;
        [Tooltip("He leaves only when at least this far (m) from the player - not before their eyes.")]
        [SerializeField, Min(0f)] private float _peacefulLeaveDistance = 35f;

        [Header("Encounter")]
        [Tooltip("Seconds of omens (the forest falls silent) before he steps out.")]
        [SerializeField, Min(0f)] private float _omenSeconds = 15f;
        [SerializeField] private Vector2 _spawnDistance = new(26f, 40f);
        [Tooltip("Farther than this from the player, a Leshy that isn't fighting goes back into the forest.")]
        [SerializeField, Min(30f)] private float _leaveDistance = 110f;
        [Tooltip("Real minutes before he can come again after being killed.")]
        [SerializeField, Min(0f)] private float _cooldownAfterDefeatMinutes = 30f;
        [Tooltip("Real minutes before he can come again after the player got away.")]
        [SerializeField, Min(0f)] private float _cooldownAfterEscapeMinutes = 8f;
        [Tooltip("Share of the wrath kept when the player got away.")]
        [SerializeField, Range(0f, 1f)] private float _wrathKeptAfterEscape = 0.6f;

        [Header("Messages")]
        [Tooltip("Shown once when the wrath first rises past each share of the guaranteed wrath (0..1), in order.")]
        [SerializeField] private List<WrathOmen> _wrathOmens = new()
        {
            new WrathOmen { Fraction = 0.35f, Text = "The forest is watching you." },
            new WrathOmen { Fraction = 0.7f, Text = "Branches creak behind your back. The forest is angry." },
        };
        [SerializeField] private string _arrivalOmen = "The forest has fallen silent...";
        [SerializeField] private string _arrivalText = "The Leshy has come for you!";
        [SerializeField] private string _defeatText = "The Leshy is dead. The forest grows quiet.";
        [SerializeField] private string _escapeText = "The Leshy has gone back into the thicket.";
        [SerializeField] private string _peacefulOmen = "Old wood creaks somewhere among the trees...";
        [SerializeField] private string _peacefulArrivalText = "The master of the forest is walking nearby. Leave him be.";
        [SerializeField] private string _provokedText = "You have angered the Leshy!";

        public GameObject BossPrefab => _bossPrefab;
        public IReadOnlyList<BiomeDefinition> WrathBiomes => _wrathBiomes;
        public IReadOnlyList<BiomeDefinition> ChanceBiomes => _chanceBiomes;
        public float WrathPerTree => _wrathPerTree;
        public float MaxWrath => _maxWrath;
        public float GuaranteedWrath => Mathf.Min(_guaranteedWrath, _maxWrath);
        public float DecayPerMinute => _decayPerMinute;
        public float CheckIntervalSeconds => _checkIntervalSeconds;
        public float BaseChance => _baseChance;
        public float WrathChance => _wrathChance;
        public float NightChanceMultiplier => _nightChanceMultiplier;
        public float OmenSeconds => _omenSeconds;
        public Vector2 SpawnDistance => _spawnDistance;
        public float LeaveDistance => _leaveDistance;
        public float CooldownAfterDefeatSeconds => _cooldownAfterDefeatMinutes * 60f;
        public float CooldownAfterEscapeSeconds => _cooldownAfterEscapeMinutes * 60f;
        public float WrathKeptAfterEscape => _wrathKeptAfterEscape;
        public IReadOnlyList<WrathOmen> WrathOmens => _wrathOmens;
        public string ArrivalOmen => _arrivalOmen;
        public string ArrivalText => _arrivalText;
        public string DefeatText => _defeatText;
        public string EscapeText => _escapeText;
        public float AngryWrathFraction => _angryWrathFraction;
        public float ProvokeTreeDistance => _provokeTreeDistance;
        public float PeacefulStaySeconds => _peacefulStaySeconds;
        public float PeacefulLeaveDistance => _peacefulLeaveDistance;
        public string PeacefulOmen => _peacefulOmen;
        public string PeacefulArrivalText => _peacefulArrivalText;
        public string ProvokedText => _provokedText;

        private void OnValidate()
        {
            _spawnDistance.x = Mathf.Max(5f, _spawnDistance.x);
            _spawnDistance.y = Mathf.Max(_spawnDistance.x, _spawnDistance.y);
        }
    }

    [System.Serializable]
    public class WrathOmen
    {
        [Range(0f, 1f)] public float Fraction;
        public string Text;
    }
}
