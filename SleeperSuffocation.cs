using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Commands;
using System.Collections.Generic;

namespace XenopurgeEvolved
{
    // ### 窒息 (Suffocation)
    // - Stuns enemies for 2s on first melee engagement
    // - Once per Sleeper
    // - Doesn't work vs synthetics
    public class SleeperSuffocation : Evolution
    {
        private const float STUN_DURATION = 2f;

        public SleeperSuffocation()
        {
            unitTag = "Sleeper";
            name = "sleeper_suffocation_name";
            description = "sleeper_suffocation_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, STUN_DURATION);
        }

        // Track which sleepers have already used suffocation (once per sleeper)
        private static HashSet<BattleUnit> _sleepersWithSuffocationUsed = new HashSet<BattleUnit>();

        // Track stunned units and their remaining stun time
        private static Dictionary<IDamagable, float> _stunnedUnits = new Dictionary<IDamagable, float>();

        // Harmony patch for Melee command - Attack method
        [HarmonyPatch(typeof(Melee))]
        public static class MeleeSuffocationPatch
        {
            [HarmonyPatch("Attack", MethodType.Normal)]
            [HarmonyPostfix]
            public static void Attack_Postfix(Melee __instance)
            {
                // Check if SleeperSuffocation evolution is activated
                if (!IsActivated<SleeperSuffocation>())
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

                // Check if this sleeper has already used suffocation (once per sleeper)
                if (_sleepersWithSuffocationUsed.Contains(_battleUnit))
                {
                    return;
                }

                // Check if target exists and is alive
                if (_target == null || !_target.IsAlive)
                {
                    return;
                }

                // Mark this sleeper as having used suffocation
                _sleepersWithSuffocationUsed.Add(_battleUnit);

                // Apply stun to the target
                if (!_stunnedUnits.ContainsKey(_target))
                {
                    _stunnedUnits[_target] = STUN_DURATION;
                    MelonLogger.Msg($"Sleeper applied suffocation stun to target for {STUN_DURATION}s");
                }
            }

            // Patch UpdateTime to implement stun effect
            [HarmonyPatch("UpdateTime", MethodType.Normal)]
            [HarmonyPrefix]
            public static bool UpdateTime_Prefix(Melee __instance, ref float time)
            {
                // Check if SleeperSuffocation evolution is activated
                if (!IsActivated<SleeperSuffocation>())
                {
                    return true; // Continue normal execution
                }

                BattleUnit _battleUnit = (BattleUnit)AccessTools.Field(typeof(Melee), "_battleUnit").GetValue(__instance);

                // Check if this unit is stunned
                if (_stunnedUnits.ContainsKey(_battleUnit))
                {
                    // Reduce stun time
                    _stunnedUnits[_battleUnit] -= time;

                    if (_stunnedUnits[_battleUnit] <= 0f)
                    {
                        // Stun expired, calculate remaining time overflow
                        float remainingTime = -_stunnedUnits[_battleUnit];
                        _stunnedUnits.Remove(_battleUnit);

                        // Adjust time to only the overflow amount
                        time = remainingTime;

                        MelonLogger.Msg($"Stun expired for unit, continuing with {remainingTime}s remaining");
                        return true; // Continue normal execution with adjusted time
                    }

                    // Unit is still stunned, prevent UpdateTime from executing
                    return false;
                }

                return true; // Continue normal execution
            }
        }

        // Clean up static data when game ends
        [HarmonyPatch(typeof(GameManager), "SetUpEndGameController")]
        public static class GameManager_SetUpEndGameController_Patch
        {
            public static void Postfix(GameManager __instance)
            {
                if (!IsActivated<SleeperSuffocation>())
                    return;

                var _endGameController = (SpaceCommander.EndGame.EndGameController)AccessTools.Field(
                    typeof(GameManager), "_endGameController"
                ).GetValue(__instance);

                _endGameController.OnGameFinished += OnGameFinished;
            }

            private static void OnGameFinished(bool obj)
            {
                ResetSuffocationData();
            }
        }

        private static void ResetSuffocationData()
        {
            _sleepersWithSuffocationUsed.Clear();
            _stunnedUnits.Clear();
        }
    }
}
