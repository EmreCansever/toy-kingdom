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
            var top = UIFactory.Frame(t, "Top", new Vector2(640f, 110f));
            UIFactory.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(640f, 110f));
            var name = UIFactory.Text(top.transform, "Name", bm.Castle.displayName, 40f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(600f, 50f));
            var barBg = UIFactory.Panel(top.transform, "GateBar", UIFactory.Ink);
            UIFactory.Place(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(560f, 30f));
            var white = GameArt.Instance != null ? GameArt.Instance.white : null;
            gateFill = UIFactory.Panel(barBg.transform, "Fill", UIFactory.Red, white);
            UIFactory.Stretch(gateFill.rectTransform, 3f);
            gateFill.type = Image.Type.Filled;
            gateFill.fillMethod = Image.FillMethod.Horizontal;
            gateText = UIFactory.Text(barBg.transform, "Label", "Kapı", 22f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(gateText.rectTransform);

            // sağ üst: süre
            var timerBox = UIFactory.Frame(t, "Timer", new Vector2(180f, 90f));
            UIFactory.Place(timerBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(180f, 90f));
            timerText = UIFactory.Text(timerBox.transform, "Value", "2:30", 50f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(timerText.rectTransform);

            // sol üst: geri çekil
            var retreat = UIFactory.Button(t, "Retreat", "Geri çekil", UIFactory.Brown, 30f);
            UIFactory.Place((RectTransform)retreat.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(230f, 80f));
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
            var cmdBox = UIFactory.Frame(t, "Command", new Vector2(340f, 120f));
            UIFactory.Place(cmdBox.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 10f), new Vector2(340f, 120f));
            var cmdLabel = UIFactory.Text(cmdBox.transform, "Label", "Komuta puanı", 26f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(cmdLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(320f, 36f));
            var cmdBg = UIFactory.Panel(cmdBox.transform, "Bar", UIFactory.Ink);
            UIFactory.Place(cmdBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(300f, 50f));
            commandFill = UIFactory.Panel(cmdBg.transform, "Fill", UIFactory.Hex("7F77DD"), white);
            UIFactory.Stretch(commandFill.rectTransform, 4f);
            commandFill.type = Image.Type.Filled;
            commandFill.fillMethod = Image.FillMethod.Horizontal;
            commandText = UIFactory.Text(cmdBg.transform, "Value", "0/10", 32f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(commandText.rectTransform);

            // ipucu
            hintText = UIFactory.Text(t, "Hint", "", 32f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1300f, 60f));
            hintText.outlineWidth = 0.25f;
            hintText.outlineColor = new Color32(40, 20, 0, 255);

            // sonuç paneli
            var dim = UIFactory.Panel(t, "Result", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var box = UIFactory.Frame(dim.transform, "Box", new Vector2(820f, 520f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 520f));
            resultTitle = UIFactory.Text(box.transform, "Title", "", 56f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 90f));
            resultDetail = UIFactory.Text(box.transform, "Detail", "", 36f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(resultDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 200f));
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
                commandFill.fillAmount = bm.Command.Current / bm.Command.Max;
            }
            int s = Mathf.CeilToInt(bm.TimeLeft);
            timerText.text = (s / 60) + ":" + (s % 60).ToString("00");
            timerText.color = s <= 20 ? UIFactory.Red : UIFactory.BrownDark;

            float hp = 1f;
            foreach (var g in bm.Field.Gates) if (g != null) hp = Mathf.Min(hp, g.Normalized);
            gateFill.fillAmount = hp;
            gateText.text = bm.Field.Gates.Count > 1 ? "Kapılar" : "Kapı";

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
