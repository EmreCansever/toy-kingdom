using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Haritada hedef şehre yürüyen rakip ordusu: üç küçük şövalye, rakip renginde, varış zamanına göre ilerler.</summary>
    public class MapRivalArmy : MonoBehaviour
    {
        public string rivalId;
        List<Vector3> route;
        readonly List<SpriteAnimator> men = new List<SpriteAnimator>();
        SpriteRenderer ring;

        public static MapRivalArmy Create(RivalState r, List<Vector3> route)
        {
            var art = GameArt.Instance;
            var go = new GameObject("RivalArmy_" + r.id);
            var a = go.AddComponent<MapRivalArmy>();
            a.rivalId = r.id;
            a.route = route;
            var def = RivalAI.Get(r.id);

            if (art != null && art.circle != null)
            {
                a.ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
                a.ring.transform.SetParent(go.transform, false);
                a.ring.sprite = art.circle;
                a.ring.color = new Color(def.color.r, def.color.g, def.color.b, 0.35f);
                a.ring.transform.localScale = new Vector3(1.3f, 0.75f, 1f);
                a.ring.sortingOrder = -15000;
            }
            for (int i = 0; i < 3; i++)
            {
                var m = new GameObject("Man").transform;
                m.SetParent(go.transform, false);
                m.localPosition = new Vector3((i - 1) * 0.22f, (i == 1 ? 0.08f : -0.04f), 0f);
                var sr = m.gameObject.AddComponent<SpriteRenderer>();
                sr.color = Color.Lerp(def.color, Color.white, 0.25f);
                m.localScale = Vector3.one * 0.8f;
                var anim = m.gameObject.AddComponent<SpriteAnimator>();
                anim.target = sr;
                anim.set = art != null ? art.knight : null;
                anim.Play(AnimType.Walk, true);
                m.gameObject.AddComponent<YSort>().bias = 7;
                a.men.Add(anim);
            }
            return a;
        }

        void Update()
        {
            var r = SaveSystem.Data.Rival(rivalId);
            if (r == null || !r.Attacking || route == null || route.Count < 2)
            {
                Destroy(gameObject);
                return;
            }
            float total = Mathf.Max(1f, r.attackArriveTicks - r.attackStartTicks);
            float t = Mathf.Clamp01((DateTime.UtcNow.Ticks - r.attackStartTicks) / total);

            // rota boyunca konum
            float length = 0f;
            for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i - 1], route[i]);
            float d = t * length;
            Vector3 pos = route[route.Count - 1], dir = Vector3.right;
            for (int i = 1; i < route.Count; i++)
            {
                float seg = Vector3.Distance(route[i - 1], route[i]);
                if (d <= seg || i == route.Count - 1)
                {
                    pos = Vector3.Lerp(route[i - 1], route[i], seg > 0f ? Mathf.Clamp01(d / seg) : 1f);
                    dir = route[i] - route[i - 1];
                    break;
                }
                d -= seg;
            }
            transform.position = pos;
            foreach (var m in men) if (m != null) m.Face(dir);
            if (ring != null) ring.transform.localScale = new Vector3(1.3f, 0.75f, 1f) * (1f + 0.08f * Mathf.Sin(Time.time * 5f));
        }
    }
}
