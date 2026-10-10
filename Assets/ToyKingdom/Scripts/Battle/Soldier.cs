using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    public enum Team { Player, Enemy }

    /// <summary>
    /// Tek bir asker. Rolüne göre yakın dövüşür, uzaktan ok atar ya da kulelere tırmanır.
    /// Zemindeki konumu (Ground) ile yüksekliği (Elevation: sur/kule üstü) ayrı tutulur.
    /// Kod ile oluşturulur (prefab gerekmez).
    /// </summary>
    public class Soldier : MonoBehaviour
    {
        public static readonly List<Soldier> All = new List<Soldier>();

        static readonly Color EnemyTint = new Color(1f, 0.52f, 0.46f);
        const float Radius = 0.17f;
        const float ArrowSpeed = 7.5f;

        public UnitData Data { get; private set; }
        public Team Team { get; private set; }
        public Squad Squad { get; private set; }
        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        public Vector3 Ground => ground;
        public float Elevation => elevation;
        public bool IsElevated => elevation > 0.05f;

        SpriteAnimator anim;
        SpriteRenderer body;
        SpriteRenderer shadow;
        YSort ysort;
        Transform hpRoot;
        Transform hpFill;
        Color baseColor;
        Vector3 ground;
        float elevation;
        float hitFlash;
        float attackCooldown;
        float retargetTimer;
        float deathTimer;
        float healthScale = 1f;
        float damageScale = 1f;
        bool climbing;
        Soldier target;
        Soldier pendingSoldier;
        CastleGate pendingGate;
        Tower pendingTower;
        bool pendingRanged;
        Vector2 lastMoveDir = new Vector2(1f, 0f);

        BattleField Field => BattleManager.Instance.Field;

        // ------------------------------------------------------------------ oluşturma

        public static Soldier Spawn(UnitData data, Team team, Squad squad, Vector3 position, float strength = 1f, float elevation = 0f)
        {
            var art = GameArt.Instance;
            var go = new GameObject(team == Team.Player ? "P_" + data.unitId : "E_" + data.unitId);

            float scale = 0.95f * Mathf.Max(0.3f, data.scale);

            var shadowSr = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadowSr.transform.SetParent(go.transform, false);
            shadowSr.transform.localScale = new Vector3(0.55f * scale, 0.55f * scale, 1f);
            shadowSr.sprite = art.shadow;
            shadowSr.color = new Color(0f, 0f, 0f, 0.3f);
            shadowSr.sortingOrder = -2;

            var bodySr = new GameObject("Body").AddComponent<SpriteRenderer>();
            bodySr.transform.SetParent(go.transform, false);
            bodySr.transform.localScale = new Vector3(scale, scale, 1f);
            bodySr.sortingOrder = 0;

            var hp = new GameObject("Hp").transform;
            hp.SetParent(go.transform, false);
            hp.localPosition = new Vector3(0f, 0.95f * scale + 0.08f, 0f);
            var hpBg = new GameObject("Bg").AddComponent<SpriteRenderer>();
            hpBg.transform.SetParent(hp, false);
            hpBg.transform.localScale = new Vector3(0.36f, 0.05f, 1f);
            hpBg.sprite = art.white;
            hpBg.color = new Color(0.1f, 0.08f, 0.08f, 0.8f);
            hpBg.sortingOrder = 1;
            var hpFg = new GameObject("Fill").AddComponent<SpriteRenderer>();
            hpFg.transform.SetParent(hp, false);
            hpFg.transform.localScale = new Vector3(0.36f, 0.05f, 1f);
            hpFg.sprite = art.white;
            hpFg.color = team == Team.Player ? new Color(0.35f, 0.75f, 0.3f) : new Color(0.89f, 0.29f, 0.29f);
            hpFg.sortingOrder = 2;
            hp.gameObject.SetActive(false);

            var animator = bodySr.gameObject.AddComponent<SpriteAnimator>();
            animator.target = bodySr;
            animator.set = data.anim != null ? data.anim : art.knight;

            var s = go.AddComponent<Soldier>();
            s.Data = data;
            s.Team = team;
            s.Squad = squad;
            s.Health = data.maxHealth;
            s.healthScale = Mathf.Max(0.1f, strength);
            s.damageScale = Mathf.Max(0.1f, strength);
            s.anim = animator;
            s.body = bodySr;
            s.shadow = shadowSr;
            s.hpRoot = hp;
            s.hpFill = hpFg.transform;
            s.baseColor = team == Team.Player ? data.tint : data.tint * EnemyTint;
            s.ground = position;
            s.elevation = elevation;
            bodySr.color = s.baseColor;
            animator.Hit += s.OnAttackHit;
            animator.speed = Random.Range(0.92f, 1.08f);
            animator.SetDirection(team == Team.Player ? 2 : 6);
            animator.Play(AnimType.Idle, true);
            s.lastMoveDir = team == Team.Player ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);

            // YSort en son eklenir: alt objelerin sıra farklarını okur
            s.ysort = go.AddComponent<YSort>();
            s.ysort.bias = 6;
            s.SyncTransform();

            All.Add(s);
            return s;
        }

        void OnDestroy() => All.Remove(this);

        void SyncTransform()
        {
            transform.position = ground + Vector3.up * elevation;
            if (ysort != null) ysort.heightOffset = elevation;
        }

        // ------------------------------------------------------------------ sorgular

        /// <summary>En yakın canlı asker. includeElevated=false ise sur/kule üstündekiler sayılmaz (yakın dövüş için).</summary>
        public static Soldier FindNearest(Team team, Vector3 from, float range, bool includeElevated)
        {
            Soldier best = null;
            float bestD = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || !s.IsAlive || s.Team != team) continue;
                if (!includeElevated && s.IsElevated) continue;
                float d = (s.ground - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public static Soldier FindRandomNear(Team team, Vector3 from, float range)
        {
            Soldier pick = null;
            int seen = 0;
            float r2 = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || !s.IsAlive || s.Team != team) continue;
                if ((s.ground - from).sqrMagnitude > r2) continue;
                seen++;
                if (Random.Range(0, seen) == 0) pick = s;
            }
            return pick;
        }

        // ------------------------------------------------------------------ ana döngü

        void Update()
        {
            if (!IsAlive)
            {
                UpdateDead();
                return;
            }

            UpdateFlash();

            var bm = BattleManager.Instance;
            if (bm == null || bm.State != BattleState.Playing)
            {
                if (anim.Current != AnimType.Attack || anim.Finished) anim.Play(AnimType.Idle);
                return;
            }

            attackCooldown -= Time.deltaTime;

            // Bozguna uğrayan bölük savaşmaz, geri kaçar
            if (Squad != null && Squad.Routed)
            {
                UpdateRouted();
                SyncTransform();
                return;
            }

            // Saldırı animasyonu bitene kadar yerinde kal
            if (anim.Current == AnimType.Attack && !anim.Finished) return;

            switch (Data.role)
            {
                case UnitRole.Ranged: UpdateRanged(); break;
                case UnitRole.Climber: UpdateClimber(); break;
                default: UpdateMelee(); break;
            }
            SyncTransform();
        }

        void UpdateDead()
        {
            deathTimer += Time.deltaTime;
            if (elevation > 0f)
            {
                // surdan/kuleden düşer
                elevation = Mathf.MoveTowards(elevation, 0f, Time.deltaTime * 2.5f);
                SyncTransform();
            }
            if (deathTimer > 2.5f)
            {
                float a = Mathf.Clamp01(1f - (deathTimer - 2.5f));
                body.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.3f * a);
                if (a <= 0f) Destroy(gameObject);
            }
        }

        bool Retarget(float range, bool includeElevated)
        {
            retargetTimer -= Time.deltaTime;
            if (retargetTimer > 0f && target != null && target.IsAlive) return target != null;
            retargetTimer = Random.Range(0.2f, 0.35f);
            Team enemy = Team == Team.Player ? Team.Enemy : Team.Player;
            target = FindNearest(enemy, ground, range, includeElevated);

            // Nöbetçiler noktalarından fazla uzaklaşmaz
            if (target != null && Team == Team.Enemy && Squad.Mode == SquadMode.Guard &&
                Squad.DistanceFromPost(this, target.ground) > Squad.LeashRange)
                target = null;
            return target != null;
        }

        float AggroRange => Squad.AggroRange + (Squad.Engaged ? 1.2f : 0f);

        // ------------------------------------------------------------------ yakın dövüş

        void UpdateMelee()
        {
            if (Retarget(AggroRange, false))
            {
                Vector3 to = target.ground - ground;
                float reach = Data.attackRange + Radius;
                Squad.NotifyCombat();
                if (to.magnitude <= reach)
                {
                    anim.Face(to);
                    if (attackCooldown <= 0f) StartAttack(target, null, null, false);
                    else anim.Play(AnimType.Idle);
                    Separate(0.6f);
                    return;
                }
                MoveTowards(target.ground, reach * 0.9f, true);
                return;
            }

            if (TryAttackGate()) return;
            MoveTowards(Squad.SlotPosition(this), 0.06f);
        }

        bool TryAttackGate()
        {
            if (Team != Team.Player || Squad.Mode != SquadMode.Assault || Squad.Gate == null || Squad.Gate.IsDestroyed) return false;
            Vector3 slot = Squad.SlotPosition(this);
            if ((slot - ground).sqrMagnitude >= 0.09f) return false;
            anim.Face(Squad.Gate.transform.position - ground);
            if (attackCooldown <= 0f) StartAttack(null, Squad.Gate, null, Data.IsRanged);
            else anim.Play(AnimType.Idle);
            Separate(0.5f);
            return true;
        }

        // ------------------------------------------------------------------ okçu

        void UpdateRanged()
        {
            float range = Data.attackRange;
            bool fixedPost = Squad.Mode == SquadMode.Post;

            if (Retarget(Mathf.Max(range, fixedPost ? range : AggroRange), true))
            {
                Vector3 to = target.ground - ground;
                if (to.magnitude <= range)
                {
                    anim.Face(to);
                    if (attackCooldown <= 0f) StartAttack(target, null, null, true);
                    else anim.Play(AnimType.Idle);
                    if (!fixedPost) Separate(0.4f);
                    return;
                }
                if (!fixedPost)
                {
                    MoveTowards(target.ground, range * 0.85f, true);
                    return;
                }
            }

            if (Team == Team.Player)
            {
                // asker yoksa menzildeki kuleyi, saldırıdaysa kapıyı vur
                var tower = Field.NearestTower(ground, range);
                if (tower != null)
                {
                    anim.Face(tower.TopGround - ground);
                    if (attackCooldown <= 0f) StartAttack(null, null, tower, true);
                    else anim.Play(AnimType.Idle);
                    return;
                }
                if (TryAttackGate()) return;
            }

            if (fixedPost)
            {
                anim.Play(AnimType.Idle);
                anim.Face(Squad.Heading);
                return;
            }
            MoveTowards(Squad.SlotPosition(this), 0.06f);
        }

        // ------------------------------------------------------------------ merdivenci

        void UpdateClimber()
        {
            Tower tower = Squad.ClimbTarget;

            if (climbing || IsElevated)
            {
                if (tower == null || tower.IsDestroyed)
                {
                    // kule yıkıldı: aşağı in
                    elevation = Mathf.MoveTowards(elevation, 0f, Time.deltaTime * 1.4f);
                    if (elevation <= 0f)
                    {
                        climbing = false;
                        ground = Field.Constrain(ground);
                    }
                    anim.Play(AnimType.Idle);
                    return;
                }

                Vector3 top = Squad.TowerTopSpot(this);
                if (elevation < Tower.TopElevation - 0.01f)
                {
                    // merdivenden tırman
                    elevation = Mathf.MoveTowards(elevation, Tower.TopElevation, Time.deltaTime * 0.55f);
                    float k = elevation / Tower.TopElevation;
                    ground = Vector3.Lerp(Squad.ClimbPoint, top, k);
                    anim.Face(top - Squad.ClimbPoint);
                    anim.Play(AnimType.Walk);
                    return;
                }

                ground = top;
                anim.Face(new Vector2(-1f, -0.4f));
                if (attackCooldown <= 0f) StartAttack(null, null, tower, false);
                else anim.Play(AnimType.Idle);
                return;
            }

            // yerde: çok yakına gelen düşmanla dövüş
            if (Retarget(0.9f, false))
            {
                Vector3 to = target.ground - ground;
                float reach = Data.attackRange + Radius;
                Squad.NotifyCombat();
                if (to.magnitude <= reach)
                {
                    anim.Face(to);
                    if (attackCooldown <= 0f) StartAttack(target, null, null, false);
                    else anim.Play(AnimType.Idle);
                    return;
                }
                MoveTowards(target.ground, reach * 0.9f, true);
                return;
            }

            if (Squad.Mode == SquadMode.Climb && tower != null && !tower.IsDestroyed)
            {
                if ((Squad.ClimbPoint - ground).magnitude < 0.2f)
                {
                    climbing = true;
                    Squad.PlaceLadder();
                    return;
                }
                MoveTowards(Squad.ClimbPoint, 0.05f);
                return;
            }

            if (TryAttackGate()) return;
            MoveTowards(Squad.SlotPosition(this), 0.06f);
        }

        // ------------------------------------------------------------------ bozgun

        void UpdateRouted()
        {
            if (elevation > 0f)
            {
                // kuleden/surdan aşağı in
                climbing = false;
                elevation = Mathf.MoveTowards(elevation, 0f, Time.deltaTime * 1.6f);
                if (elevation <= 0f) ground = Field.Constrain(ground);
                anim.Play(AnimType.Walk);
                return;
            }
            MoveTowards(Squad.SlotPosition(this), 0.08f);
        }

        /// <summary>Savaş alanından sessizce çekilir (kaçan savunucular kaleye girer).</summary>
        public void Vanish()
        {
            if (!IsAlive) return;
            Health = 0f;
            All.Remove(this);
            if (Squad != null) Squad.OnMemberDied(this, false);
            Destroy(gameObject);
        }

        float Dmg => damageScale * (Squad != null ? Squad.MoraleDamage : 1f);

        // ------------------------------------------------------------------ saldırı

        void StartAttack(Soldier enemy, CastleGate gate, Tower tower, bool ranged)
        {
            pendingSoldier = enemy;
            pendingGate = gate;
            pendingTower = tower;
            pendingRanged = ranged;
            attackCooldown = Data.attackInterval * Random.Range(0.9f, 1.1f);
            anim.Play(AnimType.Attack, true);
        }

        void OnAttackHit()
        {
            if (!IsAlive) return;
            Vector3 from = transform.position + Vector3.up * 0.55f;

            if (pendingSoldier != null && pendingSoldier.IsAlive)
            {
                if (pendingRanged)
                    Projectile.AtSoldier(from, pendingSoldier, Data.damage * Dmg, ArrowSpeed, 0.6f);
                else if ((pendingSoldier.ground - ground).magnitude <= Data.attackRange + Radius + 0.25f)
                    pendingSoldier.TakeDamage(Data.damage * Dmg, DamageType.Melee);
            }
            else if (pendingTower != null && !pendingTower.IsDestroyed)
            {
                if (pendingRanged) Projectile.AtTower(from, pendingTower, Data.towerDamage * Dmg, ArrowSpeed, 0.7f);
                else pendingTower.TakeDamage(Data.towerDamage * Dmg);
            }
            else if (pendingGate != null && !pendingGate.IsDestroyed)
            {
                if (pendingRanged) Projectile.AtGate(from, pendingGate, Data.gateDamage * Dmg, ArrowSpeed, 0.7f);
                else pendingGate.TakeDamage(Data.gateDamage * Dmg);
            }
            pendingSoldier = null;
            pendingGate = null;
            pendingTower = null;
        }

        // ------------------------------------------------------------------ hareket

        void MoveTowards(Vector3 goal, float stopDistance, bool chase = false)
        {
            Vector3 to = goal - ground;
            float dist = to.magnitude;
            Vector3 move = Vector3.zero;
            if (dist > stopDistance)
            {
                float speed = Data.moveSpeed * (chase ? 1f : Squad.SpeedFactor(this));
                move = to / dist * Mathf.Min(speed * Time.deltaTime, dist - stopDistance * 0.5f);
            }

            Vector3 sep = SeparationVector();
            ground = Field.Constrain(ground + move + sep * Time.deltaTime * 1.5f);

            if (move.sqrMagnitude > 0.000001f)
            {
                lastMoveDir = Vector2.Lerp(lastMoveDir, ((Vector2)move).normalized, 0.35f);
                anim.Face(lastMoveDir);
                anim.Play(AnimType.Walk);
            }
            else
            {
                anim.Play(AnimType.Idle);
                if (Squad.Mode != SquadMode.Assault) anim.Face(Squad.Heading);
            }
        }

        void Separate(float strength)
        {
            if (IsElevated) return;
            Vector3 sep = SeparationVector();
            if (sep.sqrMagnitude < 0.000001f) return;
            ground = Field.Constrain(ground + sep * Time.deltaTime * strength);
        }

        Vector3 SeparationVector()
        {
            Vector3 push = Vector3.zero;
            const float minDist = Radius * 2f;
            for (int i = 0; i < All.Count; i++)
            {
                var o = All[i];
                if (o == this || o == null || !o.IsAlive || o.IsElevated) continue;
                Vector3 d = ground - o.ground;
                d.y *= 1.6f; // izometrik: dikey mesafe ekranda daha kısa görünür
                float m = d.magnitude;
                if (m < minDist && m > 0.0001f) push += d / m * (minDist - m) / minDist;
                else if (m <= 0.0001f) push += (Vector3)Random.insideUnitCircle * 0.5f;
            }
            push.y /= 1.6f;
            return push * 2.2f;
        }

        // ------------------------------------------------------------------ hasar

        public void TakeDamage(float amount, DamageType type)
        {
            if (!IsAlive) return;
            Health -= amount * (1f - Data.Armor(type)) / healthScale;
            hitFlash = 0.12f;
            if (hpRoot != null)
            {
                hpRoot.gameObject.SetActive(true);
                float n = Mathf.Clamp01(Health / Data.maxHealth);
                hpFill.localScale = new Vector3(0.36f * n, 0.05f, 1f);
                hpFill.localPosition = new Vector3(-0.18f + 0.18f * n, 0f, 0f);
            }
            if (Squad != null && type == DamageType.Melee) Squad.NotifyCombat();
            if (Health <= 0f) Die();
        }

        void UpdateFlash()
        {
            if (hitFlash <= 0f) return;
            hitFlash -= Time.deltaTime;
            body.color = hitFlash > 0f ? Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f), 0.6f) : baseColor;
        }

        void Die()
        {
            Health = 0f;
            body.color = baseColor;
            if (hpRoot != null) hpRoot.gameObject.SetActive(false);
            anim.Play(AnimType.Death, true);
            All.Remove(this);
            if (Squad != null) Squad.OnMemberDied(this);
            if (ysort != null) ysort.bias = 2; // cesetler ayakta olanların altında kalsın
        }
    }
}
