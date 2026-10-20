using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>
    /// The galaxy map's trade route panel, on the left under the map toggles. It edits one route
    /// at a time: pick it with the arrows, add stations from the selected sector, reorder or
    /// remove stops. Ships are assigned in a station's Hangar, not here.
    /// </summary>
    internal class MapPanel : MonoBehaviour
    {
        private const string PanelName = "TradeRoutesPanel";
        private const float StatusRefreshSeconds = 1f;

        // Positions in the map canvas: the map's own toggles and slider end about 85 below the top.
        private static readonly Vector2 TogglePos = new Vector2(5f, -92f);
        private static readonly Vector2 PanelPos = new Vector2(5f, -118f);
        private static readonly Vector2 PanelSize = new Vector2(340f, 560f);

        public static MapPanel Instance;

        private GalaxyMap map;
        private UiKit ui;
        private RectTransform panel;
        private RectTransform content;
        private Text toggleLabel;

        /// <summary>The route the panel is editing, or null when there are none.</summary>
        private TradeRoute editing;

        private InputField nameField;
        private bool typing;

        private readonly List<KeyValuePair<Text, RouteShip>> shipLines = new List<KeyValuePair<Text, RouteShip>>();
        private float nextStatus;

        public TradeRoute Editing => editing;

        public static void Create(GalaxyMap map)
        {
            Transform canvas = map.selCanvas.transform;
            if (canvas.Find(PanelName) != null)
            {
                return;
            }
            Transform influence = canvas.Find("FactionInfluenceText");
            Text reference = influence != null ? influence.GetComponent<Text>() : null;
            if (reference == null)
            {
                reference = canvas.GetComponentInChildren<Text>(true);
            }
            RectTransform root = UiKit.Rect(PanelName, canvas);
            Instance = root.gameObject.AddComponent<MapPanel>();
            Instance.Build(map, new UiKit(reference.font));
        }

        private void Build(GalaxyMap galaxyMap, UiKit kit)
        {
            map = galaxyMap;
            ui = kit;
            var root = (RectTransform)transform;
            Stretch(root);

            RectTransform toggleRow = UiKit.Row(root);
            Place(toggleRow, TogglePos, new Vector2(150f, UiKit.RowHeight));
            Button toggle = ui.Button(toggleRow, "", TogglePanel);
            toggleLabel = toggle.GetComponentInChildren<Text>();

            panel = UiKit.Rect("Panel", root);
            Place(panel, PanelPos, PanelSize);
            panel.gameObject.AddComponent<Image>().color = UiKit.PanelColor;
            RectTransform listArea = UiKit.Rect("List", panel);
            UiKit.Stretch(listArea, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            content = UiKit.ScrollList(listArea);

            // Keep the mouse wheel and drags over the panel from moving the map.
            var trigger = panel.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => map.BlockMapMovement(true));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => map.BlockMapMovement(false));

            map.OnSectorSelect += Rebuild;
            RouteEditor.Changed += OnRoutesChanged;
            ApplyOpenState();
        }

        private void OnDestroy()
        {
            RouteEditor.Changed -= OnRoutesChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static void Stretch(RectTransform rt)
        {
            UiKit.Stretch(rt, Vector2.zero, Vector2.zero);
        }

        private static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void TogglePanel()
        {
            Plugin.MapPanelOpen.Value = !Plugin.MapPanelOpen.Value;
            ApplyOpenState();
        }

        private void ApplyOpenState()
        {
            bool open = Plugin.MapPanelOpen.Value;
            panel.gameObject.SetActive(open);
            toggleLabel.text = Loc.Get(Loc.TradeRoutes) + (open ? "  [-]" : "  [+]");
            if (!open)
            {
                map.BlockMapMovement(false);
            }
            Rebuild();
        }

        /// <summary>The game language changed.</summary>
        public void RefreshLanguage()
        {
            ApplyOpenState();
        }

        /// <summary>Called when the galaxy map opens.</summary>
        public void OnMapOpened()
        {
            gameObject.SetActive(RouteSim.GameActive);
            Rebuild();
        }

        private void OnRoutesChanged()
        {
            Rebuild();
            RouteLines.Redraw(map, editing);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextStatus)
            {
                return;
            }
            nextStatus = Time.unscaledTime + StatusRefreshSeconds;
            foreach (KeyValuePair<Text, RouteShip> line in shipLines)
            {
                if (line.Key != null)
                {
                    line.Key.text = ShipLine(line.Value);
                }
            }
        }

        public void Rebuild()
        {
            if (!panel.gameObject.activeInHierarchy || !RouteSim.GameActive)
            {
                return;
            }
            if (editing != null && !RouteStore.Routes.Contains(editing))
            {
                editing = null;
            }
            if (editing == null && RouteStore.Routes.Count > 0)
            {
                editing = RouteStore.Routes[0];
            }
            // Destroying a focused name box sends no deselect, so give the keyboard back here.
            if (typing)
            {
                SetTyping(false);
            }
            UiKit.Clear(content);
            shipLines.Clear();
            BuildRouteHeader();
            if (editing != null)
            {
                BuildStops();
                BuildShips();
            }
            BuildSectorStations();
        }

        private void BuildRouteHeader()
        {
            Heading(Loc.Get(Loc.TradeRoutes));
            if (editing == null)
            {
                ui.Label(content, Loc.Get(Loc.NoRoutesYet), 12, dim: true);
                return;
            }
            RectTransform row = UiKit.Row(content);
            ui.Button(row, "<", () => Step(-1), 26f);
            // The route's name, editable in place; Rename below focuses it.
            TradeRoute route = editing;
            nameField = ui.Input(row, route.customName ?? "", route.DefaultName, RouteEditor.MaxNameLength,
                value => RouteEditor.Rename(route, value), SetTyping);
            nameField.textComponent.color = RouteLines.ColorFor(route);
            nameField.textComponent.fontStyle = FontStyle.Bold;
            Text count = ui.Label(row, $"{RouteStore.Routes.IndexOf(editing) + 1}/{RouteStore.Routes.Count}", 11, dim: true, flexibleWidth: 0f);
            count.alignment = TextAnchor.MiddleCenter;
            count.GetComponent<LayoutElement>().minWidth = 30f;
            ui.Button(row, ">", () => Step(1), 26f);

            // Whether it loops shows in the stop list itself; this just says what that means.
            ui.Label(content, Loc.Get(editing.circular ? Loc.LoopExplained : Loc.ThereAndBackExplained), 12, dim: true);
            row = UiKit.Row(content);
            ui.Button(row, Loc.Get(Loc.Rename), () =>
            {
                nameField.Select();
                nameField.ActivateInputField();
            });
            ui.Button(row, Loc.Get(Loc.DeleteRoute), () => RouteEditor.DeleteRoute(editing));
        }

        /// <summary>
        /// While the player types a name, keys must not also act as game shortcuts or move the
        /// map; the game's own map note field does the same.
        /// </summary>
        private void SetTyping(bool on)
        {
            typing = on;
            if (PlayerControl.inst != null)
            {
                PlayerControl.inst.blockKeyboard = on;
            }
        }

        private void OnDisable()
        {
            // The map closed with the name box still focused.
            if (typing)
            {
                SetTyping(false);
            }
        }

        private void Step(int direction)
        {
            List<TradeRoute> routes = RouteStore.Routes;
            if (routes.Count == 0)
            {
                return;
            }
            int index = (routes.IndexOf(editing) + direction + routes.Count) % routes.Count;
            editing = routes[index];
            Rebuild();
            RouteLines.Redraw(map, editing);
        }

        private void BuildStops()
        {
            Heading(Loc.Get(Loc.Stops));
            List<int> stops = editing.stations;
            if (stops.Count < 2)
            {
                ui.Label(content, Loc.Get(Loc.NeedsTwoStops), 12, dim: true);
            }
            for (int i = 0; i < stops.Count; i++)
            {
                int index = i;
                int stationId = stops[i];
                Station station = GameData.GetStation(stationId, allowDestroyedOrWrecked: true);
                RectTransform row = UiKit.Row(content);
                string state = RouteStations.IsUsable(RouteStations.Get(stationId)) ? "" : "  <color=#d08a3a>" + Loc.Get(Loc.NotUsable) + "</color>";
                Button name = ui.Button(row, $"{i + 1}. {RouteStations.Name(stationId)}{state}", () => ShowSector(station));
                name.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                Button up = ui.Button(row, Loc.Get(Loc.Up), () => RouteEditor.MoveUp(editing, index), 34f);
                up.interactable = i > 0;
                ui.Button(row, Loc.Get(Loc.Remove), () => RouteEditor.RemoveStop(editing, index), 62f);
                if (RouteEditor.CanSplitAt(editing, i))
                {
                    ui.Button(row, Loc.Get(Loc.Split), () => RouteEditor.SplitAt(editing, index), 44f);
                }
                if (i > 0)
                {
                    GateWarning(stops[i - 1], stationId);
                }
            }
            if (editing.circular && stops.Count > 0)
            {
                // The closing leg, shown as the stop the loop returns to.
                RectTransform row = UiKit.Row(content);
                Station first = GameData.GetStation(stops[0], allowDestroyedOrWrecked: true);
                Button back = ui.Button(row, "   " + Loc.Get(Loc.BackTo, RouteStations.Name(stops[0])), () => ShowSector(first));
                back.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                ui.Button(row, Loc.Get(Loc.OpenLoop), () => RouteEditor.OpenLoop(editing), 80f);
                GateWarning(stops[stops.Count - 1], stops[0]);
            }
            else if (RouteEditor.CanCloseLoop(editing))
            {
                RectTransform row = UiKit.Row(content);
                ui.Button(row, Loc.Get(Loc.CloseLoopBackTo, RouteStations.Name(stops[0])), () => RouteEditor.CloseLoop(editing));
            }
            if (stops.Count > 1)
            {
                ui.Label(content, Loc.Get(editing.circular ? Loc.RemoveHelpLoop : Loc.RemoveHelp), 11, dim: true);
            }
        }

        /// <summary>
        /// Adds a warning line when no chain of jump gates links two consecutive stops, so ships can
        /// never fly that leg. Nothing when they're linked or in the same sector.
        /// </summary>
        private void GateWarning(int fromId, int toId)
        {
            Station from = GameData.GetStation(fromId, allowDestroyedOrWrecked: true);
            Station to = GameData.GetStation(toId, allowDestroyedOrWrecked: true);
            if (from == null || to == null || GateGraph.SectorPath(from.sectorIndex, to.sectorIndex) != null)
            {
                return;
            }
            ui.Label(content, "      <color=#d08a3a>" + Loc.Get(Loc.NoGateRouteFrom, from.stationName(withLevel: false)) + "</color>", 11);
        }

        private void BuildShips()
        {
            Heading(Loc.Get(Loc.Ships));
            bool any = false;
            foreach (RouteShip ship in RouteStore.Ships)
            {
                if (ship.routeId != editing.id)
                {
                    continue;
                }
                any = true;
                Text line = ui.Label(content, ShipLine(ship), 12);
                shipLines.Add(new KeyValuePair<Text, RouteShip>(line, ship));
            }
            if (!any)
            {
                ui.Label(content, Loc.Get(Loc.NoShips), 12, dim: true);
            }
        }

        private static string ShipLine(RouteShip ship)
        {
            PlayerFleetMember member = RouteStore.FindMember(ship);
            string name = member == null ? "?" : member.CommanderName(12, colored: false);
            return $"{name}  <color=#9aa4aa>{RouteSim.Status(ship)}</color>";
        }

        private void BuildSectorStations()
        {
            TSector sector = map.GetSelectedSector();
            if (sector == null)
            {
                Heading(Loc.Get(Loc.Stations));
                ui.Label(content, Loc.Get(Loc.SelectSector), 12, dim: true);
                return;
            }
            Heading(Loc.Get(Loc.StationsIn, sector.coords));
            bool any = false;
            if (sector.stationIDs != null)
            {
                foreach (int id in sector.stationIDs)
                {
                    Station station = GameData.GetStation(id, allowDestroyedOrWrecked: false);
                    if (!RouteStations.CanJoinRoute(station))
                    {
                        continue;
                    }
                    any = true;
                    BuildStationRow(station);
                }
            }
            if (!any)
            {
                ui.Label(content, Loc.Get(Loc.NoTradingStations), 12, dim: true);
            }
        }

        private void BuildStationRow(Station station)
        {
            RectTransform row = UiKit.Row(content);
            string owner = station.PlayerOwned ? "<color=#7fc8ff>" + Loc.Get(Loc.OwnerYou) + "</color>" : Loc.Get(Loc.OwnerNpc);
            ui.Label(row, $"{station.stationName(withLevel: false)}  <size=11>{owner}</size>", 12);
            if (editing != null)
            {
                int stop = editing.stations.IndexOf(station.id);
                if (stop == 0 && RouteEditor.CanCloseLoop(editing))
                {
                    // Adding the first stop again closes the route into a loop.
                    ui.Button(row, Loc.Get(Loc.CloseLoop), () => RouteEditor.CloseLoop(editing), 80f);
                }
                else
                {
                    Button add = ui.Button(row, stop >= 0 ? Loc.Get(Loc.StopNumber, stop + 1) : Loc.Get(Loc.Add), () => RouteEditor.AddStop(editing, station.id), 54f);
                    add.interactable = stop < 0;
                }
            }
            ui.Button(row, Loc.Get(Loc.New), () =>
            {
                editing = RouteEditor.NewRoute(station.id);
                Rebuild();
                RouteLines.Redraw(map, editing);
            }, 42f);
        }

        private void Heading(string text)
        {
            ui.Label(content, $"<b>{text}</b>", 14);
        }

        private void ShowSector(Station station)
        {
            if (station != null)
            {
                map.MoveCameraTo(station.Sector, selectSector: true);
            }
        }
    }
}
