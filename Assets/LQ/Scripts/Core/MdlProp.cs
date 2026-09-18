using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class MdlProp : MonoBehaviour {
        public bool rotate;
        public float bob;
        Vector3 basePos;
        void Start() { basePos = transform.localPosition; }
        void Update() {
            if (rotate) transform.Rotate(0, 100f * Time.deltaTime, 0, Space.World);
            if (bob > 0) transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * bob);
        }
    }
}
