using System.Collections;
using UnityEngine;

namespace LQ {
    /// <summary>trap_spikeshooter (fires once per trigger) and trap_shooter (fires continuously every `wait` s).
    /// spawnflags 1 = superspike (18 dmg), 2 = laser (15 dmg). Direction from `angle`/`angles` (Quake SetMovedir).</summary>
    public class TrapSpikeshooter : MonoBehaviour, IActivatable {
        QEntity ent; Vector3 dir; bool laser, super_; bool continuous;
        const float SpikeSpeed = 500f, LaserSpeed = 600f;

        public static void Attach(GameObject go, QEntity e, bool continuous) {
            var t = go.AddComponent<TrapSpikeshooter>(); t.ent = e; t.continuous = continuous;
            t.super_ = e.HasFlag(1); t.laser = e.HasFlag(2);
            t.dir = e.MoveDir(Vector3.forward);
        }

        void Start() { if (continuous) StartCoroutine(Loop()); }

        IEnumerator Loop() {
            float wait = Mathf.Max(0.1f, ent.GetFloat("wait", 1f));
            yield return new WaitForSeconds(Random.Range(0f, wait));
            while (true) { Fire(); yield return new WaitForSeconds(wait); }
        }

        public void Activate(GameObject activator) => Fire();

        void Fire() {
            Vector3 pos = ent.Origin;
            if (laser) {
                var p = Projectile.Create("progs/laser.mdl", pos, dir * LaserSpeed * QuakeUnits.Scale, gameObject);
                p.damage = 15; p.life = 5f; p.hitSound = "enforcer/enfstop.wav";
                SoundBank.Play("enforcer/enfire.wav", pos);
            } else {
                var p = Projectile.Create(super_ ? "progs/s_spike.mdl" : "progs/spike.mdl", pos, dir * SpikeSpeed * QuakeUnits.Scale, gameObject);
                p.damage = super_ ? 18 : 9; p.life = 6f; p.hitSound = "weapons/tink1.wav";
                SoundBank.Play("weapons/spike2.wav", pos);
            }
        }
    }
}
