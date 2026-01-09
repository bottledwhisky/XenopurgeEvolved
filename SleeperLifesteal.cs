using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Commands;
using System;

namespace XenopurgeEvolved
{
    public class SleeperLifesteal : Evolution
    {
        // ### 吸血 (Lifesteal)
        // - Heals for half of the damage dealt
        // - Only works when the target is above 50% HP
        // - Doesn't work vs synthetics

        private const float LIFESTEAL_RATIO = 0.5f;
        private const float HP_THRESHOLD = 0.5f;

        public SleeperLifesteal()
        {
            unitTag = "Sleeper";
            name = "sleeper_lifesteal_name";
            description = "sleeper_lifesteal_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, (int)(HP_THRESHOLD * 100));
        }

        // Harmony patch for Melee command
        [HarmonyPatch(typeof(Melee))]
        public static class MeleeLifestealPatch
        {
            [HarmonyPatch("Attack")]
            [HarmonyPrefix]
            public static void Attack_Prefix(Melee __instance, out float __state)
            {
                // Initialize state to -1 (no healing)
                __state = -1;

                // Check if SleeperLifesteal evolution is activated
                if (!IsActivated<SleeperLifesteal>())
                {
                    return;
                }

                BattleUnit _battleUnit = (BattleUnit)AccessTools.Field(typeof(Melee), "_battleUnit").GetValue(__instance);
                IDamagable _target = (IDamagable)AccessTools.Field(typeof(Melee), "_target").GetValue(__instance);

                // Check if the attacker is a Sleeper unit
                // Exclude Spitter by checking WeaponDataSO != null
                if (_battleUnit.UnitTag != Enumerations.UnitTag.Sleeper || _battleUnit.WeaponDataSO != null)
                {
                    return;
                }

                // Check if target exists and is alive
                if (_target == null || !_target.IsAlive)
                {
                    return;
                }

                // Check if target is above 50% HP
                if (_target is BattleUnit targetUnit)
                {
                    float hpRatio = (float)targetUnit.CurrentHealth / targetUnit.CurrentMaxHealth;
                    if (hpRatio > HP_THRESHOLD)
                    {
                        // Store the target's current HP to calculate damage dealt
                        __state = targetUnit.CurrentHealth;
                    }
                }
            }

            [HarmonyPatch("Attack")]
            [HarmonyPostfix]
            public static void Attack_Postfix(Melee __instance, float __state)
            {
                // Check if we should process lifesteal (state was set in prefix)
                if (__state == -1)
                {
                    return;
                }

                BattleUnit _battleUnit = (BattleUnit)AccessTools.Field(typeof(Melee), "_battleUnit").GetValue(__instance);
                IDamagable _target = (IDamagable)AccessTools.Field(typeof(Melee), "_target").GetValue(__instance);

                // Calculate damage dealt
                if (_target is BattleUnit targetUnit)
                {
                    float damageDealt = __state - targetUnit.CurrentHealth;

                    if (damageDealt > 0)
                    {
                        // Heal for half of the damage dealt
                        int healAmount = (int)(damageDealt * LIFESTEAL_RATIO);
                        _battleUnit.Heal(healAmount);

                        MelonLogger.Msg($"Sleeper lifesteal: dealt {damageDealt} damage, healed {healAmount} HP");
                    }
                }
            }
        }
    }
}