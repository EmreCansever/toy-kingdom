using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Savaş arayüzü: süre, kapı canı, bölük kartları, komuta puanı, ipucu ve sonuç paneli.</summary>
    public class BattleHUD : MonoBehaviour
    {
        BattleManager bm;
        TMP_Text timerText;
        Image gateFill;
        TMP_Text gateText;
        TMP_Text commandText;
        Image commandFill;
        TMP_Text hintText;
        float hintTimer;
        GameObject resultPanel;
        TMP_Text resultTitle;
        TMP_Text resultDetail;
        Image resultHeader;
        readonly List<UnitCardUI> cards = new List<UnitCardUI>();

        void Start()
        {
            bm = BattleManager.Instance;
            if (bm == null || bm.Castle == null) { enabled = false; return; }
            Build();
            bm.SelectedUnitChanged += OnSelected;
            bm.BattleEnded += ShowResult;
            bm.Hint += ShowHint;

            UnitData first = null;
            foreach (var c in cards)
                if (first == null && SaveSystem.Data.GetArmyCount(c.Data.unitId) > 0) first = c.Data;
            if (first != null) bm.SelectUnit(first);
            ShowHint("Bir bölük seç, işaretli bölgeden kaleye doğru parmağınla rota çiz", 6f);
        }

        void OnDestroy()
        {
            if (bm == null) return;
            bm.SelectedUnitChanged -= OnSelected;
            bm.BattleEnded -= ShowResult;
            bm.Hint -= ShowHint;
        }

        void Build()
        {
            var canvas = UIFactory.Canvas("BattleUI", 10);
            var t = canvas.transform;

            // üst orta: kale adı ve kapı canı
            var top = UIFactory.Chip(t, "Top", new Vector2(620f, 108f), 26f);
            UIFactory.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(620f, 108f));
            var name = UIFactory.Text(top.transform, "Name", bm.Castle.displayName, 32f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(580f, 46f));
            gateFill = UIFactory.Bar(top.transform, "GateBar", UIFactory.Red, new Vector2(0f, 16f), new Vector2(560f, 34f), new Vector2(0.5f, 0f));
            gateText = UIFactory.Text(gateFill.transform.parent, "Label", "Kapı", 24f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(gateText.rectTransform);
            if (UIFactory.LabelShadow != null) gateText.fontSharedMaterial = UIFactory.LabelShadow;

            // sağ üst: süre
            var timerBox = UIFactory.Chip(t, "Timer", new Vector2(170f, 84f));
            UIFactory.Place(timerBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(170f, 84f));
            timerText = UIFactory.Text(timerBox.transform, "Value", "2:30", 44f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(timerText.rectTransform);

            // sol üst: geri çekil
            var retreat = UIFactory.Button(t, "Retreat", "Geri çekil", UIFactory.Hex("8C8577"), 28f);
            UIFactory.Place((RectTransform)retreat.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(220f, 80f));
            retreat.onClick.AddListener(bm.Retreat);

            // alt: kartlar
            var row = UIFactory.Rect("Cards", t);
            UIFactory.Place(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 10f), new Vector2(900f, 180f));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18f;
            hlg.childAlignment = TextAnchor.LowerLeft;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            var db = UnitDatabase.Instance;
            if (db != null)
                foreach (var u in db.units)
                    if (u != null) cards.Add(UnitCardUI.Create(row, u));

            // sağ alt: komuta puanı
            var cmdBox = UIFactory.Chip(t, "Command", new Vector2(340f, 116f), 26f);
            UIFactory.Place(cmdBox.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(340f, 116f));
            var cmdLabel = UIFactory.Text(cmdBox.transform, "Label", "Komuta puanı", 24f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, true);
            UIFactory.Place(cmdLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(320f, 36f));
            commandFill = UIFactory.Bar(cmdBox.transform, "Bar", UIFactory.Purple, new Vector2(0f, 16f), new Vector2(296f, 46f), new Vector2(0.5f, 0f));
            commandText = UIFactory.Text(commandFill.transform.parent, "Value", "0/10", 28f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(commandText.rectTransform);
            if (UIFactory.LabelShadow != null) commandText.fontSharedMaterial = UIFactory.LabelShadow;

            // ipucu
            hintText = UIFactory.Text(t, "Hint", "", 32f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1300f, 60f));
            hintText.outlineWidth = 0.25f;
            hintText.outlineColor = new Color32(40, 20, 0, 255);

            // sonuç paneli
            var dim = UIFactory.Panel(t, "Result", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var box = UIFactory.Window(dim.transform, "Box", new Vector2(820f, 540f), "", UIFactory.Blue, out resultTitle);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 540f));
            resultHeader = box.transform.Find("Border/Header").GetComponent<Image>();
            resultTitle.alignment = TextAlignmentOptions.Center;
            UIFactory.Stretch(resultTitle.rectTransform, 30f, 0f, 30f, 0f);
            resultDetail = UIFactory.Text(box.transform, "Detail", "", 34f, UIFactory.BrownDark, TextAlignmentOptions.Center);
            UIFactory.Place(resultDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(740f, 200f));
            var back = UIFactory.Button(box.transform, "Back", "Haritaya dön", UIFactory.Blue, 36f);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 100f));
            back.onClick.AddListener(bm.ReturnToMap);
            resultPanel = dim.gameObject;
            resultPanel.SetActive(false);
        }

        void OnSelected(UnitData u)
        {
            foreach (var c in cards) c.SetSelected(c.Data == u);
        }

        public void ShowHint(string msg) => ShowHint(msg, 3f);

        public void ShowHint(string msg, float seconds)
        {
            if (hintText == null) return;
            hintText.text = msg;
            hintTimer = seconds;
        }

        void Update()
        {
            if (bm == null) return;
            if (bm.Command != null)
            {
                commandText.text = Mathf.FloorToInt(bm.Command.Current) + "/" + bm.Command.Max;
                UIFactory.SetBar(commandFill, bm.Command.Current / bm.Command.Max);
            }
            int s = Mathf.CeilToInt(bm.TimeLeft);
            timerText.text = (s / 60) + ":" + (s % 60).ToString("00");
            timerText.color = s <= 20 ? UIFactory.Hex("FF6B5E") : Color.white;

            if (bm.Capturing)
            {
                UIFactory.SetBar(gateFill, bm.CaptureProgress);
                gateFill.color = UIFactory.Blue;
                gateText.text = "Kapıyı tut  %" + Mathf.RoundToInt(bm.CaptureProgress * 100f);
            }
            else
            {
                float hp = 1f;
                foreach (var g in bm.Field.Gates) if (g != null) hp = Mathf.Min(hp, g.Normalized);
                UIFactory.SetBar(gateFill, hp);
                gateText.text = bm.Field.Gates.Count > 1 ? "Kapılar" : "Kapı";
            }

            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                if (hintTimer <= 0f) hintText.text = "";
            }
        }

        void ShowResult(BattleResult r)
        {
            hintText.text = "";
            resultPanel.SetActive(true);
            if (resultHeader != null)
            {
                resultHeader.color = r.victory ? UIFactory.Green : UIFactory.Red;
                var flat = resultHeader.transform.Find("Flat");
                if (flat != null) flat.GetComponent<Image>().color = UIFactory.Darken(resultHeader.color, 0.88f);
            }
            if (r.victory)
            {
                resultTitle.text = r.cityName + " fethedildi!";
                resultDetail.text = "Yıldız: " + r.stars + "/3\n+" + r.goldEarned + " altın" +
                                    (r.squadsLost > 0 ? "\nKaybedilen bölük: " + r.squadsLost : "");
            }
            else
            {
                resultTitle.text = "Saldırı başarısız";
                resultDetail.text = "Ordunu güçlendirip tekrar dene.\nYarısı hayatta kalan bölükler geri döndü.";
            }
        }
    }
}
