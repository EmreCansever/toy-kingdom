using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Oyun sonu ekranı: zafer ya da yenilgi, kısa özet, yeni oyun / devam düğmeleri.</summary>
    public class EndPanel : MonoBehaviour
    {
        GameObject root;
        Image header;
        TMP_Text title, reason, stats;
        Button continueBtn;
        Action onNewGame, onContinue;

        public bool IsOpen => root != null && root.activeSelf;

        public static EndPanel Create(Transform canvas, Action newGame, Action keepPlaying)
        {
            var dim = UIFactory.Panel(canvas, "EndGame", new Color(0.03f, 0.04f, 0.07f, 0.82f));
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<EndPanel>();
            p.root = dim.gameObject;
            p.onNewGame = newGame;
            p.onContinue = keepPlaying;

            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1100f, 760f), "", UIFactory.Gold, out p.title);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 760f));
            p.header = box.transform.Find("Border/Header").GetComponent<Image>();
            p.title.alignment = TextAlignmentOptions.Center;
            p.title.rectTransform.offsetMax = new Vector2(-34f, 0f);

            p.reason = UIFactory.Text(box.transform, "Reason", "", 34f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(p.reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(960f, 170f));
            p.reason.enableAutoSizing = true; p.reason.fontSizeMin = 22f; p.reason.fontSizeMax = 34f;

            var card = UIFactory.Rounded(box.transform, "Stats", UIFactory.ParchmentDark, 20f);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(900f, 230f));
            p.stats = UIFactory.Text(card.transform, "T", "", 28f, UIFactory.Ink, TextAlignmentOptions.Center);
            UIFactory.Stretch(p.stats.rectTransform, 24f);
            p.stats.lineSpacing = 8f;

            var again = UIFactory.Button(box.transform, "NewGame", "Yeni oyun", UIFactory.Red, 38f);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 50f), new Vector2(360f, 100f));
            again.onClick.AddListener(() => p.onNewGame?.Invoke());
            p.continueBtn = UIFactory.Button(box.transform, "Continue", "Oynamaya devam", UIFactory.Green, 34f);
            UIFactory.Place((RectTransform)p.continueBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 50f), new Vector2(360f, 100f));
            p.continueBtn.onClick.AddListener(() => { p.root.SetActive(false); p.onContinue?.Invoke(); });

            p.root.SetActive(false);
            return p;
        }

        public void Show(SaveData d, int owned, int total)
        {
            bool won = d.gameOver == Endgame.Won;
            title.text = won ? "ZAFER" : "YENİLGİ";
            header.color = won ? UIFactory.Hex("C98A1B") : UIFactory.Hex("8E2A20");
            var flat = header.transform.Find("Flat");
            if (flat != null) flat.GetComponent<Image>().color = UIFactory.Darken(header.color, 0.88f);
            reason.text = d.endReason;
            int minutes = Mathf.RoundToInt(d.playMinutes);
            stats.text = "Hüküm süren: <b>" + (minutes / 60 > 0 ? (minutes / 60) + " sa " : "") + (minutes % 60) + " dk</b>\n" +
                         "Krallık: <b>" + owned + " / " + total + " şehir</b>   ·   Yıkılan krallık: <b>" + Endgame.Fallen(d) + " / " + d.rivals.Count + "</b>\n" +
                         "İtibar: <b>" + Reputation.Name(d.reputation) + " " + Mathf.RoundToInt(d.reputation) + "</b>   ·   Hazine: <b>" + d.gold.ToString("N0") + " altın</b>";
            continueBtn.gameObject.SetActive(won);
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            FX.Banner(won ? "ZAFER!" : "TAHT KAYBEDİLDİ", won ? UIFactory.Gold : UIFactory.Red, 2f);
        }
    }
}
