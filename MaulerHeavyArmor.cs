using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using System;

namespace XenopurgeEvolved
{
    public class MaulerHeavyArmor : Evolution
    {
        // ### 重甲 (Heavy Armor)
        // - If damage is exactly 1, 20% chance to negate completely (permanent effect)

        public static int negateChancePercent = 20;
        public static int negateDamageThreshold = 1;

        public MaulerHeavyArmor()
        {
            unitTag = "Mauler";
            name = "mauler_heavy_armor_name";
            description = "mauler_heavy_armor_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, negateChancePercent, negateDamageThreshold);
        }

        // Harmony patch for BattleUnit damage
        [HarmonyPatch(typeof(BattleUnit))]
        public static class BattleUnitHeavyArmorPatch
        {
            [HarmonyPatch("Damage")]
            [HarmonyPrefix]
            public static void Damage_Prefix(BattleUnit __instance, ref float damage)
            {
                // Check if MaulerHeavyArmor evolution is activated
                if (!IsActivated<MaulerHeavyArmor>())
                {
                    return;
                }

                // Check if the unit is alive and is a Mauler
                if (!__instance.IsAlive)
                {
                    return;
                }

                // Check if this is a Mauler unit
                // Maulers are typically identified by UnitTag.Mauler
                // Adjust this check based on how Maulers are identified in the game
                if (__instance.UnitTag == Enumerations.UnitTag.Mauler)
                {
                    // Only apply effect if damage is not greater than threshold
                    if (damage <= negateDamageThreshold + 0.01f)
                    {
                        if (UnityEngine.Random.Range(0, 100) < negateChancePercent)
                        {
                            damage = 0f;
                            MelonLogger.Msg($"Mauler Heavy Armor: Damage negated ({negateChancePercent}% chance)");
                        }
                        else
                        {
                            MelonLogger.Msg($"Mauler Heavy Armor: Damage not negated (rolled above {negateChancePercent}%)");
                        }
                    }
                }
            }
        }
    }
}
