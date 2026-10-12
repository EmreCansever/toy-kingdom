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
        TMP_Text rankText;
        GameObject rankRoot;
        CanvasGroup group;
        GameObject selected;

        public static UnitCardUI Create(Transform parent, UnitData data)
        {
            var outer = UIFactory.Rounded(parent, "Card_" + data.unitId, UIFactory.Darken(UIFactory.Border, 0.8f), 22f);
            outer.rectTransform.sizeDelta = new Vector2(160f, 182f);
            var card = outer.gameObject.AddComponent<UnitCardUI>();
            card.Data = data;
            card.frame = outer;
            card.group = outer.gameObject.AddComponent<CanvasGroup>();

            var sel = UIFactory.Rounded(outer.transform, "Selected", UIFactory.Gold, 26f);
            sel.sprite = UIFactory.RingSprite;
            UIFactory.Stretch(sel.rectTransform, -7f);
            sel.raycastTarget = false;
            card.selected = sel.gameObject;

            var inner = UIFactory.Rounded(outer.transform, "Inner", UIFactory.Parchment, 19f, true);
            UIFactory.Stretch(inner.rectTransform, 3f, 6f, 3f, 3f);

            card.icon = UIFactory.Panel(inner.transform, "Icon", data.tint, data.icon);
            card.icon.preserveAspect = true;
            card.icon.raycastTarget = false;
            UIFactory.Place(card.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(140f, 112f));

            var name = UIFactory.Text(inner.transform, "Name", data.displayName, 22f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(148f, 30f));
            name.enableAutoSizing = true; name.fontSizeMin = 15f; name.fontSizeMax = 22f;
            var size = UIFactory.Text(inner.transform, "Size", data.squadSize + " asker", 17f, UIFactory.Muted, TextAlignmentOptions.Center);
            UIFactory.Place(size.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(148f, 22f));

            var badge = UIFactory.Rounded(inner.transform, "Cost", UIFactory.Purple, 20f, true);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(40f, 40f));
            badge.raycastTarget = false;
            var cost = UIFactory.Text(badge.transform, "Value", data.cost.ToString(), 24f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(cost.rectTransform);

            var countBg = UIFactory.Rounded(inner.transform, "CountBg", UIFactory.Slate, 16f);
            UIFactory.Place(countBg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -8f), new Vector2(54f, 34f));
            countBg.raycastTarget = false;
            card.countText = UIFactory.Text(countBg.transform, "Count", "x0", 21f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(card.countText.rectTransform);
            // en tecrübeli bölüğün rütbe nişanı (sıradaki gönderilecek olan)
            var rank = UIFactory.Rounded(inner.transform, "Rank", UIFactory.Hex("C98A1B"), 14f);
            UIFactory.Place(rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -48f), new Vector2(46f, 30f));
            rank.raycastTarget = false;
            card.rankText = UIFactory.Text(rank.transform, "R", "", 19f, Color.white, TextAlignmentOptions.Center, true);
            UIFactory.Stretch(card.rankText.rectTransform);
            card.rankRoot = rank.gameObject;
            card.rankRoot.SetActive(false);

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
            if (selected == null) return;
            if (on && !selected.activeSelf) FX.Punch(transform, 0.08f, 0.22f);
            selected.SetActive(on);
        }

        void Update()
        {
            var bm = BattleManager.Instance;
            if (Data == null || bm == null || bm.Command == null) return;
            int count = SaveSystem.Data.GetArmyCount(Data.unitId);
            countText.text = "x" + count;
            string badge = count > 0 ? Veterancy.Badge(SaveSystem.Data.BestXp(Data.unitId)) : "";
            if (rankRoot.activeSelf != (badge.Length > 0)) rankRoot.SetActive(badge.Length > 0);
            rankText.text = badge;
            bool usable = count > 0 && bm.State == BattleState.Playing;
            button.interactable = usable;
            group.alpha = usable ? (bm.Command.CanAfford(Data.cost) ? 1f : 0.6f) : 0.35f;
        }
    }
}
