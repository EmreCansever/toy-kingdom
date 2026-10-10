using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// İzometrik ızgara yardımcıları. Izgara koordinatları (u, v):
    /// u arttıkça ekranda sağ-yukarı (NE), v arttıkça sol-yukarı (NW) gidilir.
    /// Tüm küp sprite'larının pivotu alt-orta (alt köşe) olarak içe aktarılır.
    /// </summary>
    public static class Iso
    {
        /// <summary>Bir hücrenin ekrandaki yarım genişliği (dünya birimi).</summary>
        public const float HalfW = 0.45f;
        /// <summary>Bir hücrenin ekrandaki yarım yüksekliği (dünya birimi).</summary>
        public const float HalfH = 0.26f;
        /// <summary>Zemin küpünün yan yüz yüksekliği: üst yüzey bu kadar yukarıdadır.</summary>
        public const float GroundTop = 0.44f;
        /// <summary>Duvar bloğunun yan yüz yüksekliği (blok üst üste dizerken).</summary>
        public const float WallLevel = 0.53f;

        /// <summary>Zemin küpünün konumu (alt köşesi).</summary>
        public static Vector3 CellBase(int u, int v) => new Vector3((u - v) * HalfW, (u + v) * HalfH, 0f);

        /// <summary>Zemin üst yüzeyindeki bir noktanın dünya konumu. Tam sayılar hücre merkezidir.</summary>
        public static Vector3 Surface(float u, float v) =>
            new Vector3((u - v) * HalfW, (u + v) * HalfH + GroundTop + HalfH, 0f);

        /// <summary>Bir hücrenin üstüne konacak blok (duvar, kule) için konum.</summary>
        public static Vector3 BlockBase(int u, int v, int level = 0) =>
            CellBase(u, v) + new Vector3(0f, GroundTop + level * WallLevel, 0f);

        /// <summary>Dünya noktasını sürekli ızgara koordinatına çevirir (zemin yüzeyinde varsayılır).</summary>
        public static Vector2 ToGrid(Vector3 p)
        {
            float a = p.x / HalfW;                          // u - v
            float b = (p.y - GroundTop - HalfH) / HalfH;    // u + v
            return new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
        }

        /// <summary>Derinlik (u + v). Büyük değer = ekranda daha arkada.</summary>
        public static float Depth(Vector3 p) => (p.y - GroundTop - HalfH) / HalfH;

        /// <summary>Zemin üstündeki her şey için çizim sırası.</summary>
        public static int Order(Vector3 surfacePoint, int bias = 0) =>
            -Mathf.RoundToInt(Depth(surfacePoint) * 20f) + bias;

        /// <summary>Hücre üstündeki blok için çizim sırası.</summary>
        public static int BlockOrder(int u, int v, int bias = 0) => -(u + v) * 20 + bias;

        /// <summary>Zemin küpleri her şeyin altında kalır.</summary>
        public static int GroundOrder(int u, int v) => -20000 - (u + v) * 2;

        /// <summary>Ekran yönünü 8 yönlü sprite indeksine çevirir (0=G, 1=GD, 2=D, 3=KD, 4=K, 5=KB, 6=B, 7=GB).</summary>
        public static int DirectionIndex(Vector2 screenDir)
        {
            if (screenDir.sqrMagnitude < 0.000001f) return 0;
            float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
            int idx = Mathf.RoundToInt(angle / 45f) + 2;
            return ((idx % 8) + 8) % 8;
        }
    }
}
