using System.Collections;
using UnityEngine;

namespace LQ {
    /// <summary>event_lightning (end of E1): when triggered, lightning strikes between the entities named `lightning`
    /// six times over three seconds, killing anything in between, then fires its own targets.</summary>
    public class EventLightning : MonoBehaviour, IActivatable {
        QEntity ent; bool running;
        public static void Attach(GameObject go, QEntity e) { go.AddComponent<EventLightning>().ent = e; }
        public void Activate(GameObject activator) { if (!running) StartCoroutine(Run(activator)); }
        IEnumerator Run(GameObject activator) {
            running = true;
            Vector3 a = Vector3.zero, b = Vector3.zero; bool ok = false;
            var pts = QEntity.FindByTargetName("lightning");
            if (pts.Count >= 2) { a = pts[0].Origin; b = pts[1].Origin; ok = true; }
            else if (pts.Count == 1) { a = pts[0].Origin; b = a + Vector3.down * 8f; ok = true; }
            if (ok) {
                var seg = b - a; float len = Mathf.Max(0.01f, seg.magnitude); var n = seg / len;
                for (int i = 0; i < 6; i++) {
                    SoundBank.Play("weapons/lhit.wav", (a + b) * 0.5f, 1f, 80f);
                    var beam = new GameObject("event_lightning").AddComponent<LineRenderer>();
                    beam.material = new Material(Shader.Find("LQ/Particle") ?? Shader.Find("Sprites/Default"));
                    beam.startColor = beam.endColor = new Color(0.6f, 0.7f, 1f); beam.startWidth = beam.endWidth = 0.25f;
                    beam.positionCount = 2; beam.SetPosition(0, a); beam.SetPosition(1, b);
                    Destroy(beam.gameObject, 0.25f);
                    foreach (var h in Physics.SphereCastAll(a, 0.5f, n, len, ~0, QueryTriggerInteraction.Ignore)) {
                        var d = h.collider.GetComponentInParent<IDamageable>();
                        if (d != null && d.IsAlive) d.TakeDamage(new DamageInfo { amount = 200, attacker = gameObject, inflictor = gameObject, point = h.point, direction = n });
                    }
                    yield return new WaitForSeconds(0.5f);
                }
            }
            ent.FireTargets(activator);
            running = false;
        }
    }
}
