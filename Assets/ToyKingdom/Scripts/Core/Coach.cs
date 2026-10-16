using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>
    /// Öğretici işaretçisi: dokunulacak yeri nabız atan bir halka ve kısa bir açıklama balonuyla gösterir.
    /// Bir dünya noktasını, bir arayüz düğmesini ya da sürüklenecek bir yolu (parmak izi gibi kayan halka) işaret edebilir.
    /// Dokunuşları engellemez; her karede hedef yeniden verilir, verilmezse kendiliğinden gizlenir.
    /// </summary>
    public class Coach : MonoBehaviour
    {
        enum Mode { None, World, Rect, Path }

        RectTransform canvasRt, ring, finger, bubble;
        TMP_Text text;
        Camera cam;
        Mode mode;
        Vector3 world, pathA, pathB;
        RectTransform rect;
        int lastFrame = -10;
        float pathT;

        public static Coach Create(string name = "Coach")
        {
            var canvas = UIFactory.Canvas(name, 60);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            var c = canvas.gameObject.AddComponent<Coach>();
            c.canvasRt = (RectTransform)canvas.transform;
            c.Build();
            return c;
        }

        void Build()
        {
            var r = UIFactory.Panel(canvasRt, "Ring", UIFactory.Hex("FFD36E"), UIFactory.RingSprite);
            r.raycastTarget = false;
            ring = r.rectTransform;
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.sizeDelta = new Vector2(120f, 120f);

            var f = UIFactory.Panel(canvasRt, "Finger", new Color(1f, 0.83f, 0.43f, 0.85f), GameArt.Instance != null ? GameArt.Instance.circle : null);
            f.raycastTarget = false;
            finger = f.rectTransform;
            finger.anchorMin = finger.anchorMax = new Vector2(0.5f, 0.5f);
            finger.sizeDelta = new Vector2(44f, 44f);

            var b = UIFactory.Chip(canvasRt, "Bubble", new Vector2(520f, 86f), 22f);
            b.raycastTarget = false;
            b.color = new Color(0.08f, 0.1f, 0.16f, 0.97f);
            bubble = b.rectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            var edge = UIFactory.Rounded(b.transform, "Gold", UIFactory.Hex("FFD36E"), 22f);
            edge.sprite = UIFactory.RingSprite;
            UIFactory.Stretch(edge.rectTransform);
            edge.raycastTarget = false;
            text = UIFactory.Text(b.transform, "Text", "", 28f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(text.rectTransform, 20f, 8f, 20f, 8f);
            text.enableAutoSizing = true; text.fontSizeMin = 18f; text.fontSizeMax = 28f;
            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            if (ring.gameObject.activeSelf != v) ring.gameObject.SetActive(v);
            if (bubble.gameObject.activeSelf != v) bubble.gameObject.SetActive(v);
            if (!v && finger.gameObject.activeSelf) finger.gameObject.SetActive(false);
        }

        void Set(Mode m, string msg)
        {
            if (m == Mode.Path && mode != Mode.Path) pathT = 0f;
            mode = m;
            lastFrame = Time.frameCount;
            if (text.text != msg) text.text = msg;
        }

        /// <summary>Haritadaki bir noktayı gösterir (bu kare için).</summary>
        public void PointWorld(Vector3 w, string msg) { world = w; Set(Mode.World, msg); }

        /// <summary>Bir arayüz öğesini gösterir (bu kare için).</summary>
        public void PointRect(RectTransform r, string msg)
        {
            if (r == null || !r.gameObject.activeInHierarchy) { Hide(); return; }
            rect = r; Set(Mode.Rect, msg);
        }

        /// <summary>a'dan b'ye sürüklenecek yolu gösterir: halka parmak gibi kayar (bu kare için).</summary>
        public void PointPath(Vector3 a, Vector3 b, string msg) { pathA = a; pathB = b; Set(Mode.Path, msg); }

        public void Hide() { mode = Mode.None; }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (mode == Mode.None || Time.frameCount - lastFrame > 1) { SetVisible(false); return; }

            Vector2 screen;
            bool showFinger = false;
            Vector2 fingerScreen = Vector2.zero;
            switch (mode)
            {
                case Mode.World:
                    if (cam == null) { SetVisible(false); return; }
                    screen = cam.WorldToScreenPoint(world);
                    break;
                case Mode.Rect:
                    screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                    break;
                default:
                    if (cam == null) { SetVisible(false); return; }
                    pathT += Time.unscaledDeltaTime / 1.8f;
                    float k = Mathf.Repeat(pathT, 1.25f); // sonda kısa bir bekleme
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
                    Vector2 a = cam.WorldToScreenPoint(pathA), b = cam.WorldToScreenPoint(pathB);
                    screen = a;
                    fingerScreen = Vector2.Lerp(a, b, e);
                    showFinger = true;
                    break;
            }

            SetVisible(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
            ring.anchoredPosition = local;
            ring.localScale = Vector3.one * pulse;

            if (finger.gameObject.activeSelf != showFinger) finger.gameObject.SetActive(showFinger);
            if (showFinger)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, fingerScreen, null, out Vector2 fl);
                finger.anchoredPosition = fl;
            }

            // balon hedefin üstünde; yer yoksa altında, ekran dışına taşmaz
            Vector2 half = canvasRt.rect.size * 0.5f;
            float w = Mathf.Clamp(text.GetPreferredValues(text.text, 900f, 80f).x + 60f, 300f, 940f);
            bubble.sizeDelta = new Vector2(w, 86f);
            Vector2 pos = local + new Vector2(0f, 120f);
            if (pos.y + 50f > half.y - 20f) pos = local - new Vector2(0f, 120f);
            pos.x = Mathf.Clamp(pos.x, -half.x + w * 0.5f + 20f, half.x - w * 0.5f - 20f);
            pos.y = Mathf.Clamp(pos.y, -half.y + 60f, half.y - 60f);
            bubble.anchoredPosition = pos;
        }
    }
}
