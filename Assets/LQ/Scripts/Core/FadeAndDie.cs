using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class FadeAndDie : MonoBehaviour {
        public float life = 0.5f;
        float t; Light l; float startIntensity;
        void Start() { l = GetComponent<Light>(); if (l) startIntensity = l.intensity; }
        void Update() {
            t += Time.deltaTime;
            if (l) l.intensity = Mathf.Lerp(startIntensity, 0, t / life);
            if (t >= life) Destroy(gameObject);
        }
    }
}
