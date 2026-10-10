using System;
using UnityEngine;

namespace ToyKingdom
{
    public enum CastleColor { Blue, Green, Red, Wood }

    /// <summary>Haritanın bölgeleri: yeşil vadi, çöl, kış.</summary>
    public enum Biome { Spring, Desert, Winter }

    /// <summary>Bir bölgenin zemin, yol, nehir ve doğa sprite'ları.</summary>
    [Serializable]
    public class BiomeArt
    {
        public Sprite ground;
        public Sprite[] details;
        public Sprite[] trees;
        public Sprite[] stones;
        [Tooltip("Kenar maskesine göre yol parçaları. İndeks = maske (NE=1, SE=2, SW=4, NW=8), 0 kullanılmaz")]
        public Sprite[] roadByMask = new Sprite[16];
        [Tooltip("Kenar maskesine göre nehir parçaları")]
        public Sprite[] riverByMask = new Sprite[16];
        [Tooltip("Nehir NE-SW, yol NW-SE")]
        public Sprite bridgeRoadNWSE;
        [Tooltip("Nehir NW-SE, yol NE-SW")]
        public Sprite bridgeRoadNESW;

        public Sprite Road(int mask) =>
            roadByMask != null && mask > 0 && mask < roadByMask.Length && roadByMask[mask] != null ? roadByMask[mask] : ground;

        public Sprite River(int mask) =>
            riverByMask != null && mask > 0 && mask < riverByMask.Length && riverByMask[mask] != null ? riverByMask[mask] : ground;

        public Sprite Pick(Sprite[] arr, int index) =>
            arr != null && arr.Length > 0 ? arr[Mathf.Abs(index) % arr.Length] : null;
    }

    /// <summary>
    /// Oyunun kullandığı tüm sprite'lar tek yerde. Assets/.../Resources/GameArt.asset olarak durur
    /// ve çalışma anında Resources'tan yüklenir. Grafikleri değiştirmek için bu dosyadaki referansları değiştir.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyKingdom/Oyun grafikleri", fileName = "GameArt")]
    public class GameArt : ScriptableObject
    {
        [Header("Bölgeler")]
        public BiomeArt spring = new BiomeArt();
        public BiomeArt desert = new BiomeArt();
        public BiomeArt winter = new BiomeArt();

        [Header("Köy")]
        public Sprite[] fieldTiles;
        public Sprite[] houses;
        public Sprite[] mills;
        public Sprite smoke;

        [Header("Kale")]
        [Tooltip("u ekseni boyunca uzanan duvar blokları (sağ-ön ve arka-sol duvarlar)")]
        public Sprite[] wallsAlongU;
        [Tooltip("v ekseni boyunca uzanan duvar blokları (sol-ön ve arka-sağ duvarlar)")]
        public Sprite[] wallsAlongV;
        [Tooltip("Dört yöne bağlanan duvar (iç kule altı)")]
        public Sprite wallCross;
        [Tooltip("Sol (GB) yüzünde kapı olan duvar")]
        public Sprite gateLeft;
        [Tooltip("Sağ (GD) yüzünde kapı olan duvar")]
        public Sprite gateRight;
        public Sprite[] towersBlue;
        public Sprite[] towersGreen;
        public Sprite[] towersRed;
        public Sprite[] towersWood;

        [Header("Karakter")]
        public CharacterAnimSet knight;

        [Header("Üretilmiş yardımcılar")]
        public Sprite white;
        public Sprite shadow;
        public Sprite arrow;
        public Sprite tileHighlight;
        public Sprite circle;
        public Sprite ladder;
        public Sprite star;

        public BiomeArt Biome(Biome b)
        {
            switch (b)
            {
                case ToyKingdom.Biome.Desert: return desert;
                case ToyKingdom.Biome.Winter: return winter;
                default: return spring;
            }
        }

        public Sprite[] Towers(CastleColor c)
        {
            switch (c)
            {
                case CastleColor.Blue: return towersBlue;
                case CastleColor.Green: return towersGreen;
                case CastleColor.Red: return towersRed;
                default: return towersWood;
            }
        }

        public Sprite Tower(CastleColor c, int variant)
        {
            var arr = Towers(c);
            if (arr == null || arr.Length == 0) return null;
            return arr[Mathf.Abs(variant) % arr.Length];
        }

        static GameArt _instance;
        public static GameArt Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameArt>("GameArt");
                    if (_instance == null) Debug.LogError("GameArt bulunamadı. ToyKingdom > Örnek içeriği oluştur menüsünü çalıştır.");
                }
                return _instance;
            }
        }
    }
}
