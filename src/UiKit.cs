using System;
using UnityEngine;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>Builds plain uGUI pieces in code: rows, labels, buttons and a scrolling list.</summary>
    internal class UiKit
    {
        public static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.09f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.13f, 0.27f, 0.33f, 0.95f);
        public static readonly Color TextColor = new Color(0.85f, 0.88f, 0.9f);
        public static readonly Color DimTextColor = new Color(0.6f, 0.65f, 0.68f);

        public const float RowHeight = 22f;

        private readonly Font font;

        public UiKit(Font font)
        {
            this.font = font;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public Text Label(Transform parent, string text, int size = 13, bool dim = false, float flexibleWidth = 1f)
        {
            RectTransform rt = Rect("Label", parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.supportRichText = true;
            label.color = dim ? DimTextColor : TextColor;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.flexibleWidth = flexibleWidth;
            return label;
        }

        public Button Button(Transform parent, string text, Action onClick, float width = -1f)
        {
            RectTransform rt = Rect("Button", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            Text label = Label(rt, text, 12);
            label.alignment = TextAnchor.MiddleCenter;
            // Translations run longer than English; shrink to fit rather than spill out.
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 12;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(label.rectTransform, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            UnityEngine.Object.Destroy(label.GetComponent<LayoutElement>());

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.preferredHeight = RowHeight;
            if (width > 0f)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
            }
            else
            {
                layout.flexibleWidth = 1f;
            }
            return button;
        }

        /// <summary>
        /// A single-line text box. <paramref name="onFocus"/> is told when typing starts (true) and
        /// stops (false), so the caller can keep keys away from the game meanwhile.
        /// </summary>
        public InputField Input(Transform parent, string text, string placeholder, int maxLength, Action<string> onEndEdit, Action<bool> onFocus)
        {
            RectTransform rt = Rect("Input", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.preferredHeight = RowHeight;
            layout.flexibleWidth = 1f;

            Text shown = Label(rt, "", 13);
            shown.alignment = TextAnchor.MiddleCenter;
            shown.supportRichText = false;
            shown.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(shown.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            UnityEngine.Object.Destroy(shown.GetComponent<LayoutElement>());

            Text hint = Label(rt, placeholder, 13, dim: true);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.fontStyle = FontStyle.Italic;
            Stretch(hint.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            UnityEngine.Object.Destroy(hint.GetComponent<LayoutElement>());

            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = shown;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = maxLength;
            input.text = text;
            input.onEndEdit.AddListener(value =>
            {
                onFocus(false);
                onEndEdit(value);
            });

            var trigger = rt.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var select = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Select };
            select.callback.AddListener(_ => onFocus(true));
            trigger.triggers.Add(select);
            var deselect = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Deselect };
            deselect.callback.AddListener(_ => onFocus(false));
            trigger.triggers.Add(deselect);
            return input;
        }

        /// <summary>A horizontal row whose children share the width.</summary>
        public static RectTransform Row(Transform parent, float spacing = 4f)
        {
            RectTransform rt = Rect("Row", parent);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        public static void Column(GameObject go, float spacing, RectOffset padding)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        /// <summary>A vertical list that scrolls with the mouse wheel. Returns the content to fill.</summary>
        public static RectTransform ScrollList(RectTransform area)
        {
            area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            RectTransform viewport = Rect("Viewport", area);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            Column(content.gameObject, 3f, new RectOffset(0, 0, 0, 4));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
