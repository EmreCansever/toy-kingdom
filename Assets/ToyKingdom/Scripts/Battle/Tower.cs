using System;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Menzile giren en yakın oyuncu askerine ok atan kale kulesi. Merdivenciler ve okçular yıkabilir.</summary>
    public class Tower : MonoBehaviour
    {
        public TowerData data;
        public Vector3 firePoint;
        public Vector2Int cell;

        public float MaxHealth { get; private set; } = 1f;
        public float Health { get; private set; } = 1f;
        public bool IsDestroyed => Health <= 0f;
        /// <summary>Kule tepesinin altındaki zemin noktası (tırmananlar buraya yürür).</summary>
        public Vector3 TopGround => Iso.Surface(cell.x, cell.y);
        /// <summary>Kule tepesinin zeminden yüksekliği.</summary>
        public const float TopElevation = 0.7f + Iso.WallLevel;

        public event Action<Tower> Destroyed;

        float cooldown;
        SpriteRenderer sr;
        GameObject bar;
        Transform barFill;
        float barTimer;
        float shake;
        Vector3 origin;
        SpriteRenderer smoke;
        float smokeTime;

        public void Init(TowerData towerData, Vector2Int towerCell, GameArt art)
        {
            data = towerData;
            cell = towerCell;
            firePoint = Iso.BlockBase(cell.x, cell.y, 1) + Vector3.up * 1.1f;
            MaxHealth = Health = data != null ? Mathf.Max(1f, data.maxHealth) : 150f;
            sr = GetComponent<SpriteRenderer>();
            origin = transform.position;

            bar = new GameObject("HealthBar");
            bar.transform.SetParent(transform, false);
            bar.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            var bg = Bar(bar.transform, art.white, new Color(0.15f, 0.1f, 0.1f, 0.85f), 30000);
            bg.transform.localScale = new Vector3(0.7f, 0.08f, 1f);
            var fill = Bar(bar.transform, art.white, new Color(0.95f, 0.6f, 0.2f), 30001);
            fill.transform.localScale = new Vector3(0.7f, 0.08f, 1f);
            barFill = fill.transform;
            bar.SetActive(false);

            if (art.smoke != null)
            {
                smoke = Bar(transform, art.smoke, new Color(0.3f, 0.3f, 0.3f, 0f), sr != null ? sr.sortingOrder + 3 : 0);
                smoke.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                smoke.transform.localScale = new Vector3(2f, 2f, 1f);
            }
        }

        static SpriteRenderer Bar(Transform parent, Sprite s, Color c, int order)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.color = c;
            r.sortingOrder = order;
            return r;
        }

        void Start() => cooldown = UnityEngine.Random.Range(0.3f, 1.2f);

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            Health = Mathf.Max(0f, Health - amount);
            shake = 0.12f;
            barTimer = 3f;
            bar.SetActive(true);
            float n = Health / MaxHealth;
            barFill.localScale = new Vector3(0.7f * n, 0.08f, 1f);
            barFill.localPosition = new Vector3(-0.35f + 0.35f * n, 0f, 0f);
            if (IsDestroyed)
            {
                bar.SetActive(false);
                if (sr != null) sr.color = new Color(0.35f, 0.32f, 0.32f);
                smokeTime = 0.01f;
                Destroyed?.Invoke(this);
            }
        }

        void Update()
        {
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                transform.position = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.03f);
            }
            else transform.position = origin;

            if (barTimer > 0f)
            {
                barTimer -= Time.deltaTime;
                if (barTimer <= 0f && !IsDestroyed) bar.SetActive(false);
            }

            if (smokeTime > 0f && smoke != null)
            {
                smokeTime += Time.deltaTime;
                float t = Mathf.Repeat(smokeTime, 2.5f) / 2.5f;
                smoke.transform.localPosition = new Vector3(Mathf.Sin(smokeTime) * 0.1f, 1.1f + t * 0.8f, 0f);
                smoke.color = new Color(0.3f, 0.3f, 0.3f, 0.6f * (1f - t));
            }

            if (IsDestroyed || data == null) return;
            var bm = BattleManager.Instance;
            if (bm == null || bm.State != BattleState.Playing) return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            Soldier target = Soldier.FindNearest(Team.Player, TopGround, data.range, true);
            if (target == null) { cooldown = 0.2f; return; }

            for (int i = 0; i < Mathf.Max(1, data.arrowsPerShot); i++)
            {
                Soldier t = target;
                if (i > 0)
                {
                    var other = Soldier.FindRandomNear(Team.Player, target.Ground, 1.2f);
                    if (other != null) t = other;
                }
                Projectile.AtSoldier(firePoint, t, data.damage, data.projectileSpeed, data.arcHeight);
            }
            cooldown = data.fireInterval;
            if (Sfx.Ready("bow", 0.15f)) Sfx.Play("sfx_bow", 0.28f, 0.12f, 0f, 1.1f);
        }
    }
}
