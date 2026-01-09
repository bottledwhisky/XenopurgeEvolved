using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.GameFlow;
using SpaceCommander.Progression;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SpaceCommander.Enumerations;

namespace XenopurgeEvolved
{
    // ### 分裂 (Splitting)
    // - Spawns 2 Scouts at 50% max HP instead of one
    // - **Speed stacking**: +1 speed per visible Scout
    // - Creates exponential swarm pressure

    public class ScoutSplitting : Evolution
    {
        public static float extraScoutHealthFactor = 0.5f; // 50% health for spawned scouts
        public static float extraScoutSpeedFactor = 1.0f; // 1 speed for each visible scout
        public ScoutSplitting()
        {
            unitTag = "Scout";
            name = "scout_splitting_name";
            description = "scout_splitting_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, (int)(extraScoutHealthFactor * 100), (int)extraScoutSpeedFactor);
        }
    }

    [HarmonyPatch(typeof(TestGame))]
    [HarmonyPatch("PlaceUnits")]
    public static class TestGame_Patch
    {
        public static TestGame instance = null;
        [HarmonyPrefix]
        public static void Prefix(TestGame __instance)
        {
            instance = __instance;
        }
    }

    [HarmonyPatch(typeof(LevelProgressionDataSO))]
    [HarmonyPatch("GetEnemySpawnData")]
    public class LevelProgressionDataSO_Patch
    {
        public static UnitDataSO scoutUnitDataSo = null;
        public static System.Reflection.FieldInfo _unitTagField = AccessTools.Field(typeof(UnitDataSO), "_unitTag");
        [HarmonyPostfix]
        public static void Postfix(int levelIndex, ref IEnumerable<KeyValuePair<UnitDataSO, int>> __result)
        {
            if (scoutUnitDataSo == null)
            {
                foreach (var unitDataSo in __result)
                {
                    if ((UnitTag)_unitTagField.GetValue(unitDataSo.Key) == UnitTag.Scout)
                    {
                        scoutUnitDataSo = unitDataSo.Key;
                        break;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(UnitDataSO))]
    [HarmonyPatch("CreateUnitInstance")]
    public class UnitDataSO_Patch
    {
        private static System.Reflection.FieldInfo _unitTagField = AccessTools.Field(typeof(UnitDataSO), "_unitTag");

        [HarmonyPostfix]
        public static void Postfix(UnitDataSO __instance, ref UnitData __result)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }
            // Only apply to Scouts
            var unitTag = (UnitTag)_unitTagField.GetValue(__instance);
            if (unitTag == UnitTag.Scout)
            {
                __result.Health = Mathf.CeilToInt(__result.Health * ScoutSplitting.extraScoutHealthFactor);
                __result.CurrentHealth = __result.Health;
            }
        }
    }

    [HarmonyPatch(typeof(BattleUnit))]
    [HarmonyPatch("Speed", MethodType.Getter)]
    public class BattleUnit_Speed_Patch
    {
        // Cache for speed bonuses per scout
        private static Dictionary<BattleUnit, float> speedBonusCache = new Dictionary<BattleUnit, float>();

        public static void InvalidateCache()
        {
            speedBonusCache.Clear();
        }

        private static float CalculateSpeedBonus(BattleUnit scout)
        {
            // Count visible Scouts (excluding self)
            int visibleScoutsCount = 0;

            if (scout.LineOfSight != null && scout.LineOfSight.Tiles != null)
            {
                foreach (var tile in scout.LineOfSight.Tiles)
                {
                    if (tile?.CurrentStateOfTile?.UnitsOnTile != null)
                    {
                        foreach (var unit in tile.CurrentStateOfTile.UnitsOnTile)
                        {
                            // Count other Scouts on the same team
                            if (unit != scout &&
                                unit.UnitTag == UnitTag.Scout &&
                                unit.Team == scout.Team &&
                                unit.IsAlive)
                            {
                                visibleScoutsCount++;
                            }
                        }
                    }
                }
            }

            // Calculate speed bonus: +1 speed per visible Scout
            return visibleScoutsCount * ScoutSplitting.extraScoutSpeedFactor;
        }

        [HarmonyPostfix]
        public static void Postfix(BattleUnit __instance, ref float __result)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }
            // Only apply speed boost to Scouts
            if (__instance.UnitTag != UnitTag.Scout)
            {
                return;
            }

            // Check cache first
            if (!speedBonusCache.TryGetValue(__instance, out float speedBonus))
            {
                // Calculate and cache the bonus
                speedBonus = CalculateSpeedBonus(__instance);
                speedBonusCache[__instance] = speedBonus;
            }

