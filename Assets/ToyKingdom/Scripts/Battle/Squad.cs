using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    public enum SquadMode
    {
        /// <summary>Çizilen rotada düzen içinde yürüyor.</summary>
        March,
        /// <summary>Rota bitti, kapıya saldırıyor.</summary>
        Assault,
        /// <summary>Merdivenciler: bir kuleye merdiven dayayıp tırmanıyor.</summary>
        Climb,
        /// <summary>Savunucu: dağınık nöbet noktalarında bekliyor, yakına geleni karşılıyor.</summary>
        Guard,
        /// <summary>Savunucu: sur üstünde sabit duruyor (okçular).</summary>
        Post,
        /// <summary>Morali çöktü: geri kaçıyor, savaşmıyor.</summary>
        Rout,
        /// <summary>Kaçan oyuncu bölüğü toplanma alanına vardı, savaş dışında bekliyor.</summary>
        Rallied
    }

    /// <summary>Bir bölük: askerlerine yer verir, rotayı ve hedefi yönetir.</summary>
    public class Squad
    {
        public Team Team { get; }
        public UnitData Data { get; }
        public SquadMode Mode { get; private set; }
        public List<Soldier> Members { get; } = new List<Soldier>();
        public int StartSize { get; private set; }
        public int Alive => Members.Count;
        public bool IsWiped => Members.Count == 0;
        public CastleGate Gate { get; private set; }
        public Tower ClimbTarget { get; private set; }
        public Vector3 ClimbPoint { get; private set; }
        public Vector3 Anchor { get; private set; }
        public Vector2 Heading { get; private set; } = new Vector2(1f, 0f);
        public float AggroRange { get; set; } = 1.8f;
        public float LeashRange { get; set; } = 3.5f;
        public bool Engaged => Time.time - lastCombat < 1.5f;
        /// <summary>Askerlerin can ve hasar çarpanı (savunucuları kale zorluğuna göre ayarlamak için).</summary>
        public float Strength { get; set; } = 1f;
        /// <summary>Oyuncu bölüğünün tecrübe puanı (savaş sonunda artar).</summary>
        public int Xp { get; set; }
        /// <summary>0-100. Düşerse hasar azalır, 18'in altında bölük bozguna uğrar.</summary>
        public float Morale { get; private set; } = 100f;
        public bool Routed => Mode == SquadMode.Rout || Mode == SquadMode.Rallied;

        // ------------------------------------------------------------------ oyuncu emirleri
        /// <summary>"Dur" emri: bölük olduğu yerde durur, yalnızca yakına geleni karşılar.</summary>
        public bool Halted { get; private set; }
        /// <summary>Kendi isteğiyle geri çekilen bölük (bozgun değil).</summary>
        public bool Withdrawing { get; private set; }
        public bool CanCommand => Team == Team.Player && Members.Count > 0 && !Routed;

        /// <summary>Dur / devam. Kapıda ya da kulede olsa bile durup bulunduğu yeri tutar.</summary>
        public void SetHalt(bool on)
        {
            if (!CanCommand) return;
            if (on && Mode != SquadMode.March)
            {
                // saldırıdan geri çek: bulunduğu yerde bekle; devam deyince yeniden hedefe yürür
                RemoveLadder();
                ClimbTarget = null;
                Gate = null;
                path.Clear();
                path.Add(Center());
                pathSegment = 0;
                pathDistance = 0f;
                Mode = SquadMode.March;
            }
            Halted = on;
            if (on) Anchor = Center();
        }

        /// <summary>Hücum: rotanın kalanını bırakıp doğrudan kapıya / kuleye koşar, moral kazanır.</summary>
        public void Charge()
        {
            if (!CanCommand) return;
            Halted = false;
            ChangeMorale(12f);
            if (Mode == SquadMode.March) { pathSegment = Mathf.Max(0, path.Count - 1); BeginClimbOrAssault(); }
        }

        /// <summary>Geri çekil: bozgun olmadan en yakın toplanma alanına döner; yarısından fazlası sağ dönerse orduya katılır.</summary>
        public void Withdraw()
        {
            if (!CanCommand) return;
            DestroyLine();
            RemoveLadder();
            ClimbTarget = null;
            Gate = null;
            Halted = false;
            Withdrawing = true;
            Mode = SquadMode.Rout;
            Vector3 c = Center();
            Anchor = field.NearestDeployPoint(c);
            Heading = ((Vector2)(Anchor - c)).normalized;
        }
        /// <summary>Morale göre hasar çarpanı.</summary>
        public float MoraleDamage => Morale >= 70f ? 1.1f : Morale < 35f ? 0.8f : 1f;
        bool wipeReported;
        Transform flag;
        SpriteRenderer flagFill;

        readonly List<Vector3> path = new List<Vector3>();
        readonly Dictionary<Soldier, int> slotIndex = new Dictionary<Soldier, int>();
        readonly Dictionary<Soldier, Vector3> posts = new Dictionary<Soldier, Vector3>();
        readonly Dictionary<Soldier, Vector3> patrol = new Dictionary<Soldier, Vector3>();
        float patrolTimer;
        float pathDistance;
        int pathSegment;
        float lastCombat = -10f;
        LineRenderer line;
        GameObject ladder;
        readonly BattleField field;

        public Squad(Team team, UnitData data, BattleField field)
        {
            Team = team;
            Data = data;
            this.field = field;
        }

        // ------------------------------------------------------------------ kurulum

        /// <summary>Oyuncu bölüğü: bir noktada düzenli sıra halinde doğar.</summary>
        public void SpawnMembers(Vector3 center, int count)
        {
            StartSize = count;
            Anchor = center;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = field.Constrain(center + FormationOffset(i, count, Heading));
                var s = Soldier.Spawn(Data, Team, this, p, Strength);
                slotIndex[s] = i;
                Members.Add(s);
            }
        }

        /// <summary>Savunucu bölüğü: her asker kendi nöbet noktasında doğar.</summary>
        public void SpawnAtPosts(List<Vector3> points, float elevation, bool fixedPosts)
        {
            StartSize = points.Count;
            Mode = fixedPosts ? SquadMode.Post : SquadMode.Guard;
            Heading = new Vector2(-1f, -0.3f).normalized;
            for (int i = 0; i < points.Count; i++)
            {
                var s = Soldier.Spawn(Data, Team, this, points[i], Strength, elevation);
                slotIndex[s] = i;
                posts[s] = points[i];
                patrol[s] = Vector3.zero;
                Members.Add(s);
            }
            if (points.Count > 0)
            {
                Vector3 c = Vector3.zero;
                foreach (var p in points) c += p;
                Anchor = c / points.Count;
            }
        }

        public void SetPath(List<Vector3> points, Material lineMaterial)
        {
            path.Clear();
            path.AddRange(points);
            pathSegment = 0;
            pathDistance = 0f;
            Mode = SquadMode.March;
            if (path.Count > 1) Heading = ((Vector2)(path[1] - path[0])).normalized;

            if (lineMaterial != null)
            {
                var go = new GameObject("SquadPath");
                line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.widthMultiplier = 0.06f;
                line.numCapVertices = 4;
                line.numCornerVertices = 2;
                line.useWorldSpace = true;
                line.sortingOrder = 29000;
                Color c = new Color(1f, 1f, 1f, 0.45f);
                line.startColor = c;
                line.endColor = c;
            }
            UpdateLine();
        }

        /// <summary>Nöbetçilere yeni nöbet noktaları verir (takviyeler kapıdan çıkıp yerlerine yürür).</summary>
        public void ReassignPosts(List<Vector3> points)
        {
            for (int i = 0; i < Members.Count && i < points.Count; i++) posts[Members[i]] = points[i];
        }

        public void SetGuardRanges(float aggro, float leash)
        {
            AggroRange = aggro;
            LeashRange = leash;
        }

        // ------------------------------------------------------------------ güncelleme

        public void Tick(float dt)
        {
            if (Members.Count == 0)
            {
                if (!wipeReported)
                {
                    wipeReported = true;
                    if (BattleManager.Instance != null) BattleManager.Instance.OnSquadWiped(this);
                }
                Cleanup();
                return;
            }

            UpdateMorale(dt);

            switch (Mode)
            {
                case SquadMode.March:
                    AdvanceAlongPath(dt);
                    break;
                case SquadMode.Assault:
                    if (Gate == null || Gate.IsDestroyed)
                    {
                        var bm = BattleManager.Instance;
                        if (bm != null && bm.Capturing)
                        {
                            // avlu savaşı: herkes kırılan kapının önünü tutar
                            Gate = null;
                            Anchor = bm.CapturePoint;
                        }
                        else
                        {
                            Gate = field.NearestGate(Center());
                            if (Gate != null) Anchor = Gate.FrontPoint;
                        }
                    }
                    break;
                case SquadMode.Rout:
                    if ((Center() - Anchor).magnitude < 0.7f)
                    {
                        if (Team == Team.Player) Mode = SquadMode.Rallied;
                        else foreach (var m in Members.ToArray()) m.Vanish();
                    }
                    break;
                case SquadMode.Climb:
                    if (ClimbTarget == null || ClimbTarget.IsDestroyed)
                    {
                        // kule yıkıldı: inen askerler bir sonraki kuleye geçer
                        if (!AnyElevated()) BeginClimbOrAssault();
                    }
                    break;
                case SquadMode.Guard:
                    UpdatePatrol(dt);
                    break;
            }
            UpdateLine();
        }

        bool AnyElevated()
        {
            foreach (var m in Members) if (m.Elevation > 0.05f) return true;
            return false;
        }

        void UpdatePatrol(float dt)
        {
            patrolTimer -= dt;
            if (patrolTimer > 0f) return;
            patrolTimer = Random.Range(2.5f, 4.5f);
            // her seferinde birkaç asker küçük bir devriye adımı atar
            foreach (var m in Members)
            {
                if (Random.value > 0.45f) continue;
                Vector2 r = Random.insideUnitCircle * 0.55f;
                patrol[m] = new Vector3(r.x, r.y * 0.6f, 0f);
            }
        }

        void AdvanceAlongPath(float dt)
        {
            // dur emri, yakın dövüş ya da geride kalan askerler: bekle
            if (Halted || Engaged) return;
            float lag = AverageSlotError();
            float speed = Data.moveSpeed * (lag > 0.7f ? 0.25f : lag > 0.4f ? 0.7f : 1f);
            float step = speed * dt;

            while (step > 0f && pathSegment < path.Count - 1)
            {
                Vector3 a = path[pathSegment], b = path[pathSegment + 1];
                float segLen = Vector3.Distance(a, b);
                float remain = segLen - pathDistance;
                if (step < remain)
                {
                    pathDistance += step;
                    step = 0f;
                }
                else
                {
                    step -= remain;
                    pathSegment++;
                    pathDistance = 0f;
                }
            }

            if (pathSegment >= path.Count - 1)
            {
                Anchor = path.Count > 0 ? path[path.Count - 1] : Anchor;
                BeginClimbOrAssault();
                return;
            }

            Vector3 p0 = path[pathSegment], p1 = path[pathSegment + 1];
            float len = Mathf.Max(0.0001f, Vector3.Distance(p0, p1));
            Anchor = Vector3.Lerp(p0, p1, pathDistance / len);
            Vector2 dir = ((Vector2)(p1 - p0)).normalized;
            if (dir.sqrMagnitude > 0.5f) Heading = Vector2.Lerp(Heading, dir, 0.15f).normalized;
        }

        void BeginClimbOrAssault()
        {
            DestroyLine();
            RemoveLadder();
            if (Data.role == UnitRole.Climber)
            {
                var t = field.NearestTower(Center());
                if (t != null)
                {
                    Mode = SquadMode.Climb;
                    ClimbTarget = t;
                    ClimbPoint = field.ClimbPoint(t, Center());
                    Anchor = ClimbPoint;
                    return;
                }
            }
            Mode = SquadMode.Assault;
            ClimbTarget = null;
            Gate = field.NearestGate(Anchor);
            if (Gate != null) Anchor = Gate.FrontPoint;
        }

        float AverageSlotError()
        {
            if (Members.Count == 0) return 0f;
            float sum = 0f;
            foreach (var m in Members) sum += (SlotPosition(m) - m.Ground).magnitude;
            return sum / Members.Count;
        }

        // ------------------------------------------------------------------ moral

        /// <summary>Tecrübeli bölük savaşa yüksek moralle başlar.</summary>
        public void SetStartMorale(float bonus) => Morale = Mathf.Clamp(Morale + bonus, 0f, 100f);

        public void ChangeMorale(float amount)
        {
            if (Members.Count == 0 || Mode == SquadMode.Post) return;
            Morale = Mathf.Clamp(Morale + amount, 0f, 100f);
            if (Morale < 18f && !Routed) BeginRout();
        }

        void UpdateMorale(float dt)
        {
            if (Mode == SquadMode.Post) { UpdateFlag(); return; }
            // savaş dışında moral yavaşça toparlanır; kaçanlar en fazla 60'a kadar
            if (!Engaged) Morale = Mathf.Min(Routed ? 60f : 100f, Morale + dt * (Team == Team.Player ? 2f : 1f));
            UpdateFlag();
        }

        void BeginRout()
        {
            DestroyLine();
            RemoveLadder();
            ClimbTarget = null;
            Gate = null;
            Mode = SquadMode.Rout;
            Vector3 c = Center();
            if (Team == Team.Player)
            {
                // en yakın toplanma alanına geri çekil
                Anchor = field.NearestDeployPoint(c);
                Heading = ((Vector2)(Anchor - c)).normalized;
            }
            else
            {
                var g = field.NearestGate(c);
                Anchor = g != null ? g.FrontPoint : field.DefenderHome(0);
            }
            if (BattleManager.Instance != null) BattleManager.Instance.OnSquadRouted(this);
        }

        /// <summary>Bölüğün üstünde moral rengini gösteren küçük bayrak.</summary>
        void UpdateFlag()
        {
            bool show = Team == Team.Player || Morale < 99f;
            if (!show || Members.Count == 0)
            {
                if (flag != null) flag.gameObject.SetActive(false);
                return;
            }
            if (flag == null)
            {
                var art = GameArt.Instance;
                if (art == null || art.circle == null) return;
                flag = new GameObject("MoraleFlag").transform;
                var rim = flag.gameObject.AddComponent<SpriteRenderer>();
                rim.sprite = art.circle;
                rim.color = Team == Team.Player ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.15f, 0.05f, 0.05f, 0.9f);
                rim.sortingOrder = 30400;
                flag.localScale = Vector3.one * 0.2f;
                var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
                fill.transform.SetParent(flag, false);
                fill.transform.localScale = Vector3.one * 0.72f;
                fill.sprite = art.circle;
                fill.sortingOrder = 30401;
                flagFill = fill;
            }
            flag.gameObject.SetActive(true);
            Vector3 top = Center();
            float maxElev = 0f;
            foreach (var m in Members) maxElev = Mathf.Max(maxElev, m.Elevation);
            flag.position = top + Vector3.up * (1.3f + maxElev);
            Color c = Morale >= 60f ? new Color(0.42f, 0.83f, 0.36f) : Morale >= 35f ? new Color(0.98f, 0.78f, 0.25f) : new Color(0.95f, 0.3f, 0.25f);
            if (Routed) c = Color.Lerp(c, Color.white, Mathf.PingPong(Time.time * 3f, 1f));
            flagFill.color = c;
        }

        public Vector3 Center()
        {
            if (Members.Count == 0) return Anchor;
            Vector3 c = Vector3.zero;
            foreach (var m in Members) c += m.Ground;
            return c / Members.Count;
        }

        // ------------------------------------------------------------------ merdiven

        public void PlaceLadder()
        {
            if (ladder != null || ClimbTarget == null) return;
            var art = GameArt.Instance;
            if (art == null || art.ladder == null) return;
            ladder = new GameObject("Ladder");
            Vector3 top = ClimbTarget.TopGround + Vector3.up * Tower.TopElevation;
            Vector3 bottom = ClimbPoint;
            ladder.transform.position = bottom;
            Vector3 d = top - bottom;
            float angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            ladder.transform.rotation = Quaternion.Euler(0f, 0f, -angle);
            var sr = ladder.AddComponent<SpriteRenderer>();
            sr.sprite = art.ladder;
            sr.sortingOrder = Iso.Order(bottom, 4);
            float h = art.ladder.bounds.size.y;
            float s = h > 0.01f ? d.magnitude / h : 1f;
            ladder.transform.localScale = new Vector3(1f, s, 1f);
        }

        void RemoveLadder()
        {
            if (ladder != null) Object.Destroy(ladder);
            ladder = null;
        }

        // ------------------------------------------------------------------ yerler

        public Vector3 SlotPosition(Soldier s)
        {
            int idx = slotIndex.TryGetValue(s, out int i) ? i : 0;
            int count = Mathf.Max(StartSize, 1);

            switch (Mode)
            {
                case SquadMode.Guard:
                case SquadMode.Post:
                    if (posts.TryGetValue(s, out var post))
                        return Mode == SquadMode.Guard && patrol.TryGetValue(s, out var off) ? field.Constrain(post + off) : post;
                    return Anchor;

                case SquadMode.Climb:
                {
                    // merdivenin dibinde küçük bir yığın
                    Vector2 g = Iso.ToGrid(ClimbPoint);
                    float a = idx * 2.39996f;
                    float r = 0.18f + 0.12f * (idx % 3);
                    Vector3 p = Iso.Surface(g.x + Mathf.Cos(a) * r, g.y + Mathf.Sin(a) * r);
                    return field.Constrain(p);
                }

                case SquadMode.Assault:
                    if (Gate != null)
                    {
                        Vector2 outward = Gate.Outward;
                        float t = count <= 1 ? 0.5f : (float)idx / (count - 1);
                        float angle = Mathf.Lerp(-70f, 70f, t) * Mathf.Deg2Rad;
                        int row = idx % 2;
                        // okçular kapıdan biraz geride durur
                        float radius = (Data.IsRanged ? 2.2f : 0.55f) + row * 0.4f;
                        Vector2 dir = Rotate(outward, angle) * radius;
                        Vector2 gg = Iso.ToGrid(Gate.FrontPoint) + dir - outward * 0.2f;
                        return field.Constrain(Iso.Surface(gg.x, gg.y));
                    }
                    break;
            }
            return Anchor + FormationOffset(idx, count, Heading);
        }

        /// <summary>Bir merdivencinin kule tepesinde duracağı nokta (zemin karşılığı).</summary>
        public Vector3 TowerTopSpot(Soldier s)
        {
            if (ClimbTarget == null) return s.Ground;
            int idx = slotIndex.TryGetValue(s, out int i) ? i : 0;
            Vector2 g = Iso.ToGrid(ClimbTarget.TopGround);
            float a = idx * 2.39996f;
            float r = 0.12f + 0.06f * (idx % 3);
            return Iso.Surface(g.x + Mathf.Cos(a) * r, g.y + Mathf.Sin(a) * r);
        }

        static Vector3 FormationOffset(int index, int count, Vector2 heading)
        {
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)cols);
            int r = index / cols, c = index % cols;
            const float spacing = 0.3f;
            float x = (c - (cols - 1) * 0.5f) * spacing;
            float y = -(r - (rows - 1) * 0.5f) * spacing;
            Vector2 fwd = heading.sqrMagnitude > 0.01f ? heading.normalized : Vector2.up;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            Vector2 o = right * x + fwd * y;
            o.y *= 0.62f; // izometrik basıklık
            return o;
        }

        static Vector2 Rotate(Vector2 v, float rad)
        {
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        /// <summary>Geride kalan asker biraz hızlanır.</summary>
        public float SpeedFactor(Soldier s)
        {
            if (Mode == SquadMode.Guard) return 0.55f; // devriye yavaş yürür
            if (Mode == SquadMode.Rout) return 1.15f;
            if (Mode != SquadMode.March) return 1f;
            float err = (SlotPosition(s) - s.Ground).magnitude;
            return err > 0.5f ? 1.35f : 1f;
        }

        /// <summary>Nöbetçi bu askerin nöbet noktasından ne kadar uzakta?</summary>
        public float DistanceFromPost(Soldier s, Vector3 point)
        {
            if (posts.TryGetValue(s, out var p)) return (point - p).magnitude;
            return (point - Anchor).magnitude;
        }

        // ------------------------------------------------------------------ olaylar

        public void NotifyCombat() => lastCombat = Time.time;

        public void OnMemberDied(Soldier s, bool killed = true)
        {
            Members.Remove(s);
            if (!killed) return;
            lastCombat = Time.time;
            // her kayıp morali düşürür: bölüğün yarısını kaybetmek yaklaşık -30
            ChangeMorale(-60f / Mathf.Max(1, StartSize));
        }

        void UpdateLine()
        {
            if (line == null) return;
            if (Mode != SquadMode.March || path.Count < 2)
            {
                DestroyLine();
                return;
            }
            int n = path.Count - pathSegment;
            line.positionCount = n;
            line.SetPosition(0, Anchor + Vector3.up * 0.02f);
            for (int i = 1; i < n; i++) line.SetPosition(i, path[pathSegment + i] + Vector3.up * 0.02f);
        }

        void DestroyLine()
        {
            if (line != null) Object.Destroy(line.gameObject);
            line = null;
        }

        public void Cleanup()
        {
            DestroyLine();
            RemoveLadder();
            if (flag != null) Object.Destroy(flag.gameObject);
            flag = null;
        }
    }
}
