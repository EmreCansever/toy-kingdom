using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Savaş alanını CastleData'dan çalışma anında kurar. Yatay ekran için düzenlenmiştir:
    /// bölükler soldan çıkar, kale sağda durur. Zemin, kapıya giden yol, bloklardan kale, ok kuleleri,
    /// kapılar ve konuşlanma bölgesini oluşturur; yürünemez alan ve yardımcı konumları sağlar.
    /// </summary>
    public class BattleField : MonoBehaviour
    {
        /// <summary>Oynanabilir alan (ekran/dünya koordinatı, zemin yüzeyi).</summary>
        public static readonly Rect Playable = new Rect(-6.8f, 1.6f, 15.0f, 6.1f);
        /// <summary>Bu x değerinin solu konuşlanma bölgesidir.</summary>
        public const float DeployMaxX = -3.0f;

        public CastleData Castle { get; private set; }
        public List<CastleGate> Gates { get; } = new List<CastleGate>();
        public List<Tower> Towers { get; } = new List<Tower>();
        public Material SpriteMaterial { get; private set; }

        int cu0, cv0, cu1, cv1;
        readonly List<Vector2Int> defenderCells = new List<Vector2Int>();
        readonly List<Vector2> defenderDirs = new List<Vector2>();
        readonly List<Vector2Int> frontWallCells = new List<Vector2Int>();
        System.Random postRandom;
        GameArt art;
        Transform root;

        public void Build(CastleData castle)
        {
            Castle = castle;
            art = GameArt.Instance;
            root = new GameObject("Field").transform;
            root.SetParent(transform, false);

            int w = Mathf.Clamp(castle.width, 4, 7);
            int d = Mathf.Clamp(castle.depth, 4, 7);
            // kale merkezi ekranda sağda (u - v = 10, u + v = 16)
            cu0 = 13 - w / 2; cu1 = cu0 + w - 1;
            cv0 = 3 - d / 2; cv1 = cv0 + d - 1;

            int gv = cv0 + d / 2;   // ana kapı: sol-ön (SW) duvarın ortası, saldıranlara bakar
            int gu = cu0 + w / 2;   // ikinci kapı: sağ-ön (SE) duvarın ortası

            postRandom = new System.Random(castle.cityId.GetHashCode() ^ 0x2f1);
            BuildGround(gu, gv, castle.secondGate);
            BuildCastle(castle, gu, gv);
            BuildDeployZone();
        }

        // ------------------------------------------------------------------ zemin

        void BuildGround(int gu, int gv, bool second)
        {
            var road = new HashSet<Vector2Int>();
            for (int u = -16; u < cu0; u++) road.Add(new Vector2Int(u, gv));
            if (second) for (int v = -12; v < cv0; v++) road.Add(new Vector2Int(gu, v));

            var rnd = new System.Random(Castle.cityId.GetHashCode());
            for (int u = -16; u <= 30; u++)
            {
                for (int v = -14; v <= 20; v++)
                {
                    Vector3 basePos = Iso.CellBase(u, v);
                    if (basePos.x < -13f || basePos.x > 13f || basePos.y < -3.5f || basePos.y > 10.5f) continue;

                    var cell = new Vector2Int(u, v);
                    bool isRoad = road.Contains(cell);
                    Sprite s = art.grass;
                    if (isRoad)
                    {
                        int m = 0;
                        if (road.Contains(new Vector2Int(u + 1, v)) || (v == gv && u + 1 == cu0)) m |= 1;           // NE
                        if (road.Contains(new Vector2Int(u, v - 1)) || (second && u == gu && v - 1 < -12)) m |= 2;    // SE
                        if (road.Contains(new Vector2Int(u - 1, v)) || (v == gv && u - 1 < -16)) m |= 4;            // SW
                        if (road.Contains(new Vector2Int(u, v + 1)) || (second && u == gu && v + 1 == cv0)) m |= 8;   // NW
                        if (m == 0) m = 5;
                        if (art.roadByMask != null && m < art.roadByMask.Length && art.roadByMask[m] != null) s = art.roadByMask[m];
                    }
                    var sr = PlaceSprite(s, basePos, Iso.GroundOrder(u, v), "G");
                    if (SpriteMaterial == null) SpriteMaterial = sr.sharedMaterial;

                    bool inCastle = u >= cu0 && u <= cu1 && v >= cv0 && v <= cv1;
                    if (isRoad || inCastle) continue;

                    Vector3 surf = Iso.Surface(u, v);
                    bool inside = Playable.Contains(new Vector2(surf.x, surf.y));
                    if (art.grassDetails != null && art.grassDetails.Length > 0 && rnd.NextDouble() < 0.12)
                    {
                        float ox = (float)rnd.NextDouble() * 0.5f - 0.25f, oy = (float)rnd.NextDouble() * 0.4f - 0.2f;
                        var p = Iso.Surface(u + ox, v + oy);
                        PlaceSprite(art.grassDetails[rnd.Next(art.grassDetails.Length)], p, Iso.Order(p, -2), "D");
                    }
                    if (art.trees != null && art.trees.Length > 0 && rnd.NextDouble() < (inside ? 0.0 : 0.4))
                    {
                        var p = Iso.Surface(u + (float)rnd.NextDouble() * 0.3f - 0.15f, v + (float)rnd.NextDouble() * 0.3f - 0.15f);
                        PlaceSprite(art.trees[rnd.Next(art.trees.Length)], p + Vector3.down * 0.04f, Iso.Order(p, 3), "T");
                    }
                    else if (inside && art.stones != null && art.stones.Length > 0 && rnd.NextDouble() < 0.025)
                    {
                        var p = Iso.Surface(u, v);
                        PlaceSprite(art.stones[rnd.Next(art.stones.Length)], p + Vector3.down * 0.08f, Iso.Order(p, 1), "S");
                    }
                }
            }
        }

        void BuildDeployZone()
        {
            if (art.tileHighlight == null) return;
            for (int u = -16; u <= 30; u++)
                for (int v = -14; v <= 20; v++)
                {
                    Vector3 p = Iso.Surface(u, v);
                    if (!InDeployZone(p)) continue;
                    var sr = PlaceSprite(art.tileHighlight, p, -15000, "Z");
                    sr.color = new Color(1f, 1f, 1f, 0.17f);
                }
        }

        // ------------------------------------------------------------------ kale

        void BuildCastle(CastleData c, int gu, int gv)
        {
            var towerCells = new HashSet<Vector2Int>
            {
                new Vector2Int(cu0, cv0), new Vector2Int(cu1, cv0), new Vector2Int(cu0, cv1), new Vector2Int(cu1, cv1)
            };
            if (c.midTowers)
            {
                // ön duvarlarda, kapıların iki yanına
                if (gv - 2 > cv0) towerCells.Add(new Vector2Int(cu0, gv - 2));
                if (gv + 2 < cv1) towerCells.Add(new Vector2Int(cu0, gv + 2));
                if (gu - 2 > cu0) towerCells.Add(new Vector2Int(gu - 2, cv0));
                if (gu + 2 < cu1) towerCells.Add(new Vector2Int(gu + 2, cv0));
            }

            var rnd = new System.Random(c.cityId.GetHashCode() ^ 0x5bd1);
            for (int u = cu0; u <= cu1; u++)
            {
                for (int v = cv0; v <= cv1; v++)
                {
                    bool perimeter = u == cu0 || u == cu1 || v == cv0 || v == cv1;
                    var cell = new Vector2Int(u, v);
                    if (!perimeter)
                    {
                        BuildInterior(c, u, v, rnd);
                        continue;
                    }

                    if (towerCells.Contains(cell))
                    {
                        var t = PlaceSprite(art.Tower(c.color, rnd.Next(9)), Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1), "Tower");
                        var tower = t.gameObject.AddComponent<Tower>();
                        tower.Init(c.towerData, cell, art);
                        Towers.Add(tower);
                        continue;
                    }

                    bool isMainGate = u == cu0 && v == gv;
                    bool isSecondGate = c.secondGate && v == cv0 && u == gu;
                    if (isMainGate || isSecondGate)
                    {
                        var gs = PlaceSprite(isMainGate ? art.gateLeft : art.gateRight, Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1), "Gate");
                        var gate = gs.gameObject.AddComponent<CastleGate>();
                        Vector2 outward = isMainGate ? new Vector2(-1f, 0f) : new Vector2(0f, -1f);
                        var front = isMainGate ? new Vector2Int(u - 1, v) : new Vector2Int(u, v - 1);
                        gate.Init(c.gateHealth, Iso.Surface(front.x, front.y), outward, art);
                        Gates.Add(gate);
                        defenderCells.Add(front);
                        defenderDirs.Add(outward);
                        continue;
                    }

                    // u boyunca uzanan duvarlar (sağ-ön ve sol-arka) ile v boyunca uzananlar farklı bloklar
                    bool alongU = v == cv0 || v == cv1;
                    Sprite[] set = alongU ? art.wallsAlongU : art.wallsAlongV;
                    Sprite s = set != null && set.Length > 0 ? set[rnd.Next(set.Length)] : null;
                    PlaceSprite(s, Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1), "Wall");
                    if (u == cu0 || v == cv0) frontWallCells.Add(cell);
                }
            }
        }

        void BuildInterior(CastleData c, int u, int v, System.Random rnd)
        {
            int ku = (cu0 + cu1 + 1) / 2, kv = (cv0 + cv1 + 1) / 2;
            if (c.keep && u == ku && v == kv)
            {
                PlaceSprite(art.wallCross, Iso.BlockBase(u, v), Iso.BlockOrder(u, v, 1), "Keep");
                PlaceSprite(art.Tower(c.color, 4), Iso.BlockBase(u, v, 1), Iso.BlockOrder(u, v, 2), "KeepTower");
                return;
            }
            if (art.houses != null && art.houses.Length > 0 && rnd.NextDouble() < 0.5)
            {
                var p = Iso.Surface(u, v);
                PlaceSprite(art.houses[rnd.Next(art.houses.Length)], p + Vector3.down * 0.14f, Iso.Order(p, 3), "House");
            }
        }

        SpriteRenderer PlaceSprite(Sprite s, Vector3 pos, int order, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ sorgular

        /// <summary>Kale dörtgeni içinde mi (yürünemez)?</summary>
        public bool IsBlocked(Vector3 world)
        {
            Vector2 g = Iso.ToGrid(world);
            return g.x > cu0 - 0.5f && g.x < cu1 + 0.5f && g.y > cv0 - 0.5f && g.y < cv1 + 0.5f;
        }

        /// <summary>Noktayı oynanabilir alanın içine ve kalenin dışına iter.</summary>
        public Vector3 Constrain(Vector3 world)
        {
            world = ClampToPlayable(world);
            Vector2 g = Iso.ToGrid(world);
            const float margin = 0.62f;
            float minU = cu0 - margin, maxU = cu1 + margin, minV = cv0 - margin, maxV = cv1 + margin;
            if (g.x > minU && g.x < maxU && g.y > minV && g.y < maxV)
            {
                float dl = g.x - minU, dr = maxU - g.x, db = g.y - minV, dt = maxV - g.y;
                float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                if (m == dl) g.x = minU;
                else if (m == dr) g.x = maxU;
                else if (m == db) g.y = minV;
                else g.y = maxV;
                world = ClampToPlayable(Iso.Surface(g.x, g.y));
            }
            return world;
        }

        static Vector3 ClampToPlayable(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, Playable.xMin, Playable.xMax);
            p.y = Mathf.Clamp(p.y, Playable.yMin, Playable.yMax);
            p.z = 0f;
            return p;
        }

        public bool InDeployZone(Vector3 world) =>
            world.x <= DeployMaxX && Playable.Contains(new Vector2(world.x, world.y)) && !IsBlocked(world);

        public CastleGate NearestGate(Vector3 from)
        {
            CastleGate best = null;
            float bestD = float.MaxValue;
            foreach (var g in Gates)
            {
                if (g == null || g.IsDestroyed) continue;
                float d = (g.FrontPoint - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = g; }
            }
            return best;
        }

        /// <summary>Savunucu bölüklerin bekleyeceği nokta: kapıların önünde, sıra sıra.</summary>
        public Vector3 DefenderHome(int index)
        {
            if (defenderCells.Count == 0) return Iso.Surface(cu0 - 1.5f, cv0 - 1.5f);
            int gi = index % defenderCells.Count;
            int ring = index / defenderCells.Count;
            Vector2 cell = defenderCells[gi];
            Vector2 outward = defenderDirs[gi];
            Vector2 lateral = new Vector2(outward.y, outward.x);
            float side = ring % 2 == 0 ? 0f : (ring % 4 == 1 ? 1.4f : -1.4f);
            Vector2 g = cell + outward * (0.7f + (ring / 2) * 1.4f) + lateral * side;
            return Constrain(Iso.Surface(g.x, g.y));
        }

        public Vector3 GateSpawnPoint(int index)
        {
            if (Gates.Count == 0) return DefenderHome(0);
            return Gates[index % Gates.Count].FrontPoint;
        }


        // ------------------------------------------------------------------ savunma yerleşimi

        /// <summary>Sur üstündeki düşman okçuları için ön duvar hücreleri (kapı ve kule hariç).</summary>
        public List<Vector3> WallArcherPosts(int count)
        {
            var cells = new List<Vector2Int>(frontWallCells);
            // dengeli dağılsın: ön köşeden uzaklığa göre sırala ve aralıklı seç
            cells.Sort((a, b) => (a.x + a.y).CompareTo(b.x + b.y));
            var result = new List<Vector3>();
            if (cells.Count == 0 || count <= 0) return result;
            float step = cells.Count / (float)Mathf.Min(count, cells.Count);
            for (int i = 0; i < count && i < cells.Count; i++)
            {
                var c = cells[Mathf.Min(cells.Count - 1, Mathf.FloorToInt(i * step + step * 0.5f))];
                result.Add(Iso.Surface(c.x, c.y));
            }
            return result;
        }

        /// <summary>Surların üst yürüme yolunun zeminden yüksekliği.</summary>
        public const float WallTopElevation = 0.53f;

        /// <summary>
        /// Kale önündeki savunma bölgesinde birbirinden uzak, dağınık nöbet noktaları üretir.
        /// zone: 0 = ana kapı önü, 1 = sol-ön duvar boyu, 2 = sağ-ön duvar boyu, 3 = ön köşe açığı.
        /// </summary>
        public List<Vector3> GuardPosts(int zone, int count, bool nearWalls)
        {
            var posts = new List<Vector3>();
            int guard = 0;
            while (posts.Count < count && guard++ < 600)
            {
                // bölüğün askerleri iki komşu bölgeye bölünür: savunma kale önüne yayılır
                int z = (zone + posts.Count % 2) % 4;
                ZoneShape(z, nearWalls, out Vector2 center, out Vector2 extent);
                float a = (float)postRandom.NextDouble() * 2f - 1f;
                float b = (float)postRandom.NextDouble() * 2f - 1f;
                Vector3 p = Constrain(Iso.Surface(center.x + a * extent.x, center.y + b * extent.y));
                bool ok = true;
                foreach (var q in usedPosts) if ((q - p).sqrMagnitude < 0.56f) { ok = false; break; }
                if (ok || guard > 450)
                {
                    posts.Add(p);
                    usedPosts.Add(p);
                }
            }
            return posts;
        }

        readonly List<Vector3> usedPosts = new List<Vector3>();

        void ZoneShape(int zone, bool nearWalls, out Vector2 center, out Vector2 extent)
        {
            float gvMid = (cv0 + cv1) * 0.5f, guMid = (cu0 + cu1) * 0.5f;
            switch (zone)
            {
                case 0: center = new Vector2(cu0 - 2.2f, gvMid); extent = new Vector2(1.6f, 2.4f); break;
                case 1: center = new Vector2(cu0 - 2.6f, cv1 - 0.5f); extent = new Vector2(1.6f, 1.6f); break;
                case 2: center = new Vector2(guMid, cv0 - 2.3f); extent = new Vector2(2.6f, 1.4f); break;
                default: center = new Vector2(cu0 - 2.6f, cv0 - 2.2f); extent = new Vector2(1.8f, 1.8f); break;
            }
            if (nearWalls) center += zone == 2 ? new Vector2(0f, 0.9f) : new Vector2(0.9f, 0f);
        }

        public Tower NearestTower(Vector3 from, float maxRange = float.MaxValue)
        {
            Tower best = null;
            float bestD = maxRange * maxRange;
            foreach (var t in Towers)
            {
                if (t == null || t.IsDestroyed) continue;
                float d = (t.TopGround - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// <summary>Kulenin dışında, merdiven dayanacak zemin noktası (gelen yöne en yakın kenar).</summary>
        public Vector3 ClimbPoint(Tower t, Vector3 from)
        {
            Vector2Int c = t.cell;
            Vector2Int[] dirs = { new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1) };
            Vector3 best = Iso.Surface(c.x - 1, c.y);
            float bestD = float.MaxValue;
            foreach (var d in dirs)
            {
                Vector2Int n = c + d;
                bool inside = n.x >= cu0 && n.x <= cu1 && n.y >= cv0 && n.y <= cv1;
                if (inside) continue;
                Vector3 p = Iso.Surface(c.x + d.x * 0.62f, c.y + d.y * 0.62f);
                if (!Playable.Contains(new Vector2(p.x, p.y))) continue;
                float dist = (p - from).sqrMagnitude;
                if (dist < bestD) { bestD = dist; best = p; }
            }
            return best;
        }

        /// <summary>Kamerayı oynanabilir alanı gösterecek şekilde yerleştirir. bottomUI: alt arayüzün ekran oranı.</summary>
        public void FitCamera(Camera cam, float bottomUI = 0.22f)
        {
            float xMin = Playable.xMin - 0.3f, xMax = Playable.xMax + 0.3f;
            float yMin = Playable.yMin - 0.4f, yMax = Playable.yMax + 0.5f;
            float height = (yMax - yMin) / (1f - bottomUI);
            float size = Mathf.Max(height * 0.5f, (xMax - xMin) * 0.5f / cam.aspect);
            cam.orthographicSize = size;
            cam.transform.position = new Vector3((xMin + xMax) * 0.5f, yMax - size, -10f);
        }
    }
}