            // Add speed bonus
            if (speedBonus > 0)
            {
                __result += speedBonus;
            }
        }
    }

    [HarmonyPatch(typeof(MovementManager))]
    [HarmonyPatch("ChangePosition")]
    public class MovementManager_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleUnit battleUnit, bool canChangePosition)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }

            // Invalidate cache when any enemy AI unit moves
            if (canChangePosition && battleUnit?.Team == Team.EnemyAI)
            {
                BattleUnit_Speed_Patch.InvalidateCache();
            }
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public static class ScoutSplittingBattleUnitConstructorPatch
    {
        public static void Postfix(BattleUnit __instance, UnitData unitData, Enumerations.Team team, GridManager gridManager)
        {
            if (!Evolution.IsActivated<ScoutSacrifice>())
            {
                return;
            }
            __instance.OnDeath += () => __instance_OnDeath(__instance);
        }

        private static void __instance_OnDeath(BattleUnit __instance)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }

            // Invalidate cache when any enemy AI unit dies
            if (__instance?.Team == Team.EnemyAI && __instance.UnitTag == UnitTag.Scout)
            {
                BattleUnit_Speed_Patch.InvalidateCache();
            }
        }
    }

    [HarmonyPatch(typeof(UnitsPlacementPhase))]
    [HarmonyPatch("AddUnitsPhase")]
    public class UnitsPlacementPhase_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            GridManager gridManager,
            int playerSpawnRoomsCount,
            IEnumerable<BattleUnit> enemies,
            UnitsPlacementPhase __instance)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }

            if (LevelProgressionDataSO_Patch.scoutUnitDataSo == null)
            {
                MelonLogger.Warning("[ScoutSplitting] scoutUnitDataSo is null, cannot spawn scouts");
                return;
            }

            if (TestGame_Patch.instance == null)
            {
                MelonLogger.Warning("[ScoutSplitting] TestGame instance is null, cannot spawn scouts");
                return;
            }

            try
            {
                var _unitsPlacement = (UnitsPlacementRules)AccessTools.Field(typeof(UnitsPlacementPhase), "_unitsPlacement").GetValue(__instance);
                List<Tile> list2 = new List<Tile>();
                foreach (Room room in _unitsPlacement.EnemyPositionRooms)
                {
                    list2.AddRange(room.TilesOfRoom);
                }


                // Get CreateUnits method
                var createUnitsMethod = AccessTools.Method(typeof(TestGame), "CreateUnits");
                var gameManagerField = AccessTools.Field(typeof(TestGame), "_gameManager");
                var gridManagerField = AccessTools.Field(typeof(TestGame), "_gridManager");

                var gameManager = (GameManager)gameManagerField.GetValue(TestGame_Patch.instance);
                var testGridManager = (GridManager)gridManagerField.GetValue(TestGame_Patch.instance);


                // Find tiles with NEW scouts (not already processed)
                List<Tile> tilesToSpawnOn = new List<Tile>();
                foreach (var tile in list2)
                {
                    if (tile?.CurrentStateOfTile?.UnitsOnTile != null)
                    {
                        foreach (var unit in tile.CurrentStateOfTile.UnitsOnTile)
                        {
                            if (unit.UnitTag == UnitTag.Scout &&
                                unit.Team == Team.EnemyAI)
                            {
                                tilesToSpawnOn.Add(tile);
                                break; // Only process one scout per tile
                            }
                        }
                    }
                }


                // Create units data for CreateUnits method
                var scoutSpawnData = new List<KeyValuePair<UnitDataSO, int>>
                {
                    new KeyValuePair<UnitDataSO, int>(LevelProgressionDataSO_Patch.scoutUnitDataSo, tilesToSpawnOn.Count)
                };

                // Create the additional scouts using CreateUnits
                if (tilesToSpawnOn.Count > 0)
                {
                    createUnitsMethod.Invoke(TestGame_Patch.instance, new object[] {
                        scoutSpawnData,
                        testGridManager,
                        Team.EnemyAI
                    });

                    // Get the newly created units and place them on tiles
                    var teamManager = gameManager.GetTeamManager(Team.EnemyAI);
                    var allEnemies = teamManager.BattleUnits;

                    // Get the last N scouts (the ones we just created)
                    var newScouts = allEnemies
                        .Where(u => u.UnitTag == UnitTag.Scout && u.CurrentTile == null)
                        .Take(tilesToSpawnOn.Count)
                        .ToList();


                    // Place each new scout on the corresponding tile
                    for (int i = 0; i < Math.Min(newScouts.Count, tilesToSpawnOn.Count); i++)
                    {
                        var scout = newScouts[i];
                        var tile = tilesToSpawnOn[i];

                        scout.PlaceOnTile(tile);
                    }
                }

            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ScoutSplitting] Error in UnitsPlacementPhase_Patch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(SpawnEnemiesManager))]
    [HarmonyPatch("SpawnEnemies", MethodType.Normal)]
    public class SpawnEnemiesManager_Patch
    {
        // Track if we're currently spawning to prevent re-entry
        private static bool isSpawning = false;

        public class ExistingScouts {
            public List<BattleUnit> units = new List<BattleUnit>();
        }

        [HarmonyPrefix]
        public static void Prefix(SpawnEnemiesManager __instance, out ExistingScouts __state)
        {
            __state = null;

            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }

            // Get all spawn points
            var spawnPointsField = AccessTools.Field(typeof(SpawnEnemiesManager), "_spawnPoints");
            var spawnPoints = (List<EnemyUnitSpawner>)spawnPointsField.GetValue(__instance);

            if (spawnPoints == null)
            {
                MelonLogger.Warning("[ScoutSplitting] Spawn points is null");
                return;
            }


            // Record all existing Scouts on spawn tiles BEFORE spawning
            __state = new ExistingScouts();
            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint?.Tile?.CurrentStateOfTile?.UnitsOnTile != null)
                {
                    foreach (var unit in spawnPoint.Tile.CurrentStateOfTile.UnitsOnTile)
                    {
                        if (unit.UnitTag == UnitTag.Scout && unit.Team == Enumerations.Team.EnemyAI)
                        {
                            __state.units.Add(unit);
                        }
                    }
                }
            }

        }

        [HarmonyPostfix]
        public static void Postfix(SpawnEnemiesManager __instance, ExistingScouts __state)
        {
            if (!Evolution.IsActivated<ScoutSplitting>())
            {
                return;
            }

            if (__state == null)
            {
                return;
            }

            // Prevent re-entry
            if (isSpawning)
            {
                MelonLogger.Warning("[ScoutSplitting] Already spawning, preventing re-entry!");
                return;
            }

            try
            {
                isSpawning = true;

                // Get the SpawnUnitCallback delegate
                var spawnUnitCallbackField = AccessTools.Field(typeof(SpawnEnemiesManager), "SpawnUnitCallback");
                var spawnUnitCallback = (Action<UnitDataSO, Tile>)spawnUnitCallbackField.GetValue(__instance);

                if (spawnUnitCallback == null)
                {
                    MelonLogger.Warning("[ScoutSplitting] SpawnUnitCallback is null");
                    return;
                }

                if (LevelProgressionDataSO_Patch.scoutUnitDataSo == null)
                {
                    MelonLogger.Warning("[ScoutSplitting] scoutUnitDataSo is null");
                    return;
                }

                // Get all spawn points
                var spawnPointsField = AccessTools.Field(typeof(SpawnEnemiesManager), "_spawnPoints");
                var spawnPoints = (List<EnemyUnitSpawner>)spawnPointsField.GetValue(__instance);

                if (spawnPoints == null)
                {
                    MelonLogger.Warning("[ScoutSplitting] Spawn points is null in postfix");
                    return;
                }

                // Track tiles where NEW scouts were spawned (exclude pre-existing ones)
                List<Tile> newScoutSpawnTiles = new List<Tile>();

                // Check each spawn point for newly spawned scouts
                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint?.Tile?.CurrentStateOfTile?.UnitsOnTile != null)
                    {
                        foreach (var unit in spawnPoint.Tile.CurrentStateOfTile.UnitsOnTile)
                        {
                            if (__state.units.Contains(unit))
                            {
                                continue;
                            }
                            // Only count scouts that WEREN'T in the existing set
                            if (unit.UnitTag == UnitTag.Scout &&
                                unit.Team == Enumerations.Team.EnemyAI)
                            {
                                newScoutSpawnTiles.Add(spawnPoint.Tile);
                                break; // Only spawn one additional scout per tile
                            }
                        }
                    }
                }


                // Spawn an additional Scout on each tile that had a NEW Scout spawned
                foreach (var tile in newScoutSpawnTiles)
                {
                    spawnUnitCallback(LevelProgressionDataSO_Patch.scoutUnitDataSo, tile);
                }

                // Invalidate speed cache after spawning new scouts
                if (newScoutSpawnTiles.Count > 0)
                {
                    BattleUnit_Speed_Patch.InvalidateCache();
                }

            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
            finally
            {
                isSpawning = false;
            }
        }
    }
}