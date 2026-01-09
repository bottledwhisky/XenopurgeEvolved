using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using System;

namespace XenopurgeEvolved
{
    // ### 神射手 (Sharpshooter)
    // - +50 accuracy(50% → 100%)
    // - -2 speed(25 → 23)
    // - Stand-and-shoot specialist
    public class SpitterSharpshooter : Evolution
    {
        public static float finalAccuracyPercent = 1f;
        public static float baseAccuracyPercent = .5f;
        public static int speedPenalty = 2;

        public SpitterSharpshooter()
        {
            unitTag = "Spitter";
            name = "spitter_sharpshooter_name";
            description = "spitter_sharpshooter_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, finalAccuracyPercent * 100, baseAccuracyPercent * 100, speedPenalty);
        }
    }

    [HarmonyPatch(typeof(UnitDataSO))]
    [HarmonyPatch("CreateUnitInstance")]
    public class SpitterSharpshooterUnitDataSO_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UnitDataSO __instance, ref UnitData __result)
        {
            if (!Evolution.IsActivated<SpitterSharpshooter>())
            {
                return;
            }

            // Only apply to Sleepers with ranged weapons, because the bug
            if (__result.UnitTag == Enumerations.UnitTag.Sleeper &&
                __result.UnitEquipmentManager.RangedWeaponDataSO != null)
            {
                // Adjust accuracy to finalAccuracyPercent
                __result.Accuracy = SpitterSharpshooter.finalAccuracyPercent;

                // -speedPenalty speed
                __result.Speed -= SpitterSharpshooter.speedPenalty;

                MelonLogger.Msg($"Applied SpitterSharpshooter stats: Accuracy={__result.Accuracy}, Speed={__result.Speed}");
            }
        }
    }
}
