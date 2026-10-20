using System.Collections.Generic;
using HarmonyLib;

namespace StationTransport
{
    /// <summary>
    /// Keeps route ships out of the player's active fleet: they never spawn, launch or dock with
    /// the player, and don't count against the Fleet Commander limit.
    /// </summary>
    [HarmonyPatch]
    internal static class FleetPatches
    {
        /// <summary>
        /// The game spawns every fleet ship that IsActive() in the player's sector. A route ship in
        /// flight there would otherwise appear and follow the player.
        /// </summary>
        [HarmonyPatch(typeof(AIMercenaryCharacter), nameof(AIMercenaryCharacter.IsActive))]
        [HarmonyPostfix]
        private static void IsActivePostfix(AIMercenaryCharacter __instance, ref bool __result)
        {
            if (__result && RouteStore.IsControlled(__instance))
            {
                __result = false;
            }
        }

        /// <summary>
        /// Every capacity check and display reads this sum, so leaving route ships out of it is the
        /// whole Fleet Commander exemption.
        /// </summary>
        [HarmonyPatch(typeof(PChar), nameof(PChar.GetFleetSpaceOcupied))]
        [HarmonyPostfix]
        private static void FleetSpacePostfix(ref int __result)
        {
            if (!Plugin.ExemptFromFleetCommander.Value || PChar.Char?.mercenaries == null)
            {
                return;
            }
            foreach (AIMercenaryCharacter character in PChar.Char.mercenaries)
            {
                if (character is PlayerFleetMember && RouteStore.IsControlled(character))
                {
                    __result -= character.ModelData().fleetSpaceOcupied;
                }
            }
        }

        /// <summary>Covers launching from the fleet panel, warping in from another sector and game events.</summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchPlayerFleetMember))]
        [HarmonyPrefix]
        private static bool LaunchPrefix(AIMercenaryCharacter aiMercChar)
        {
            return !Refuse(aiMercChar);
        }

        /// <summary>Runs before the slot's own bookkeeping; "Launch fleet" calls this for every slot.</summary>
        [HarmonyPatch(typeof(FleetMemberSlot), nameof(FleetMemberSlot.OrderLaunch))]
        [HarmonyPrefix]
        private static bool OrderLaunchPrefix(FleetMemberSlot __instance)
        {
            return !Refuse(__instance.aiMercChar);
        }

        /// <summary>
        /// A route ship in flight through the player's sector passes the slot's own dock checks and
        /// could be taken into a carrier hangar.
        /// </summary>
        [HarmonyPatch(typeof(FleetMemberSlot), nameof(FleetMemberSlot.OrderDock))]
        [HarmonyPrefix]
        private static bool OrderDockPrefix(FleetMemberSlot __instance)
        {
            return !RouteStore.IsControlled(__instance.aiMercChar);
        }

        /// <summary>Refitting changes the hold and speed mid-route; unassign the ship first.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CallEditFleetShip))]
        [HarmonyPrefix]
        private static bool EditFleetShipPrefix(AIMercenaryCharacter aiMercChar)
        {
            RouteShip ship = RouteStore.Get(aiMercChar);
            if (ship == null || ship.InReserve)
            {
                return true;
            }
            Warn(Loc.Get(Loc.OnTradeRouteWarning));
            return false;
        }

        /// <summary>Shows the ship's route status where the game leaves the line empty.</summary>
        [HarmonyPatch(typeof(FleetMemberSlot), "UpdateBars")]
        [HarmonyPostfix]
        private static void UpdateBarsPostfix(FleetMemberSlot __instance)
        {
            RouteShip ship = __instance.aiMercChar == null ? null : RouteStore.Get(__instance.aiMercChar);
            if (ship != null && __instance.aiMercControl == null)
            {
                GameAccess.SlotStatusText(__instance).text = RouteSim.Status(ship);
            }
        }

        /// <summary>
        /// The panel lists a ship as away only when it's outside the player's sector. A route ship in
        /// flight to a stop in this sector would drop out of every list.
        /// </summary>
        [HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.GetAwayAIFleetMembers))]
        [HarmonyPostfix]
        private static void AwayMembersPostfix(PlayerCharacter __instance, List<AIMercenaryCharacter> __result)
        {
            foreach (AIMercenaryCharacter character in __instance.mercenaries)
            {
                RouteShip ship = RouteStore.Get(character);
                if (ship != null && character.dockedStationID == -1 && !__result.Contains(character))
                {
                    __result.Add(character);
                }
            }
        }

        private static bool Refuse(AIMercenaryCharacter character)
        {
            if (character == null || !RouteStore.IsControlled(character))
            {
                return false;
            }
            Warn(Loc.Get(Loc.OnTradeRouteWarning));
            return true;
        }

        private static void Warn(string message)
        {
            if (InfoPanelControl.inst != null)
            {
                InfoPanelControl.inst.ShowWarning(message, 1, playAudio: false);
            }
        }
    }
}
