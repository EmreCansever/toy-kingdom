using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>
    /// Arayüzü koddan kurmak için yardımcılar ve oyunun görsel dili (renkler, yazı tipi, yuvarlak köşeler, gölgeler).
    /// Referans çözünürlük 1920x1080. Bütün paneller ve düğmeler buradan geçtiği için stil tek yerden değişir.
    /// </summary>
    public static class UIFactory
    {
        // ------------------------------------------------------------------ renkler

        public static readonly Color Parchment = Hex("FBF4E4");
        public static readonly Color ParchmentDark = Hex("F1E4C8");
        public static readonly Color Ink = Hex("2A2B36");
        public static readonly Color Brown = Hex("7A5432");
        public static readonly Color BrownDark = Hex("3A2817");
        public static readonly Color Gold = Hex("F2A93B");
        public static readonly Color Blue = Hex("3A7BD5");
        public static readonly Color Red = Hex("D9483B");
        public static readonly Color Green = Hex("3E9E4F");
        public static readonly Color Purple = Hex("6C5CE7");
        public static readonly Color Slate = Hex("1C2333");
        public static readonly Color Border = Hex("D8C29A");
        public static readonly Color Muted = Hex("8A7B66");
        public static readonly Color Dim = new Color(0.05f, 0.06f, 0.1f, 0.62f);
        public static readonly Color Glass = new Color(0.09f, 0.11f, 0.17f, 0.84f);

        // ------------------------------------------------------------------ yazı tipleri

        static TMP_FontAsset regular, bold;
        static Material labelShadow;

        /// <summary>Poppins Medium (gövde yazıları).</summary>
        public static TMP_FontAsset Regular => regular != null ? regular : (regular = LoadFont("Fonts/Poppins-Medium"));
        /// <summary>Poppins Bold (başlıklar ve düğmeler).</summary>
        public static TMP_FontAsset Bold => bold != null ? bold : (bold = LoadFont("Fonts/Poppins-Bold"));

        static TMP_FontAsset LoadFont(string path)
        {
            var font = Resources.Load<Font>(path);
            if (font == null) return TMP_Settings.defaultFontAsset;
            var fa = TMP_FontAsset.CreateFontAsset(font);
            if (fa == null) return TMP_Settings.defaultFontAsset;
            fa.name = font.name;
            if (TMP_Settings.defaultFontAsset != null)
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            return fa;
        }

        /// <summary>Renkli zeminlerdeki beyaz yazılar için hafif gölgeli malzeme.</summary>
        public static Material LabelShadow
        {
            get
            {
                if (labelShadow != null) return labelShadow;
                var src = Bold != null ? Bold.material : null;
                if (src == null) return null;
                labelShadow = new Material(src) { name = "LabelShadow" };
                labelShadow.EnableKeyword("UNDERLAY_ON");
                labelShadow.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.35f));
                labelShadow.SetFloat("_UnderlayOffsetX", 0f);
                labelShadow.SetFloat("_UnderlayOffsetY", -0.6f);
                labelShadow.SetFloat("_UnderlaySoftness", 0.25f);
                labelShadow.SetFloat("_UnderlayDilate", 0.1f);
                return labelShadow;
            }
        }

        // ------------------------------------------------------------------ çalışma anında üretilen sprite'lar

        const int R = 32; // temel köşe yarıçapı (piksel)
        static Sprite rounded, roundedFlat, soft, ring;

        /// <summary>Yuvarlak köşeli, hafif dikey degrade (üst açık, alt koyu) 9-dilim sprite.</summary>
        public static Sprite RoundedSprite => rounded != null ? rounded : (rounded = MakeRounded(true));
        /// <summary>Yuvarlak köşeli, düz renk 9-dilim sprite.</summary>
        public static Sprite RoundedFlatSprite => roundedFlat != null ? roundedFlat : (roundedFlat = MakeRounded(false));
        /// <summary>Yumuşak kenarlı gölge sprite'ı.</summary>
        public static Sprite SoftSprite => soft != null ? soft : (soft = MakeSoft());
        /// <summary>İçi boş yuvarlak çerçeve (seçim halkası).</summary>
        public static Sprite RingSprite => ring != null ? ring : (ring = MakeRing());

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite Finish(Texture2D tex, int border)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Sprite MakeRounded(bool gradient)
        {
            int size = R * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float h = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundBox(x + 0.5f - h, y + 0.5f - h, h, h, R);
                    float a = Mathf.Clamp01(0.5f - d);
                    float shade = gradient ? Mathf.Lerp(0.86f, 1f, (y + 0.5f) / size) : 1f;
                    px[y * size + x] = new Color(shade, shade, shade, a);
                }
            tex.SetPixels(px);
            return Finish(tex, R + 1);
        }

        static Sprite MakeSoft()
        {
            int size = 128, pad = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float h = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundBox(x + 0.5f - h, y + 0.5f - h, h - pad, h - pad, 24f);
                    float a = 1f - Mathf.SmoothStep(-6f, pad, d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            return Finish(tex, 60);
        }

        static Sprite MakeRing()
        {
            int size = R * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float h = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundBox(x + 0.5f - h, y + 0.5f - h, h, h, R);
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 6.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            return Finish(tex, R + 1);
        }

        // ------------------------------------------------------------------ temel

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

        public static RectTransform Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
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

        /// <summary>Yuvarlak köşeli düz renk kutu. radius: köşe yarıçapı (arayüz birimi).</summary>
        public static Image Rounded(Transform parent, string name, Color color, float radius = 16f, bool gradient = false)
        {
            var img = Panel(parent, name, color, gradient ? RoundedSprite : RoundedFlatSprite);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = R / Mathf.Max(1f, radius);
            return img;
        }

        /// <summary>Bir öğenin arkasına yumuşak gölge ekler (öğe ile aynı ebeveynde, hemen önünde çizilir).</summary>
        public static Image Shadow(RectTransform target, float spread = 18f, float drop = 8f, float alpha = 0.32f)
        {
            var img = Panel(target.parent, target.name + "_Shadow", new Color(0f, 0f, 0f, alpha), SoftSprite);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0f, -drop);
            rt.sizeDelta = target.sizeDelta + new Vector2(spread * 2f, spread * 2f);
            img.transform.SetSiblingIndex(target.GetSiblingIndex());
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold ? Bold : Regular;
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.lineSpacing = -6f;
            return t;
        }

        // ------------------------------------------------------------------ bileşenler

        /// <summary>
        /// Dokunsal, yuvarlak köşeli düğme: alt dudak (3B kenar), degrade yüz, parlama, gölgeli beyaz yazı.
        /// Basınca yüz aşağı iner; devre dışıyken gri olur.
        /// </summary>
        public static Button Button(Transform parent, string name, string label, Color color, float fontSize = 34f)
        {
            var root = Panel(parent, name, new Color(1f, 1f, 1f, 0f));
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            var lip = Rounded(root.transform, "Lip", Darken(color, 0.62f), 18f);
            Stretch(lip.rectTransform);
            lip.raycastTarget = false;
            var face = Rounded(root.transform, "Face", color, 18f, true);
            Stretch(face.rectTransform, 0f, 7f, 0f, 0f);
            face.raycastTarget = false;
            var shine = Rounded(face.transform, "Shine", new Color(1f, 1f, 1f, 0.22f), 14f);
            var srt = shine.rectTransform;
            srt.anchorMin = new Vector2(0f, 0.55f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(6f, 0f);
            srt.offsetMax = new Vector2(-6f, -4f);
            shine.raycastTarget = false;

            bool lightBg = color.grayscale > 0.62f;
            var t = Text(face.transform, "Label", label, fontSize, lightBg ? BrownDark : Color.white, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform, 10f, 2f, 10f, 2f);
            t.enableAutoSizing = true;
            t.fontSizeMax = fontSize;
            t.fontSizeMin = Mathf.Min(fontSize, 16f);
            if (!lightBg && LabelShadow != null) t.fontSharedMaterial = LabelShadow;

            var style = root.gameObject.AddComponent<TKButton>();
            style.Init(btn, face, lip, t, color);
            return btn;
        }

        /// <summary>Gölgeli, yuvarlak köşeli kart pencere. Dönen Image kökün kendisidir (gölge); içerik onun üstüne eklenir.</summary>
        public static Image Frame(Transform parent, string name, Vector2 size)
        {
            var root = Panel(parent, name, new Color(0f, 0f, 0f, 0.28f), SoftSprite);
            root.type = Image.Type.Sliced;
            root.rectTransform.sizeDelta = size;
            var border = Rounded(root.transform, "Border", Border, 26f);
            Stretch(border.rectTransform, 10f, 14f, 10f, 6f);
            var inner = Rounded(border.transform, "Inner", Parchment, 23f, true);
            Stretch(inner.rectTransform, 3f);
            return root;
        }

        /// <summary>Frame + renkli başlık şeridi.</summary>
        public static Image Window(Transform parent, string name, Vector2 size, string title, Color accent, out TMP_Text titleText, float headerHeight = 96f)
        {
            var root = Frame(parent, name, size);
            var border = root.transform.Find("Border");
            var head = Rounded(border, "Header", accent, 23f, true);
            var hrt = head.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(3f, -headerHeight);
            hrt.offsetMax = new Vector2(-3f, -3f);
            // başlığın alt köşeleri düz kalsın
            var flat = Panel(head.transform, "Flat", accent);
            var frt = flat.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = new Vector2(0f, 26f);
            flat.color = Darken(accent, 0.88f);
            flat.transform.SetAsFirstSibling();
            titleText = Text(head.transform, "Title", title, 46f, Color.white, TextAlignmentOptions.Left, true);
            Stretch(titleText.rectTransform, 34f, 0f, 220f, 0f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMax = 46f;
            titleText.fontSizeMin = 26f;
            if (LabelShadow != null) titleText.fontSharedMaterial = LabelShadow;
            return root;
        }

        /// <summary>Koyu yarı saydam hap (üst bilgi çubuğu öğeleri, isim plakaları, bildirimler).</summary>
        public static Image Chip(Transform parent, string name, Vector2 size, float radius = 0f)
        {
            var img = Rounded(parent, name, Glass, radius > 0f ? radius : size.y * 0.5f);
            img.rectTransform.sizeDelta = size;
            var edge = Rounded(img.transform, "Edge", new Color(1f, 1f, 1f, 0.10f), radius > 0f ? radius : size.y * 0.5f);
            edge.sprite = RingSprite;
            Stretch(edge.rectTransform);
            edge.raycastTarget = false;
            return img;
        }

        /// <summary>Yuvarlak ilerleme çubuğu. Değeri SetBar ile ver.</summary>
        public static Image Bar(Transform parent, string name, Color fill, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var bg = Rounded(parent, name, new Color(0f, 0f, 0f, 0.28f), size.y * 0.5f);
            Place(bg.rectTransform, anchor, anchor, pos, size);
            bg.raycastTarget = false;
            var f = Rounded(bg.transform, "Fill", fill, (size.y - 6f) * 0.5f, true);
            f.raycastTarget = false;
            var rt = f.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
            return f;
        }

        public static void SetBar(Image fill, float value)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            float v = Mathf.Clamp01(value);
            rt.anchorMax = new Vector2(v, 1f);
            rt.offsetMax = new Vector2(-3f, -3f);
            fill.enabled = v > 0.02f;
        }

        public static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static Color Lighten(Color c, float k) => Color.Lerp(c, Color.white, k);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }

    /// <summary>UIFactory düğmelerinin basılma ve devre dışı görünümü.</summary>
    public class TKButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Button button;
        Image face, lip;
        TMP_Text label;
        Color baseColor;
        Color labelColor;
        bool pressed;
        bool lastInteractable = true;
        RectTransform faceRt;

        public void Init(Button b, Image f, Image l, TMP_Text t, Color c)
        {
            button = b; face = f; lip = l; label = t; baseColor = c;
            labelColor = t.color;
            faceRt = f.rectTransform;
            Apply();
        }

        /// <summary>Düğmenin rengini sonradan değiştirir (ör. seçili sekme).</summary>
        public void SetColor(Color c)
        {
            baseColor = c;
            labelColor = c.grayscale > 0.62f ? UIFactory.BrownDark : Color.white;
            if (label != null) label.color = labelColor;
            Apply();
        }

        void Update()
        {
            if (button != null && button.interactable != lastInteractable) Apply();
        }

        void Apply()
        {
            if (button == null) return;
            lastInteractable = button.interactable;
            Color c = lastInteractable ? baseColor : Color.Lerp(new Color(0.62f, 0.62f, 0.64f), baseColor, 0.15f);
            face.color = c;
            lip.color = UIFactory.Darken(c, 0.62f);
            if (label != null) label.color = lastInteractable ? labelColor : new Color(1f, 1f, 1f, 0.8f);
            SetPressed(false);
        }

        void SetPressed(bool on)
        {
            pressed = on;
            if (faceRt == null) return;
            faceRt.offsetMin = new Vector2(0f, on ? 2f : 7f);
            faceRt.offsetMax = new Vector2(0f, on ? -5f : 0f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (button != null && button.interactable) SetPressed(true);
        }

        public void OnPointerUp(PointerEventData e) => SetPressed(false);
        public void OnPointerExit(PointerEventData e) { if (pressed) SetPressed(false); }
    }
}
