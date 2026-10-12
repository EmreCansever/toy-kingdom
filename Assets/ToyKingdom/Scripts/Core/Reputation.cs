using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Hükümdarın itibarı (0-100, başlangıç 50). Zaferler, adil yönetim ve sözünde durmak artırır;
    /// isyanı son anda parayla bastırmak, haraç ödemek, yemin bozmak, şehir kaybetmek düşürür.
    /// Etkileri: halkın huzuru, valilerin sadakati, vali adaylarının kalitesi, rakip kralların tavrı ve isyanların gücü.
    /// </summary>
    public static class Reputation
    {
        public const float Start = 50f;

        static readonly List<KeyValuePair<float, string>> pending = new List<KeyValuePair<float, string>>();

        /// <summary>İtibarı değiştirir; değişiklik haber olarak bildirilir.</summary>
        public static void Change(SaveData d, float delta, string reason)
        {
            if (d == null || Mathf.Approximately(delta, 0f)) return;
            float before = d.reputation;
            // zirvede kalmak zordur: yüksek itibarda kazanç azalır, kayıp büyür ("yüksekten düşen sert düşer")
            if (reason != null && d.reputation > 50f)
            {
                float k = (d.reputation - 50f) / 100f;
                delta *= delta > 0f ? 1f - k : 1f + k;
            }
            d.reputation = Mathf.Clamp(d.reputation + delta, 0f, 100f);
            if (!string.IsNullOrEmpty(reason)) pending.Add(new KeyValuePair<float, string>(delta, reason));
            if (Tier(before) != Tier(d.reputation))
                pending.Add(new KeyValuePair<float, string>(delta * 10f, "Artık halk seni \"" + Name(d.reputation) + "\" olarak anıyor"));
        }

        /// <summary>Birikmiş itibar haberleri (değişim, açıklama).</summary>
        public static List<KeyValuePair<float, string>> Take()
        {
            var l = new List<KeyValuePair<float, string>>(pending);
            pending.Clear();
            return l;
        }

        static int Tier(float r) => r < 20f ? 0 : r < 40f ? 1 : r < 60f ? 2 : r < 80f ? 3 : 4;

        public static string Name(float r)
        {
            switch (Tier(r))
            {
                case 0: return "Zalim";
                case 1: return "Güvenilmez";
                case 2: return "Saygın";
                case 3: return "Adil";
                default: return "Efsanevi";
            }
        }

        public static Color Color(float r) =>
            r < 20f ? UIFactory.Hex("FF5E4D") : r < 40f ? UIFactory.Hex("FFB443") : r < 60f ? UIFactory.Hex("E8E1D0") : r < 80f ? UIFactory.Hex("A8D45A") : UIFactory.Hex("FFD36E");

        // ------------------------------------------------------------------ etkiler

        static float Rep => SaveSystem.Data != null ? SaveSystem.Data.reputation : Start;

        /// <summary>Halkın huzursuzluğuna dakikalık etki: itibarlı hükümdara halk sabreder.</summary>
        public static float UnrestEffect() => (50f - Rep) * 0.012f;

        /// <summary>
        /// Valilerin sadakatine dakikalık etki: kötü şöhret sadakati aşındırır, ama iyi şöhret sadakat satın almaz
        /// (yoksa isyan hiç çıkmaz, itibar da hiç düşmez).
        /// </summary>
        public static float LoyaltyEffect() => Mathf.Min(0f, (Rep - 50f) * 0.006f);

        /// <summary>
        /// Rakip kralların ilişkisinin kendiliğinden döndüğü denge. Saygın hükümdar biraz daha iyi karşılanır,
        /// ama efsaneleşen hükümdarı krallar kıskanır ve ondan korkar.
        /// </summary>
        public static float RelationBaseline()
        {
            float r = Rep;
            return -10f + Mathf.Min(r - 50f, 15f) * 0.6f - Mathf.Max(0f, r - 70f) * 0.8f;
        }

        /// <summary>Yüksek itibar kendiliğinden korunmaz: zamanla 50'ye doğru unutulur (dakikalık değişim).</summary>
        public static float Decay(float r) => r > 50f ? -Mathf.Min(r - 50f, (r - 50f) * 0.004f + 0.03f) : 0f;

        /// <summary>Barış ve pazarlık tekliflerinin kabul şansına katkı.</summary>
        public static float DiplomacyBonus() => (Rep - 50f) / 250f;

        /// <summary>Vali adaylarının güç ve sadakat düzeltmesi.</summary>
        public static int CandidatePowerBonus() => Rep >= 70f ? 1 : 0;
        public static float CandidateLoyaltyBonus() => (Rep - 50f) * 0.3f;

        /// <summary>Kötü şöhretli hükümdara karşı isyanlar daha güçlü olur.</summary>
        public static int RebelBonus() => Rep < 30f ? 1 : 0;

        /// <summary>Yeni fethedilen şehrin ilk huzursuzluğuna ek.</summary>
        public static float ConquestUnrest() => (50f - Rep) * 0.3f;

        public static string Effects()
        {
            string sign(float v) => (v >= 0f ? "+" : "") + v.ToString("0.0");
            return "Halk huzuru " + sign(-UnrestEffect()) + "/dk · vali sadakati " + sign(LoyaltyEffect()) + "/dk · krallarla ilişki dengesi " + Mathf.RoundToInt(RelationBaseline()) +
                   (CandidatePowerBonus() > 0 ? " · güçlü vali adayları" : "") + (RebelBonus() > 0 ? " · isyanlar daha güçlü" : "") +
                   (Rep > 70f ? " · krallar seni kıskanıyor" : "") + (Rep > 50f ? " · şöhret zamanla unutulur" : "");
        }
    }
}
