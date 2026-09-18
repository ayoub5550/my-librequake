using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LQ {
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
        public System.Action<bool> onHold; bool held; Image img; Color baseColor;
        /// <summary>Touch fingerId / pointerId currently pressing this button, or int.MinValue.</summary>
        public int pointerId = int.MinValue;
        public bool IsHeld => held;
        void Awake() { img = GetComponent<Image>(); baseColor = img.color; }
        public void OnPointerDown(PointerEventData e) { held = true; pointerId = e != null ? e.pointerId : int.MinValue; onHold?.Invoke(true); img.color = baseColor * 1.5f + new Color(0, 0, 0, 0.3f); }
        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { if (held && e.pointerPress == gameObject) { /* keep holding while finger slides off */ } }
        void Release() { if (!held) return; held = false; pointerId = int.MinValue; onHold?.Invoke(false); img.color = baseColor; }
        public void SetHeld(bool v) { if (v) OnPointerDown(null); else Release(); }
        void OnDisable() { Release(); }
    }
}
