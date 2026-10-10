#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ToyKingdom.EditorTools
{
    /// <summary>ToyKingdom/Art altındaki görselleri otomatik olarak doğru sprite ayarlarıyla içe aktarır.</summary>
    public class ArtImportRules : AssetPostprocessor
    {
        public const string MedievalPath = "Assets/ToyKingdom/Art/Medieval/";
        public const string CrusaderPath = "Assets/ToyKingdom/Art/Crusader/";

        void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            Apply(importer, assetPath);
        }

        /// <summary>Ayar değiştiyse true döner.</summary>
        public static bool Apply(TextureImporter importer, string path)
        {
            path = path.Replace('\\', '/');
            bool medieval = path.StartsWith(MedievalPath);
            bool crusader = path.StartsWith(CrusaderPath);
            if (!medieval && !crusader) return false;

            float ppu = medieval ? 100f : 125f;
            Vector2 pivot = medieval ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.255f);

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            bool changed = importer.textureType != TextureImporterType.Sprite
                           || s.spriteMode != (int)SpriteImportMode.Single
                           || !Mathf.Approximately(s.spritePixelsPerUnit, ppu)
                           || s.spriteAlignment != (int)SpriteAlignment.Custom
                           || (s.spritePivot - pivot).sqrMagnitude > 0.00001f
                           || s.mipmapEnabled
                           || s.spriteMeshType != SpriteMeshType.FullRect;
            if (!changed) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.ReadTextureSettings(s);
            s.spriteMode = (int)SpriteImportMode.Single;
            s.spritePixelsPerUnit = ppu;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.mipmapEnabled = false;
            s.alphaIsTransparency = true;
            s.filterMode = FilterMode.Bilinear;
            s.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(s);
            importer.textureCompression = medieval ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            return true;
        }
    }
}
#endif
