using UnityEngine;

namespace LQ {
    /// <summary>Unified input: touch HUD writes here, keyboard/mouse is read as a fallback (editor / desktop testing).</summary>
    public static class GameInput {
        // written by TouchControls
        public static Vector2 touchMove;
        public static Vector2 touchLookDelta;   // degrees this frame
        public static bool touchFire, touchJump;
        public static bool touchNextWeapon, touchPrevWeapon;
        public static bool touchUse;
        public static bool uiBlocked;             // menus open

        public static float mouseSensitivity = 2.0f;
        public static float touchSensitivity = 0.35f;  // degrees per pixel (scaled by DPI)
        public static bool invertY;

        static bool jumpConsumed, nextConsumed, prevConsumed;
        static float lastFireTime;

        public static bool IsMobile => Application.isMobilePlatform;

        public static Vector2 Move {
            get {
                if (uiBlocked) return Vector2.zero;
                var kb = new Vector2(
                    (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
                var m = kb + touchMove;
                return m.sqrMagnitude > 1 ? m.normalized : m;
            }
        }

        public static Vector2 Look {
            get {
                if (uiBlocked) return Vector2.zero;
                var l = touchLookDelta;
                if (!IsMobile && Cursor.lockState == CursorLockMode.Locked) {
                    l += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;
                }
                if (invertY) l.y = -l.y;
                return l;
            }
        }

        public static bool Fire => !uiBlocked && (touchFire || (!IsMobile && Input.GetMouseButton(0)));
        public static bool JumpHeld => !uiBlocked && (touchJump || (!IsMobile && Input.GetKey(KeyCode.Space)));
        public static bool NextWeapon => !uiBlocked && (Take(ref touchNextWeapon) || (!IsMobile && (Input.GetKeyDown(KeyCode.E) || Input.GetAxis("Mouse ScrollWheel") > 0.01f)));
        public static bool PrevWeapon => !uiBlocked && (Take(ref touchPrevWeapon) || (!IsMobile && (Input.GetKeyDown(KeyCode.Q) || Input.GetAxis("Mouse ScrollWheel") < -0.01f)));

        static bool Take(ref bool flag) { if (!flag) return false; flag = false; return true; }

        public static int WeaponKey() {
            if (IsMobile || uiBlocked) return 0;
            for (int i = 1; i <= 8; i++) if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
            return 0;
        }

        /// <summary>Call at end of frame (from TouchControls.LateUpdate) to clear per-frame deltas.</summary>
        public static void EndFrame() { touchLookDelta = Vector2.zero; }
    }
}
