using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Oyunun sonu. Zafer: tüm rakip krallıklar diz çöker. Yenilgi: başkent düşer ya da saray darbesi
    /// (hazine uzun süre derin borçta ya da itibar dipte kalırsa saray seni tahttan indirir).
    /// </summary>
    public static class Endgame
    {
        public const int None = 0, Won = 1, Lost = 2, WonContinuing = 3;
        public const float CrisisLimit = 5f;   // darbeye kadar dakika
        public const int DebtLimit = -300;

        public static bool InCrisis(SaveData d) => d.gold < DebtLimit || d.reputation <= 2f;

        /// <summary>Durumu kontrol eder; oyun yeni bittiyse true döner. message: uyarı (varsa).</summary>
        public static bool Tick(SaveData d, int minutes, bool online, out string message)
        {
            message = null;
            if (d.gameOver != None) return false;

            if (d.rivals.Count > 0 && d.rivals.TrueForAll(r => !r.alive))
            {
                d.gameOver = Won;
                d.endReason = "Kum Sultanlığı da Buz Krallığı da diz çöktü. Ada artık tek bir tacın altında: seninkinin.";
                return true;
            }
            if (d.capitalLost)
            {
                d.gameOver = Lost;
                d.endReason = "Düşman orduları başkentin kapılarını kırdı. Taç başka bir başın üstünde.";
                return true;
            }

            // saray darbesi: kriz sürerse
            if (InCrisis(d))
            {
                if (!online || minutes <= 0) return false;
                bool first = d.crisisMinutes <= 0f;
                d.crisisMinutes += minutes;
                if (d.crisisMinutes >= CrisisLimit)
                {
                    d.gameOver = Lost;
                    d.endReason = d.gold < DebtLimit
                        ? "Maaşını alamayan muhafızlar saraya yürüdü. Vezir kapıyı onlara kendi eliyle açtı."
                        : "Halk da soylular da senden bıktı. Saray darbesiyle tahttan indirildin.";
                    return true;
                }
                if (first || Mathf.FloorToInt(d.crisisMinutes) != Mathf.FloorToInt(d.crisisMinutes - minutes))
                    message = "Sarayda darbe hazırlığı! " + Mathf.CeilToInt(CrisisLimit - d.crisisMinutes) + " dakika içinde " +
                              (d.gold < DebtLimit ? "hazineyi borçtan kurtar" : "itibarını yükselt") + ", yoksa tahttan indirileceksin";
            }
            else if (d.crisisMinutes > 0f)
            {
                d.crisisMinutes = 0f;
                message = "Darbe tehlikesi geçti. Saray sakinleşti";
            }
            return false;
        }

        /// <summary>Rakiplerin kaçı yıkıldı (zafer ilerlemesi).</summary>
        public static int Fallen(SaveData d)
        {
            int n = 0;
            foreach (var r in d.rivals) if (!r.alive) n++;
            return n;
        }
    }
}
