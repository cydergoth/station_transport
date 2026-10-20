using System;
using HarmonyLib;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>Hooks the inventory screen and fleet panel for the trade route controls.</summary>
    [HarmonyPatch]
    internal static class HangarPatches
    {
        [HarmonyPatch(typeof(Inventory), "Start")]
        [HarmonyPostfix]
        private static void InventoryStartPostfix(Inventory __instance)
        {
            try
            {
                HangarUI.Create(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not add the trade route buttons: {e}");
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SelectItem))]
        [HarmonyPostfix]
        private static void SelectItemPostfix(Inventory __instance)
        {
            HangarUI.OnSelect(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DeselectItems))]
        [HarmonyPostfix]
        private static void DeselectItemsPostfix()
        {
            HangarUI.Hide();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Close))]
        [HarmonyPostfix]
        private static void ClosePostfix()
        {
            HangarUI.Hide();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.LoadItems))]
        [HarmonyPostfix]
        private static void LoadItemsPostfix(Inventory __instance)
        {
            HangarUI.AfterLoadItems(__instance);
        }

        /// <summary>Commissions a stored hull onto a route instead, when the player chose "Route ship...".</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddToFleet))]
        [HarmonyPrefix]
        private static bool AddToFleetPrefix(Inventory __instance, CrewMember captain)
        {
            return !HangarUI.TryCommission(__instance, captain);
        }

        [HarmonyPatch(typeof(AssignCaptainDialog), nameof(AssignCaptainDialog.Close))]
        [HarmonyPostfix]
        private static void CaptainDialogClosePostfix()
        {
            HangarUI.ClearCommission();
        }

        /// <summary>
        /// "Remove from fleet" refuses a damaged ship, judged by HPstatus / HPbase. A ship that has
        /// never been spawned, such as one commissioned straight onto a route, has HPstatus -1
        /// ("full", to the spawn code) and HPbase 0, which reads as badly damaged.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), "RemoveFromFleet")]
        [HarmonyPrefix]
        private static void RemoveFromFleetPrefix(Inventory __instance, out SpaceShipData __state)
        {
            __state = null;
            int selected = GameAccess.InvSelectedItem(__instance);
            if (selected >= 0 && selected < PChar.Char.mercenaries.Count
                && PChar.Char.mercenaries[selected].shipData is SpaceShipData data && data.HPstatus == -1f && data.HPbase <= 0f)
            {
                __state = data;
                data.HPbase = 1f;
                data.HPstatus = 1f;
            }
        }

        [HarmonyPatch(typeof(Inventory), "RemoveFromFleet")]
        [HarmonyPostfix]
        private static void RemoveFromFleetPostfix(SpaceShipData __state)
        {
            if (__state != null)
            {
                __state.HPbase = 0f;
                __state.HPstatus = -1f;
            }
        }

        /// <summary>Route ships show a route icon on the launch/dock button and a trader role icon.</summary>
        [HarmonyPatch(typeof(FleetMemberSlot), nameof(FleetMemberSlot.UpdateButtons))]
        [HarmonyPostfix]
        private static void SlotButtonsPostfix(FleetMemberSlot __instance)
        {
            bool controlled = __instance.aiMercChar != null && RouteStore.IsControlled(__instance.aiMercChar);
            FleetSlotIcons.Apply(__instance, controlled);
        }

        /// <summary>The role button does nothing for route ships; they always trade.</summary>
        [HarmonyPatch(typeof(FleetMemberSlot), nameof(FleetMemberSlot.ChangeRole))]
        [HarmonyPrefix]
        private static bool ChangeRolePrefix(FleetMemberSlot __instance)
        {
            return __instance.aiMercChar == null || !RouteStore.IsControlled(__instance.aiMercChar);
        }

        /// <summary>Hints for the role button (mode 1) and the launch/dock button (mode 3).</summary>
        [HarmonyPatch(typeof(FleetMemberSlot), nameof(FleetMemberSlot.ButtonEnter), typeof(int))]
        [HarmonyPostfix]
        private static void SlotHintPostfix(FleetMemberSlot __instance, int mode)
        {
            RouteShip ship = __instance.aiMercChar == null ? null : RouteStore.Get(__instance.aiMercChar);
            if (ship == null)
            {
                return;
            }
            if (mode == 1)
            {
                GameAccess.SlotHintText(__instance).text = Loc.Get(Loc.TraderRole);
            }
            else if (mode == 3)
            {
                GameAccess.SlotHintText(__instance).text = Loc.Get(ship.InReserve ? Loc.InRouteReserve : Loc.OnTradeRoute);
            }
        }
    }
}
