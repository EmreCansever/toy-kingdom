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
        TMP_Text capacity;

        public bool IsOpen => root != null && root.activeSelf;

        public static ArmyShopUI Create(Transform canvas, MapUI ui)
        {
            var dim = UIFactory.Panel(canvas, "ArmyShop", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var shop = dim.gameObject.AddComponent<ArmyShopUI>();
            shop.root = dim.gameObject;
            shop.ui = ui;

            var box = UIFactory.Window(dim.transform, "Box", new Vector2(1240f, 1020f), "Ordu", UIFactory.Blue, out _);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 1020f));
            var header = box.transform.Find("Border/Header");
            var sub = UIFactory.Text(header, "Sub", "Rota çizerek savaşa gönder. Yarısı yaşayan bölük geri döner.", 26f,
                new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
            UIFactory.Place(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(560f, 60f));
            sub.enableAutoSizing = true; sub.fontSizeMin = 16f; sub.fontSizeMax = 26f;

            shop.capacity = UIFactory.Text(header, "Capacity", "", 30f, Color.white, TextAlignmentOptions.Right, true);
            UIFactory.Place(shop.capacity.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(240f, 60f));

            var close = UIFactory.Button(header, "Close", "Kapat", UIFactory.Hex("F4EBDD"), 30f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(170f, 68f));
            close.onClick.AddListener(shop.Close);

            var list = UIFactory.Rect("Rows", box.transform);
            UIFactory.Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(1160f, 860f));
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
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
            RefreshAll();
        }

        public void Close() => root.SetActive(false);

        void RefreshAll()
        {
            var d = SaveSystem.Data;
            if (capacity != null) capacity.text = "Ordu  " + d.UsedCapacity() + " / " + d.ArmyCapacity();
            foreach (var r in rows) r.Refresh();
        }

        void Buy(UnitData data, int amount)
        {
            var save = SaveSystem.Data;
            int price = KingdomSim.UnitPrice(data) * amount;
            if (save.gold < price)
            {
                ui.Toast("Yeterli altının yok");
                return;
            }
            if (save.UsedCapacity() + amount > save.ArmyCapacity())
            {
                ui.Toast("Ordu kapasitesi dolu. Şehir fethet ya da Kışla kur");
                return;
            }
            save.gold -= price;
            save.AddArmy(data.unitId, amount);
            SaveSystem.Save();
            RefreshAll();
            ui.RefreshTop();
        }
    }
}
