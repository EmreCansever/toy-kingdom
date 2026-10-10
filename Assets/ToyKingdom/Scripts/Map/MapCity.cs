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
        /// <summary>Bizim şehrimizdi ama isyanda.</summary>
        public bool IsRebel => !isHomeCity && SaveSystem.Data.IsRebel(CityId);
        public int BaseIncome => castle != null && !isHomeCity ? castle.passiveIncomePerMinute : KingdomSim.HomeBaseIncome;
        public int Income => KingdomSim.Income(SaveSystem.Data.State(CityId), BaseIncome, isHomeCity);
        public CityState State => SaveSystem.Data.State(CityId);

        public CityInfo Info => new CityInfo
        {
            id = CityId, name = DisplayName, baseIncome = BaseIncome, home = isHomeCity,
            difficulty = castle != null ? castle.difficulty : 0
        };

        Vector3 Top => transform.position + Vector3.up * 1.2f;

        /// <summary>Kuleler sırayla maviye döner, parıltılar saçılır.</summary>
        public void PlayConquest()
        {
            var art = GameArt.Instance;
            if (art == null) return;
            FlipTo(CastleColor.Red);
            FX.FlipTowers(towers, i => art.Tower(CastleColor.Blue, i < towerVariants.Count ? towerVariants[i] : 0),
                new Color(1f, 0.85f, 0.3f), 0.15f, () =>
                {
                    FX.Sparkles(Top, new Color(0.5f, 0.75f, 1f), 26, 1.3f);
                    FX.Punch(transform, 0.12f, 0.4f);
                });
        }

        /// <summary>İsyan: kuleler kırmızıya döner ve şehir sarsılır.</summary>
        public void PlayRebellion()
        {
            var art = GameArt.Instance;
            if (art == null) return;
            FX.Shake(transform, 0.1f, 0.8f);
            FX.FlipTowers(towers, i => art.Tower(CastleColor.Red, i < towerVariants.Count ? towerVariants[i] : 0),
                new Color(1f, 0.35f, 0.2f), 0.12f, () => RefreshVisual(true));
        }

        void FlipTo(CastleColor color)
        {
            var art = GameArt.Instance;
            for (int i = 0; i < towers.Count; i++)
                if (towers[i] != null) towers[i].sprite = art.Tower(color, i < towerVariants.Count ? towerVariants[i] : 0);
        }

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
            bool rebel = IsRebel;
            CastleColor color = IsConquered ? CastleColor.Blue : rebel ? CastleColor.Red : castle != null ? castle.color : CastleColor.Red;
            if (art != null)
                for (int i = 0; i < towers.Count; i++)
                    if (towers[i] != null)
                        towers[i].sprite = art.Tower(color, i < towerVariants.Count ? towerVariants[i] : 0);

            if (label != null)
            {
                label.text = DisplayName;
                label.color = IsConquered ? new Color(0.12f, 0.3f, 0.6f) : rebel || attackable ? new Color(0.6f, 0.12f, 0.1f) : new Color(0.3f, 0.28f, 0.26f);
            }
            if (subLabel != null)
            {
                if (IsConquered)
                {
                    var st = State;
                    string warn = isHomeCity ? "" : st.unrest >= 70f ? " · Huzursuz!" : !st.hasGovernor ? " · Valisiz" :
                        st.governor.loyalty < 25f ? " · Vali şüpheli" : "";
                    subLabel.text = (isHomeCity ? "Başkent" : "Senin") + " · +" + Income + "/dk" + warn;
                }
                else if (rebel) subLabel.text = State.rebel == RebelState.Governor ? "İSYAN! Asi vali" : "İSYAN! Halk ayaklandı";
                else if (attackable) subLabel.text = "Saldırılabilir · Zorluk " + castle.difficulty;
                else subLabel.text = "Uzak · Zorluk " + (castle != null ? castle.difficulty : 0);
                subLabel.color = label != null ? label.color : Color.black;
            }
        }
    }
}
