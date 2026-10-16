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

        static string Pick(params string[] options) => options[UnityEngine.Random.Range(0, options.Length)];

        static void Opt(Letter l, string label, string hint, int cost = 0) =>
            l.options.Add(new LetterOption { label = label, hint = hint, cost = cost });

        public static string Sender(Letter l) =>
            l.from == "court" ? Vizier : RivalAI.KingFull(l.from) + " (" + RivalAI.Get(l.from).name + ")";

        static string Gold(int n) => n.ToString("N0") + " altın";

        public static int PlayerStrength(SaveData data)
        {
            int g = 0;
            foreach (var c in data.cities) if (data.Holds(c)) g += c.garrison;
            int p = 0;
            foreach (var r in data.roads) p += r.PatrolCount();
            return 12 * data.TotalArmy() + 8 * g + 6 * p;
        }

        static void AllUnrest(SaveData data, float amount)
        {
            foreach (var c in data.cities)
                if (data.Holds(c) && c.cityId != "home" && c.rebel == RebelState.None)
                    c.unrest = Mathf.Clamp(c.unrest + amount, 0f, 100f);
        }

        // ------------------------------------------------------------------ krallardan gelen mektuplar

        public static void TributeDemand(SaveData data, RivalState r, string target, string from, string cityName, int amount, float strength)
        {
            var def = RivalAI.Get(r.id);
            string g = Gold(amount);
            var l = New(data, LetterKind.TributeDemand, r.id, "Haraç talebi", LetterStyle.Compose(r.id, Pick(
                "Kervanlarımın getirdiği haberlere göre " + cityName + " ambarları dolu, surları ise ince. Ordum kumda kavrulmaktan bıktı; serin bir şehirde dinlenmek ister.\n\n" +
                "Komşuluk hakkı için " + g + " gönder, sancaklarım başka ufuklara yönelsin. Göndermezsen " + cityName + " kapılarını kervan değil, kılıç çalar.",
                "Develerim susadı, askerlerim aç. Senin " + cityName + " şehrin ise bereketli, derler. Bu adaletsizliği bir kese altınla düzeltebiliriz: " + g + ".\n\n" +
                "Kum saatim döndü. Son tanesi düştüğünde ordum yola çıkar; o vakit pazarlık değil, yağma konuşulur.",
                "Bilirsin, ben kan dökmeyi değil, altın saymayı severim. " + cityName + " için sana bir iyilik teklif ediyorum: " + g + " öde, şehrin rahat uyusun.\n\n" +
                "Reddedersen, o şehrin damlarına kum yerine ok yağar. Seçim senin, hükümdar.")), 2f);
            l.cityId = target; l.cityName = cityName; l.amount = amount; l.extra = from; l.power = strength;
            Opt(l, "Öde  (" + Gold(amount) + ")", "Saldırı iptal, ilişki +8. Ama " + def.kingTitle + " bir dahakine daha fazlasını ister.", amount);
            Opt(l, "Pazarlık et  (" + Gold(amount / 2) + ")", "Kabul edebilir; reddederse öfkelenip daha güçlü saldırır.", amount / 2);
            Opt(l, "Reddet", "Ordu " + cityName + " üzerine yürür (3 dk). İlişki -8.");
            l.defaultOption = 2;
        }

        public static void WarDeclaration(SaveData data, RivalState r, string target, string cityName, float strength)
        {
            int price = Mathf.RoundToInt(strength * 4f / 10f) * 10;
            var l = New(data, LetterKind.WarDeclaration, r.id, "Savaş ilanı", LetterStyle.Compose(r.id, Pick(
                "Kuzeyin buzlu rüzgârları adına bildiririm: ordum " + cityName + " üzerine yürüyor. Arkadan vurmak bize yakışmaz; bu yüzden sana önceden haber veriyorum.\n\n" +
                "Şerefin varsa surlarına çık ve bizi karşıla. Yoksa hazinenin ağzını aç; belki altının sesi kılıçlarımızın sesini bastırır.",
                "Kar ve demirle büyüdük, eğilmeyi bilmeyiz. " + cityName + " artık hedefimizdir; borular çalındı, sancaklar açıldı.\n\n" +
                "Bir hükümdar gibi karşıla bizi. Ya da bir tüccar gibi pazarlık et. Sana hangisi yakışıyorsa onu seç.",
                "Bu mektubu getiren atlı, ordumdan yalnızca birkaç gün öndedir. Hedefimiz " + cityName + ".\n\n" +
                "Kuzeyin kanunu açıktır: savaş önceden ilan edilir, düşman yüz yüze vurulur. Surlarında bekle, hükümdar.")), RivalAI.MarchMinutes);
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
                ? Pick("Savaş kervan yollarını kapatır, kapalı yol da hazineyi kurutur. Ben hesabını bilen adamım: kılıçlarımızı bir süre kınında tutalım, kervanlarımız serbestçe gidip gelsin.\n\nKabul edersen elimi uzatıyorum. Etmezsen kumlar yine kızışır.",
                       "Çarşılarımda ipek satılmıyor, çünkü herkes kılıç alıyor. Bu iş ikimize de yaramaz.\n\nGel, bir süreliğine barışalım. Altın kazanmak, kan dökmekten tatlıdır.")
                : Pick("Çok kan döktük; karlar kızıla boyandı. Yorgun askerlerime bir nefes, senin halkına bir huzur bağışlayalım.\n\nBir süre sürecek bir barış öneriyorum. Sözüm buz gibi serttir ama asla kırılmaz.",
                       "Sana saygı duyuyorum, hükümdar; iyi dövüştün. Şimdi kılıçlar dinlensin, yaralar sarılsın.\n\nBarışı kabul edersen, anlaşma bitene dek tek bir askerim sınırını geçmeyecek.");
            var l = New(data, LetterKind.PeaceOffer, r.id, "Barış teklifi", LetterStyle.Compose(r.id, body), 3f);
            Opt(l, "Kabul et", "15 dakika barış: birbirinize saldırmazsınız. İlişki +10.");
            Opt(l, "Haraç karşılığında kabul et", "Barış için onlardan altın iste. Güçlüysen kabul ederler; değilse ilişki bozulur.");
            Opt(l, "Reddet", "İlişki -5.");
            l.defaultOption = 2;
        }

        public static void BuyBack(SaveData data, RivalState r, string cityId, string cityName, int price)
        {
            var l = New(data, LetterKind.BuyBack, r.id, cityName + " hakkında", LetterStyle.Compose(r.id, r.id == "sand"
                ? cityName + " artık sancağımı dalgalandırıyor; halkı da dilimizi öğrenmeye başladı. Ama ben inatçı bir adam değilim, pazarlık ehliyim.\n\n" +
                  Gold(price) + " karşılığında şehrin anahtarlarını sana iade ederim. Teklifim kumdaki ayak izi gibidir: rüzgâr çıkmadan değerlendir."
                : cityName + " burçlarında artık bizim bayrağımız dalgalanıyor. Savaşta alınanı savaşta geri vermek âdettir; ama halkın çile çekmesin diye sana bir yol açıyorum.\n\n" +
                  Gold(price) + " öde, şehrin anahtarları sana dönsün."), 3f);
            l.cityId = cityId; l.cityName = cityName; l.amount = price;
            Opt(l, "Şehri satın al  (" + Gold(price) + ")", "Şehir geri döner (vali ve takviyesiz). İlişki +5.", price);
            Opt(l, "Reddet", "Şehri savaşarak geri almalısın.");
            l.defaultOption = 1;
        }

        public static void Ransom(SaveData data, RivalState r, List<string> captives, string cityName)
        {
            int price = 70 * captives.Count;
            var l = New(data, LetterKind.Ransom, r.id, "Esirler", LetterStyle.Compose(r.id, r.id == "sand"
                ? cityName + " önünde düşen yiğitlerinden " + captives.Count + " bölük zindanlarımda. Hepsi sağ, ama zindan ekmeği pahalıdır.\n\n" +
                  "Bölük başına 70 altın, toplam " + Gold(price) + ". Öde, askerlerin evlerine dönsün. Ödemezsen kum fırtınası izlerini siler."
                : cityName + " önünde teslim olan " + captives.Count + " bölüğünün askerleri esirimdir. Onlara onurlu davranıyoruz, çünkü cesurca dövüştüler.\n\n" +
                  "Fidyeleri " + Gold(price) + ". Yiğitlerini geri istersen hazinenin kapısını aç; istemezsen kuzeyin madenlerinde kazma sallarlar."), 3f);
            l.cityName = cityName; l.amount = price; l.extra = string.Join(",", captives);
            Opt(l, "Fidyeyi öde  (" + Gold(price) + ")", captives.Count + " bölük ordusuna geri döner.", price);
            Opt(l, "Reddet", "Esirler kaybolur. Ordunun morali için kötü bir haber.");
            l.defaultOption = 1;
        }

        /// <summary>Gururlu kral, sınırına dayanan oyuncuyu uyarır.</summary>
        public static void BorderThreat(SaveData data, RivalState r, string cityName)
        {
            if (HasOpen(data, LetterKind.Threat, r.id)) return;
            var l = New(data, LetterKind.Threat, r.id, "Uyarı", LetterStyle.Compose(r.id,
                "Duydum ki sancağın artık " + cityName + " burçlarında dalgalanıyormuş. O şehir benim sınır taşımın dibindedir.\n\n" +
                "Bir adım daha atarsan kuzeyin bütün kışı senin üzerine iner. Haddini bil, sınırını koru; yoksa ben korurum."), 3f);
            l.cityName = cityName;
            Opt(l, "Özür dile", "İlişki +8. Ama halkın seni zayıf bulur (huzursuzluk +4).");
            Opt(l, "Meydan oku", "İlişki -12. Halkın gururlanır (huzursuzluk -4).");
            l.defaultOption = 1;
        }

        public static void Reply(SaveData data, string rivalId, string title, string body)
        {
            New(data, LetterKind.Reply, rivalId, title, LetterStyle.Compose(rivalId, body), 0f);
        }

        static bool HasOpen(SaveData data, LetterKind kind, string from)
        {
            foreach (var l in data.letters) if (l.kind == kind && l.from == from && l.NeedsAnswer) return true;
            return false;
        }

        // ------------------------------------------------------------------ saray olayları

        /// <summary>Öğretici "mektubu cevapla" adımına gelince cevap bekleyen bir vezir mektubu yoksa bir tane yazar.</summary>
        public static bool EnsureTutorialLetter(SaveData data)
        {
            foreach (var x in data.letters) if (x.from == "court" && x.NeedsAnswer) return false;
            var l = New(data, LetterKind.Caravan, "court", "Tüccar kervanı",
                LetterStyle.Compose("court", "Doğudan gelen bir tüccar heyeti huzurunuza çıkmak için kapıda bekliyor. İpek, baharat ve tuz yüklü bir kervan hazırlamışlar; sermaye arıyorlar.\n\n" +
                "100 altın yatırırsak kısa zamanda iki katını getireceklerine sakallarının üstüne yemin ediyorlar. Bu ilk alışverişte yol güvenli; size yalnızca karar vermek düşer."), 10f);
            Opt(l, "Yatırım yap  (100 altın)", "8 dakika sonra 200 altın döner.", 100);
            Opt(l, "Geç", "Bir şey olmaz.");
            l.defaultOption = 1;
            l.extra = "tutorial";
            return true;
        }

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
                        LetterStyle.Compose("court", city.name + " ambarlarından gelen haber acıdır: buğday bitti, değirmenler sustu. Analar çocuklarına suda ekmek kırıntısı kaynatıyor; " +
                        "pazarda bir somun için bıçaklar çekildi.\n\nHalk saray kapısında \"Hükümdarımız bizi unuttu mu?\" diye feryat ediyor. Vaktinde yetişmezsek açlık öfkeye, öfke isyana döner."), 5f);
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
                        LetterStyle.Compose("court", g.name + " valisi " + st.governor.name + " hakkında kulağıma fısıltılar geliyor. Hazineye akması gereken vergilerin bir kısmının " +
                        "onun konağına aktığı, hanımının boynunda yeni incilerin parladığı söyleniyor.\n\nElimizde kesin delil yok; yalnızca dedikodu ve şüphe var. " +
                        "Soruşturma açmak hem masraflıdır hem de masumsa valiyi gücendirir."), 5f);
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
                        LetterStyle.Compose("court", "Doğudan gelen bir tüccar heyeti huzurunuza çıkmak için kapıda bekliyor. İpek, baharat ve tuz yüklü bir kervan hazırlamışlar; sermaye arıyorlar.\n\n" +
                        "200 altın yatırırsak kısa zamanda iki katını getireceklerine sakallarının üstüne yemin ediyorlar. Lakin yollar haydut dolu; nice kervan çölde kaybolup gitmiştir."), 2f);
                    Opt(l, "Yatırım yap  (200 altın)", "%70 ihtimalle 8 dakika sonra 400 altın; %30 ihtimalle hepsi kaybolur.", 200);
                    Opt(l, "Geç", "Bir şey olmaz.");
                    l.defaultOption = 1;
                    break;
                }
                case 3:
                {
                    var l = New(data, LetterKind.Plague, "court", "Salgın: " + city.name,
                        LetterStyle.Compose("court", city.name + " sokaklarında kara bir hastalık yayılıyor. Önce ateş, sonra öksürük, sonra sessizlik. Kapılara tebeşirle işaret konuyor, " +
                        "ölüler gece yarısı arabalarla şehir dışına taşınıyor.\n\nHekim göndermezsek salgın şehri kemirecek; kapıları kapatırsak ticaret durur. Karar sizindir."), 5f);
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
                        LetterStyle.Compose("court", "Zaferlerinizin şanı köy köy dolaşıyor; ozanlar meydanlarda destanınızı çalıp söylüyor. Halk bu sevinci bir şölenle kutlamak için izninizi bekliyor.\n\n" +
                        "Kuzu çevirmeleri, cambazlar, davul zurna... Küçük bir masraf, büyük bir gönül kazancıdır."), 2f);
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
                    Reputation.Change(data, -2f, "Mektup cevapsız kaldı: " + l.title);
                    if (!string.IsNullOrEmpty(res)) pending.messages.Add("Cevap verilmedi: " + res);
                }
            }

            // kervanlar
            for (int i = data.caravanTicks.Count - 1; i >= 0; i--)
            {
                if (now < data.caravanTicks[i]) continue;
                int amount = data.caravanAmounts[i];
                bool sure = amount < 0; // öğretici kervanı: her zaman döner
                amount = Math.Abs(amount);
                data.caravanTicks.RemoveAt(i);
                data.caravanAmounts.RemoveAt(i);
                if (sure || UnityEngine.Random.value < 0.7f)
                {
                    data.gold += amount;
                    Reply(data, "court", "Kervan döndü", "Müjdeler olsun! Doğuya giden kervan sağ salim döndü. Develer ipek ve baharat yerine altın keseleriyle geldi: hazineye " + Gold(amount) + " girdi.");
                }
                else Reply(data, "court", "Kervan kayıp", "Acı bir haber vermek zorundayım. Kervan çölün ortasında haydutlara yakalanmış; develer dağılmış, mallar yağmalanmış. Yatırdığımız altın kum olup gitti.");
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
                    LetterStyle.Compose("court", "Hazine sandıkları boşaldı. Askerlerin maaşı ödenemiyor; kışlalarda homurdanmalar başladı, bazı bölükler gece karanlığında firar ediyor.\n\n" +
                    "Orduyu küçültmek, vergiyi artırmak ya da valilerin maaşlarını kısmak gerekebilir. Aksi hâlde saray kapısına aç askerler dayanır."), 0f);
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
                        Reputation.Change(data, -5f, "Haraç ödedin");
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
                        float chance = 0.35f + r.relation / 200f + Reputation.DiplomacyBonus();
                        if (UnityEngine.Random.value < chance)
                        {
                            r.awaitingAnswer = false;
                            r.relation += 3f;
                            r.greed *= 1.1f;
                            r.attackTimer = 10f + UnityEngine.Random.Range(0f, 5f);
                            res = "Pazarlık tuttu: " + RivalAI.KingFull(r.id) + " yarı fiyata razı oldu";
                            Reputation.Change(data, -3f, "Yarı fiyatına da olsa haraç ödedin");
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
                        if (UnityEngine.Random.value < 0.55f + r.relation / 300f + Reputation.DiplomacyBonus())
                        {
                            r.attackTarget = "";
                            r.attackFrom = "";
                            r.power += r.attackPower * 0.5f;
                            r.relation += 4f;
                            res = RivalAI.KingFull(r.id) + " altını kabul etti, ordusu geri çekildi";
                            Reputation.Change(data, -3f, "Savaştan altınla kurtuldun");
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
                        Reputation.Change(data, 2f, "Savaş ilanına meydan okudun");
                    }
                    break;

                case LetterKind.PeaceOffer:
                    if (r == null) { res = ""; break; }
                    if (option == 0)
                    {
                        MakePeace(r, 15f);
                        r.relation += 10f;
                        res = RivalAI.Get(r.id).name + " ile 15 dakikalık barış imzalandı";
                        Reputation.Change(data, 2f, "Barış imzaladın");
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
                        data.Conquer(l.cityId, false);
                        data.State(l.cityId).unrest = 50f;
                        data.pendingConquestAnim = "";
                        r.relation += 5f;
                        r.power += 20f;
                        pending.cityChanged.Add(l.cityId);
                        res = l.cityName + " geri satın alındı";
                        Reputation.Change(data, -2f, "Şehrini altınla geri aldın");
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
                        Reputation.Change(data, 2f, "Askerlerini esaretten kurtardın");
                    }
                    else { res = "Esirler kayboldu"; Reputation.Change(data, -3f, "Askerlerini esarette bıraktın"); }
                    break;

                case LetterKind.Threat:
                    if (r == null) { res = ""; break; }
                    if (option == 0) { r.relation += 8f; AllUnrest(data, 4f); res = "Özür diledin. Halkın bunu zayıflık saydı"; Reputation.Change(data, -3f, "Tehdit karşısında özür diledin"); }
                    else { r.relation -= 12f; AllUnrest(data, -4f); res = "Meydan okudun. " + RivalAI.KingFull(r.id) + " bunu unutmayacak"; }
                    break;

                case LetterKind.Famine:
                    if (st == null || !data.Holds(st)) { res = ""; break; }
                    if (option == 0) { st.unrest = Mathf.Max(0f, st.unrest - 20f); res = l.cityName + " halkı doydu"; Reputation.Change(data, 2f, "Aç halkı doyurdun"); }
                    else if (option == 1) { st.tax = TaxLevel.Low; st.unrest = Mathf.Max(0f, st.unrest - 5f); res = l.cityName + " vergisi düşürüldü"; }
                    else { st.unrest = Mathf.Min(100f, st.unrest + 15f); res = l.cityName + " halkı aç ve öfkeli (huzursuzluk +15)"; Reputation.Change(data, -3f, "Aç halkı görmezden geldin"); }
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
                        data.caravanAmounts.Add(l.extra == "tutorial" ? -200 : 400);
                        res = "Kervan yola çıktı, 8 dakika sonra haber gelecek";
                    }
                    else res = "Kervana yatırım yapılmadı";
                    break;

                case LetterKind.Plague:
                    if (st == null || !data.Holds(st)) { res = ""; break; }
                    if (option == 0) { res = "Hekimler salgını durdurdu"; Reputation.Change(data, 2f, "Salgına hekim gönderdin"); }
                    else if (option == 1) { SetIncomeMod(st, 0f, 4f); res = l.cityName + " karantinada (4 dk gelir yok)"; }
                    else { SetIncomeMod(st, 0.5f, 10f); st.unrest = Mathf.Min(100f, st.unrest + 10f); res = "Salgın " + l.cityName + " şehrini kasıp kavuruyor"; Reputation.Change(data, -2f, "Salgını görmezden geldin"); }
                    break;

                case LetterKind.Festival:
                    if (option == 0) { AllUnrest(data, -10f); res = "Şenlik gönülleri kazandı (huzursuzluk -10)"; Reputation.Change(data, 3f, "Halka şenlik düzenledin"); }
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
                ? "Gönderdiğin sandığı açtırdım; altının parıltısı gözlerimi okşadı. Komşuluğumuz bal gibi tatlanıyor. Böyle devam edersen kervanlarım da kılıçlarım da sana dostça bakar."
                : "Hediyeni aldım ve elçine soframda yer verdim. Krallığım cömertliği unutmaz; ama bil ki gurur altınla satın alınmaz.");
            return def.name + " ile ilişki +" + Mathf.RoundToInt(gain);
        }

        public static string ProposePeace(SaveData data, RivalState r)
        {
            if (r.AtPeace) return "Zaten barıştasınız";
            if (data.gold < PeaceCost) return "Yeterli altın yok";
            data.gold -= PeaceCost;
            var def = RivalAI.Get(r.id);
            float chance = 0.25f + r.relation / 120f + (PlayerStrength(data) > r.power ? 0.2f : 0f) + (def.trait == RivalAI.KingTrait.Proud ? -0.05f : 0f) + Reputation.DiplomacyBonus();
            if (r.Attacking) chance -= 0.2f;
            if (UnityEngine.Random.value < chance)
            {
                MakePeace(r, 15f);
                if (r.Attacking) { r.power += r.attackPower * 0.5f; r.attackTarget = ""; r.attackFrom = ""; }
                r.relation += 5f;
                Reply(data, r.id, "Barış", "Elçini dinledim; sözleri yerindeydi. Anlaşma süresince kılıçlarımız kınında, sancaklarımız sarılı kalacak. Ahdimi bozan ben olmayacağım.");
                Reputation.Change(data, 2f, "Barış imzaladın");
                return def.name + " barışı kabul etti (15 dk)";
            }
            r.relation -= 5f;
            Reply(data, r.id, "Barış reddedildi", "Elçin boş elle dönüyor. Barışın vakti ve şartları benim soframa aittir, senin elçinin heybesine değil. İstediğimde konuşuruz.");
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
                Reply(data, r.id, "Haraç", "Gücüne boyun eğiyorum... şimdilik. Hazinemden " + Gold(pay) + " çıkarıp elçinin atına yükledim. Bu günü unutmayacağım; tarih de unutmayacak.");
                Reputation.Change(data, 3f, def.name + " sana haraç ödedi");
                return def.name + " " + Gold(pay) + " haraç ödedi";
            }
            r.relation -= 25f;
            r.attackTimer = Mathf.Min(r.attackTimer, 1f);
            Reply(data, r.id, "Küstahlık", "Benden haraç mı istiyorsun? Elçinin sakalını kestirip geri gönderiyorum. Cevabımı mürekkeple değil, ordumla yazacağım.");
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
                Reputation.Change(data, -12f, "Yeminini bozdun");
                foreach (var o in data.rivals) if (o != r) o.relation -= 10f;
            }
            Reply(data, r.id, "Savaş", broke
                ? "Yeminini bozdun. Kutsal ahdi ayaklar altına aldın. Bundan sonra senin sözün rüzgârda savrulan tozdur; ne ben inanırım ne de başka bir taç sahibi."
                : "Demek savaş istiyorsun. Öyle olsun. Davullar çalsın, demirciler kılıç dövsün; sınırında görüşeceğiz.");
            return broke ? def.name + " ile barış bozuldu! Halkın huzursuz (+12)" : def.name + " ile savaştasın";
        }
    }
}
