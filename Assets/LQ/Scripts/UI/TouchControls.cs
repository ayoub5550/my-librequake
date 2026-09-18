using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LQ {
    /// <summary>On-screen controls for Android: dynamic left joystick, right-side drag to look, fire / jump / weapon buttons.</summary>
    public class TouchControls : MonoBehaviour {
        public static bool forceShow;   // for desktop testing
        RectTransform root; GameObject stickBase, stickKnob; RectTransform stickBaseRt, stickKnobRt;
        int moveFinger = -1, lookFinger = -1; Vector2 moveOrigin;
        float stickRadius = 110f;
        readonly List<HoldButton> buttons = new List<HoldButton>();
        HoldButton fireBtn, jumpBtn;
        Canvas canvas; bool visible;

        public void Build(RectTransform parent) {
            canvas = parent.GetComponentInParent<Canvas>();
            root = UIKit.Panel(parent, "Touch", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // joystick visuals
            stickBase = UIKit.Image(root, "StickBase", new Vector2(0, 0), new Vector2(200, 200), new Vector2(220, 220), Ring(64, 0.85f, 0.95f), new Color(1, 1, 1, 0.25f)).gameObject;
            stickKnob = UIKit.Image(root, "StickKnob", new Vector2(0, 0), new Vector2(200, 200), new Vector2(90, 90), Ring(64, 0f, 1f), new Color(1, 0.9f, 0.7f, 0.5f)).gameObject;
            stickBaseRt = stickBase.GetComponent<RectTransform>(); stickKnobRt = stickKnob.GetComponent<RectTransform>();
            stickBase.SetActive(false); stickKnob.SetActive(false);
            // buttons (right side)
            fireBtn = MakeButton("FIRE", new Vector2(1, 0), new Vector2(-150, 150), 150, new Color(0.8f, 0.2f, 0.1f, 0.55f), v => GameInput.touchFire = v);
            jumpBtn = MakeButton("JUMP", new Vector2(1, 0), new Vector2(-300, 60), 100, new Color(0.2f, 0.5f, 0.9f, 0.5f), v => GameInput.touchJump = v);
            MakeButton(">", new Vector2(1, 0), new Vector2(-60, 320), 76, new Color(1, 1, 1, 0.35f), v => { if (v) GameInput.touchNextWeapon = true; });
            MakeButton("<", new Vector2(1, 0), new Vector2(-60, 410), 76, new Color(1, 1, 1, 0.35f), v => { if (v) GameInput.touchPrevWeapon = true; });
            SetVisible(false);
        }

        HoldButton MakeButton(string label, Vector2 anchor, Vector2 pos, float size, Color color, System.Action<bool> onHold) {
            var img = UIKit.Image(root, "Btn" + label, anchor, pos, new Vector2(size, size), Ring(64, 0f, 1f), color);
            img.raycastTarget = true;
            var t = UIKit.Text(img.transform, "L", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), label, Mathf.RoundToInt(size * 0.22f));
            var hb = img.gameObject.AddComponent<HoldButton>(); hb.onHold = onHold;
            buttons.Add(hb);
            return hb;
        }

        static Sprite Ring(int size, float innerFrac, float outerFrac) {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size]; float c = size * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                bool inside = d <= outerFrac && d >= innerFrac;
                float edge = Mathf.Clamp01((outerFrac - d) * size * 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 * edge : 0));
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public void SetVisible(bool v) { visible = v; root.gameObject.SetActive(v); }

        float DegPerPixel => GameInput.touchSensitivity * (160f / Mathf.Max(100f, Screen.dpi > 0 ? Screen.dpi : 320f));

        void Update() {
            if (!visible || GameInput.uiBlocked) { GameInput.touchMove = Vector2.zero; return; }
            bool moveSeen = false, lookSeen = false;
            for (int i = 0; i < Input.touchCount; i++) {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began) {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                    if (t.position.x < Screen.width * 0.4f && moveFinger < 0) { moveFinger = t.fingerId; moveOrigin = t.position; ShowStick(t.position); }
                    else if (lookFinger < 0) lookFinger = t.fingerId;
                }
                if (t.fingerId == moveFinger) {
                    moveSeen = true;
                    var d = (t.position - moveOrigin) / (stickRadius * canvas.scaleFactor);
                    if (d.magnitude > 1) d.Normalize();
                    GameInput.touchMove = d;
                    stickKnobRt.anchoredPosition = stickBaseRt.anchoredPosition + d * stickRadius * 0.6f;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { moveFinger = -1; GameInput.touchMove = Vector2.zero; stickBase.SetActive(false); stickKnob.SetActive(false); }
                }
                if (t.fingerId == lookFinger) {
                    lookSeen = true;
                    GameInput.touchLookDelta += new Vector2(t.deltaPosition.x, t.deltaPosition.y) * DegPerPixel;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) lookFinger = -1;
                }
            }
            if (!moveSeen && moveFinger >= 0) { moveFinger = -1; GameInput.touchMove = Vector2.zero; stickBase.SetActive(false); stickKnob.SetActive(false); }
            if (!lookSeen) lookFinger = -1;
#if UNITY_EDITOR || UNITY_STANDALONE
            // mouse emulation of the look drag for testing touch UI on desktop
            if (forceShow && Input.GetMouseButton(1)) GameInput.touchLookDelta += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
#endif
        }

        void LateUpdate() { GameInput.EndFrame(); }

        /// <summary>Scripted demo: show the joystick at a fixed spot with the given direction (or hide it).</summary>
        public void DemoStick(Vector2 dir, bool active) {
            if (!active) { stickBase.SetActive(false); stickKnob.SetActive(false); return; }
            if (!stickBase.activeSelf) ShowStick(new Vector2(Screen.width * 0.18f, Screen.height * 0.3f));
            stickKnobRt.anchoredPosition = stickBaseRt.anchoredPosition + dir * stickRadius * 0.6f;
        }
        public void DemoHold(string label, bool held) { foreach (var b in buttons) if (b.name == "Btn" + label) b.SetHeld(held); }
        public static TouchControls Instance { get; private set; }
        void Awake() { Instance = this; }

        void ShowStick(Vector2 screenPos) {
            stickBase.SetActive(true); stickKnob.SetActive(true);
            var local = screenPos / canvas.scaleFactor;
            stickBaseRt.anchoredPosition = local; stickKnobRt.anchoredPosition = local;
        }
    }

}
