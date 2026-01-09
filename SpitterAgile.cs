using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XenopurgeEvolved
{
    // ### 灵敏 (Agile)
    // - Changes retreat → kiting fire(shoot while retreating)
    // - -10 accuracy(50% → 40%)
    // - Mobile harasser role
    public class SpitterAgile : Evolution
    {
        public static float reducedAccuracyPercent = .4f;
        public static float baseAccuracyPercent = .5f;

        public SpitterAgile()
        {
            unitTag = "Spitter";
            name = "spitter_agile_name";
            description = "spitter_agile_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, reducedAccuracyPercent * 100, baseAccuracyPercent * 100);
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public class SpitterAgileBattleUnitConstructorPatch
    {
        public static void Postfix(BattleUnit __instance, UnitData unitData, Enumerations.Team team, GridManager gridManager)
        {
            if (!Evolution.IsActivated<SpitterAgile>())
            {
                return;
            }
            try
            {
                // Check if this unit should have the Spitter Agile evolution applied
                if (!ShouldApplySpitterAgile(__instance))
                {
                    return;
                }

                // Access the private _commandsDataSOList field
                var commandsDataSOListField = AccessTools.Field(typeof(BattleUnit), "_commandsDataSOList");
                if (commandsDataSOListField == null)
                {
                    MelonLogger.Error("Failed to find _commandsDataSOList field");
                    return;
                }

                var commandsDataSOList = commandsDataSOListField.GetValue(__instance) as IEnumerable<CommandDataSO>;
                if (commandsDataSOList == null)
                {
                    MelonLogger.Error("_commandsDataSOList is null");
                    return;
                }

                // Convert to a mutable list
                List<CommandDataSO> modifiedCommands = new List<CommandDataSO>(commandsDataSOList);

                // Remove FallbackCommandDataSO
                bool removedCommands = false;
                modifiedCommands = modifiedCommands.Where(cmd =>
                {
                    if (cmd is FallbackCommandDataSO)
                    {
                        MelonLogger.Msg($"Removing command: {cmd.GetType().Name}");
                        removedCommands = true;
                        return false;
                    }
                    return true;
                }).ToList();

                // Only proceed if we actually removed commands
                if (!removedCommands)
                {
                    return;
                }

                // Find or create RunAndGunCommandDataSO instance
                RunAndGunCommandDataSO runAndGunCommand = FindRunAndGunCommandDataSO();
                if (runAndGunCommand == null)
                {
                    MelonLogger.Error("Failed to find RunAndGunCommandDataSO instance");
                    return;
                }

                // Add RunAndGunCommandDataSO to the list
                modifiedCommands.Add(runAndGunCommand);
                MelonLogger.Msg("Added RunAndGunCommandDataSO to commands list");

                // Set the modified list back to the field
                commandsDataSOListField.SetValue(__instance, modifiedCommands);

                MelonLogger.Msg($"Successfully modified commands for unit: {__instance.UnitId}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SpitterAgileBattleUnitConstructorPatch: {ex}");
            }
        }

        public static bool ShouldApplySpitterAgile(BattleUnit battleUnit)
        {
            return battleUnit.UnitTag == Enumerations.UnitTag.Sleeper && battleUnit.WeaponDataSO != null;
        }

        static RunAndGunCommandDataSO runAndGunCommand;

        private static RunAndGunCommandDataSO FindRunAndGunCommandDataSO()
        {
            if (runAndGunCommand != null)
            {
                return runAndGunCommand;
            }

            try
            {
                // Method 1: Try to find existing instance in Resources
                RunAndGunCommandDataSO[] runAndGunCommands = UnityEngine.Resources.FindObjectsOfTypeAll<RunAndGunCommandDataSO>();
                if (runAndGunCommands != null && runAndGunCommands.Length > 0)
                {
                    MelonLogger.Msg($"Found {runAndGunCommands.Length} RunAndGunCommandDataSO instances");
                    runAndGunCommand = runAndGunCommands[0];
                    return runAndGunCommand;
                }

                // Method 2: Try to create a new instance
                // Note: ScriptableObjects typically need to be created via Unity's asset system
                // This might not work at runtime, but worth trying
                runAndGunCommand = UnityEngine.ScriptableObject.CreateInstance<RunAndGunCommandDataSO>();
                if (runAndGunCommand != null)
                {
                    MelonLogger.Msg("Created new RunAndGunCommandDataSO instance");
                    return runAndGunCommand;
                }

                MelonLogger.Warning("Could not find or create RunAndGunCommandDataSO");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding RunAndGunCommandDataSO: {ex}");
                return null;
            }
        }
    }

    // Optional: Stat modifications for SpitterAgile
    // Uncomment and modify if you want to change Spitter stats when this evolution is active
    [HarmonyPatch(typeof(UnitDataSO))]
    [HarmonyPatch("CreateUnitInstance")]
    public class SpitterAgileUnitDataSO_Patch
    {

        [HarmonyPostfix]
        public static void Postfix(UnitDataSO __instance, ref UnitData __result)
        {
            if (!Evolution.IsActivated<SpitterAgile>())
            {
                return;
            }
            // Only apply to Spitters
            if (__result.UnitTag == Enumerations.UnitTag.Sleeper &&
                __result.UnitEquipmentManager.RangedWeaponDataSO != null)
            {
                __result.Accuracy = SpitterAgile.reducedAccuracyPercent;
            }
        }
    }
}