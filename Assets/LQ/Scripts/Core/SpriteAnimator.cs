using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LQ {
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
