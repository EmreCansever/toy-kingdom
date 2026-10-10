using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Küçük görsel efektler: kıvılcımlar, zıplama, sarsılma, ekran yazısı. Koddan çağrılır.</summary>
    public class FX : MonoBehaviour
    {
        static FX _runner;
        static FX Runner
        {
            get
            {
                if (_runner == null) _runner = new GameObject("FX").AddComponent<FX>();
                return _runner;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Runner.StartCoroutine(routine);

        /// <summary>Bir noktadan yukarı saçılıp sönen parıltılar.</summary>
        public static void Sparkles(Vector3 pos, Color color, int count = 14, float spread = 0.9f, int order = 30500)
        {
            var art = GameArt.Instance;
            Sprite s = art != null ? (art.star != null ? art.star : art.circle) : null;
            if (s == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Spark");
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.color = Color.Lerp(color, Color.white, UnityEngine.Random.value * 0.4f);
                sr.sortingOrder = order;
                float size = UnityEngine.Random.Range(0.1f, 0.22f);
                go.transform.localScale = Vector3.one * size;
                Vector2 dir = UnityEngine.Random.insideUnitCircle;
                Vector3 vel = new Vector3(dir.x * spread * 1.6f, 1.2f + UnityEngine.Random.value * 1.6f * spread, 0f);
                Run(SparkRoutine(go.transform, sr, vel, 0.7f + UnityEngine.Random.value * 0.5f, size));
            }
        }

        static IEnumerator SparkRoutine(Transform t, SpriteRenderer sr, Vector3 vel, float life, float size)
        {
            float time = 0f;
            Color c = sr.color;
            while (time < life && t != null)
            {
                time += Time.deltaTime;
                vel += Vector3.down * 3.2f * Time.deltaTime;
                t.position += vel * Time.deltaTime;
                t.Rotate(0f, 0f, 360f * Time.deltaTime);
                float k = time / life;
                t.localScale = Vector3.one * size * (1f - k * 0.6f);
                sr.color = new Color(c.r, c.g, c.b, 1f - k * k);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        /// <summary>Nesneyi kısa bir an büyütüp eski boyutuna döndürür.</summary>
        public static Coroutine Punch(Transform t, float amount = 0.25f, float duration = 0.35f) => Run(PunchRoutine(t, amount, duration));

        static IEnumerator PunchRoutine(Transform t, float amount, float duration)
        {
            if (t == null) yield break;
            Vector3 baseScale = t.localScale;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = time / duration;
                float s = 1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
                t.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        /// <summary>Nesneyi yerinde sarsar.</summary>
        public static Coroutine Shake(Transform t, float amount = 0.08f, float duration = 0.5f) => Run(ShakeRoutine(t, amount, duration));

        static IEnumerator ShakeRoutine(Transform t, float amount, float duration)
        {
            if (t == null) yield break;
            Vector3 origin = t.position;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = 1f - time / duration;
                t.position = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * amount * k);
                yield return null;
            }
            if (t != null) t.position = origin;
        }

        /// <summary>Ekranın ortasında büyüyerek beliren büyük yazı.</summary>
        public static void Banner(string text, Color color, float hold = 1.6f, string sub = null)
        {
            var canvas = UIFactory.Canvas("Banner", 50);
            var box = UIFactory.Frame(canvas.transform, "Box", new Vector2(980f, sub != null ? 230f : 170f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, sub != null ? 230f : 170f));
            var t = UIFactory.Text(box.transform, "Text", text, 92f, color, TextAlignmentOptions.Center, true);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(940f, 130f));
            if (sub != null)
            {
                var st = UIFactory.Text(box.transform, "Sub", sub, 36f, UIFactory.BrownDark, TextAlignmentOptions.Center, true);
                UIFactory.Place(st.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(940f, 60f));
            }
            Run(BannerRoutine(canvas.gameObject, box.rectTransform, hold));
        }

        static IEnumerator BannerRoutine(GameObject root, RectTransform box, float hold)
        {
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            float time = 0f;
            while (time < 0.35f && box != null)
            {
                time += Time.deltaTime;
                float k = time / 0.35f;
                float s = k < 0.7f ? Mathf.Lerp(0.2f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
                box.localScale = Vector3.one * s;
                group.alpha = Mathf.Clamp01(k * 2f);
                yield return null;
            }
            yield return new WaitForSeconds(hold);
            time = 0f;
            while (time < 0.4f && root != null)
            {
                time += Time.deltaTime;
                group.alpha = 1f - time / 0.4f;
                yield return null;
            }
            if (root != null) Destroy(root);
        }

        /// <summary>Kule görsellerini sırayla yeni renge çevirir; her birinde zıplama ve parıltı.</summary>
        public static Coroutine FlipTowers(System.Collections.Generic.IList<SpriteRenderer> towers, Func<int, Sprite> spriteFor,
            Color sparkColor, float interval = 0.18f, Action done = null)
            => Run(FlipRoutine(towers, spriteFor, sparkColor, interval, done));

        static IEnumerator FlipRoutine(System.Collections.Generic.IList<SpriteRenderer> towers, Func<int, Sprite> spriteFor,
            Color sparkColor, float interval, Action done)
        {
            for (int i = 0; i < towers.Count; i++)
            {
                var sr = towers[i];
                if (sr == null) continue;
                var s = spriteFor(i);
                if (s != null) sr.sprite = s;
                Punch(sr.transform, 0.22f, 0.3f);
                Sparkles(sr.transform.position + Vector3.up * 0.9f, sparkColor, 8, 0.6f);
                yield return new WaitForSeconds(interval);
            }
            done?.Invoke();
        }
    }
}
