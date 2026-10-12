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
        TMP_Text title;
        TMP_Text headerInfo;
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

            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1560f, 960f), "", UIFactory.Blue, out p.title);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 960f));
            var header = box.transform.Find("Border/Header");
            p.headerInfo = UIFactory.Text(header, "Info", "", 26f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Right);
            UIFactory.Place(p.headerInfo.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(700f, 60f));
            p.headerInfo.enableAutoSizing = true; p.headerInfo.fontSizeMin = 16f; p.headerInfo.fontSizeMax = 26f;
            p.content = UIFactory.Rect("Content", box.transform);
            UIFactory.Stretch(p.content, 16f, 16f, 16f, 112f);

            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
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
            float interval = city != null && city.IncomingAttack != null ? 2f : 5f;
            if (refreshTimer >= interval) { refreshTimer = 0f; Rebuild(); }
        }

        // ------------------------------------------------------------------ çizim

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            var data = SaveSystem.Data;
            var s = data.State(city.CityId);
            bool home = city.isHomeCity;
            int income = city.Income;

            title.text = city.DisplayName;
            headerInfo.text = (home ? "Başkent  ·  " : "") + "+" + income + " altın / dakika";
            bool roads = data.roads.Exists(r => r.active && (r.a == city.CityId || r.b == city.CityId));
            if (roads)
            {
                float sec = Trade.CitySecurity(data, city.CityId);
                headerInfo.text += "  ·  yollar: <color=#" + ColorUtility.ToHtmlStringRGB(Trade.SecurityColor(sec)) + ">" + Trade.SecurityName(sec).ToLowerInvariant() + " " + Mathf.RoundToInt(sec) + "</color>";
            }

            if (!home)
            {
                float rate = KingdomSim.UnrestRate(s);
                var row = UIFactory.Rounded(content, "Unrest", UIFactory.ParchmentDark, 18f);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(1470f, 74f));
                Label(row.transform, "Halkın huzursuzluğu", 28f, UIFactory.BrownDark, new Vector2(24f, -16f), new Vector2(330f, 44f), TextAlignmentOptions.Left);
                Bar(row.transform, s.unrest / 100f, s.unrest >= 70f ? Bad : s.unrest >= 40f ? UIFactory.Gold : Good, new Vector2(370f, -22f), new Vector2(560f, 30f));
                string unrestText = Mathf.RoundToInt(s.unrest) + "  " + Trend(rate, true);
                if (s.revoltTimer >= 0f) unrestText = "İsyana " + Mathf.CeilToInt(s.revoltTimer) + " dk!";
                var ut = Label(row.transform, unrestText, 28f, s.unrest >= 70f || s.revoltTimer >= 0f ? Bad : UIFactory.BrownDark,
                    new Vector2(948f, -16f), new Vector2(228f, 44f), TextAlignmentOptions.Left);
                ut.enableAutoSizing = true; ut.fontSizeMin = 18f; ut.fontSizeMax = 28f;
                FeastButton(row.transform, s);
                GovernorColumn(s);
                TaxRow(s);
                DefenseRow(s);
            }
            BuildingsColumn(s, home);
        }

        void GovernorColumn(CityState s)
        {
            var col = Column("Governor", new Vector2(14f, -96f), new Vector2(720f, 640f));
            Label(col, "Vali", 40f, UIFactory.BrownDark, new Vector2(16f, -10f), new Vector2(660f, 50f), TextAlignmentOptions.Left);

            if (!s.hasGovernor)
            {
                Label(col, "Valisiz şehirde huzursuzluk hızla artar. Güçlü vali halkı yatıştırır ama hırslıdır; isyan ederse bastırmak zorlaşır.",
                    26f, UIFactory.Ink, new Vector2(16f, -62f), new Vector2(660f, 70f), TextAlignmentOptions.TopLeft);
                var cands = KingdomSim.Candidates(s);
                for (int i = 0; i < cands.Count; i++)
                {
                    var g = cands[i];
                    float y = -146f - i * 162f;
                    var row = UIFactory.Rounded(col, "Cand" + i, UIFactory.Parchment, 16f);
                    UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(688f, 150f));
                    Label(row.transform, g.name + "  " + Stars(g.power), 32f, UIFactory.BrownDark, new Vector2(16f, -8f), new Vector2(420f, 44f), TextAlignmentOptions.Left);
                    Label(row.transform, KingdomSim.TraitName(g.trait) + ": " + KingdomSim.TraitInfo(g.trait), 25f, UIFactory.Ink,
                        new Vector2(16f, -54f), new Vector2(430f, 40f), TextAlignmentOptions.Left);
                    Label(row.transform, "Sadakat " + Mathf.RoundToInt(g.loyalty) + "  ·  maaş " + (0.6f * g.power).ToString("0.#") + " altın/dk", 25f, UIFactory.Brown, new Vector2(16f, -96f), new Vector2(430f, 36f), TextAlignmentOptions.Left);
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
                if (s.revoltTimer >= 0f) Reputation.Change(SaveSystem.Data, -4f, "İsyana hazırlanan valiye rüşvet verdin");
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
                Reputation.Change(SaveSystem.Data, s.revoltTimer >= 0f ? 1f : -2f, s.revoltTimer >= 0f ? "Hain valiyi zamanında azlettin" : "Valini sebepsiz azlettin");
                Done(gov.name + " görevden alındı");
            });
        }

        /// <summary>Rakip saldırılarına karşı savunma: güç, takviye bölükleri, yaklaşan ordu.</summary>
        void DefenseRow(CityState s)
        {
            var atk = city.IncomingAttack;
            var row = UIFactory.Rounded(content, "Defense", atk != null ? UIFactory.Hex("FFE1DC") : UIFactory.ParchmentDark, 18f);
            UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -748f), new Vector2(1470f, 82f));
            float defense = RivalAI.Defense(s, city.Difficulty);
            string text = "Savunma gücü <b>" + Mathf.RoundToInt(defense) + "</b>   ·   Takviye: " + s.garrison + " bölük";
            if (atk != null)
            {
                long left = System.Math.Max(0L, atk.attackArriveTicks - System.DateTime.UtcNow.Ticks) / System.TimeSpan.TicksPerSecond;
                bool holds = defense >= atk.attackPower;
                text += "   ·   <color=#B8382F>" + RivalAI.Get(atk.id).name + " geliyor " + (left / 60) + ":" + (left % 60).ToString("00") +
                        "  ·  saldırı gücü " + Mathf.RoundToInt(atk.attackPower) + "</color>" + (holds ? "  <color=#3E9E4F>(dayanır)</color>" : "  <color=#B8382F>(düşer!)</color>");
            }
            var t = Label(row.transform, text, 24f, UIFactory.BrownDark, new Vector2(24f, -12f), new Vector2(880f, 60f), TextAlignmentOptions.Left);
            t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 24f;

            var data = SaveSystem.Data;
            string unit = BestGarrisonUnit(data);

            // asker topla: şehirden bedava piyade, ama halk kızar
            long now = System.DateTime.UtcNow.Ticks;
            bool levyReady = now >= s.levyReadyTicks;
            bool room = data.UsedCapacity() < data.ArmyCapacity();
            string levyLabel = levyReady ? "Asker topla" : "Asker topla  " + ((s.levyReadyTicks - now) / System.TimeSpan.TicksPerSecond / 60 + 1) + " dk";
            var levy = Btn(row.transform, levyLabel, UIFactory.Hex("8C5A2B"), new Vector2(-322f, 0f), new Vector2(240f, 66f), 22f, true);
            levy.interactable = levyReady && room;
            levy.onClick.AddListener(() =>
            {
                SaveSystem.Data.AddArmy("infantry", 1);
                s.unrest = Mathf.Min(100f, s.unrest + 20f);
                s.levyReadyTicks = System.DateTime.UtcNow.Ticks + 10 * System.TimeSpan.TicksPerMinute;
                Done(city.DisplayName + " halkından bir piyade bölüğü toplandı (huzursuzluk +20)");
            });

            var btn = Btn(row.transform, "Takviye gönder  (+" + Mathf.RoundToInt(RivalAI.GarrisonDefense) + ")", UIFactory.Purple,
                new Vector2(-12f, 0f), new Vector2(300f, 66f), 22f, true);
            btn.interactable = unit != null;
            btn.onClick.AddListener(() =>
            {
                string u = BestGarrisonUnit(SaveSystem.Data);
                if (u == null) { toast?.Invoke("Ordunda bölük yok"); return; }
                SaveSystem.Data.AddArmy(u, -1);
                s.garrison++;
                Done("Bir bölük " + city.DisplayName + " surlarına yerleşti");
            });
        }

        /// <summary>Ziyafet: parayla anında huzursuzluk düşürür (bekleme süreli). İsyan eşiğindeki şehri kurtarmanın hızlı yolu.</summary>
        void FeastButton(Transform row, CityState s)
        {
            int cost = KingdomSim.FeastCost(city.castle != null ? city.castle.difficulty : 1);
            long now = DateTime.UtcNow.Ticks;
            bool ready = now >= s.feastReadyTicks;
            string label = ready ? "Ziyafet ver  (" + cost + ")" : "Ziyafet  " + Mathf.CeilToInt((s.feastReadyTicks - now) / (float)TimeSpan.TicksPerMinute) + " dk";
            var b = Btn(row, label, s.revoltTimer >= 0f ? UIFactory.Red : UIFactory.Green, new Vector2(-12f, 0f), new Vector2(270f, 60f), 24f, true);
            b.interactable = ready && s.unrest > 1f;
            b.onClick.AddListener(() =>
            {
                var data = SaveSystem.Data;
                if (data.gold < cost) { toast?.Invoke("Yeterli altın yok"); return; }
                data.gold -= cost;
                if (s.revoltTimer >= 0f) Reputation.Change(data, -4f, "Ayaklanmayı son anda ziyafetle satın aldın");
                s.unrest = Mathf.Max(0f, s.unrest - KingdomSim.FeastRelief);
                s.feastReadyTicks = DateTime.UtcNow.Ticks + (long)(KingdomSim.FeastCooldownMinutes * TimeSpan.TicksPerMinute);
                Done(city.DisplayName + " halkı ziyafetle yatıştı (huzursuzluk -" + (int)KingdomSim.FeastRelief + ")");
            });
        }

        /// <summary>Takviyeye gönderilecek bölük: ordudaki en kalabalık birlik türü.</summary>
        static string BestGarrisonUnit(SaveData data)
        {
            string best = null;
            int n = 0;
            foreach (var e in data.army) if (e.count > n) { n = e.count; best = e.unitId; }
            return best;
        }

        void TaxRow(CityState s)
        {
            var col = Column("Tax", new Vector2(750f, -96f), new Vector2(734f, 218f));
            // vergi
            Label(col, "Vergi", 30f, UIFactory.BrownDark, new Vector2(16f, -10f), new Vector2(200f, 40f), TextAlignmentOptions.Left);
            Label(col, s.tax == TaxLevel.Low ? "Gelir %70 · halk sakinleşir" : s.tax == TaxLevel.High ? "Gelir %140 · halk öfkelenir" : "Gelir %100",
                20f, UIFactory.Ink, new Vector2(16f, -50f), new Vector2(230f, 50f), TextAlignmentOptions.TopLeft);
            var levels = new[] { TaxLevel.Low, TaxLevel.Normal, TaxLevel.High };
            for (int i = 0; i < 3; i++)
            {
                var lv = levels[i];
                bool on = s.tax == lv;
                var b = Btn(col, KingdomSim.TaxName(lv), on ? UIFactory.Blue : UIFactory.Hex("F4EBDD"),
                    new Vector2(250f + i * 160f, -14f), new Vector2(150f, 74f), 26f);
                b.onClick.AddListener(() =>
                {
                    if (s.tax == lv) return;
                    s.tax = lv;
                    Done(null);
                });
            }
            // kararname
            Label(col, "Kararname", 30f, UIFactory.BrownDark, new Vector2(16f, -112f), new Vector2(200f, 40f), TextAlignmentOptions.Left);
            Label(col, KingdomSim.PolicyInfo(s.policy), 18f, UIFactory.Ink, new Vector2(16f, -150f), new Vector2(230f, 62f), TextAlignmentOptions.TopLeft);
            var pols = new[] { CityPolicy.Trade, CityPolicy.Military, CityPolicy.Peace };
            for (int i = 0; i < 3; i++)
            {
                var pv = pols[i];
                bool on = s.policy == pv;
                var b = Btn(col, KingdomSim.PolicyName(pv), on ? UIFactory.Purple : UIFactory.Hex("F4EBDD"),
                    new Vector2(250f + i * 160f, -122f), new Vector2(150f, 74f), 26f);
                b.onClick.AddListener(() =>
                {
                    // aynı karara tekrar basmak kaldırır
                    s.policy = s.policy == pv ? CityPolicy.None : pv;
                    Done(city.DisplayName + ": " + KingdomSim.PolicyName(s.policy) + " kararnamesi");
                });
            }
        }

        void BuildingsColumn(CityState s, bool home)
        {
            Vector2 pos = home ? new Vector2(14f, -6f) : new Vector2(750f, -324f);
            var list = home ? new[] { Building.Market, Building.Barracks, Building.Walls } : new[] { Building.Market, Building.Barracks, Building.Walls, Building.Temple };
            var col = Column("Buildings", pos, new Vector2(home ? 1470f : 734f, home ? 420f : 414f));
            Label(col, "Yatırımlar", 30f, UIFactory.BrownDark, new Vector2(16f, -8f), new Vector2(400f, 46f), TextAlignmentOptions.Left);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                int lvl = s.Level(b);
                float y = home ? -62f - i * 108f : -52f - i * 88f;
                float rh = home ? 98f : 80f;
                var row = UIFactory.Rounded(col, b.ToString(), UIFactory.Parchment, 16f);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(home ? 1438f : 702f, rh));
                Label(row.transform, KingdomSim.BuildingName(b) + "  " + Pips(lvl), 27f, UIFactory.BrownDark, new Vector2(14f, -4f), new Vector2(420f, 38f), TextAlignmentOptions.Left);
                Label(row.transform, KingdomSim.BuildingInfo(b), 22f, UIFactory.Ink, new Vector2(14f, -42f), new Vector2(470f, 32f), TextAlignmentOptions.Left);
                if (lvl >= KingdomSim.MaxLevel)
                {
                    Label(row.transform, "En üst seviye", 26f, Good, new Vector2(-14f, -26f), new Vector2(200f, 44f), TextAlignmentOptions.Right, true);
                    continue;
                }
                int cost = KingdomSim.BuildingCost(b, lvl);
                var btn = Btn(row.transform, "Yükselt\n" + cost, UIFactory.Gold, new Vector2(-10f, 0f), new Vector2(170f, rh - 10f), 24f, true);
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
            var bg = UIFactory.Rounded(content, name, UIFactory.ParchmentDark, 20f);
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            return bg.rectTransform;
        }

        static TMP_Text Label(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align, bool rightAnchor = false)
        {
            var t = UIFactory.Text(parent, "Text", text, size, color, align, size >= 27f);
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
            var fill = UIFactory.Bar(parent, "Bar", color, pos, size, new Vector2(0f, 1f));
            ((RectTransform)fill.transform.parent).pivot = new Vector2(0f, 1f);
            UIFactory.SetBar(fill, value);
        }
    }
}
