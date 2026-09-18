using UnityEngine;

namespace LQ {
    public class Flicker : MonoBehaviour {
        Light l; float baseI;
        void Start() { l = GetComponent<Light>(); if (l) baseI = l.intensity; }
        void Update() { if (l) l.intensity = baseI * (0.85f + Mathf.PerlinNoise(Time.time * 6f, transform.position.x) * 0.3f); }
    }
}
