using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.EndGame;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeSystem;

namespace XenopurgeEvolved
{
    public class ScoutSacrifice : Evolution
    {
        // ### 献祭 (Sacrifice)
        // - On death: allies on same tile get +2 speed, +2 melee damage for 5s
        // - Buff doesn't stack from multiple deaths, but refreshes duration
        // - Turns Scout deaths into tactical advantage

        public static float BUFF_SPEED = 2f;
        public static float BUFF_POWER = 2f;
        public static float BUFF_DURATION = 5f;
        public static string BUFF_GUID_SPEED = "scout_sacrifice_speed";
        public static string BUFF_GUID_POWER = "scout_sacrifice_power";

        // Store active buffs: UnitId -> (ExpiryTime in seconds, IsActive)
        public static Dictionary<string, (float expiryTime, bool isActive)> activeSpeedBuffs = new Dictionary<string, (float, bool)>();
        public static Dictionary<string, (float expiryTime, bool isActive)> activePowerBuffs = new Dictionary<string, (float, bool)>();

        // Track elapsed time using game's update delta
        public static float currentGameTime = 0f;

        public ScoutSacrifice()
        {
            unitTag = "Scout";
            name = "scout_sacrifice_name";
            description = "scout_sacrifice_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, BUFF_SPEED, BUFF_POWER, BUFF_DURATION);
        }


        // Called from GameManager OnUpdate patch to accumulate game time
        public static void OnGameUpdate(float deltaTime)
        {
            currentGameTime += deltaTime;

            // Check all active buffs for expiry
            CheckExpiredBuffs();
        }

        private static void CheckExpiredBuffs()
        {
            // Check speed buffs
            List<string> expiredSpeedBuffs = new List<string>();
            foreach (var kvp in activeSpeedBuffs)
            {
                if (kvp.Value.isActive && currentGameTime >= kvp.Value.expiryTime)
                {
                    expiredSpeedBuffs.Add(kvp.Key);
                }
            }
            foreach (var key in expiredSpeedBuffs)
            {
                activeSpeedBuffs[key] = (0f, false);
            }

            // Check power buffs
            List<string> expiredPowerBuffs = new List<string>();
            foreach (var kvp in activePowerBuffs)
            {
                if (kvp.Value.isActive && currentGameTime >= kvp.Value.expiryTime)
                {
                    expiredPowerBuffs.Add(kvp.Key);
                }
            }
            foreach (var key in expiredPowerBuffs)
            {
                activePowerBuffs[key] = (0f, false);
            }
        }

        public static void CheckAndExpireBuff(
            BattleUnit unit,
            Dictionary<string, (float expiryTime, bool isActive)> buffs,
            Enumerations.UnitStats stat,
            string buffGuidPrefix)
        {
            string unitId = unit.UnitId;

            if (!buffs.ContainsKey(unitId))
                return;

            var buffData = buffs[unitId];

            // If buff was marked as expired by the update loop, remove it now
            if (!buffData.isActive && unit._statChanges.ContainsKey(buffGuidPrefix + "_" + unitId))
            {
                string fullGuid = buffGuidPrefix + "_" + unitId;
                unit.ReverseChangeOfStat(fullGuid);
                buffs.Remove(unitId);
                MelonLogger.Msg($"Scout Sacrifice buff ({stat}) expired for {unit.UnitName}");
            }
        }


        public static void CleanupBuffsForUnit(string unitId)
        {
            activeSpeedBuffs.Remove(unitId);
            activePowerBuffs.Remove(unitId);
        }

