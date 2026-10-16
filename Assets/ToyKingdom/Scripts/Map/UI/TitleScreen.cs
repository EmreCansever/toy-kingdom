using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>
    /// Açılış ekranı: arkada yaşayan harita yavaşça şehirden şehre süzülür, solda arma, oyun adı ve düğmeler,
    /// sağda kayıtlı krallığın özeti. Uygulama her açıldığında bir kez gösterilir; açıkken krallıkta zaman durur.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        static readonly Color Gold = UIFactory.Hex("FFD36E");
        static readonly Color GoldDeep = UIFactory.Hex("C8861E");
        static readonly Color Navy = UIFactory.Hex("101624");

        Canvas canvas;
        RectTransform root;
        MapController map;
        MapUI ui;
        readonly List<CanvasGroup> intro = new List<CanvasGroup>();
        readonly List<Vector2> introFrom = new List<Vector2>();
        readonly List<Vector2> introTo = new List<Vector2>();
        readonly List<RectTransform> dust = new List<RectTransform>();
        readonly List<Vector3> dustSeed = new List<Vector3>();
        RectTransform crestRing;
        GameObject confirm;
        CanvasGroup all;
        float t, closingT = -1f, tourT;
        int tourIndex;
        List<MapCity> tour;
        bool hiddenForSettings;

        public static TitleScreen Show(MapController map, MapUI ui, IList<MapCity> cities)
        {
            var c = UIFactory.Canvas("TitleScreen", 80);
            var ts = c.gameObject.AddComponent<TitleScreen>();
            ts.canvas = c;
            ts.map = map;
            ts.ui = ui;
            ts.root = (RectTransform)c.transform;
            ts.tour = new List<MapCity>();
            foreach (var x in cities) if (x != null) ts.tour.Add(x);
            ts.tour.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            ts.Build();
            ts.SetGameHud(false);
            IsOpen = true;
            return ts;
        }

        /// <summary>Açılışta oyun arayüzü ve şehir plakaları gizlenir: arkada sadece harita görünür.</summary>
        void SetGameHud(bool on)
        {
            foreach (var n in new[] { "MapUI", "CityNameplates", "Coach" })
            {
                var go = GameObject.Find(n);
                var c = go != null ? go.GetComponent<Canvas>() : null;
                if (c != null) c.enabled = on;
            }
        }

        // ------------------------------------------------------------------ kurulum

        static Sprite Gradient(bool horizontal, float from, float to)
        {
            var tex = new Texture2D(horizontal ? 256 : 1, horizontal ? 1 : 256, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < 256; i++)
            {
                float k = i / 255f;
                float a = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                var col = new Color(1f, 1f, 1f, a);
                if (horizontal) tex.SetPixel(i, 0, col); else tex.SetPixel(0, i, col);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        CanvasGroup Group(RectTransform rt, Vector2 slide)
        {
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            intro.Add(g);
            introTo.Add(rt.anchoredPosition);
            introFrom.Add(rt.anchoredPosition + slide);
            return g;
        }

        void Build()
        {
            var bg = UIFactory.Rect("Root", root);
            UIFactory.Stretch(bg);
            all = bg.gameObject.AddComponent<CanvasGroup>();

            // arkadaki haritayı karart: hafif genel örtü, solda koyu geçiş, üst ve altta gölge
            var veil = UIFactory.Panel(bg, "Veil", new Color(0.04f, 0.06f, 0.11f, 0.38f));
            UIFactory.Stretch(veil.rectTransform);
            var left = UIFactory.Panel(bg, "Left", new Color(0.03f, 0.05f, 0.1f, 1f), Gradient(true, 1f, 0f));
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0.8f, 1f);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
            var bottom = UIFactory.Panel(bg, "Bottom", new Color(0.03f, 0.05f, 0.1f, 1f), Gradient(false, 0.85f, 0f));
            bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            var top = UIFactory.Panel(bg, "Top", new Color(0.03f, 0.05f, 0.1f, 1f), Gradient(false, 0f, 0.6f));
            top.rectTransform.anchorMin = new Vector2(0f, 0.8f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            foreach (var img in new[] { veil, left, bottom, top }) img.raycastTarget = img == veil; // örtü dokunuşu haritaya geçirmez

            // altın tozu
            var dustRoot = UIFactory.Rect("Dust", bg);
            UIFactory.Stretch(dustRoot);
            var rnd = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                var mote = UIFactory.Panel(dustRoot, "D" + i, new Color(1f, 0.85f, 0.5f, 0.5f), GameArt.Instance != null ? GameArt.Instance.circle : null);
                mote.raycastTarget = false;
                float s = 4f + (float)rnd.NextDouble() * 8f;
                mote.rectTransform.anchorMin = mote.rectTransform.anchorMax = new Vector2((float)rnd.NextDouble(), 0f);
                mote.rectTransform.sizeDelta = new Vector2(s, s);
                dust.Add(mote.rectTransform);
                dustSeed.Add(new Vector3((float)rnd.NextDouble() * 10f, 18f + (float)rnd.NextDouble() * 40f, (float)rnd.NextDouble()));
            }

            // ---- sol sütun
            var col = UIFactory.Rect("Column", bg);
            col.anchorMin = col.anchorMax = new Vector2(0f, 0.5f);
            col.pivot = new Vector2(0f, 0.5f);
            col.anchoredPosition = new Vector2(150f, 10f);
            col.sizeDelta = new Vector2(820f, 1000f);

            // arma: altın halka, lacivert kalkan yüzü, içinde mavi kule
            var crest = UIFactory.Rect("Crest", col);
            UIFactory.Place(crest, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(150f, 150f));
            var glow = UIFactory.Panel(crest, "Glow", new Color(1f, 0.8f, 0.4f, 0.14f), GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Stretch(glow.rectTransform, -22f);
            glow.raycastTarget = false;
            var disk = UIFactory.Panel(crest, "Disk", Navy, GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Stretch(disk.rectTransform, 8f);
            disk.raycastTarget = false;
            var ring = UIFactory.Panel(crest, "Ring", Gold, UIFactory.RingSprite);
            UIFactory.Stretch(ring.rectTransform);
            ring.raycastTarget = false;
            crestRing = ring.rectTransform;
            var tower = UIFactory.Panel(crest, "Tower", Color.white, GameArt.Instance != null ? GameArt.Instance.Tower(CastleColor.Blue, 0) : null);
            tower.preserveAspect = true;
            UIFactory.Stretch(tower.rectTransform, 30f, 26f, 30f, 22f);
            tower.raycastTarget = false;
            Group(crest, new Vector2(0f, 30f));

            var eyebrow = UIFactory.Text(col, "Eyebrow", "ORTA ÇAĞ STRATEJİ", 26f, Gold, TextAlignmentOptions.Left, true);
            UIFactory.Place(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178f, -78f), new Vector2(600f, 40f));
            eyebrow.characterSpacing = 18f;
            Group(eyebrow.rectTransform, new Vector2(-30f, 0f));

            var toy = UIFactory.Text(col, "Toy", "TOY", 74f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(toy.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178f, -116f), new Vector2(600f, 84f));
            toy.characterSpacing = 42f;
            Group(toy.rectTransform, new Vector2(-40f, 0f));

            var title = UIFactory.Text(col, "Title", "KINGDOM", 168f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-6f, -196f), new Vector2(900f, 190f));
            title.characterSpacing = 4f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UIFactory.Hex("FFF1C2"), UIFactory.Hex("FFE08A"), GoldDeep, UIFactory.Hex("B8741A"));
            var mat = title.fontMaterial;
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, UIFactory.Hex("3A230C"));
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            title.fontMaterial = mat;
            Group(title.rectTransform, new Vector2(-60f, 0f));

            var tag = UIFactory.Text(col, "Tagline", "Kaleleri kuşat, valilerini yönet, tahtını koru.", 32f, new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.Left);
            UIFactory.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -392f), new Vector2(800f, 48f));
            Group(tag.rectTransform, new Vector2(-30f, 0f));

            // ince altın çizgi ve ortasında elmas
            var rule = UIFactory.Rect("Rule", col);
            UIFactory.Place(rule, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -462f), new Vector2(520f, 20f));
            var line = UIFactory.Panel(rule, "Line", new Color(1f, 0.83f, 0.43f, 0.55f), Gradient(true, 1f, 0f));
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f); line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 3f);
            line.raycastTarget = false;
            var gem = UIFactory.Panel(rule, "Gem", Gold);
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            gem.rectTransform.sizeDelta = new Vector2(14f, 14f);
            gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            gem.raycastTarget = false;
            Group(rule, new Vector2(-30f, 0f));

            // düğmeler
            var d = SaveSystem.Data;
            bool hasSave = d.conqueredCities.Count > 0 || d.playMinutes >= 1f || d.tutorialStep > 0;
            float y = -508f;
            var cont = MenuButton(col, hasSave ? "Devam et" : "Oyuna başla", hasSave ? Summary(d) : "Küçük bir krallık seni bekliyor", true, new Vector2(4f, y));
            cont.onClick.AddListener(Close);
            y -= 138f;
            if (hasSave)
            {
                var neu = MenuButton(col, "Yeni oyun", "Baştan, tek bir kaleyle", false, new Vector2(4f, y));
                neu.onClick.AddListener(AskNewGame);
                y -= 116f;
            }
            var set = MenuButton(col, "Ayarlar", null, false, new Vector2(4f, y));
            set.onClick.AddListener(OpenSettings);

            // ---- sağ alt: krallığın özeti
            if (hasSave) BuildCard(bg, d);

            var ver = UIFactory.Text(bg, "Version", "v" + Application.version, 22f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Right);
            UIFactory.Place(ver.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 26f), new Vector2(300f, 34f));
        }

        static string Summary(SaveData d)
        {
            int owned = 1;
            foreach (var id in d.conqueredCities) if (!d.IsRebel(id)) owned++;
            return owned + " şehir  ·  " + d.gold.ToString("N0") + " altın  ·  " + Reputation.Name(d.reputation);
        }

        Button MenuButton(RectTransform parent, string label, string sub, bool primary, Vector2 pos)
        {
            float h = primary ? 120f : 96f;
            var rt = UIFactory.Rect("Btn_" + label, parent);
            UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(540f, h));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            if (primary)
            {
                var lip = UIFactory.Rounded(rt, "Lip", UIFactory.Hex("8A5410"), 24f);
                UIFactory.Stretch(lip.rectTransform);
                lip.raycastTarget = false;
                var face = UIFactory.Rounded(rt, "Face", UIFactory.Hex("F2B33D"), 24f, true);
                UIFactory.Stretch(face.rectTransform, 0f, 8f, 0f, 0f);
                face.raycastTarget = false;
                var shine = UIFactory.Rounded(face.transform, "Shine", new Color(1f, 1f, 1f, 0.25f), 18f);
                shine.rectTransform.anchorMin = new Vector2(0f, 0.55f); shine.rectTransform.anchorMax = new Vector2(1f, 1f);
                shine.rectTransform.offsetMin = new Vector2(8f, 0f); shine.rectTransform.offsetMax = new Vector2(-8f, -5f);
                shine.raycastTarget = false;
            }
            else
            {
                var face = UIFactory.Rounded(rt, "Face", new Color(0.08f, 0.1f, 0.16f, 0.78f), 22f);
                UIFactory.Stretch(face.rectTransform);
                face.raycastTarget = false;
                var edge = UIFactory.Rounded(rt, "Edge", new Color(1f, 0.83f, 0.43f, 0.45f), 22f);
                edge.sprite = UIFactory.RingSprite;
                UIFactory.Stretch(edge.rectTransform);
                edge.raycastTarget = false;
            }

            Color ink = primary ? UIFactory.Hex("3A230C") : Color.white;
            var lt = UIFactory.Text(rt, "Label", label, primary ? 44f : 36f, ink, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(lt.rectTransform, 40f, sub != null ? h * 0.42f : 6f, 90f, sub != null ? 6f : 6f);
            if (sub != null)
            {
                var st = UIFactory.Text(rt, "Sub", sub, 24f, primary ? new Color(0.23f, 0.14f, 0.05f, 0.85f) : new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Left);
                UIFactory.Stretch(st.rectTransform, 40f, 14f, 90f, h * 0.56f);
                st.enableAutoSizing = true; st.fontSizeMin = 16f; st.fontSizeMax = 24f;
            }
            // sağda küçük ok
            var arrow = UIFactory.Text(rt, "Arrow", ">", primary ? 46f : 36f, primary ? ink : Gold, TextAlignmentOptions.Right, true);
            UIFactory.Stretch(arrow.rectTransform, 0f, 8f, 36f, 8f);
            Group(rt, new Vector2(-40f, 0f));

            // dokununca küçük basma tepkisi
            var trig = rt.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerDown, () => rt.localScale = Vector3.one * 0.97f);
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerUp, () => rt.localScale = Vector3.one);
            btn.onClick.AddListener(() => Sfx.Click());
            return btn;
        }

        static void AddTrigger(UnityEngine.EventSystems.EventTrigger trig, UnityEngine.EventSystems.EventTriggerType type, Action a)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => a());
            trig.triggers.Add(e);
        }

        void BuildCard(RectTransform bg, SaveData d)
        {
            var card = UIFactory.Chip(bg, "Kingdom", new Vector2(470f, 300f), 26f);
            card.color = new Color(0.07f, 0.09f, 0.15f, 0.86f);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 90f), new Vector2(470f, 300f));
            var head = UIFactory.Text(card.transform, "Head", "KRALLIĞIN", 24f, Gold, TextAlignmentOptions.Left, true);
            UIFactory.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -24f), new Vector2(400f, 36f));
            head.characterSpacing = 14f;

            int owned = 1;
            foreach (var id in d.conqueredCities) if (!d.IsRebel(id)) owned++;
            int total = map != null ? map.CityCount : 0;
            int mins = Mathf.FloorToInt(d.playMinutes);
            var rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Şehirler", owned + (total > 0 ? " / " + total : "")),
                new KeyValuePair<string, string>("Hazine", d.gold.ToString("N0") + " altın"),
                new KeyValuePair<string, string>("İtibar", Reputation.Name(d.reputation)),
                new KeyValuePair<string, string>("Fetih yıldızı", d.TotalStars().ToString()),
                new KeyValuePair<string, string>("Hüküm süresi", (mins >= 60 ? (mins / 60) + " sa " : "") + (mins % 60) + " dk"),
            };
            for (int i = 0; i < rows.Count; i++)
            {
                float yy = -76f - i * 42f;
                var k = UIFactory.Text(card.transform, "K" + i, rows[i].Key, 25f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Left);
                UIFactory.Place(k.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, yy), new Vector2(220f, 38f));
                var v = UIFactory.Text(card.transform, "V" + i, rows[i].Value, 26f, Color.white, TextAlignmentOptions.Right, true);
                UIFactory.Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, yy), new Vector2(240f, 38f));
            }
            Group(card.rectTransform, new Vector2(40f, 0f));
        }

        // ------------------------------------------------------------------ eylemler

        void Close()
        {
            if (closingT >= 0f) return;
            closingT = 0f;
        }

        void AskNewGame()
        {
            if (confirm != null) return;
            var dim = UIFactory.Panel(root, "Confirm", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            confirm = dim.gameObject;
            var box = UIFactory.Chip(dim.transform, "Box", new Vector2(780f, 380f), 30f);
            box.color = new Color(0.07f, 0.09f, 0.15f, 0.98f);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 380f));
            var edge = UIFactory.Rounded(box.transform, "Edge", new Color(1f, 0.83f, 0.43f, 0.5f), 30f);
            edge.sprite = UIFactory.RingSprite;
            UIFactory.Stretch(edge.rectTransform);
            edge.raycastTarget = false;
            var h = UIFactory.Text(box.transform, "T", "Yeni bir krallık mı?", 44f, Gold, TextAlignmentOptions.Center, true);
            UIFactory.Place(h.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(700f, 60f));
            var b = UIFactory.Text(box.transform, "B", "Kayıtlı krallığın, şehirlerin ve ordun silinir. Bu geri alınamaz.", 28f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.Place(b.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(680f, 100f));
            var no = UIFactory.Button(box.transform, "No", "Vazgeç", UIFactory.Hex("4A5568"), 32f);
            UIFactory.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 40f), new Vector2(300f, 96f));
            no.onClick.AddListener(() => { Destroy(confirm); confirm = null; });
            var yes = UIFactory.Button(box.transform, "Yes", "Baştan başla", UIFactory.Red, 32f);
            UIFactory.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(300f, 96f));
            yes.onClick.AddListener(() =>
            {
                IsOpen = false;
                GameSession.TitleShown = true;
                map.NewGame();
            });
        }

        void OpenSettings()
        {
            if (ui == null) return;
            ui.OpenSettings();
            canvas.enabled = false;
            SetGameHud(true);
            hiddenForSettings = true;
        }

        // ------------------------------------------------------------------ canlandırma

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            if (hiddenForSettings && (ui == null || !ui.SettingsOpen))
            {
                hiddenForSettings = false;
                canvas.enabled = true;
                SetGameHud(false);
            }

            // girişte sırayla süzülerek belirir
            for (int i = 0; i < intro.Count; i++)
            {
                float k = Mathf.Clamp01((t - 0.15f - i * 0.07f) / 0.55f);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                intro[i].alpha = e;
                ((RectTransform)intro[i].transform).anchoredPosition = Vector2.Lerp(introFrom[i], introTo[i], e);
            }

            // arma halkası hafifçe nefes alır
            if (crestRing != null) crestRing.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(t * 2.2f));

            // altın tozu aşağıdan yukarı süzülür
            float hgt = root.rect.height;
            for (int i = 0; i < dust.Count; i++)
            {
                var s = dustSeed[i];
                float y = Mathf.Repeat(s.x * 60f + t * s.y, hgt + 40f) - 20f;
                dust[i].anchoredPosition = new Vector2(Mathf.Sin(t * 0.6f + s.x) * 18f, y);
                var img = dust[i].GetComponent<Image>();
                var c = img.color;
                c.a = (0.18f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 1.7f + s.z * 6f))) * Mathf.Clamp01(1f - y / hgt);
                img.color = c;
            }

            // arkada harita şehirden şehre süzülür
            tourT -= dt;
            if (tourT <= 0f && tour != null && tour.Count > 0 && map != null && map.rig != null)
            {
                tourT = 7f;
                tourIndex = (tourIndex + 1) % tour.Count;
                map.rig.FocusOn(tour[tourIndex].doorPoint);
            }

            if (closingT >= 0f)
            {
                closingT += dt;
                float k = Mathf.Clamp01(closingT / 0.45f);
                all.alpha = 1f - k;
                if (k >= 1f)
                {
                    IsOpen = false;
                    GameSession.TitleShown = true;
                    SetGameHud(true);
                    if (map != null && map.hero != null && map.hero.CurrentCity != null) map.rig.FocusOn(map.hero.CurrentCity.doorPoint);
                    Destroy(gameObject);
                }
            }
        }

        void OnDestroy() => IsOpen = false;
    }
}
