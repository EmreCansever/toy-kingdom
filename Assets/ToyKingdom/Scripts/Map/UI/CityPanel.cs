using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Kendi şehrine dokununca açılan yönetim paneli: vali, vergi, yatırımlar (koddan kurulur).</summary>
    public class CityPanel : MonoBehaviour
    {
        GameObject root;
        RectTransform content;
        MapCity city;
        Action changed;
        Action<string> toast;

        public bool IsOpen => root != null && root.activeSelf;

        static readonly Color Bad = UIFactory.Red;
        static readonly Color Good = UIFactory.Green;

        public static CityPanel Create(Transform canvas, Action onChanged, Action<string> toast)
        {
            var dim = UIFactory.Panel(canvas, "CityPanel", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<CityPanel>();
            p.root = dim.gameObject;
            p.changed = onChanged;
            p.toast = toast;

            var box = UIFactory.Frame(dim.transform, "Box", new Vector2(1560f, 920f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1560f, 920f));
            p.content = UIFactory.Rect("Content", box.transform);
            UIFactory.Stretch(p.content, 16f);

            var close = UIFactory.Button(box.transform, "Close", "Kapat", UIFactory.Brown, 36f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(200f, 80f));
            close.onClick.AddListener(p.Hide);

            p.Hide();
            return p;
        }

        public void Show(MapCity c)
        {
            city = c;
            root.SetActive(true);
            Rebuild();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        float refreshTimer;

        void Update()
        {
            // açıkken simülasyon değişirse göstergeler güncel kalsın
            if (!IsOpen) return;
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 5f) { refreshTimer = 0f; Rebuild(); }
        }

        // ------------------------------------------------------------------ çizim

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            var data = SaveSystem.Data;
            var s = data.State(city.CityId);
            bool home = city.isHomeCity;
            int income = city.Income;

            Label(content, city.DisplayName, 56f, UIFactory.BrownDark, new Vector2(30f, -20f), new Vector2(1100f, 70f), TextAlignmentOptions.Left);
            Label(content, "Gelir: dakikada " + income + " altın" + (home ? " · Krallığın kalbi" : ""), 32f, UIFactory.Brown,
                new Vector2(30f, -92f), new Vector2(1100f, 44f), TextAlignmentOptions.Left);

            if (!home)
            {
                float rate = KingdomSim.UnrestRate(s);
                Label(content, "Huzursuzluk", 30f, UIFactory.BrownDark, new Vector2(30f, -150f), new Vector2(250f, 40f), TextAlignmentOptions.Left);
                Bar(content, s.unrest / 100f, s.unrest >= 70f ? Bad : s.unrest >= 40f ? UIFactory.Gold : Good, new Vector2(280f, -156f), new Vector2(520f, 30f));
                Label(content, Mathf.RoundToInt(s.unrest) + " " + Trend(rate, true), 30f, s.unrest >= 70f ? Bad : UIFactory.BrownDark,
                    new Vector2(820f, -150f), new Vector2(400f, 40f), TextAlignmentOptions.Left);
                GovernorColumn(s);
                TaxRow(s);
            }
            BuildingsColumn(s, home);
        }

        void GovernorColumn(CityState s)
        {
            var col = Column("Governor", new Vector2(30f, -215f), new Vector2(700f, 640f));
            Label(col, "Vali", 40f, UIFactory.BrownDark, new Vector2(16f, -10f), new Vector2(660f, 50f), TextAlignmentOptions.Left);

            if (!s.hasGovernor)
            {
                Label(col, "Valisiz şehirde huzursuzluk hızla artar. Güçlü vali halkı yatıştırır ama hırslıdır; isyan ederse bastırmak zorlaşır.",
                    26f, UIFactory.Ink, new Vector2(16f, -62f), new Vector2(660f, 70f), TextAlignmentOptions.TopLeft);
                var cands = KingdomSim.Candidates(s);
                for (int i = 0; i < cands.Count; i++)
                {
                    var g = cands[i];
                    float y = -145f - i * 160f;
                    var row = UIFactory.Panel(col, "Cand" + i, UIFactory.ParchmentDark);
                    UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(660f, 145f));
                    Label(row.transform, g.name + "  " + Stars(g.power), 32f, UIFactory.BrownDark, new Vector2(16f, -8f), new Vector2(420f, 44f), TextAlignmentOptions.Left);
                    Label(row.transform, KingdomSim.TraitName(g.trait) + ": " + KingdomSim.TraitInfo(g.trait), 25f, UIFactory.Ink,
                        new Vector2(16f, -54f), new Vector2(430f, 40f), TextAlignmentOptions.Left);
                    Label(row.transform, "Sadakat " + Mathf.RoundToInt(g.loyalty), 25f, UIFactory.Brown, new Vector2(16f, -96f), new Vector2(430f, 36f), TextAlignmentOptions.Left);
                    int cost = KingdomSim.HireCost(g);
                    var btn = Btn(row.transform, "Ata\n" + cost + " altın", UIFactory.Blue, new Vector2(-14f, 0f), new Vector2(190f, 115f), 28f, true);
                    btn.interactable = SaveSystem.Data.gold >= cost;
                    var chosen = g;
                    btn.onClick.AddListener(() =>
                    {
                        if (!Pay(cost)) return;
                        s.governor = chosen;
                        s.hasGovernor = true;
                        s.warnedLoyalty = false;
                        Done(chosen.name + " " + city.DisplayName + " valisi oldu");
                    });
                }
                return;
            }

            var gov = s.governor;
            float lr = KingdomSim.LoyaltyRate(s);
            Label(col, gov.name + "  " + Stars(gov.power), 36f, UIFactory.BrownDark, new Vector2(16f, -66f), new Vector2(660f, 50f), TextAlignmentOptions.Left);
            Label(col, KingdomSim.TraitName(gov.trait) + ": " + KingdomSim.TraitInfo(gov.trait), 26f, UIFactory.Ink, new Vector2(16f, -118f), new Vector2(660f, 40f), TextAlignmentOptions.Left);
            Label(col, "Sadakat", 28f, UIFactory.BrownDark, new Vector2(16f, -172f), new Vector2(150f, 40f), TextAlignmentOptions.Left);
            Bar(col, gov.loyalty / 100f, gov.loyalty < 25f ? Bad : gov.loyalty < 50f ? UIFactory.Gold : Good, new Vector2(170f, -178f), new Vector2(330f, 28f));
            Label(col, Mathf.RoundToInt(gov.loyalty) + " " + Trend(lr, false), 28f, gov.loyalty < 25f ? Bad : UIFactory.BrownDark,
                new Vector2(515f, -172f), new Vector2(180f, 40f), TextAlignmentOptions.Left);
            Label(col, "İsyan ederse gücü: " + (gov.power + (gov.trait == GovernorTrait.Cruel ? 1 : 0)) + "/6", 26f, Bad,
                new Vector2(16f, -222f), new Vector2(660f, 36f), TextAlignmentOptions.Left);

            int giftCost = KingdomSim.GiftCost(gov);
            var gift = Btn(col, "Hediye gönder  (" + giftCost + ")\nSadakat +15", UIFactory.Green, new Vector2(16f, -280f), new Vector2(660f, 100f), 28f);
            gift.interactable = SaveSystem.Data.gold >= giftCost && gov.loyalty < 100f;
            gift.onClick.AddListener(() =>
            {
                if (!Pay(giftCost)) return;
                gov.loyalty = Mathf.Min(100f, gov.loyalty + 15f);
                Done(gov.name + " hediyeni memnuniyetle kabul etti");
            });

            if (gov.power < 5)
            {
                int upCost = KingdomSim.UpgradeCost(gov);
                var up = Btn(col, "Yetki ver  (" + upCost + ")\nGüç +1 · halk sakinleşir · sadakat -10", UIFactory.Blue, new Vector2(16f, -395f), new Vector2(660f, 100f), 26f);
                up.interactable = SaveSystem.Data.gold >= upCost;
                up.onClick.AddListener(() =>
                {
                    if (!Pay(upCost)) return;
                    gov.power++;
                    gov.loyalty = Mathf.Max(0f, gov.loyalty - 10f);
                    Done(gov.name + " artık daha güçlü");
                });
            }

            var fire = Btn(col, "Azlet\nHuzursuzluk +10", UIFactory.Red, new Vector2(16f, -510f), new Vector2(660f, 100f), 26f);
            fire.onClick.AddListener(() =>
            {
                s.hasGovernor = false;
                s.candidateSeed++;
                s.unrest = Mathf.Min(100f, s.unrest + 10f);
                Done(gov.name + " görevden alındı");
            });
        }

        void TaxRow(CityState s)
        {
            var col = Column("Tax", new Vector2(760f, -215f), new Vector2(740f, 130f));
            Label(col, "Vergi", 34f, UIFactory.BrownDark, new Vector2(16f, -10f), new Vector2(200f, 46f), TextAlignmentOptions.Left);
            Label(col, s.tax == TaxLevel.Low ? "Gelir %70 · halk sakinleşir" : s.tax == TaxLevel.High ? "Gelir %140 · halk öfkelenir" : "Gelir %100",
                22f, UIFactory.Ink, new Vector2(16f, -62f), new Vector2(220f, 60f), TextAlignmentOptions.TopLeft);
            var levels = new[] { TaxLevel.Low, TaxLevel.Normal, TaxLevel.High };
            for (int i = 0; i < 3; i++)
            {
                var lv = levels[i];
                bool on = s.tax == lv;
                var b = Btn(col, KingdomSim.TaxName(lv), on ? UIFactory.Brown : new Color(0.62f, 0.52f, 0.38f),
                    new Vector2(250f + i * 160f, -22f), new Vector2(150f, 86f), 28f);
                b.onClick.AddListener(() =>
                {
                    if (s.tax == lv) return;
                    s.tax = lv;
                    Done(null);
                });
            }
        }

        void BuildingsColumn(CityState s, bool home)
        {
            Vector2 pos = home ? new Vector2(30f, -160f) : new Vector2(760f, -360f);
            var list = home ? new[] { Building.Market, Building.Walls } : new[] { Building.Market, Building.Barracks, Building.Walls, Building.Temple };
            var col = Column("Buildings", pos, new Vector2(home ? 1440f : 740f, home ? 400f : 495f));
            Label(col, "Yatırımlar", 34f, UIFactory.BrownDark, new Vector2(16f, -8f), new Vector2(400f, 46f), TextAlignmentOptions.Left);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                int lvl = s.Level(b);
                float y = -62f - i * 106f;
                var row = UIFactory.Panel(col, b.ToString(), UIFactory.ParchmentDark);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(home ? 1408f : 708f, 96f));
                Label(row.transform, KingdomSim.BuildingName(b) + "  " + Pips(lvl), 30f, UIFactory.BrownDark, new Vector2(14f, -6f), new Vector2(420f, 44f), TextAlignmentOptions.Left);
                Label(row.transform, KingdomSim.BuildingInfo(b), 25f, UIFactory.Ink, new Vector2(14f, -52f), new Vector2(470f, 36f), TextAlignmentOptions.Left);
                if (lvl >= KingdomSim.MaxLevel)
                {
                    Label(row.transform, "En üst seviye", 26f, Good, new Vector2(-14f, -26f), new Vector2(200f, 44f), TextAlignmentOptions.Right, true);
                    continue;
                }
                int cost = KingdomSim.BuildingCost(b, lvl);
                var btn = Btn(row.transform, "Yükselt\n" + cost, UIFactory.Gold, new Vector2(-10f, 0f), new Vector2(180f, 80f), 26f, true);
                btn.interactable = SaveSystem.Data.gold >= cost;
                var building = b;
                btn.onClick.AddListener(() =>
                {
                    if (!Pay(cost)) return;
                    s.SetLevel(building, lvl + 1);
                    Done(KingdomSim.BuildingName(building) + " seviye " + (lvl + 1) + " oldu");
                });
            }
        }

        // ------------------------------------------------------------------ yardımcılar

        bool Pay(int cost)
        {
            var d = SaveSystem.Data;
            if (d.gold < cost) { toast?.Invoke("Yeterli altın yok"); return false; }
            d.gold -= cost;
            return true;
        }

        void Done(string msg)
        {
            SaveSystem.Save();
            if (!string.IsNullOrEmpty(msg)) toast?.Invoke(msg);
            changed?.Invoke();
            Rebuild();
        }

        static string Stars(int n) => "(Güç " + Mathf.Clamp(n, 0, 5) + "/5)";
        static string Pips(int n) => "Sv " + n + "/" + KingdomSim.MaxLevel;
        static string Trend(float rate, bool upIsBad)
        {
            if (Mathf.Abs(rate) < 0.05f) return "(sabit)";
            bool up = rate > 0f;
            return "(" + (up ? "+" : "-") + Mathf.Abs(rate).ToString("0.0") + "/dk)";
        }

        RectTransform Column(string name, Vector2 pos, Vector2 size)
        {
            var bg = UIFactory.Panel(content, name, new Color(1f, 1f, 1f, 0.25f));
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            return bg.rectTransform;
        }

        static TMP_Text Label(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align, bool rightAnchor = false)
        {
            var t = UIFactory.Text(parent, "Text", text, size, color, align, true);
            var a = rightAnchor ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            UIFactory.Place(t.rectTransform, a, a, pos, box);
            return t;
        }

        static Button Btn(Transform parent, string label, Color color, Vector2 pos, Vector2 size, float font, bool rightMid = false)
        {
            var b = UIFactory.Button(parent, "Button", label, color, font);
            var a = rightMid ? new Vector2(1f, 0.5f) : new Vector2(0f, 1f);
            UIFactory.Place((RectTransform)b.transform, a, a, pos, size);
            return b;
        }

        static void Bar(Transform parent, float value, Color color, Vector2 pos, Vector2 size)
        {
            var bg = UIFactory.Panel(parent, "Bar", new Color(0.25f, 0.16f, 0.08f, 0.6f));
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var fill = UIFactory.Panel(bg.transform, "Fill", color);
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
        }
    }
}
