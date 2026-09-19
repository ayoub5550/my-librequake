using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Base for brush triggers. Colliders are children (Scopa) so we forward via a kinematic rigidbody on the root.</summary>
    public abstract class TriggerBase : MonoBehaviour {
        protected QEntity ent;
        protected virtual void Awake() {
            ent = GetComponent<QEntity>();
            var rb = gameObject.GetOrAdd<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var cols = GetComponentsInChildren<Collider>();
            if (cols.Length == 0) { // no collider imported (e.g. all faces culled): use the brush bounds
                var b = ent.GetBounds(); var box = gameObject.AddComponent<BoxCollider>();
                box.center = transform.InverseTransformPoint(b.center); box.size = b.size; cols = new Collider[] { box };
            }
            foreach (var c in cols) {
                if (c is MeshCollider mc && !mc.convex) mc.convex = true; // non-convex mesh colliders cannot be triggers
                c.isTrigger = true; c.gameObject.layer = LayerMask.NameToLayer("Trigger");
            }
            gameObject.layer = LayerMask.NameToLayer("Trigger");
        }
        protected static bool IsPlayer(Collider c) => c.GetComponentInParent<Player>() != null;
        protected static bool IsMonster(Collider c) => c.GetComponentInParent<Monster>() != null;
    }

    /// <summary>trigger_multiple / trigger_once / trigger_secret / trigger_onlyregistered</summary>
    public class TriggerMultiple : TriggerBase, IActivatable, IDamageable {
        const int NOTOUCH = 1;
        float wait, nextFire; bool once; float health; public bool IsAlive => true;

        protected override void Awake() {
            base.Awake();
            once = ent.classname == "trigger_once" || ent.classname == "trigger_secret";
            wait = ent.GetFloat("wait", once ? -1 : 0.2f);
            health = ent.GetFloat("health", 0);
            if (ent.classname == "trigger_secret") {
                if (!ent.Has("message")) ent.Set("message", "You found a secret area!");
                if (!ent.Has("sounds")) ent.Set("sounds", "1");
            }
        }

        void OnTriggerEnter(Collider other) {
            if (ent.HasFlag(NOTOUCH) || health > 0) return;
            if (!IsPlayer(other)) return;
            Fire(other.GetComponentInParent<Player>().gameObject);
        }

        public void Activate(GameObject activator) => Fire(activator);

        public void TakeDamage(DamageInfo info) { if (health > 0) { health -= info.amount; if (health <= 0) Fire(info.attacker); } }

        void Fire(GameObject activator) {
            if (Time.time < nextFire) return;
            nextFire = wait > 0 ? Time.time + wait : float.PositiveInfinity;
            var msg = ent.Get("message");
            if (!string.IsNullOrEmpty(msg)) HUD.CenterPrint(msg);
            switch (ent.GetInt("sounds", 0)) {
                case 1: SoundBank.Play2D("misc/secret.wav"); break;
                case 2: SoundBank.Play2D("misc/talk.wav"); break;
                case 3: SoundBank.Play2D("misc/trigger1.wav"); break;
            }
            if (ent.classname == "trigger_secret") GameManager.Instance?.OnSecretFound();
            ent.FireTargets(activator);
            if (once) Destroy(gameObject, 0.05f);
        }
    }



    public class TriggerTeleport : TriggerBase, IActivatable {
        const int PLAYER_ONLY = 1, SILENT = 2;
        bool armed = true; AudioSource hum;

        protected override void Awake() {
            base.Awake();
            if (!string.IsNullOrEmpty(ent.TargetName)) armed = false;
            if (!ent.HasFlag(SILENT)) {
                // child object: putting the AudioSource on the trigger itself and moving it shifted the whole
                // trigger volume to its bounds centre (portals stopped teleporting once audio was on).
                var sgo = new GameObject("hum"); sgo.transform.SetParent(transform, false);
                sgo.transform.position = ent.GetBounds().center;
                hum = SoundBank.Loop(sgo, "ambience/hum1.wav", 0.5f, 12f);
                if (hum == null) Destroy(sgo);
            }
        }

        public void Activate(GameObject activator) { armed = !armed; }

        void OnTriggerEnter(Collider other) { Try(other); }
        void OnTriggerStay(Collider other) { Try(other); }

        void Try(Collider other) {
            if (!armed) return;
            var player = other.GetComponentInParent<Player>();
            var monster = other.GetComponentInParent<Monster>();
            if (player == null && (monster == null || ent.HasFlag(PLAYER_ONLY))) return;
            var dests = QEntity.FindByTargetName(ent.Target);
            if (dests == null || dests.Count == 0) { Debug.LogWarning($"trigger_teleport: no destination '{ent.Target}'"); return; }
            var dest = dests[Random.Range(0, dests.Count)];
            var pos = dest.Origin;
            if (player != null) {
                Effects.Teleport(player.Center);
                player.motor.Teleport(pos - Vector3.up * (24f / 32f) + Vector3.up * 0.05f, dest.Yaw);
                player.motor.velocity = QuakeUnits.YawToDir(dest.GetFloat("angle", 0)) * (300f / 32f);
                Effects.Teleport(player.Center);
                SoundBank.Play2D("misc/r_tele" + Random.Range(1, 6) + ".wav");
                // telefrag monsters at destination
                foreach (var m in new List<Monster>(Monster.All))
                    if (m.IsAlive && Vector3.Distance(m.transform.position, pos) < 1.5f) m.TakeDamage(new DamageInfo { amount = 50000, attacker = player.gameObject, explosion = true, point = m.transform.position });
            } else if (monster != null) {
                var cc = monster.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                monster.transform.position = pos; monster.transform.rotation = Quaternion.Euler(0, dest.Yaw, 0);
                if (cc) cc.enabled = true;
                SoundBank.Play("misc/r_tele" + Random.Range(1, 6) + ".wav", pos);
                Effects.Teleport(pos);
            }
            ent.FireTargets(player != null ? player.gameObject : monster.gameObject);
        }
    }

    public class TriggerChangeLevel : TriggerBase, IActivatable {
        bool fired;
        void OnTriggerEnter(Collider other) { if (IsPlayer(other)) Go(); }
        public void Activate(GameObject activator) => Go();
        void Go() {
            if (fired) return; fired = true;
            var map = ent.Get("map", "start");
            GameManager.Instance?.ChangeLevel(map, !ent.HasFlag(1));
        }
    }

    public class TriggerHurt : TriggerBase {
        readonly Dictionary<IDamageable, float> next = new Dictionary<IDamageable, float>();
        void OnTriggerStay(Collider other) {
            var d = other.GetComponentInParent<IDamageable>();
            if (d == null || !d.IsAlive) return;
            if (d is FuncDoor || d is FuncButton || d is TriggerMultiple) return;
            next.TryGetValue(d, out var t);
            if (Time.time < t) return;
            next[d] = Time.time + 1f;
            d.TakeDamage(new DamageInfo { amount = ent.GetFloat("dmg", 5f), point = other.transform.position, inflictor = null });
        }
    }

    public class TriggerPush : TriggerBase {
        const int PUSH_ONCE = 1;
        void OnTriggerStay(Collider other) {
            var p = other.GetComponentInParent<Player>();
            if (p == null) return;
            var dir = ent.MoveDir(Vector3.up);
            float speed = ent.GetFloat("speed", 1000f) * QuakeUnits.Scale;
            p.motor.velocity = dir * speed * 0.35f; // scaled: Quake applies per physics frame with a 10x factor
            if (Time.time > nextSound) { nextSound = Time.time + 1.5f; SoundBank.Play("ambience/windfly.wav", p.transform.position, 0.6f); }
            if (ent.HasFlag(PUSH_ONCE)) Destroy(gameObject);
        }
        float nextSound;
    }

    public class TriggerSetSkill : TriggerBase {
        void OnTriggerEnter(Collider other) { if (IsPlayer(other)) GameManager.Instance.skill = ent.GetInt("message", 1); }
    }

    /// <summary>trigger_monsterjump: gives monsters inside a velocity boost.</summary>
    public class TriggerMonsterJump : TriggerBase {
        void OnTriggerStay(Collider other) {
            var m = other.GetComponentInParent<Monster>(); if (m == null) return;
            var cc = m.GetComponent<CharacterController>(); if (cc == null) return;
            var dir = ent.MoveDir(Vector3.forward);
            cc.Move((dir * ent.GetFloat("speed", 200f) * QuakeUnits.Scale + Vector3.up * ent.GetFloat("height", 200f) * QuakeUnits.Scale) * Time.deltaTime);
        }
    }
}
