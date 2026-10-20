using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>
    /// A small list of trade routes over the inventory's button column, built from the game's own
    /// button so it matches. Picking one calls back and closes the list.
    /// </summary>
    internal class RoutePicker
    {
        /// <summary>More than this and the list would run off the bottom of the inventory.</summary>
        private const int MaxRows = 13;

        private const float Width = 240f;
        private const float RowHeight = 26f;
        private const float RowSpacing = 4f;
        private const float Padding = 6f;

        private readonly GameObject root;
        private readonly GameObject buttonTemplate;
        private readonly Text title;
        private readonly Transform list;
        private readonly List<GameObject> rows = new List<GameObject>();

        public bool IsOpen => root.activeSelf;

        /// <param name="parent">The inventory panel the buttons live in.</param>
        /// <param name="template">A game button to copy for each row.</param>
        /// <param name="topLeft">Where the list's top-left corner goes, in the buttons' coordinates.</param>
        public RoutePicker(Transform parent, GameObject template, Vector2 topLeft)
        {
            buttonTemplate = template;
            root = new GameObject("TradeRoutePicker", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            var templateRT = (RectTransform)template.transform;
            rt.anchorMin = templateRT.anchorMin;
            rt.anchorMax = templateRT.anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            // Blocks clicks to the buttons underneath while open.
            root.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.96f);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            layout.spacing = RowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rt.sizeDelta = new Vector2(Width, 0f);

            Text templateText = template.GetComponentInChildren<Text>(true);
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(root.transform, false);
            title = titleGO.AddComponent<Text>();
            title.font = templateText.font;
            title.fontSize = templateText.fontSize;
            title.color = templateText.color;
            title.alignment = TextAnchor.MiddleCenter;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 9;
            title.resizeTextMaxSize = templateText.fontSize;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            titleGO.AddComponent<LayoutElement>().preferredHeight = RowHeight;

            list = root.transform;
            root.SetActive(false);
        }

        public void Open(IList<TradeRoute> routes, string heading, Action<TradeRoute> onPick)
        {
            title.text = heading;
            foreach (GameObject row in rows)
            {
                UnityEngine.Object.Destroy(row);
            }
            rows.Clear();
            int shown = Mathf.Min(routes.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                TradeRoute route = routes[i];
                AddRow(Describe(route), () =>
                {
                    Close();
                    onPick(route);
                });
            }
            if (routes.Count > shown)
            {
                AddRow(Loc.Get(Loc.MoreRoutes, routes.Count - shown), null);
            }
            AddRow(Loc.Get(Loc.Cancel), Close);
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        public void Close()
        {
            root.SetActive(false);
        }

        private void AddRow(string label, Action onClick)
        {
            GameObject row = UnityEngine.Object.Instantiate(buttonTemplate, list, false);
            row.name = "Route";
            HangarUI.StripGameHandlers(row);
            row.SetActive(true);
            row.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            Text text = row.GetComponentInChildren<Text>(true);
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Button button = row.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
            else
            {
                button.interactable = false;
            }
            rows.Add(row);
        }

        /// <summary>"Route 2: Alpha – Beta – Gamma", shortened to fit a row.</summary>
        private static string Describe(TradeRoute route)
        {
            var names = new List<string>();
            foreach (int id in route.stations)
            {
                Station station = GameData.GetStation(id, allowDestroyedOrWrecked: true);
                names.Add(station == null ? "?" : station.stationName(withLevel: false));
            }
            string text = route.DisplayName + ": " + string.Join(" - ", names);
            const int MaxChars = 34;
            return text.Length <= MaxChars ? text : text.Substring(0, MaxChars - 3) + "...";
        }
    }
}
