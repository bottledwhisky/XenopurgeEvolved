using System.Linq;
using HarmonyLib;
using Traptics.EventsSystem;
using SpaceCommander.Abilities;
using MelonLoader;
using SpaceCommander;

namespace XenopurgeEvolved
{
    public class SleeperStealth : Evolution
    {
        // ### 潜伏 (Stealth)
        // - Undetectable except by vision
        // - +2 speed when in rooms(encourages ambushes)
        public static float speedBonus = 2f;

        public SleeperStealth()
        {
            unitTag = "Sleeper";
            name = "sleeper_stealth_name";
            description = "sleeper_stealth_description";
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" +
                ModLocalization.Get(description, speedBonus);
        }
    }

    [HarmonyPatch(typeof(BattleUnit))]
    [HarmonyPatch("Speed", MethodType.Getter)]
    public class SleeperStealth_Speed_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleUnit __instance, ref float __result)
        {
            if (!Evolution.IsActivated<SleeperStealth>())
            {
                return;
            }

            // Only apply speed boost to Sleepers
            if (__instance.UnitTag != SpaceCommander.Enumerations.UnitTag.Sleeper || __instance.WeaponDataSO != null)
            {
                return;
            }

            // Check if the Sleeper is in a room (Tile.Room is not null)
            if (__instance.CurrentTile != null && __instance.CurrentTile.Room != null)
            {
                __result += SleeperStealth.speedBonus;
            }
        }
    }

    // Instead of patching the generic Publish<TEvent> method, we patch it specifically for SonarPulseCreatedEvent
    [HarmonyPatch(typeof(SonarDrawer), "OnSonarPulseCreated")]
    public class SonarPulseInterceptor
    {
        // Use object instead of generic - Harmony will call this for all Publish calls
        public static bool Prefix(ref SonarPulseCreatedEvent @event)
        {
            if (!Evolution.IsActivated<SleeperStealth>())
                return true;
            var sonarEvent = @event;

            try
            {
                var pulseInfo = sonarEvent.PulseInfo;

                // Filter out units with Tag == "Sleeper"
                // There's a current bug that Spitter is tagged as Sleeper
                // So we check WeaponDataSO
                var filteredEnemies = pulseInfo.Enemies
                    .Where(unit => unit.UnitTag != SpaceCommander.Enumerations.UnitTag.Sleeper || unit.WeaponDataSO != null)
                    .ToList();

                // If no Sleepers were filtered out, continue normally
                if (filteredEnemies.Count == pulseInfo.Enemies.Count())
                    return true;

                // Create a new PulseInfo with filtered enemies
                var newPulseInfo = new Sonar.PulseInfo
                {
                    SonarId = pulseInfo.SonarId,
                    Enemies = filteredEnemies,
                    PulseScale = pulseInfo.PulseScale,
                    PulseDurationInSeconds = pulseInfo.PulseDurationInSeconds,
                    Owner = pulseInfo.Owner
                };

                var filteredEvent = new SonarPulseCreatedEvent(newPulseInfo);
                @event = filteredEvent;

                MelonLogger.Msg($"Filtered {pulseInfo.Enemies.Count() - filteredEnemies.Count} Sleeper(s) from sonar pulse");

                // Skip the original method since we already published the filtered event
                return true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in SleeperStealth sonar filter: {ex}");
                return true; // Continue with original method if error occurs
            }
        }
    }
}
