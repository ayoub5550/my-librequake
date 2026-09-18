using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LQ {
    public class SprModel {
        public string name;
        public Texture2D[] frames;
        public Vector2[] origins;   // in Quake units: (x, y) offset of top-left from center
        public Vector2Int[] sizes;
        public float[] intervals;
        public Material[] materials;
    }

    /// <summary>Parses Quake IDSP v1 sprites (explosions, bubbles...).</summary>
    public static class SprLoader {
        static readonly Dictionary<string, SprModel> cache = new Dictionary<string, SprModel>();

        public static SprModel Load(string quakePath) {
            if (cache.TryGetValue(quakePath, out var m)) return m;
            var ta = Resources.Load<TextAsset>(quakePath);
            if (ta == null) { Debug.LogWarning("SprLoader: missing " + quakePath); cache[quakePath] = null; return null; }
            try { m = Parse(ta.bytes, quakePath); } catch (Exception e) { Debug.LogError("SprLoader failed " + quakePath + ": " + e); m = null; }
            cache[quakePath] = m;
            return m;
        }

        public static SprModel Parse(byte[] data, string name) {
            var br = new BinaryReader(new MemoryStream(data));
            var ident = Encoding.ASCII.GetString(br.ReadBytes(4));
            int version = br.ReadInt32();
            if (ident != "IDSP" || version != 1) throw new Exception("not an IDSP v1 sprite");
            br.ReadInt32(); br.ReadSingle(); br.ReadInt32(); br.ReadInt32(); // type, radius, maxwidth, maxheight
            int numFrames = br.ReadInt32();
            br.ReadSingle(); br.ReadInt32(); // beamlength, synctype
            var frames = new List<Texture2D>(); var origins = new List<Vector2>(); var sizes = new List<Vector2Int>(); var intervals = new List<float>();
            for (int f = 0; f < numFrames; f++) {
                int type = br.ReadInt32();
                int count = 1; float[] iv = null;
                if (type != 0) {
                    count = br.ReadInt32(); iv = new float[count];
                    for (int i = 0; i < count; i++) iv[i] = br.ReadSingle();
                }
                for (int k = 0; k < count; k++) {
                    int ox = br.ReadInt32(), oy = br.ReadInt32(), w = br.ReadInt32(), h = br.ReadInt32();
                    long pos = br.BaseStream.Position;
                    var tex = QuakePalette.ToTexture(data, (int)pos, w, h, true);
                    tex.name = name + "_" + frames.Count;
                    br.BaseStream.Position = pos + (long)w * h;
                    frames.Add(tex); origins.Add(new Vector2(ox, oy)); sizes.Add(new Vector2Int(w, h));
                    intervals.Add(iv != null ? iv[k] : 0.1f);
                }
            }
            var shader = Shader.Find("LQ/Sprite") ?? Shader.Find("Sprites/Default");
            var m = new SprModel { name = name, frames = frames.ToArray(), origins = origins.ToArray(), sizes = sizes.ToArray(), intervals = intervals.ToArray() };
            m.materials = new Material[m.frames.Length];
            for (int i = 0; i < m.frames.Length; i++) m.materials[i] = new Material(shader) { mainTexture = m.frames[i] };
            return m;
        }
    }

    /// <summary>Plays a sprite once (or looping) on a camera-facing quad.</summary>
    public class SpriteAnimator : MonoBehaviour {
        public SprModel sprite;
        public bool loop;
        public float fps = 10f;
        public float scale = 1f;
        float t; int frame = -1;
        MeshRenderer mr; MeshFilter mf;

        public static SpriteAnimator Spawn(string sprPath, Vector3 pos, bool loop = false, float fps = 10f, float scale = 1f) {
            var spr = SprLoader.Load(sprPath);
            if (spr == null) return null;
            var go = new GameObject("spr:" + sprPath);
            go.transform.position = pos;
            var sa = go.AddComponent<SpriteAnimator>();
            sa.sprite = spr; sa.loop = loop; sa.fps = fps; sa.scale = scale;
            return sa;
        }

        void Awake() {
            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        void Update() {
            if (sprite == null) return;
            t += Time.deltaTime * fps;
            int f = Mathf.FloorToInt(t);
            if (f >= sprite.frames.Length) {
                if (loop) { f %= sprite.frames.Length; } else { Destroy(gameObject); return; }
            }
            if (f != frame) {
                frame = f;
                mr.sharedMaterial = sprite.materials[f];
                var sz = sprite.sizes[f];
                transform.localScale = new Vector3(sz.x * QuakeUnits.Scale * scale, sz.y * QuakeUnits.Scale * scale, 1f);
            }
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
