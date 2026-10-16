using System;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Vezirin görevleri: oyunun sistemlerini sırayla tanıtan, ödüllü ilk adımlar.
    /// Her görev kayıttaki duruma bakarak kendiliğinden tamamlanır.
    /// </summary>
    public static class Tutorial
    {
        public class Step
        {
            public string title;
            public string how;
            public int reward;
            /// <summary>Vurgulanacak arayüz düğmesi ("ArmyButton", "TradeButton"...), yoksa null.</summary>
            public string highlight;
            public Func<SaveData, bool> done;
        }

        // olay bayrakları (kayıtta): oyuncunun yaptığı ama durumdan anlaşılmayan şeyler
        public const string FlagBought = "bought", FlagAnswered = "answered", FlagDiplomacy = "diplomacy", FlagIntro = "intro",
            FlagGift = "gift", FlagCalm = "calm";

        /// <summary>Görev listesinin sürümü: eski kayıtlar yeni eklenen görevlere göre kaydırılır.</summary>
        const int Version = 2;

        static int Owned(SaveData d)
        {
            int n = 0;
            foreach (var id in d.conqueredCities) if (!d.IsRebel(id)) n++;
            return n;
        }

        static bool Any(SaveData d, Func<CityState, bool> f)
        {
            foreach (var c in d.cities) if (d.Holds(c) && f(c)) return true;
            return false;
        }

        public static readonly Step[] Steps =
        {
            new Step { title = "İlk fetih", reward = 150,
                how = "Parlayan halkayı takip et: yolla bağlı şehre dokun, kahraman oraya yürüsün. Saldır'a bas; savaşta bir bölük kartı seç ve parmağınla kapıya rota çiz.",
                done = d => Owned(d) >= 1 },
            new Step { title = "Bir vali ata", reward = 100,
                how = "Fethettiğin şehre dokun ve adaylardan birini vali yap. Valisiz şehir hızla huzursuzlanır; güçlü vali halkı yatıştırır ama hırslı olur.",
                done = d => Any(d, c => c.hasGovernor) },
            new Step { title = "Valinin gönlünü kazan", reward = 80,
                how = "Valinin sadakati zamanla düşer; sıfıra inerse isyan eder. Valili şehrine dokun ve hediye gönder. Valiyi azletmek diğer valileri de ürkütür.",
                done = d => d.HasFlag(FlagGift) },
            new Step { title = "Halkı yatıştır", reward = 80,
                how = "Huzursuzluk 100'e varırsa halk ayaklanır. Şehrine dokun: ziyafet ver, vergiyi düşür ya da Huzur kararnamesi çıkar.",
                done = d => d.HasFlag(FlagCalm) || Any(d, c => c.cityId != "home" && (c.tax == TaxLevel.Low || c.policy == CityPolicy.Peace)) },
            new Step { title = "Ordunu büyüt", reward = 80, highlight = "ArmyButton",
                how = "Sağ üstteki Ordu düğmesinden bir bölük satın al. Her bölük maaş alır, ordunu hazinene göre büyüt.",
                done = d => d.HasFlag(FlagBought) },
            new Step { title = "Şehrine yatırım yap", reward = 120,
                how = "Kendi şehrine dokun ve Pazar, Kışla, Sur ya da Tapınak'tan birini kur.",
                done = d => Any(d, c => c.market + c.barracks + c.walls + c.temple > 0) },
            new Step { title = "Kararname çıkar", reward = 80,
                how = "Şehir panelinde Ticaret, Askeri ya da Huzur kararnamesinden birini seç. Her biri bir şey verir, bir şey alır.",
                done = d => Any(d, c => c.policy != CityPolicy.None) },
            new Step { title = "Yola devriye koy", reward = 100, highlight = "TradeButton",
                how = "Şehirlerin arasında kervanlar gider gelir; yol güvensizse eşkıya keser. Ticaret'e bas, bir yol seç ve + ile bir bölüğü devriyeye ver. Güvenli yol isyanı da azaltır.",
                done = d => d.roads.Exists(r => r.active && r.PatrolCount() > 0) },
            new Step { title = "Vezirin mektubunu cevapla", reward = 80, highlight = "LettersButton",
                how = "Mektuplar'dan cevap bekleyen bir mektubu aç ve bir seçenek seç. Cevapsız kalan mektup itibarını düşürür.",
                done = d => d.HasFlag(FlagAnswered) },
            new Step { title = "Komşu krallarla tanış", reward = 100, highlight = "DiplomacyButton",
                how = "Diplomasi'den bir krala hediye gönder ya da barış öner. İlişkiler saldırıları ve haraç isteklerini belirler.",
                done = d => d.HasFlag(FlagDiplomacy) },
            new Step { title = "Krallığını büyüt", reward = 300,
                how = "Üç şehre ulaş. Bundan sonrası senin: itibarını koru, halkını doyur, rakip başkentleri düşür.",
                done = d => Owned(d) >= 3 },
        };

        public static Step Current(SaveData d) => d.tutorialStep >= 0 && d.tutorialStep < Steps.Length ? Steps[d.tutorialStep] : null;

        /// <summary>Geçerli görev tamamlandıysa ödülü verir, sıradakine geçer ve haberi döndürür (her çağrıda en fazla bir görev).</summary>
        public static string Check(SaveData d)
        {
            if (d.tutorialVersion < Version)
            {
                // ikinci sürümde 3. ve 4. sıraya iki görev eklendi: eski kayıt ilerideyse kaydır
                if (d.tutorialStep >= 2) d.tutorialStep += 2;
                d.tutorialVersion = Version;
            }
            if (!d.HasFlag(FlagIntro))
            {
                d.SetFlag(FlagIntro);
                Court.Reply(d, "court", "Hoş geldiniz, efendim",
                    "Tahtınız kutlu, ömrünüz uzun olsun! Ataların kılıcı artık sizin belinizde, tacı sizin başınızdadır. Krallığımız küçüktür; ama hırsımız dağlardan yüce.\n\n" +
                    "Bu yaşlı kulunuz size yol gösterecek: sol taraftaki görevleri sırayla yerine getirin, her biri hazineye altın kazandıracaktır.\n\n" +
                    "Lakin unutmayın: aç kalan halk ayaklanır, hırslanan vali ihanet eder, güneyin sultanı ile kuzeyin kraliçesi ise zayıf düştüğünüz günü kollar.");
            }
            var s = Current(d);
            // mektup adımında cevaplanacak bir mektup hep olsun
            if (s != null && s.title == "Vezirin mektubunu cevapla" && !s.done(d)) Court.EnsureTutorialLetter(d);
            if (s == null || !s.done(d)) return null;
            d.gold += s.reward;
            d.tutorialStep++;
            return "Görev tamamlandı: " + s.title + "  +" + s.reward + " altın";
        }
    }
}
