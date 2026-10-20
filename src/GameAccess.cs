using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>Non-public game members the mod needs, bound once through Harmony's AccessTools.</summary>
    internal static class GameAccess
    {
        /// <summary>The line under a ship's name in the fleet panel.</summary>
        public static readonly AccessTools.FieldRef<FleetMemberSlot, Text> SlotStatusText =
            AccessTools.FieldRefAccess<FleetMemberSlot, Text>("statusText");

        /// <summary>The fleet panel's launch/dock button for one ship.</summary>
        public static readonly AccessTools.FieldRef<FleetMemberSlot, Transform> SlotLaunchDock =
            AccessTools.FieldRefAccess<FleetMemberSlot, Transform>("btnLaunchDock");

        /// <summary>The fleet panel's role button; its first three children are the role icons.</summary>
        public static readonly AccessTools.FieldRef<FleetMemberSlot, Transform> SlotRole =
            AccessTools.FieldRefAccess<FleetMemberSlot, Transform>("btnRole");

        /// <summary>The fleet panel's hover hint.</summary>
        public static readonly AccessTools.FieldRef<FleetMemberSlot, Text> SlotHintText =
            AccessTools.FieldRefAccess<FleetMemberSlot, Text>("hintPanelText");

        // The inventory screen, which the Hangar and Trade tabs show with the Cargo/Crew/Fleet lists.

        /// <summary>"Add to fleet" / "Remove from fleet".</summary>
        public static readonly AccessTools.FieldRef<Inventory, GameObject> InvBtnAddToFleet =
            AccessTools.FieldRefAccess<Inventory, GameObject>("btnAddToFleet");

        /// <summary>"Launch" / "Dock" for a fleet ship.</summary>
        public static readonly AccessTools.FieldRef<Inventory, GameObject> InvBtnLoad =
            AccessTools.FieldRefAccess<Inventory, GameObject>("btnLoad");

        /// <summary>"Refit" for a fleet ship.</summary>
        public static readonly AccessTools.FieldRef<Inventory, GameObject> InvBtnEquip =
            AccessTools.FieldRefAccess<Inventory, GameObject>("btnEquip");

        public static readonly AccessTools.FieldRef<Inventory, Transform> InvItemPanel =
            AccessTools.FieldRefAccess<Inventory, Transform>("itemPanel");

        /// <summary>0 = cargo, 1 = crew, 2 = fleet.</summary>
        public static readonly AccessTools.FieldRef<Inventory, int> InvCargoMode =
            AccessTools.FieldRefAccess<Inventory, int>("cargoMode");

        /// <summary>Index into the cargo list, or into PChar.Char.mercenaries in fleet mode.</summary>
        public static readonly AccessTools.FieldRef<Inventory, int> InvSelectedItem =
            AccessTools.FieldRefAccess<Inventory, int>("selectedItem");

        public static readonly Action<Inventory> InvAddItemSlot =
            AccessTools.MethodDelegate<Action<Inventory>>(AccessTools.Method(typeof(Inventory), "AddItemSlot"));
    }
}
