using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    [Serializable]
    public class GarrisonEntry
    {
        public UnitData unit;
        public int squads = 1;
    }

    /// <summary>
    /// Bir şehrin kalesi. Savaş alanı bu verilerden çalışma anında kurulur:
    /// yeni kale eklemek için yeni bir CastleData oluşturman yeterli.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyKingdom/Kale", fileName = "Castle_")]
    public class CastleData : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Kayıtta kullanılan benzersiz ad. Sonradan değiştirme!")]
        public string cityId = "city_01";
        public string displayName = "Çam Köyü";
        [TextArea] public string description = "Ahşap kuleli küçük bir kale.";
        [Range(1, 5)] public int difficulty = 1;
        public CastleColor color = CastleColor.Wood;
        [Tooltip("Kalenin bulunduğu bölge (savaş alanının zemini)")]
        public Biome biome = Biome.Spring;

        [Header("Kale yapısı (hücre)")]
        [Range(4, 9)] public int width = 5;
        [Range(4, 9)] public int depth = 5;
        [Tooltip("Ön duvarın ortasına ek kule")]
        public bool midTowers = false;
        [Tooltip("Sol ön duvara ikinci kapı")]
        public bool secondGate = false;
        [Tooltip("Kale avlusunda yüksek iç kule")]
        public bool keep = true;
        public float gateHealth = 300f;
        public TowerData towerData;

        [Header("Savunma")]
        [Tooltip("Savunucu askerlerin can ve hasar çarpanı (1 = normal)")]
        [Range(0.3f, 2f)] public float defenderStrength = 1f;
        [Tooltip("Surların üstünde duran düşman okçusu sayısı")]
        [Range(0, 12)] public int wallArchers = 0;
        [Tooltip("Sur okçularının birlik türü")]
        public UnitData wallArcherUnit;
        [Tooltip("Savaş başında kalenin önüne dağılmış bekleyen bölükler")]
        public List<GarrisonEntry> garrison = new List<GarrisonEntry>();
        [Tooltip("Belirli aralıklarla kapıdan çıkan takviye bölüğü")]
        public UnitData reinforcement;
        public float reinforcementInterval = 30f;
        public int reinforcementCount = 0;

        [Header("Savaş")]
        public float timeLimit = 150f;
        public int startingCommandPoints = 6;
        public int maxCommandPoints = 10;
        public float commandRegenPerSecond = 0.6f;

        [Header("Ödül")]
        public int goldReward = 300;
        [Tooltip("Fethedildikten sonra dakikada ürettiği altın")]
        public int passiveIncomePerMinute = 5;

        /// <summary>Kuşatma kademesi (çalışma anında): 0 tarafsız/asi (normal), 1 rakip krallık şehri (zor), 2 rakip başkenti (en zor).</summary>
        [System.NonSerialized] public int siegeTier;

        // ------------------------------------------------------------------ kuşatma derinliği (zorluktan türetilir)

        /// <summary>Kapıya vurulunca yakındaki nöbetçiler kapıyı savunmaya koşar.</summary>
        public bool RallyDefense => difficulty >= 2;
        /// <summary>Kapı önünde kümelenenlerin üstüne kızgın yağ dökülür.</summary>
        public bool BoilingOil => difficulty + siegeTier >= 4;
        /// <summary>Kapı kırılınca avlu savaşı başlar: kapı önü tutulmalı.</summary>
        public bool Courtyard => difficulty >= 2;
        /// <summary>Takviyeler oyuncunun okçularına akın eder.</summary>
        public bool Sortie => difficulty >= 4 && reinforcement != null;
        /// <summary>Avlu savaşında son direniş için çıkan bölük sayısı.</summary>
        public int FinalStandSquads => Mathf.Clamp(difficulty - 2 + siegeTier, 0, 3);
        /// <summary>Kapı önünün kaç saniye tutulması gerektiği.</summary>
        public float CaptureTime => 6f + 1.5f * difficulty;
    }
}
