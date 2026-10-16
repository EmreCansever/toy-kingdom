using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>
    /// Şehir isim plakaları: her kalenin üstünde, ekran uzayında keskin ve okunaklı duran koyu haplar.
    /// Renkli nokta şehrin durumunu, alt satır geliri ya da zorluğu gösterir. Dokunuşları engellemez.
    /// </summary>
    public class CityNameplates : MonoBehaviour
    {
        class Plate
        {
            public MapCity city;
            public RectTransform rt;
            public Image bg;
            public Image dot;
            public TMP_Text name;
            public TMP_Text status;
            public RectTransform pips;
            public CityStatus lastStatus = (CityStatus)(-1);
            public string lastText;
        }

        readonly List<Plate> plates = new List<Plate>();
        RectTransform canvasRt;
        Camera cam;

        public static CityNameplates Create(MapCity[] cities)
        {
            var canvas = UIFactory.Canvas("CityNameplates", 5);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            var np = canvas.gameObject.AddComponent<CityNameplates>();
            np.canvasRt = (RectTransform)canvas.transform;
            foreach (var c in cities) np.plates.Add(np.Build(c));
            return np;
        }

        Plate Build(MapCity city)
        {
            var p = new Plate { city = city };
            var bg = UIFactory.Chip(canvasRt, "Plate_" + city.CityId, new Vector2(300f, 96f), 26f);
            bg.raycastTarget = false;
            p.bg = bg;
            p.rt = bg.rectTransform;
            p.rt.pivot = new Vector2(0.5f, 0f);

            p.dot = UIFactory.Rounded(bg.transform, "Dot", Color.white, 11f);
            UIFactory.Place(p.dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, -29f), new Vector2(22f, 22f));
            p.dot.raycastTarget = false;

            p.name = UIFactory.Text(bg.transform, "Name", city.DisplayName, 36f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(p.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -4f), new Vector2(460f, 52f));
            p.name.textWrappingMode = TextWrappingModes.NoWrap;
            if (UIFactory.LabelShadow != null) p.name.fontSharedMaterial = UIFactory.LabelShadow;

            p.status = UIFactory.Text(bg.transform, "Status", "", 25f, new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.Left);
            UIFactory.Place(p.status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 9f), new Vector2(460f, 36f));
            p.status.textWrappingMode = TextWrappingModes.NoWrap;

            // zorluk noktaları (düşman şehirlerinde)
            p.pips = UIFactory.Rect("Pips", bg.transform);
            UIFactory.Place(p.pips, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 19f), new Vector2(112f, 16f));
            for (int i = 0; i < 5; i++)
            {
                var pip = UIFactory.Rounded(p.pips, "Pip" + i, Color.white, 7f);
                UIFactory.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 22f, 0f), new Vector2(17f, 17f));
                pip.raycastTarget = false;
            }

            // küçük işaretçi üçgen yerine alt kenarda ince çizgi
            var stem = UIFactory.Rounded(bg.transform, "Stem", UIFactory.Glass, 3f);
            UIFactory.Place(stem.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(7f, 18f));
            stem.raycastTarget = false;
            return p;
        }

        static Color StatusColor(CityStatus s)
        {
            switch (s)
            {
                case CityStatus.Home: return UIFactory.Gold;
                case CityStatus.Owned: return UIFactory.Hex("5AA2FF");
                case CityStatus.Warning: return UIFactory.Hex("FFB443");
                case CityStatus.Attackable: return UIFactory.Hex("FF5E4D");
                case CityStatus.Rebel: return UIFactory.Hex("FF3B30");
                case CityStatus.Threat: return UIFactory.Hex("FF3B30");
                default: return UIFactory.Hex("9AA0AA");
            }
        }

        void Refresh(Plate p)
        {
            var c = p.city;
            string text = c.StatusText;
            if (p.lastStatus == c.Status && p.lastText == text) return;
            p.lastStatus = c.Status;
            p.lastText = text;

            var col = c.Status == CityStatus.Rival && c.RivalId != null ? RivalAI.Get(c.RivalId).color : StatusColor(c.Status);
            p.dot.color = col;
            p.name.text = c.DisplayName;
            p.status.text = text;
            // renkli durum yazısı biraz beyaza çekilir: koyu zeminde göz yormaz, kum ve karda da okunur
            Color soft = Color.Lerp(col, Color.white, 0.3f);
            p.status.color = c.Status == CityStatus.Rebel || c.Status == CityStatus.Warning || c.Status == CityStatus.Threat || c.Status == CityStatus.Rival ? soft : new Color(1f, 1f, 1f, 0.9f);

            bool enemy = c.Status == CityStatus.Attackable || c.Status == CityStatus.Far || c.Status == CityStatus.Rebel || c.Status == CityStatus.Rival;
            p.pips.gameObject.SetActive(enemy && c.Difficulty > 0);
            for (int i = 0; i < p.pips.childCount; i++)
            {
                var img = p.pips.GetChild(i).GetComponent<Image>();
                // uzak şehirlerin gri noktası koyu zeminde kayboluyordu: dolu noktalar açık, boşlar belirgin
                Color on = c.Status == CityStatus.Far ? UIFactory.Hex("E3E7EE") : soft;
                img.color = i < c.Difficulty ? on : new Color(1f, 1f, 1f, 0.26f);
            }

            float w = Mathf.Max(p.name.GetPreferredValues(c.DisplayName).x + 80f,
                p.status.GetPreferredValues(text).x + 50f + (p.pips.gameObject.activeSelf ? 128f : 0f));
            p.rt.sizeDelta = new Vector2(Mathf.Clamp(w, 210f, 640f), 96f);
            // plaka hemen hemen opak: açık renkli çöl ve kar zemininde de yazılar okunur
            p.bg.color = c.Status == CityStatus.Far ? new Color(0.19f, 0.21f, 0.27f, 1f) : new Color(0.08f, 0.1f, 0.16f, 1f);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // uzaklaştıkça küçül, yakınlaştıkça biraz büyü
            float scale = Mathf.Clamp(3.6f / cam.orthographicSize, 0.8f, 1.15f);
            Vector2 half = canvasRt.rect.size * 0.5f;
            shown.Clear();
            foreach (var p in plates)
            {
                if (p.city == null) continue;
                Refresh(p);
                Vector3 sp = cam.WorldToScreenPoint(p.city.LabelAnchor);
                bool visible = sp.z > 0f && sp.x > -200f && sp.x < Screen.width + 200f && sp.y > -100f && sp.y < Screen.height + 200f;
                if (p.rt.gameObject.activeSelf != visible) p.rt.gameObject.SetActive(visible);
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 local);
                p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
                p.rt.anchoredPosition = local;
                p.rt.localScale = Vector3.one * scale;
                shown.Add(p);
            }
            Declutter(scale);
        }

        readonly List<Plate> shown = new List<Plate>();

        /// <summary>Birbirine yakın şehirlerin plakaları üst üste binmesin: üstteki plaka yukarı itilir.</summary>
        void Declutter(float scale)
        {
            shown.Sort((a, b) => a.rt.anchoredPosition.y.CompareTo(b.rt.anchoredPosition.y));
            const float gap = 6f;
            for (int i = 1; i < shown.Count; i++)
            {
                var a = shown[i];
                for (int pass = 0; pass < 3; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        var b = shown[j];
                        Vector2 pa = a.rt.anchoredPosition, pb = b.rt.anchoredPosition;
                        float wa = a.rt.sizeDelta.x * scale, wb = b.rt.sizeDelta.x * scale;
                        float ha = a.rt.sizeDelta.y * scale, hb = b.rt.sizeDelta.y * scale;
                        bool overlapX = Mathf.Abs(pa.x - pb.x) < (wa + wb) * 0.5f;
                        bool overlapY = pa.y < pb.y + hb + gap && pb.y < pa.y + ha;
                        if (overlapX && overlapY)
                        {
                            a.rt.anchoredPosition = new Vector2(pa.x, pb.y + hb + gap);
                            moved = true;
                        }
                    }
                    if (!moved) break;
                }
            }
        }
    }
}
