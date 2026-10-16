using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Diplomasi: her krallığın hükümdarı, ilişki, anlaşma durumu ve elçilik eylemleri.</summary>
    public class DiplomacyPanel : MonoBehaviour
    {
        GameObject root;
        RectTransform content;
        Func<string, RivalState, string> act;
        float refresh;

        public bool IsOpen => root != null && root.activeSelf;

        public static DiplomacyPanel Create(Transform canvas, Func<string, RivalState, string> act)
        {
            var dim = UIFactory.Panel(canvas, "Diplomacy", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<DiplomacyPanel>();
            p.root = dim.gameObject;
            p.act = act;
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1600f, 960f), "Diplomasi", UIFactory.Hex("5B3A8C"), out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 960f));
            var header = box.transform.Find("Border/Header");
            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(() => p.root.SetActive(false));
            p.strength = UIFactory.Text(header, "Strength", "", 26f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Right);
            UIFactory.Place(p.strength.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(1000f, 60f));
            p.strength.enableAutoSizing = true; p.strength.fontSizeMin = 18f; p.strength.fontSizeMax = 26f;
            p.content = UIFactory.Rect("Content", box.transform);
            UIFactory.Stretch(p.content, 30f, 30f, 30f, 120f);
            p.root.SetActive(false);
            return p;
        }

        TMP_Text strength;

        public void Open()
        {
            root.SetActive(true);
            Rebuild();
        }

        void Update()
        {
            if (!IsOpen) return;
            refresh += Time.deltaTime;
            if (refresh > 3f) { refresh = 0f; Rebuild(); }
        }

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            var data = SaveSystem.Data;
            string kings = RivalWars.Status(data);
            strength.text = (kings.Length > 0 ? kings + "   ·   " : "") + "Senin ordu gücün: <b>" + Court.PlayerStrength(data) + "</b>";
            for (int i = 0; i < data.rivals.Count; i++) Card(data.rivals[i], i);
        }

        void Card(RivalState r, int index)
        {
            var def = RivalAI.Get(r.id);
            var data = SaveSystem.Data;
            var card = UIFactory.Rounded(content, "Card" + index, UIFactory.ParchmentDark, 22f);
            UIFactory.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(index * 776f, 0f), new Vector2(756f, 790f));
            var t = card.transform;

            var portrait = KingPortrait.Create(t, r.id, 200f);
            UIFactory.Place(portrait, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(200f, 200f));

            Txt(t, def.kingTitle + " " + def.king, 40f, UIFactory.BrownDark, true, new Vector2(250f, -30f), new Vector2(480f, 54f));
            Txt(t, def.name, 26f, UIFactory.Darken(def.color, 0.8f), true, new Vector2(250f, -84f), new Vector2(480f, 36f));
            Txt(t, def.traitName, 24f, UIFactory.Brown, true, new Vector2(250f, -124f), new Vector2(480f, 34f));
            var info = Txt(t, def.traitInfo, 20f, UIFactory.Ink, false, new Vector2(250f, -160f), new Vector2(480f, 70f));
            info.enableAutoSizing = true; info.fontSizeMin = 15f; info.fontSizeMax = 20f;

            if (!r.alive)
            {
                Txt(t, "Krallık yıkıldı. Toprakları artık senin.", 32f, UIFactory.Green, true, new Vector2(26f, -300f), new Vector2(700f, 60f));
                return;
            }

            // ilişki
            Txt(t, "İlişki", 26f, UIFactory.BrownDark, true, new Vector2(26f, -250f), new Vector2(200f, 40f));
            float rel = Mathf.Clamp(r.relation, -100f, 100f);
            Color rc = rel < -40f ? UIFactory.Red : rel < 0f ? UIFactory.Gold : rel < 40f ? UIFactory.Hex("8FBF5A") : UIFactory.Green;
            var fill = UIFactory.Bar(t, "Rel", rc, new Vector2(150f, -256f), new Vector2(400f, 30f), new Vector2(0f, 1f));
            UIFactory.SetBar(fill, (rel + 100f) / 200f);
            string relName = rel < -40f ? "Düşman" : rel < -10f ? "Gergin" : rel < 20f ? "Nötr" : rel < 50f ? "Dostane" : "Müttefik gibi";
            Txt(t, relName + " (" + Mathf.RoundToInt(rel) + ")", 24f, rc, true, new Vector2(566f, -250f), new Vector2(180f, 40f));

            // durum
            string status;
            if (r.AtPeace)
            {
                long left = Math.Max(0L, r.treatyUntilTicks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
                status = "<color=#3E9E4F>Barış anlaşması: " + (left / 60) + ":" + (left % 60).ToString("00") + "</color>";
            }
            else if (r.Attacking) status = "<color=#B8382F>Ordusu şehrine yürüyor!</color>";
            else if (r.awaitingAnswer) status = "<color=#B86E00>Mektubuna cevap bekliyor</color>";
            else status = "Savaş hali (anlaşma yok)";
            Txt(t, status, 26f, UIFactory.BrownDark, true, new Vector2(26f, -302f), new Vector2(700f, 40f));
            int cities = RivalAI.OwnedBy(data, r.id).Count;
            int mine = Court.PlayerStrength(data);
            float threat = RivalAI.Threat(data, r);
            var st = Txt(t, cities + " şehir  ·  ordu gücü " + Mathf.RoundToInt(r.power) + (mine > r.power ? "  ·  <color=#3E9E4F>senden zayıf</color>" : "  ·  <color=#B8382F>senden güçlü</color>") +
                (threat > 0.25f ? "  ·  <color=#B86E00>ordundan " + (threat > 0.6f ? "korkuyor, silahlanıyor" : "tedirgin") + "</color>" : ""),
                24f, UIFactory.Brown, false, new Vector2(26f, -346f), new Vector2(700f, 36f));
            st.enableAutoSizing = true; st.fontSizeMin = 16f; st.fontSizeMax = 24f;

            // eylemler
            AddAction("Hediye gönder  (" + Court.GiftCost + ")", "İlişki artar. Açgözlü krallar daha çok sever.", UIFactory.Green, "gift", r, 0, data.gold >= Court.GiftCost);
            AddAction("Barış öner  (" + Court.PeaceCost + ")", "15 dk barış. Başarı ilişkiye ve güce bağlı; reddedilirse ücret yanar.", UIFactory.Blue, "peace", r, 1, data.gold >= Court.PeaceCost && !r.AtPeace);
            AddAction("Haraç iste", "Ondan güçlüysen altın öder; reddederse öfkelenip saldırır.", UIFactory.Gold, "tribute", r, 2, !r.AtPeace);
            AddAction(r.AtPeace ? "Barışı boz" : "Savaş ilan et", r.AtPeace ? "Yeminbozan olursun: tüm şehirlerinde huzursuzluk +12, herkesle ilişki düşer." : "İlişki -15.",
                UIFactory.Red, "war", r, 3, true);
            if (RivalWars.Other(data, r) != null)
                AddAction("Kışkırt  (" + RivalWars.InciteCost + ")", "Onu komşu krala düşürür (aralarındaki ilişki -25). %25 ihtimalle elçin yakalanır.", UIFactory.Hex("6B4A8F"), "incite", r, 4, data.gold >= RivalWars.InciteCost);

            void AddAction(string label, string hint, Color col, string id, RivalState rs, int row, bool enabled)
            {
                float y = -396f - row * 78f;
                var b = UIFactory.Button(t, id, label, col, 26f);
                UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, y), new Vector2(330f, 70f));
                b.interactable = enabled;
                var h = Txt(t, hint, 20f, UIFactory.Brown, false, new Vector2(372f, y - 2f), new Vector2(360f, 70f));
                h.enableAutoSizing = true; h.fontSizeMin = 14f; h.fontSizeMax = 20f;
                b.onClick.AddListener(() => { act?.Invoke(id, rs); Rebuild(); });
            }
        }

        static TMP_Text Txt(Transform parent, string text, float size, Color color, bool bold, Vector2 pos, Vector2 box)
        {
            var t = UIFactory.Text(parent, "T", text, size, color, TextAlignmentOptions.TopLeft, bold);
            UIFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, box);
            return t;
        }
    }
}
