using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Frame-name tables extracted from the QuakeC $frame macros (Resources/anims.json).
    /// Sequences are groups of consecutive frames sharing an alphabetic prefix, e.g. stand1..stand8 -> "stand".</summary>
    public static class AnimDB {
        [Serializable] class Entry { public string model; public string[] frames; }
        [Serializable] class Root { public Entry[] models; }

        static Dictionary<string, string[]> table;

        public static string[] FrameNames(string model) {
            if (table == null) Load();
            table.TryGetValue(model, out var f);
            return f;
        }

        static void Load() {
            table = new Dictionary<string, string[]>();
            var ta = Resources.Load<TextAsset>("anims");
            if (ta == null) { Debug.LogWarning("AnimDB: anims.json missing"); return; }
            var root = JsonUtility.FromJson<Root>(ta.text);
            foreach (var e in root.models) table[e.model] = e.frames;
        }

        public struct Seq { public string name; public int start; public int count; }

        /// <summary>Split a frame list into named sequences by stripping trailing digits.</summary>
        public static List<Seq> Sequences(string[] frames) {
            var list = new List<Seq>();
            if (frames == null) return list;
            string cur = null; int start = 0;
            for (int i = 0; i <= frames.Length; i++) {
                string prefix = i < frames.Length ? Prefix(frames[i]) : null;
                if (prefix != cur) {
                    if (cur != null) list.Add(new Seq { name = cur, start = start, count = i - start });
                    cur = prefix; start = i;
                }
            }
            return list;
        }

        public static string Prefix(string frame) {
            int end = frame.Length;
            while (end > 0 && char.IsDigit(frame[end - 1])) end--;
            return frame.Substring(0, end).ToLowerInvariant();
        }
    }
}
