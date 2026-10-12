using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Birlik tecrübesi: savaştan sağ dönen bölük tecrübe kazanır, rütbesi yükseldikçe daha sert vurur,
    /// daha dayanıklı olur ve morali daha yüksek başlar. Ölen bölüğün tecrübesi de onunla gider.
    /// </summary>
    public static class Veterancy
    {
        static readonly int[] Thresholds = { 2, 5, 9 };
        static readonly string[] Names = { "Acemi", "Tecrübeli", "Kıdemli", "Usta" };
        static readonly float[] Mults = { 1f, 1.12f, 1.25f, 1.4f };

        public static int Rank(int xp)
        {
            int r = 0;
            foreach (int t in Thresholds) if (xp >= t) r++;
            return r;
        }

        public static string Name(int xp) => Names[Rank(xp)];
        public static float Mult(int xp) => Mults[Rank(xp)];
        public static float MoraleBonus(int xp) => 8f * Rank(xp);

        /// <summary>Rütbe nişanı (I, II, III); acemide boş.</summary>
        public static string Badge(int xp) { int r = Rank(xp); return r == 0 ? "" : r == 1 ? "I" : r == 2 ? "II" : "III"; }

        /// <summary>Savaş sonunda sağ dönen bölüğün kazandığı tecrübe.</summary>
        public static int Gain(bool victory, float survivalRatio) => 1 + (victory ? 1 : 0) + (survivalRatio >= 0.9f ? 1 : 0);

        public static string Next(int xp)
        {
            int r = Rank(xp);
            return r >= Thresholds.Length ? "en yüksek rütbe" : (Thresholds[r] - xp) + " savaş puanı sonra " + Names[r + 1];
        }
    }
}
