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
        /// <summary>Her bölüğün tecrübe puanı (count kadar). Eski kayıtlarda boş gelir, sıfırla doldurulur.</summary>
        public List<int> xp = new List<int>();

        public void Normalize()
        {
            if (xp == null) xp = new List<int>();
            while (xp.Count < count) xp.Add(0);
            while (xp.Count > count) xp.Remove(Min());
        }

        int Min() { int m = int.MaxValue; foreach (var x in xp) m = Mathf.Min(m, x); return m; }
        public int Best() { Normalize(); int m = 0; foreach (var x in xp) m = Mathf.Max(m, x); return m; }
    }

    [Serializable]
    public class SaveData
    {
        public int version = 5;
        public int gold = 600;
        // ------------------------------------------------------------------ oyun sonu
        /// <summary>0 sürüyor, 1 zafer, 2 yenilgi, 3 zaferden sonra devam.</summary>
        public int gameOver;
        public string endReason = "";
        /// <summary>Darbe krizinde geçen (çevrimiçi) dakika.</summary>
        public float crisisMinutes;
        public bool capitalLost;

        /// <summary>Vezirin görevlerinde kaçıncı adımdayız.</summary>
        public int tutorialStep;
        public int tutorialVersion;
        /// <summary>Oyuncunun yaptığı ama durumdan anlaşılmayan eylemler (görevler için).</summary>
        public List<string> flags = new List<string>();
        public bool HasFlag(string f) => flags.Contains(f);
        public void SetFlag(string f) { if (!flags.Contains(f)) flags.Add(f); }
        /// <summary>Hükümdarın itibarı (0-100).</summary>
        public float reputation = 50f;
        /// <summary>Vali azletme lekesi: her azil artırır, zamanla söner. Diğer valilerin ve yeni adayların sadakatini düşürür.</summary>
        public float dismissStain;
        /// <summary>İki rakip kral arasındaki ilişki (-100..100) ve anlaşma (RivalWars.None/War/Alliance).</summary>
        public float kingsRelation = -5f;
        public int kingsPact;
        public float kingsWarTimer = 5f;
        public float kingsEventTimer = 8f;
        public List<string> conqueredCities = new List<string>();
        /// <summary>Birlik türüne göre bölük sayısı.</summary>
        public List<ArmyEntry> army = new List<ArmyEntry>();
        public string heroCityId = "";
        public long lastIncomeTicks;
        /// <summary>Fethedilen şehirlerin yönetim durumu (vali, yapılar, vergi, isyan).</summary>
        public List<CityState> cities = new List<CityState>();
        /// <summary>Son fethedilen şehir: haritada fetih animasyonu oynatılır.</summary>
        public string pendingConquestAnim = "";
        /// <summary>Rakip krallıklar (ilk açılışta haritaya göre kurulur).</summary>
        public List<RivalState> rivals = new List<RivalState>();
        /// <summary>Oyunun başından beri oynanan (çevrimiçi) dakika: rakiplerin ilk saldırısı için.</summary>
        public float playMinutes;
        /// <summary>Haritaya dönüşte gösterilecek önemli bildirim.</summary>
        public string pendingMessage = "";

        // ------------------------------------------------------------------ saray
        public List<Letter> letters = new List<Letter>();
        public int nextLetterId = 1;
        /// <summary>Bir sonraki saray olayına kalan (çevrimiçi) dakika.</summary>
        public float eventTimer = 4f;
        /// <summary>Bakım giderinin küsurat birikimi.</summary>
        public float upkeepRemainder;
        public bool inDebt;
        /// <summary>Kaybedilen savaşta esir düşen bölüklerin birlik türleri (fidye mektubu için).</summary>
        public List<string> pendingCaptives = new List<string>();
        public string pendingCaptor = "";
        public string pendingCaptiveCity = "";
        /// <summary>Kervan yatırımları: dönüş zamanı ve tutarı.</summary>
        public List<long> caravanTicks = new List<long>();
        public List<int> caravanAmounts = new List<int>();

        // ------------------------------------------------------------------ ticaret
        /// <summary>Kendi şehirlerimiz arasındaki ticaret yolları.</summary>
        public List<RoadState> roads = new List<RoadState>();
        /// <summary>Şu an yolda olan kervanlar.</summary>
        public List<CaravanState> caravans = new List<CaravanState>();
        public int nextCaravanId = 1;

        public RoadState Road(string key) => roads.Find(r => r.key == key);

        // ------------------------------------------------------------------ günlük
        /// <summary>Krallıkta olan önemli olayların kaydı (en yenisi sonda).</summary>
        public List<LogEntry> log = new List<LogEntry>();
        public const int MaxLog = 80;

        public void AddLog(string text, int level)
        {
            if (string.IsNullOrEmpty(text)) return;
            log.Add(new LogEntry { ticks = DateTime.UtcNow.Ticks, text = text, level = level });
            if (log.Count > MaxLog) log.RemoveRange(0, log.Count - MaxLog);
        }

        public int UnreadLetters()
        {
            int n = 0;
            foreach (var l in letters) if (!l.read || l.NeedsAnswer) n++;
            return n;
        }

        public RivalState Rival(string id) => rivals.Find(r => r.id == id);

        /// <summary>Şehir bir rakip krallığa mı ait? Öyleyse kimliğini döner.</summary>
        public string RivalOwner(string cityId)
        {
            var s = cities.Find(c => c.cityId == cityId);
            if (s == null || string.IsNullOrEmpty(s.owner) || s.owner == "player") return null;
            return s.owner;
        }

        /// <summary>Oyuncu şehri bir rakibe kaybeder.</summary>
        public void LoseCity(string cityId, string rivalId)
        {
            conqueredCities.Remove(cityId);
            var s = State(cityId);
            s.owner = rivalId;
            s.hasGovernor = false;
            s.garrison = 0;
            s.rebel = RebelState.None;
            s.unrest = 30f;
            s.candidateSeed++;
        }

        /// <summary>Şehir bizim ve isyanda değil.</summary>
        public bool IsConquered(string cityId) => conqueredCities.Contains(cityId) && !IsRebel(cityId);

        public bool Owns(string cityId) => conqueredCities.Contains(cityId);

        /// <summary>Şehir oyuncunun mu (başkent dahil, isyanda olsa da)? Eski kayıtlarda owner alanı boş kalabildiği için listeye bakar.</summary>
        public bool Holds(CityState c) => c != null && (c.cityId == "home" || conqueredCities.Contains(c.cityId));

        public bool IsRebel(string cityId)
        {
            var s = cities.Find(c => c.cityId == cityId);
            return s != null && s.rebel != RebelState.None;
        }

        public CityState State(string cityId)
        {
            var s = cities.Find(c => c.cityId == cityId);
            if (s == null)
            {
                s = new CityState { cityId = cityId };
                cities.Add(s);
            }
            return s;
        }

        /// <summary>Şehri ilk kez fetheder ya da isyanı bastırır.</summary>
        public void Conquer(string cityId, bool fame = true)
        {
            var s = State(cityId);
            string former = s.owner;
            s.owner = "player";
            if (!string.IsNullOrEmpty(former) && former != "player")
            {
                // rakipten alındı: vali ve takviye sıfırdan
                s.hasGovernor = false;
                s.garrison = 0;
                string msg = RivalAI.OnPlayerConquered(this, cityId, former);
                if (msg != null) pendingMessage = msg;
            }
            if (!conqueredCities.Contains(cityId))
            {
                conqueredCities.Add(cityId);
                s.unrest = Mathf.Clamp(35f + Reputation.ConquestUnrest(), 15f, 60f); // yeni fethedilen halk tedirgin; kötü şöhret korkutur
                if (fame) Reputation.Change(this, 2f, "Fetih: şehir krallığına katıldı");
            }
            else if (s.rebel != RebelState.None)
            {
                // asi vali yargılanır, halk ayaklanmasında vali kaçar; şehir sindirilmiştir
                s.hasGovernor = false;
                s.unrest = 30f;
                s.candidateSeed++;
                if (fame) Reputation.Change(this, 3f, "İsyan bastırıldı");
            }
            s.rebel = RebelState.None;
            s.rebelPower = 0;
            s.warnedUnrest = s.warnedLoyalty = false;
            pendingConquestAnim = cityId;
        }

        public int GetArmyCount(string unitId)
        {
            var entry = army.Find(a => a.unitId == unitId);
            return entry != null ? entry.count : 0;
        }

        /// <summary>
        /// En fazla kaç bölük beslenebilir: başkent 10, her şehir +2, her Kışla seviyesi +3.
        /// </summary>
        public int ArmyCapacity()
        {
            int cap = 10;
            foreach (var id in conqueredCities) if (!IsRebel(id)) cap += 2;
            foreach (var c in cities) if (c.rebel == RebelState.None && (c.cityId == "home" || conqueredCities.Contains(c.cityId))) cap += 3 * c.barracks;
            return cap;
        }

        /// <summary>Kapasiteden düşen bölükler: ordudakiler + yol devriyeleri. Şehre takviye bırakılan bölük
        /// ordudan ayrılır ve kapasiteyi boşaltır; onun yerine her şehrin kendi takviye sınırı vardır.</summary>
        public int UsedCapacity()
        {
            int n = TotalArmy();
            foreach (var r in roads) n += r.PatrolCount();
            return n;
        }

        /// <summary>Şehirlerdeki takviye bölüklerinin toplamı (maaş alırlar ama ordu kapasitesinden yer tutmazlar).</summary>
        public int GarrisonTotal()
        {
            int n = 0;
            foreach (var c in cities) if (Holds(c)) n += c.garrison;
            return n;
        }

        /// <summary>Bir şehre en fazla kaç takviye bölüğü bırakılabilir: 2, her Sur seviyesi +1.</summary>
        public static int GarrisonCap(CityState c) => 2 + (c != null ? c.walls : 0);

        /// <summary>Fetihlerde kazanılan yıldızların toplamı (her şehrin en iyisi).</summary>
        public int TotalStars()
        {
            int n = 0;
            foreach (var c in cities) n += Mathf.Clamp(c.stars, 0, 3);
            return n;
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
            entry.Normalize(); // eklenenler acemi, çıkarılanlar en az tecrübeliler
        }

        /// <summary>Savaşa en tecrübeli bölüğü çıkarır; tecrübesini döndürür (-1: bölük yok).</summary>
        public int TakeVeteran(string unitId)
        {
            var e = army.Find(a => a.unitId == unitId);
            if (e == null || e.count <= 0) return -1;
            e.Normalize();
            int best = e.Best();
            e.xp.Remove(best);
            e.count--;
            return best;
        }

        /// <summary>Savaştan dönen bölüğü tecrübesiyle orduya katar.</summary>
        public void ReturnVeteran(string unitId, int xp)
        {
            var e = army.Find(a => a.unitId == unitId);
            if (e == null) { e = new ArmyEntry { unitId = unitId, count = 0 }; army.Add(e); }
            e.Normalize();
            e.count++;
            e.xp.Add(Mathf.Max(0, xp));
        }

        /// <summary>Bu birlik türündeki en tecrübeli bölüğün puanı.</summary>
        public int BestXp(string unitId)
        {
            var e = army.Find(a => a.unitId == unitId);
            return e != null ? e.Best() : 0;
        }
    }

    /// <summary>Oyun durumunu cihaza JSON olarak kaydeder.</summary>
    public static class SaveSystem
    {
        const int CurrentVersion = 5;
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
                    if (loaded != null && loaded.version == 4)
                    {
                        // rakip krallıklar eklendi: sahiplik alanı yeniden kurulur
                        loaded.version = CurrentVersion;
                        if (loaded.rivals == null) loaded.rivals = new List<RivalState>();
                        foreach (var c in loaded.cities) c.owner = loaded.conqueredCities.Contains(c.cityId) || c.cityId == "home" ? "player" : "";
                    }
                    if (loaded != null && loaded.version == 3)
                    {
                        // eski kayıt: fethedilenler korunur, yönetim durumu eklenir
                        loaded.version = CurrentVersion;
                        if (loaded.cities == null) loaded.cities = new List<CityState>();
                        foreach (var id in loaded.conqueredCities) loaded.State(id).owner = "player";
                        if (loaded.rivals == null) loaded.rivals = new List<RivalState>();
                    }
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
