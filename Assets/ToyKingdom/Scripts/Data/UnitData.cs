using UnityEngine;

namespace ToyKingdom
{
    public enum DamageType { Melee, Arrow, Fire }

    public enum UnitRole
    {
        /// <summary>Yakın dövüş: savunucularla çarpışır, kapıya vurur.</summary>
        Melee,
        /// <summary>Menzilli: uzaktan ok atar (asker, kule, kapı).</summary>
        Ranged,
        /// <summary>Merdivenci: kulelere tırmanıp onları yıkar.</summary>
        Climber
    }

    /// <summary>Bir birlik türü. Savaşa bölük (squad) halinde girer.</summary>
    [CreateAssetMenu(menuName = "ToyKingdom/Birlik", fileName = "Unit_")]
    public class UnitData : ScriptableObject
    {
        [Header("Kimlik")]
        public string unitId = "infantry";
        public string displayName = "Piyade";
        [TextArea] public string description = "Ucuz ve kalabalık.";
        public Sprite icon;
        public CharacterAnimSet anim;
        [Tooltip("Asker sprite'ına uygulanan renk (oyuncu tarafı)")]
        public Color tint = Color.white;
        [Tooltip("Asker boyutu çarpanı")]
        public float scale = 1f;
        public UnitRole role = UnitRole.Melee;

        [Header("Bölük")]
        [Tooltip("Bir bölükteki asker sayısı")]
        public int squadSize = 9;
        [Tooltip("Sahaya sürmek için gereken komuta puanı")]
        public int cost = 3;
        [Tooltip("Haritadaki dükkânda bir bölüğün altın fiyatı")]
        public int goldPrice = 60;

        [Header("Asker")]
        public float maxHealth = 40f;
        public float moveSpeed = 1.1f;
        public float damage = 6f;
        public float attackInterval = 1.1f;
        [Tooltip("Yakın dövüşte erişim; menzillilerde atış menzili")]
        public float attackRange = 0.42f;
        [Tooltip("Kapıya vurduğunda verdiği hasar")]
        public float gateDamage = 3f;
        [Tooltip("Kuleye verdiği hasar (merdivenci ve okçu)")]
        public float towerDamage = 3f;
        [Range(0f, 0.9f)] public float meleeArmor = 0f;
        [Range(0f, 0.9f)] public float arrowArmor = 0f;

        public bool IsRanged => role == UnitRole.Ranged;
        public float Armor(DamageType type) =>
            type == DamageType.Arrow ? arrowArmor : type == DamageType.Fire ? arrowArmor * 0.7f : meleeArmor;
    }
}
