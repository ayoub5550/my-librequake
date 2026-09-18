using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public struct DamageInfo {
        public float amount;
        public GameObject attacker;   // who gets the credit / wakes monsters
        public GameObject inflictor;  // projectile or attacker
        public Vector3 point;
        public Vector3 direction;
        public bool explosion;
    }

    public interface IDamageable {
        void TakeDamage(DamageInfo info);
        bool IsAlive { get; }
    }

    public static class Combat {
        public static int WorldMask => ~LayerMask.GetMask("Ignore Raycast", "Trigger", "Player");

        /// <summary>Quake-style hitscan: returns first thing hit and applies damage.</summary>
        public static bool TraceAttack(Vector3 origin, Vector3 dir, float range, float damage, GameObject attacker, out RaycastHit hit, bool spawnEffects = true) {
            if (Physics.Raycast(origin, dir, out hit, range, WorldMask, QueryTriggerInteraction.Ignore)) {
                var d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null && d.IsAlive) {
                    d.TakeDamage(new DamageInfo { amount = damage, attacker = attacker, inflictor = attacker, point = hit.point, direction = dir, explosion = false });
                    if (spawnEffects) Effects.Blood(hit.point, dir);
                } else if (spawnEffects) {
                    Effects.Gunshot(hit.point, hit.normal);
                }
                return true;
            }
            return false;
        }

        /// <summary>T_RadiusDamage: linear falloff, half damage to attacker.</summary>
        public static void RadiusDamage(Vector3 center, float damage, float radiusMeters, GameObject attacker, GameObject inflictor, GameObject ignore = null) {
            var hits = Physics.OverlapSphere(center, radiusMeters, ~0, QueryTriggerInteraction.Ignore);
            var done = new HashSet<IDamageable>();
            foreach (var h in hits) {
                var d = h.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive || done.Contains(d)) continue;
                var mb = d as MonoBehaviour;
                if (mb == null || mb.gameObject == ignore) continue;
                done.Add(d);
                var closest = h.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                float points = damage - dist / radiusMeters * damage;   // Quake: damage - 0.5*dist(units) with radius = damage+40
                if (mb.gameObject == attacker) points *= 0.5f;
                if (points <= 0) continue;
                // line of sight check from explosion to target
                if (Physics.Linecast(center, closest + (center - closest).normalized * 0.05f, out var block, WorldMask, QueryTriggerInteraction.Ignore)) {
                    if (block.collider.GetComponentInParent<IDamageable>() != d) continue;
                }
                d.TakeDamage(new DamageInfo { amount = points, attacker = attacker, inflictor = inflictor, point = closest, direction = (closest - center).normalized, explosion = true });
            }
        }
    }

    /// <summary>Simple visual effects (blood, sparks, explosions, teleport).</summary>
    public static class Effects {
        static Material particleMat;

        static Material ParticleMaterial {
            get {
                if (particleMat == null) {
                    var sh = Shader.Find("LQ/Particle") ?? Shader.Find("Sprites/Default");
                    particleMat = new Material(sh);
                    var tex = new Texture2D(2, 2); tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) }); tex.Apply();
                    particleMat.mainTexture = tex;
                }
                return particleMat;
            }
        }

        public static void Blood(Vector3 pos, Vector3 dir) => Particles(pos, -dir, 12, new Color(0.45f, 0.02f, 0.02f), 0.06f, 3f, 0.6f);
        public static void Gunshot(Vector3 pos, Vector3 normal) => Particles(pos, normal, 8, new Color(0.7f, 0.6f, 0.4f), 0.04f, 2.5f, 0.4f);
        public static void Teleport(Vector3 pos) => Particles(pos, Vector3.up, 60, new Color(0.6f, 0.6f, 1f), 0.08f, 4f, 0.8f, 1f);

        public static void Explosion(Vector3 pos) {
            SpriteAnimator.Spawn("progs/s_explod.spr", pos, false, 10f, 1f);
            Particles(pos, Vector3.up, 40, new Color(1f, 0.6f, 0.15f), 0.1f, 6f, 0.7f, 1.2f);
            DynamicLight(pos, new Color(1f, 0.7f, 0.3f), 8f, 0.4f);
        }

        public static void DynamicLight(Vector3 pos, Color color, float range, float life) {
            var go = new GameObject("dlight");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.range = range; l.intensity = 2.5f; l.shadows = LightShadows.None;
            go.AddComponent<FadeAndDie>().life = life;
        }

        public static void Particles(Vector3 pos, Vector3 dir, int count, Color color, float size, float speed, float life, float spread = 0.6f) {
            var go = new GameObject("particles");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMaterial; r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.duration = 0.1f; main.loop = false; main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
            main.startColor = color; main.gravityModifier = 1.5f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = count + 4;
            var em = ps.emission; em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = spread * 60f; shape.radius = 0.02f;
            go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.up);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color * 0.5f, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
            Object.Destroy(go, life + 0.3f);
        }
    }

}
