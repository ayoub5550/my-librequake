using UnityEngine;

namespace LQ {
    /// <summary>Quake &lt;-&gt; Unity conversions. Quake is Z-up, 32 units = 1 m. Same convention as Scopa (x, z, y).</summary>
    public static class QuakeUnits {
        public const float Scale = 1f / 32f;

        public static Vector3 ToUnity(float qx, float qy, float qz) => new Vector3(qx * Scale, qz * Scale, qy * Scale);
        public static Vector3 ToUnityDir(float qx, float qy, float qz) => new Vector3(qx, qz, qy);
        public static float U(float quakeUnits) => quakeUnits * Scale;

        /// <summary>Quake yaw angle (degrees, 0 = +X) to a Unity horizontal direction.</summary>
        public static Vector3 YawToDir(float yaw) {
            var q = Quaternion.Euler(0, -yaw + 90f, 0);
            return q * Vector3.forward;
        }

        /// <summary>Quake "angle" key: -1 = up, -2 = down, else yaw.</summary>
        public static Vector3 AngleToDir(float angle) {
            int a = Mathf.RoundToInt(angle);
            if (a == -1) return Vector3.up;
            if (a == -2) return Vector3.down;
            return YawToDir(angle);
        }

        public static bool TryParseVector3(string s, out Vector3 v) {
            v = Vector3.zero;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var parts = s.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out var x)) return false;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out var y)) return false;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out var z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }
    }
}

namespace LQ {
    /// <summary>GameObject helpers. Never write `GetComponent<T>() ?? AddComponent<T>()`:
    /// in the Editor GetComponent returns a fake-null object, so `??` never adds the component
    /// (MissingComponentException in headless playtests). Use GetOrAdd instead.</summary>
    public static class GoExt {
        public static T GetOrAdd<T>(this UnityEngine.GameObject go) where T : UnityEngine.Component {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
