using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.GameFlow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XenopurgeEvolved
{
    public class MaulerIncubator : Evolution
    {
        // ### 孵化者 (Incubator)
        // - Spawns 1 Scout on death
        // - Can chain with Scout's 献祭 naturally (not designed combo)

        public MaulerIncubator()
        {
            unitTag = "Mauler";
            name = "mauler_incubator_name";
            description = "mauler_incubator_description";
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public static class MaulerIncubatorBattleUnitConstructorPatch
    {
        public static void Postfix(BattleUnit __instance, UnitData unitData, Enumerations.Team team, GridManager gridManager)
        {
            if (!Evolution.IsActivated<MaulerIncubator>())
            {
                return;
            }

            // Subscribe to death event
            __instance.OnDeath += () => OnMaulerDeath(__instance);
        }

        private static void OnMaulerDeath(BattleUnit __instance)
        {
            // Check if this is a Mauler unit
            if (__instance.UnitTag != Enumerations.UnitTag.Mauler)
            {
                return;
            }

            // Check if the unit was on the enemy team
            if (__instance.Team != Enumerations.Team.EnemyAI)
            {
                return;
            }

            Tile deathTile = __instance.CurrentTile;
            if (deathTile == null)
            {
                MelonLogger.Warning("[MaulerIncubator] Mauler died but has no current tile");
                return;
            }

            // Get Scout UnitDataSO
            if (ScoutUnitDataLoader.GetScoutUnitDataSO() == null)
            {
                MelonLogger.Warning("[MaulerIncubator] scoutUnitDataSo is null, cannot spawn Scout");
                return;
            }

            // Get SpawnEnemiesManager instance
            SpawnEnemiesManager spawnManager = null;

            try
            {
                if (TestGame_Patch.instance != null)
                {
                    var gameManagerField = AccessTools.Field(typeof(TestGame), "_gameManager");
                    var gameManager = (GameManager)gameManagerField.GetValue(TestGame_Patch.instance);

                    if (gameManager != null)
                    {
                        spawnManager = gameManager.EnemiesSpawnerInBattle;
                    }
                }

                if (spawnManager == null)
                {
                    MelonLogger.Warning("[MaulerIncubator] Could not get SpawnEnemiesManager instance");
                    return;
                }

                // Get the SpawnUnitCallback from SpawnEnemiesManager
                var spawnUnitCallbackField = AccessTools.Field(typeof(SpawnEnemiesManager), "SpawnUnitCallback");
                var spawnUnitCallback = (Action<UnitDataSO, Tile>)spawnUnitCallbackField.GetValue(spawnManager);

                if (spawnUnitCallback == null)
                {
                    MelonLogger.Warning("[MaulerIncubator] SpawnUnitCallback is null");
                    return;
                }

                // Spawn the Scout on the death tile
                spawnUnitCallback(ScoutUnitDataLoader.GetScoutUnitDataSO(), deathTile);

                MelonLogger.Msg($"[MaulerIncubator] Spawned Scout at {deathTile.Coords} after Mauler death");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[MaulerIncubator] Error spawning Scout: {e}");
            }
        }
    }
}
