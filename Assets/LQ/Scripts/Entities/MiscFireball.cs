using System.Collections;
using UnityEngine;

namespace LQ {
    /// <summary>misc_fireball: lava vent. Every 0–5 s throws a lavaball upward (`speed`, default 1000) that burns for 20 dmg on touch.</summary>
    public class MiscFireball : MonoBehaviour {
        QEntity ent;
        public static void Attach(GameObject go, QEntity e) { go.AddComponent<MiscFireball>().ent = e; }
        void Start() { StartCoroutine(Loop()); }
        IEnumerator Loop() {
            float speed = ent.GetFloat("speed", 1000f);
            while (true) {
                yield return new WaitForSeconds(Random.Range(0f, 5f));
                var vel = new Vector3(Random.Range(-100f, 100f), speed + Random.Range(0f, 200f), Random.Range(-100f, 100f)) * QuakeUnits.Scale;
                var p = Projectile.Create("progs/lavaball.mdl", ent.Origin, vel, gameObject);
                p.damage = 20; p.gravity = true; p.life = 5f; p.radius = 0;
                p.AddTrail(new Color(1f, 0.45f, 0.1f));
            }
        }
    }
}
