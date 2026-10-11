using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Şehre varınca açılan saldırı paneli (koddan kurulur): başlık, savunma kutucukları, ganimet, saldır düğmesi.</summary>
    public class AttackPanel : MonoBehaviour
    {
        GameObject root;
        TMP_Text title;
        Image header;
        TMP_Text description;
        RectTransform pips;
        readonly List<TMP_Text> statValues = new List<TMP_Text>();
        readonly List<TMP_Text> statLabels = new List<TMP_Text>();
        TMP_Text rewardValue, rewardLabel, incomeValue, incomeLabel;
        TMP_Text status;
        Button attackButton;
        TMP_Text attackLabel;
        Action onAttack;

        public bool IsOpen => root != null && root.activeSelf;

        const float W = 920f, H = 760f;

        public static AttackPanel Create(Transform canvas)
        {
            var dim = UIFactory.Panel(canvas, "AttackPanel", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<AttackPanel>();
            p.root = dim.gameObject;

            var box = UIFactory.Window(dim.transform, "Box", new Vector2(W, H), "", UIFactory.Red, out p.title);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            p.header = box.transform.Find("Border/Header").GetComponent<Image>();
            var b = box.transform;

            // başlıkta zorluk noktaları
            p.pips = UIFactory.Rect("Pips", p.header.transform);
            UIFactory.Place(p.pips, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(5 * 30f, 24f));
            for (int i = 0; i < 5; i++)
            {
                var pip = UIFactory.Rounded(p.pips, "Pip" + i, Color.white, 12f);
                UIFactory.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 30f, 0f), new Vector2(22f, 22f));
            }

            p.description = UIFactory.Text(b, "Description", "", 30f, UIFactory.Ink, TextAlignmentOptions.Center);
            UIFactory.Place(p.description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(W - 120f, 86f));
            p.description.enableAutoSizing = true;
            p.description.fontSizeMin = 20f;
            p.description.fontSizeMax = 30f;

            // savunma kutucukları
            float tileW = 182f, gap = 14f;
            float startX = -(4 * tileW + 3 * gap) * 0.5f + tileW * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                var tile = UIFactory.Rounded(b, "Stat" + i, UIFactory.ParchmentDark, 18f);
                UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(startX + i * (tileW + gap), -228f), new Vector2(tileW, 132f));
                var v = UIFactory.Text(tile.transform, "Value", "0", 50f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
                UIFactory.Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(tileW, 64f));
                var l = UIFactory.Text(tile.transform, "Label", "", 25f, UIFactory.Brown, TextAlignmentOptions.Center);
                UIFactory.Place(l.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(tileW - 12f, 50f));
                l.enableAutoSizing = true; l.fontSizeMin = 18f; l.fontSizeMax = 25f;
                p.statValues.Add(v);
                p.statLabels.Add(l);
            }

            // ganimet ve gelir
            var reward = UIFactory.Rounded(b, "Reward", UIFactory.Hex("FFF1CF"), 18f);
            UIFactory.Place(reward.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-7f, -378f), new Vector2(370f, 104f));
            p.rewardValue = UIFactory.Text(reward.transform, "Value", "", 40f, UIFactory.Hex("B86E00"), TextAlignmentOptions.Center, true);
            UIFactory.Place(p.rewardValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(360f, 56f));
            p.rewardLabel = UIFactory.Text(reward.transform, "Label", "Ganimet (altın)", 24f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(p.rewardLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(360f, 30f));

            var income = UIFactory.Rounded(b, "Income", UIFactory.Hex("E3F0FF"), 18f);
            UIFactory.Place(income.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(7f, -378f), new Vector2(370f, 104f));
            p.incomeValue = UIFactory.Text(income.transform, "Value", "", 40f, UIFactory.Hex("1F5DB0"), TextAlignmentOptions.Center, true);
            UIFactory.Place(p.incomeValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(360f, 56f));
            p.incomeLabel = UIFactory.Text(income.transform, "Label", "Gelir (altın / dakika)", 24f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(p.incomeLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(360f, 30f));

            p.status = UIFactory.Text(b, "Status", "", 25f, UIFactory.Red, TextAlignmentOptions.Center, true);
            UIFactory.Place(p.status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(W - 120f, 40f));

            p.attackButton = UIFactory.Button(b, "Attack", "Saldır", UIFactory.Red, 42f);
            UIFactory.Place((RectTransform)p.attackButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 44f), new Vector2(400f, 100f));
            p.attackLabel = p.attackButton.GetComponentInChildren<TMP_Text>();
            p.attackButton.onClick.AddListener(() =>
            {
                var cb = p.onAttack;
                p.Hide();
                cb?.Invoke();
            });
            var close = UIFactory.Button(b, "Close", "Vazgeç", UIFactory.Hex("8C8577"), 34f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 44f), new Vector2(220f, 100f));
            close.onClick.AddListener(p.Hide);

            p.Hide();
            return p;
        }

        void SetStat(int i, string value, string label)
        {
            statValues[i].text = value;
            statLabels[i].text = label;
        }

        public void Show(MapCity city, CastleData castle, bool canAttack, Action attack)
        {
            onAttack = attack;
            var c = castle;
            bool rebel = city.IsRebel;
            title.text = c != null ? c.displayName : city.DisplayName;
            attackLabel.text = rebel ? "Bastır" : "Saldır";
            string rivalId = city.RivalId;
            header.color = rebel ? UIFactory.Hex("A3271E") : rivalId != null ? UIFactory.Darken(RivalAI.Get(rivalId).color, 0.85f) : UIFactory.Red;
            var flat = header.transform.Find("Flat");
            if (flat != null) flat.GetComponent<Image>().color = UIFactory.Darken(header.color, 0.88f);
            description.text = c != null ? c.description : "";
            if (c != null)
            {
                var tags = new List<string>();
                if (rivalId != null)
                {
                    var rs = SaveSystem.Data.Rival(rivalId);
                    tags.Add(RivalAI.Get(rivalId).name + (rs != null && rs.capitalId == city.CityId ? " başkenti" : "") + (rs != null ? " (güç " + Mathf.RoundToInt(rs.power) + ")" : ""));
                }
                if (c.RallyDefense) tags.Add("Hızlı savunma");
                if (c.BoilingOil) tags.Add("Kızgın yağ");
                if (c.Courtyard) tags.Add("Son direniş: " + c.FinalStandSquads + " bölük");
                if (c.Sortie) tags.Add("Okçulara akın");
                if (tags.Count > 0) description.text += "\n<color=#B8382F><b>" + string.Join("  ·  ", tags) + "</b></color>";
            }

            if (c != null)
            {
                int towers = 4 + (c.midTowers ? 4 : 0);
                int squads = 0;
                foreach (var g in c.garrison) if (g != null) squads += g.squads;
                SetStat(0, towers.ToString(), "Okçu kulesi");
                SetStat(1, (c.secondGate ? 2 : 1).ToString(), c.secondGate ? "Kapı" : "Kapı");
                SetStat(2, squads.ToString(), "Savunma bölüğü");
                SetStat(3, c.wallArchers + (c.reinforcementCount > 0 ? " + " + c.reinforcementCount : ""),
                    c.reinforcementCount > 0 ? "Sur okçusu + takviye" : "Sur okçusu");
                rewardValue.text = c.goldReward.ToString("N0");
                incomeValue.text = rebel ? "Geri al" : "+" + c.passiveIncomePerMinute;
                incomeLabel.text = rebel ? "Şehir yeniden senin olur" : "Gelir (altın / dakika)";
                for (int i = 0; i < pips.childCount; i++)
                    pips.GetChild(i).GetComponent<Image>().color = i < c.difficulty ? Color.white : new Color(1f, 1f, 1f, 0.28f);
            }
            attackButton.interactable = canAttack;
            status.text = canAttack ? "" : "Önce bu şehre yolla bağlı bir şehri fethet";
            root.SetActive(true);
            FX.Punch(transform.Find("Box"), 0.04f, 0.2f);
        }

        public void Hide()
        {
            onAttack = null;
            if (root != null) root.SetActive(false);
        }
    }
}
