using UnityEngine;

namespace LQ {
    /// <summary>Moves an object upward with a slight wobble and destroys it after `life` seconds (bubbles).</summary>
    public class RiseAndDie : MonoBehaviour {
        public float speed = 0.6f, life = 3f; float t;
        void Update() {
            transform.position += Vector3.up * speed * Time.deltaTime + new Vector3(Mathf.Sin(Time.time * 3f), 0, Mathf.Cos(Time.time * 2.3f)) * 0.15f * Time.deltaTime;
            if ((t += Time.deltaTime) > life) Destroy(gameObject);
        }
    }
}
