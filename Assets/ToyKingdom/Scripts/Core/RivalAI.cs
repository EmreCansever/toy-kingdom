using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Rakip krallıklar: Kum Sultanlığı (çöl) ve Buz Krallığı (kış).
    /// Dakikada bir güç toplarlar, komşu tarafsız şehirleri ele geçirirler ve zamanla oyuncunun şehirlerine
    /// önceden haber verilen saldırılar düzenlerler. Saldırılar savaş oynatmadan, şehrin savunma gücüyle çözülür;
    /// oyuncu sur, kışla, güçlü vali ve takviye bölükleriyle hazırlanır. Rakip başkenti düşerse krallık dağılır.
    /// Kural seti küçük tutuldu: oyuncuyu sıkmayacak kadar hareketli, ama haksız hissettirmeyecek kadar öngörülebilir.
    /// </summary>
    public static class RivalAI
    {
        public enum KingTrait { Greedy, Proud }

        public struct Def
        {
            public string id, name, shortName;
            public char biome;
            public CastleColor towers;
            public Color color;
            // hükümdar
            public string king, kingTitle;
            public KingTrait trait;
            public string traitName, traitInfo;
            /// <summary>Habersiz saldırı ve anlaşma bozma eğilimi (0-1).</summary>
            public float treachery;
        }

        public static readonly Def[] Defs =
        {
            new Def
            {
                id = "sand", name = "Kum Sultanlığı", shortName = "Sultanlık", biome = 'D', towers = CastleColor.Red, color = new Color(0.93f, 0.42f, 0.2f),
                king = "Kara Davud", kingTitle = "Sultan", trait = KingTrait.Greedy, treachery = 0.2f,
                traitName = "Açgözlü ve sinsi",
                traitInfo = "Altına düşkün: saldırmadan önce haraç ister, hediyeyi sever. Ödedikçe iştahı büyür; bazen habersiz saldırır, anlaşmayı bozabilir."
            },
            new Def
            {
                id = "frost", name = "Buz Krallığı", shortName = "Buz Krallığı", biome = 'W', towers = CastleColor.Green, color = new Color(0.3f, 0.78f, 0.62f),
                king = "Ayaz Hatun", kingTitle = "Kraliçe", trait = KingTrait.Proud, treachery = 0f,
                traitName = "Gururlu ve onurlu",
                traitInfo = "Savaşı önceden ilan eder, sözünü tutar. Pazarlıktan ve tehditten hoşlanmaz; sınırına yaklaşanı uyarır."
            },
        };

        public static string KingFull(string id) { var d = Get(id); return d.kingTitle + " " + d.king; }

        public static Def Get(string id)
        {
            foreach (var d in Defs) if (d.id == id) return d;
            return Defs[0];
        }

        // ------------------------------------------------------------------ ayarlar

        /// <summary>Saldırı ordusunun yolda geçirdiği süre (gerçek dakika). Oyuncuya hazırlanma fırsatı.</summary>
        public const float MarchMinutes = 3f;
        /// <summary>Oyuncu en az bu kadar şehir sahibi olmadan ve bu kadar dakika oynamadan saldırı gelmez.</summary>
        const int AttackMinCities = 4;
        const float AttackMinPlayMinutes = 30f;
        const float MaxPower = 300f;
        public const float GarrisonDefense = 14f;

        /// <summary>Oyuncu şehrinin rakip saldırısına karşı savunma gücü.</summary>
        public static float Defense(CityState s, int difficulty)
        {
            float d = 15f + 10f * Mathf.Max(1, difficulty) + 15f * s.walls + 8f * s.barracks + GarrisonDefense * s.garrison;
            if (s.hasGovernor) d += 6f * s.governor.power;
            if (s.policy == CityPolicy.Military) d *= 1.35f;
            if (s.policy == CityPolicy.Trade) d *= 0.85f;
            return d;
        }

        /// <summary>Tarafsız bir şehri ele geçirmek için gereken güç.</summary>
        static float ExpandCost(int difficulty) => 22f * Mathf.Max(1, difficulty);

        // ------------------------------------------------------------------ kurulum

        /// <summary>İlk açılışta: her bölgenin en zor şehri başkent olur, ona en yakın bir komşu da baştan rakibindir.</summary>
        public static void Setup(SaveData data, IList<CityInfo> cities)
        {
            if (data.rivals.Count > 0) return;
            foreach (var def in Defs)
            {
                CityInfo? capital = null;
                foreach (var c in cities)
                {
                    if (c.home || c.biome != def.biome || data.Owns(c.id)) continue;
                    if (capital == null || c.difficulty > capital.Value.difficulty) capital = c;
                }
                if (capital == null) continue;
                var r = new RivalState { id = def.id, capitalId = capital.Value.id };
                data.rivals.Add(r);
                data.State(capital.Value.id).owner = def.id;

                // başkente komşu, aynı bölgedeki en kolay şehir
                CityInfo? extra = null;
                foreach (var n in capital.Value.neighbors ?? new string[0])
                {
                    var ci = Find(cities, n);
                    if (ci == null || ci.Value.home || ci.Value.biome != def.biome || data.Owns(n) || data.RivalOwner(n) != null) continue;
                    if (extra == null || ci.Value.difficulty < extra.Value.difficulty) extra = ci;
                }
                if (extra != null) data.State(extra.Value.id).owner = def.id;
            }
        }

        static CityInfo? Find(IList<CityInfo> cities, string id)
        {
            foreach (var c in cities) if (c.id == id) return c;
            return null;
        }

        public static List<string> OwnedBy(SaveData data, string rivalId)
        {
            var list = new List<string>();
            foreach (var s in data.cities) if (s.owner == rivalId) list.Add(s.cityId);
            return list;
        }

        // ------------------------------------------------------------------ adım

        public class Report
        {
            public readonly List<string> messages = new List<string>();
            /// <summary>Rakibin ele geçirdiği şehirler (tarafsızdan ya da oyuncudan).</summary>
            public readonly List<string> captured = new List<string>();
            /// <summary>Savunmayı tutan oyuncu şehirleri.</summary>
            public readonly List<string> defended = new List<string>();
            /// <summary>Yeni başlayan saldırılar (rakip kimliği).</summary>
            public readonly List<string> attacksStarted = new List<string>();
            public bool Any => messages.Count > 0 || captured.Count > 0 || defended.Count > 0 || attacksStarted.Count > 0;
        }

        /// <summary>
        /// Dakikalık adım. Çevrimdışıyken (online=false) rakipler büyür ve en fazla bir şehir alır ama yeni saldırı başlatmaz.
        /// Yoldaki saldırılar varış zamanı geldiyse her durumda çözülür (oyuncu önceden uyarılmıştı).
        /// </summary>
        public static void Advance(SaveData data, IList<CityInfo> cities, int minutes, bool online, Report rep)
        {
            if (minutes > 0)
            {
                if (online) data.playMinutes += minutes;
                foreach (var r in data.rivals)
                {
                    if (!r.alive) continue;
                    var def = Get(r.id);
                    var owned = OwnedBy(data, r.id);
                    if (owned.Count == 0) { r.alive = false; continue; }

                    float income = 0f;
                    foreach (var id in owned) { var ci = Find(cities, id); if (ci != null) income += ci.Value.baseIncome; }
                    // çevrimdışıyken rakipler en fazla yarım saatlik güç toplar: dönüşte oyuncu ezilmiş bulmasın
                    int growMinutes = online ? minutes : Mathf.Min(minutes, 30);
                    // büyüyen ordun komşu kralları tedirgin eder: silahlanırlar ve sana soğurlar
                    float threat = Threat(data, r);
                    r.power = Mathf.Min(MaxPower, r.power + growMinutes * (1f + 0.08f * income) * (1f + 0.6f * threat));

                    // ilişkiler zamanla gergin dengeye (-10) döner
                    float baseline = Reputation.RelationBaseline() - 20f * threat; // itibarlı hükümdara ılımlı, güçlü orduya soğuk
                    r.relation += (baseline - r.relation) * Mathf.Min(1f, 0.02f * minutes);
                    if (online && threat > 0.5f && !r.warnedArmy)
                    {
                        r.warnedArmy = true;
                        Court.Reply(data, r.id, "Ordun bizi tedirgin ediyor", def.trait == KingTrait.Proud
                            ? "Sınırlarımıza yığdığın askerleri görüyoruz. Krallığım korkmaz, ama hazırlıksız da yakalanmaz. Kılıçlarımızı biliyoruz."
                            : "Ordun her gün büyüyor... Komşuna bunca asker neden gerek, merak ediyorum. Ben de boş durmayacağım.");
                        rep.messages.Add(def.name + " büyüyen ordundan tedirgin: silahlanıyor");
                    }
                    if (threat < 0.25f) r.warnedArmy = false;

                    if (r.treaty == Treaty.Peace && !r.AtPeace)
                    {
                        r.treaty = Treaty.None;
                        rep.messages.Add(def.name + " ile barış anlaşmasının süresi doldu");
                    }

                    // zayıflayan ya da dost rakip barış önerir
                    if (online && !r.AtPeace && r.relation > -45f)
                    {
                        r.peaceOfferTimer -= minutes;
                        if (r.peaceOfferTimer <= 0f)
                        {
                            r.peaceOfferTimer = 14f + UnityEngine.Random.Range(0f, 6f);
                            if (r.power < 90f || r.relation > 20f) Court.PeaceOffer(data, r);
                        }
                    }

                    // genişleme
                    r.expandTimer -= minutes;
                    if (r.expandTimer <= 0f)
                    {
                        r.expandTimer = 7f + UnityEngine.Random.Range(0f, 3f);
                        TryExpand(data, cities, r, def, rep);
                    }

                    // saldırı hazırlığı
                    if (online && !r.Attacking && !r.awaitingAnswer)
                    {
                        r.attackTimer -= minutes;
                        if (r.attackTimer <= 0f) TryStartAttack(data, cities, r, def, rep);
                    }
                }
            }
            ResolveArrivals(data, cities, rep);
        }

        /// <summary>Oyuncunun ordusunun bu rakip için tehdit düzeyi (0-1): ordu gücü rakibinkini aştıkça artar.</summary>
        public static float Threat(SaveData data, RivalState r)
        {
            float mine = Court.PlayerStrength(data);
            return Mathf.Clamp01((mine - r.power) / Mathf.Max(60f, r.power));
        }

        static void TryExpand(SaveData data, IList<CityInfo> cities, RivalState r, Def def, Report rep)
        {
            CityInfo? best = null;
            foreach (var id in OwnedBy(data, r.id))
            {
                var ci = Find(cities, id);
                if (ci == null) continue;
                foreach (var n in ci.Value.neighbors ?? new string[0])
                {
                    var t = Find(cities, n);
                    if (t == null || t.Value.home || data.Owns(n) || data.RivalOwner(n) != null) continue;
                    if (best == null || t.Value.difficulty < best.Value.difficulty) best = t;
                }
            }
            if (best == null) return;
            float cost = ExpandCost(best.Value.difficulty);
            if (r.power < cost * 1.3f) return;
            r.power -= cost;
            data.State(best.Value.id).owner = r.id;
            rep.captured.Add(best.Value.id);
            rep.messages.Add(def.name + " " + best.Value.name + " şehrini ele geçirdi");
        }

        static void TryStartAttack(SaveData data, IList<CityInfo> cities, RivalState r, Def def, Report rep)
        {
            r.attackTimer = 3f; // koşullar uymazsa birkaç dakika sonra yeniden bakar
            int playerCities = 0;
            foreach (var id in data.conqueredCities) if (!data.IsRebel(id)) playerCities++;
            if (playerCities + 1 < AttackMinCities || data.playMinutes < AttackMinPlayMinutes) return;
            // aynı anda yalnızca bir rakip saldırır
            foreach (var o in data.rivals) if (o != r && o.Attacking) return;

            // en zayıf komşu oyuncu şehri (başkent hariç)
            string target = null, from = null;
            float bestRatio = float.MaxValue;
            foreach (var id in OwnedBy(data, r.id))
            {
                var ci = Find(cities, id);
                if (ci == null) continue;
                foreach (var n in ci.Value.neighbors ?? new string[0])
                {
                    var t = Find(cities, n);
                    if (t == null || t.Value.home || !data.IsConquered(n)) continue;
                    float def0 = Defense(data.State(n), t.Value.difficulty);
                    if (def0 < bestRatio) { bestRatio = def0; target = n; from = id; }
                }
            }
            if (target == null) return;

            float strength = r.power * 0.65f;
            if (strength < 40f) return;

            // dost hükümdar saldırmaz; barıştaysa ancak sinsi bir kral anlaşmayı bozar
            if (r.relation > 40f) { r.attackTimer = 8f; return; }
            if (r.AtPeace)
            {
                if (UnityEngine.Random.value > def.treachery * 0.5f) { r.attackTimer = 5f; return; }
                r.treaty = Treaty.None;
                r.relation -= 30f;
                rep.messages.Add(KingFull(r.id) + " barış anlaşmasını bozdu!");
            }

            var tn = Find(cities, target);
            string tname = tn != null ? tn.Value.name : "şehrin";
            r.attackTimer = 12f + UnityEngine.Random.Range(0f, 6f);

            if (def.trait == KingTrait.Greedy && UnityEngine.Random.value > def.treachery)
            {
                // önce haraç ister: cevap gelene kadar ordu bekler
                float defense = Defense(data.State(target), tn != null ? tn.Value.difficulty : 1);
                int amount = Mathf.RoundToInt((60f + defense * 2.2f) * r.greed / 10f) * 10;
                r.awaitingAnswer = true;
                Court.TributeDemand(data, r, target, from, tname, amount, strength);
                rep.messages.Add(KingFull(r.id) + " bir mektup gönderdi");
                return;
            }

            StartAttack(r, target, from, strength);
            rep.attacksStarted.Add(r.id);
            if (def.trait == KingTrait.Proud)
            {
                Court.WarDeclaration(data, r, target, tname, strength);
                rep.messages.Add(KingFull(r.id) + " savaş ilan etti: ordusu " + tname + " üzerine yürüyor!");
            }
            else rep.messages.Add("Habersiz saldırı! " + def.name + " ordusu " + tname + " üzerine yürüyor!");
        }

        /// <summary>Saldırıyı başlatır (ordu yola çıkar, MarchMinutes sonra varır).</summary>
        public static void StartAttack(RivalState r, string target, string from, float strength)
        {
            r.awaitingAnswer = false;
            r.power -= strength * 0.5f; // ordunun bir kısmı sefere çıkar
            r.attackTarget = target;
            r.attackFrom = from;
            r.attackPower = strength;
            long now = DateTime.UtcNow.Ticks;
            r.attackStartTicks = now;
            r.attackArriveTicks = now + (long)(MarchMinutes * TimeSpan.TicksPerMinute);
        }

        static void ResolveArrivals(SaveData data, IList<CityInfo> cities, Report rep)
        {
            long now = DateTime.UtcNow.Ticks;
            foreach (var r in data.rivals)
            {
                if (!r.Attacking || now < r.attackArriveTicks) continue;
                var def = Get(r.id);
                string target = r.attackTarget;
                var ci = Find(cities, target);
                string name = ci != null ? ci.Value.name : target;
                r.attackTarget = "";
                r.attackFrom = "";

                if (!data.IsConquered(target)) continue; // şehir bu arada el değiştirdi
                var s = data.State(target);
                float defense = Defense(s, ci != null ? ci.Value.difficulty : 1);
                if (r.attackPower > defense)
                {
                    data.LoseCity(target, r.id);
                    Reputation.Change(data, -6f, name + " düşmana kaptırıldı");
                    r.power += r.attackPower * 0.3f; // sağ kalanlar geri döner
                    Court.BuyBack(data, r, target, name, 200 + 70 * (ci != null ? ci.Value.difficulty : 1));
                    rep.captured.Add(target);
                    rep.messages.Add(name + " düştü! " + def.name + " şehri ele geçirdi. Geri almak için saldır");
                }
                else
                {
                    if (s.garrison > 0) s.garrison--;
                    Reputation.Change(data, 3f, name + " saldırıyı püskürttü");
                    rep.defended.Add(target);
                    rep.messages.Add(name + " saldırıyı püskürttü! " + def.name + " geri çekildi");
                }
                r.attackPower = 0f;
            }
        }

        /// <summary>Oyuncu bir rakip şehri fethetti: rakip güç kaybeder; başkentse krallık dağılır ve kalan şehirleri sana geçer.</summary>
        public static string OnPlayerConquered(SaveData data, string cityId, string formerOwner)
        {
            var r = data.Rival(formerOwner);
            if (r == null) return null;
            var def = Get(r.id);
            r.power *= 0.8f;
            if (r.capitalId != cityId) return null;

            r.alive = false;
            r.attackTarget = "";
            foreach (var id in OwnedBy(data, r.id))
            {
                data.Conquer(id, false);
                data.State(id).unrest = 60f;
            }
            data.pendingConquestAnim = cityId;
            Reputation.Change(data, 8f, def.name + " diz çöktürüldü");
            return def.name + " diz çöktü! Kalan şehirleri artık senin";
        }

        // ------------------------------------------------------------------ savaş

        /// <summary>Rakip şehrine saldırırken savaşılacak kalenin çalışma anı kopyası.</summary>
        public static CastleData RivalCastle(CastleData original, RivalState r, CityState s, UnitData extraUnit, UnitData eliteUnit)
        {
            var def = Get(r.id);
            var c = UnityEngine.Object.Instantiate(original);
            c.name = original.name + "_" + r.id;
            c.color = def.towers;
            bool capital = r.capitalId == original.cityId;
            c.displayName = original.displayName + (capital ? " (" + def.shortName + " başkenti)" : "");
            c.garrison = new List<GarrisonEntry>();
            foreach (var g in original.garrison) if (g != null) c.garrison.Add(new GarrisonEntry { unit = g.unit, squads = g.squads });
            c.defenderStrength = original.defenderStrength * (1f + Mathf.Min(r.power, 300f) / 600f);
            if (extraUnit != null) c.garrison.Add(new GarrisonEntry { unit = extraUnit, squads = 1 });
            if (capital && eliteUnit != null) c.garrison.Add(new GarrisonEntry { unit = eliteUnit, squads = 1 });
            c.gateHealth = original.gateHealth * (1f + 0.25f * s.walls);
            c.description = def.name + " şehri. " + (capital
                ? "Başkent düşerse krallık dağılır ve kalan şehirleri sana geçer."
                : "Rakibin gücü savunucularını sertleştiriyor.");
            if (capital) c.goldReward = Mathf.RoundToInt(original.goldReward * 1.5f);
            return c;
        }
    }
}
