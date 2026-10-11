using System;
using System.Collections.Generic;

namespace ToyKingdom
{
    public enum GovernorTrait { Loyal, Ambitious, Just, Cruel, Merchant }
    public enum TaxLevel { Low, Normal, High }
    public enum RebelState { None, People, Governor }
    public enum Building { Market, Barracks, Walls, Temple }
    /// <summary>Şehir kararnamesi: şehrin önceliği. Her biri bir şey verir, bir şey alır.</summary>
    public enum CityPolicy { None, Trade, Military, Peace }
    public enum Treaty { None, Peace }

    public enum LetterKind
    {
        // rakip krallardan
        TributeDemand, WarDeclaration, PeaceOffer, BuyBack, Ransom, Threat, Reply,
        // saraydan (vezir)
        Famine, Corruption, Caravan, Plague, Festival, Debt, Info
    }

    [Serializable]
    public class LetterOption
    {
        public string label;
        /// <summary>Seçeneğin olası sonucu (oyuncuya ipucu).</summary>
        public string hint;
        /// <summary>Gereken altın (yetmezse seçenek kapalı).</summary>
        public int cost;
    }

    /// <summary>Bir mektup: gönderen, içerik, cevap seçenekleri ve cevap süresi. Süre dolarsa varsayılan seçenek uygulanır.</summary>
    [Serializable]
    public class Letter
    {
        public int id;
        public LetterKind kind;
        /// <summary>Rakip kimliği ya da "court" (vezir).</summary>
        public string from;
        public string cityId;
        public string cityName;
        public float power;
        public string title;
        public string body;
        public int amount;
        public string extra;
        public List<LetterOption> options = new List<LetterOption>();
        /// <summary>Cevap verilmezse uygulanan seçenek (-1: yok, sadece bilgi).</summary>
        public int defaultOption = -1;
        public long createdTicks;
        public long expiresTicks;
        public bool read;
        public bool resolved;
        public int chosen = -1;
        public string result;

        public bool NeedsAnswer => !resolved && options.Count > 0;
    }

    [Serializable]
    public class Governor
    {
        public string name;
        public GovernorTrait trait;
        /// <summary>1-5. Güçlü vali halkı daha iyi yatıştırır ama isyan ederse bastırmak zorlaşır.</summary>
        public int power = 1;
        /// <summary>0-100. Sıfıra inerse vali isyan eder.</summary>
        public float loyalty = 60f;
    }

    /// <summary>Bir şehrin yönetim durumu. Oyuncu da ileride rakip krallıklar da aynı yapıyı kullanır.</summary>
    [Serializable]
    public class CityState
    {
        public string cityId;
        /// <summary>Sahibi: "" tarafsız, "player" oyuncu ya da rakip krallık kimliği ("sand", "frost").</summary>
        public string owner = "";
        /// <summary>Şehre takviye olarak bırakılan bölük sayısı (rakip saldırılarına karşı savunma).</summary>
        public int garrison;
        public CityPolicy policy = CityPolicy.None;
        /// <summary>Bir sonraki asker toplama zamanı (UTC ticks).</summary>
        public long levyReadyTicks;
        /// <summary>Geçici gelir çarpanı (salgın, yolsuzluk...) ve bitiş zamanı.</summary>
        public float incomeMod = 1f;
        public long incomeModUntil;
        public bool hasGovernor;
        public Governor governor = new Governor();
        public int market, barracks, walls, temple;
        public TaxLevel tax = TaxLevel.Normal;
        /// <summary>0-100 halk huzursuzluğu. 100 olunca halk ayaklanır.</summary>
        public float unrest = 40f;
        public RebelState rebel = RebelState.None;
        public int rebelPower;
        /// <summary>Aday listesini yenilemek için sayaç.</summary>
        public int candidateSeed;
        // uyarıların tekrar etmemesi için
        public bool warnedUnrest, warnedLoyalty;

        public int Level(Building b)
        {
            switch (b)
            {
                case Building.Market: return market;
                case Building.Barracks: return barracks;
                case Building.Walls: return walls;
                default: return temple;
            }
        }

        public void SetLevel(Building b, int level)
        {
            switch (b)
            {
                case Building.Market: market = level; break;
                case Building.Barracks: barracks = level; break;
                case Building.Walls: walls = level; break;
                default: temple = level; break;
            }
        }
    }

    /// <summary>Rakip krallığın durumu. Kayıtta tutulur.</summary>
    [Serializable]
    public class RivalState
    {
        public string id;
        public string capitalId;
        public bool alive = true;
        /// <summary>Ordu gücü: şehirlerinden gelen gelirle büyür, fetihlerde ve saldırılarda harcanır.</summary>
        public float power = 30f;
        /// <summary>Bir sonraki genişleme ve saldırıya kalan dakika.</summary>
        public float expandTimer = 6f;
        public float attackTimer = 14f;
        // devam eden saldırı
        public string attackTarget = "";
        public string attackFrom = "";
        public float attackPower;
        public long attackStartTicks;
        public long attackArriveTicks;

        public bool Attacking => !string.IsNullOrEmpty(attackTarget);

        // diplomasi
        /// <summary>-100 (düşman) ... +100 (dost).</summary>
        public float relation = -10f;
        public Treaty treaty = Treaty.None;
        public long treatyUntilTicks;
        /// <summary>Açgözlü kralların her ödenen haraçta büyüyen iştahı.</summary>
        public float greed = 1f;
        /// <summary>Haraç mektubu gönderildi, cevap bekleniyor (saldırı askıda).</summary>
        public bool awaitingAnswer;
        public float peaceOfferTimer = 12f;
        public long lastLetterTicks;

        public bool AtPeace => treaty == Treaty.Peace && DateTime.UtcNow.Ticks < treatyUntilTicks;
    }
}
