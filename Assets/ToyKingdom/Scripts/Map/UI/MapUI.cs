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
        Coroutine toastRoutine;

        public bool IsModalOpen => (attack != null && attack.IsOpen) || (shop != null && shop.IsOpen);

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
            UIFactory.Place((RectTransform)armyBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(240f, 90f));

            // bildirim
            toastBox = UIFactory.Frame(t, "Toast", new Vector2(1100f, 80f)).gameObject;
            UIFactory.Place((RectTransform)toastBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 80f));
            toastText = UIFactory.Text(toastBox.transform, "Text", "", 32f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(toastText.rectTransform, 10f);
            toastBox.SetActive(false);

            // ipucu (ilk açılışta)
            attack = AttackPanel.Create(t);
            shop = ArmyShopUI.Create(t, this);
            armyBtn.onClick.AddListener(shop.Open);

            RefreshTop();
        }

        public void RefreshTop()
        {
            if (goldText != null) goldText.text = SaveSystem.Data.gold.ToString("N0");
            if (kingdomText != null && map != null) kingdomText.text = "Krallık: " + map.OwnedCount() + "/" + map.CityCount + " şehir";
        }

        public void ShowAttack(MapCity city, bool canAttack, Action onAttack) => attack.Show(city, canAttack, onAttack);

        public void Toast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastBox.SetActive(true);
            yield return new WaitForSeconds(3f);
            toastBox.SetActive(false);
        }
    }
}
