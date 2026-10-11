using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Harita arayüzü (koddan kurulur): altın, krallık, ordu düğmesi, bildirim, saldırı paneli, dükkân.</summary>
    public class MapUI : MonoBehaviour
    {
        MapController map;
        TMP_Text goldText;
        TMP_Text kingdomText;
        TMP_Text incomeText;
        TMP_Text toastText;
        GameObject toastBox;
        AttackPanel attack;
        ArmyShopUI shop;
        CityPanel cityPanel;
        LettersUI letters;
        DiplomacyPanel diplomacy;
        TradePanel tradePanel;
        TMP_Text letterBadge;
        GameObject letterBadgeRoot;
        Coroutine toastRoutine;
        readonly System.Collections.Generic.Queue<string> toasts = new System.Collections.Generic.Queue<string>();

        public bool IsModalOpen => (attack != null && attack.IsOpen) || (shop != null && shop.IsOpen) || (cityPanel != null && cityPanel.IsOpen) ||
                                   (letters != null && letters.IsOpen) || (diplomacy != null && diplomacy.IsOpen) || (tradePanel != null && tradePanel.IsOpen);

        public void Build(MapController controller)
        {
            map = controller;
            var canvas = UIFactory.Canvas("MapUI", 10);
            var t = canvas.transform;
            canvasRoot = t;

            // sol üst: altın
            var goldBox = UIFactory.Chip(t, "Gold", new Vector2(300f, 84f));
            UIFactory.Place(goldBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(300f, 84f));
            var coin = UIFactory.Panel(goldBox.transform, "Coin", UIFactory.Gold, GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 0f), new Vector2(52f, 52f));
            var coinIn = UIFactory.Panel(coin.transform, "Inner", UIFactory.Hex("FFD36E"), GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Stretch(coinIn.rectTransform, 9f);
            goldText = UIFactory.Text(goldBox.transform, "Value", "0", 40f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Place(goldText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(200f, 70f));
            incomeText = UIFactory.Text(t, "Income", "", 26f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Left);
            UIFactory.Place(incomeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(620f, 40f));
            incomeText.outlineWidth = 0.22f;
            incomeText.outlineColor = new Color32(20, 24, 36, 220);

            // sol alt: krallık
            var kBox = UIFactory.Chip(t, "Kingdom", new Vector2(360f, 64f));
            UIFactory.Place(kBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(360f, 64f));
            var crown = UIFactory.Rounded(kBox.transform, "Dot", UIFactory.Hex("5AA2FF"), 8f);
            UIFactory.Place(crown.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(16f, 16f));
            kingdomText = UIFactory.Text(kBox.transform, "Value", "", 28f, Color.white, TextAlignmentOptions.Left, true);
            UIFactory.Stretch(kingdomText.rectTransform, 52f, 0f, 16f, 0f);

            // sağ üst: ordu
            var armyBtn = UIFactory.Button(t, "ArmyButton", "Ordu", UIFactory.Gold, 38f);
            UIFactory.Place((RectTransform)armyBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -20f), new Vector2(230f, 92f));

            // bildirim
            toastBox = UIFactory.Chip(t, "Toast", new Vector2(1000f, 96f), 28f).gameObject;
            UIFactory.Place((RectTransform)toastBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1000f, 96f));
            toastText = UIFactory.Text(toastBox.transform, "Text", "", 30f, Color.white, TextAlignmentOptions.Center, true);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 20f;
            toastText.fontSizeMax = 30f;
            UIFactory.Stretch(toastText.rectTransform, 30f, 8f, 30f, 8f);
            toastBox.GetComponent<Image>().raycastTarget = false;
            toastBox.SetActive(false);

            // ipucu (ilk açılışta)
            attack = AttackPanel.Create(t);
            shop = ArmyShopUI.Create(t, this);
            cityPanel = CityPanel.Create(t, () => map.RefreshAll(), Toast);

            // sağ: saray (alt kenar telefonlarda hareket çubuğuyla çakışmasın diye üstte)
            var dipBtn = UIFactory.Button(t, "DiplomacyButton", "Diplomasi", UIFactory.Hex("5B3A8C"), 32f);
            UIFactory.Place((RectTransform)dipBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -268f), new Vector2(240f, 84f));
            var letBtn = UIFactory.Button(t, "LettersButton", "Mektuplar", UIFactory.Hex("8A5A2B"), 32f);
            UIFactory.Place((RectTransform)letBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -364f), new Vector2(240f, 84f));
            var badge = UIFactory.Rounded(letBtn.transform, "Badge", UIFactory.Red, 20f);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -6f), new Vector2(42f, 42f));
            badge.raycastTarget = false;
            letterBadge = UIFactory.Text(badge.transform, "N", "0", 24f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(letterBadge.rectTransform);
            letterBadgeRoot = badge.gameObject;

            var tradeBtn = UIFactory.Button(t, "TradeButton", "Ticaret", UIFactory.Hex("2E7D5B"), 32f);
            UIFactory.Place((RectTransform)tradeBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -460f), new Vector2(240f, 84f));
            tradePanel = TradePanel.Create(t, map.InfoDict, () => map.RefreshAll(), Toast);
            tradeBtn.onClick.AddListener(() => tradePanel.Open());
            toastBox.transform.SetAsLastSibling();

            letters = LettersUI.Create(t, (l, i) => map.AnswerLetter(l, i), Toast);
            diplomacy = DiplomacyPanel.Create(t, (a, r) => map.Diplomacy(a, r));
            letBtn.onClick.AddListener(letters.OpenInbox);
            dipBtn.onClick.AddListener(diplomacy.Open);
            armyBtn.onClick.AddListener(shop.Open);

            RefreshTop();
        }

        public void RefreshTop()
        {
            if (goldText != null) goldText.text = SaveSystem.Data.gold.ToString("N0");
            if (incomeText != null && map != null)
            {
                int inc = map.IncomePerMinute();
                int up = Mathf.RoundToInt(KingdomSim.Upkeep(SaveSystem.Data));
                int net = inc - up;
                incomeText.text = "<color=" + (net < 0 ? "#FF7A6B" : "#BFF2A6") + ">" + (net >= 0 ? "+" : "") + net + " / dk</color>  (gelir " + inc + ", gider " + up + ")";
            }
            if (goldText != null) goldText.color = SaveSystem.Data.gold < 0 ? UIFactory.Hex("FF7A6B") : Color.white;
            RefreshRivals();
            if (kingdomText != null && map != null) kingdomText.text = "Krallık  " + map.OwnedCount() + " / " + map.CityCount + " şehir";
        }

        public void ShowAttack(MapCity city, CastleData castle, bool canAttack, Action onAttack) => attack.Show(city, castle, canAttack, onAttack);

        public void ShowCity(MapCity city) => cityPanel.Show(city);

        public void ShowTrade(string roadKey) => tradePanel.Open(roadKey);

        public void ShowLetter(Letter l)
        {
            if (letters == null) return;
            // başka bir pencere açıksa zorla açma: rozet yeterli
            if ((attack != null && attack.IsOpen) || (shop != null && shop.IsOpen) || (cityPanel != null && cityPanel.IsOpen) || (diplomacy != null && diplomacy.IsOpen) || (tradePanel != null && tradePanel.IsOpen) || letters.IsOpen)
            {
                RefreshLetters();
                return;
            }
            letters.Show(l);
        }

        public void RefreshLetters()
        {
            if (letterBadge == null) return;
            int n = 0;
            foreach (var l in SaveSystem.Data.letters) if (l.NeedsAnswer || !l.read) n++;
            letterBadgeRoot.SetActive(n > 0);
            letterBadge.text = n.ToString();
        }

        // ------------------------------------------------------------------ rakipler

        readonly System.Collections.Generic.List<TMP_Text> rivalTexts = new System.Collections.Generic.List<TMP_Text>();
        readonly System.Collections.Generic.List<Image> rivalDots = new System.Collections.Generic.List<Image>();
        Transform canvasRoot;

        /// <summary>Sağ üstte, Ordu düğmesinin altında rakip krallıkların özeti.</summary>
        public void BuildRivals()
        {
            if (canvasRoot == null) return;
            var data = SaveSystem.Data;
            for (int i = 0; i < data.rivals.Count; i++)
            {
                var chip = UIFactory.Chip(canvasRoot, "Rival" + i, new Vector2(500f, 56f));
                UIFactory.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -128f - i * 66f), new Vector2(500f, 56f));
                chip.raycastTarget = false;
                var dot = UIFactory.Rounded(chip.transform, "Dot", RivalAI.Get(data.rivals[i].id).color, 9f);
                UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(18f, 18f));
                dot.raycastTarget = false;
                var t = UIFactory.Text(chip.transform, "Text", "", 23f, Color.white, TextAlignmentOptions.Left, true);
                UIFactory.Stretch(t.rectTransform, 50f, 0f, 16f, 0f);
                t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 23f;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                rivalTexts.Add(t);
                rivalDots.Add(dot);
            }
            RefreshRivals();
        }

        public void RefreshRivals()
        {
            var data = SaveSystem.Data;
            for (int i = 0; i < rivalTexts.Count && i < data.rivals.Count; i++)
            {
                var r = data.rivals[i];
                var def = RivalAI.Get(r.id);
                if (!r.alive)
                {
                    rivalTexts[i].text = "<s>" + def.name + "</s>  yıkıldı";
                    rivalTexts[i].color = new Color(1f, 1f, 1f, 0.5f);
                    continue;
                }
                int count = RivalAI.OwnedBy(data, r.id).Count;
                rivalTexts[i].text = def.name + "   " + count + " şehir  ·  güç " + Mathf.RoundToInt(r.power) + (r.Attacking ? "  ·  <color=#FF7A6B>saldırıda</color>" : "");
            }
        }

        /// <summary>Bildirimler sırayla gösterilir; aynı mesaj sırada varsa tekrar eklenmez.</summary>
        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message) || toasts.Contains(message)) return;
            toasts.Enqueue(message);
            if (toastRoutine == null) toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            while (toasts.Count > 0)
            {
                toastText.text = toasts.Peek();
                toastBox.transform.SetAsLastSibling(); // pencerelerin üstünde görünsün
                toastBox.SetActive(true);
                FX.Punch(toastBox.transform, 0.06f, 0.25f);
                yield return new WaitForSeconds(toasts.Count > 1 ? 2.2f : 3f);
                toasts.Dequeue();
            }
            toastBox.GetComponent<Image>().raycastTarget = false;
            toastBox.SetActive(false);
            toastRoutine = null;
        }
    }
}
