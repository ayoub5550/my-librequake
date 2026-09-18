using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public enum LiquidType { None, Water, Slime, Lava, Teleport }

    /// <summary>Marks a brush entity (created at import from *water/*slime/*lava brushes) as a liquid volume.</summary>
    public class LiquidVolume : MonoBehaviour {
        public LiquidType type = LiquidType.Water;
        static readonly List<LiquidVolume> all = new List<LiquidVolume>();
        Collider[] cols; Bounds bounds;

        void Awake() { all.Add(this); }
        void OnDestroy() { all.Remove(this); }

        void Start() {
            cols = GetComponentsInChildren<Collider>();
            foreach (var c in cols) { c.isTrigger = true; c.gameObject.layer = LayerMask.NameToLayer("Trigger"); }
            bool first = true;
            foreach (var c in cols) { if (first) { bounds = c.bounds; first = false; } else bounds.Encapsulate(c.bounds); }
            // water surfaces are non-solid, no rigidbody needed
        }

        public bool Contains(Vector3 p) {
            if (cols == null || !bounds.Contains(p)) return false;
            foreach (var c in cols) {
                if (!c.bounds.Contains(p)) continue;
                if (c is MeshCollider mc && !mc.convex) { return true; }
                var cp = c.ClosestPoint(p);
                if ((cp - p).sqrMagnitude < 0.0004f) return true;
            }
            return false;
        }

        public static LiquidType TypeAt(Vector3 p) {
            foreach (var v in all) if (v.Contains(p)) return v.type;
            return LiquidType.None;
        }

        public static LiquidType FromTextureName(string tex) {
            tex = tex.ToLowerInvariant();
            if (tex.StartsWith("*lava")) return LiquidType.Lava;
            if (tex.StartsWith("*slime")) return LiquidType.Slime;
            if (tex.StartsWith("*tele")) return LiquidType.Teleport;
            if (tex.StartsWith("*")) return LiquidType.Water;
            return LiquidType.None;
        }
    }

    /// <summary>Worldspawn keys for the loaded level (message, worldtype, next map...).</summary>
    public class LevelInfo : MonoBehaviour {
        public static LevelInfo Current;
        public string mapName, message; public int worldtype; public int monsterCount, secretCount;
        public static int WorldType => Current != null ? Current.worldtype : 0;
        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }
    }
}
