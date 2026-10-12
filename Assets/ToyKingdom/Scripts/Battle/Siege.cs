using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Kuşatma savunmasını sertleştirir: kaleyi büyütür, savunucuları güçlendirir, kale önüne gizli mayınlar ve
    /// kule tepelerine toplar yerleştirir. Kale verisinin çalışma anı kopyası üzerinde çalışır; kayıtlı veri değişmez.
    /// </summary>
    public static class Siege
    {
        // ------------------------------------------------------------------ kale verisi

        const string HardTag = "_H";
        public static bool IsHardened(CastleData c) => c != null && c.name.EndsWith(HardTag);

        /// <summary>
        /// Kademeye göre güçlendirilmiş kale kopyası. 0: tarafsız ve asi şehirler (normal, ilk fetihler rahat),
        /// 1: rakip krallık şehirleri (zor), 2: rakip başkentleri (en zor ama aşılabilir).
        /// </summary>
        public static CastleData Harden(CastleData original, int tier = 0)
        {
            if (IsHardened(original)) return original;
            tier = Mathf.Clamp(tier, 0, 2);
            var c = Object.Instantiate(original);
            c.name = original.name + HardTag;
            c.siegeTier = tier;
            int d = Mathf.Clamp(original.difficulty, 1, 5);
            c.terrain = PickTerrain(original, tier);

            // boyut: başkentler ve zor kaleler büyür
            int grow = tier == 0 ? (d >= 3 ? 1 : 0) : tier == 1 ? 1 : 2;
            c.width = Mathf.Clamp(original.width + grow, 4, 9);
            c.depth = Mathf.Clamp(original.depth + grow, 4, 9);
            c.midTowers = original.midTowers || d + tier >= 3;
            c.keep = original.keep || tier >= 1;

            float[] gate = { 1.1f, 1.3f, 1.5f };
            c.gateHealth = original.gateHealth * gate[tier];
            float[] str = { 0.6f, 0.75f, 0.9f };
            c.defenderStrength = Mathf.Max(original.defenderStrength, str[tier] + 0.08f * d);
            c.wallArchers = original.wallArchers + tier + (tier == 0 ? 0 : d / 2);

            c.garrison = new List<GarrisonEntry>();
            foreach (var g in original.garrison) if (g != null) c.garrison.Add(new GarrisonEntry { unit = g.unit, squads = g.squads });
            // rakip şehirlerine bir, başkentlere iki bölük daha
            if (tier >= 1 && c.garrison.Count > 0 && c.garrison[0].unit != null) c.garrison.Add(new GarrisonEntry { unit = c.garrison[0].unit, squads = tier });
            if (original.reinforcement != null) c.reinforcementCount = original.reinforcementCount + (tier == 2 ? 1 : 0);

            if (original.towerData != null)
            {
                float[] dmg = { 1.05f, 1.2f, 1.3f }, hp = { 1.1f, 1.25f, 1.4f };
                var t = Object.Instantiate(original.towerData);
                t.damage = original.towerData.damage * dmg[tier];
                t.fireInterval = original.towerData.fireInterval * (tier == 0 ? 1f : 0.9f);
                t.maxHealth = original.towerData.maxHealth * hp[tier];
                t.range = original.towerData.range + 0.2f * tier;
                c.towerData = t;
            }
            return c;
        }

        /// <summary>
        /// Kalenin arazisi: ilk (en kolay) kaleler düz, başkentler hendekli; diğerleri şehre göre sabit:
        /// hendek, nehir kenarı, tepe ya da düz.
        /// </summary>
        static int PickTerrain(CastleData c, int tier)
        {
            if (tier == 2) return CastleData.TerrainMoat;
            if (tier == 0 && c.difficulty <= 1) return CastleData.TerrainPlain;
            int h = 7;
            foreach (char ch in c.cityId ?? "") h = h * 31 + ch;
            int pick = Mathf.Abs(h) % 4;
            if (c.biome == Biome.Desert && pick == CastleData.TerrainRiver) pick = CastleData.TerrainHill; // çölde nehir az
            return pick;
        }

        /// <summary>Gizli mayınlar: tarafsız kalelerde yalnızca zorlarında birkaç tane, rakiplerde daha çok.</summary>
        public static int MineCount(CastleData c)
        {
            int d = c.difficulty;
            switch (c.siegeTier)
            {
                case 0: return d >= 3 ? d - 1 : 0;
                case 1: return 2 + d;
                default: return 4 + d;
            }
        }

        /// <summary>Kule topları: tarafsızlarda yok, rakip şehirlerinde en fazla iki, başkentlerde en fazla üç.</summary>
        public static int CannonCount(CastleData c)
        {
            int d = c.difficulty;
            switch (c.siegeTier)
            {
                case 0: return 0;
                case 1: return Mathf.Clamp(d - 2, 1, 2);
                default: return Mathf.Clamp(d - 1, 2, 3);
            }
        }

        // ------------------------------------------------------------------ yerleştirme

        public static void Build(BattleField field, CastleData c)
        {
            var rnd = new System.Random(c.cityId.GetHashCode() ^ 0x77a1);

            // toplar: öne bakan kulelerin tepesinde
            var towers = new List<Tower>(field.Towers);
            towers.Sort((a, b) => (a.cell.x + a.cell.y).CompareTo(b.cell.x + b.cell.y));
            int cannons = CannonCount(c);
            for (int i = 0; i < cannons && i < towers.Count; i++) Cannon.Create(towers[i], c);

            // mayınlar: kalenin önünde ve yanlarında, konuşlanma şeridinin dışında
            int mines = MineCount(c);
            int guard = 0;
            var placed = new List<Vector3>();
            while (placed.Count < mines && guard++ < 400)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float r = 2.4f + (float)rnd.NextDouble() * 4.2f;
                Vector3 p = field.CastleCenter + new Vector3(Mathf.Cos(a) * r * 1.3f, Mathf.Sin(a) * r * 0.75f, 0f);
                if (p.x > field.CastleCenter.x + 1.5f) continue; // kalenin arkasına değil
                if (!field.Playable.Contains(new Vector2(p.x, p.y)) || field.IsBlocked(p) || field.InDeployZone(p)) continue;
                bool ok = true;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 1.2f) { ok = false; break; }
                if (!ok) continue;
                placed.Add(p);
                Mine.Create(p, c);
            }
        }

        // ------------------------------------------------------------------ çizimler

        static Sprite cannonSprite, moundSprite;

        public static Sprite CannonSprite
        {
            get
            {
                if (cannonSprite != null) return cannonSprite;
                int w = 64, h = 48;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                Color iron = new Color(0.2f, 0.21f, 0.24f), hi = new Color(0.42f, 0.44f, 0.5f), wood = new Color(0.45f, 0.27f, 0.12f), woodDark = new Color(0.28f, 0.16f, 0.07f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                        Color col = Color.clear;
                        // namlu: sola doğru uzanan, hafif yukarı bakan silindir
                        float axis = 0.62f + (0.6f - u) * 0.25f;
                        float half = Mathf.Lerp(0.13f, 0.17f, u);
                        if (u > 0.08f && u < 0.78f && Mathf.Abs(v - axis) < half)
                            col = Color.Lerp(iron, hi, Mathf.Clamp01(1f - Mathf.Abs(v - axis - half * 0.4f) / half));
                        if (u > 0.06f && u < 0.12f && Mathf.Abs(v - axis) < half * 1.25f) col = iron; // ağız halkası
                        // kızak
                        if (u > 0.4f && u < 0.9f && v > 0.22f && v < 0.42f) col = wood;
                        // tekerlek
                        float dx = u - 0.6f, dy = (v - 0.25f) * (float)h / w;
                        float rr = Mathf.Sqrt(dx * dx + dy * dy);
                        if (rr < 0.16f) col = rr > 0.12f ? woodDark : (rr < 0.04f ? iron : wood);
                        px[y * w + x] = col;
                    }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                cannonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 100f);
                return cannonSprite;
            }
        }

        /// <summary>Gömülü mayının üstündeki hafif toprak tümseği.</summary>
        public static Sprite MoundSprite
        {
            get
            {
                if (moundSprite != null) return moundSprite;
                int w = 48, h = 24;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.5f;
                        float d = Mathf.Sqrt(dx * dx * 4f + dy * dy * 4f);
                        float a = Mathf.Clamp01((1f - d) * 3f);
                        Color c = Color.Lerp(new Color(0.36f, 0.25f, 0.14f), new Color(0.5f, 0.37f, 0.22f), Mathf.Clamp01(0.5f - dy));
                        // ortada küçük demir tetik
                        if (Mathf.Abs(dx) < 0.04f && Mathf.Abs(dy) < 0.12f) c = new Color(0.25f, 0.25f, 0.28f);
                        px[y * w + x] = new Color(c.r, c.g, c.b, a);
                    }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                moundSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return moundSprite;
            }
        }

        /// <summary>Patlama: parlama halkası, kıvılcım ve duman.</summary>
        public static void Explosion(Vector3 at, float radius)
        {
            Sfx.Play("sfx_cannon", 0.75f, 0.08f, 0.1f, 0.7f);
            FX.Sparkles(at + Vector3.up * 0.15f, new Color(1f, 0.55f, 0.15f), 22, 1.1f);
            FX.Sparkles(at + Vector3.up * 0.3f, new Color(0.3f, 0.28f, 0.26f), 12, 0.7f);
            var art = GameArt.Instance;
            if (art == null || art.circle == null) return;
            var go = new GameObject("Blast");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.circle;
            sr.sortingOrder = 24000;
            FX.Run(BlastRoutine(go.transform, sr, radius));
            var bm = BattleManager.Instance;
            if (bm != null && bm.Cam != null) FX.Shake(bm.Cam.transform, 0.06f, 0.25f);
        }

        static System.Collections.IEnumerator BlastRoutine(Transform t, SpriteRenderer sr, float radius)
        {
            float time = 0f;
            while (time < 0.35f && t != null)
            {
                time += Time.deltaTime;
                float k = time / 0.35f;
                t.localScale = new Vector3(radius * 2.6f * k, radius * 1.5f * k, 1f);
                sr.color = new Color(1f, 0.75f, 0.3f, 0.75f * (1f - k));
                yield return null;
            }
            if (t != null) Object.Destroy(t.gameObject);
        }

        /// <summary>Alan hasarı: yarıçap içindeki oyuncu askerleri (zemindekiler).</summary>
        public static void Damage(Vector3 at, float radius, float damage)
        {
            foreach (var s in Soldier.All.ToArray())
            {
                if (s == null || !s.IsAlive || s.Team != Team.Player || s.IsElevated) continue;
                Vector3 d = s.Ground - at;
                d.y *= 1.6f; // izometrik basıklık
                if (d.magnitude <= radius) s.TakeDamage(damage * (1f - 0.4f * d.magnitude / radius), DamageType.Fire);
            }
        }
    }

    /// <summary>Gizli mayın: üstüne basan oyuncu askeri patlatır.</summary>
    public class Mine : MonoBehaviour
    {
        float damage;
        bool armed = true;

        public static Mine Create(Vector3 at, CastleData c)
        {
            var go = new GameObject("Mine");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Siege.MoundSprite;
            sr.color = new Color(1f, 1f, 1f, 0.55f); // dikkatli bakan görür
            sr.sortingOrder = Iso.Order(at, -3);
            go.transform.localScale = Vector3.one * 0.9f;
            var m = go.AddComponent<Mine>();
            m.damage = 55f * c.defenderStrength;
            return m;
        }

        void Update()
        {
            if (!armed) return;
            var bm = BattleManager.Instance;
            if (bm == null || bm.State != BattleState.Playing) return;
            Vector3 p = transform.position;
            for (int i = 0; i < Soldier.All.Count; i++)
            {
                var s = Soldier.All[i];
                if (s == null || !s.IsAlive || s.Team != Team.Player || s.IsElevated) continue;
                Vector3 d = s.Ground - p;
                d.y *= 1.6f;
                if (d.sqrMagnitude > 0.3f * 0.3f) continue;
                armed = false;
                Siege.Explosion(p, 0.9f);
                Siege.Damage(p, 0.95f, damage);
                bm.ReportMine();
                Destroy(gameObject);
                return;
            }
        }
    }

    /// <summary>Kule tepesindeki top: en kalabalık oyuncu kümesine gülle atar. Düşeceği yer önceden kırmızıyla görünür.</summary>
    public class Cannon : MonoBehaviour
    {
        Tower tower;
        float cooldown;
        float damage;
        const float Range = 7f;
        const float Interval = 5.5f;
        SpriteRenderer sr;

        public static Cannon Create(Tower tower, CastleData c)
        {
            var go = new GameObject("Cannon");
            go.transform.position = tower.TopGround + Vector3.up * (Tower.TopElevation + 0.05f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Siege.CannonSprite;
            var tsr = tower.GetComponent<SpriteRenderer>();
            sr.sortingOrder = (tsr != null ? tsr.sortingOrder : 0) + 6;
            go.transform.localScale = Vector3.one * 0.9f;
            var cn = go.AddComponent<Cannon>();
            cn.tower = tower;
            cn.sr = sr;
            cn.damage = 34f * c.defenderStrength;
            cn.cooldown = 3f + Random.value * 2f;
            tower.Destroyed += _ => cn.Disable();
            return cn;
        }

        void Disable()
        {
            if (sr != null) sr.color = new Color(0.35f, 0.33f, 0.33f, 0.8f);
            enabled = false;
        }

        void Update()
        {
            var bm = BattleManager.Instance;
            if (bm == null || bm.State != BattleState.Playing || tower == null || tower.IsDestroyed) return;
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            // en kalabalık kümeyi hedefle
            Soldier best = null;
            int bestCount = 0;
            Vector3 from = tower.TopGround;
            foreach (var s in Soldier.All)
            {
                if (s == null || !s.IsAlive || s.Team != Team.Player || s.IsElevated) continue;
                if ((s.Ground - from).magnitude > Range) continue;
                int n = 0;
                foreach (var o in Soldier.All)
                    if (o != null && o.IsAlive && o.Team == Team.Player && (o.Ground - s.Ground).sqrMagnitude < 0.8f) n++;
                if (n > bestCount) { bestCount = n; best = s; }
            }
            if (best == null) { cooldown = 0.5f; return; }
            cooldown = Interval;
            Cannonball.Fire(transform.position + Vector3.up * 0.15f, best.Ground, damage);
            Sfx.Play("sfx_cannon", 0.9f, 0.06f, 0.2f);
            FX.Sparkles(transform.position + Vector3.up * 0.2f, new Color(0.9f, 0.9f, 0.85f), 8, 0.4f);
            FX.Punch(transform, 0.18f, 0.2f);
            if (sr != null) sr.flipX = best.Ground.x > transform.position.x;
        }
    }

    /// <summary>Gülle: kavisle uçar, düşeceği yeri işaretler, alan hasarı verir.</summary>
    public class Cannonball : MonoBehaviour
    {
        Vector3 start, end;
        float t, duration, damage;
        SpriteRenderer marker;

        public static void Fire(Vector3 from, Vector3 target, float damage)
        {
            var art = GameArt.Instance;
            var go = new GameObject("Cannonball");
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art != null ? art.circle : null;
            sr.color = new Color(0.12f, 0.12f, 0.14f);
            sr.sortingOrder = 25500;
            go.transform.localScale = Vector3.one * 0.16f;
            var b = go.AddComponent<Cannonball>();
            b.start = from;
            b.end = target;
            b.damage = damage;
            b.duration = 1.25f;

            // düşeceği yer: kaçmak için kısa bir uyarı
            if (art != null && art.circle != null)
            {
                b.marker = new GameObject("Impact").AddComponent<SpriteRenderer>();
                b.marker.sprite = art.circle;
                b.marker.transform.position = target;
                b.marker.transform.localScale = new Vector3(2.0f, 1.15f, 1f);
                b.marker.color = new Color(1f, 0.2f, 0.15f, 0.25f);
                b.marker.sortingOrder = -14000;
            }
        }

        void Update()
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (2.2f * 4f * k * (1f - k));
            if (marker != null) marker.color = new Color(1f, 0.2f, 0.15f, 0.2f + 0.3f * Mathf.PingPong(Time.time * 6f, 1f));
            if (k < 1f) return;
            Siege.Explosion(end, 0.85f);
            Siege.Damage(end, 0.9f, damage);
            if (marker != null) Destroy(marker.gameObject);
            Destroy(gameObject);
        }
    }
}
