using System;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>8 yönlü sprite animasyonunu oynatır.</summary>
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteRenderer target;
        public CharacterAnimSet set;
        public float speed = 1f;

        public AnimType Current { get; private set; } = AnimType.Idle;
        public int Direction { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Saldırı animasyonu vuruş karesine geldiğinde çağrılır.</summary>
        public event Action Hit;

        float time;
        bool hitFired;

        void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        public void Play(AnimType type, bool restart = false)
        {
            if (type == Current && !restart && !(Finished && type == AnimType.Attack)) return;
            Current = type;
            time = 0f;
            Finished = false;
            hitFired = false;
            Apply();
        }

        public void Face(Vector2 screenDir)
        {
            if (screenDir.sqrMagnitude < 0.0001f) return;
            int d = Iso.DirectionIndex(screenDir);
            if (d == Direction) return;
            Direction = d;
            Apply();
        }

        public void SetDirection(int dir)
        {
            Direction = ((dir % 8) + 8) % 8;
            Apply();
        }

        void Update()
        {
            if (set == null || Finished) return;
            var clip = set.Get(Current);
            if (clip == null || !clip.IsValid) return;

            time += Time.deltaTime * speed;
            float duration = clip.Duration;

            if (Current == AnimType.Attack && !hitFired && time >= duration * set.attackHitPoint)
            {
                hitFired = true;
                Hit?.Invoke();
            }

            if (time >= duration)
            {
                if (clip.loop)
                {
                    time %= duration;
                    hitFired = false;
                }
                else
                {
                    time = duration;
                    Finished = true;
                }
            }
            Apply();
        }

        void Apply()
        {
            if (set == null || target == null) return;
            var clip = set.Get(Current);
            if (clip == null || !clip.IsValid) return;
            int frame = Mathf.Min(clip.framesPerDirection - 1, Mathf.FloorToInt(time * clip.fps));
            target.sprite = clip.Get(Direction, frame);
        }
    }
}
