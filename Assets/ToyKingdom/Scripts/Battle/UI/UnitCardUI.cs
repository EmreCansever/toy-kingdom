using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Savaş ekranının altındaki bölük kartı (koddan kurulur).</summary>
    public class UnitCardUI : MonoBehaviour
    {
        public UnitData Data { get; private set; }

        Button button;
        Image frame;
        Image icon;
        TMP_Text countText;
        CanvasGroup group;
        GameObject selected;

        public static UnitCardUI Create(Transform parent, UnitData data)
        {
            var outer = UIFactory.Panel(parent, "Card_" + data.unitId, UIFactory.BrownDark);
            outer.rectTransform.sizeDelta = new Vector2(160f, 178f);
            var card = outer.gameObject.AddComponent<UnitCardUI>();
            card.Data = data;
            card.frame = outer;
            card.group = outer.gameObject.AddComponent<CanvasGroup>();

            var sel = UIFactory.Panel(outer.transform, "Selected", UIFactory.Gold);
            UIFactory.Stretch(sel.rectTransform, -6f);
            sel.transform.SetAsFirstSibling();
            card.selected = sel.gameObject;

            var inner = UIFactory.Panel(outer.transform, "Inner", UIFactory.Parchment);
            UIFactory.Stretch(inner.rectTransform, 5f);

            card.icon = UIFactory.Panel(inner.transform, "Icon", data.tint, data.icon);
            card.icon.preserveAspect = true;
            UIFactory.Place(card.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(140f, 112f));

            var name = UIFactory.Text(inner.transform, "Name", data.displayName, 23f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(150f, 30f));
            var size = UIFactory.Text(inner.transform, "Size", data.squadSize + " asker", 18f, UIFactory.Brown, TextAlignmentOptions.Center);
            UIFactory.Place(size.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(150f, 22f));

            var badge = UIFactory.Panel(inner.transform, "Cost", UIFactory.Hex("534AB7"));
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(44f, 44f));
            var cost = UIFactory.Text(badge.transform, "Value", data.cost.ToString(), 26f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(cost.rectTransform);

            card.countText = UIFactory.Text(inner.transform, "Count", "x0", 26f, UIFactory.BrownDark, TextAlignmentOptions.Right, true);
            UIFactory.Place(card.countText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -4f), new Vector2(90f, 40f));

            card.button = outer.gameObject.AddComponent<Button>();
            card.button.targetGraphic = inner;
            card.button.onClick.AddListener(() =>
            {
                if (BattleManager.Instance != null) BattleManager.Instance.SelectUnit(card.Data);
            });
            card.SetSelected(false);
            return card;
        }

        public void SetSelected(bool on)
        {
            if (selected != null) selected.SetActive(on);
        }

        void Update()
        {
            var bm = BattleManager.Instance;
            if (Data == null || bm == null || bm.Command == null) return;
            int count = SaveSystem.Data.GetArmyCount(Data.unitId);
            countText.text = "x" + count;
            bool usable = count > 0 && bm.State == BattleState.Playing;
            button.interactable = usable;
            group.alpha = usable ? (bm.Command.CanAfford(Data.cost) ? 1f : 0.6f) : 0.35f;
        }
    }
}
