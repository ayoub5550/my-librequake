using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Plays Quake sounds from Resources/sound/... ("weapons/guncock.wav" -&gt; sound/weapons/guncock).</summary>
    public static class SoundBank {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static GameObject oneShotHolder;

        public static AudioClip Get(string quakeSound) {
            if (string.IsNullOrEmpty(quakeSound)) return null;
            if (cache.TryGetValue(quakeSound, out var c)) return c;
            var path = "sound/" + quakeSound;
            if (path.EndsWith(".wav")) path = path.Substring(0, path.Length - 4);
            c = Resources.Load<AudioClip>(path);
            if (c == null && !Application.isBatchMode) Debug.LogWarning("SoundBank: missing " + quakeSound); // headless Editor runs have audio disabled: every clip loads as null there
            cache[quakeSound] = c;
            return c;
        }

        public static void Play(string quakeSound, Vector3 pos, float volume = 1f, float maxDistance = 40f) {
            var clip = Get(quakeSound);
            if (clip == null) return;
            var go = new GameObject("snd:" + quakeSound);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.volume = volume; src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f; src.maxDistance = maxDistance; src.dopplerLevel = 0;
            src.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        /// <summary>Non-positional (player's own weapon, UI, item pickup).</summary>
        public static void Play2D(string quakeSound, float volume = 1f) {
            var clip = Get(quakeSound);
            if (clip == null) return;
            if (oneShotHolder == null) { oneShotHolder = new GameObject("snd2d"); Object.DontDestroyOnLoad(oneShotHolder); }
            var src = oneShotHolder.AddComponent<AudioSource>();
            src.clip = clip; src.volume = volume; src.spatialBlend = 0f; src.Play();
            Object.Destroy(src, clip.length + 0.1f);
        }

        public static AudioSource Loop(GameObject on, string quakeSound, float volume = 1f, float maxDistance = 25f) {
            var clip = Get(quakeSound);
            if (clip == null) return null;
            var src = on.AddComponent<AudioSource>();
            src.clip = clip; src.loop = true; src.volume = volume; src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1.5f; src.maxDistance = maxDistance; src.dopplerLevel = 0;
            src.Play();
            return src;
        }
    }
}
