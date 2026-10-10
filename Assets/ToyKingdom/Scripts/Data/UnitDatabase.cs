using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Oyundaki tüm birlik türlerinin listesi.
    /// Assets/Resources/UnitDatabase.asset adıyla kaydedilmeli.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyKingdom/Birlik veritabanı", fileName = "UnitDatabase")]
    public class UnitDatabase : ScriptableObject
    {
        public List<UnitData> units = new List<UnitData>();

        public UnitData Get(string unitId) =>
            units.Find(u => u != null && u.unitId == unitId);

        static UnitDatabase _instance;

        public static UnitDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UnitDatabase>("UnitDatabase");
                    if (_instance == null)
                        Debug.LogError("Assets/Resources/UnitDatabase.asset bulunamadı. Menüden ToyKingdom > Örnek veri oluştur ile yaratabilirsin.");
                }
                return _instance;
            }
        }
    }
}
