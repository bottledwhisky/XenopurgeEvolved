using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Commands;
using SpaceCommander.EndGame;
using System.Collections.Generic;
using TimeSystem;

namespace XenopurgeEvolved
{
    public class SleeperBleed : Evolution
    {
        // ### 出血 (Bleed)
        // - Melee hits cause 3 damage over 30s (1 per 10s)
        // - Stacks up to 3 times
        // - Can be removed if healed
        // - Only appears vs synthetics, as a replacement for Lifesteal

        private const float BLEED_DAMAGE = 3f;
        private const float BLEED_DURATION = 30f;
        private const float BLEED_DAMAGE_INTERVAL = 10f;
        private const int MAX_BLEED_STACKS = 3;

        public SleeperBleed()
        {
            unitTag = "Sleeper";
            name = "sleeper_bleed_name";
            description = "sleeper_bleed_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, BLEED_DAMAGE, BLEED_DURATION, MAX_BLEED_STACKS);
        }

        // Bleed effect manager
        private class BleedEffect
        {
            private IDamagable _target;
            private List<BleedStack> _activeStacks = new List<BleedStack>();

            private class BleedStack
            {
                public float remainingTime;
                public float nextDealDamageTime;

                public BleedStack(float duration)
                {
                    remainingTime = duration;
                    nextDealDamageTime = BLEED_DAMAGE_INTERVAL; // Deal first damage after 10 seconds
                }
            }

            public BleedEffect(IDamagable target)
            {
                _target = target;
            }

            public void AddStack()
            {
                // Remove oldest stack if at max capacity
                if (_activeStacks.Count >= MAX_BLEED_STACKS)
                {
                    _activeStacks.RemoveAt(0);
                }

                // Add new stack
                _activeStacks.Add(new BleedStack(BLEED_DURATION));
            }

            public void Update(float deltaTime)
            {
                if (!_target.IsAlive)
                {
                    _activeStacks.Clear();
                    return;
                }

                // Update all stacks and deal damage independently
                for (int i = _activeStacks.Count - 1; i >= 0; i--)
                {
                    BleedStack stack = _activeStacks[i];
                    stack.remainingTime -= deltaTime;
                    stack.nextDealDamageTime -= deltaTime;

                    // Check if this stack should deal damage
                    if (stack.nextDealDamageTime <= 0f)
                    {
                        _target.Damage(1);
                        stack.nextDealDamageTime += BLEED_DAMAGE_INTERVAL; // Schedule next damage tick
                    }

                    if (stack.remainingTime <= 0f)
                    {
                        // Stack expired, remove it
                        _activeStacks.RemoveAt(i);
                    }
                }
            }

            public bool IsActive()
            {
                return _activeStacks.Count > 0 && _target.IsAlive;
            }
        }

        // Static manager to track bleed effects on targets
        private static Dictionary<IDamagable, BleedEffect> _activeEffects = new Dictionary<IDamagable, BleedEffect>();
        private static bool _isTimeListenerRegistered = false;

        // Patch GameManager to register time listener
        [HarmonyPatch(typeof(GameManager), "SetUpEndGameController")]
        public static class GameManager_SetUpEndGameController_Patch
        {
            public static void Postfix(GameManager __instance)
            {
                if (!IsActivated<SleeperBleed>())
                    return;

                RegisterTimeListener();

                EndGameController _endGameController = (EndGameController)AccessTools.Field(
                    typeof(GameManager), "_endGameController"
                ).GetValue(__instance);
                _endGameController.OnGameFinished += OnGameFinished;
            }

            private static void OnGameFinished(bool obj)
            {
                UnregisterTimeListener();
                ResetBleedEffects();
            }
        }

        private static void RegisterTimeListener()
        {
            if (!_isTimeListenerRegistered)
            {
                TempSingleton<TimeManager>.Instance.OnTimeUpdated += OnGameUpdate;
                _isTimeListenerRegistered = true;
            }
        }

        private static void UnregisterTimeListener()
        {
            if (_isTimeListenerRegistered)
            {
                TempSingleton<TimeManager>.Instance.OnTimeUpdated -= OnGameUpdate;
                _isTimeListenerRegistered = false;
            }
        }

        private static void OnGameUpdate(float deltaTime)
        {
            // Update all active bleed effects
            List<IDamagable> toRemove = new List<IDamagable>();

            foreach (var kvp in _activeEffects)
            {
                kvp.Value.Update(deltaTime);

                // Mark for removal if no longer active
                if (!kvp.Value.IsActive())
                {
                    toRemove.Add(kvp.Key);
                }
            }

            // Clean up inactive effects
            foreach (var target in toRemove)
            {
                _activeEffects.Remove(target);
            }
        }

        private static void ApplyBleed(IDamagable target)
        {
            if (!_activeEffects.TryGetValue(target, out BleedEffect effect))
            {
                // Create new bleed effect for this target
                effect = new BleedEffect(target);
                _activeEffects[target] = effect;
            }

            effect.AddStack();
        }

        public static void RemoveBleed(IDamagable target)
        {
            if (_activeEffects.ContainsKey(target))
            {
                _activeEffects.Remove(target);
            }
        }

        private static void ResetBleedEffects()
        {
            _activeEffects.Clear();
        }

        // Harmony patch for Melee command
        [HarmonyPatch(typeof(Melee))]
        public static class MeleeBleedPatch
        {
            [HarmonyPatch("Attack")]
            [HarmonyPostfix]
            public static void Attack_Postfix(Melee __instance)
            {
                // Check if SleeperBleed evolution is activated
                if (!IsActivated<SleeperBleed>())
                {
                    return;
                }
                BattleUnit _battleUnit = (BattleUnit)AccessTools.Field(typeof(Melee), "_battleUnit").GetValue(__instance);

                IDamagable _target = (IDamagable)AccessTools.Field(typeof(Melee), "_target").GetValue(__instance);

                // Check if the attacker is a Sleeper unit
                // There's a current bug that Spitter is tagged as Sleeper
                // So we check WeaponDataSO != null
                if (_battleUnit.UnitTag != Enumerations.UnitTag.Sleeper || _battleUnit.WeaponDataSO != null)
                {
                    return;
                }

                // Check if target exists and is alive
                if (_target == null || !_target.IsAlive)
                {
                    return;
                }

                // Apply bleed effect
                ApplyBleed(_target);

                MelonLogger.Msg($"Sleeper applied bleed to target");
            }
        }

        // Harmony patch for BattleUnit.Heal to remove bleeding when healed
        [HarmonyPatch(typeof(BattleUnit), "Heal")]
        public static class BattleUnitHealPatch
        {
            [HarmonyPostfix]
            public static void Heal_Postfix(BattleUnit __instance, float heal)
            {
                // Check if SleeperBleed evolution is activated
                if (!IsActivated<SleeperBleed>())
                {
                    return;
                }

                // Remove bleeding if healed (heal > 0)
                if (heal > 0f)
                {
                    RemoveBleed(__instance);
                    MelonLogger.Msg($"Removed bleed from {__instance.UnitName} due to healing");
                }
            }
        }
    }
}