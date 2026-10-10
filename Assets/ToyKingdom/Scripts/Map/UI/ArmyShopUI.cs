using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Haritadaki "Ordu" paneli: altınla bölük alınır (koddan kurulur).</summary>
    public class ArmyShopUI : MonoBehaviour
    {
        GameObject root;
        MapUI ui;
        readonly List<ShopRowUI> rows = new List<ShopRowUI>();

        public bool IsOpen => root != null && root.activeSelf;

        public static ArmyShopUI Create(Transform canvas, MapUI ui)
        {
            var dim = UIFactory.Panel(canvas, "ArmyShop", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var shop = dim.gameObject.AddComponent<ArmyShopUI>();
            shop.root = dim.gameObject;
            shop.ui = ui;

            var box = UIFactory.Frame(dim.transform, "Box", new Vector2(1240f, 900f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 900f));
            var title = UIFactory.Text(box.transform, "Title", "Ordu", 58f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 76f));
            var sub = UIFactory.Text(box.transform, "Sub", "Bölükler savaşta rota çizerek gönderilir. Yarısı hayatta kalan bölük geri döner.", 26f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1100f, 40f));

            var close = UIFactory.Button(box.transform, "Close", "Kapat", UIFactory.Brown, 34f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(180f, 76f));
            close.onClick.AddListener(shop.Close);

            var list = UIFactory.Rect("Rows", box.transform);
            UIFactory.Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1160f, 700f));
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

            var db = UnitDatabase.Instance;
            if (db != null)
                foreach (var u in db.units)
                    if (u != null) shop.rows.Add(ShopRowUI.Create(list, u, shop.Buy));

            shop.root.SetActive(false);
            return shop;
        }

        public void Open()
        {
            root.SetActive(true);
            foreach (var r in rows) r.Refresh();
        }

        public void Close() => root.SetActive(false);

        void Buy(UnitData data, int amount)
        {
            var save = SaveSystem.Data;
            int price = data.goldPrice * amount;
            if (save.gold < price)
            {
                ui.Toast("Yeterli altının yok");
                return;
            }
            save.gold -= price;
            save.AddArmy(data.unitId, amount);
            SaveSystem.Save();
            foreach (var r in rows) r.Refresh();
            ui.RefreshTop();
        }
    }
}
