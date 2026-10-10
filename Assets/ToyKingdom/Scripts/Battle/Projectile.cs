using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Kavisli uçan ok. Askere, kuleye veya kapıya atılabilir. Hedef ölse bile son konumuna düşer.</summary>
    public class Projectile : MonoBehaviour
    {
        Soldier soldier;
        Tower tower;
        CastleGate gate;
        float damage;
        Vector3 start;
        Vector3 end;
        float arc;
        float t;
        float duration;
        SpriteRenderer sr;
        float stuckTimer = -1f;

        public static void AtSoldier(Vector3 from, Soldier target, float damage, float speed, float arc)
        {
            var p = Create(from, target.transform.position + Vector3.up * 0.3f, speed, arc);
            p.soldier = target;
            p.damage = damage;
        }

        public static void AtTower(Vector3 from, Tower target, float damage, float speed, float arc)
        {
            var p = Create(from, target.firePoint - Vector3.up * 0.3f, speed, arc);
            p.tower = target;
            p.damage = damage;
        }

        public static void AtGate(Vector3 from, CastleGate target, float damage, float speed, float arc)
        {
            var p = Create(from, target.transform.position + Vector3.up * 0.5f, speed, arc);
            p.gate = target;
            p.damage = damage;
        }

        static Projectile Create(Vector3 from, Vector3 to, float speed, float arc)
        {
            var art = GameArt.Instance;
            var go = new GameObject("Arrow");
            go.transform.position = from;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = art != null ? art.arrow : null;
            r.sortingOrder = 25000;
            var p = go.AddComponent<Projectile>();
            p.sr = r;
            p.start = from;
            p.end = to;
            p.arc = arc;
            p.duration = Mathf.Max(0.15f, Vector3.Distance(from, to) / Mathf.Max(0.5f, speed));
            return p;
        }

        void Update()
        {
            if (stuckTimer >= 0f)
            {
                stuckTimer += Time.deltaTime;
                if (sr != null) sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.5f - stuckTimer));
                if (stuckTimer > 1.5f) Destroy(gameObject);
                return;
            }

            if (soldier != null && soldier.IsAlive) end = soldier.transform.position + Vector3.up * 0.3f;
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(start, end, k) + Vector3.up * (arc * 4f * k * (1f - k));
            Vector3 dir = pos - transform.position;
            if (dir.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            transform.position = pos;
            if (k < 1f) return;

            if (soldier != null)
            {
                if (soldier.IsAlive && (soldier.transform.position + Vector3.up * 0.3f - pos).sqrMagnitude < 0.3f)
                {
                    soldier.TakeDamage(damage, DamageType.Arrow);
                    Destroy(gameObject);
                    return;
                }
            }
            else if (tower != null && !tower.IsDestroyed)
            {
                tower.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
            else if (gate != null && !gate.IsDestroyed)
            {
                gate.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            // yere saplanır ve kaybolur
            stuckTimer = 0f;
            if (sr != null) sr.sortingOrder = Iso.Order(pos, 2);
        }
    }
}
