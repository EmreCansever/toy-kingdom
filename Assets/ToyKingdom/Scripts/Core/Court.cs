using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Saray: krallardan ve vezirden gelen mektuplar, cevapların sonuçları, diplomasi eylemleri ve saray olayları.
    /// Her mektubun bir cevap süresi vardır; süre dolarsa varsayılan (genelde en kötü) seçenek uygulanır.
    /// Böylece oyuncu kararlarını zamanında ve dikkatle vermek zorundadır.
    /// </summary>
    public static class Court
    {
        public const string Vizier = "Vezir Lala Paşa";
        const int MaxOpenEvents = 3;

        /// <summary>Mektup akışında haritanın bilmesi gerekenler.</summary>
        public class Report
        {
            public readonly List<string> messages = new List<string>();
            public readonly List<Letter> newLetters = new List<Letter>();
            /// <summary>Sahibi değişen şehirler (oyuncuya dönen ya da kaybedilen).</summary>
            public readonly List<string> cityChanged = new List<string>();
            public readonly List<string> attacksStarted = new List<string>();
            public bool Any => messages.Count > 0 || newLetters.Count > 0 || cityChanged.Count > 0 || attacksStarted.Count > 0;
        }

        static Report pending = new Report();
        /// <summary>Biriken olayları alır ve sıfırlar.</summary>
        public static Report Take()
        {
            var r = pending;
            pending = new Report();
            return r;
        }

        // ------------------------------------------------------------------ yardımcılar

        static Letter New(SaveData data, LetterKind kind, string from, string title, string body, float minutes)
        {
            long now = DateTime.UtcNow.Ticks;
            var l = new Letter
            {
                id = data.nextLetterId++, kind = kind, from = from, title = title, body = body,
                createdTicks = now, expiresTicks = minutes > 0f ? now + (long)(minutes * TimeSpan.TicksPerMinute) : 0
            };
            data.letters.Add(l);
            // eski, cevaplanmış mektupları temizle
            while (data.letters.Count > 30)
            {
                int idx = data.letters.FindIndex(x => !x.NeedsAnswer);
                if (idx < 0) break;
                data.letters.RemoveAt(idx);
            }
            pending.newLetters.Add(l);
            return l;
        }

        static void Opt(Letter l, string label, string hint, int cost = 0) =>
            l.options.Add(new LetterOption { label = label, hint = hint, cost = cost });

        public static string Sender(Letter l) =>
            l.from == "court" ? Vizier : RivalAI.KingFull(l.from) + " (" + RivalAI.Get(l.from).name + ")";

        static string Gold(int n) => n.ToString("N0") + " altın";

        public static int PlayerStrength(SaveData data)
        {
            int g = 0;
            foreach (var c in data.cities) if (c.owner == "player") g += c.garrison;
            return 12 * data.TotalArmy() + 8 * g;
        }

        static void AllUnrest(SaveData data, float amount)
        {
            foreach (var c in data.cities)
                if (c.owner == "player" && c.cityId != "home" && c.rebel == RebelState.None)
                    c.unrest = Mathf.Clamp(c.unrest + amount, 0f, 100f);
        }

        // ------------------------------------------------------------------ krallardan gelen mektuplar

        public static void TributeDemand(SaveData data, RivalState r, string target, string from, string cityName, int amount, float strength)
        {
            var def = RivalAI.Get(r.id);
            var l = New(data, LetterKind.TributeDemand, r.id, "Haraç talebi",
                "Ey komşu hükümdar,\n\n" + cityName + " surlarının gölgesi askerlerimin iştahını kabartıyor. " +
                Gold(amount) + " gönderirsen ordum başka yöne bakar. Göndermezsen kumlar kanla sulanır.\n\n" + RivalAI.KingFull(r.id), 2f);
            l.cityId = target; l.cityName = cityName; l.amount = amount; l.extra = from; l.power = strength;
            Opt(l, "Öde  (" + Gold(amount) + ")", "Saldırı iptal, ilişki +8. Ama " + def.kingTitle + " bir dahakine daha fazlasını ister.", amount);
            Opt(l, "Pazarlık et  (" + Gold(amount / 2) + ")", "Kabul edebilir; reddederse öfkelenip daha güçlü saldırır.", amount / 2);
            Opt(l, "Reddet", "Ordu " + cityName + " üzerine yürür (3 dk). İlişki -8.");
            l.defaultOption = 2;
        }

        public static void WarDeclaration(SaveData data, RivalState r, string target, string cityName, float strength)
        {
            int price = Mathf.RoundToInt(strength * 4f / 10f) * 10;
            var l = New(data, LetterKind.WarDeclaration, r.id, "Savaş ilanı",
                "Ayaz Hatun kimseden izin almaz.\n\nOrdum " + cityName + " üzerine yürüyor. Şerefli bir hükümdarsan surlarında karşıla. " +
                "Yok eğer korkuyorsan, hazinen konuşsun.\n\n" + RivalAI.KingFull(r.id), RivalAI.MarchMinutes);
            l.cityId = target; l.cityName = cityName; l.amount = price; l.power = strength;
            Opt(l, "Geri çekilmeleri için öde  (" + Gold(price) + ")", "Kraliçe gururunu kolay satmaz: yaklaşık %55 ihtimalle kabul eder.", price);
            Opt(l, "Meydan oku", "Halkın gururlanır (tüm şehirlerde huzursuzluk -5). İlişki -5. Savunmana güven.");
            l.defaultOption = 1;
        }

        public static void PeaceOffer(SaveData data, RivalState r)
        {
            if (HasOpen(data, LetterKind.PeaceOffer, r.id)) return;
            var def = RivalAI.Get(r.id);
            string body = def.trait == RivalAI.KingTrait.Greedy
                ? "Savaş ticarete zarar veriyor, kervanlarım boş dönüyor. On beş dakikalık bir barışa ne dersin?"
                : "Kılıçlarımızı bir süre kınında tutalım. Sözüm sözdür: barış süresince sana dokunmam.";
            var l = New(data, LetterKind.PeaceOffer, r.id, "Barış teklifi", body + "\n\n" + RivalAI.KingFull(r.id), 3f);
            Opt(l, "Kabul et", "15 dakika barış: birbirinize saldırmazsınız. İlişki +10.");
            Opt(l, "Haraç karşılığında kabul et", "Barış için onlardan altın iste. Güçlüysen kabul ederler; değilse ilişki bozulur.");
            Opt(l, "Reddet", "İlişki -5.");
            l.defaultOption = 2;
        }

        public static void BuyBack(SaveData data, RivalState r, string cityId, string cityName, int price)
        {
            var l = New(data, LetterKind.BuyBack, r.id, cityName + " hakkında",
                cityName + " artık bizim bayrağımızı taşıyor. Ama altın her kapıyı açar: " + Gold(price) +
                " gönderirsen şehri sana geri veririm.\n\n" + RivalAI.KingFull(r.id), 3f);
            l.cityId = cityId; l.cityName = cityName; l.amount = price;
            Opt(l, "Şehri satın al  (" + Gold(price) + ")", "Şehir geri döner (vali ve takviyesiz). İlişki +5.", price);
            Opt(l, "Reddet", "Şehri savaşarak geri almalısın.");
            l.defaultOption = 1;
        }

        public static void Ransom(SaveData data, RivalState r, List<string> captives, string cityName)
        {
            int price = 70 * captives.Count;
            var l = New(data, LetterKind.Ransom, r.id, "Esirler",
                cityName + " önlerinde " + captives.Count + " bölüğünü esir aldık. Bölük başı 70 altın fidye istiyorum; " +
                "ödemezsen hepsi zindanda çürür.\n\n" + RivalAI.KingFull(r.id), 3f);
            l.cityName = cityName; l.amount = price; l.extra = string.Join(",", captives);
            Opt(l, "Fidyeyi öde  (" + Gold(price) + ")", captives.Count + " bölük ordusuna geri döner.", price);
            Opt(l, "Reddet", "Esirler kaybolur. Ordunun morali için kötü bir haber.");
            l.defaultOption = 1;
        }

        /// <summary>Gururlu kral, sınırına dayanan oyuncuyu uyarır.</summary>
        public static void BorderThreat(SaveData data, RivalState r, string cityName)
        {
            if (HasOpen(data, LetterKind.Threat, r.id)) return;
            var l = New(data, LetterKind.Threat, r.id, "Uyarı",
                cityName + " artık sınırımda duruyor. Bir adım daha atarsan buzlar senin için erimeyecek. Haddini bil.\n\n" + RivalAI.KingFull(r.id), 3f);
            l.cityName = cityName;
            Opt(l, "Özür dile", "İlişki +8. Ama halkın seni zayıf bulur (huzursuzluk +4).");
            Opt(l, "Meydan oku", "İlişki -12. Halkın gururlanır (huzursuzluk -4).");
            l.defaultOption = 1;
        }

        static void Reply(SaveData data, string rivalId, string title, string body)
        {
            New(data, LetterKind.Reply, rivalId, title, body + "\n\n" + (rivalId == "court" ? Vizier : RivalAI.KingFull(rivalId)), 0f);
        }

        static bool HasOpen(SaveData data, LetterKind kind, string from)
        {
            foreach (var l in data.letters) if (l.kind == kind && l.from == from && l.NeedsAnswer) return true;
            return false;
        }

        // ------------------------------------------------------------------ saray olayları

        static void CourtEvent(SaveData data, IList<CityInfo> cities)
        {
            int open = 0;
            foreach (var l in data.letters) if (l.from == "court" && l.NeedsAnswer) open++;
            if (open >= MaxOpenEvents) return;

            var mine = new List<CityInfo>();
            var governed = new List<CityInfo>();
            foreach (var c in cities)
            {
                if (c.home || !data.IsConquered(c.id)) continue;
                mine.Add(c);
                if (data.State(c.id).hasGovernor) governed.Add(c);
            }

            var pool = new List<int> { 2, 4 }; // kervan, şenlik her zaman olabilir
            if (mine.Count > 0) { pool.Add(0); pool.Add(0); pool.Add(3); }
            if (governed.Count > 0) { pool.Add(1); pool.Add(1); }
            int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            var city = mine.Count > 0 ? mine[UnityEngine.Random.Range(0, mine.Count)] : default;

            switch (pick)
            {
                case 0:
                {
                    var l = New(data, LetterKind.Famine, "court", "Kıtlık: " + city.name,
                        "Efendim, " + city.name + " ambarları boşaldı. Halk ekmek kuyruğunda bekliyor ve sabrı tükeniyor.", 5f);
                    l.cityId = city.id; l.cityName = city.name;
                    Opt(l, "Tahıl gönder  (150 altın)", "Huzursuzluk -20.", 150);
                    Opt(l, "Vergiyi düşür", "Şehrin vergisi düşük olur: gelir azalır, halk yatışır.");
                    Opt(l, "Görmezden gel", "Huzursuzluk +15. İsyan riski!");
                    l.defaultOption = 2;
                    break;
                }
                case 1:
                {
                    var g = governed[UnityEngine.Random.Range(0, governed.Count)];
                    var st = data.State(g.id);
                    var l = New(data, LetterKind.Corruption, "court", "Yolsuzluk ihbarı",
                        "Efendim, " + g.name + " valisi " + st.governor.name + " hazineden çalıyormuş. Kanıtımız yok, yalnızca fısıltılar var.", 5f);
                    l.cityId = g.id; l.cityName = g.name;
                    Opt(l, "Soruştur  (80 altın)", "Suçluysa (%60) vali azledilir ve çaldığı 250 altın geri gelir; değilse sadakati çok düşer.", 80);
                    Opt(l, "Valiyi azarla", "Sadakat -12. Yarı ihtimalle hırsızlık sürer (gelir -%20, 10 dk).");
                    Opt(l, "Görmezden gel", "Şehrin geliri 10 dakika %30 azalır.");
                    l.defaultOption = 2;
                    break;
                }
                case 2:
                {
                    var l = New(data, LetterKind.Caravan, "court", "Tüccar kervanı",
                        "Efendim, doğuya gidecek bir kervan sermaye arıyor. 200 altın yatırırsak 8 dakika sonra 400 altın dönebilir. Ama yollar haydut dolu.", 2f);
                    Opt(l, "Yatırım yap  (200 altın)", "%70 ihtimalle 8 dakika sonra 400 altın; %30 ihtimalle hepsi kaybolur.", 200);
                    Opt(l, "Geç", "Bir şey olmaz.");
                    l.defaultOption = 1;
                    break;
                }
                case 3:
                {
                    var l = New(data, LetterKind.Plague, "court", "Salgın: " + city.name,
                        "Efendim, " + city.name + " sokaklarında salgın baş gösterdi. Hemen önlem almazsak şehir çöker.", 5f);
                    l.cityId = city.id; l.cityName = city.name;
                    Opt(l, "Hekim gönder  (220 altın)", "Salgın durur.", 220);
                    Opt(l, "Karantina", "Şehir 4 dakika hiç gelir getirmez, salgın söner.");
                    Opt(l, "Görmezden gel", "Gelir 10 dakika yarıya iner, huzursuzluk +10.");
                    l.defaultOption = 2;
                    break;
                }
                default:
                {
                    var l = New(data, LetterKind.Festival, "court", "Halk şenlik istiyor",
                        "Efendim, halk zaferlerinizi kutlamak için şenlik istiyor. Küçük bir şölen gönülleri kazanır.", 2f);
                    Opt(l, "Şenlik düzenle  (150 altın)", "Tüm şehirlerde huzursuzluk -10.", 150);
                    Opt(l, "Geç", "Halk biraz küser (huzursuzluk +3).");
                    l.defaultOption = 1;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ adım

        /// <summary>Her kontrolde çağrılır: süresi dolan mektuplar, kervanlar, fidye, borç, yeni saray olayları.</summary>
        public static void Tick(SaveData data, IList<CityInfo> cities, int minutes, bool online)
        {
            long now = DateTime.UtcNow.Ticks;

            // süresi dolan mektuplar: varsayılan seçenek
            foreach (var l in data.letters.ToArray())
            {
                // oyun kapalıyken (ya da savaştayken) cevap süresi işlemez: dönünce en az 2 dakika kalır
                if (!online && l.NeedsAnswer && l.expiresTicks > 0)
                {
                    l.expiresTicks = Math.Max(l.expiresTicks, now + 2 * TimeSpan.TicksPerMinute);
                    continue;
                }
                if (l.NeedsAnswer && l.expiresTicks > 0 && now >= l.expiresTicks)
                {
                    string res = Resolve(data, cities, l, l.defaultOption < 0 ? l.options.Count - 1 : l.defaultOption, true);
                    if (!string.IsNullOrEmpty(res)) pending.messages.Add("Cevap verilmedi: " + res);
                }
            }

            // kervanlar
            for (int i = data.caravanTicks.Count - 1; i >= 0; i--)
            {
                if (now < data.caravanTicks[i]) continue;
                int amount = data.caravanAmounts[i];
                data.caravanTicks.RemoveAt(i);
                data.caravanAmounts.RemoveAt(i);
                if (UnityEngine.Random.value < 0.7f)
                {
                    data.gold += amount;
                    Reply(data, "court", "Kervan döndü", "Efendim, kervan sağ salim döndü ve hazineye " + Gold(amount) + " getirdi.");
                }
                else Reply(data, "court", "Kervan kayıp", "Efendim, kervan çölde haydutlara yakalandı. Yatırımımız kayboldu.");
            }

            // kaybedilen savaşın esirleri
            if (data.pendingCaptives.Count > 0)
            {
                var r = data.Rival(data.pendingCaptor);
                if (r != null && r.alive) Ransom(data, r, new List<string>(data.pendingCaptives), data.pendingCaptiveCity);
                data.pendingCaptives.Clear();
                data.pendingCaptor = "";
            }

            // borç
            if (data.gold < 0 && !data.inDebt)
            {
                data.inDebt = true;
                New(data, LetterKind.Debt, "court", "Hazine boş!",
                    "Efendim, hazine borçta. Askerlerin maaşı ödenemiyor: bölükler kaçmaya, halk homurdanmaya başladı. " +
                    "Ordu küçültün, vergiyi artırın ya da valilerin maaşını düşürün.\n\n" + Vizier, 0f);
            }
            else if (data.gold >= 0) data.inDebt = false;

            // yeni saray olayı
            if (online && minutes > 0)
            {
                data.eventTimer -= minutes;
                if (data.eventTimer <= 0f)
                {
                    data.eventTimer = 5f + UnityEngine.Random.Range(0f, 3f);
                    CourtEvent(data, cities);
                }
            }
        }

        // ------------------------------------------------------------------ cevaplar

        /// <summary>Bir mektuba cevap verir ve sonucu döndürür. expired: süre dolduğu için otomatik.</summary>
        public static string Resolve(SaveData data, IList<CityInfo> cities, Letter l, int option, bool expired = false)
        {
            if (!l.NeedsAnswer || option < 0 || option >= l.options.Count) return null;
            var opt = l.options[option];
            if (opt.cost > 0 && data.gold < opt.cost)
            {
                if (!expired) return "Yeterli altın yok";
                option = l.defaultOption; // süre dolduysa ve parası yoksa varsayılan
                opt = l.options[option];
            }
            l.read = true;
            l.resolved = true;
            l.chosen = option;
            data.gold -= opt.cost;
            var r = data.Rival(l.from);
            var st = string.IsNullOrEmpty(l.cityId) ? null : data.State(l.cityId);
            string res;

            switch (l.kind)
            {
                case LetterKind.TributeDemand:
                    if (r == null) { res = ""; break; }
                    if (option == 0)
                    {
                        r.awaitingAnswer = false;
                        r.relation += 8f;
                        r.greed *= 1.25f;
                        r.attackTimer = 10f + UnityEngine.Random.Range(0f, 5f);
                        res = RivalAI.KingFull(r.id) + " haracı aldı, ordusu geri döndü";
                        // sinsi kral bazen parayı alıp yine saldırır
                        if (UnityEngine.Random.value < RivalAI.Get(r.id).treachery)
                        {
                            RivalAI.StartAttack(r, l.cityId, l.extra, l.power);
                            pending.attacksStarted.Add(r.id);
                            res = RivalAI.KingFull(r.id) + " haracı aldı ama yine de saldırıyor! Sinsi Sultan...";
                            r.relation -= 25f;
                        }
                    }
                    else if (option == 1)
                    {
                        float chance = 0.35f + r.relation / 200f;
                        if (UnityEngine.Random.value < chance)
                        {
                            r.awaitingAnswer = false;
                            r.relation += 3f;
                            r.greed *= 1.1f;
                            r.attackTimer = 10f + UnityEngine.Random.Range(0f, 5f);
                            res = "Pazarlık tuttu: " + RivalAI.KingFull(r.id) + " yarı fiyata razı oldu";
                        }
                        else
                        {
                            data.gold += opt.cost; // ödeme geri
                            r.relation -= 10f;
                            RivalAI.StartAttack(r, l.cityId, l.extra, l.power * 1.15f);
                            pending.attacksStarted.Add(r.id);
                            res = "Pazarlık başarısız! Öfkelenen " + RivalAI.KingFull(r.id) + " ordusunu " + l.cityName + " üzerine sürdü";
                        }
                    }
                    else
                    {
                        r.relation -= 8f;
                        RivalAI.StartAttack(r, l.cityId, l.extra, l.power);
                        pending.attacksStarted.Add(r.id);
                        res = "Haraç reddedildi: " + RivalAI.Get(r.id).name + " ordusu " + l.cityName + " üzerine yürüyor";
                    }
                    break;

                case LetterKind.WarDeclaration:
                    if (r == null) { res = ""; break; }
                    if (option == 0)
                    {
                        if (UnityEngine.Random.value < 0.55f + r.relation / 300f)
                        {
                            r.attackTarget = "";
                            r.attackFrom = "";
                            r.power += r.attackPower * 0.5f;
                            r.relation += 4f;
                            res = RivalAI.KingFull(r.id) + " altını kabul etti, ordusu geri çekildi";
                        }
                        else
                        {
                            data.gold += opt.cost;
                            r.relation -= 6f;
                            res = RivalAI.KingFull(r.id) + " teklifini hakaret saydı! Ordu yoluna devam ediyor";
                        }
                    }
                    else
                    {
                        r.relation -= 5f;
                        AllUnrest(data, -5f);
                        res = "Meydan okudun. Halkın gururlandı; şimdi " + l.cityName + " surları konuşsun";
                    }
                    break;

                case LetterKind.PeaceOffer:
                    if (r == null) { res = ""; break; }
                    if (option == 0)
                    {
                        MakePeace(r, 15f);
                        r.relation += 10f;
                        res = RivalAI.Get(r.id).name + " ile 15 dakikalık barış imzalandı";
                    }
                    else if (option == 1)
                    {
                        int str = PlayerStrength(data);
                        if (str > r.power * 1.1f || UnityEngine.Random.value < 0.3f)
                        {
                            int pay = Mathf.RoundToInt((80f + r.power * 0.5f) / 10f) * 10;
                            data.gold += pay;
                            MakePeace(r, 15f);
                            r.relation += 2f;
                            res = "Barış imzalandı ve " + RivalAI.KingFull(r.id) + " " + Gold(pay) + " ödedi";
                        }
                        else
                        {
                            r.relation -= 12f;
                            res = RivalAI.KingFull(r.id) + " şartını küstahlık saydı, barış olmadı";
                        }
                    }
                    else
                    {
                        r.relation -= 5f;
                        res = "Barış teklifi reddedildi";
                    }
                    break;

                case LetterKind.BuyBack:
                    if (option == 0 && r != null && data.RivalOwner(l.cityId) == r.id)
                    {
                        data.Conquer(l.cityId);
                        data.State(l.cityId).unrest = 50f;
                        data.pendingConquestAnim = "";
                        r.relation += 5f;
                        r.power += 20f;
                        pending.cityChanged.Add(l.cityId);
                        res = l.cityName + " geri satın alındı";
                    }
                    else if (option == 0)
                    {
                        data.gold += opt.cost;
                        res = "Şehir artık onların elinde değil, anlaşma geçersiz";
                    }
                    else res = l.cityName + " düşman elinde kaldı";
                    break;

                case LetterKind.Ransom:
                    if (option == 0)
                    {
                        foreach (var u in (l.extra ?? "").Split(',')) if (!string.IsNullOrEmpty(u)) data.AddArmy(u, 1);
                        if (r != null) { r.relation += 3f; r.power += 10f; }
                        res = "Fidye ödendi, esirler orduna döndü";
                    }
                    else res = "Esirler kayboldu";
                    break;

                case LetterKind.Threat:
                    if (r == null) { res = ""; break; }
                    if (option == 0) { r.relation += 8f; AllUnrest(data, 4f); res = "Özür diledin. Halkın bunu zayıflık saydı"; }
                    else { r.relation -= 12f; AllUnrest(data, -4f); res = "Meydan okudun. " + RivalAI.KingFull(r.id) + " bunu unutmayacak"; }
                    break;

                case LetterKind.Famine:
                    if (st == null || st.owner != "player") { res = ""; break; }
                    if (option == 0) { st.unrest = Mathf.Max(0f, st.unrest - 20f); res = l.cityName + " halkı doydu"; }
                    else if (option == 1) { st.tax = TaxLevel.Low; st.unrest = Mathf.Max(0f, st.unrest - 5f); res = l.cityName + " vergisi düşürüldü"; }
                    else { st.unrest = Mathf.Min(100f, st.unrest + 15f); res = l.cityName + " halkı aç ve öfkeli (huzursuzluk +15)"; }
                    break;

                case LetterKind.Corruption:
                    if (st == null || !st.hasGovernor) { res = "Vali artık görevde değil"; break; }
                    if (option == 0)
                    {
                        if (UnityEngine.Random.value < 0.6f)
                        {
                            string name = st.governor.name;
                            st.hasGovernor = false;
                            st.candidateSeed++;
                            data.gold += 250;
                            res = name + " suçlu çıktı, azledildi; 250 altın geri alındı";
                        }
                        else
                        {
                            st.governor.loyalty = Mathf.Max(0f, st.governor.loyalty - 30f);
                            res = st.governor.name + " masum çıktı ve gücendi (sadakat -30)";
                        }
                    }
                    else if (option == 1)
                    {
                        st.governor.loyalty = Mathf.Max(0f, st.governor.loyalty - 12f);
                        if (UnityEngine.Random.value < 0.5f) SetIncomeMod(st, 0.8f, 10f);
                        res = "Vali azarlandı (sadakat -12)";
                    }
                    else { SetIncomeMod(st, 0.7f, 10f); res = l.cityName + " geliri 10 dakika %30 azaldı"; }
                    break;

                case LetterKind.Caravan:
                    if (option == 0)
                    {
                        data.caravanTicks.Add(DateTime.UtcNow.Ticks + 8 * TimeSpan.TicksPerMinute);
                        data.caravanAmounts.Add(400);
                        res = "Kervan yola çıktı, 8 dakika sonra haber gelecek";
                    }
                    else res = "Kervana yatırım yapılmadı";
                    break;

                case LetterKind.Plague:
                    if (st == null || st.owner != "player") { res = ""; break; }
                    if (option == 0) res = "Hekimler salgını durdurdu";
                    else if (option == 1) { SetIncomeMod(st, 0f, 4f); res = l.cityName + " karantinada (4 dk gelir yok)"; }
                    else { SetIncomeMod(st, 0.5f, 10f); st.unrest = Mathf.Min(100f, st.unrest + 10f); res = "Salgın " + l.cityName + " şehrini kasıp kavuruyor"; }
                    break;

                case LetterKind.Festival:
                    if (option == 0) { AllUnrest(data, -10f); res = "Şenlik gönülleri kazandı (huzursuzluk -10)"; }
                    else { AllUnrest(data, 3f); res = "Halk biraz küstü"; }
                    break;

                default:
                    res = "";
                    break;
            }
            l.result = res;
            return res;
        }

        static void SetIncomeMod(CityState s, float mod, float minutes)
        {
            s.incomeMod = mod;
            s.incomeModUntil = DateTime.UtcNow.Ticks + (long)(minutes * TimeSpan.TicksPerMinute);
        }

        static void MakePeace(RivalState r, float minutes)
        {
            r.treaty = Treaty.Peace;
            r.treatyUntilTicks = DateTime.UtcNow.Ticks + (long)(minutes * TimeSpan.TicksPerMinute);
            r.awaitingAnswer = false;
        }

        // ------------------------------------------------------------------ oyuncunun diplomasi eylemleri

        public const int GiftCost = 100;
        public const int PeaceCost = 120;

        public static string SendGift(SaveData data, RivalState r)
        {
            if (data.gold < GiftCost) return "Yeterli altın yok";
            data.gold -= GiftCost;
            var def = RivalAI.Get(r.id);
            float gain = def.trait == RivalAI.KingTrait.Greedy ? 16f : 9f;
            r.relation = Mathf.Min(100f, r.relation + gain);
            Reply(data, r.id, "Hediyene cevap", def.trait == RivalAI.KingTrait.Greedy
                ? "Altının parıltısı gözlerimi okşadı. Komşuluğumuz tatlanıyor."
                : "Hediyeni aldım. Krallığım cömertliği unutmaz, ama gurur altınla satın alınmaz.");
            return def.name + " ile ilişki +" + Mathf.RoundToInt(gain);
        }

        public static string ProposePeace(SaveData data, RivalState r)
        {
            if (r.AtPeace) return "Zaten barıştasınız";
            if (data.gold < PeaceCost) return "Yeterli altın yok";
            data.gold -= PeaceCost;
            var def = RivalAI.Get(r.id);
            float chance = 0.25f + r.relation / 120f + (PlayerStrength(data) > r.power ? 0.2f : 0f) + (def.trait == RivalAI.KingTrait.Proud ? -0.05f : 0f);
            if (r.Attacking) chance -= 0.2f;
            if (UnityEngine.Random.value < chance)
            {
                MakePeace(r, 15f);
                if (r.Attacking) { r.power += r.attackPower * 0.5f; r.attackTarget = ""; r.attackFrom = ""; }
                r.relation += 5f;
                Reply(data, r.id, "Barış", "Elçini dinledim. On beş dakika boyunca kılıçlarımız kınında kalacak.");
                return def.name + " barışı kabul etti (15 dk)";
            }
            r.relation -= 5f;
            Reply(data, r.id, "Barış reddedildi", "Elçin boş elle dönüyor. Barışı ben istediğimde konuşuruz.");
            return def.name + " barışı reddetti";
        }

        public static string DemandTribute(SaveData data, RivalState r)
        {
            var def = RivalAI.Get(r.id);
            int str = PlayerStrength(data);
            if (str < r.power * 1.2f) return "Haraç isteyecek kadar güçlü değilsin (ordu gücün " + str + ", onların " + Mathf.RoundToInt(r.power) + ")";
            float chance = 0.45f + (str > r.power * 2f ? 0.25f : 0f) - (def.trait == RivalAI.KingTrait.Proud ? 0.2f : 0f);
            if (UnityEngine.Random.value < chance)
            {
                int pay = Mathf.RoundToInt((80f + r.power * 0.6f) / 10f) * 10;
                data.gold += pay;
                r.power = Mathf.Max(10f, r.power - pay * 0.1f);
                r.relation -= 15f;
                Reply(data, r.id, "Haraç", "Gücüne boyun eğiyorum... şimdilik. " + Gold(pay) + " gönderdim. Bu günü unutmayacağım.");
                return def.name + " " + Gold(pay) + " haraç ödedi";
            }
            r.relation -= 25f;
            r.attackTimer = Mathf.Min(r.attackTimer, 1f);
            Reply(data, r.id, "Küstahlık", "Benden haraç mı istiyorsun? Cevabımı ordum getirecek.");
            return def.name + " haracı reddetti ve öfkelendi!";
        }

        /// <summary>Barışı bozarak savaş ilan eder. Yeminbozan hükümdara halk da öteki krallar da güvenmez.</summary>
        public static string DeclareWar(SaveData data, RivalState r)
        {
            var def = RivalAI.Get(r.id);
            bool broke = r.AtPeace;
            r.treaty = Treaty.None;
            r.relation -= broke ? 40f : 15f;
            if (broke)
            {
                AllUnrest(data, 12f);
                foreach (var o in data.rivals) if (o != r) o.relation -= 10f;
            }
            Reply(data, r.id, "Savaş", broke
                ? "Yeminini bozdun. Bundan sonra senin sözün rüzgârda toz."
                : "Demek savaş istiyorsun. Öyle olsun.");
            return broke ? def.name + " ile barış bozuldu! Halkın huzursuz (+12)" : def.name + " ile savaştasın";
        }
    }
}
