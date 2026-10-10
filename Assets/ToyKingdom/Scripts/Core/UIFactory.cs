using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Arayüzü koddan kurmak için küçük yardımcılar. Referans çözünürlük 1920x1080.</summary>
    public static class UIFactory
    {
        public static readonly Color Parchment = Hex("F3E6C8");
        public static readonly Color ParchmentDark = Hex("E2CFA4");
        public static readonly Color Ink = Hex("2C2C2A");
        public static readonly Color Brown = Hex("6B3E12");
        public static readonly Color BrownDark = Hex("412402");
        public static readonly Color Gold = Hex("EF9F27");
        public static readonly Color Blue = Hex("2F6FB3");
        public static readonly Color Red = Hex("B8382F");
        public static readonly Color Green = Hex("3B6D11");
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.5f);

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#endif
        }

        public static Canvas Canvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color color, float fontSize = 34f)
        {
            var img = Panel(parent, name, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            btn.colors = colors;
            var t = Text(img.transform, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform, 4f);
            return btn;
        }

        /// <summary>Kenarlıklı parşömen kutu (iki katmanlı).</summary>
        public static Image Frame(Transform parent, string name, Vector2 size)
        {
            var outer = Panel(parent, name, BrownDark);
            outer.rectTransform.sizeDelta = size;
            var inner = Panel(outer.transform, "Inner", Parchment);
            Stretch(inner.rectTransform, 6f);
            return outer;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
