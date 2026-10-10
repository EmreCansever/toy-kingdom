using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ToyKingdom
{
    [Serializable]
    public class ArmyEntry
    {
        public string unitId;
        public int count;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 3;
        public int gold = 600;
        public List<string> conqueredCities = new List<string>();
        /// <summary>Birlik türüne göre bölük sayısı.</summary>
        public List<ArmyEntry> army = new List<ArmyEntry>();
        public string heroCityId = "";
        public long lastIncomeTicks;

        public bool IsConquered(string cityId) => conqueredCities.Contains(cityId);

        public void Conquer(string cityId)
        {
            if (!conqueredCities.Contains(cityId)) conqueredCities.Add(cityId);
        }

        public int GetArmyCount(string unitId)
        {
            var entry = army.Find(a => a.unitId == unitId);
            return entry != null ? entry.count : 0;
        }

        public int TotalArmy()
        {
            int total = 0;
            foreach (var e in army) total += e.count;
            return total;
        }

        public void AddArmy(string unitId, int amount)
        {
            var entry = army.Find(a => a.unitId == unitId);
            if (entry == null)
            {
                entry = new ArmyEntry { unitId = unitId, count = 0 };
                army.Add(entry);
            }
            entry.count = Mathf.Max(0, entry.count + amount);
        }
    }

    /// <summary>Oyun durumunu cihaza JSON olarak kaydeder.</summary>
    public static class SaveSystem
    {
        const int CurrentVersion = 3;
        static SaveData _data;

        static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static void Load()
        {
            _data = null;
            if (File.Exists(SavePath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                    if (loaded != null && loaded.version == CurrentVersion) _data = loaded;
                }
                catch (Exception e) { Debug.LogWarning("Kayıt okunamadı, yeni oyun başlıyor: " + e.Message); }
            }
            if (_data == null) _data = CreateNew();
        }

        public static void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning("Kayıt yazılamadı: " + e.Message); }
        }

        public static void ResetSave()
        {
            _data = CreateNew();
            Save();
        }

        static SaveData CreateNew()
        {
            var data = new SaveData { version = CurrentVersion, lastIncomeTicks = DateTime.UtcNow.Ticks };
            data.AddArmy("infantry", 3);
            data.AddArmy("shield", 1);
            data.AddArmy("archer", 1);
            data.AddArmy("climber", 1);
            return data;
        }
    }
}
