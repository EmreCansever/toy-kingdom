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

    public enum CityStatus { Home, Owned, Warning, Attackable, Rebel, Far, Rival, Threat }

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
        /// <summary>Krallığın en yakın şehrinden yol üzerinden kaç şehir ötede (harita doldurur; -1 yol yok).</summary>
        [System.NonSerialized] public int roadHops = -1;

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
        /// <summary>Şehri elinde tutan rakip krallık (yoksa null).</summary>
        public string RivalId => isHomeCity ? null : SaveSystem.Data.RivalOwner(CityId);
        /// <summary>Bu şehre yürüyen rakip (yoksa null).</summary>
        public RivalState IncomingAttack
        {
            get
            {
                foreach (var r in SaveSystem.Data.rivals) if (r.Attacking && r.attackTarget == CityId) return r;
                return null;
            }
        }
        public string StatusText { get; private set; } = "";
        public CityStatus Status { get; private set; } = CityStatus.Far;
        public int Difficulty { get; private set; }
        /// <summary>İsim plakasının dünyadaki yeri (kalenin tepesi).</summary>
        public Vector3 LabelAnchor => transform.position + Vector3.up * (label != null ? label.transform.localPosition.y - 0.25f : 1.5f);

        public CityInfo Info => new CityInfo
        {
            id = CityId, name = DisplayName, baseIncome = BaseIncome, home = isHomeCity,
            difficulty = castle != null ? castle.difficulty : 0,
            biome = castle == null ? 'S' : castle.biome == Biome.Desert ? 'D' : castle.biome == Biome.Winter ? 'W' : 'S',
            neighbors = links.ConvertAll(l => l.to != null ? l.to.CityId : "").ToArray()
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

        /// <summary>Rakip şehri ele geçirdi: kuleler rakip rengine döner.</summary>
        public void PlayCapturedByRival(string rivalId)
        {
            var art = GameArt.Instance;
            if (art == null) return;
            var def = RivalAI.Get(rivalId);
            FX.Shake(transform, 0.09f, 0.7f);
            FX.FlipTowers(towers, i => art.Tower(def.towers, i < towerVariants.Count ? towerVariants[i] : 0),
                def.color, 0.12f, () => RefreshVisual(false));
        }

        /// <summary>Saldırı püskürtüldü.</summary>
        public void PlayDefended()
        {
            FX.Punch(transform, 0.1f, 0.4f);
            FX.Sparkles(Top, new Color(0.5f, 0.75f, 1f), 24, 1.2f);
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
            string rival = RivalId;
            CastleColor color = IsConquered ? CastleColor.Blue : rebel ? CastleColor.Red
                : rival != null ? RivalAI.Get(rival).towers : CastleColor.Wood;
            if (art != null)
                for (int i = 0; i < towers.Count; i++)
                    if (towers[i] != null)
                        towers[i].sprite = art.Tower(color, i < towerVariants.Count ? towerVariants[i] : 0);

            // durum bilgisi (isim plakası arayüzü bunu okur)
            if (IsConquered)
            {
                var st = State;
                string warn = isHomeCity ? "" : st.unrest >= 70f ? "  ·  Huzursuz!" : !st.hasGovernor ? "  ·  Valisiz" :
                    st.governor.loyalty < 25f ? "  ·  Vali şüpheli" : "";
                StatusText = "+" + Income + " altın/dk" + warn;
                Status = warn.Length > 0 ? CityStatus.Warning : isHomeCity ? CityStatus.Home : CityStatus.Owned;
                if (!isHomeCity && st.revoltTimer >= 0f)
                {
                    StatusText = "İSYAN EŞİĞİNDE  ·  " + Mathf.CeilToInt(st.revoltTimer) + " dk";
                    Status = CityStatus.Threat;
                }
                var atk = IncomingAttack;
                if (atk != null)
                {
                    long left = Math.Max(0L, atk.attackArriveTicks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
                    StatusText = "Saldırı geliyor  " + (left / 60) + ":" + (left % 60).ToString("00");
                    Status = CityStatus.Threat;
                }
            }
            else if (rival != null)
            {
                var r = SaveSystem.Data.Rival(rival);
                StatusText = RivalAI.Get(rival).name + (r != null && r.capitalId == CityId ? " başkenti" : "") + (attackable ? "  ·  Saldırılabilir" : "");
                Status = CityStatus.Rival;
            }
            else if (rebel)
            {
                StatusText = State.rebel == RebelState.Governor ? "İsyan: asi vali" : "İsyan: halk ayaklandı";
                Status = CityStatus.Rebel;
            }
            else if (attackable)
            {
                StatusText = "Saldırılabilir";
                Status = CityStatus.Attackable;
            }
            else
            {
                StatusText = roadHops > 1 ? "Uzak  ·  önce aradaki " + (roadHops - 1) + " şehri al" : "Uzak  ·  yol bağlı değil";
                Status = CityStatus.Far;
            }
            Difficulty = castle != null ? castle.difficulty : 0;

            // eski dünya yazıları kullanılmıyor
            if (label != null) label.gameObject.SetActive(false);
            if (subLabel != null) subLabel.gameObject.SetActive(false);
        }
    }
}
