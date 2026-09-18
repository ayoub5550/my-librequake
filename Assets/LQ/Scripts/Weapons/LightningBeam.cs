using UnityEngine;

namespace LQ {
    public class LightningBeam : MonoBehaviour {
        LineRenderer lr; float nextDamage; AudioSource loop;
        public bool Active { get; private set; }
    
        public void Start(Transform cam) {
            if (lr == null) {
                var go = new GameObject("bolt"); go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("LQ/Particle") ?? Shader.Find("Sprites/Default")) { color = new Color(0.7f, 0.8f, 1f) };
                lr.startWidth = 0.12f; lr.endWidth = 0.12f; lr.positionCount = 2; lr.useWorldSpace = true; lr.textureMode = LineTextureMode.Tile;
                var bolt = MdlLoader.Load("progs/bolt2.mdl");
                if (bolt != null) lr.material.mainTexture = bolt.skins[0];
            }
            lr.enabled = true; Active = true;
        }
    
        public void Fire(Vector3 origin, Vector3 dir, float range, float damage, GameObject attacker) {
            var end = origin + dir * range;
            if (Physics.Raycast(origin, dir, out var hit, range, Combat.WorldMask, QueryTriggerInteraction.Ignore)) {
                end = hit.point;
                if (Time.time >= nextDamage) {
                    nextDamage = Time.time + 0.1f;
                    var d = hit.collider.GetComponentInParent<IDamageable>();
                    if (d != null && d.IsAlive) { d.TakeDamage(new DamageInfo { amount = damage, attacker = attacker, inflictor = attacker, point = hit.point, direction = dir }); Effects.Blood(hit.point, dir); }
                    else Effects.Particles(hit.point, hit.normal, 6, new Color(0.6f, 0.7f, 1f), 0.05f, 3f, 0.3f);
                    SoundBank.Play("weapons/lhit.wav", hit.point, 0.8f);
                }
            }
            lr.SetPosition(0, origin + dir * 0.6f + Vector3.down * 0.12f); lr.SetPosition(1, end);
            Effects.DynamicLight(origin + dir * 1f, new Color(0.6f, 0.7f, 1f), 6f, 0.05f);
        }
    
        public void Stop() { if (lr) lr.enabled = false; Active = false; }
    }
}
