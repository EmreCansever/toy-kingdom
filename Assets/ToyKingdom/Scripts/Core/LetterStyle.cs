using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Mektupların orta çağ üslubu: her gönderenin kendine has hitabı, kapanışı ve mühür altı imzası.
    /// Aynı mektup türünün birkaç farklı yazılışı vardır; oyuncu hep aynı cümleleri okumaz.
    /// </summary>
    public static class LetterStyle
    {
        static string Pick(params string[] options) => options[Random.Range(0, options.Length)];

        /// <summary>Hitap: mektubun ilk satırı.</summary>
        public static string Salute(string from)
        {
            switch (from)
            {
                case "court":
                    return Pick("Devletlü, inayetlü Efendim Hazretleri,",
                                "Şevketlü Efendim, gölgeniz eksik olmasın,",
                                "Saadetlü Efendim Hazretleri,");
                case "sand":
                    return Pick("Kızıl Kumların gölgesinden, kuzeydeki komşu hükümdara selam olsun.",
                                "Vahaların efendisinden, toprak beyi komşusuna:",
                                "Ey yeşil vadilerin hükümdarı! Kumların sultanı sana seslenir.");
                case "frost":
                    return Pick("Kar ve demirle yoğrulmuş Buz Tahtı'ndan, güneydeki hükümdara:",
                                "Kuzeyin buzlu burçlarından sana sesleniyorum, komşu hükümdar.",
                                "Ayaz Hatun'dan, sınırın öte yakasındaki taç sahibine:");
                default:
                    return "Ey hükümdar,";
            }
        }

        /// <summary>Kapanış ve imza (mühür altı).</summary>
        public static string Close(string from)
        {
            switch (from)
            {
                case "court":
                    return Pick("Emir ve ferman Efendimizindir.", "Takdir, yüce katınızındır.", "Gerisini Efendimizin engin aklına bırakırım.") +
                           "\n\nKulunuz,\n<b>Vezir Lala Paşa</b>";
                case "sand":
                    return Pick("Kumların sabrı vardır; benimki yoktur.", "Altın konuşur, gerisi susar.", "Rüzgâr dönmeden kararını ver.") +
                           "\n\nMühür ve imza:\n<b>Sultan Kara Davud</b>\n<size=85%>Kızıl Kumların Sultanı, Vahaların Hâkimi</size>";
                case "frost":
                    return Pick("Kar erir, sözüm erimez.", "Kuzeyin rüzgârı soğuktur ama yalan söylemez.", "Buz kırılır, yeminim kırılmaz.") +
                           "\n\nMühür ve imza:\n<b>Kraliçe Ayaz Hatun</b>\n<size=85%>Buz Tahtı'nın Hanımı, Kuzey Rüzgârlarının Efendisi</size>";
                default:
                    return "";
            }
        }

        /// <summary>Mektubun tamamı: hitap, gövde, kapanış.</summary>
        public static string Compose(string from, string body) => Salute(from) + "\n\n" + body + "\n\n" + Close(from);

        /// <summary>Mühürdeki harf.</summary>
        public static string SealLetter(string from) => from == "court" ? "V" : from == "sand" ? "D" : from == "frost" ? "A" : "?";
    }
}
