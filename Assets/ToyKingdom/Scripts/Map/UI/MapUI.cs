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
            var goldBox = UIFactory.Frame(t, "Gold", new Vector2(320f, 90f));
            UIFactory.Place(goldBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(320f, 90f));
            var coin = UIFactory.Panel(goldBox.transform, "Coin", UIFactory.Gold, GameArt.Instance != null ? GameArt.Instance.circle : null);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(54f, 54f));
            goldText = UIFactory.Text(goldBox.transform, "Value", "0", 44f, UIFactory.BrownDark, TextAlignmentOptions.Left, true);
            UIFactory.Place(goldText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(220f, 70f));

            // sol alt: krallık
            var kBox = UIFactory.Frame(t, "Kingdom", new Vector2(420f, 70f));
            UIFactory.Place(kBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(420f, 70f));
            kingdomText = UIFactory.Text(kBox.transform, "Value", "", 32f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(kingdomText.rectTransform);

            // sağ üst: ordu
            var armyBtn = UIFactory.Button(t, "ArmyButton", "Ordu", UIFactory.Blue, 40f);
            UIFactory.Place((RectTransform)armyBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -16f), new Vector2(240f, 90f));

            // bildirim
            toastBox = UIFactory.Frame(t, "Toast", new Vector2(1100f, 110f)).gameObject;
            UIFactory.Place((RectTransform)toastBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 110f));
            toastText = UIFactory.Text(toastBox.transform, "Text", "", 32f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 20f;
            toastText.fontSizeMax = 32f;
            UIFactory.Stretch(toastText.rectTransform, 12f);
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
            if (kingdomText != null && map != null) kingdomText.text = "Krallık: " + map.OwnedCount() + "/" + map.CityCount + " şehir";
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
            toastBox.SetActive(false);
            toastRoutine = null;
        }
    }
}
