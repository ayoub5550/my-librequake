using UnityEngine;

namespace LQ {
    /// <summary>Rockets, grenades, nails, lasers, monster projectiles. Swept with raycasts (no rigidbody).</summary>
    public class Projectile : MonoBehaviour {
        public Vector3 velocity;
        public float damage, radius;           // radius in Quake units (0 = no explosion)
        public bool gravity, bounce, explodeOnTimer;
        public float life = 5f;
        public GameObject owner;
        public string hitSound, bounceSound;
        public bool homing; public Transform homingTarget;
        float born; bool dead;
        MdlAnimator anim;

        public static Projectile Spawn(WeaponDef def, Vector3 pos, Vector3 vel, GameObject owner, float mult) {
            var p = Create(def.projectileModel, pos, vel, owner);
            p.damage = def.damage * mult; p.radius = def.radius;
            if (def.arc) { p.gravity = true; p.bounce = true; p.explodeOnTimer = true; p.life = 2.5f; p.bounceSound = "weapons/bounce.wav"; }
            if (def.id == WeaponDefs.Nailgun || def.id == WeaponDefs.SuperNailgun) { p.hitSound = "weapons/tink1.wav"; p.life = 6f; }
            if (def.id == WeaponDefs.RocketLauncher) p.AddTrail(new Color(1f, 0.5f, 0.2f));
            return p;
        }

        public static Projectile Create(string model, Vector3 pos, Vector3 vel, GameObject owner) {
            var go = new GameObject("proj:" + model);
            go.transform.position = pos;
            var p = go.AddComponent<Projectile>();
            p.velocity = vel; p.owner = owner; p.born = Time.time;
            var m = MdlLoader.Load(model);
            if (m != null) {
                var holder = new GameObject("model").transform; holder.SetParent(go.transform, false);
                holder.localRotation = Quaternion.Euler(0, -90f, 0);
                p.anim = holder.gameObject.AddComponent<MdlAnimator>();
                p.anim.SetModel(m, System.IO.Path.GetFileNameWithoutExtension(model));
                if (m.frames.Length > 1) p.anim.Play("all", true);
            }
            if (vel.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(vel);
            return p;
        }

        public void AddTrail(Color c) {
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.material = new Material(Shader.Find("LQ/Particle") ?? Shader.Find("Sprites/Default"));
            tr.startColor = c; tr.endColor = new Color(c.r, c.g, c.b, 0); tr.startWidth = 0.18f; tr.endWidth = 0.02f; tr.time = 0.4f;
            var l = gameObject.AddComponent<Light>(); l.color = c; l.range = 5f; l.intensity = 1.5f; l.shadows = LightShadows.None;
        }

        void Update() {
            if (dead) return;
            float dt = Time.deltaTime;
            if (Time.time - born > life) { if (explodeOnTimer || radius > 0) Explode(transform.position, null); else Destroy(gameObject); return; }
            if (gravity) velocity += Vector3.down * (800f / 32f) * dt;
            if (homing && homingTarget != null) {
                var want = (homingTarget.position + Vector3.up * 0.8f - transform.position).normalized;
                velocity = Vector3.Lerp(velocity.normalized, want, dt * 2f).normalized * velocity.magnitude;
            }
            var step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0 && Physics.SphereCast(transform.position, 0.06f, step / dist, out var hit, dist, Combat.WorldMask, QueryTriggerInteraction.Ignore)) {
                if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) {
                    transform.position += step; return;
                }
                OnHit(hit);
                return;
            }
            transform.position += step;
            if (velocity.sqrMagnitude > 0.01f && !bounce) transform.rotation = Quaternion.LookRotation(velocity);
            else if (bounce) transform.Rotate(300f * dt, 100f * dt, 0);
        }

        void OnHit(RaycastHit hit) {
            var d = hit.collider.GetComponentInParent<IDamageable>();
            if (bounce && (d == null || !d.IsAlive)) {
                // grenade bounce
                transform.position = hit.point + hit.normal * 0.08f;
                velocity = Vector3.Reflect(velocity, hit.normal) * 0.5f;
                if (velocity.magnitude < 1.5f) velocity = Vector3.zero;
                if (!string.IsNullOrEmpty(bounceSound)) SoundBank.Play(bounceSound, hit.point, 0.7f);
                return;
            }
            if (radius > 0) { Explode(hit.point + hit.normal * 0.1f, d); return; }
            if (d != null && d.IsAlive) {
                d.TakeDamage(new DamageInfo { amount = damage, attacker = owner, inflictor = gameObject, point = hit.point, direction = velocity.normalized });
                Effects.Blood(hit.point, velocity.normalized);
            } else {
                Effects.Gunshot(hit.point, hit.normal);
                if (!string.IsNullOrEmpty(hitSound)) SoundBank.Play(hitSound, hit.point, 0.6f);
            }
            Kill();
        }

        void Explode(Vector3 pos, IDamageable direct) {
            dead = true;
            if (direct != null && direct.IsAlive) direct.TakeDamage(new DamageInfo { amount = damage, attacker = owner, inflictor = gameObject, point = pos, direction = velocity.normalized });
            Combat.RadiusDamage(pos, damage, radius * QuakeUnits.Scale, owner, gameObject, direct as MonoBehaviour ? (direct as MonoBehaviour).gameObject : null);
            Effects.Explosion(pos);
            SoundBank.Play("weapons/r_exp3.wav", pos, 1f, 60f);
            Monster.NoiseAt(pos, 30f, owner);
            Kill();
        }

        void Kill() { dead = true; Destroy(gameObject); }
    }
}
