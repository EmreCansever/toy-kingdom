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
        int price;

        public static ShopRowUI Create(Transform parent, UnitData unit, Action<UnitData, int> onBuy)
        {
            var outer = UIFactory.Rounded(parent, "Row_" + unit.unitId, UIFactory.ParchmentDark, 20f);
            outer.rectTransform.sizeDelta = new Vector2(1160f, 148f);
            var row = outer.gameObject.AddComponent<ShopRowUI>();
            row.data = unit;
            var t = outer.transform;

            var iconBg = UIFactory.Rounded(t, "IconBg", UIFactory.Hex("E4D3AF"), 18f);
            UIFactory.Place(iconBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(124f, 124f));
            var icon = UIFactory.Panel(iconBg.transform, "Icon", unit.tint, unit.icon);
            icon.preserveAspect = true;
            UIFactory.Stretch(icon.rectTransform, -14f, -6f, -14f, -22f);

            // sol sütun: isim, açıklama, değerler (sağdaki 300 piksellik alana taşmaz; sığmayan yazı küçülür)
            const float textX = 150f, textW = 640f;
            var name = Fit(UIFactory.Text(t, "Name", unit.displayName + " bölüğü", 34f, UIFactory.BrownDark, TextAlignmentOptions.Left, true), 24f);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -6f), new Vector2(textW, 42f));
            var desc = Fit(UIFactory.Text(t, "Desc", unit.description, 23f, UIFactory.Ink, TextAlignmentOptions.TopLeft), 19f);
            UIFactory.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -46f), new Vector2(textW, 62f));
            var stats = Fit(UIFactory.Text(t, "Stats",
                unit.squadSize + " asker · Can " + unit.maxHealth + " · Hasar " + unit.damage + " · Kapıya " + unit.gateDamage + " · Komuta " + unit.cost,
                21f, UIFactory.Brown, TextAlignmentOptions.TopLeft), 17f);
            UIFactory.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -110f), new Vector2(textW, 32f));

            // sağ sütun: elde/fiyat ve satın alma düğmeleri
            row.owned = Fit(UIFactory.Text(t, "Owned", "", 24f, UIFactory.BrownDark, TextAlignmentOptions.Center, true), 16f);
            UIFactory.Place(row.owned.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(282f, 40f));
            row.price = unit.goldPrice;

            row.buy1 = UIFactory.Button(t, "Buy1", "Al", UIFactory.Green, 32f);
            UIFactory.Place((RectTransform)row.buy1.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160f, 12f), new Vector2(136f, 80f));
            row.buy1.onClick.AddListener(() => onBuy(row.data, 1));
            row.buy3 = UIFactory.Button(t, "Buy3", "3 al", UIFactory.Green, 32f);
            UIFactory.Place((RectTransform)row.buy3.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 12f), new Vector2(136f, 80f));
            row.buy3.onClick.AddListener(() => onBuy(row.data, 3));

            row.Refresh();
            return row;
        }

        static TMP_Text Fit(TMP_Text t, float min)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            return t;
        }

        public void Refresh()
        {
            if (data == null) return;
            int gold = SaveSystem.Data.gold;
            owned.text = "Elde: " + SaveSystem.Data.GetArmyCount(data.unitId) + "  ·  " + price + " altın";
            var d = SaveSystem.Data;
            int room = d.ArmyCapacity() - d.UsedCapacity();
            buy1.interactable = gold >= data.goldPrice && room >= 1;
            buy3.interactable = gold >= data.goldPrice * 3 && room >= 3;
        }
    }
}
