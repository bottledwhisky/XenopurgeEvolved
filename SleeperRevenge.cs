using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;

namespace XenopurgeEvolved
{
    public class SleeperRevenge : Evolution
    {
        // ### 复仇 (Revenge)
        // - Gains +1 health for each other ally that dies
        // - Gains +1 melee damage for each 4 ally that dies
        // - Gains +1 speed for each 4 ally that dies

        public static int healthPerAllyDeath = 1;
        public static int allyDeathsPerMeleeBonus = 4;
        public static int allyDeathsPerSpeedBonus = 4;

        // Track ally deaths per Sleeper: UnitId -> death count
        public static Dictionary<string, int> sleeperAllyDeathCount = new Dictionary<string, int>();

        public SleeperRevenge()
        {
            unitTag = "Sleeper";
            name = "sleeper_revenge_name";
            description = "sleeper_revenge_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, healthPerAllyDeath, allyDeathsPerMeleeBonus, allyDeathsPerSpeedBonus);
        }

        public static void ResetTracking()
        {
            sleeperAllyDeathCount.Clear();
        }

        public static void CleanupUnit(string unitId)
        {
            sleeperAllyDeathCount.Remove(unitId);
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public static class SleeperRevengeBattleUnitConstructorPatch
    {
        private static readonly System.Reflection.FieldInfo _currentHealthField = AccessTools.Field(typeof(BattleUnit), "_currentHealth");
        private static readonly System.Reflection.FieldInfo _currentMaxHealthField = AccessTools.Field(typeof(BattleUnit), "_currentMaxHealth");

        public static void Postfix(BattleUnit __instance, GridManager gridManager)
        {
            if (!Evolution.IsActivated<SleeperRevenge>())
            {
                return;
            }

            // Subscribe to death event for all units
            __instance.OnDeath += () => __instance_OnDeath(__instance, gridManager);
        }

        private static void __instance_OnDeath(BattleUnit dyingUnit, GridManager gridManager)
        {
            // Only track enemy AI unit deaths
            if (dyingUnit.Team != Enumerations.Team.EnemyAI)
            {
                return;
            }

            // Clean up the dying unit's tracking
            SleeperRevenge.CleanupUnit(dyingUnit.UnitId);

            // Find all living Sleeper allies via TestGame patch
            if (TestGame_Patch.instance == null)
            {
                MelonLogger.Warning("[SleeperRevenge] TestGame instance is null");
                return;
            }

            try
            {
                var gameManagerField = AccessTools.Field(typeof(TestGame), "_gameManager");
                var gameManager = (GameManager)gameManagerField.GetValue(TestGame_Patch.instance);

                if (gameManager == null)
                {
                    MelonLogger.Warning("[SleeperRevenge] GameManager is null");
                    return;
                }

                var teamManager = gameManager.GetTeamManager(Enumerations.Team.EnemyAI);
                var allEnemies = teamManager.BattleUnits;

                var livingSleepers = allEnemies
                    .Where(u => u.IsAlive && u.UnitTag == Enumerations.UnitTag.Sleeper && u != dyingUnit)
                    .ToList();

                if (livingSleepers.Count == 0)
                {
                    return;
                }

                MelonLogger.Msg($"[SleeperRevenge] Ally died, buffing {livingSleepers.Count} Sleeper(s)");

                // Buff each living Sleeper
                foreach (var sleeper in livingSleepers)
                {
                    string unitId = sleeper.UnitId;

                    // Initialize death count if needed
                    if (!SleeperRevenge.sleeperAllyDeathCount.ContainsKey(unitId))
                    {
                        SleeperRevenge.sleeperAllyDeathCount[unitId] = 0;
                    }

                    int currentDeaths = SleeperRevenge.sleeperAllyDeathCount[unitId];
                    int newDeaths = currentDeaths + 1;
                    SleeperRevenge.sleeperAllyDeathCount[unitId] = newDeaths;

                    // Apply health buff (every death) - use AccessTools to modify private health fields
                    float currentHealth = (float)_currentHealthField.GetValue(sleeper);
                    float currentMaxHealth = (float)_currentMaxHealthField.GetValue(sleeper);

                    _currentHealthField.SetValue(sleeper, currentHealth + SleeperRevenge.healthPerAllyDeath);
                    _currentMaxHealthField.SetValue(sleeper, currentMaxHealth + SleeperRevenge.healthPerAllyDeath);

                    MelonLogger.Msg($"[SleeperRevenge] {sleeper.UnitName} gained +{SleeperRevenge.healthPerAllyDeath} health (total deaths: {newDeaths})");

                    // Apply melee damage buff (every 4 deaths)
                    if (newDeaths % SleeperRevenge.allyDeathsPerMeleeBonus == 0)
                    {
                        string powerGuid = "sleeper_revenge_power_" + unitId + "_" + newDeaths;
                        sleeper.ChangeStat(Enumerations.UnitStats.Power, 1f, powerGuid);
                        MelonLogger.Msg($"[SleeperRevenge] {sleeper.UnitName} gained +1 melee damage");
                    }

                    // Apply speed buff (every 4 deaths)
                    if (newDeaths % SleeperRevenge.allyDeathsPerSpeedBonus == 0)
                    {
                        string speedGuid = "sleeper_revenge_speed_" + unitId + "_" + newDeaths;
                        sleeper.ChangeStat(Enumerations.UnitStats.Speed, 1f, speedGuid);
                        MelonLogger.Msg($"[SleeperRevenge] {sleeper.UnitName} gained +1 speed");
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[SleeperRevenge] Error processing ally death: {e}");
                MelonLogger.Error(e.StackTrace);
            }
        }
    }
}
