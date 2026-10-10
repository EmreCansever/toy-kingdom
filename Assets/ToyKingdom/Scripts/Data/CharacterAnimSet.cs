using System;
using UnityEngine;

namespace ToyKingdom
{
    public enum AnimType { Idle, Walk, Attack, Death }

    [Serializable]
    public class AnimClip
    {
        public int framesPerDirection;
        public float fps = 20f;
        public bool loop = true;
        [Tooltip("Yön sırasına göre dizilmiş kareler: [yön * kareSayısı + kare]")]
        public Sprite[] frames;

        public bool IsValid => frames != null && framesPerDirection > 0 && frames.Length >= framesPerDirection * 8;
        public float Duration => framesPerDirection / Mathf.Max(1f, fps);

        public Sprite Get(int dir, int frame)
        {
            dir = Mathf.Clamp(dir, 0, 7);
            frame = Mathf.Clamp(frame, 0, framesPerDirection - 1);
            return frames[dir * framesPerDirection + frame];
        }
    }

    /// <summary>8 yönlü karakter animasyonları.</summary>
    [CreateAssetMenu(menuName = "ToyKingdom/Karakter animasyonları", fileName = "Anim_")]
    public class CharacterAnimSet : ScriptableObject
    {
        public AnimClip idle;
        public AnimClip walk;
        public AnimClip attack;
        public AnimClip death;
        [Tooltip("Saldırı animasyonunun hangi oranında vuruş gerçekleşir")]
        [Range(0f, 1f)] public float attackHitPoint = 0.55f;

        public AnimClip Get(AnimType type)
        {
            switch (type)
            {
                case AnimType.Walk: return walk;
                case AnimType.Attack: return attack;
                case AnimType.Death: return death;
                default: return idle;
            }
        }
    }
}
