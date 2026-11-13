using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.Gameplay.Survival;
using NAV.UI;
using NAV.World.Generation;

namespace NAV.Dev
{
    /// <summary>
    /// Automatic run through the game in a player build (STEAM_DEMO_PLAN.md stage 2, 2026-11-12).
    /// Does nothing unless the build is started with <c>-navSmokeTest</c>:
    /// <list type="bullet">
    /// <item><c>-navSmokeTest</c> - new world "SmokeTest" from the main menu; frame times standing
    /// still and while travelling 30 m/s (chunk streaming); craft by hand and at a roofed workbench,
    /// build, eat, die (gravestone), respawn, save; quits.</item>
    /// <item><c>-navSmokeTest -navSmokeLoad &lt;saveId&gt;</c> - loads that save from the main menu and
    /// checks the buildings, the gravestone and the inventory came back; frame times; quits.</item>
    /// </list>
    /// Everything is logged as lines starting with "SMOKE" (use <c>-logFile</c>). Fails are logged as
    /// "SMOKE FAIL" and the run goes on. Delete the "SmokeTest" save afterwards.
    /// </summary>
    public class BuildSmokeTest : MonoBehaviour
    {
        private const string WorldName = "SmokeTest";
        private const int Seed = 20261112;
        private const string GameScene = "WorldGenTest";

        private int _fails;

        // -navSmokeProfile <path>: a Development Build writes a Profiler capture (<path>.raw) from the
        // world load to the end of the travel run - open it in the Profiler window, or let
        // NAV > Dev > Analyze Smoke Profile list the slow frames.
        private static string _profilePath;

        private static bool Profiling => _profilePath != null;

        private static void StartProfile()
        {
            if (!Profiling)
            {
                return;
            }

            Profiler.maxUsedMemory = 512 * 1024 * 1024;
            Profiler.logFile = _profilePath;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            Log($"profiling to {_profilePath}.raw (supported: {Profiler.supported})");
        }

        private static void StopProfile()
        {
            if (!Profiling || !Profiler.enabled)
            {
                return;
            }

            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
            Log("profile written");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-navSmokeTest") || FindAnyObjectByType<BuildSmokeTest>() != null)
            {
                return;
            }

            int index = Array.IndexOf(args, "-navSmokeLoad");
            string loadId = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            int profile = Array.IndexOf(args, "-navSmokeProfile");
            _profilePath = profile >= 0 && profile + 1 < args.Length ? args[profile + 1] : null;
            var host = new GameObject(nameof(BuildSmokeTest));
            DontDestroyOnLoad(host);
            host.AddComponent<BuildSmokeTest>().StartCoroutine(host.GetComponent<BuildSmokeTest>().Run(loadId));
        }

        private static void Log(string message) => Debug.Log("SMOKE " + message);

        private void Fail(string message)
        {
            _fails++;
            Debug.Log("SMOKE FAIL " + message);
        }

