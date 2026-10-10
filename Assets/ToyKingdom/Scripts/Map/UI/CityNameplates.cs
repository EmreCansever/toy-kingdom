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
            var bg = UIFactory.Chip(canvasRt, "Plate_" + city.CityId, new Vector2(260f, 74f), 22f);
            bg.raycastTarget = false;
            p.bg = bg;
            p.rt = bg.rectTransform;
            p.rt.pivot = new Vector2(0.5f, 0f);

            p.dot = UIFactory.Rounded(bg.transform, "Dot", Color.white, 9f);
            UIFactory.Place(p.dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -22f), new Vector2(18f, 18f));
            p.dot.raycastTarget = false;

            p.name = UIFactory.Text(bg.transform, "Name", city.DisplayName, 27f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(p.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -2f), new Vector2(400f, 40f));
            p.name.enableWordWrapping = false;

            p.status = UIFactory.Text(bg.transform, "Status", "", 19f, new Color(1f, 1f, 1f, 0.78f), TextAlignmentOptions.Left);
            UIFactory.Place(p.status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 7f), new Vector2(400f, 28f));
            p.status.enableWordWrapping = false;

            // zorluk noktaları (düşman şehirlerinde)
            p.pips = UIFactory.Rect("Pips", bg.transform);
            UIFactory.Place(p.pips, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(90f, 14f));
            for (int i = 0; i < 5; i++)
            {
                var pip = UIFactory.Rounded(p.pips, "Pip" + i, Color.white, 5f);
                UIFactory.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 17f, 0f), new Vector2(12f, 12f));
                pip.raycastTarget = false;
            }

            // küçük işaretçi üçgen yerine alt kenarda ince çizgi
            var stem = UIFactory.Rounded(bg.transform, "Stem", UIFactory.Glass, 3f);
            UIFactory.Place(stem.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(6f, 16f));
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

            var col = StatusColor(c.Status);
            p.dot.color = col;
            p.name.text = c.DisplayName;
            p.status.text = text;
            p.status.color = c.Status == CityStatus.Rebel || c.Status == CityStatus.Warning ? col : new Color(1f, 1f, 1f, 0.78f);

            bool enemy = c.Status == CityStatus.Attackable || c.Status == CityStatus.Far || c.Status == CityStatus.Rebel;
            p.pips.gameObject.SetActive(enemy && c.Difficulty > 0);
            for (int i = 0; i < p.pips.childCount; i++)
            {
                var img = p.pips.GetChild(i).GetComponent<Image>();
                img.color = i < c.Difficulty ? col : new Color(1f, 1f, 1f, 0.18f);
            }

            float w = Mathf.Max(p.name.GetPreferredValues(c.DisplayName).x + 64f,
                p.status.GetPreferredValues(text).x + 40f + (p.pips.gameObject.activeSelf ? 100f : 0f));
            p.rt.sizeDelta = new Vector2(Mathf.Clamp(w, 170f, 520f), 74f);
            p.bg.color = c.Status == CityStatus.Far ? new Color(0.09f, 0.11f, 0.17f, 0.62f) : UIFactory.Glass;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // uzaklaştıkça küçül, yakınlaştıkça biraz büyü
            float scale = Mathf.Clamp(3.2f / cam.orthographicSize, 0.62f, 1.05f);
            Vector2 half = canvasRt.rect.size * 0.5f;
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
            }
        }
    }
}
