using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Demo;
using NAV.Gameplay.Items;
using NAV.World.Biomes;
using NAV.World.Encounters;
using NAV.World.Generation.Hybrid;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Demo > Validate Demo Progression (developer 2026-11-12: "check that the crafting
    /// chain still works with the far biomes switched off"). Walks the game's data to a fixed
    /// point, twice - full game and demo rules (DemoConfig, whatever its On/Off) - starting
    /// from bare hands: what spawns (biome nodes and decor, with what each needs as a tool;
    /// creatures and their loot; the Leshy through ForestWrath; POI prefabs), what the hammer
    /// can build (cost, a workbench nearby, station levels from upgrade pieces, a roof for
    /// stations that need one), what the stations craft and smelt. Reports whether every
    /// finale item (the copper armour) can be made, the chain that makes it, and the content
    /// the demo shows but can never be reached. Reads data only - nothing in the world is run.
    /// </summary>
    internal static class DemoProgressionValidator
    {
        private const string ConfigPath = "Assets/NAV/Resources/DemoConfig.asset";

        private sealed class Source
        {
            public string Name;
            public readonly List<(ToolType Tool, int Tier)> Needs = new();
            public readonly List<ItemDefinition> Gives = new();
        }

        private sealed class Rules
        {
            public bool Demo;
            public DemoConfig Config;

            public bool Locked(ItemDefinition item) => Demo && item != null && Config.LockedItems.Contains(item);
            public bool LockedBiome(BiomeDefinition biome) => Demo && biome != null && Config.LockedBiomes.Contains(biome);

            public int Cap(WorkbenchDefinition station)
            {
                if (!Demo || station == null)
                {
                    return 0;
                }

                DemoConfig.StationCap cap = Config.StationCaps.FirstOrDefault(c => c != null && c.Station == station);
                return cap != null ? cap.MaxLevel : 0;
            }

            public bool Allows(RecipeDefinition recipe)
            {
                int cap = Cap(recipe.Station);
                return !(Demo && ((cap > 0 && recipe.RequiredWorkbenchTier > cap)
                    || Locked(recipe.OutputItem) || recipe.Ingredients.Any(i => Locked(i.Item))));
            }

            public bool Allows(BuildingPieceDefinition piece)
            {
                int cap = Cap(piece.RequiredStation);
                return !(Demo && (Config.LockedPieces.Contains(piece) || (cap > 0 && piece.RequiredStationLevel > cap)
                    || piece.Cost.Any(i => Locked(i.Item))));
            }

            public bool AllowsSpawn(BiomeDefinition biome, GameObject prefab) =>
                !Demo || (!LockedBiome(biome) && !DemoMode.Yields(prefab, Locked));

            public bool AllowsDecor(GameObject prefab) => !Demo || !DemoMode.Yields(prefab, Locked);
        }

        private sealed class Result
        {
            public readonly Dictionary<ItemDefinition, string> How = new();
            public readonly HashSet<BuildingPieceDefinition> Buildable = new();
            public readonly Dictionary<WorkbenchDefinition, int> Levels = new();
            public readonly Dictionary<WorkbenchDefinition, int> BuildLevels = new();
            public readonly Dictionary<ItemDefinition, RecipeDefinition> MadeBy = new();
            public readonly Dictionary<ItemDefinition, ItemDefinition[]> ProcessedFrom = new();
            public readonly Dictionary<ItemDefinition, List<(ToolType Tool, int Tier)>> GatheredWith = new();
            public bool Has(ItemDefinition item) => item != null && How.ContainsKey(item);
        }

        [MenuItem("NAV/Demo/Validate Demo Progression")]
        private static void Validate()
        {
            var config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("DemoProgressionValidator: no DemoConfig - run NAV > Demo > Build Demo Config first.");
                return;
            }

            Result full = Run(new Rules { Demo = false, Config = config });
            Result demo = Run(new Rules { Demo = true, Config = config });
            Debug.Log(Report(config, full, demo));
        }

        /// <summary>For execute_code / tests: the report as text.</summary>
        internal static string ReportText()
        {
            var config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
            if (config == null)
            {
                return "no DemoConfig";
            }

            return Report(config, Run(new Rules { Demo = false, Config = config }), Run(new Rules { Demo = true, Config = config }));
        }

        private static List<T> All<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToList();

        private static Result Run(Rules rules)
        {
            var sources = new List<Source>();
            foreach (BiomeDefinition biome in All<BiomeDefinition>())
            {
                foreach (BiomeResourceSpawn spawn in biome.ResourceSpawns)
                {
                    if (spawn.Prefab != null && rules.AllowsSpawn(biome, spawn.Prefab))
                    {
                        AddSources(sources, spawn.Prefab, $"{biome.name}: {spawn.Prefab.name}", new List<(ToolType, int)>(), 0);
                    }
                }

                foreach (BiomeDecorSpawn spawn in biome.DecorSpawns)
                {
                    if (spawn.Prefab != null && rules.AllowsDecor(spawn.Prefab))
                    {
                        AddSources(sources, spawn.Prefab, $"{biome.name} decor: {spawn.Prefab.name}", new List<(ToolType, int)>(), 0);
                    }
                }

                foreach (BiomeCreatureSpawn spawn in biome.CreatureSpawns)
                {
                    if (spawn.Prefab != null && rules.AllowsSpawn(biome, spawn.Prefab))
                    {
                        AddSources(sources, spawn.Prefab, $"{biome.name} creature: {spawn.Prefab.name}", new List<(ToolType, int)>(), 0);
                    }
                }
            }

            foreach (ForestWrathSettings wrath in All<ForestWrathSettings>())
            {
                bool reachable = wrath.WrathBiomes.Concat(wrath.ChanceBiomes).Any(b => b != null && !rules.LockedBiome(b));
                if (wrath.BossPrefab != null && reachable)
                {
                    AddSources(sources, wrath.BossPrefab, $"boss: {wrath.BossPrefab.name}", new List<(ToolType, int)>(), 0);
                }
            }

            foreach (PoiDefinition poi in All<PoiDefinition>())
            {
                bool reachable = poi.AllowedBiomes.Count == 0 || poi.AllowedBiomes.Any(b => b != null && !rules.LockedBiome(b));
                if (poi.Prefab != null && reachable && (!rules.Demo || !DemoMode.Yields(poi.Prefab, rules.Locked)))
                {
                    AddSources(sources, poi.Prefab, $"POI: {poi.DisplayName}", new List<(ToolType, int)>(), 0);
                }
            }

            List<RecipeDefinition> recipes = All<RecipeDefinition>().Where(r => r.OutputItem != null && rules.Allows(r)).ToList();
            List<BuildingPieceDefinition> pieces = All<BuildingPieceDefinition>().Where(p => p.Prefab != null && rules.Allows(p)).ToList();

            var result = new Result();
            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 100)
            {
                changed = false;

                foreach (Source source in sources)
                {
                    if (source.Needs.All(n => HasTool(result, n.Tool, n.Tier)))
                    {
                        foreach (ItemDefinition item in source.Gives)
                        {
                            if (Give(result, item, source.Name + Needs(source)))
                            {
                                result.GatheredWith[item] = source.Needs;
                                changed = true;
                            }
                        }
                    }
                }

                bool hammer = result.How.Keys.Any(i => i.ToolType == ToolType.Hammer);
                foreach (BuildingPieceDefinition piece in pieces)
                {
                    if (!result.Buildable.Contains(piece) && hammer
                        && piece.Cost.All(c => result.Has(c.Item))
                        && (!piece.RequiresWorkbench || BuildLevel(result, piece.RequiredStation) >= Mathf.Max(1, piece.RequiredStationLevel)))
                    {
                        result.Buildable.Add(piece);
                        changed = true;
                    }
                }

                changed |= UpdateLevels(result, pieces, rules);

                foreach (RecipeDefinition recipe in recipes)
                {
                    bool station = recipe.RequiredWorkbenchTier <= 0 || StationLevel(result, recipe.Station) >= recipe.RequiredWorkbenchTier;
                    if (station && recipe.Ingredients.All(i => result.Has(i.Item)) && !result.Has(recipe.OutputItem))
                    {
                        result.MadeBy[recipe.OutputItem] = recipe;
                        changed |= Give(result, recipe.OutputItem, $"craft '{recipe.DisplayName}'" + (recipe.RequiredWorkbenchTier > 0 ? $" at {Name(recipe.Station)} {recipe.RequiredWorkbenchTier}" : " by hand"));
                    }
                }

                foreach (BuildingPieceDefinition piece in result.Buildable.ToList())
                {
                    // The campfire's spit (cooking, bone glue) - no fuel, no demo filter.
                    foreach (NAV.Gameplay.Survival.CookingStation cooking in piece.Prefab.GetComponentsInChildren<NAV.Gameplay.Survival.CookingStation>(true))
                    {
                        var list = (List<NAV.Gameplay.Survival.CookingRecipe>)typeof(NAV.Gameplay.Survival.CookingStation).GetField("_recipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cooking);
                        foreach (NAV.Gameplay.Survival.CookingRecipe recipe in list)
                        {
                            if (recipe.Input != null && recipe.Output != null && result.Has(recipe.Input)
                                && Give(result, recipe.Output, $"{piece.DisplayName}: {recipe.Input.DisplayName} -> {recipe.Output.DisplayName}"))
                            {
                                result.ProcessedFrom[recipe.Output] = new[] { recipe.Input };
                                changed = true;
                            }
                        }
                    }

                    foreach (ProcessingStation station in piece.Prefab.GetComponentsInChildren<ProcessingStation>(true))
                    {
                        var fuel = (ItemDefinition)typeof(ProcessingStation).GetField("_fuel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(station);
                        var list = (List<ProcessingRecipe>)typeof(ProcessingStation).GetField("_recipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(station);
                        foreach (ProcessingRecipe recipe in list)
                        {
                            bool allowed = !rules.Demo || (!rules.Locked(recipe.Input) && !rules.Locked(recipe.Output));
                            if (allowed && recipe.Input != null && recipe.Output != null && result.Has(recipe.Input) && (fuel == null || result.Has(fuel))
                                && Give(result, recipe.Output, $"{piece.DisplayName}: {recipe.Input.DisplayName} -> {recipe.Output.DisplayName}" + (fuel != null ? $" (fuel {fuel.DisplayName})" : "")))
                            {
                                result.ProcessedFrom[recipe.Output] = fuel != null ? new[] { recipe.Input, fuel } : new[] { recipe.Input };
                                changed = true;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static void AddSources(List<Source> sources, GameObject prefab, string name, List<(ToolType, int)> needs, int depth)
        {
            if (prefab == null || depth > 4)
            {
                return;
            }

            foreach (ResourceNode node in prefab.GetComponentsInChildren<ResourceNode>(true))
            {
                ResourceNodeDefinition definition = node.Definition;
                if (definition == null)
                {
                    continue;
                }

                var own = new List<(ToolType, int)>(needs);
                if (definition.RequiredTool != ToolType.None)
                {
                    own.Add((definition.RequiredTool, definition.MinToolTier));
                }

                var source = new Source { Name = depth == 0 ? name : $"{name} -> {definition.DisplayName}" };
                source.Needs.AddRange(own);
                source.Gives.AddRange(definition.Drops.Where(d => d != null && d.Item != null).Select(d => d.Item));
                sources.Add(source);
                AddSources(sources, definition.BecomesPrefab, source.Name, own, depth + 1);
                AddSources(sources, definition.LeavesBehindPrefab, source.Name, own, depth + 1);
            }

            foreach (ItemPickup pickup in prefab.GetComponentsInChildren<ItemPickup>(true))
            {
                if (pickup.Definition != null)
                {
                    var source = new Source { Name = $"{name} (pick up)" };
                    source.Needs.AddRange(needs);
                    source.Gives.Add(pickup.Definition);
                    sources.Add(source);
                }
            }

            foreach (Creature creature in prefab.GetComponentsInChildren<Creature>(true))
            {
                if (creature.Definition != null)
                {
                    var source = new Source { Name = $"{name} (loot)" };
                    source.Gives.AddRange(creature.Definition.LootDrops.Where(d => d != null && d.Item != null).Select(d => d.Item));
                    sources.Add(source);
                }
            }
        }

        private static string Needs(Source source) =>
            source.Needs.Count == 0 ? "" : " [needs " + string.Join(", ", source.Needs.Select(n => $"{n.Tool} {n.Tier}")) + "]";

        private static bool Give(Result result, ItemDefinition item, string how)
        {
            if (item == null || result.How.ContainsKey(item))
            {
                return false;
            }

            result.How[item] = how;
            return true;
        }

        private static bool HasTool(Result result, ToolType tool, int tier) =>
            tool == ToolType.None || result.How.Keys.Any(i => i.ToolType == tool && i.ToolTier >= tier);

        /// <summary>The level crafting can use (a station that needs a roof counts only once a
        /// roof can be built over it).</summary>
        private static int StationLevel(Result result, WorkbenchDefinition station) => Level(result.Levels, station);

        /// <summary>The level for placing pieces - building needs no roof (Workbench.Tier).</summary>
        private static int BuildLevel(Result result, WorkbenchDefinition station) => Level(result.BuildLevels, station);

        private static int Level(Dictionary<WorkbenchDefinition, int> levels, WorkbenchDefinition station)
        {
            if (station == null)
            {
                return levels.Count == 0 ? 0 : levels.Values.Max();
            }

            return levels.TryGetValue(station, out int level) ? level : 0;
        }

        private static bool UpdateLevels(Result result, List<BuildingPieceDefinition> pieces, Rules rules)
        {
            bool changed = false;
            bool roof = result.Buildable.Any(p => p.name.Contains("Roof") || p.name.Contains("Floor"));
            foreach (BuildingPieceDefinition piece in result.Buildable)
            {
                Workbench bench = piece.Prefab.GetComponentInChildren<Workbench>(true);
                if (bench == null || bench.Definition == null)
                {
                    continue;
                }

                WorkbenchDefinition station = bench.Definition;
                var ids = new HashSet<string>();
                foreach (BuildingPieceDefinition other in result.Buildable)
                {
                    foreach (WorkbenchExtension extension in other.Prefab.GetComponentsInChildren<WorkbenchExtension>(true))
                    {
                        if (extension.Upgrades == station)
                        {
                            ids.Add(extension.ExtensionId);
                        }
                    }
                }

                int level = station.Tier + ids.Count;
                int cap = rules.Cap(station);
                if (cap > 0)
                {
                    level = Mathf.Min(level, cap);
                }

                if (!result.BuildLevels.TryGetValue(station, out int knownBuild) || level > knownBuild)
                {
                    result.BuildLevels[station] = level;
                    changed = true;
                }

                if ((!station.RequiresRoof || roof) && (!result.Levels.TryGetValue(station, out int known) || level > known))
                {
                    result.Levels[station] = level;
                    changed = true;
                }
            }

            return changed;
        }

        private static string Name(WorkbenchDefinition station) => station != null ? station.DisplayName : "any station";

        private static string Report(DemoConfig config, Result full, Result demo)
        {
            var text = new StringBuilder();
            bool ok = config.FinaleItems.All(demo.Has);
            text.AppendLine(ok
                ? "DemoProgressionValidator: OK - the demo's finale items can be made with the far biomes switched off."
                : "DemoProgressionValidator: BROKEN - a finale item can't be made in the demo.");

            text.AppendLine($"Stations reached - full: {Levels(full)}; demo: {Levels(demo)}");
            text.AppendLine($"Items reachable - full: {full.How.Count}, demo: {demo.How.Count}; pieces buildable - full: {full.Buildable.Count}, demo: {demo.Buildable.Count}");

            foreach (ItemDefinition finale in config.FinaleItems)
            {
                text.AppendLine();
                text.AppendLine($"Chain for {finale.DisplayName} (demo):");
                Chain(text, demo, finale, 1, new HashSet<ItemDefinition>());
            }

            var rules = new Rules { Demo = true, Config = config };
            List<string> deadRecipes = All<RecipeDefinition>()
                .Where(r => r.OutputItem != null && rules.Allows(r) && !demo.Has(r.OutputItem))
                .Select(r => r.DisplayName + " (missing: " + string.Join(", ", r.Ingredients.Where(i => !demo.Has(i.Item)).Select(i => i.Item != null ? i.Item.DisplayName : "?"))
                    + (r.RequiredWorkbenchTier > 0 && StationLevel(demo, r.Station) < r.RequiredWorkbenchTier ? $"; {Name(r.Station)} {r.RequiredWorkbenchTier}" : "") + ")")
                .ToList();
            List<string> deadPieces = All<BuildingPieceDefinition>()
                .Where(p => p.Prefab != null && rules.Allows(p) && !demo.Buildable.Contains(p))
                .Select(p => p.DisplayName).ToList();
            text.AppendLine();
            text.AppendLine($"Shown in the demo but never reachable - recipes ({deadRecipes.Count}): {(deadRecipes.Count == 0 ? "none" : string.Join("; ", deadRecipes))}");
            text.AppendLine($"Shown in the demo but never buildable - pieces ({deadPieces.Count}): {(deadPieces.Count == 0 ? "none" : string.Join(", ", deadPieces))}");

            // Items the full game reaches but the demo doesn't, although the demo still uses them
            // somewhere (a shown recipe or piece) - the far biomes took their only source.
            var used = new HashSet<ItemDefinition>(All<RecipeDefinition>().Where(r => r.OutputItem != null && rules.Allows(r)).SelectMany(r => r.Ingredients.Select(i => i.Item))
                .Concat(All<BuildingPieceDefinition>().Where(rules.Allows).SelectMany(p => p.Cost.Select(c => c.Item))));
            List<string> lostItems = full.How.Keys.Where(i => !demo.Has(i) && used.Contains(i)).Select(i => i.DisplayName).ToList();
            text.Append($"Still used in the demo but lost with the far biomes ({lostItems.Count}): {(lostItems.Count == 0 ? "none" : string.Join(", ", lostItems))}");
            return text.ToString();
        }

        private static string Levels(Result result) =>
            result.Levels.Count == 0 ? "none" : string.Join(", ", result.Levels.Select(l => $"{l.Key.DisplayName} {l.Value}"));

        private static void Chain(StringBuilder text, Result result, ItemDefinition item, int depth, HashSet<ItemDefinition> seen)
        {
            string indent = new string(' ', depth * 2);
            if (!result.Has(item))
            {
                text.AppendLine($"{indent}- {item.DisplayName}: NOT REACHABLE");
                return;
            }

            if (!seen.Add(item))
            {
                text.AppendLine($"{indent}- {item.DisplayName}: (see above)");
                return;
            }

            text.AppendLine($"{indent}- {item.DisplayName}: {result.How[item]}");
            if (depth > 10)
            {
                return;
            }

            var next = new List<ItemDefinition>();
            if (result.MadeBy.TryGetValue(item, out RecipeDefinition recipe))
            {
                next.AddRange(recipe.Ingredients.Select(i => i.Item));
            }
            else if (result.ProcessedFrom.TryGetValue(item, out ItemDefinition[] inputs))
            {
                next.AddRange(inputs);
            }
            else if (result.GatheredWith.TryGetValue(item, out List<(ToolType Tool, int Tier)> needs))
            {
                // The tool that first made the gathering possible.
                foreach ((ToolType tool, int tier) in needs)
                {
                    ItemDefinition first = result.How.Keys.FirstOrDefault(i => i.ToolType == tool && i.ToolTier >= tier);
                    if (first != null)
                    {
                        next.Add(first);
                    }
                }
            }

            foreach (ItemDefinition input in next)
            {
                if (input != null)
                {
                    Chain(text, result, input, depth + 1, seen);
                }
            }
        }
    }
}
