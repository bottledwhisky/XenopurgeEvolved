using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XenopurgeEvolved
{
    // ### 猎手 (Hunter)
    // - Actively seeks nearest target
    // - More aggressive targeting AI
    public class ScoutHunter : Evolution
    {
        public ScoutHunter()
        {
            unitTag = "Scout";
            name = "scout_hunter_name";
            description = "scout_hunter_description";
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public class ScoutHunterBattleUnitConstructorPatch
    {
        public static void Postfix(BattleUnit __instance, UnitData unitData, Enumerations.Team team, GridManager gridManager)
        {
            if (!Evolution.IsActivated<ScoutHunter>())
            {
                return;
            }
            try
            {
                // Check if this unit should have the Scout Hunter evolution applied
                // You may need to adjust this condition based on your evolution system
                if (!ShouldApplyScoutHunter(__instance))
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

                // Remove ReconCommandDataSO and PatrolCommandDataSO
                bool removedCommands = false;
                modifiedCommands = modifiedCommands.Where(cmd =>
                {
                    if (cmd is ReconCommandDataSO || cmd is PatrolCommandDataSO)
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

                // Find or create HuntCommandDataSO instance
                HuntCommandDataSO huntCommand = FindHuntCommandDataSO();
                if (huntCommand == null)
                {
                    MelonLogger.Error("Failed to find HuntCommandDataSO instance");
                    return;
                }

                // Add HuntCommandDataSO to the list
                modifiedCommands.Add(huntCommand);
                MelonLogger.Msg("Added HuntCommandDataSO to commands list");

                // Set the modified list back to the field
                commandsDataSOListField.SetValue(__instance, modifiedCommands);

                MelonLogger.Msg($"Successfully modified commands for unit: {__instance.UnitId}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BattleUnitConstructorPatch: {ex}");
            }
        }

        private static bool ShouldApplyScoutHunter(BattleUnit battleUnit)
        {
            return battleUnit.UnitTag == Enumerations.UnitTag.Scout;
        }

        static HuntCommandDataSO huntCommand;

        private static HuntCommandDataSO FindHuntCommandDataSO()
        {
            if (huntCommand != null)
            {
                return huntCommand;
            }

            try
            {
                // Method 1: Try to find existing instance in Resources
                HuntCommandDataSO[] huntCommands = UnityEngine.Resources.FindObjectsOfTypeAll<HuntCommandDataSO>();
                if (huntCommands != null && huntCommands.Length > 0)
                {
                    MelonLogger.Msg($"Found {huntCommands.Length} HuntCommandDataSO instances");
                    return huntCommands[0];
                }

                // Method 2: Try to create a new instance
                // Note: ScriptableObjects typically need to be created via Unity's asset system
                // This might not work at runtime, but worth trying
                huntCommand = UnityEngine.ScriptableObject.CreateInstance<HuntCommandDataSO>();
                if (huntCommand != null)
                {
                    MelonLogger.Msg("Created new HuntCommandDataSO instance");
                    return huntCommand;
                }

                MelonLogger.Warning("Could not find or create HuntCommandDataSO");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding HuntCommandDataSO: {ex}");
                return null;
            }
        }
    }
}