using System;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Kale kapısı. Canı sıfırlanınca kale düşer.</summary>
    public class CastleGate : MonoBehaviour
    {
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsDestroyed => Health <= 0f;
        public float Normalized => MaxHealth > 0f ? Health / MaxHealth : 0f;
        /// <summary>Saldıranların toplandığı nokta (kapının hemen önü).</summary>
        public Vector3 FrontPoint { get; private set; }
        /// <summary>Kapıdan dışarı doğru ızgara yönü.</summary>
        public Vector2 Outward { get; private set; }

        public event Action<CastleGate> Destroyed;
        public event Action HealthChanged;

        SpriteRenderer sr;
        Transform barFill;
        GameObject bar;
        Vector3 origin;
        float shake;
        float barTimer;

        public void Init(float health, Vector3 front, Vector2 outward, GameArt art)
        {
            MaxHealth = Health = Mathf.Max(1f, health);
            FrontPoint = front;
            Outward = outward;
            sr = GetComponent<SpriteRenderer>();
            origin = transform.position;

            // Can barı
            bar = new GameObject("HealthBar");
            bar.transform.SetParent(transform, false);
            bar.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            var bg = MakeBar(bar.transform, art.white, new Color(0.15f, 0.1f, 0.1f, 0.85f), 30000);
            bg.transform.localScale = new Vector3(0.8f, 0.09f, 1f);
            var fill = MakeBar(bar.transform, art.white, new Color(0.89f, 0.29f, 0.29f), 30001);
            fill.transform.localScale = new Vector3(0.8f, 0.09f, 1f);
            barFill = fill.transform;
            bar.SetActive(false);
        }

        static SpriteRenderer MakeBar(Transform parent, Sprite white, Color c, int order)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = white;
            r.color = c;
            r.sortingOrder = order;
            return r;
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            Health = Mathf.Max(0f, Health - amount);
            shake = 0.12f;
            barTimer = 3f;
            if (bar != null) bar.SetActive(true);
            if (barFill != null)
            {
                barFill.localScale = new Vector3(0.8f * Normalized, 0.09f, 1f);
                barFill.localPosition = new Vector3(-0.4f + 0.4f * Normalized, 0f, 0f);
            }
            HealthChanged?.Invoke();
            if (IsDestroyed)
            {
                if (sr != null) sr.color = new Color(0.45f, 0.4f, 0.4f);
                if (bar != null) bar.SetActive(false);
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
                if (barTimer <= 0f && bar != null) bar.SetActive(false);
            }
        }
    }
}
