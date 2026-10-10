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
        public const int HomeBaseIncome = 4;

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
                case Building.Barracks: return "Huzursuzluk azalır, halk isyanı zayıflar";
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
                    power = powers[i],
                    loyalty = trait == GovernorTrait.Loyal ? 80f + rnd.Next(15) : trait == GovernorTrait.Ambitious ? 45f + rnd.Next(15) : 55f + rnd.Next(25)
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
            if (!home)
            {
                m *= TaxIncome(s.tax);
                if (s.hasGovernor && s.governor.trait == GovernorTrait.Merchant) m *= 1.25f;
                if (s.hasGovernor && s.governor.trait == GovernorTrait.Ambitious) m *= 1.15f;
            }
            return Mathf.Max(1, Mathf.RoundToInt(baseIncome * m));
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
            return r;
        }

        // ------------------------------------------------------------------ adım

        public class TickReport
        {
            public int gold;
            public readonly List<string> messages = new List<string>();
            public readonly List<string> newRebels = new List<string>();
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

                if (s.hasGovernor && s.governor.loyalty <= 0f)
                {
                    StartRebellion(s, RebelState.Governor, c.difficulty);
                    rep.newRebels.Add(c.id);
                    rep.messages.Add(c.name + ": vali " + s.governor.name + " sana isyan etti!");
                }
                else if (s.unrest >= 100f)
                {
                    StartRebellion(s, RebelState.People, c.difficulty);
                    rep.newRebels.Add(c.id);
                    rep.messages.Add(c.name + ": halk ayaklandı!");
                }
                else
                {
                    if (s.unrest >= 85f && !s.warnedUnrest) { s.warnedUnrest = true; rep.messages.Add(c.name + " isyanın eşiğinde! Vergiyi düşür ya da tapınak kur"); }
                    if (s.unrest < 70f) s.warnedUnrest = false;
                    if (s.hasGovernor && s.governor.loyalty < 25f && !s.warnedLoyalty) { s.warnedLoyalty = true; rep.messages.Add(c.name + " valisi " + s.governor.name + " huzursuz. Hediye gönder!"); }
                    if (s.hasGovernor && s.governor.loyalty >= 40f) s.warnedLoyalty = false;
                }
            }
            data.gold += rep.gold;
            return rep;
        }

        static void StartRebellion(CityState s, RebelState kind, int difficulty)
        {
            s.rebel = kind;
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
                if (extraUnit != null) c.garrison.Add(new GarrisonEntry { unit = extraUnit, squads = Mathf.Max(1, (s.rebelPower + 1) / 2) });
                if (eliteUnit != null && s.rebelPower >= 4) c.garrison.Add(new GarrisonEntry { unit = eliteUnit, squads = 1 });
                c.wallArchers = original.wallArchers + s.rebelPower / 2;
            }
            else
            {
                c.displayName = original.displayName + " (ayaklanma)";
                c.description = "Halk silahlandı. Asiler acemi ama kalabalık.";
                c.defenderStrength = original.defenderStrength * (0.75f + 0.05f * s.rebelPower);
                if (extraUnit != null) c.garrison.Add(new GarrisonEntry { unit = extraUnit, squads = s.rebelPower });
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
    }
}
