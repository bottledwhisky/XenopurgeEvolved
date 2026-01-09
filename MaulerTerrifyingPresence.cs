using HarmonyLib;
using MelonLoader;
using SpaceCommander;
using SpaceCommander.Area;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SpaceCommander.Enumerations;

namespace XenopurgeEvolved
{
    // ### 恐怖外表 (Terrifying Presence)
    // - All visible enemies must target this Mauler
    // - Forces focus fire, protects other xenos
    public class MaulerTerrifyingPresence : Evolution
    {
        public MaulerTerrifyingPresence()
        {
            unitTag ="Mauler";
            name = "mauler_terrifying_presence_name";
            description = "mauler_terrifying_presence_description";
        }
    }

    [HarmonyPatch(typeof(MovementManager))]
    [HarmonyPatch("ChangePosition")]
    public class MaulerTerrifyingPresenceMovementManager_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattleUnit battleUnit, bool canChangePosition)
        {
            if (!Evolution.IsActivated<MaulerTerrifyingPresence>())
            {
                return;
            }

            // Invalidate cache when any enemy AI unit moves
            if (canChangePosition && battleUnit?.Team == Team.EnemyAI)
            {
                LockTarget_GetClosestVisibleUnitFromTeam_MaulerPatch.InvalidateCache();
            }
        }
    }


    [HarmonyPatch(typeof(LockTarget))]
    [HarmonyPatch("GetClosestVisibleUnitFromTeam")]
    public static class LockTarget_GetClosestVisibleUnitFromTeam_MaulerPatch
    {
        static Dictionary<BattleUnit, BattleUnit> lockedTargets = [];

        [HarmonyPrefix]
        public static bool Prefix(LockTarget __instance, ref BattleUnit __result)
        {
            if (!Evolution.IsActivated<MaulerTerrifyingPresence>())
            {
                return true; // Continue with original method
            }

            // Get private fields using reflection
            var _self = (BattleUnit)AccessTools.Field(typeof(LockTarget), "_self").GetValue(__instance);

            if (lockedTargets.TryGetValue(_self, out __result) && __result != null && __result.IsAlive)
            {
                return false;
            }

            var _lineOfSight = (LineOfSight)AccessTools.Field(typeof(LockTarget), "_lineOfSight").GetValue(__instance);
            var _targetTeam = (Enumerations.Team)AccessTools.Field(typeof(LockTarget), "_targetTeam").GetValue(__instance);
            var _currentPosition = (Vector2Int)AccessTools.Field(typeof(LockTarget), "_currentPosition").GetValue(__instance);

            // Only affect player units targeting enemy AI
            if (_self == null || _self.Team != Enumerations.Team.Player || _targetTeam != Enumerations.Team.EnemyAI)
            {
                return true; // Continue with original method
            }

            // Get all visible tiles occupied by enemy team
            IEnumerable<Tile> visibleTiles = _lineOfSight.Tiles.Where((Tile tile) =>
                tile.CurrentStateOfTile.IsOccupiedByTeam(_targetTeam));

            // Find all visible Maulers
            List<BattleUnit> visibleMaulers = new List<BattleUnit>();

            foreach (Tile tile in visibleTiles)
            {
                if (_self == null ||
                    !tile.CurrentStateOfTile.GetUnitsOnTile(_targetTeam).Contains(_self) ||
                    tile.CurrentStateOfTile.GetUnitsOnTile(_targetTeam).Count() != 1)
                {
                    var unitsOnTile = tile.CurrentStateOfTile.GetUnitsOnTile(_targetTeam)
                        .Where(battleUnit => battleUnit != _self &&
                               battleUnit.CanBeFollowed &&
                               battleUnit.IsAlive &&
                               battleUnit.UnitTag == Enumerations.UnitTag.Mauler);

                    visibleMaulers.AddRange(unitsOnTile);
                }
            }

            // If there are visible Maulers, force target to closest Mauler
            if (visibleMaulers.Count > 0)
            {
                float closestDistance = float.MaxValue;
                BattleUnit closestMauler = null;

                foreach (var mauler in visibleMaulers)
                {
                    float distance = Vector2Int.Distance(_currentPosition, mauler.MovementManager.CurrentTileCoords);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestMauler = mauler;
                    }
                }

                if (closestMauler != null)
                {
                    __result = closestMauler;
                    MelonLogger.Msg($"[MaulerTerrifyingPresence] Forcing {_self.UnitName} to target Mauler at distance {closestDistance}");
                    lockedTargets[_self] = closestMauler;
                    return false; // Skip original method
                }
            }

            // No Maulers visible, continue with original method
            return true;
        }

        internal static void InvalidateCache()
        {
            lockedTargets.Clear();
        }
    }
}
