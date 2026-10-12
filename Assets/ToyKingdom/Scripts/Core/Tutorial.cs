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
        public const string FlagBought = "bought", FlagAnswered = "answered", FlagDiplomacy = "diplomacy", FlagIntro = "intro";

        static int Owned(SaveData d)
        {
            int n = 0;
            foreach (var id in d.conqueredCities) if (!d.IsRebel(id)) n++;
            return n;
        }

        static bool Any(SaveData d, Func<CityState, bool> f)
        {
            foreach (var c in d.cities) if ((c.owner == "player" || c.cityId == "home") && f(c)) return true;
            return false;
        }

        public static readonly Step[] Steps =
        {
            new Step { title = "İlk fetih", reward = 150,
                how = "Başkentine yolla bağlı bir şehre dokun, kahramanın oraya yürüsün. Saldır'a bas, bölük kartı seç ve parmağınla kaleye rota çiz.",
                done = d => Owned(d) >= 1 },
            new Step { title = "Bir vali ata", reward = 100,
                how = "Fethettiğin şehre dokun ve adaylardan birini vali yap. Valisiz şehir hızla huzursuzlanır.",
                done = d => Any(d, c => c.hasGovernor) },
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
                how = "Ticaret düğmesine bas, bir yol seç ve + ile bir bölüğü devriyeye ver. Güvenli yolda kervanlar soyulmaz.",
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
            if (!d.HasFlag(FlagIntro))
            {
                d.SetFlag(FlagIntro);
                Court.Reply(d, "court", "Hoş geldiniz, efendim",
                    "Tahtınız kutlu, ömrünüz uzun olsun! Ataların kılıcı artık sizin belinizde, tacı sizin başınızdadır. Krallığımız küçüktür; ama hırsımız dağlardan yüce.\n\n" +
                    "Bu yaşlı kulunuz size yol gösterecek: sol taraftaki görevleri sırayla yerine getirin, her biri hazineye altın kazandıracaktır.\n\n" +
                    "Lakin unutmayın: aç kalan halk ayaklanır, hırslanan vali ihanet eder, güneyin sultanı ile kuzeyin kraliçesi ise zayıf düştüğünüz günü kollar.");
            }
            var s = Current(d);
            if (s == null || !s.done(d)) return null;
            d.gold += s.reward;
            d.tutorialStep++;
            return "Görev tamamlandı: " + s.title + "  +" + s.reward + " altın";
        }
    }
}
