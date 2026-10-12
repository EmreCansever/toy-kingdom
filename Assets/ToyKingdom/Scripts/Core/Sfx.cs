using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Ses ve müzik. Dosyalar Resources/Audio altında (hepsi CC0: RandomMind müzikleri, Kenney, StarNinjas, Thimras, artisticdude).
    /// Müzik sahneler arasında kesilmeden yumuşakça geçer; kalabalık savaşta aynı ses art arda çok çalmasın diye sınır vardır.
    /// Ayarlardaki müzik / efekt düğmelerine uyar.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx _i;
        static Sfx I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _i = go.AddComponent<Sfx>();
                    _i.Init();
                }
                return _i;
            }
        }

        AudioSource musicA, musicB;
        AudioSource current;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        string currentMusic;
        string nextMusic;
        Coroutine fade;

        const float MusicVolume = 0.42f;

        void Init()
        {
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.playOnAwake = false; m.loop = true; m.volume = 0f; }
            for (int i = 0; i < 14; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                pool.Add(s);
            }
        }

        static bool MusicOn => SettingsPanel.Get(SettingsPanel.MusicKey, true);
        static bool SfxOn => SettingsPanel.Get(SettingsPanel.SfxKey, true);

        AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
            return c;
        }

        // ------------------------------------------------------------------ efektler

        /// <summary>Kısa ses. minGap: aynı sesin en az kaç saniye arayla çalacağı (kalabalıkta gürültüyü önler).</summary>
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f, float minGap = 0.05f, float pitch = 1f)
        {
            if (!SfxOn || string.IsNullOrEmpty(name)) return;
            var i = I;
            float now = Time.unscaledTime;
            if (i.lastPlayed.TryGetValue(name, out float last) && now - last < minGap) return;
            var clip = i.Clip(name);
            if (clip == null) return;
            i.lastPlayed[name] = now;
            AudioSource src = null;
            foreach (var s in i.pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) return; // hepsi meşgul: bu sesi atla
            src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            src.PlayOneShot(clip, volume);
        }

        /// <summary>Numaralı ses ailesinden rastgele biri ("sfx_clash" + 1..10); aile için ortak aralık sınırı.</summary>
        public static void PlayAny(string prefix, int from, int to, float volume = 1f, float minGap = 0.06f)
        {
            if (!Ready(prefix, minGap)) return;
            Play(prefix + Random.Range(from, to + 1), volume, 0.08f, 0f);
        }

        /// <summary>Aile sınırını önceden kontrol eder (kalabalık savaş için).</summary>
        public static bool Ready(string family, float minGap)
        {
            if (!SfxOn) return false;
            var i = I;
            float now = Time.unscaledTime;
            if (i.lastPlayed.TryGetValue(family, out float last) && now - last < minGap) return false;
            i.lastPlayed[family] = now;
            return true;
        }

        public static void Click() => Play("ui_click", 0.55f, 0.04f, 0.04f);
        public static void Error() => Play("ui_error", 0.6f, 0f, 0.2f);
        public static void Coins(bool big = false) => Play(big ? "sfx_coins" : "sfx_coins2", big ? 0.8f : 0.45f, 0.06f, 0.3f);
        public static void Alert() => Play("ui_alert", 0.7f, 0f, 0.6f);

        // ------------------------------------------------------------------ müzik

        /// <summary>Müziği yumuşakça değiştirir. then: döngüsüz parça bitince geçilecek müzik.</summary>
        public static void Music(string name, bool loop = true, string then = null)
        {
            var i = I;
            i.nextMusic = then;
            if (i.currentMusic == name && i.current != null && i.current.isPlaying) return;
            i.currentMusic = name;
            var clip = i.Clip(name);
            var to = i.current == i.musicA ? i.musicB : i.musicA;
            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            if (clip != null) to.Play();
            if (i.fade != null) i.StopCoroutine(i.fade);
            i.fade = i.StartCoroutine(i.Crossfade(i.current, to, 1.2f));
            i.current = to;
        }

        public static void StopMusic()
        {
            var i = I;
            i.currentMusic = null;
            i.nextMusic = null;
            if (i.fade != null) i.StopCoroutine(i.fade);
            i.fade = i.StartCoroutine(i.Crossfade(i.current, null, 1f));
            i.current = null;
        }

        /// <summary>Ayar değişince müziğin sesini hemen uygular.</summary>
        public static void ApplySettings()
        {
            var i = I;
            if (i.current != null) i.current.volume = MusicOn ? MusicVolume : 0f;
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float time)
        {
            float t = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / time);
                if (from != null) from.volume = fromStart * (1f - k);
                if (to != null) to.volume = (MusicOn ? MusicVolume : 0f) * k;
                yield return null;
            }
            if (from != null) { from.Stop(); from.volume = 0f; }
            fade = null;
        }

        void Update()
        {
            if (current != null && fade == null) current.volume = MusicOn ? MusicVolume : 0f;
            // döngüsüz parça (zafer) bitti: sıradakine geç
            if (current != null && !current.loop && !current.isPlaying && fade == null && nextMusic != null)
            {
                string n = nextMusic;
                nextMusic = null;
                currentMusic = null;
                Music(n);
            }
        }
    }
}
