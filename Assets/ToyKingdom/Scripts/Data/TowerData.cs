using UnityEngine;

namespace ToyKingdom
{
    [CreateAssetMenu(menuName = "ToyKingdom/Kule", fileName = "Tower_")]
    public class TowerData : ScriptableObject
    {
        public string towerId = "archer";
        public string displayName = "Okçu kulesi";
        [Tooltip("Kulenin canı (merdivenciler ve okçular yıkabilir)")]
        public float maxHealth = 160f;

        [Header("Atış")]
        public float range = 4f;
        [Tooltip("İki atış arasındaki saniye")]
        public float fireInterval = 1.2f;
        public float damage = 7f;
        [Tooltip("Aynı anda atılan ok sayısı")]
        public int arrowsPerShot = 1;
        public float projectileSpeed = 7f;
        [Tooltip("Okun kavis yüksekliği")]
        public float arcHeight = 0.8f;
    }
}
