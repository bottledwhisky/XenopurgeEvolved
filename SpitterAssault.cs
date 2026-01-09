using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using SpaceCommander.Commands;
using SpaceCommander.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XenopurgeEvolved
{
    // ### 突击 (Assault)
    // - Optimal range: close instead of far
    // - Changes retreat → rush-down shooting(shoot while advancing)
    // - +5 melee damage(2 → 7 power in melee)
    // - Becomes melee hybrid
    public class SpitterAssault : Evolution
    {
        public static int meleeDamageBonus = 5;

        public SpitterAssault()
        {
            unitTag = "Spitter";
            name = "spitter_assault_name";
            description = "spitter_assault_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, meleeDamageBonus);
        }
    }

    [HarmonyPatch(typeof(BattleUnit), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(UnitData), typeof(Enumerations.Team), typeof(GridManager) })]
    public class SpitterAssaultBattleUnitConstructorPatch
    {
        public static void Postfix(BattleUnit __instance, UnitData unitData, Enumerations.Team team, GridManager gridManager)
        {
            if (!Evolution.IsActivated<SpitterAssault>())
            {
                return;
            }
            try
            {
                // Check if this unit should have the Spitter Assault evolution applied
                if (!ShouldApplySpitterAssault(__instance))
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
                MelonLogger.Error($"Error in SpitterAssaultBattleUnitConstructorPatch: {ex}");
            }
        }

        public static bool ShouldApplySpitterAssault(BattleUnit battleUnit)
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

    [HarmonyPatch(typeof(UnitDataSO))]
    [HarmonyPatch("CreateUnitInstance")]
    public class SpitterAssaultUnitDataSO_Patch
    {
        private static Dictionary<RangedWeaponDataSO, RangedWeaponDataSO> clonedWeapons = new Dictionary<RangedWeaponDataSO, RangedWeaponDataSO>();

        [HarmonyPostfix]
        public static void Postfix(UnitDataSO __instance, ref UnitData __result)
        {
            if (!Evolution.IsActivated<SpitterAssault>())
            {
                return;
            }

            // Only apply to Sleepers with ranged weapons
            if (__result.UnitTag == Enumerations.UnitTag.Sleeper &&
                __result.UnitEquipmentManager.RangedWeaponDataSO != null)
            {
                // Clone the weapon if we haven't already
                RangedWeaponDataSO originalWeapon = __result.UnitEquipmentManager.RangedWeaponDataSO;

                if (!clonedWeapons.TryGetValue(originalWeapon, out RangedWeaponDataSO clonedWeapon))
                {
                    clonedWeapon = CloneRangedWeapon(originalWeapon);
                    clonedWeapons[originalWeapon] = clonedWeapon;
                }

                // Set the cloned weapon
                __result.UnitEquipmentManager.RangedWeaponDataSO = clonedWeapon;

                // +meleeDamageBonus melee damage
                __result.Power += SpitterAssault.meleeDamageBonus;

                MelonLogger.Msg($"Applied SpitterAssault stats: MeleeDamage={__result.Power}, OptimalRange=Close");
            }
        }

        private static RangedWeaponDataSO CloneRangedWeapon(RangedWeaponDataSO original)
        {
            try
            {
                RangedWeaponDataSO clone = UnityEngine.Object.Instantiate(original);

                // Set optimal range to Close
                var optimalRangeField = AccessTools.Field(typeof(RangedWeaponDataSO), "_optimalRangeOfWeapon");
                if (optimalRangeField != null)
                {
                    optimalRangeField.SetValue(clone, Enumerations.OptimalRangeOfWeapon.Close);
                }

                // Set accuracy modifier per tile: [0, 0, 0, -0.2, -0.2, -0.2]
                var accuracyModifierField = AccessTools.Field(typeof(RangedWeaponDataSO), "_accuracyModifierPerTile");
                if (accuracyModifierField != null)
                {
                    float[] newAccuracyModifiers = new float[] { 0f, 0f, 0f, -0.2f, -0.2f, -0.2f };
                    accuracyModifierField.SetValue(clone, newAccuracyModifiers);
                }

                MelonLogger.Msg("Successfully cloned RangedWeaponDataSO with close range configuration");
                return clone;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cloning RangedWeaponDataSO: {ex}");
                return original;
            }
        }
    }
}