using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Displays a Quake .mdl and plays frame sequences (10 fps like Quake, with optional interpolation off).</summary>
    public class MdlAnimator : MonoBehaviour {
        public MdlModel model;
        public float fps = 10f;
        public int skin;
        public bool interpolate;

        MeshFilter mf; MeshRenderer mr;
        List<AnimDB.Seq> sequences = new List<AnimDB.Seq>();
        AnimDB.Seq current; bool hasCurrent; bool loop; float time; int shownFrame = -1;
        public Action onSequenceEnd;
        public string CurrentSequence => hasCurrent ? current.name : null;
        public bool IsPlaying => hasCurrent && (loop || time < current.count);
        public int CurrentFrameInSequence => hasCurrent ? Mathf.Min(current.count - 1, Mathf.FloorToInt(time)) : 0;

        public static MdlAnimator Create(Transform parent, string mdlPath, string animModelName = null) {
            var model = MdlLoader.Load(mdlPath);
            if (model == null) return null;
            var go = new GameObject("mdl:" + mdlPath);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<MdlAnimator>();
            a.SetModel(model, animModelName ?? System.IO.Path.GetFileNameWithoutExtension(mdlPath));
            return a;
        }

        public void SetModel(MdlModel m, string animModelName) {
            model = m;
            EnsureComponents();
            var names = AnimDB.FrameNames(animModelName) ?? m.frameNames;
            sequences = AnimDB.Sequences(names);
            if (sequences.Count == 0) sequences.Add(new AnimDB.Seq { name = "all", start = 0, count = m.frames.Length });
            SetSkin(skin);
            ShowFrame(0);
        }

        void EnsureComponents() {
            if (mf == null) mf = gameObject.GetOrAdd<MeshFilter>();
            if (mr == null) {
                mr = gameObject.GetOrAdd<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        public void SetSkin(int s) {
            skin = Mathf.Clamp(s, 0, model.skinMaterials.Length - 1);
            if (mr != null) mr.sharedMaterial = model.skinMaterials[skin];
        }

        public bool HasSequence(string name) => FindSeq(name) >= 0;

        int FindSeq(string name) {
            for (int i = 0; i < sequences.Count; i++) if (sequences[i].name == name) return i;
            return -1;
        }

        /// <summary>Find the first sequence whose name starts with any of the given prefixes.</summary>
        public string FindSequence(params string[] prefixes) {
            foreach (var p in prefixes)
                foreach (var s in sequences)
                    if (s.name == p) return s.name;
            foreach (var p in prefixes)
                foreach (var s in sequences)
                    if (s.name.StartsWith(p)) return s.name;
            return null;
        }

        /// <summary>All sequences whose name starts with prefix (used to pick random pain/death anims).</summary>
        public List<string> FindSequences(string prefix) {
            var l = new List<string>();
            foreach (var s in sequences) if (s.name.StartsWith(prefix)) l.Add(s.name);
            return l;
        }

        public bool Play(string name, bool loop, Action onEnd = null, float speed = 10f) {
            int i = FindSeq(name);
            if (i < 0) return false;
            if (hasCurrent && current.name == name && this.loop && loop) return true; // already looping it
            current = sequences[i]; hasCurrent = true; this.loop = loop; time = 0; fps = speed;
            onSequenceEnd = onEnd;
            ShowFrame(current.start);
            return true;
        }

        public void Stop() { hasCurrent = false; onSequenceEnd = null; }

        public void ShowFrame(int absoluteFrame) {
            if (model == null || model.frames.Length == 0) return;
            absoluteFrame = Mathf.Clamp(absoluteFrame, 0, model.frames.Length - 1);
            if (absoluteFrame == shownFrame) return;
            shownFrame = absoluteFrame;
            mf.sharedMesh = model.frames[absoluteFrame];
        }

        void Update() {
            if (!hasCurrent) return;
            time += Time.deltaTime * fps;
            if (time >= current.count) {
                if (loop) time -= current.count;
                else {
                    time = current.count - 0.001f;
                    ShowFrame(current.start + current.count - 1);
                    hasCurrent = false;
                    var cb = onSequenceEnd; onSequenceEnd = null;
                    cb?.Invoke();
                    return;
                }
            }
            ShowFrame(current.start + Mathf.FloorToInt(time));
        }
    }

    /// <summary>Static model display for pickups; rotates if the model has EF_ROTATE or when forced.</summary>
}
