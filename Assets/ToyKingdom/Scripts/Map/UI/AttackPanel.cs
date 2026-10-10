using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Şehre varınca açılan saldırı paneli (koddan kurulur).</summary>
    public class AttackPanel : MonoBehaviour
    {
        GameObject root;
        TMP_Text title;
        TMP_Text description;
        TMP_Text defense;
        TMP_Text reward;
        TMP_Text status;
        Button attackButton;
        Action onAttack;

        public bool IsOpen => root != null && root.activeSelf;

        public static AttackPanel Create(Transform canvas)
        {
            var dim = UIFactory.Panel(canvas, "AttackPanel", UIFactory.Dim);
            UIFactory.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<AttackPanel>();
            p.root = dim.gameObject;

            var box = UIFactory.Frame(dim.transform, "Box", new Vector2(860f, 700f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 700f));
            var b = box.transform;

            p.title = UIFactory.Text(b, "Title", "", 58f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
            UIFactory.Place(p.title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(780f, 80f));
            p.description = UIFactory.Text(b, "Description", "", 30f, UIFactory.Ink, TextAlignmentOptions.Center);
            UIFactory.Place(p.description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(760f, 90f));
            p.defense = UIFactory.Text(b, "Defense", "", 30f, UIFactory.Red, TextAlignmentOptions.Center, true);
            UIFactory.Place(p.defense.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(780f, 130f));
            p.reward = UIFactory.Text(b, "Reward", "", 32f, UIFactory.Brown, TextAlignmentOptions.Center, true);
            UIFactory.Place(p.reward.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(780f, 100f));
            p.status = UIFactory.Text(b, "Status", "", 28f, UIFactory.Red, TextAlignmentOptions.Center);
            UIFactory.Place(p.status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(780f, 44f));

            p.attackButton = UIFactory.Button(b, "Attack", "Saldır", UIFactory.Red, 44f);
            UIFactory.Place((RectTransform)p.attackButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 36f), new Vector2(360f, 100f));
            p.attackButton.onClick.AddListener(() =>
            {
                var cb = p.onAttack;
                p.Hide();
                cb?.Invoke();
            });
            var close = UIFactory.Button(b, "Close", "Vazgeç", UIFactory.Brown, 36f);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(240f, 100f));
            close.onClick.AddListener(p.Hide);

            p.Hide();
            return p;
        }

        public void Show(MapCity city, bool canAttack, Action attack)
        {
            onAttack = attack;
            var c = city.castle;
            title.text = city.DisplayName;
            description.text = c != null ? c.description : "";
            if (c != null)
            {
                int towers = 4 + (c.midTowers ? 4 : 0);
                int gates = c.secondGate ? 2 : 1;
                int squads = 0;
                foreach (var g in c.garrison) if (g != null) squads += g.squads;
                var sb = new StringBuilder();
                sb.Append("Zorluk ").Append(c.difficulty).Append("/5\n");
                sb.Append(towers).Append(" kule · ").Append(gates).Append(gates > 1 ? " kapı · " : " kapı · ").Append(squads).Append(" savunma bölüğü");
                if (c.wallArchers > 0) sb.Append("\nSurlarda ").Append(c.wallArchers).Append(" okçu");
                if (c.reinforcementCount > 0) sb.Append(c.wallArchers > 0 ? " · " : "\n").Append(c.reinforcementCount).Append(" takviye bölüğü gelecek");
                defense.text = sb.ToString();
                reward.text = "Ganimet: " + c.goldReward + " altın\nGelir: dakikada " + c.passiveIncomePerMinute + " altın";
            }
            attackButton.interactable = canAttack;
            status.text = canAttack ? "" : "Önce bu şehre yolla bağlı bir şehri fethet";
            root.SetActive(true);
        }

        public void Hide()
        {
            onAttack = null;
            if (root != null) root.SetActive(false);
        }
    }
}
