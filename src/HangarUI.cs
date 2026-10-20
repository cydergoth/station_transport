using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>
    /// Trade route controls in the inventory screen the Hangar tab shows:
    /// - Fleet list: "Transfer to trade route" for a ship docked here, "Remove from route" for a route
    ///   ship, and a "Trade routes" section listing every route ship wherever it is.
    /// - Cargo list: "Route ship..." on a stored hull, which commissions it straight onto a route
    ///   without the Fleet Commander check.
    /// </summary>
    internal static class HangarUI
    {
        // The inventory's buttons sit in one column, 30 apart (positions from the game's layout).
        // In fleet view the row under "Remove from fleet" is free; in cargo view, with a stored
        // ship selected, the "Scrap" row is free.
        private const float FleetButtonY = -448f;
        private const float CargoButtonY = -493f;
        private const float PickerTopY = -45f;
        private const float PickerLeftX = -245f;

        private const int FleetMode = 2;
        private const int ShipItem = 4;

        private enum Action
        {
            None,
            Assign,
            Remove,
            Commission,
        }

        private static Inventory inventory;
        private static GameObject button;
        private static Text buttonText;
        private static RoutePicker picker;
        private static Action action;

        /// <summary>The route chosen for a stored hull, waiting for the captain dialog.</summary>
        private static TradeRoute commissionRoute;

        public static void Create(Inventory inv)
        {
            inventory = inv;
            GameObject template = GameAccess.InvBtnAddToFleet(inv);
            button = Object.Instantiate(template, template.transform.parent, false);
            button.name = "BtnTradeRoute";
            StripGameHandlers(button);
            buttonText = button.GetComponentInChildren<Text>(true);
            // "Transfer to trade route" is longer than the game's labels for this button size.
            buttonText.resizeTextForBestFit = true;
            buttonText.resizeTextMinSize = 9;
            buttonText.resizeTextMaxSize = buttonText.fontSize;
            // Best fit only shrinks text that wraps and truncates.
            buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            buttonText.verticalOverflow = VerticalWrapMode.Truncate;
            Button b = button.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(OnClick);
            button.SetActive(false);

            picker = new RoutePicker(template.transform.parent, template, new Vector2(PickerLeftX, PickerTopY));
        }

        /// <summary>
        /// Clones of game buttons carry the game's own click and hover handlers, and maybe a
        /// translated label that would overwrite ours.
        /// </summary>
        public static void StripGameHandlers(GameObject go)
        {
            foreach (EventTrigger trigger in go.GetComponentsInChildren<EventTrigger>(true))
            {
                Object.DestroyImmediate(trigger);
            }
            foreach (LangText lang in go.GetComponentsInChildren<LangText>(true))
            {
                Object.DestroyImmediate(lang);
            }
        }

        /// <summary>After the game sets up its buttons for the selected item.</summary>
        public static void OnSelect(Inventory inv)
        {
            Hide();
            if (inv != inventory || button == null || !RouteSim.GameActive)
            {
                return;
            }
            int selected = GameAccess.InvSelectedItem(inv);
            if (selected < 0)
            {
                return;
            }
            if (GameAccess.InvCargoMode(inv) == FleetMode)
            {
                OnSelectFleetShip(inv, selected);
            }
            else
            {
                OnSelectCargo(inv, selected);
            }
        }

        private static void OnSelectFleetShip(Inventory inv, int selected)
        {
            List<AIMercenaryCharacter> mercenaries = PChar.Char.mercenaries;
            if (selected >= mercenaries.Count || !(mercenaries[selected] is PlayerFleetMember member))
            {
                return;
            }
            RouteShip ship = RouteStore.Get(member);
            if (ship != null)
            {
                // Route ships can't launch or dock with the player.
                GameAccess.InvBtnLoad(inv).SetActive(false);
                if (!ship.InReserve)
                {
                    GameAccess.InvBtnEquip(inv).SetActive(false);
                    Show(FleetButtonY, Loc.Get(ship.unassignPending ? Loc.LeavingRoute : Loc.RemoveFromRoute), Action.Remove, !ship.unassignPending);
                    return;
                }
            }
            if (inv.inStation && member.dockedStationID == inv.currStation.id && RouteSim.CanAssign(member))
            {
                Show(FleetButtonY, Loc.Get(Loc.TransferToRoute), Action.Assign, true);
            }
        }

        private static void OnSelectCargo(Inventory inv, int selected)
        {
            List<CargoItem> cargo = PlayerControl.inst.GetCargoSystem.cargo;
            if (inv.inStation && selected < cargo.Count && cargo[selected].itemType == ShipItem
                && GameAccess.InvBtnAddToFleet(inv).activeSelf)
            {
                Show(CargoButtonY, Loc.Get(Loc.RouteShipButton), Action.Commission, true);
            }
        }

        private static void Show(float y, string label, Action newAction, bool interactable)
        {
            action = newAction;
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            buttonText.text = label;
            button.GetComponent<Button>().interactable = interactable;
            button.SetActive(true);
        }

        public static void Hide()
        {
            action = Action.None;
            if (button != null)
            {
                button.SetActive(false);
            }
            picker?.Close();
        }

        private static void OnClick()
        {
            Inventory inv = inventory;
            int selected = GameAccess.InvSelectedItem(inv);
            switch (action)
            {
                case Action.Remove:
                    if (RouteStore.Get(SelectedMember(inv, selected)) is RouteShip ship)
                    {
                        RouteSim.Unassign(ship);
                    }
                    Refresh(inv);
                    break;
                case Action.Assign:
                    PlayerFleetMember member = SelectedMember(inv, selected);
                    PickRoute(Loc.Get(Loc.TransferTitle), route =>
                    {
                        if (member != null)
                        {
                            // Route ships can't be refitted; stop if this one is open in the refit screen.
                            inv.CallStopEditingFleetShip(member);
                            RouteSim.Assign(member, route);
                        }
                        Refresh(inv);
                    });
                    break;
                case Action.Commission:
                    if (!inv.currStation.HasDocking(broadcastingRequired: false))
                    {
                        Warn(Loc.Get(Loc.NeedsHangar));
                        break;
                    }
                    PickRoute(Loc.Get(Loc.ChooseRoute), route =>
                    {
                        commissionRoute = route;
                        inv.OpenAssignCaptainDialog();
                    });
                    break;
            }
        }

        private static void PickRoute(string heading, System.Action<TradeRoute> onPick)
        {
            if (RouteStore.Routes.Count == 0)
            {
                Warn(Loc.Get(Loc.NoRoutesMakeOnMap));
                return;
            }
            picker.Open(RouteStore.Routes, heading, onPick);
        }

        private static PlayerFleetMember SelectedMember(Inventory inv, int selected)
        {
            List<AIMercenaryCharacter> mercenaries = PChar.Char.mercenaries;
            return GameAccess.InvCargoMode(inv) == FleetMode && selected >= 0 && selected < mercenaries.Count
                ? mercenaries[selected] as PlayerFleetMember
                : null;
        }

        private static void Refresh(Inventory inv)
        {
            inv.RefreshIfOpen(null, resetScreen: true, alsoUpdateShipInfo: false);
        }

        /// <summary>The captain dialog closed: a hull commissioned later goes through the normal path.</summary>
        public static void ClearCommission()
        {
            commissionRoute = null;
        }

        /// <summary>
        /// Runs in place of Inventory.AddToFleet when the player chose "Route ship...". Repeats the
        /// game's steps without the Fleet Commander check, docks the ship at this station instead
        /// of launching it, and puts it on the route before anything counts capacity again.
        /// </summary>
        public static bool TryCommission(Inventory inv, CrewMember captain)
        {
            TradeRoute route = commissionRoute;
            commissionRoute = null;
            if (route == null)
            {
                return false;
            }
            CargoSystem cs = PlayerControl.inst.GetCargoSystem;
            int selected = GameAccess.InvSelectedItem(inv);
            Station station = inv.currStation;
            if (captain == null || station == null || selected < 0 || selected >= cs.cargo.Count || cs.cargo[selected].itemType != ShipItem)
            {
                return true;
            }
            CargoItem hull = cs.cargo[selected];
            ShipModelData model = ShipDB.GetModel(hull.itemID);
            captain.aiChar.shipData = hull.GetShipLoadout();
            int loadoutId = hull.shipLoadoutID;
            captain.aiChar.shipData.shipModelID = model.id;
            cs.RemoveItem(selected, 1);
            GameData.data.DeleteShipLoadout(loadoutId);
            cs.ConsumeItem(5, captain.id, 1, station.id);

            var member = new PlayerFleetMember(captain.aiChar, captain)
            {
                dockedStationID = station.id,
                sectorIndex = station.sectorIndex,
            };
            PChar.Char.mercenaries.Add(member);
            RouteSim.Assign(member, route);

            PlayerControl.inst.CalculateShip(changeWeapon: false);
            FleetControl.instance.Refresh(forceOpen: false);
            inv.RefreshIfOpen(null, resetScreen: true, alsoUpdateShipInfo: true);
            return true;
        }

        /// <summary>
        /// The fleet list only shows ships docked at this station, and a route ship only docks for a
        /// few seconds at a time. Lists every route ship elsewhere under a "Trade routes" heading,
        /// and adds each route ship's status after its name.
        /// </summary>
        public static void AfterLoadItems(Inventory inv)
        {
            if (inv != inventory || !RouteSim.GameActive || GameAccess.InvCargoMode(inv) != FleetMode || RouteStore.Ships.Count == 0)
            {
                return;
            }
            Transform panel = GameAccess.InvItemPanel(inv);
            List<AIMercenaryCharacter> mercenaries = PChar.Char.mercenaries;
            int used = 0;
            for (; used < panel.childCount && panel.GetChild(used).gameObject.activeSelf; used++)
            {
                InventorySlot slot = panel.GetChild(used).GetComponent<InventorySlot>();
                if (slot.isFleet && slot.itemIndex >= 0 && slot.itemIndex < mercenaries.Count
                    && RouteStore.Get(mercenaries[slot.itemIndex]) is RouteShip ship)
                {
                    Label(panel, used).text += StatusSuffix(ship);
                }
            }

            var elsewhere = new List<PlayerFleetMember>();
            foreach (RouteShip ship in RouteStore.Ships)
            {
                PlayerFleetMember member = RouteStore.FindMember(ship);
                if (member != null && (inv.currStation == null || member.dockedStationID != inv.currStation.id))
                {
                    elsewhere.Add(member);
                }
            }
            if (elsewhere.Count == 0)
            {
                return;
            }

            InventorySlot header = SlotAt(inv, panel, used);
            Label(panel, used).text = ColorSys.mediumGray + "<size=15>" + Loc.Get(Loc.TradeRoutes) + "</size></color>";
            header.SetTextAlignCenter();
            Icon(panel, used).enabled = false;
            RowButton(panel, used).interactable = false;
            header.itemIndex = -1;
            header.isFleet = false;
            used++;

            foreach (PlayerFleetMember member in elsewhere)
            {
                InventorySlot slot = SlotAt(inv, panel, used);
                Label(panel, used).text = member.CommanderName(14, colored: false) + StatusSuffix(RouteStore.Get(member));
                slot.SetTextAlignLeft();
                Image icon = Icon(panel, used);
                icon.sprite = member.ModelData()?.image;
                icon.enabled = icon.sprite != null;
                RowButton(panel, used).interactable = true;
                slot.itemIndex = mercenaries.IndexOf(member);
                slot.slotIndex = used;
                slot.isFleet = true;
                used++;
            }
            var size = ((RectTransform)panel).sizeDelta;
            ((RectTransform)panel).sizeDelta = new Vector2(size.x, used * 18 + 5);
        }

        private static string StatusSuffix(RouteShip ship)
        {
            return "  " + ColorSys.mediumGray + RouteSim.Status(ship) + "</color>";
        }

        // An inventory row: child 0 holds the button, its "ItemName" text and, as child 2, the icon.

        private static InventorySlot SlotAt(Inventory inv, Transform panel, int index)
        {
            if (index >= panel.childCount)
            {
                GameAccess.InvAddItemSlot(inv);
            }
            GameObject row = panel.GetChild(index).gameObject;
            row.SetActive(true);
            return row.GetComponent<InventorySlot>();
        }

        private static Text Label(Transform panel, int index)
        {
            return panel.GetChild(index).GetChild(0).GetComponentInChildren<Text>();
        }

        private static Image Icon(Transform panel, int index)
        {
            return panel.GetChild(index).GetChild(0).GetChild(2).GetComponent<Image>();
        }

        private static Button RowButton(Transform panel, int index)
        {
            return panel.GetChild(index).GetChild(0).GetComponent<Button>();
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
