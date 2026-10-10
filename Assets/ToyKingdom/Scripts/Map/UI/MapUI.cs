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
        Coroutine toastRoutine;
        readonly System.Collections.Generic.Queue<string> toasts = new System.Collections.Generic.Queue<string>();

        public bool IsModalOpen => (attack != null && attack.IsOpen) || (shop != null && shop.IsOpen) || (cityPanel != null && cityPanel.IsOpen);

        public void Build(MapController controller)
        {
            map = controller;
            var canvas = UIFactory.Canvas("MapUI", 10);
            var t = canvas.transform;

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
            UIFactory.Place(incomeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(420f, 40f));
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
            armyBtn.onClick.AddListener(shop.Open);

            RefreshTop();
        }

        public void RefreshTop()
        {
            if (goldText != null) goldText.text = SaveSystem.Data.gold.ToString("N0");
            if (incomeText != null && map != null) incomeText.text = "+" + map.IncomePerMinute() + " altın / dakika";
            if (kingdomText != null && map != null) kingdomText.text = "Krallık  " + map.OwnedCount() + " / " + map.CityCount + " şehir";
        }

        public void ShowAttack(MapCity city, CastleData castle, bool canAttack, Action onAttack) => attack.Show(city, castle, canAttack, onAttack);

        public void ShowCity(MapCity city) => cityPanel.Show(city);

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