        private IEnumerator Run(string loadId)
        {
            Application.runInBackground = true;
            Log($"start ({(loadId == null ? "new world" : "load " + loadId)}), Unity {Application.unityVersion}, {Application.productName} {Application.version}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}, {Screen.width}x{Screen.height}");

            // Main menu -> game scene.
            MainMenuUIController menu = null;
            for (float t = 0f; t < 30f && (menu = FindAnyObjectByType<MainMenuUIController>()) == null; t += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            if (menu == null)
            {
                Fail("no main menu");
                Quit();
                yield break;
            }

            yield return null;
            StartProfile();
            float loadStart = Time.realtimeSinceStartup;
            if (loadId == null)
            {
                menu.LaunchNewGame(WorldName, Seed);
            }
            else
            {
                SaveSystem.PendingLoadSaveId = loadId;
                SaveSystem.PendingNewGameName = null;
                typeof(MainMenuUIController).GetMethod("StartLoadingGameScene", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, null);
            }

            WorldGenerator world = null;
            PlayerMotor player = null;
            float deadline = Time.realtimeSinceStartup + 240f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == GameScene)
                {
                    world ??= FindAnyObjectByType<WorldGenerator>();
                    player ??= FindAnyObjectByType<PlayerMotor>();
                    if (world != null && player != null && world.HasCreatureWorld)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (world == null || player == null || !world.HasCreatureWorld)
            {
                Fail($"world not ready after 240 s (scene {SceneManager.GetActiveScene().name})");
                Quit();
                yield break;
            }

            Log($"world ready in {Time.realtimeSinceStartup - loadStart:0.0} s, save {SaveSystem.CurrentSaveId}, player at {player.transform.position}");
            yield return Measure("idle after load", Profiling ? 6f : 15f, null);

            if (loadId == null)
            {
                // A 300 m circle around the spawn at 30 m/s - stays on the starting land and
                // keeps the chunk/tile streaming busy.
                Vector3 home = player.transform.position;
                yield return Measure("travel 30 m/s", Profiling ? 20f : 40f, player);
                StopProfile();
                Teleport(player, home);
                yield return Measure("back home", 5f, null);
                yield return NewWorldChecks(player);
            }
            else
            {
                LoadChecks(player);
                yield return Measure("idle after checks", 10f, null);
            }

            StopProfile();
            Log($"done, {_fails} fail(s)");
            Quit();
        }

        // ---- Frame times ----

        private IEnumerator Measure(string label, float seconds, PlayerMotor traveller)
        {
            var frames = new List<float>(4096);
            var spikes = new List<string>();
            int gc0 = GC.CollectionCount(0);
            long mono0 = Profiler.GetMonoUsedSizeLong();
            Vector3 centre = traveller != null ? traveller.transform.position - Flat(traveller.transform.forward) * 300f : Vector3.zero;
            float angle = traveller != null ? Mathf.Atan2(traveller.transform.forward.z, traveller.transform.forward.x) : 0f;
            float nextHop = 0f;
            float start = Time.realtimeSinceStartup;

            // What the local NavMesh did on a spike frame: started a rebuild (main-thread source
            // collection) or finished one (the async result swapped in).
            var world = FindAnyObjectByType<WorldGenerator>();
            object nav = world != null ? typeof(WorldGenerator).GetField("_localNav", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(world) : null;
            Vector3 lastCentre = nav != null ? ((LocalNavMesh)nav).Centre : Vector3.zero;
            bool lastBuilding = nav != null && ((LocalNavMesh)nav).IsBuilding;
            string prevNavNote = "";
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                frames.Add(ms);
                string navNote = "";
                if (nav is LocalNavMesh local)
                {
                    bool started = local.Centre != lastCentre;
                    bool finished = lastBuilding && !local.IsBuilding && !started;
                    navNote = started ? $"(nav rebuild: collect {local.LastCollectMs:0}ms, {local.LastSourceCount} sources)" : finished ? "(nav swap-in)" : "";
                    lastCentre = local.Centre;
                    lastBuilding = local.IsBuilding;
                }

                if (world != null && world.LastStreamMs > 10f)
                {
                    navNote += $"(stream {world.LastStreamMs:0}ms)";
                }

                // A slow frame shows up in the next frame's delta, so the previous frame's note counts too.
                if (ms > 50f && spikes.Count < 12)
                {
                    spikes.Add($"{Time.realtimeSinceStartup - start:0.0}s:{ms:0}ms{prevNavNote}{navNote}");
                }

                prevNavNote = navNote;

                if (traveller != null && Time.realtimeSinceStartup >= nextHop)
                {
                    nextHop = Time.realtimeSinceStartup + 1f;
                    angle += 30f / 300f; // 30 m along a 300 m circle
                    Teleport(traveller, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 300f);
                }
            }

            frames.Sort();
            float P(float q) => frames[Mathf.Clamp(Mathf.FloorToInt(q * (frames.Count - 1)), 0, frames.Count - 1)];
            Log($"frames [{label}]: {frames.Count} in {seconds:0} s = {frames.Count / seconds:0} fps avg; p50 {P(0.5f):0.0} ms, p95 {P(0.95f):0.0}, p99 {P(0.99f):0.0}, max {frames[frames.Count - 1]:0.0}; "
                + $">33 ms: {frames.Count(f => f > 33.4f)}, >50 ms: {frames.Count(f => f > 50f)}, >100 ms: {frames.Count(f => f > 100f)}; "
                + $"GC gen0 +{GC.CollectionCount(0) - gc0}, mono {mono0 / 1048576}->{Profiler.GetMonoUsedSizeLong() / 1048576} MB, total {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB"
                + (spikes.Count > 0 ? "; spikes " + string.Join(" ", spikes) : ""));
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward;
        }

        private static void Teleport(PlayerMotor player, Vector3 target)
        {
            if (Physics.Raycast(new Vector3(target.x, 2000f, target.z), Vector3.down, out RaycastHit hit, 4000f, ~0, QueryTriggerInteraction.Ignore))
            {
                target.y = hit.point.y + 0.2f;
            }

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = target;
            controller.enabled = true;
        }

        // ---- Gameplay ----

        private IEnumerator NewWorldChecks(PlayerMotor player)
        {
            var save = FindAnyObjectByType<SaveManager>();
            var inventory = Get<PlayerInventory>(player);
            var crafting = Get<PlayerCrafting>(player);
            var food = Get<PlayerFood>(player);
            var health = Get<PlayerHealth>(player);
            var death = Get<PlayerDeath>(player);
            if (save == null || inventory == null || crafting == null || health == null || death == null)
            {
                Fail("player/save components missing");
                yield break;
            }

            var items = (ItemDatabase)typeof(SaveManager).GetField("_itemDatabase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(save);
            var pieces = (BuildingPieceDatabase)typeof(SaveManager).GetField("_buildingPieceDatabase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(save);

            // Materials (a whole first evening's gathering) and the Leshy's heartwood.
            foreach ((string id, int amount) in new[] { ("wood", 60), ("Rock", 20), ("Flint", 10), ("Resin", 5), ("LeshyHeartwood", 3), ("CookedMeat", 2) })
            {
                ItemDefinition item = items.Get(id);
                if (item == null)
                {
                    Fail($"item '{id}' not in the item database");
                    continue;
                }

                inventory.Inventory.AddItem(item, amount);
            }

            yield return null;
            Craft(crafting, "WoodHammer");

            // A workbench under a roof, the player beside it.
            Vector3 spot = player.transform.position + Flat(player.transform.forward) * 4f;
            BuildingPiece bench = Place(pieces, "WoodWorkbenchPiece", spot, player.transform.rotation);
            Place(pieces, "WoodRoofPiece", spot + Vector3.up * 2.6f, player.transform.rotation);
            // Walk up to it (a teleport into a trigger doesn't fire OnTriggerEnter; walking does).
            Vector3 toBench = Flat(spot - player.transform.position);
            Teleport(player, spot - toBench * 8f);
            var controller = player.GetComponent<CharacterController>();
            for (int i = 0; i < 90; i++)
            {
                if (i < 60)
                {
                    controller.Move(toBench * 0.1f + Vector3.down * 0.05f);
                }

                yield return null; // the shelter check and the trigger run on their own clock
            }

            var workbench = bench != null ? bench.GetComponentInChildren<Workbench>() : null;
            Log($"workbench: placed {bench != null}, sheltered {(workbench != null && workbench.IsSheltered)}, level {(workbench != null ? workbench.UsableTier : 0)}, player sees level {(workbench != null ? crafting.GetStationLevel(workbench.Definition) : 0)}");
            Craft(crafting, "StoneAxe");
            Craft(crafting, "StonePickaxe");

            if (food != null)
            {
                ItemDefinition meat = items.Get("CookedMeat");
                int slot = IndexOf(inventory.Inventory, meat);
                bool ate = slot >= 0 && food.TryEatFromSlot(slot);
                Log($"eat cooked meat: {(ate ? "ok" : "no")}, active foods {food.ActiveFoods.Count}");
                if (!ate)
                {
                    Fail("couldn't eat cooked meat");
                }
            }

            // Death: everything goes into a gravestone; respawn.
            int carried = inventory.Inventory.Slots.Count(s => !s.IsEmpty);
            health.TakeDamage(100000f);
            yield return null;
            int stones = Gravestone.All.Count;
            Log($"death: dead {death.IsDead}, gravestones {stones}, carried {carried} stacks, left in inventory {inventory.Inventory.Slots.Count(s => !s.IsEmpty)}");
            if (!death.IsDead || stones == 0)
            {
                Fail("death didn't leave a gravestone");
            }

            death.Respawn();
            yield return null;
            Log($"respawn: alive {health.IsAlive}, health {health.CurrentHealth:0}/{health.MaxHealth:0}, at {player.transform.position}");
            inventory.Inventory.AddItem(items.Get("Flint"), 1); // marker for the load run

            save.Autosave();
            Log($"saved {SaveSystem.CurrentSaveId}: {BuildingPiece.AllPieces.Count} pieces, {Gravestone.All.Count} gravestone(s). Next: -navSmokeTest -navSmokeLoad {SaveSystem.CurrentSaveId}");
        }

        /// <summary>The player's component, wherever on the player it sits.</summary>
        private static T Get<T>(PlayerMotor player) where T : Component
        {
            T found = player.GetComponentInParent<T>();
            if (found == null)
            {
                found = player.GetComponentInChildren<T>();
            }

            if (found == null)
            {
                found = FindAnyObjectByType<T>();
            }

            return found;
        }

        private void LoadChecks(PlayerMotor player)
        {
            var inventory = Get<PlayerInventory>(player);
            int pieces = BuildingPiece.AllPieces.Count;
            int stones = Gravestone.All.Count;
            int stoneStacks = stones > 0 ? Gravestone.All[0].Contents.Slots.Count(s => !s.IsEmpty) : 0;
            bool hasHammer = stones > 0 && Gravestone.All.Any(g => g.Contents.Slots.Any(s => !s.IsEmpty && s.Definition.Id == "WoodHammer"));
            bool marker = inventory != null && inventory.Inventory.Slots.Any(s => !s.IsEmpty && s.Definition.Id == "Flint");
            Log($"loaded: {pieces} pieces, {stones} gravestone(s) with {stoneStacks} stacks (hammer inside {hasHammer}), marker flint in inventory {marker}");
            if (pieces < 2)
            {
                Fail("buildings not restored");
            }

            if (stones == 0 || !hasHammer)
            {
                Fail("gravestone not restored");
            }

            if (!marker)
            {
                Fail("inventory not restored");
            }
        }

        private void Craft(PlayerCrafting crafting, string itemId)
        {
            RecipeDefinition recipe = crafting.KnownRecipes.FirstOrDefault(r => r != null && r.OutputItem != null && r.OutputItem.Id == itemId);
            if (recipe == null)
            {
                Fail($"no recipe for {itemId}");
                return;
            }

            bool ok = crafting.TryCraft(recipe);
            Log($"craft {itemId}: {(ok ? "ok" : "FAILED")} (needs {(recipe.Station != null ? recipe.Station.DisplayName : "-")} {recipe.RequiredWorkbenchTier}, have {crafting.GetStationLevel(recipe.Station)})");
            if (!ok)
            {
                Fail($"craft {itemId}");
            }
        }

        private BuildingPiece Place(BuildingPieceDatabase database, string assetName, Vector3 position, Quaternion rotation)
        {
            var list = (List<BuildingPieceDefinition>)typeof(BuildingPieceDatabase).GetField("_pieces", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
            BuildingPieceDefinition definition = list.FirstOrDefault(p => p != null && p.name == assetName);
            if (definition == null || definition.Prefab == null)
            {
                Fail($"piece {assetName} not in the database");
                return null;
            }

            if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore) && assetName.Contains("Workbench"))
            {
                position.y = hit.point.y;
            }

            GameObject instance = Instantiate(definition.Prefab, position, rotation);
            var piece = instance.GetComponent<BuildingPiece>();
            if (piece != null)
            {
                piece.Configure(definition);
            }

            Log($"placed {assetName} at {position}");
            return piece;
        }

        private static int IndexOf(NAV.Gameplay.Inventory.Inventory inventory, ItemDefinition item)
        {
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                if (!inventory.Slots[i].IsEmpty && inventory.Slots[i].Definition == item)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Quit()
        {
            Debug.Log("SMOKE quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
