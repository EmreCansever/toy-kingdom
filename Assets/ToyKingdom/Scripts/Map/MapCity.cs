using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ToyKingdom
{
    [Serializable]
    public class RoadLink
    {
        public MapCity to;
        [Tooltip("Bu şehrin kapısından hedef şehrin kapısına yol üzerindeki noktalar")]
        public Vector3[] points;
    }

    /// <summary>Haritadaki bir şehir. Kulelerin rengi sahibine göre değişir.</summary>
    public class MapCity : MonoBehaviour
    {
        public CastleData castle;
        public bool isHomeCity;
        public string homeName = "Başkent";
        public string homeId = "home";
        [Tooltip("Kahramanın şehre vardığında durduğu nokta (kapı önü)")]
        public Vector3 doorPoint;
        public List<RoadLink> links = new List<RoadLink>();

        [Header("Görsel")]
        public List<SpriteRenderer> towers = new List<SpriteRenderer>();
        public List<int> towerVariants = new List<int>();
        public TextMeshPro label;
        public TextMeshPro subLabel;

        public string CityId => isHomeCity || castle == null ? homeId : castle.cityId;
        public string DisplayName => isHomeCity || castle == null ? homeName : castle.displayName;
        public bool IsConquered => isHomeCity || SaveSystem.Data.IsConquered(CityId);
        public int Income => castle != null && !isHomeCity ? castle.passiveIncomePerMinute : 3;

        void Awake()
        {
            // Yazı kenarlığı çalışma anında eklenir (düzenleme modunda malzeme kopyalanmasın)
            foreach (var t in new[] { label, subLabel })
            {
                if (t == null) continue;
                t.outlineWidth = 0.3f;
                t.outlineColor = new Color32(255, 248, 230, 255);
            }
        }

        public void RefreshVisual(bool attackable)
        {
            var art = GameArt.Instance;
            CastleColor color = IsConquered ? CastleColor.Blue : castle != null ? castle.color : CastleColor.Red;
            if (art != null)
                for (int i = 0; i < towers.Count; i++)
                    if (towers[i] != null)
                        towers[i].sprite = art.Tower(color, i < towerVariants.Count ? towerVariants[i] : 0);

            if (label != null)
            {
                label.text = DisplayName;
                label.color = IsConquered ? new Color(0.12f, 0.3f, 0.6f) : attackable ? new Color(0.6f, 0.12f, 0.1f) : new Color(0.3f, 0.28f, 0.26f);
            }
            if (subLabel != null)
            {
                if (IsConquered) subLabel.text = isHomeCity ? "Krallığın" : "Senin · +" + Income + "/dk";
                else if (attackable) subLabel.text = "Saldırılabilir · Zorluk " + castle.difficulty;
                else subLabel.text = "Uzak · Zorluk " + (castle != null ? castle.difficulty : 0);
                subLabel.color = label != null ? label.color : Color.black;
            }
        }
    }
}
