using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Ticaret ve yollar: kendi şehirlerin arasındaki yollar, güvenlikleri, kervan kazancı ve devriye ataması.</summary>
    public class TradePanel : MonoBehaviour
    {
        GameObject root;
        RectTransform list, detail;
        TMP_Text summary;
        Func<IDictionary<string, CityInfo>> infos;
        Action changed;
        Action<string> toast;
        string selected;
        int page;
        float refresh;

        const int PerPage = 7;

        public bool IsOpen => root != null && root.activeSelf;

        public static TradePanel Create(Transform canvas, Func<IDictionary<string, CityInfo>> infos, Action onChanged, Action<string> toast)
        {
            var dim = UIFactory.Panel(canvas, "Trade", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<TradePanel>();
            p.root = dim.gameObject;
            p.infos = infos;
            p.changed = onChanged;
            p.toast = toast;
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1640f, 980f), "Ticaret ve Yollar", UIFactory.Hex("2E7D5B"), out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 980f));
            var header = box.transform.Find("Border/Header");
            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(p.Hide);
            p.summary = UIFactory.Text(header, "Summary", "", 26f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Right);
            UIFactory.Place(p.summary.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(760f, 60f));
            p.summary.enableAutoSizing = true; p.summary.fontSizeMin = 16f; p.summary.fontSizeMax = 26f;

            p.list = UIFactory.Rect("List", box.transform);
            UIFactory.Place(p.list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -118f), new Vector2(760f, 840f));
            p.detail = UIFactory.Rect("Detail", box.transform);
            UIFactory.Place(p.detail, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(806f, -118f), new Vector2(808f, 840f));
            p.root.SetActive(false);
            return p;
        }

        public void Open(string roadKey = null)
        {
            if (roadKey != null) selected = roadKey;
            root.SetActive(true);
            var roads = Roads();
            if (selected != null)
            {
                int i = roads.FindIndex(r => r.key == selected);
                if (i >= 0) page = i / PerPage;
            }
            Rebuild();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            refresh += Time.deltaTime;
            if (refresh > 2f) { refresh = 0f; Rebuild(); }
        }

        List<RoadState> Roads()
        {
            var l = SaveSystem.Data.roads.FindAll(r => r.active);
            l.Sort((x, y) => x.security.CompareTo(y.security)); // en tehlikeli üstte
            return l;
        }

        void Rebuild()
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            for (int i = detail.childCount - 1; i >= 0; i--) Destroy(detail.GetChild(i).gameObject);
            var data = SaveSystem.Data;
            var inf = infos();
            var roads = Roads();

            int patrols = 0, caravans = data.caravans.Count;
            foreach (var r in roads) patrols += r.PatrolCount();
            summary.text = roads.Count + " yol  ·  " + caravans + " kervan yolda  ·  " + patrols + " devriye  ·  ordu " + data.UsedCapacity() + "/" + data.ArmyCapacity();

            if (roads.Count == 0)
            {
                var t = Txt(list, "Henüz ticaret yolu yok.\n\nBaşkentine yolla bağlı bir şehri fethet: iki şehrin arasındaki yolda kervanlar gidip gelmeye başlar ve hazineye altın taşır.",
                    30f, UIFactory.BrownDark, false, new Vector2(10f, -10f), new Vector2(1560f, 300f));
                return;
            }
            if (selected == null || roads.Find(r => r.key == selected) == null) selected = roads[0].key;
            int pages = (roads.Count + PerPage - 1) / PerPage;
            page = Mathf.Clamp(page, 0, pages - 1);

            for (int i = page * PerPage; i < Mathf.Min(roads.Count, (page + 1) * PerPage); i++)
                Row(roads[i], i - page * PerPage, inf);

            if (pages > 1)
            {
                var prev = UIFactory.Button(list, "Prev", "Önceki", UIFactory.Hex("8C8577"), 26f);
                UIFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 70f));
                prev.interactable = page > 0;
                prev.onClick.AddListener(() => { page--; Rebuild(); });
                var next = UIFactory.Button(list, "Next", "Sonraki", UIFactory.Hex("8C8577"), 26f);
                UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(200f, 70f));
                next.interactable = page < pages - 1;
                next.onClick.AddListener(() => { page++; Rebuild(); });
                var pt = Txt(list, (page + 1) + " / " + pages, 28f, UIFactory.BrownDark, true, new Vector2(300f, -790f), new Vector2(160f, 50f));
                pt.alignment = TextAlignmentOptions.Center;
            }

            Detail(data.Road(selected), inf);
        }

        void Row(RoadState r, int index, IDictionary<string, CityInfo> inf)
        {
            bool sel = r.key == selected;
            var bg = UIFactory.Rounded(list, "Row" + index, sel ? UIFactory.Hex("E3F3E8") : UIFactory.ParchmentDark, 18f);
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -index * 106f), new Vector2(760f, 96f));
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            string key = r.key;
            btn.onClick.AddListener(() => { selected = key; Rebuild(); });
            if (sel)
            {
                var edge = UIFactory.Rounded(bg.transform, "Edge", UIFactory.Hex("2E7D5B"), 4f);
                UIFactory.Place(edge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(8f, 70f));
                edge.raycastTarget = false;
            }

            var col = Trade.SecurityColor(r.security);
            var n = Txt(bg.transform, Trade.Name(r, inf), 27f, UIFactory.BrownDark, true, new Vector2(26f, -8f), new Vector2(500f, 40f));
            n.enableAutoSizing = true; n.fontSizeMin = 18f; n.fontSizeMax = 27f;
            n.textWrappingMode = TextWrappingModes.NoWrap;
            int p = r.PatrolCount();
            Txt(bg.transform, "Kervan ~" + Trade.CaravanValue(SaveSystem.Data, r, inf) + " altın  ·  kayıp riski %" + Mathf.RoundToInt(Trade.LossChance(r) * 100f) +
                (p > 0 ? "  ·  " + p + " devriye" : "  ·  devriye yok"), 21f, p > 0 ? UIFactory.Brown : UIFactory.Red, false, new Vector2(26f, -52f), new Vector2(520f, 34f));
            var bar = UIFactory.Bar(bg.transform, "Sec", col, new Vector2(546f, -30f), new Vector2(120f, 24f), new Vector2(0f, 1f));
            UIFactory.SetBar(bar, r.security / 100f);
            var st = Txt(bg.transform, Mathf.RoundToInt(r.security).ToString(), 30f, UIFactory.Darken(col, 0.75f), true, new Vector2(676f, -20f), new Vector2(70f, 44f));
            st.alignment = TextAlignmentOptions.Center;
        }

        void Detail(RoadState r, IDictionary<string, CityInfo> inf)
        {
            if (r == null) return;
            var data = SaveSystem.Data;
            var card = UIFactory.Rounded(detail, "Card", UIFactory.ParchmentDark, 22f);
            UIFactory.Stretch(card.rectTransform);
            var t = card.transform;
            var col = Trade.SecurityColor(r.security);
            float target = Trade.Target(data, r, inf);

            var title = Txt(t, Trade.Name(r, inf), 36f, UIFactory.BrownDark, true, new Vector2(26f, -18f), new Vector2(760f, 50f));
            title.enableAutoSizing = true; title.fontSizeMin = 22f; title.fontSizeMax = 36f;
            Txt(t, Trade.SecurityName(r.security) + (Trade.IsBorderRoad(data, r, inf) ? "  ·  sınır yolu (düşman yakın)" : ""), 26f, UIFactory.Darken(col, 0.7f), true,
                new Vector2(26f, -70f), new Vector2(760f, 38f));

            Txt(t, "Güvenlik", 26f, UIFactory.BrownDark, true, new Vector2(26f, -124f), new Vector2(160f, 40f));
            var bar = UIFactory.Bar(t, "Sec", col, new Vector2(190f, -128f), new Vector2(380f, 30f), new Vector2(0f, 1f));
            UIFactory.SetBar(bar, r.security / 100f);
            string trend = target > r.security + 2f ? "  yükseliyor, hedef " + Mathf.RoundToInt(target) : target < r.security - 2f ? "  düşüyor, hedef " + Mathf.RoundToInt(target) : "  dengede";
            Txt(t, Mathf.RoundToInt(r.security) + trend, 24f, target < r.security - 2f ? UIFactory.Red : UIFactory.BrownDark, true,
                new Vector2(584f, -124f), new Vector2(214f, 40f)).enableAutoSizing = true;

            int value = Trade.CaravanValue(data, r, inf);
            float effect = (45f - r.security) * 0.02f;
            string lines =
                "Kervan değeri: <b>~" + value + " altın</b>, yaklaşık 2 dakikada bir\n" +
                "Eşkıya baskını: <b>%" + Mathf.RoundToInt(Trade.Risk(r) * 100f) + "</b>   ·   devriye püskürtür: <b>%" + Mathf.RoundToInt(Trade.RepelChance(r) * 100f) + "</b>\n" +
                "Kervan kaybetme riski: <b><color=" + (Trade.LossChance(r) > 0.2f ? "#B8382F" : "#3E9E4F") + ">%" + Mathf.RoundToInt(Trade.LossChance(r) * 100f) + "</color></b>\n" +
                "Yolun şehirlerine etkisi: huzursuzluk " + (effect > 0f ? "<color=#B8382F>+" : "<color=#3E9E4F>") + effect.ToString("0.0") + " / dk</color>" +
                (r.security < 30f ? "\n<color=#B8382F>İsyan çıkarsa eşkıyalar asilere katılır!</color>" : "") +
                "\nBugüne dek: " + r.delivered + " kervan vardı (" + r.earned + " altın), " + r.robbed + " soyuldu, " + r.repelled + " kurtarıldı";
            var info = Txt(t, lines, 23f, UIFactory.Ink, false, new Vector2(26f, -176f), new Vector2(760f, 220f));
            info.lineSpacing = 4f;
            info.enableAutoSizing = true; info.fontSizeMin = 16f; info.fontSizeMax = 23f;

            // devriye
            Txt(t, "Devriye bölükleri", 30f, UIFactory.BrownDark, true, new Vector2(26f, -410f), new Vector2(420f, 44f));
            Txt(t, "güç " + Trade.PatrolStrength(r).ToString("0.0"), 26f, UIFactory.Brown, true, new Vector2(560f, -414f), new Vector2(220f, 40f)).alignment = TextAlignmentOptions.Right;
            var db = UnitDatabase.Instance;
            int row = 0;
            if (db != null)
                foreach (var u in db.units)
                {
                    if (u == null) continue;
                    float y = -456f - row * 60f;
                    row++;
                    var line = UIFactory.Rounded(t, "U" + u.unitId, UIFactory.Parchment, 14f);
                    UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(768f, 56f));
                    var lt = line.transform;
                    Txt(lt, u.displayName + "  <size=19><color=#7A5432>x" + Trade.PatrolWeight(u.unitId).ToString("0.0#") + "</color></size>", 24f, UIFactory.BrownDark, true,
                        new Vector2(14f, -10f), new Vector2(270f, 38f));
                    int inArmy = data.GetArmyCount(u.unitId), inPatrol = r.PatrolOf(u.unitId);
                    Txt(lt, "orduda " + inArmy, 22f, UIFactory.Brown, false, new Vector2(290f, -12f), new Vector2(150f, 34f));
                    Txt(lt, "devriyede " + inPatrol, 22f, inPatrol > 0 ? UIFactory.Green : UIFactory.Brown, true, new Vector2(440f, -12f), new Vector2(170f, 34f));
                    string id = u.unitId;
                    var minus = UIFactory.Button(lt, "Minus", "–", UIFactory.Hex("8C8577"), 32f);
                    UIFactory.Place((RectTransform)minus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-82f, 2f), new Vector2(66f, 50f));
                    minus.interactable = inPatrol > 0;
                    minus.onClick.AddListener(() => { if (Trade.RemovePatrol(SaveSystem.Data, r, id)) Done(null); });
                    var plus = UIFactory.Button(lt, "Plus", "+", UIFactory.Hex("2E7D5B"), 32f);
                    UIFactory.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 2f), new Vector2(66f, 50f));
                    plus.interactable = inArmy > 0;
                    plus.onClick.AddListener(() =>
                    {
                        if (Trade.AddPatrol(SaveSystem.Data, r, id)) Done(null);
                        else toast?.Invoke("Orduda bu birlikten bölük kalmadı");
                    });
                }
            var hint = Txt(t, "Devriyeler ordudan ayrılır: savaşa katılmaz ama maaş alır ve ordu sınırından düşer. Atlı şövalye yolu en iyi korur. Sur ve Askeri kararname de güvenliği artırır.",
                19f, UIFactory.Brown, false, new Vector2(26f, -462f - row * 60f), new Vector2(760f, 64f));
            hint.enableAutoSizing = true; hint.fontSizeMin = 14f; hint.fontSizeMax = 19f;
        }

        void Done(string msg)
        {
            if (!string.IsNullOrEmpty(msg)) toast?.Invoke(msg);
            SaveSystem.Save();
            changed?.Invoke();
            Rebuild();
        }

        static TMP_Text Txt(Transform parent, string text, float size, Color color, bool bold, Vector2 pos, Vector2 box)
        {
            var t = UIFactory.Text(parent, "T", text, size, color, TextAlignmentOptions.TopLeft, bold);
            UIFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, box);
            return t;
        }
    }
}
