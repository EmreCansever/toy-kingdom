using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Haritada yollar boyunca şehirden şehre yürüyen kahraman şövalye.</summary>
    public class MapHero : MonoBehaviour
    {
        public float speed = 2.2f;
        public SpriteAnimator anim;

        public bool IsMoving => route.Count > 0;
        public MapCity CurrentCity { get; private set; }

        readonly List<Vector3> route = new List<Vector3>();
        Action onArrive;
        MapCity destination;
        Vector2 lastDir = new Vector2(1f, -1f);

        public void PlaceAt(MapCity city)
        {
            CurrentCity = city;
            transform.position = city.doorPoint;
            route.Clear();
            if (anim != null) { anim.SetDirection(0); anim.Play(AnimType.Idle, true); }
        }

        public void Travel(List<Vector3> points, MapCity target, Action arrived)
        {
            route.Clear();
            route.AddRange(points);
            destination = target;
            onArrive = arrived;
            if (route.Count == 0) Arrive();
        }

        void Update()
        {
            if (route.Count == 0) return;
            Vector3 next = route[0];
            Vector3 to = next - transform.position;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                transform.position = next;
                route.RemoveAt(0);
                if (route.Count == 0) Arrive();
            }
            else
            {
                transform.position += to.normalized * step;
                lastDir = Vector2.Lerp(lastDir, ((Vector2)to).normalized, 0.4f);
                if (anim != null) { anim.Face(lastDir); anim.Play(AnimType.Walk); }
            }
        }

        void Arrive()
        {
            CurrentCity = destination;
            if (anim != null) anim.Play(AnimType.Idle);
            var cb = onArrive;
            onArrive = null;
            cb?.Invoke();
        }
    }
}
