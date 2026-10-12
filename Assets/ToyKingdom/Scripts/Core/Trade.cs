using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Şehirler arası ticaret: kendi iki şehrimizi bağlayan her yolda kervanlar gidip gelir ve altın getirir.
    /// Yolun güvenliği düşerse eşkıyalar kervanları soyar, çevreyi yağmalar ve halkı huzursuzlaştırır.
    /// Ordudan ayrılan devriye bölükleri güvenliği artırır ve baskınları püskürtür.
    /// </summary>
    public static class Trade
    {
        /// <summary>Haritadaki yol uzunlukları (kervanın yolculuk süresi için). Harita sahnesi doldurur.</summary>
        public static readonly Dictionary<string, float> RoadLengths = new Dictionary<string, float>();

        public const float CaravanSpeed = 0.7f;       // dünya birimi / saniye
        public const float MinInterval = 100f, MaxInterval = 160f; // saniye

        public class Report
        {
            public readonly List<string> messages = new List<string>();
            /// <summary>Kervanın vardığı şehir ve getirdiği altın.</summary>
            public readonly List<KeyValuePair<string, int>> gains = new List<KeyValuePair<string, int>>();
            public bool roadsChanged;
            public bool Any => messages.Count > 0 || gains.Count > 0 || roadsChanged;
        }

        static Report pending = new Report();

        public static Report Take()
        {
            var r = pending;
            pending = new Report();
            return r;
        }

        public static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        // ------------------------------------------------------------------ birlikler

        /// <summary>Bir bölüğün devriye gücü: atlı şövalye yolları en iyi korur.</summary>
        public static float PatrolWeight(string unitId)
        {
            switch (unitId)
            {
                case "knight": return 2f;
                case "shield": return 1.3f;
                case "archer": return 1.15f;
                case "climber": return 0.6f;
                default: return 1f;
            }
        }

        public static float PatrolStrength(RoadState r)
        {
            float s = 0f;
            foreach (var e in r.patrol) s += e.count * PatrolWeight(e.unitId);
            return s;
        }

        public static bool AddPatrol(SaveData d, RoadState r, string unitId)
        {
            if (r == null || !r.active || d.GetArmyCount(unitId) <= 0) return false;
            d.AddArmy(unitId, -1);
            var e = r.patrol.Find(x => x.unitId == unitId);
            if (e == null) r.patrol.Add(e = new ArmyEntry { unitId = unitId, count = 0 });
            e.count++;
            return true;
        }

        public static bool RemovePatrol(SaveData d, RoadState r, string unitId)
        {
            var e = r != null ? r.patrol.Find(x => x.unitId == unitId) : null;
            if (e == null || e.count <= 0) return false;
            e.count--;
            if (e.count == 0) r.patrol.Remove(e);
            d.AddArmy(unitId, 1);
            return true;
        }

        static void ReturnPatrol(SaveData d, RoadState r)
        {
            foreach (var e in r.patrol) d.AddArmy(e.unitId, e.count);
            r.patrol.Clear();
        }

        // ------------------------------------------------------------------ hesaplar

        static float CityUnrest(SaveData d, string id, bool home) => home ? 20f : d.State(id).unrest;

        /// <summary>Yolun ulaşmaya çalıştığı güvenlik seviyesi.</summary>
        public static float Target(SaveData d, RoadState r, IDictionary<string, CityInfo> infos)
        {
            infos.TryGetValue(r.a, out var ia);
            infos.TryGetValue(r.b, out var ib);
            var sa = d.State(r.a);
            var sb = d.State(r.b);
            float t = 45f + 22f * Mathf.Min(PatrolStrength(r), 2.6f);
            t += 4f * (sa.walls + sb.walls);
            if (sa.policy == CityPolicy.Military || sb.policy == CityPolicy.Military) t += 8f;
            float unrest = (CityUnrest(d, r.a, ia.home) + CityUnrest(d, r.b, ib.home)) * 0.5f;
            t -= 0.3f * (unrest - 40f);
            if (Border(d, ia, infos) || Border(d, ib, infos)) t -= 8f;
            return Mathf.Clamp(t, 0f, 100f);
        }

        /// <summary>Şehir düşman ya da asi bir şehre komşu mu? (sınır yolları daha tehlikeli)</summary>
        static bool Border(SaveData d, CityInfo c, IDictionary<string, CityInfo> infos)
        {
            if (c.neighbors == null) return false;
            foreach (var n in c.neighbors)
                if (!string.IsNullOrEmpty(n) && (d.RivalOwner(n) != null || d.IsRebel(n))) return true;
            return false;
        }

        public static bool IsBorderRoad(SaveData d, RoadState r, IDictionary<string, CityInfo> infos)
        {
            infos.TryGetValue(r.a, out var ia);
            infos.TryGetValue(r.b, out var ib);
            return Border(d, ia, infos) || Border(d, ib, infos);
        }

        /// <summary>Kervanın eşkıya baskınına uğrama olasılığı.</summary>
        public static float Risk(RoadState r) => r.security >= 65f ? 0.03f : 0.03f + (65f - r.security) / 65f * 0.7f;

        /// <summary>Baskın olursa devriyenin eşkıyaları püskürtme olasılığı.</summary>
        public static float RepelChance(RoadState r)
        {
            float s = PatrolStrength(r);
            return s <= 0f ? 0f : Mathf.Min(0.85f, 0.35f + 0.25f * s);
        }

        public static float LossChance(RoadState r) => Risk(r) * (1f - RepelChance(r));

        /// <summary>Bir kervanın getirdiği altın: iki şehrin gelirine göre.</summary>
        public static int CaravanValue(SaveData d, RoadState r, IDictionary<string, CityInfo> infos)
        {
            float v = 0f;
            foreach (var id in new[] { r.a, r.b })
            {
                if (!infos.TryGetValue(id, out var c)) continue;
                var s = d.State(id);
                v += KingdomSim.Income(s, c.baseIncome, c.home) * (s.policy == CityPolicy.Trade ? 1.25f : 1f);
            }
            return Mathf.Max(6, Mathf.RoundToInt(v * 1.4f));
        }

        public static string SecurityName(float s) => s >= 70f ? "Güvenli" : s >= 45f ? "Sakin" : s >= 25f ? "Riskli" : "Eşkıya yatağı";

        public static Color SecurityColor(float s) =>
            s >= 70f ? UIFactory.Hex("4CC46A") : s >= 45f ? UIFactory.Hex("A8D45A") : s >= 25f ? UIFactory.Hex("FFB443") : UIFactory.Hex("FF5E4D");

        /// <summary>Şehre bağlı açık yolların ortalama güvenliği (yol yoksa 50).</summary>
        public static float CitySecurity(SaveData d, string cityId)
        {
            float sum = 0f; int n = 0;
            foreach (var r in d.roads)
                if (r.active && (r.a == cityId || r.b == cityId)) { sum += r.security; n++; }
            return n > 0 ? sum / n : 50f;
        }

        /// <summary>Yolların şehrin huzursuzluğuna dakikalık etkisi (artı: kötüleşir).</summary>
        public static float UnrestEffect(SaveData d, string cityId)
        {
            if (d == null || d.roads == null) return 0f;
            bool any = false;
            foreach (var r in d.roads) if (r.active && (r.a == cityId || r.b == cityId)) { any = true; break; }
            if (!any) return 0f;
            return (40f - CitySecurity(d, cityId)) * 0.015f;
        }

        static float Length(string key) => RoadLengths.TryGetValue(key, out float l) ? l : 14f;

        public static float TravelSeconds(string key) => Mathf.Clamp(Length(key) / CaravanSpeed, 10f, 45f);

        // ------------------------------------------------------------------ adım

        static bool Active(SaveData d, CityInfo c) => c.home || d.IsConquered(c.id);

        /// <summary>Yolları şehirlerin sahipliğine göre açar/kapatır. Kopan yolun devriyesi orduya döner.</summary>
        public static void Sync(SaveData d, IList<CityInfo> infos)
        {
            var byId = new Dictionary<string, CityInfo>();
            foreach (var c in infos) byId[c.id] = c;
            var keys = new HashSet<string>();
            foreach (var c in infos)
            {
                if (!Active(d, c) || c.neighbors == null) continue;
                foreach (var n in c.neighbors)
                    if (!string.IsNullOrEmpty(n) && byId.TryGetValue(n, out var o) && Active(d, o)) keys.Add(Key(c.id, n));
            }
            long now = DateTime.UtcNow.Ticks;
            foreach (var k in keys)
            {
                if (d.Road(k) != null) continue;
                var parts = k.Split('|');
                d.roads.Add(new RoadState
                {
                    key = k, a = parts[0], b = parts[1], security = 50f,
                    nextCaravanTicks = now + (long)(UnityEngine.Random.Range(15f, 45f) * TimeSpan.TicksPerSecond)
                });
                pending.roadsChanged = true;
            }
            foreach (var r in d.roads)
            {
                bool act = keys.Contains(r.key);
                if (act == r.active) continue;
                r.active = act;
                pending.roadsChanged = true;
                if (!act)
                {
                    if (r.PatrolCount() > 0)
                    {
                        pending.messages.Add(Name(r, byId) + " yolu koptu. Devriye orduya döndü");
                        ReturnPatrol(d, r);
                    }
                    d.caravans.RemoveAll(c => c.roadKey == r.key);
                }
                else r.nextCaravanTicks = now + (long)(30f * TimeSpan.TicksPerSecond);
            }
        }

        public static string Name(RoadState r, IDictionary<string, CityInfo> infos)
        {
            string a = infos.TryGetValue(r.a, out var ia) ? ia.name : r.a;
            string b = infos.TryGetValue(r.b, out var ib) ? ib.name : r.b;
            return a + " – " + b;
        }

        /// <summary>
        /// Ticareti ilerletir. minutes &gt; 0 iken güvenlik değişir; çevrimdışıyken kervanların beklenen kazancı eklenir.
        /// Çevrimiçiyken kervanlar gerçek zamanlı yola çıkar ve varır.
        /// </summary>
        public static void Tick(SaveData d, IList<CityInfo> infos, int minutes, bool online)
        {
            Sync(d, infos);
            var byId = new Dictionary<string, CityInfo>();
            foreach (var c in infos) byId[c.id] = c;
            long now = DateTime.UtcNow.Ticks;

            if (minutes > 0)
            {
                int offlineGold = 0;
                foreach (var r in d.roads)
                {
                    if (!r.active) continue;
                    float target = Target(d, r, byId);
                    r.security = Mathf.Clamp(r.security + (target - r.security) * (1f - Mathf.Pow(0.9f, minutes)), 0f, 100f);

                    if (r.security < 30f && !r.warnedLow)
                    {
                        r.warnedLow = true;
                        pending.messages.Add(Name(r, byId) + " yolunda eşkıyalar çoğaldı! Devriye ata");
                    }
                    if (r.security > 45f) r.warnedLow = false;

                    if (online)
                    {
                        // eşkıya yatağına dönen yolun çevresi yağmalanır
                        if (r.security < 10f && UnityEngine.Random.value < 0.1f * Mathf.Min(minutes, 3))
                        {
                            string id = UnityEngine.Random.value < 0.5f ? r.a : r.b;
                            if (byId.TryGetValue(id, out var ci))
                            {
                                int loss = Mathf.Min(Mathf.Max(0, d.gold), 10 + UnityEngine.Random.Range(0, 21));
                                d.gold -= loss;
                                if (!ci.home) d.State(id).unrest = Mathf.Min(100f, d.State(id).unrest + 3f);
                                pending.messages.Add("Eşkıyalar " + ci.name + " köylerini yağmaladı! -" + loss + " altın");
                            }
                        }
                    }
                    else
                    {
                        float n = minutes * 60f / ((MinInterval + MaxInterval) * 0.5f);
                        offlineGold += Mathf.RoundToInt(n * CaravanValue(d, r, byId) * (1f - LossChance(r)));
                    }
                }
                if (offlineGold > 0)
                {
                    d.gold += offlineGold;
                    pending.messages.Add("Yokken kervanlar " + offlineGold + " altın getirdi");
                }
            }

            if (!online) return;

            // yeni kervanlar
            foreach (var r in d.roads)
            {
                if (!r.active || now < r.nextCaravanTicks) continue;
                r.nextCaravanTicks = now + (long)(UnityEngine.Random.Range(MinInterval, MaxInterval) * TimeSpan.TicksPerSecond);
                var c = new CaravanState
                {
                    id = d.nextCaravanId++, roadKey = r.key, fromA = r.nextFromA,
                    startTicks = now, arriveTicks = now + (long)(TravelSeconds(r.key) * TimeSpan.TicksPerSecond),
                    value = CaravanValue(d, r, byId), eventT = UnityEngine.Random.Range(0.3f, 0.7f)
                };
                r.nextFromA = !r.nextFromA;
                if (UnityEngine.Random.value < Risk(r))
                    c.fate = UnityEngine.Random.value < RepelChance(r) ? 2 : 1;
                d.caravans.Add(c);
            }

            // varışlar ve baskınlar
            for (int i = d.caravans.Count - 1; i >= 0; i--)
            {
                var c = d.caravans[i];
                var r = d.Road(c.roadKey);
                if (r == null || !r.active) { d.caravans.RemoveAt(i); continue; }
                // soyulan kervanın sahnesi biraz görünsün
                long due = c.fate == 1 ? c.EventTicks + (long)(2.5f * TimeSpan.TicksPerSecond) : c.arriveTicks;
                if (now < due) continue;
                d.caravans.RemoveAt(i);
                string dest = c.fromA ? r.b : r.a;
                byId.TryGetValue(r.a, out var ia);
                byId.TryGetValue(r.b, out var ib);
                if (c.fate == 1)
                {
                    r.robbed++;
                    r.security = Mathf.Max(0f, r.security - 6f);
                    if (!ia.home) d.State(r.a).unrest = Mathf.Min(100f, d.State(r.a).unrest + 1.5f);
                    if (!ib.home) d.State(r.b).unrest = Mathf.Min(100f, d.State(r.b).unrest + 1.5f);
                    pending.messages.Add("Eşkıyalar " + Name(r, byId) + " yolunda kervanı soydu! (" + c.value + " altınlık mal)");
                    continue;
                }
                d.gold += c.value;
                r.delivered++;
                r.earned += c.value;
                pending.gains.Add(new KeyValuePair<string, int>(dest, c.value));
                if (!ia.home) d.State(r.a).unrest = Mathf.Max(0f, d.State(r.a).unrest - 0.5f);
                if (!ib.home) d.State(r.b).unrest = Mathf.Max(0f, d.State(r.b).unrest - 0.5f);
                if (c.fate == 2)
                {
                    r.repelled++;
                    r.security = Mathf.Min(100f, r.security + 3f);
                    string msg = "Devriye " + Name(r, byId) + " yolunda eşkıyaları püskürttü";
                    if (UnityEngine.Random.value < 0.2f && r.patrol.Count > 0)
                    {
                        var e = r.patrol[UnityEngine.Random.Range(0, r.patrol.Count)];
                        e.count--;
                        if (e.count <= 0) r.patrol.Remove(e);
                        var u = UnitDatabase.Instance != null ? UnitDatabase.Instance.Get(e.unitId) : null;
                        msg += ", ama bir " + (u != null ? u.displayName : "devriye") + " bölüğü düştü";
                    }
                    pending.messages.Add(msg);
                }
            }
        }
    }
}
