using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LQ {
    /// <summary>A loaded Quake .mdl: one Unity Mesh per frame (vertex animation) plus skins.</summary>
    public class MdlModel {
        public string name;
        public Mesh[] frames;
        public string[] frameNames;
        public Texture2D[] skins;
        public Material[] skinMaterials;
        public Vector3 eyePosition;
        public int flags;
        public Bounds bounds;
        public const int EF_ROCKET = 1, EF_GRENADE = 2, EF_GIB = 4, EF_ROTATE = 8, EF_TRACER = 16, EF_ZOMGIB = 32, EF_TRACER2 = 64, EF_TRACER3 = 128;

        public int FindFrame(string frameName) {
            for (int i = 0; i < frameNames.Length; i++) if (frameNames[i] == frameName) return i;
            return -1;
        }
    }

    /// <summary>Parses Quake IDPO v6 models from Resources/progs/*.mdl.bytes.</summary>
    public static class MdlLoader {
        static readonly Dictionary<string, MdlModel> cache = new Dictionary<string, MdlModel>();
        static Shader modelShader;

        public static Shader ModelShader {
            get {
                if (modelShader == null) modelShader = Shader.Find("LQ/Model");
                if (modelShader == null) modelShader = Shader.Find("Unlit/Texture");
                return modelShader;
            }
        }

        /// <summary>Load "progs/soldier.mdl" (path relative to the Quake game dir, with extension).</summary>
        public static MdlModel Load(string quakePath) {
            if (cache.TryGetValue(quakePath, out var m)) return m;
            var ta = Resources.Load<TextAsset>(quakePath);
            if (ta == null) {
                Debug.LogWarning("MdlLoader: missing " + quakePath);
                cache[quakePath] = null;
                return null;
            }
            try {
                m = Parse(ta.bytes, quakePath);
            } catch (Exception e) {
                Debug.LogError("MdlLoader: failed " + quakePath + ": " + e);
                m = null;
            }
            cache[quakePath] = m;
            return m;
        }

        public static MdlModel Parse(byte[] data, string name) {
            var br = new BinaryReader(new MemoryStream(data));
            var ident = Encoding.ASCII.GetString(br.ReadBytes(4));
            int version = br.ReadInt32();
            if (ident != "IDPO" || version != 6) throw new Exception("not an IDPO v6 model: " + ident + " v" + version);
            var scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            var translate = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            br.ReadSingle(); // bounding radius
            var eye = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            int numSkins = br.ReadInt32(), skinW = br.ReadInt32(), skinH = br.ReadInt32();
            int numVerts = br.ReadInt32(), numTris = br.ReadInt32(), numFrames = br.ReadInt32();
            br.ReadInt32(); // synctype
            int flags = br.ReadInt32();
            br.ReadSingle(); // size

            var model = new MdlModel { name = name, flags = flags, eyePosition = QuakeUnits.ToUnity(eye.x, eye.y, eye.z) };

            // skins (only first sub-skin of a skin group is used)
            model.skins = new Texture2D[numSkins];
            for (int s = 0; s < numSkins; s++) {
                int group = br.ReadInt32();
                int count = 1;
                if (group != 0) {
                    count = br.ReadInt32();
                    br.ReadBytes(4 * count); // intervals
                }
                long pos = br.BaseStream.Position;
                model.skins[s] = QuakePalette.ToTexture(data, (int)pos, skinW, skinH, false);
                model.skins[s].name = name + "_skin" + s;
                br.BaseStream.Position = pos + (long)skinW * skinH * count;
            }

            var onseam = new int[numVerts]; var st = new Vector2Int[numVerts];
            for (int i = 0; i < numVerts; i++) { onseam[i] = br.ReadInt32(); st[i] = new Vector2Int(br.ReadInt32(), br.ReadInt32()); }
            var facesFront = new int[numTris]; var tri = new int[numTris * 3];
            for (int i = 0; i < numTris; i++) {
                facesFront[i] = br.ReadInt32();
                tri[i * 3] = br.ReadInt32(); tri[i * 3 + 1] = br.ReadInt32(); tri[i * 3 + 2] = br.ReadInt32();
            }

            // UVs are per triangle corner (seam handling), so we un-share vertices: 3 unique vertices per triangle.
            var uvs = new Vector2[numTris * 3];
            for (int t = 0; t < numTris; t++) {
                for (int c = 0; c < 3; c++) {
                    int v = tri[t * 3 + c];
                    float s = st[v].x, tt = st[v].y;
                    if (facesFront[t] == 0 && onseam[v] != 0) s += skinW * 0.5f;
                    uvs[t * 3 + c] = new Vector2((s + 0.5f) / skinW, 1f - (tt + 0.5f) / skinH);
                }
            }

            var frames = new List<Mesh>(); var frameNames = new List<string>();
            var rawVerts = new byte[numVerts * 4];
            bool flipDecided = false, flip = false;
            var bounds = new Bounds();
            for (int f = 0; f < numFrames; f++) {
                int type = br.ReadInt32();
                int sub = 1;
                if (type != 0) {
                    sub = br.ReadInt32();
                    br.ReadBytes(8);           // group bbox min/max
                    br.ReadBytes(4 * sub);     // intervals
                }
                for (int k = 0; k < sub; k++) {
                    br.ReadBytes(8); // bbox min/max
                    var fname = Encoding.ASCII.GetString(br.ReadBytes(16)).Split('\0')[0];
                    br.Read(rawVerts, 0, numVerts * 4);
                    if (k > 0) continue; // only first sub-frame of a frame group
                    var pos = new Vector3[numTris * 3];
                    for (int t = 0; t < numTris; t++) {
                        for (int c = 0; c < 3; c++) {
                            int v = tri[t * 3 + c];
                            float qx = rawVerts[v * 4] * scale.x + translate.x;
                            float qy = rawVerts[v * 4 + 1] * scale.y + translate.y;
                            float qz = rawVerts[v * 4 + 2] * scale.z + translate.z;
                            pos[t * 3 + c] = QuakeUnits.ToUnity(qx, qy, qz);
                        }
                    }
                    if (!flipDecided) { flip = ShouldFlipWinding(pos, numTris); flipDecided = true; }
                    var indices = new int[numTris * 3];
                    for (int t = 0; t < numTris; t++) {
                        indices[t * 3] = t * 3;
                        indices[t * 3 + 1] = flip ? t * 3 + 2 : t * 3 + 1;
                        indices[t * 3 + 2] = flip ? t * 3 + 1 : t * 3 + 2;
                    }
                    var mesh = new Mesh { name = name + ":" + fname };
                    if (pos.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = pos; mesh.uv = uvs; mesh.triangles = indices;
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (f == 0) bounds = mesh.bounds; else bounds.Encapsulate(mesh.bounds);
                    frames.Add(mesh); frameNames.Add(fname);
                }
            }
            model.frames = frames.ToArray(); model.frameNames = frameNames.ToArray(); model.bounds = bounds;
            model.skinMaterials = new Material[numSkins];
            for (int s = 0; s < numSkins; s++) {
                model.skinMaterials[s] = new Material(ModelShader) { mainTexture = model.skins[s], name = name + "_mat" + s };
            }
            return model;
        }

        static bool ShouldFlipWinding(Vector3[] pos, int numTris) {
            var center = Vector3.zero;
            for (int i = 0; i < pos.Length; i++) center += pos[i];
            center /= Mathf.Max(1, pos.Length);
            int outward = 0, inward = 0;
            for (int t = 0; t < numTris; t++) {
                var a = pos[t * 3]; var b = pos[t * 3 + 1]; var c = pos[t * 3 + 2];
                var n = Vector3.Cross(b - a, c - a);
                var toC = (a + b + c) / 3f - center;
                if (Vector3.Dot(n, toC) >= 0) outward++; else inward++;
            }
            return inward > outward;
        }
    }
}
