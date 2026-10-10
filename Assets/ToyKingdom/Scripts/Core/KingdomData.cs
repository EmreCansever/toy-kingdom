using System;

namespace ToyKingdom
{
    public enum GovernorTrait { Loyal, Ambitious, Just, Cruel, Merchant }
    public enum TaxLevel { Low, Normal, High }
    public enum RebelState { None, People, Governor }
    public enum Building { Market, Barracks, Walls, Temple }

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
        /// <summary>Sahibi: "player" ya da ileride rakip krallık kimliği.</summary>
        public string owner = "player";
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
}
