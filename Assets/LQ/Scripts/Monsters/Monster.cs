using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Generic Quake monster AI: stand -> (sight/noise) -> chase -> melee / ranged -> pain -> death.</summary>
    public class Monster : MonoBehaviour, IDamageable {
        public static readonly List<Monster> All = new List<Monster>();
        public MonsterDef def;
        public float health;
        public bool IsAlive => health > 0;
        public bool awake;
        public string spawnTarget;       // "target" key: fired on death
        public ScopaEntityRef entityRef;

        enum State { Stand, Chase, Attack, Pain, Dead }
        State state = State.Stand;
        CharacterController cc; MdlAnimator anim; Transform model;
        Transform target; Vector3 velocity; float stateUntil; float nextRanged; float nextIdleSound; float nextSightCheck;
        float steerUntil; float steerSign; Vector3 lastPos; float stuckTime; float pathYaw; bool hasPathYaw;
        float standingYaw;
        static readonly Collider[] tmp = new Collider[8];

        public static Monster Attach(GameObject go, MonsterDef def) {
            var m = go.AddComponent<Monster>();
            m.def = def; m.health = def.health;
            return m;
        }

        void Awake() { All.Add(this); }
        void OnDestroy() { All.Remove(this); }

        void Start() {
            gameObject.layer = LayerMask.NameToLayer("Monster");
            cc = gameObject.AddComponent<CharacterController>();
            float radius = def.big ? 1f : 0.5f;
            float height = def.swimming ? 1.5f : (def.big ? 88f / 32f : 64f / 32f);
            cc.radius = radius; cc.height = height; cc.center = new Vector3(0, height * 0.5f - 24f / 32f, 0); // Quake origin is 24u above feet
            cc.stepOffset = 18f / 32f; cc.slopeLimit = 46f; cc.skinWidth = 0.04f; cc.minMoveDistance = 0;
            // model: Quake models face +X; sit under a child rotated -90 so transform.forward is facing direction
            model = new GameObject("model").transform; model.SetParent(transform, false);
            model.localRotation = Quaternion.Euler(0, -90f, 0);
            anim = MdlAnimator.Create(model, def.model);
            if (anim != null) anim.gameObject.layer = gameObject.layer;
            standingYaw = transform.eulerAngles.y;
            Stand();
            nextIdleSound = Time.time + Random.Range(2f, 8f);
            lastPos = transform.position;
            // nudge out of the floor
            if (!def.flying && !def.swimming) { cc.Move(Vector3.down * 0.01f); }
        }

        void Stand() { state = State.Stand; PlayLoop("stand", "hover", "swim", "walk"); }

        void PlayLoop(params string[] prefixes) {
            if (anim == null) return;
            var s = anim.FindSequence(prefixes);
            if (s != null) anim.Play(s, true);
        }

        // ---------------- perception ----------------
        public static void NoiseAt(Vector3 pos, float radius, GameObject source) {
            foreach (var m in All) {
                if (m.awake || !m.IsAlive) continue;
                if (Vector3.Distance(m.transform.position, pos) > radius) continue;
                if (m.CanSeePoint(pos)) m.WakeUp();
            }
        }

        bool CanSeePoint(Vector3 p) {
            var eye = transform.position + Vector3.up * (cc != null ? cc.height * 0.6f : 1f);
            var dir = p - eye; float dist = dir.magnitude;
            if (dist < 0.01f) return true;
            if (Physics.Raycast(eye, dir / dist, out var hit, dist, Combat.WorldMask, QueryTriggerInteraction.Ignore)) {
                return hit.collider.GetComponentInParent<Player>() != null;
            }
            return true;
        }

        bool CanSeePlayer() {
            var p = Player.Instance;
            if (p == null || p.IsDead || p.stats.HasRing) return false;
            var to = p.Center - transform.position;
            if (to.magnitude > 1000f / 32f) return false;
            var fwd = transform.forward; to.y = 0;
            bool infront = Vector3.Dot(fwd, to.normalized) > 0.3f || to.magnitude < 3f;
            if (!infront) return false;
            return CanSeePoint(p.Center) && CanSeePoint(p.EyePosition);
        }

        public void WakeUp() {
            if (awake || !IsAlive) return;
            awake = true; target = Player.Instance != null ? Player.Instance.transform : null;
            if (!string.IsNullOrEmpty(def.sightSound)) SoundBank.Play(def.sightSound, transform.position);
            state = State.Chase; PlayLoop("run", "fly", "swim", "walk", "runb");
        }

        // ---------------- main loop ----------------
        void Update() {
            if (!IsAlive || Time.timeScale == 0) return;
            float dt = Time.deltaTime;
            if (target == null && Player.Instance != null) target = Player.Instance.transform;

            if (Time.time > nextIdleSound) {
                nextIdleSound = Time.time + Random.Range(3f, 9f);
                if (!string.IsNullOrEmpty(def.idleSound) && Random.value < 0.5f) SoundBank.Play(def.idleSound, transform.position, 0.7f);
            }

            switch (state) {
                case State.Stand:
                    ApplyGravity(dt);
                    if (Time.time > nextSightCheck) { nextSightCheck = Time.time + 0.2f; if (CanSeePlayer()) WakeUp(); }
                    break;
                case State.Chase:
                    Chase(dt);
                    break;
                case State.Attack:
                case State.Pain:
                    ApplyGravity(dt);
                    if (target != null && state == State.Attack && def.ranged != AttackKind.Suicide) FaceTarget(dt, 360f);
                    if (Time.time > stateUntil) { state = State.Chase; PlayLoop("run", "fly", "swim", "walk", "runb"); }
                    break;
            }
        }

        void ApplyGravity(float dt) {
            if (def.flying || def.swimming || cc == null) return;
            velocity.y -= 800f / 32f * dt;
            var flags = cc.Move(new Vector3(0, velocity.y * dt, 0));
            if ((flags & CollisionFlags.Below) != 0 || cc.isGrounded) velocity.y = -1f;
        }

        void FaceTarget(float dt, float turnSpeed) {
            if (target == null) return;
            var to = target.position - transform.position; to.y = 0;
            if (to.sqrMagnitude < 0.001f) return;
            var want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }

        void Chase(float dt) {
            var p = Player.Instance;
            if (p == null || p.IsDead || target == null) { awake = false; Stand(); return; }
            var toTarget = target.position + Vector3.up * 0.75f - transform.position;
            float dist = new Vector3(toTarget.x, 0, toTarget.z).magnitude;
            bool los = CanSeePoint(p.Center);

            // melee?
            if (def.meleeDamage > 0 && dist < def.meleeRange + (def.big ? 0.7f : 0.3f) && Mathf.Abs(toTarget.y) < 1.5f && los) { StartMelee(); return; }
            // ranged?
            if (def.ranged != AttackKind.None && los && Time.time > nextRanged) {
                float chance = def.ranged == AttackKind.Suicide ? (dist < def.meleeRange * 2f ? 1f : 0f) : (dist < 4f ? 0.9f : dist < 12f ? 0.5f : 0.25f);
                if (def.meleeDamage > 0 && dist < 6f && def.ranged != AttackKind.Suicide) chance *= 0.3f; // prefer closing in
                if (Random.value < chance * dt * 3f) { StartRanged(); return; }
                if (Random.value < 0.02f) nextRanged = Time.time + 0.3f;
            }

            // movement
            FaceTarget(dt, 240f);
            var dir = transform.forward;
            if (def.flying || def.swimming) {
                dir = (target.position + Vector3.up * (def.flying ? 1.5f : 0.3f) - transform.position);
                if (def.swimming && LiquidVolume.TypeAt(target.position) == LiquidType.None) dir.y = Mathf.Min(0, dir.y);
                dir.Normalize();
                if (dist < 3f && def.flying) dir = Vector3.zero; // hover at distance
            } else {
                // simple steering around obstacles / stuck detection
                if (Time.time < steerUntil) dir = Quaternion.Euler(0, 60f * steerSign, 0) * dir;
                else if (!GroundAhead(dir)) { steerSign = Random.value < 0.5f ? -1 : 1; steerUntil = Time.time + 0.7f; dir = Quaternion.Euler(0, 90f * steerSign, 0) * dir; }
                if (!los && dist > 20f) dir = Vector3.zero; // lost the player far away: wait
            }
            var moveSpeed = def.speed;
            var move = dir * moveSpeed;
            if (!def.flying && !def.swimming) { velocity.y -= 800f / 32f * dt; move.y = velocity.y; }
            var flags = cc.Move(move * dt);
            if ((flags & CollisionFlags.Below) != 0 || cc.isGrounded) velocity.y = -1f;
            if ((flags & CollisionFlags.Sides) != 0) {
                stuckTime += dt;
                if (stuckTime > 0.4f && Time.time > steerUntil) { steerSign = Random.value < 0.5f ? -1 : 1; steerUntil = Time.time + Random.Range(0.5f, 1.2f); stuckTime = 0; }
            } else stuckTime = 0;
            if (dir.sqrMagnitude < 0.01f) PlayLoop("stand", "hover", "swim"); else PlayLoop("run", "fly", "swim", "walk", "runb");
        }

        bool GroundAhead(Vector3 dir) {
            var probe = transform.position + dir * (cc.radius + 0.4f) + Vector3.up * 0.3f;
            return Physics.Raycast(probe, Vector3.down, 2.2f, Combat.WorldMask, QueryTriggerInteraction.Ignore) || LiquidVolume.TypeAt(probe + Vector3.down * 1.5f) != LiquidType.None;
        }

        // ---------------- attacks ----------------
        void StartMelee() {
            state = State.Attack;
            var seq = Pick(def.meleeAnims);
            float len = PlayOnce(seq, 10f);
            stateUntil = Time.time + Mathf.Max(0.5f, len);
            Invoke(nameof(MeleeHit), Mathf.Min(def.attackDelay, len * 0.6f));
        }

        void MeleeHit() {
            if (!IsAlive || target == null) return;
            var p = Player.Instance; if (p == null || p.IsDead) return;
            var to = p.Center - transform.position; to.y = 0;
            if (to.magnitude < def.meleeRange + (def.big ? 0.9f : 0.5f) && Vector3.Dot(transform.forward, to.normalized) > 0.3f) {
                p.TakeDamage(new DamageInfo { amount = def.meleeDamage * Random.Range(0.6f, 1.4f), attacker = gameObject, inflictor = gameObject, point = p.Center, direction = to.normalized });
                if (!string.IsNullOrEmpty(def.meleeSound)) SoundBank.Play(def.meleeSound, transform.position);
            }
        }

        void StartRanged() {
            state = State.Attack; nextRanged = Time.time + def.rangedCooldown * Random.Range(0.8f, 1.4f);
            var seq = Pick(def.attackAnims);
            float len = PlayOnce(seq, 10f);
            stateUntil = Time.time + Mathf.Max(0.4f, len);
            if (def.ranged == AttackKind.Suicide) {
                // tarbaby: leap at the player and explode on contact / landing
                var to = (target.position - transform.position); to.y = 0;
                velocity = to.normalized * 8f + Vector3.up * 6f;
                Invoke(nameof(SuicideExplode), Mathf.Clamp(to.magnitude / 8f, 0.3f, 1.2f));
                return;
            }
            Invoke(nameof(RangedFire), Mathf.Min(def.attackDelay, len * 0.5f));
            if (def.rangedCount > 1 && def.ranged == AttackKind.Projectile && def.classname == "monster_wizard") Invoke(nameof(RangedFire), Mathf.Min(def.attackDelay, len * 0.5f) + 0.2f);
        }

        void SuicideExplode() {
            if (!IsAlive) return;
            Combat.RadiusDamage(transform.position, def.rangedDamage, def.projectileRadius * QuakeUnits.Scale, gameObject, gameObject, gameObject);
            Effects.Explosion(transform.position);
            SoundBank.Play("blob/death1.wav", transform.position);
            health = 0; RemoveBody();
        }

        void RangedFire() {
            if (!IsAlive || target == null) return;
            var p = Player.Instance; if (p == null) return;
            var muzzle = transform.position + Vector3.up * (def.big ? 1.2f : 0.6f) + transform.forward * 0.5f;
            var aimPoint = p.Center;
            if (!string.IsNullOrEmpty(def.attackSound)) SoundBank.Play(def.attackSound, transform.position);
            switch (def.ranged) {
                case AttackKind.Hitscan: {
                    var aim = (aimPoint - muzzle).normalized;
                    var right = Vector3.Cross(Vector3.up, aim).normalized; var up = Vector3.Cross(aim, right);
                    for (int i = 0; i < def.rangedCount; i++) {
                        var dir = aim + right * (Random.value * 2 - 1) * def.rangedSpread + up * (Random.value * 2 - 1) * def.rangedSpread;
                        Combat.TraceAttack(muzzle, dir.normalized, 2048f / 32f, def.rangedDamage, gameObject, out _);
                    }
                    Effects.DynamicLight(muzzle, new Color(1f, 0.8f, 0.5f), 4f, 0.1f);
                    break;
                }
                case AttackKind.Projectile: {
                    int count = def.classname == "monster_wizard" ? 1 : def.rangedCount;
                    for (int i = 0; i < count; i++) {
                        var aim = (aimPoint - muzzle);
                        float speed = def.projectileSpeed * QuakeUnits.Scale;
                        Vector3 vel;
                        if (def.projectileArc) {
                            // lob: aim a bit higher and add up velocity like Ogre grenades
                            var flat = aim; flat.y = 0;
                            vel = flat.normalized * speed + Vector3.up * (200f / 32f + Mathf.Max(0, aim.y) * 2f);
                        } else {
                            var right = Vector3.Cross(Vector3.up, aim.normalized).normalized; var up = Vector3.Cross(aim.normalized, right);
                            var dir = aim.normalized + right * (Random.value * 2 - 1) * def.rangedSpread + up * (Random.value * 2 - 1) * def.rangedSpread;
                            if (count > 1) dir = Quaternion.Euler(0, (i - (count - 1) * 0.5f) * 8f, 0) * aim.normalized;
                            vel = dir.normalized * speed;
                        }
                        var proj = Projectile.Create(def.projectileModel, muzzle, vel, gameObject);
                        proj.damage = def.rangedDamage; proj.radius = def.projectileRadius; proj.gravity = def.projectileArc; proj.bounce = def.projectileArc;
                        proj.explodeOnTimer = def.projectileArc && def.projectileRadius > 0; proj.life = def.projectileArc ? 2.5f : 6f;
                        proj.homing = def.homing; proj.homingTarget = target;
                        if (def.projectileArc) proj.bounceSound = "weapons/bounce.wav";
                    }
                    break;
                }
                case AttackKind.Beam: {
                    // shambler lightning: instant beam, several hits
                    var aim = (aimPoint - muzzle).normalized;
                    var go = new GameObject("shambler_bolt"); var lr = go.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("LQ/Particle") ?? Shader.Find("Sprites/Default")) { color = new Color(0.7f, 0.8f, 1f) };
                    lr.startWidth = 0.15f; lr.endWidth = 0.15f; lr.positionCount = 2;
                    var end = muzzle + aim * 600f / 32f;
                    if (Physics.Raycast(muzzle, aim, out var hit, 600f / 32f, Combat.WorldMask, QueryTriggerInteraction.Ignore)) {
                        end = hit.point;
                        var d = hit.collider.GetComponentInParent<IDamageable>();
                        if (d != null && d.IsAlive) d.TakeDamage(new DamageInfo { amount = def.rangedDamage * def.rangedCount, attacker = gameObject, inflictor = gameObject, point = hit.point, direction = aim });
                    }
                    lr.SetPosition(0, muzzle); lr.SetPosition(1, end);
                    Destroy(go, 0.25f);
                    SoundBank.Play("shambler/sboom.wav", transform.position);
                    Effects.DynamicLight(muzzle, new Color(0.6f, 0.7f, 1f), 8f, 0.25f);
                    break;
                }
            }
        }

        string Pick(string[] names) {
            if (names == null || names.Length == 0 || anim == null) return null;
            var candidates = new List<string>();
            foreach (var n in names) if (anim.HasSequence(n)) candidates.Add(n);
            if (candidates.Count == 0) return anim.FindSequence(names);
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>Plays a sequence once and returns its duration in seconds.</summary>
        float PlayOnce(string seq, float fps) {
            if (anim == null || seq == null) return 0.6f;
            anim.Play(seq, false, null, fps);
            return anim.CurrentSequence == seq ? SequenceLength(seq) / fps : 0.6f;
        }

        float SequenceLength(string seq) {
            foreach (var s in AnimDB.Sequences(AnimDB.FrameNames(System.IO.Path.GetFileNameWithoutExtension(def.model)) ?? anim.model.frameNames))
                if (s.name == seq) return s.count;
            return 6;
        }

        // ---------------- damage ----------------
        public void TakeDamage(DamageInfo info) {
            if (!IsAlive) return;
            health -= info.amount;
            if (!awake) WakeUp();
            if (health <= 0) { Die(info); return; }
            if (state != State.Pain && Random.Range(0, 100) < def.painAnimChance && info.amount > 4) {
                if (!string.IsNullOrEmpty(def.painSound)) SoundBank.Play(def.painSound, transform.position);
                var pains = anim != null ? anim.FindSequences("pain") : null;
                if (pains != null && pains.Count > 0) {
                    var seq = pains[Random.Range(0, pains.Count)];
                    float len = PlayOnce(seq, 10f);
                    state = State.Pain; stateUntil = Time.time + Mathf.Min(len, 1.2f);
                    CancelInvoke(nameof(MeleeHit)); CancelInvoke(nameof(RangedFire));
                }
            }
        }

        void Die(DamageInfo info) {
            float overkill = -health;
            health = 0; state = State.Dead;
            CancelInvoke();
            GameManager.Instance?.OnMonsterKilled(this);
            bool gib = (info.amount >= 60 && info.explosion) || overkill > 40;
            if (gib) { Gib(); return; }
            if (!string.IsNullOrEmpty(def.deathSound)) SoundBank.Play(def.deathSound, transform.position);
            if (anim != null) {
                var deaths = anim.FindSequences("death"); if (deaths.Count == 0) deaths = anim.FindSequences("bdeath"); if (deaths.Count == 0) deaths = anim.FindSequences("fdeath"); if (deaths.Count == 0) deaths = anim.FindSequences("exp");
                if (deaths.Count > 0) anim.Play(deaths[Random.Range(0, deaths.Count)], false);
                else anim.Stop();
            }
            if (cc) Destroy(cc);
            // corpse: keep the mesh, drop physics; monsters fall through nothing since floor stays
            foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
            FireTarget();
            if (def.flying || def.swimming) StartCoroutine(SinkCorpse());
            enabled = false;
        }

        System.Collections.IEnumerator SinkCorpse() {
            float t = 0; var start = transform.position;
            while (t < 1.5f) { t += Time.deltaTime; transform.position = start + Vector3.down * t * 1.5f; yield return null; }
        }

        void Gib() {
            SoundBank.Play("player/udeath.wav", transform.position);
            Effects.Blood(transform.position + Vector3.up, Vector3.up);
            var center = transform.position + Vector3.up * 0.8f;
            ThrowGib(def.headModel, center); ThrowGib("progs/gib1.mdl", center); ThrowGib("progs/gib2.mdl", center); ThrowGib("progs/gib3.mdl", center);
            FireTarget();
            RemoveBody();
        }

        void ThrowGib(string model, Vector3 pos) {
            if (string.IsNullOrEmpty(model)) return;
            var vel = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), Random.Range(-1f, 1f)) * 6f;
            var p = Projectile.Create(model, pos, vel, null);
            p.gravity = true; p.bounce = true; p.life = 8f; p.damage = 0; p.radius = 0; p.explodeOnTimer = false;
        }

        void FireTarget() {
            if (entityRef != null) entityRef.FireTargets();
        }

        void RemoveBody() { Destroy(gameObject); }
    }
}
