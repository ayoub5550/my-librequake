using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public interface IActivatable { void Activate(GameObject activator); }

    /// <summary>Serializable copy of a Quake entity's key/values, attached at import time. Handles target/killtarget/message firing.</summary>
    public class QEntity : MonoBehaviour {
        public string classname;
        public int spawnflags;
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
        public bool isBrushEntity;

        static readonly Dictionary<string, List<QEntity>> byTargetName = new Dictionary<string, List<QEntity>>();
        public static readonly List<QEntity> AllEntities = new List<QEntity>();

        public string TargetName => Get("targetname");
        public string Target => Get("target");

        public static void ClearRegistry() { byTargetName.Clear(); AllEntities.Clear(); }
        /// <summary>Drop destroyed entries (safe to call any time, unlike ClearRegistry).</summary>
        public static void PruneRegistry() {
            AllEntities.RemoveAll(e => e == null);
            foreach (var l in byTargetName.Values) l.RemoveAll(e => e == null);
        }

        void Awake() {
            AllEntities.Add(this);
            var tn = TargetName;
            if (!string.IsNullOrEmpty(tn)) {
                if (!byTargetName.TryGetValue(tn, out var l)) byTargetName[tn] = l = new List<QEntity>();
                l.Add(this);
            }
        }

        void OnDestroy() {
            AllEntities.Remove(this);
            var tn = TargetName;
            if (!string.IsNullOrEmpty(tn) && byTargetName.TryGetValue(tn, out var l)) l.Remove(this);
        }

        public static List<QEntity> FindByTargetName(string name) {
            if (string.IsNullOrEmpty(name)) return null;
            byTargetName.TryGetValue(name, out var l);
            if (l != null) l.RemoveAll(e => e == null);
            if (l == null || l.Count == 0) {
                // fallback: scan the scene (covers entities whose Awake ran before the registry was ready)
                foreach (var q in FindObjectsOfType<QEntity>(true))
                    if (q.TargetName == name) { if (l == null) byTargetName[name] = l = new List<QEntity>(); if (!l.Contains(q)) l.Add(q); }
            }
            return l;
        }

        public void Set(string key, string value) {
            int i = keys.IndexOf(key);
            if (i >= 0) values[i] = value; else { keys.Add(key); values.Add(value); }
        }

        public bool Has(string key) => keys.IndexOf(key) >= 0;

        public string Get(string key, string def = null) {
            int i = keys.IndexOf(key);
            return i >= 0 ? values[i] : def;
        }

        public float GetFloat(string key, float def = 0) {
            var s = Get(key);
            if (s == null) return def;
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        public int GetInt(string key, int def = 0) => Mathf.RoundToInt(GetFloat(key, def));

        public bool HasFlag(int flag) => (spawnflags & flag) != 0;

        /// <summary>Quake vector key converted to Unity metres (x, z, y) / 32.</summary>
        public bool TryGetPosition(string key, out Vector3 v) {
            v = Vector3.zero;
            if (!QuakeUnits.TryParseVector3(Get(key), out var q)) return false;
            v = QuakeUnits.ToUnity(q.x, q.y, q.z);
            return true;
        }

        public Vector3 Origin {
            get { return TryGetPosition("origin", out var v) ? v : transform.position; }
        }

        /// <summary>Movement direction from the "angle" key (-1 up, -2 down), or "angles".</summary>
        public Vector3 MoveDir(Vector3 fallback) {
            if (Has("angle")) return QuakeUnits.AngleToDir(GetFloat("angle"));
            if (QuakeUnits.TryParseVector3(Get("angles"), out var a)) {
                // pitch yaw roll
                var rot = Quaternion.Euler(a.x, -a.y + 90f, a.z);
                return rot * Vector3.forward;
            }
            return fallback;
        }

        public float Yaw {
            get {
                if (Has("angle")) return -GetFloat("angle") + 90f;
                if (QuakeUnits.TryParseVector3(Get("angles"), out var a)) return -a.y + 90f;
                return 90f;
            }
        }

        /// <summary>SUB_UseTargets: fire target (with delay), killtarget, message.</summary>
        public void FireTargets(GameObject activator = null) {
            float delay = GetFloat("delay", 0);
            if (delay > 0) { StartCoroutine(FireDelayed(delay, activator)); return; }
            FireNow(activator);
        }

        System.Collections.IEnumerator FireDelayed(float d, GameObject activator) {
            yield return new WaitForSeconds(d);
            FireNow(activator);
        }

        void FireNow(GameObject activator) {
            var msg = Get("message");
            if (!string.IsNullOrEmpty(msg) && activator != null && activator.GetComponent<Player>() != null && !classname.StartsWith("trigger_secret") && classname != "trigger_multiple" && classname != "trigger_once") {
                HUD.CenterPrint(msg);
            }
            var kill = Get("killtarget");
            if (!string.IsNullOrEmpty(kill)) {
                var l = FindByTargetName(kill);
                if (l != null) foreach (var e in new List<QEntity>(l)) if (e != null) Destroy(e.gameObject);
            }
            var t = Target;
            if (string.IsNullOrEmpty(t)) return;
            var targets = FindByTargetName(t);
            if (targets == null) return;
            foreach (var e in new List<QEntity>(targets)) {
                if (e == null) continue;
                foreach (var a in e.GetComponents<IActivatable>()) a.Activate(activator);
            }
        }

        /// <summary>World-space bounds of this brush entity's renderers (or colliders).</summary>
        public Bounds GetBounds() {
            var rends = GetComponentsInChildren<Renderer>();
            bool any = false; var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in rends) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any) foreach (var c in GetComponentsInChildren<Collider>()) { if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds); }
            return b;
        }
    }

    /// <summary>Used by monsters to fire their "target" on death without depending on QEntity directly.</summary>
    public class ScopaEntityRef {
        public QEntity entity;
        public void FireTargets() { if (entity != null) entity.FireTargets(); }
    }
}
