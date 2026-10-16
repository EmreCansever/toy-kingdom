using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Krallık simülasyonu: gelir, huzursuzluk, vali sadakati ve isyanlar. Dakikada bir adım ilerler.
    /// Saf mantık; sahne nesnelerine bağlı değil, ileride rakip krallıklar da aynı kuralları kullanır.
    /// </summary>
    public static class KingdomSim
    {
        public const int MaxLevel = 3;
        public const int HomeBaseIncome = 5;
        /// <summary>Genel gelir çarpanı (ekonomi dengesi).</summary>
        public const float IncomeScale = 0.6f;
        /// <summary>Eşik aşıldıktan sonra isyana kadar tanınan süre (dakika).</summary>
        public const float RevoltGrace = 3f;

        /// <summary>Ziyafet: parayla anında huzursuzluk -20 (bekleme süreli).</summary>
        public static int FeastCost(int difficulty) => 50 + 25 * Mathf.Max(1, difficulty);
        public const float FeastRelief = 20f;
        public const float FeastCooldownMinutes = 6f;

        /// <summary>Bir bölüğün dükkân fiyatı.</summary>
        public static int UnitPrice(UnitData u) => u == null ? 0 : Mathf.RoundToInt(u.goldPrice * 2.2f / 10f) * 10;

        // ------------------------------------------------------------------ tanımlar

        public static string TraitName(GovernorTrait t)
        {
            switch (t)
            {
                case GovernorTrait.Loyal: return "Sadık";
                case GovernorTrait.Ambitious: return "Hırslı";
                case GovernorTrait.Just: return "Adil";
                case GovernorTrait.Cruel: return "Zalim";
                default: return "Tüccar";
            }
        }

        public static string TraitInfo(GovernorTrait t)
        {
            switch (t)
            {
                case GovernorTrait.Loyal: return "Sadakati zamanla artar";
                case GovernorTrait.Ambitious: return "Gelir +%15, sadakati hızla düşer";
                case GovernorTrait.Just: return "Halkı daha iyi yatıştırır";
                case GovernorTrait.Cruel: return "Korkuyla yatıştırır, isyanı daha sert olur";
                default: return "Gelir +%25";
            }
        }

        public static string BuildingName(Building b)
        {
            switch (b)
            {
                case Building.Market: return "Pazar";
                case Building.Barracks: return "Kışla";
                case Building.Walls: return "Sur";
                default: return "Tapınak";
            }
        }

        public static string BuildingInfo(Building b)
        {
            switch (b)
            {
                case Building.Market: return "Gelir +%25 / seviye";
                case Building.Barracks: return "+3 ordu kapasitesi, halkı yatıştırır";
                case Building.Walls: return "Kapı +%25 sağlam, şehir daha güvenli";
                default: return "Halkı en çok yatıştıran yapı";
            }
        }

        public static int BuildingCost(Building b, int currentLevel)
        {
            int baseCost = b == Building.Market ? 150 : b == Building.Barracks ? 200 : b == Building.Walls ? 250 : 180;
            return baseCost * (currentLevel + 1);
        }

        public static string TaxName(TaxLevel t) => t == TaxLevel.Low ? "Düşük" : t == TaxLevel.High ? "Yüksek" : "Normal";

        static readonly string[] Names =
        {
            "Ahmet Bey", "Kara Murat", "Selim Ağa", "Demir Han", "Yusuf Paşa", "Bilge Hatun", "Tarık Bey", "Aslan Bey",
            "Gülbahar Hatun", "Orhan Ağa", "Kılıç Arslan", "Turgut Reis", "Mihrimah Hatun", "Battal Gazi", "Ertuğ Bey",
            "Doğan Bey", "Akın Han", "Ilgaz Bey", "Toprak Ağa", "Ayla Hatun"
        };

        /// <summary>Şehir için üç vali adayı (aynı tohumla hep aynı adaylar).</summary>
        public static List<Governor> Candidates(CityState s)
        {
            int h = 17;
            foreach (char ch in s.cityId ?? "x") h = h * 31 + ch;
            var rnd = new System.Random(h + s.candidateSeed * 7919);
            var list = new List<Governor>();
            int[] powers = { 1 + rnd.Next(2), 2 + rnd.Next(2), 4 + rnd.Next(2) };
            for (int i = 0; i < 3; i++)
            {
                var trait = (GovernorTrait)rnd.Next(5);
                list.Add(new Governor
                {
                    name = Names[rnd.Next(Names.Length)],
                    trait = trait,
                    power = Mathf.Min(5, powers[i] + Reputation.CandidatePowerBonus()),
                    loyalty = Mathf.Clamp((trait == GovernorTrait.Loyal ? 80f + rnd.Next(15) : trait == GovernorTrait.Ambitious ? 45f + rnd.Next(15) : 55f + rnd.Next(25))
                        + Reputation.CandidateLoyaltyBonus(), 20f, 100f)
                });
            }
            return list;
        }

        public static int HireCost(Governor g) => 100 * g.power;
        public static int UpgradeCost(Governor g) => 150 * (g.power + 1);
        public static int GiftCost(Governor g) => 40 + 30 * g.power;

        // ------------------------------------------------------------------ hesaplar

        static float TaxIncome(TaxLevel t) => t == TaxLevel.Low ? 0.7f : t == TaxLevel.High ? 1.4f : 1f;

        /// <summary>Şehrin dakikalık geliri (isyandaysa 0).</summary>
        public static int Income(CityState s, int baseIncome, bool home)
        {
            if (s == null) return baseIncome;
            if (s.rebel != RebelState.None) return 0;
            float m = 1f + 0.25f * s.market;
            m *= PolicyIncome(s.policy);
            if (s.incomeModUntil > System.DateTime.UtcNow.Ticks) m *= s.incomeMod;
            if (!home)
            {
                m *= TaxIncome(s.tax);
                if (s.hasGovernor && s.governor.trait == GovernorTrait.Merchant) m *= 1.25f;
                if (s.hasGovernor && s.governor.trait == GovernorTrait.Ambitious) m *= 1.15f;
            }
            return Mathf.Max(1, Mathf.RoundToInt(baseIncome * m * IncomeScale));
        }

        public static float PolicyIncome(CityPolicy p) => p == CityPolicy.Trade ? 1.3f : p == CityPolicy.Military ? 0.85f : p == CityPolicy.Peace ? 0.75f : 1f;

        public static string PolicyName(CityPolicy p) =>
            p == CityPolicy.Trade ? "Ticaret" : p == CityPolicy.Military ? "Askeri" : p == CityPolicy.Peace ? "Huzur" : "Serbest";

        public static string PolicyInfo(CityPolicy p) =>
            p == CityPolicy.Trade ? "Gelir +%30, halk biraz huzursuzlanır, savunma -%15" :
            p == CityPolicy.Military ? "Savunma +%35, gelir -%15" :
            p == CityPolicy.Peace ? "Huzursuzluk hızla azalır, gelir -%25" : "Özel bir öncelik yok";

        /// <summary>Dakikalık bakım gideri: her bölük (ordu ve takviye) 1 altın, her vali gücü kadar maaş alır.</summary>
        public static float Upkeep(SaveData data)
        {
            float u = 1f * (data.UsedCapacity() + data.GarrisonTotal());
            foreach (var c in data.cities)
                if (data.Holds(c) && c.hasGovernor && c.rebel == RebelState.None) u += 1f * c.governor.power;
            return u;
        }

        /// <summary>Huzursuzluğun dakikalık değişimi (artı = kötüleşiyor).</summary>
        public static float UnrestRate(CityState s)
        {
            float r = s.tax == TaxLevel.Low ? -1.2f : s.tax == TaxLevel.High ? 1.6f : 0.35f;
            if (!s.hasGovernor) r += 0.6f;
            else
            {
                r -= 0.28f * s.governor.power;
                if (s.governor.trait == GovernorTrait.Just) r -= 0.4f;
                if (s.governor.trait == GovernorTrait.Cruel) r -= 0.7f;
            }
            r -= 0.4f * s.temple + 0.25f * s.barracks + 0.1f * s.walls;
            if (s.policy == CityPolicy.Trade) r += 0.25f;
            if (s.policy == CityPolicy.Peace) r -= 0.7f;
            // yolların güvenliği: eşkıya baskını halkı korkutur, güvenli yollar refah getirir
            r += Trade.UnrestEffect(SaveSystem.Data, s.cityId);
            r += Reputation.UnrestEffect();
            return r;
        }

        /// <summary>Vali sadakatinin dakikalık değişimi. Güçlü valiler daha hırslıdır.</summary>
        public static float LoyaltyRate(CityState s)
        {
            if (!s.hasGovernor) return 0f;
            var g = s.governor;
            float r = -0.12f * (g.power - 1);
            if (g.trait == GovernorTrait.Loyal) r += 0.35f;
            if (g.trait == GovernorTrait.Ambitious) r -= 0.35f;
            if (g.trait == GovernorTrait.Cruel) r -= 0.1f;
            if (s.tax == TaxLevel.High) r -= 0.1f; // halkın öfkesi valiye de yansır
            r += Reputation.LoyaltyEffect();
            return r;
        }

        // ------------------------------------------------------------------ adım

        public class TickReport
        {
            public int gold;
            public int upkeep;
            public readonly List<string> messages = new List<string>();
            public readonly List<string> newRebels = new List<string>();
            /// <summary>İsyan geri sayımı başlayan şehirler (kamera oraya döner).</summary>
            public readonly List<string> urgent = new List<string>();
        }

        /// <summary>
        /// Simülasyonu belirtilen dakika kadar ilerletir. online=false iken (oyun kapalıyken) isyan çıkmaz:
        /// huzursuzluk 90'da, sadakat 10'da durur; oyuncu döndüğünde önlem alacak zamanı olur.
        /// </summary>
        public static TickReport Advance(SaveData data, IList<CityInfo> cities, int minutes, bool online)
        {
            var rep = new TickReport();
            if (minutes <= 0) return rep;
            foreach (var c in cities)
            {
                if (!c.home && !data.Owns(c.id)) continue;
                var s = data.State(c.id);
                rep.gold += Income(s, c.baseIncome, c.home) * minutes;
                if (c.home || s.rebel != RebelState.None) continue;

                s.unrest = Mathf.Clamp(s.unrest + UnrestRate(s) * minutes, 0f, 100f);
                if (s.hasGovernor) s.governor.loyalty = Mathf.Clamp(s.governor.loyalty + LoyaltyRate(s) * minutes, 0f, 100f);

                if (!online)
                {
                    s.unrest = Mathf.Min(s.unrest, 90f);
                    if (s.hasGovernor) s.governor.loyalty = Mathf.Max(s.governor.loyalty, 10f);
                    continue;
                }

                // eşik aşılınca hemen ayaklanma yok: önce uyarı ve RevoltGrace dakika süre
                bool counting = s.revoltTimer >= 0f;
                bool govDanger = s.hasGovernor && s.governor.loyalty <= (counting ? 5f : 0f);
                bool popDanger = s.unrest >= (counting ? 92f : 100f);
                if (!govDanger && !popDanger)
                {
                    if (counting)
                    {
                        s.revoltTimer = -1f;
                        rep.messages.Add(c.name + ": isyan tehlikesi geçti");
                    }
                }
                else if (!counting)
                {
                    s.revoltTimer = RevoltGrace;
                    rep.urgent.Add(c.id);
                    rep.messages.Add(govDanger
                        ? c.name + " valisi " + s.governor.name + " isyan hazırlığında! " + (int)RevoltGrace + " dakika içinde hediye gönder ya da azlet"
                        : c.name + " ayaklanmak üzere! " + (int)RevoltGrace + " dakika içinde ziyafet ver ya da vergiyi düşür");
                    continue;
                }
                else
                {
                    s.revoltTimer -= minutes;
                    if (s.revoltTimer > 0f) continue;
                    s.revoltTimer = -1f;
                }

                if (govDanger)
                {
                    StartRebellion(s, RebelState.Governor, c.difficulty);
                    rep.newRebels.Add(c.id);
                    if (Trade.CitySecurity(data, c.id) < 30f) s.rebelPower += 1;
                    s.rebelPower += Reputation.RebelBonus();
                    Reputation.Change(data, -5f, "Valin " + s.governor.name + " sana isyan etti");
                    rep.messages.Add(c.name + ": vali " + s.governor.name + " sana isyan etti!");
                }
                else if (popDanger)
                {
                    StartRebellion(s, RebelState.People, c.difficulty);
                    rep.newRebels.Add(c.id);
                    bool bandits = Trade.CitySecurity(data, c.id) < 30f;
                    if (bandits) s.rebelPower += 2; // yollardaki eşkıyalar asilere katılır
                    s.rebelPower += Reputation.RebelBonus();
                    Reputation.Change(data, -8f, c.name + " halkı ayaklandı");
                    rep.messages.Add(c.name + ": halk ayaklandı!" + (bandits ? " Eşkıyalar da asilere katıldı" : ""));
                }
                else
                {
                    if (s.unrest >= 85f && !s.warnedUnrest) { s.warnedUnrest = true; rep.messages.Add(c.name + " isyanın eşiğinde! Vergiyi düşür ya da tapınak kur"); }
                    if (s.unrest < 70f) s.warnedUnrest = false;
                    if (s.hasGovernor && s.governor.loyalty < 25f && !s.warnedLoyalty) { s.warnedLoyalty = true; rep.messages.Add(c.name + " valisi " + s.governor.name + " huzursuz. Hediye gönder!"); }
                    if (s.hasGovernor && s.governor.loyalty >= 40f) s.warnedLoyalty = false;
                }
            }
            // vergi: ağır vergi itibarı yavaşça aşındırır, hafif vergi kazandırır
            if (online)
            {
                float drift = 0f;
                foreach (var c in cities)
                {
                    if (c.home || !data.IsConquered(c.id)) continue;
                    var t = data.State(c.id).tax;
                    drift += t == TaxLevel.High ? -0.05f : t == TaxLevel.Low ? 0.03f : 0f;
                }
                if (data.gold < 0) drift -= 0.5f; // maaş ödenemeyen hükümdar
                drift += Reputation.Decay(data.reputation); // şöhret korunmazsa unutulur
                if (drift != 0f) Reputation.Change(data, drift * minutes, null);
            }

            // bakım giderleri: ordu ve vali maaşları
            float upkeep = Upkeep(data) * minutes + data.upkeepRemainder;
            int paid = Mathf.FloorToInt(upkeep);
            data.upkeepRemainder = upkeep - paid;
            rep.upkeep = paid;
            data.gold += rep.gold - paid;

            // hazine borçta: halk huzursuzlanır, askerler kaçar
            if (data.gold < 0)
            {
                foreach (var c in cities)
                {
                    if (c.home || !data.IsConquered(c.id)) continue;
                    var s = data.State(c.id);
                    s.unrest = Mathf.Min(online ? 100f : 90f, s.unrest + 3f * Mathf.Min(minutes, 10));
                }
                if (online)
                {
                    for (int i = 0; i < minutes && i < 5; i++)
                    {
                        if (UnityEngine.Random.value > 0.3f) continue;
                        string unit = null; int n = 0;
                        foreach (var e in data.army) if (e.count > n) { n = e.count; unit = e.unitId; }
                        if (unit == null) break;
                        data.AddArmy(unit, -1);
                        rep.messages.Add("Maaşı ödenmeyen bir bölük ordudan kaçtı!");
                    }
                }
            }
            return rep;
        }

        static void StartRebellion(CityState s, RebelState kind, int difficulty)
        {
            s.rebel = kind;
            s.revoltTimer = -1f;
            if (kind == RebelState.Governor)
                s.rebelPower = s.governor.power + (s.governor.trait == GovernorTrait.Cruel ? 1 : 0);
            else
                s.rebelPower = Mathf.Max(1, 1 + difficulty / 2 - s.barracks);
        }

        /// <summary>Asi şehri geri almak için savaşılacak kalenin çalışma anı kopyası.</summary>
        public static CastleData RebelCastle(CastleData original, CityState s, UnitData extraUnit, UnitData eliteUnit)
        {
            var c = UnityEngine.Object.Instantiate(original);
            c.name = original.name + "_Rebel";
            c.color = CastleColor.Red;
            c.garrison = new List<GarrisonEntry>();
            foreach (var g in original.garrison) if (g != null) c.garrison.Add(new GarrisonEntry { unit = g.unit, squads = g.squads });
            // senin yaptırdığın surlar artık onların
            c.gateHealth = original.gateHealth * (1f + 0.25f * s.walls);
            c.goldReward = Mathf.RoundToInt(original.goldReward * 0.3f);

            if (s.rebel == RebelState.Governor)
            {
                c.displayName = original.displayName + " (asi vali)";
                c.description = s.governor.name + " şehri ele geçirdi. Güçlü bir vali, güçlü bir isyan demek: " + s.rebelPower + " kat güç.";
                c.defenderStrength = original.defenderStrength * (1f + 0.1f * s.rebelPower);
                if (extraUnit != null) c.garrison.Add(new GarrisonEntry { unit = extraUnit, squads = Mathf.Clamp((s.rebelPower + 1) / 2, 1, 2) });
                if (eliteUnit != null && s.rebelPower >= 4) c.garrison.Add(new GarrisonEntry { unit = eliteUnit, squads = 1 });
                c.wallArchers = original.wallArchers + s.rebelPower / 2;
            }
            else
            {
                c.displayName = original.displayName + " (ayaklanma)";
                c.description = "Halk silahlandı. Asiler acemi ama kalabalık.";
                c.defenderStrength = original.defenderStrength * (0.75f + 0.05f * s.rebelPower);
                if (extraUnit != null) c.garrison.Add(new GarrisonEntry { unit = extraUnit, squads = Mathf.Clamp(s.rebelPower, 1, 3) });
                c.reinforcement = null;
                c.reinforcementCount = 0;
            }
            return c;
        }
    }

    /// <summary>Simülasyonun bir şehir hakkında bilmesi gerekenler.</summary>
    public struct CityInfo
    {
        public string id;
        public string name;
        public int baseIncome;
        public int difficulty;
        public bool home;
        /// <summary>'S' yeşil vadi, 'D' çöl, 'W' kış.</summary>
        public char biome;
        /// <summary>Yolla bağlı komşu şehirler.</summary>
        public string[] neighbors;
    }
}
