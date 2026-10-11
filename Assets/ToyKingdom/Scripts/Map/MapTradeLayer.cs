using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Haritaya dokunulabilir yol işareti (orta noktada).</summary>
    public class MapRoadTap : MonoBehaviour
    {
        public string key;
    }

    /// <summary>
    /// Ticaret katmanı: yol güvenliği etiketleri, yolda giden kervanlar, eşkıya baskınları ve devriyeler.
    /// Durumu kayıttan okur; hiçbir kararı kendisi vermez.
    /// </summary>
    public class MapTradeLayer : MonoBehaviour
    {
        class RoadView
        {
            public string key;
            public string aId;
            public List<Vector3> pts;
            public float length;
            public Vector3 mid;
            public GameObject tap;
            public RectTransform chip;
            public Image chipBg, dot;
            public TMP_Text text;
            public string lastText;
            public GameObject patrol;
            public readonly List<SpriteAnimator> patrolMen = new List<SpriteAnimator>();
            public int patrolShown = -1;
        }

        class CaravanView
        {
            public int id;
            public GameObject go;
            public SpriteAnimator merchant;
            public GameObject bandits;
            public bool fought;
            public Vector3 lastPos;
        }

        readonly Dictionary<string, RoadView> roads = new Dictionary<string, RoadView>();
        readonly Dictionary<int, CaravanView> caravans = new Dictionary<int, CaravanView>();
        readonly Dictionary<string, MapCity> cityById = new Dictionary<string, MapCity>();
        RectTransform canvasRt;
        Camera cam;

        public static MapTradeLayer Create(MapCity[] cities)
        {
            var canvas = UIFactory.Canvas("RoadMarkers", 4);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            var layer = canvas.gameObject.AddComponent<MapTradeLayer>();
            layer.canvasRt = (RectTransform)canvas.transform;
            foreach (var c in cities) layer.cityById[c.CityId] = c;

            // yol geometrisi: her bağlantı bir kez
            foreach (var c in cities)
                foreach (var l in c.links)
                {
                    if (l.to == null) continue;
                    string key = Trade.Key(c.CityId, l.to.CityId);
                    if (layer.roads.ContainsKey(key)) continue;
                    var pts = new List<Vector3> { c.doorPoint };
                    if (l.points != null) pts.AddRange(l.points);
                    if (Vector3.Distance(pts[pts.Count - 1], l.to.doorPoint) > 0.05f) pts.Add(l.to.doorPoint);
                    float len = 0f;
                    for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
                    var v = new RoadView { key = key, aId = c.CityId, pts = pts, length = len };
                    v.mid = PointAt(pts, len, 0.5f, out _);
                    layer.roads[key] = v;
                    Trade.RoadLengths[key] = len;
                }
            return layer;
        }

        // ------------------------------------------------------------------ geometri

        static Vector3 PointAt(List<Vector3> pts, float length, float t, out Vector3 dir)
        {
            dir = Vector3.right;
            if (pts.Count == 0) return Vector3.zero;
            if (pts.Count == 1) return pts[0];
            float d = Mathf.Clamp01(t) * length;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (d <= seg || i == pts.Count - 1)
                {
                    dir = pts[i] - pts[i - 1];
                    return Vector3.Lerp(pts[i - 1], pts[i], seg > 0f ? Mathf.Clamp01(d / seg) : 1f);
                }
                d -= seg;
            }
            return pts[pts.Count - 1];
        }

        /// <summary>Kervanın A şehrinden mi çıktığına göre yol üzerindeki konumu.</summary>
        Vector3 Along(RoadView v, RoadState r, bool fromA, float t, out Vector3 dir)
        {
            // geometri r.a'dan değil, ilk bulunan şehirden çizildi
            bool forward = (v.aId == r.a) == fromA;
            var p = PointAt(v.pts, v.length, forward ? t : 1f - t, out dir);
            if (!forward) dir = -dir;
            return p;
        }

        // ------------------------------------------------------------------ çizim

        RoadView View(string key) => roads.TryGetValue(key, out var v) ? v : null;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var data = SaveSystem.Data;
            float scale = Mathf.Clamp(3.6f / cam.orthographicSize, 0.7f, 1.05f);
            foreach (var v in roads.Values)
            {
                var r = data.Road(v.key);
                bool show = r != null && r.active;
                if (show && v.chip == null) BuildMarker(v);
                if (v.chip != null)
                {
                    // görünürlük ekran boyutuna değil, kameranın görüş alanına göre (simülatörde Screen farklı olabilir)
                    Vector3 vp = cam.WorldToViewportPoint(v.mid);
                    bool vis = show && vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
                    if (v.chip.gameObject.activeSelf != vis) v.chip.gameObject.SetActive(vis);
                    if (v.tap.activeSelf != show) v.tap.SetActive(show);
                    if (vis)
                    {
                        Vector2 size = canvasRt.rect.size;
                        Vector2 local = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
                        v.chip.anchoredPosition = local;
                        v.chip.localScale = Vector3.one * scale;
                        RefreshMarker(v, r);
                    }
                }
                UpdatePatrol(v, show ? r : null);
            }
            UpdateCaravans(data);
        }

        void BuildMarker(RoadView v)
        {
            var bg = UIFactory.Chip(canvasRt, "Road_" + v.key, new Vector2(200f, 50f), 25f);
            bg.raycastTarget = false;
            v.chipBg = bg;
            v.chip = bg.rectTransform;
            v.chip.anchorMin = v.chip.anchorMax = new Vector2(0.5f, 0.5f);
            v.chip.pivot = new Vector2(0.5f, 0.5f);
            v.dot = UIFactory.Rounded(bg.transform, "Dot", Color.white, 9f);
            UIFactory.Place(v.dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 18f));
            v.dot.raycastTarget = false;
            v.text = UIFactory.Text(bg.transform, "T", "", 23f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(v.text.rectTransform, 44f, 0f, 12f, 0f);
            v.text.textWrappingMode = TextWrappingModes.NoWrap;

            v.tap = new GameObject("RoadTap_" + v.key);
            v.tap.transform.position = v.mid;
            var col = v.tap.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            v.tap.AddComponent<MapRoadTap>().key = v.key;
        }

        void RefreshMarker(RoadView v, RoadState r)
        {
            int p = r.PatrolCount();
            string t = Trade.SecurityName(r.security) + " " + Mathf.RoundToInt(r.security) + (p > 0 ? "  ·  " + p + " devriye" : "");
            if (t == v.lastText) return;
            v.lastText = t;
            var col = Trade.SecurityColor(r.security);
            v.dot.color = col;
            v.text.text = t;
            v.text.color = r.security < 25f ? col : Color.white;
            v.chip.sizeDelta = new Vector2(v.text.GetPreferredValues(t).x + 62f, 50f);
        }

        // ------------------------------------------------------------------ devriye

        void UpdatePatrol(RoadView v, RoadState r)
        {
            int n = r != null ? Mathf.Min(3, r.PatrolCount()) : 0;
            if (n != v.patrolShown)
            {
                v.patrolShown = n;
                if (v.patrol != null) Destroy(v.patrol);
                v.patrolMen.Clear();
                v.patrol = null;
                if (n > 0)
                {
                    v.patrol = new GameObject("Patrol_" + v.key);
                    for (int i = 0; i < n; i++)
                        v.patrolMen.Add(Man(v.patrol.transform, new Vector3((i - (n - 1) * 0.5f) * 0.2f, (i % 2) * 0.06f, 0f),
                            UIFactory.Hex("8FC1FF"), 0.62f, AnimType.Walk));
                }
            }
            if (v.patrol == null || v.length <= 0f) return;
            // yolun ortasındaki bölümde gidip gelir
            float period = Mathf.Max(8f, v.length * 0.6f / 0.45f);
            float ph = Mathf.PingPong(Time.time / period * 2f + v.key.Length * 0.37f, 1f);
            float t = 0.2f + 0.6f * ph;
            var pos = PointAt(v.pts, v.length, t, out Vector3 dir);
            bool back = Mathf.Repeat(Time.time / period * 2f + v.key.Length * 0.37f, 2f) > 1f;
            if (back) dir = -dir;
            v.patrol.transform.position = pos;
            foreach (var m in v.patrolMen) if (m != null) m.Face(dir);
        }

        // ------------------------------------------------------------------ kervanlar

        void UpdateCaravans(SaveData data)
        {
            long now = DateTime.UtcNow.Ticks;
            var alive = new HashSet<int>();
            foreach (var c in data.caravans)
            {
                var r = data.Road(c.roadKey);
                var v = View(c.roadKey);
                if (r == null || v == null) continue;
                alive.Add(c.id);
                if (!caravans.TryGetValue(c.id, out var cv)) caravans[c.id] = cv = BuildCaravan(c);

                float total = Mathf.Max(1f, c.arriveTicks - c.startTicks);
                float t = Mathf.Clamp01((now - c.startTicks) / total);
                bool ambush = c.fate != 0 && t >= c.eventT;
                if (c.fate == 1 && ambush) t = c.eventT; // soyulan kervan olduğu yerde kalır
                var pos = Along(v, r, c.fromA, t, out Vector3 dir);
                cv.go.transform.position = pos;
                if (cv.merchant != null) cv.merchant.Face(dir);
                cv.lastPos = pos;

                if (ambush && !cv.fought)
                {
                    cv.fought = true;
                    cv.bandits = new GameObject("Bandits");
                    cv.bandits.transform.position = pos + new Vector3(0.05f, 0.32f, 0f);
                    for (int i = 0; i < 2; i++)
                    {
                        var b = Man(cv.bandits.transform, new Vector3((i - 0.5f) * 0.32f, 0f, 0f), new Color(0.32f, 0.27f, 0.24f), 0.66f, AnimType.Attack);
                        b.Face(Vector3.down);
                    }
                    if (c.fate == 1)
                    {
                        if (cv.merchant != null) cv.merchant.Play(AnimType.Death);
                        FX.Shake(cv.go.transform, 0.06f, 0.6f);
                    }
                    else
                    {
                        // devriye yetişir: eşkıyalar kaçar
                        FX.Sparkles(pos + Vector3.up * 0.3f, UIFactory.Hex("8FC1FF"), 14, 0.7f);
                        Destroy(cv.bandits, 1.6f);
                    }
                }
            }

            // biten kervanlar
            var gone = new List<int>();
            foreach (var kv in caravans) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                var cv = caravans[id];
                caravans.Remove(id);
                if (cv.bandits != null) Destroy(cv.bandits);
                if (cv.go != null) Destroy(cv.go);
            }
        }

        CaravanView BuildCaravan(CaravanState c)
        {
            var cv = new CaravanView { id = c.id, go = new GameObject("Caravan_" + c.id) };
            var cart = new GameObject("Cart").AddComponent<SpriteRenderer>();
            cart.transform.SetParent(cv.go.transform, false);
            cart.transform.localPosition = new Vector3(0.16f, 0.02f, 0f);
            cart.sprite = CartSprite;
            cart.transform.localScale = Vector3.one * 0.9f;
            var ys = cart.gameObject.AddComponent<YSort>();
            ys.bias = 6;
            cv.merchant = Man(cv.go.transform, new Vector3(-0.16f, 0f, 0f), UIFactory.Hex("E8C995"), 0.6f, AnimType.Walk);
            return cv;
        }

        static SpriteAnimator Man(Transform parent, Vector3 local, Color tint, float scale, AnimType anim)
        {
            var art = GameArt.Instance;
            var m = new GameObject("Man").transform;
            m.SetParent(parent, false);
            m.localPosition = local;
            m.localScale = Vector3.one * scale;
            var sr = m.gameObject.AddComponent<SpriteRenderer>();
            sr.color = tint;
            var a = m.gameObject.AddComponent<SpriteAnimator>();
            a.target = sr;
            a.set = art != null ? art.knight : null;
            a.Play(anim, true);
            m.gameObject.AddComponent<YSort>().bias = 7;
            return a;
        }

        /// <summary>Altın kazancını şehrin üstünde yükselen bir yazıyla gösterir.</summary>
        public void ShowGain(string cityId, int amount)
        {
            if (!cityById.TryGetValue(cityId, out var city)) return;
            var go = new GameObject("Gain");
            go.transform.position = city.doorPoint + Vector3.up * 0.9f;
            var t = go.AddComponent<TextMeshPro>();
            t.font = UIFactory.Bold;
            t.text = "+" + amount;
            t.fontSize = 4.2f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = UIFactory.Hex("FFD36E");
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(60, 40, 10, 255);
            t.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            t.sortingOrder = 30000;
            FX.Run(Rise(go.transform, t));
        }

        static System.Collections.IEnumerator Rise(Transform tr, TextMeshPro t)
        {
            float time = 0f;
            Vector3 start = tr.position;
            while (time < 1.8f && tr != null)
            {
                time += Time.deltaTime;
                tr.position = start + Vector3.up * (time * 0.5f);
                var c = t.color;
                c.a = time < 1.2f ? 1f : 1f - (time - 1.2f) / 0.6f;
                t.color = c;
                yield return null;
            }
            if (tr != null) Destroy(tr.gameObject);
        }

        // ------------------------------------------------------------------ çizimler

        static Sprite cart;

        /// <summary>Koddan çizilen küçük yük arabası: tekerlekler, kasa ve krem renkli branda.</summary>
        static Sprite CartSprite
        {
            get
            {
                if (cart != null) return cart;
                int w = 64, h = 52;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                Color wood = new Color(0.55f, 0.34f, 0.17f), woodDark = new Color(0.33f, 0.2f, 0.1f);
                Color cloth = new Color(0.97f, 0.9f, 0.74f), clothDark = new Color(0.82f, 0.72f, 0.55f), outline = new Color(0.16f, 0.11f, 0.07f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = Color.clear;
                        // branda: yarım elips
                        float cx = (x - 32f) / 25f, cy = (y - 22f) / 26f;
                        if (y >= 22 && cx * cx + cy * cy <= 1f)
                        {
                            c = (x / 8) % 2 == 0 ? cloth : Color.Lerp(cloth, clothDark, 0.5f);
                            if (cx * cx + cy * cy > 0.84f) c = outline;
                        }
                        // kasa
                        if (y >= 12 && y < 24 && x >= 5 && x < 59)
                        {
                            c = (y == 12 || y == 23 || x == 5 || x == 58) ? outline : (y % 4 == 0 ? woodDark : wood);
                        }
                        // tekerlekler
                        foreach (float wx in new[] { 17f, 47f })
                        {
                            float d = new Vector2(x - wx, y - 10f).magnitude;
                            if (d <= 9.5f) c = d >= 7.5f ? outline : d >= 5.5f ? woodDark : d <= 2f ? outline : wood;
                        }
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                cart = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 100f);
                return cart;
            }
        }
    }
}
