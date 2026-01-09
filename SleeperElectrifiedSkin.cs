using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Commands;
using System;

namespace XenopurgeEvolved
{
    public class SleeperElectrifiedSkin : Evolution
    {
        // ### 感电皮肤 (Electrified Skin)
        // - Received melee hits deal 2 electric damage back to attacker
        // - Only appears vs synthetics, as a replacement for Suffocation

        private const float ELECTRIFIED_DAMAGE = 2f;

        public SleeperElectrifiedSkin()
        {
            unitTag = "Sleeper";
            name = "sleeper_electrified_skin_name";
            description = "sleeper_electrified_skin_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, ELECTRIFIED_DAMAGE);
        }

        // Harmony patch for Melee command
        [HarmonyPatch(typeof(Melee))]
        public static class MeleeElectrifiedSkinPatch
        {
            [HarmonyPatch("Attack")]
            [HarmonyPostfix]
            public static void Attack_Postfix(Melee __instance)
            {
                // Check if SleeperElectrifiedSkin evolution is activated
                if (!IsActivated<SleeperElectrifiedSkin>())
                {
                    return;
                }

                BattleUnit _battleUnit = (BattleUnit)AccessTools.Field(typeof(Melee), "_battleUnit").GetValue(__instance);
                IDamagable _target = (IDamagable)AccessTools.Field(typeof(Melee), "_target").GetValue(__instance);

                // Check if target exists and is alive
                if (_target == null || !_target.IsAlive)
                {
                    return;
                }

                // Check if the target is a BattleUnit and is a Sleeper
                if (_target is BattleUnit targetUnit)
                {
                    // Check if the target is a Sleeper unit
                    // Exclude Spitter by checking WeaponDataSO != null
                    if (targetUnit.UnitTag == Enumerations.UnitTag.Sleeper && targetUnit.WeaponDataSO == null)
                    {
                        // Deal electric damage back to the attacker
                        _battleUnit.Damage((int)ELECTRIFIED_DAMAGE);
                        MelonLogger.Msg($"Sleeper Electrified Skin dealt {ELECTRIFIED_DAMAGE} damage back to attacker");
                    }
                }
            }
        }
    }
}
