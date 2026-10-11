using UnityEngine;
using UnityEngine.UI;

namespace ToyKingdom
{
    /// <summary>Hükümdar portresi: krallık renginde madalyon, içinde şövalye figürü ve başında taç (koddan çizilir).</summary>
    public static class KingPortrait
    {
        static Sprite crown;

        /// <summary>Basit bir taç: bant, üç sivri uç, mücevherler.</summary>
        public static Sprite Crown
        {
            get
            {
                if (crown != null) return crown;
                int w = 96, h = 64;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                Color gold = new Color(1f, 0.8f, 0.25f), dark = new Color(0.6f, 0.38f, 0.05f), ruby = new Color(0.85f, 0.15f, 0.2f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                        Color c = Color.clear;
                        bool band = v < 0.36f && u > 0.08f && u < 0.92f;
                        // üç uç: üçgenler
                        bool spike = false;
                        foreach (float cx in new[] { 0.2f, 0.5f, 0.8f })
                        {
                            float half = 0.15f * (1f - (v - 0.3f) / 0.62f);
                            if (v >= 0.3f && v < 0.92f && Mathf.Abs(u - cx) < half) spike = true;
                        }
                        if (band || spike) c = Color.Lerp(dark, gold, 0.35f + 0.65f * v);
                        // kenar koyuluğu
                        if (band && (v < 0.06f || v > 0.3f)) c = dark;
                        // mücevherler
                        foreach (float cx in new[] { 0.3f, 0.5f, 0.7f })
                            if ((new Vector2((u - cx) * 1.5f, v - 0.18f)).magnitude < 0.08f) c = ruby;
                        foreach (float cx in new[] { 0.2f, 0.5f, 0.8f })
                            if ((new Vector2((u - cx) * 1.5f, v - 0.9f)).magnitude < 0.07f) c = Color.white;
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                crown = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return crown;
            }
        }

        public static RectTransform Create(Transform parent, string rivalId, float size)
        {
            var art = GameArt.Instance;
            var def = RivalAI.Get(rivalId);
            var rim = UIFactory.Panel(parent, "Portrait", UIFactory.Darken(def.color, 0.55f), art != null ? art.circle : null);
            rim.rectTransform.sizeDelta = new Vector2(size, size);
            rim.raycastTarget = false;
            var bg = UIFactory.Panel(rim.transform, "Bg", Color.Lerp(def.color, Color.white, 0.25f), art != null ? art.circle : null);
            UIFactory.Stretch(bg.rectTransform, size * 0.05f);
            bg.raycastTarget = false;
            bg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // figür: şövalyenin önden duruşu
            Sprite fig = null;
            if (art != null && art.knight != null && art.knight.idle != null && art.knight.idle.IsValid) fig = art.knight.idle.Get(0, 0);
            var body = UIFactory.Panel(bg.transform, "King", Color.Lerp(def.color, Color.white, 0.55f), fig);
            body.preserveAspect = true;
            body.raycastTarget = false;
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            // yakın plan: baş ve omuzlar madalyonu doldursun
            brt.offsetMin = new Vector2(-size * 0.6f, -size * 0.95f);
            brt.offsetMax = new Vector2(size * 0.6f, size * 0.35f);

            var c = UIFactory.Panel(rim.transform, "Crown", Color.white, Crown);
            c.preserveAspect = true;
            c.raycastTarget = false;
            UIFactory.Place(c.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -size * 0.04f), new Vector2(size * 0.5f, size * 0.33f));
            return rim.rectTransform;
        }
    }
}
