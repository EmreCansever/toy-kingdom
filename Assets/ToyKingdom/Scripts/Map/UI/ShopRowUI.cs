using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Ordu dükkânında tek bir bölük satırı (koddan kurulur).</summary>
    public class ShopRowUI : MonoBehaviour
    {
        UnitData data;
        TMP_Text owned;
        Button buy1;
        Button buy3;

        public static ShopRowUI Create(Transform parent, UnitData unit, Action<UnitData, int> onBuy)
        {
            var outer = UIFactory.Panel(parent, "Row_" + unit.unitId, UIFactory.ParchmentDark);
            outer.rectTransform.sizeDelta = new Vector2(1160f, 160f);
            var row = outer.gameObject.AddComponent<ShopRowUI>();
            row.data = unit;
            var t = outer.transform;

            var icon = UIFactory.Panel(t, "Icon", unit.tint, unit.icon);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 4f), new Vector2(180f, 150f));

            var name = UIFactory.Text(t, "Name", unit.displayName + " bölüğü", 38f, UIFactory.BrownDark, TextAlignmentOptions.Left, true);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -10f), new Vector2(560f, 50f));
            var desc = UIFactory.Text(t, "Desc", unit.description, 24f, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            UIFactory.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -62f), new Vector2(560f, 40f));
            var stats = UIFactory.Text(t, "Stats",
                unit.squadSize + " asker · Can " + unit.maxHealth + " · Hasar " + unit.damage + " · Kapıya " + unit.gateDamage + " · Komuta " + unit.cost,
                22f, UIFactory.Brown, TextAlignmentOptions.TopLeft);
            UIFactory.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -108f), new Vector2(600f, 40f));

            row.owned = UIFactory.Text(t, "Owned", "", 30f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(row.owned.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-330f, 22f), new Vector2(200f, 44f));
            var price = UIFactory.Text(t, "Price", unit.goldPrice + " altın", 28f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-330f, -24f), new Vector2(200f, 40f));

            row.buy1 = UIFactory.Button(t, "Buy1", "Al", UIFactory.Green, 34f);
            UIFactory.Place((RectTransform)row.buy1.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(140f, 90f));
            row.buy1.onClick.AddListener(() => onBuy(row.data, 1));
            row.buy3 = UIFactory.Button(t, "Buy3", "3 al", UIFactory.Green, 34f);
            UIFactory.Place((RectTransform)row.buy3.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(140f, 90f));
            row.buy3.onClick.AddListener(() => onBuy(row.data, 3));

            row.Refresh();
            return row;
        }

        public void Refresh()
        {
            if (data == null) return;
            int gold = SaveSystem.Data.gold;
            owned.text = "Elde: " + SaveSystem.Data.GetArmyCount(data.unitId);
            buy1.interactable = gold >= data.goldPrice;
            buy3.interactable = gold >= data.goldPrice * 3;
        }
    }
}