        // Reset game time when needed (e.g., new battle)
        public static void ResetGameTime()
        {
            currentGameTime = 0f;
            activeSpeedBuffs.Clear();
            activePowerBuffs.Clear();
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetUpEndGameController")]
    public static class GameManager_SetUpEndGameController_Patch
    {
        public static void Postfix(GameManager __instance)
        {
            if (!Evolution.IsActivated<ScoutSacrifice>())
                return;

            TempSingleton<TimeManager>.Instance.OnTimeUpdated += ScoutSacrifice.OnGameUpdate;
            EndGameController _endGameController = (EndGameController)AccessTools.Field(
                typeof(GameManager), "_endGameController"
            ).GetValue(__instance);
            _endGameController.OnGameFinished += _endGameController_OnGameFinished;
        }

        private static void _endGameController_OnGameFinished(bool obj)
        {
            TempSingleton<TimeManager>.Instance.OnTimeUpdated -= ScoutSacrifice.OnGameUpdate;
            ScoutSacrifice.ResetGameTime();
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch([typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager)])]
    public static class ScoutSacrificeBattleUnitConstructorPatch
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
            // Check if unit died (health is now 0 after damage)
            if (__instance.UnitTag == Enumerations.UnitTag.Scout)
            {
                // Check if this is a Scout unit (you may need to check UnitTag or another identifier)
                // For now, assuming Scouts have a specific tag or we apply to all enemy deaths
                // You may need to add: if (__instance.UnitTag != Enumerations.UnitTag.Scout) return;

                Tile currentTile = __instance.CurrentTile;
                if (currentTile == null)
                    return;

                // Get all allied units on the same tile (other enemy AI units)
                var alliesOnTile = currentTile.CurrentStateOfTile.UnitsOnTile
                    .Where(u => u.Team == __instance.Team && u != __instance && u.IsAlive)
                    .ToList();

                if (alliesOnTile.Count == 0)
                {
                    MelonLogger.Msg($"Scout died, but no allies on the tile.");
                    return;
                }

                var currentGameTime = ScoutSacrifice.currentGameTime;
                var activeSpeedBuffs = ScoutSacrifice.activeSpeedBuffs;
                var activePowerBuffs = ScoutSacrifice.activePowerBuffs;
                var BUFF_DURATION = ScoutSacrifice.BUFF_DURATION;
                var BUFF_SPEED = ScoutSacrifice.BUFF_SPEED;
                var BUFF_POWER = ScoutSacrifice.BUFF_POWER;
                var BUFF_GUID_SPEED = ScoutSacrifice.BUFF_GUID_SPEED;
                var BUFF_GUID_POWER = ScoutSacrifice.BUFF_GUID_POWER;

                float expiryTime = currentGameTime + BUFF_DURATION;
                ScoutSacrifice.CleanupBuffsForUnit(__instance.UnitId);

                MelonLogger.Msg($"Scout Sacrifice triggered! Buffing {alliesOnTile.Count} enemy allies on tile.");

                // Apply buffs to each ally
                foreach (var ally in alliesOnTile)
                {
                    if (ally == __instance) continue;
                    string unitId = ally.UnitId;

                    // Apply or refresh speed buff
                    if (activeSpeedBuffs.ContainsKey(unitId))
                    {
                        // Refresh duration only - don't reapply the stat change
                        activeSpeedBuffs[unitId] = (expiryTime, true);
                        MelonLogger.Msg($"Refreshed speed buff for {ally.UnitName}");
                    }
                    else
                    {
                        // New buff - apply stat change
                        activeSpeedBuffs[unitId] = (expiryTime, true);
                        ally.ChangeStat(Enumerations.UnitStats.Speed, BUFF_SPEED, BUFF_GUID_SPEED + "_" + unitId);
                        MelonLogger.Msg($"Applied speed buff to {ally.UnitName}");
                    }

                    // Apply or refresh power buff
                    if (activePowerBuffs.ContainsKey(unitId))
                    {
                        // Refresh duration only
                        activePowerBuffs[unitId] = (expiryTime, true);
                        MelonLogger.Msg($"Refreshed power buff for {ally.UnitName}");
                    }
                    else
                    {
                        // New buff - apply stat change
                        activePowerBuffs[unitId] = (expiryTime, true);
                        ally.ChangeStat(Enumerations.UnitStats.Power, BUFF_POWER, BUFF_GUID_POWER + "_" + unitId);
                        MelonLogger.Msg($"Applied power buff to {ally.UnitName}");
                    }
                }
            }
        }
    }

    // Patch to check and expire buffs when stats are accessed
    [HarmonyPatch(typeof(BattleUnit), "Speed", MethodType.Getter)]
    public static class BattleUnit_Speed_Getter_Patch
    {
        public static void Postfix(BattleUnit __instance)
        {
            if (!Evolution.IsActivated<ScoutSacrifice>())
                return;

            ScoutSacrifice.CheckAndExpireBuff(__instance,
                ScoutSacrifice.activeSpeedBuffs,
                Enumerations.UnitStats.Speed,
                ScoutSacrifice.BUFF_GUID_SPEED
            );
        }
    }

    [HarmonyPatch(typeof(BattleUnit), "Power", MethodType.Getter)]
    public static class BattleUnit_Power_Getter_Patch
    {
        public static void Postfix(BattleUnit __instance)
        {
            if (!Evolution.IsActivated<ScoutSacrifice>())
                return;

            ScoutSacrifice.CheckAndExpireBuff(__instance,
                ScoutSacrifice.activePowerBuffs,
                Enumerations.UnitStats.Power,
                ScoutSacrifice.BUFF_GUID_POWER
            );
        }
    }
}