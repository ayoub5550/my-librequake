using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Base for brush entities that translate between positions (doors, plats, buttons, trains). Carries the player standing on top.</summary>
    public abstract class Mover : MonoBehaviour {
        protected QEntity ent;
        protected Vector3 pos1, pos2;        // closed / open
        protected float speed = 100f / 32f;
        protected Vector3 targetPos; protected bool moving; System.Action onArrive;
        protected string moveSound, stopSound; AudioSource moveSrc;
        protected Bounds localBounds;         // bounds at spawn, world space
        protected Vector3 spawnPos;
        Collider[] myColliders;

        protected virtual void Awake() {
            ent = GetComponent<QEntity>();
            var rb = gameObject.GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            localBounds = ent.GetBounds();
            spawnPos = transform.position;
            myColliders = GetComponentsInChildren<Collider>();
        }

        protected Vector3 Size => localBounds.size;

        protected void MoveTo(Vector3 dest, System.Action arrive) {
            targetPos = dest; moving = true; onArrive = arrive;
            if (!string.IsNullOrEmpty(moveSound)) {
                if (moveSrc == null) moveSrc = SoundBank.Loop(gameObject, moveSound, 0.8f);
                else { moveSrc.clip = SoundBank.Get(moveSound); moveSrc.Play(); }
                if (moveSrc) moveSrc.transform.position = localBounds.center;
            }
        }

        protected virtual void Update() {
            if (!moving) return;
            var before = transform.position;
            var next = Vector3.MoveTowards(before, targetPos, speed * Time.deltaTime);
            var delta = next - before;
            if (!CanMove(delta)) { OnBlocked(); return; }
            transform.position = next;
            CarryPlayer(delta);
            if ((next - targetPos).sqrMagnitude < 0.00001f) {
                moving = false;
                if (moveSrc) moveSrc.Stop();
                if (!string.IsNullOrEmpty(stopSound)) SoundBank.Play(stopSound, WorldBounds().center);
                var cb = onArrive; onArrive = null; cb?.Invoke();
            }
        }

        protected virtual bool CanMove(Vector3 delta) => true;
        protected virtual void OnBlocked() { }

        /// <summary>Is the player standing on (or inside) this mover? Then move them with it.</summary>
        void CarryPlayer(Vector3 delta) {
            var p = Player.Instance; if (p == null) return;
            var cc = p.motor.cc;
            var feet = p.transform.position + Vector3.up * 0.1f;
            if (Physics.SphereCast(feet, cc.radius * 0.9f, Vector3.down, out var hit, 0.35f, ~LayerMask.GetMask("Player", "Monster"), QueryTriggerInteraction.Ignore)) {
                if (hit.collider.transform.IsChildOf(transform)) {
                    cc.Move(delta + Vector3.down * 0.001f);
                    return;
                }
            }
            // moving into the player (doors closing / lifts from above): push them
            if (delta.y <= 0.0001f) {
                var b = WorldBounds();
                var pb = new Bounds(p.Center, new Vector3(cc.radius * 2, cc.height, cc.radius * 2));
                if (b.Intersects(pb) && delta.sqrMagnitude > 0) cc.Move(delta * 1.05f);
            }
        }

        protected Bounds WorldBounds() { var b = localBounds; b.center += transform.position - spawnPos; return b; }

        protected bool PlayerIntersects(float expand = 0f) {
            var p = Player.Instance; if (p == null) return false;
            var b = WorldBounds(); b.Expand(expand);
            var cc = p.motor.cc;
            var pb = new Bounds(p.Center, new Vector3(cc.radius * 2, cc.height, cc.radius * 2));
            return b.Intersects(pb);
        }

        protected static void SetSounds(int sounds, out string move, out string stop, string kind) {
            move = stop = null;
            if (kind == "door") {
                switch (sounds) {
                    case 1: move = "doors/doormv1.wav"; stop = "doors/drclos4.wav"; break;
                    case 2: move = "doors/hydro1.wav"; stop = "doors/hydro2.wav"; break;
                    case 3: move = "doors/stndr1.wav"; stop = "doors/stndr2.wav"; break;
                    case 4: move = "doors/ddoor1.wav"; stop = "doors/ddoor2.wav"; break;
                }
            } else if (kind == "plat") {
                if (sounds == 2) { move = "plats/medplat1.wav"; stop = "plats/medplat2.wav"; }
                else if (sounds != 0) { move = "plats/plat1.wav"; stop = "plats/plat2.wav"; }
                else { move = "plats/plat1.wav"; stop = "plats/plat2.wav"; }
            } else if (kind == "train") {
                if (sounds == 1) { move = "plats/train2.wav"; stop = "plats/train1.wav"; }
            }
        }
    }

    // -------------------------------------------------------------------------------------------------
    public class FuncDoor : Mover, IActivatable, IDamageable {
        const int START_OPEN = 1, DONT_LINK = 4, GOLD_KEY = 8, SILVER_KEY = 16, TOGGLE = 32;
        public List<FuncDoor> linked = new List<FuncDoor>();
        public FuncDoor master;                 // the door that owns the trigger field for the group
        float wait = 3f, lip = 8f, dmg = 2f;
        bool open, busy; float returnTime = -1;
        float health; public bool IsAlive => true;
        string lockedSound, useSound;
        float nextTouch;
        BoxCollider field;

        protected override void Awake() {
            base.Awake();
            speed = ent.GetFloat("speed", 100f) * QuakeUnits.Scale;
            wait = ent.GetFloat("wait", 3f); lip = ent.GetFloat("lip", 8f); dmg = ent.GetFloat("dmg", 2f);
            health = ent.GetFloat("health", 0);
            var dir = ent.MoveDir(Vector3.up);
            var size = Size;
            float dist = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z)))) - lip * QuakeUnits.Scale;
            pos1 = transform.position; pos2 = pos1 + dir * dist;
            SetSounds(ent.GetInt("sounds", 0), out moveSound, out stopSound, "door");
            int wt = LevelInfo.WorldType;
            lockedSound = wt == 0 ? "doors/medtry.wav" : wt == 1 ? "doors/runetry.wav" : "doors/basetry.wav";
            useSound = wt == 0 ? "doors/meduse.wav" : wt == 1 ? "doors/runeuse.wav" : "doors/baseuse.wav";
            if (ent.HasFlag(START_OPEN)) { transform.position = pos2; var t = pos1; pos1 = pos2; pos2 = t; }
            if (ent.HasFlag(GOLD_KEY) || ent.HasFlag(SILVER_KEY)) wait = -1;
        }

        /// <summary>Called by LevelSetup after all doors exist: link touching doors and create the touch field.</summary>
        public void Link(List<FuncDoor> all) {
            if (master != null) return;
            var group = new List<FuncDoor> { this };
            if (!ent.HasFlag(DONT_LINK)) {
                bool grew = true;
                while (grew) {
                    grew = false;
                    foreach (var d in all) {
                        if (group.Contains(d) || d.master != null || d.ent.HasFlag(DONT_LINK)) continue;
                        if (d.ent.TargetName != ent.TargetName) continue;
                        foreach (var g in group) {
                            var a = g.WorldBounds(); a.Expand(0.05f);
                            if (a.Intersects(d.WorldBounds())) { group.Add(d); grew = true; break; }
                        }
                    }
                }
            }
            foreach (var d in group) { d.master = this; d.linked = group; }
            if (string.IsNullOrEmpty(ent.TargetName) && health <= 0) {
                // touch field: union of bounds, expanded by 60 units
                var b = WorldBounds();
                foreach (var d in group) b.Encapsulate(d.WorldBounds());
                b.Expand(new Vector3(60f, 60f, 60f) * QuakeUnits.Scale);
                var fgo = new GameObject("door_field"); fgo.transform.SetParent(transform, true);
                fgo.transform.position = b.center; fgo.layer = LayerMask.NameToLayer("Trigger");
                field = fgo.AddComponent<BoxCollider>(); field.isTrigger = true; field.size = b.size;
                fgo.AddComponent<DoorField>().door = this;
            }
        }

        public void Touched(GameObject who) {
            if (Time.time < nextTouch) return;
            if (who.GetComponent<Player>() == null && who.GetComponent<Monster>() == null) return;
            nextTouch = Time.time + 0.5f;
            if (who.GetComponent<Player>() != null && (ent.HasFlag(GOLD_KEY) || ent.HasFlag(SILVER_KEY))) {
                var p = who.GetComponent<Player>();
                bool gold = ent.HasFlag(GOLD_KEY);
                if (!p.HasKey(gold)) {
                    SoundBank.Play(lockedSound, WorldBounds().center);
                    HUD.CenterPrint(gold ? "You need the gold key" : "You need the silver key");
                    return;
                }
                SoundBank.Play(useSound, WorldBounds().center);
                if (gold) p.stats.goldKey = false; else p.stats.silverKey = false;
                foreach (var d in linked) d.ent.spawnflags &= ~(GOLD_KEY | SILVER_KEY);
            }
            OpenGroup(who);
        }

        void OpenGroup(GameObject activator) {
            foreach (var d in linked) d.DoOpen(activator);
        }

        public void Activate(GameObject activator) {
            if (ent.HasFlag(TOGGLE) && open) { foreach (var d in linked) d.DoClose(); return; }
            OpenGroup(activator);
            var msg = ent.Get("message"); if (!string.IsNullOrEmpty(msg) && !open) HUD.CenterPrint(msg);
        }

        void DoOpen(GameObject activator) {
            if (open || busy) return;
            busy = true; open = true;
            var m = ent.Get("message");
            MoveTo(pos2, () => {
                busy = false;
                if (wait >= 0 && !ent.HasFlag(TOGGLE)) returnTime = Time.time + wait;
                if (master == this || linked.Count == 1) ent.FireTargets(activator);
            });
        }

        void DoClose() {
            if (!open || busy) return;
            busy = true; returnTime = -1;
            MoveTo(pos1, () => { busy = false; open = false; });
        }

        protected override void Update() {
            base.Update();
            if (open && !busy && returnTime > 0 && Time.time > returnTime) {
                if (PlayerIntersects(0.1f)) { returnTime = Time.time + 1f; return; }
                DoClose();
            }
        }

        protected override bool CanMove(Vector3 delta) {
            if (open && busy && targetPos == pos1) {
                // closing: blocked by player?
                var p = Player.Instance;
                if (p != null && PlayerIntersects(-0.02f)) {
                    if (dmg > 0) p.TakeDamage(new DamageInfo { amount = dmg, point = p.Center });
                    // reverse
                    busy = false; open = true; returnTime = Time.time + 1f;
                    foreach (var d in linked) d.DoOpen(null);
                    return false;
                }
            }
            return true;
        }

        public void TakeDamage(DamageInfo info) {
            if (health <= 0) return;
            health -= info.amount;
            if (health <= 0) { health = 0; OpenGroup(info.attacker); }
        }
    }


    // -------------------------------------------------------------------------------------------------
    /// <summary>func_door_secret: moves back then sideways (simplified: back into the wall, then along the wall).</summary>
    public class FuncDoorSecret : Mover, IActivatable, IDamageable {
        const int OPEN_ONCE = 1, FIRST_LEFT = 2, FIRST_DOWN = 4, NO_SHOOT = 8, YES_SHOOT = 16;
        Vector3 pos0, p1, p2; bool open, busy; float wait; float health = 1; public bool IsAlive => true;
        BoxCollider field; float nextTouch;

        protected override void Awake() {
            base.Awake();
            speed = 50f / 32f; wait = ent.GetFloat("wait", 5f);
            pos0 = transform.position; pos1 = pos0;
            var size = Size;
            var yawDir = QuakeUnits.YawToDir(ent.GetFloat("angle", 0)); // facing
            var fwd = new Vector3(Mathf.Round(yawDir.x), 0, Mathf.Round(yawDir.z)); if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.right;
            var side = Vector3.Cross(Vector3.up, fwd); if (ent.HasFlag(FIRST_LEFT)) side = -side;
            Vector3 first = ent.HasFlag(FIRST_DOWN) ? Vector3.down : side;
            Vector3 second = fwd * -1f;
            float d1 = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(first.x), Mathf.Abs(first.y), Mathf.Abs(first.z)))) - 8f * QuakeUnits.Scale;
            float d2 = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(second.x), Mathf.Abs(second.y), Mathf.Abs(second.z))));
            // Quake: first move is "back" (into the wall) by the door depth minus lip, then sideways by width
            p1 = pos0 + second * (Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(second.x), Mathf.Abs(second.y), Mathf.Abs(second.z)))) - 8f * QuakeUnits.Scale);
            p2 = p1 + first * Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(first.x), Mathf.Abs(first.y), Mathf.Abs(first.z))));
            moveSound = "doors/latch2.wav"; stopSound = null;
            health = ent.HasFlag(YES_SHOOT) ? 10000 : 0;
            if (string.IsNullOrEmpty(ent.TargetName) || ent.HasFlag(YES_SHOOT)) {
                var b = WorldBounds(); b.Expand(new Vector3(0.6f, 0.6f, 0.6f));
                var fgo = new GameObject("door_field"); fgo.transform.SetParent(transform, true); fgo.transform.position = b.center; fgo.layer = LayerMask.NameToLayer("Trigger");
                field = fgo.AddComponent<BoxCollider>(); field.isTrigger = true; field.size = b.size;
                fgo.AddComponent<SecretField>().door = this;
            }
        }

        public void Touched(GameObject who) {
            if (who.GetComponent<Player>() == null || Time.time < nextTouch) return;
            nextTouch = Time.time + 1f;
            var msg = ent.Get("message"); if (!string.IsNullOrEmpty(msg)) HUD.CenterPrint(msg);
            if (ent.HasFlag(YES_SHOOT) && !string.IsNullOrEmpty(ent.TargetName)) return;
            if (string.IsNullOrEmpty(ent.TargetName) && !ent.HasFlag(YES_SHOOT)) Activate(who);
        }

        public void Activate(GameObject activator) {
            if (open || busy) return;
            busy = true; open = true;
            SoundBank.Play("doors/latch2.wav", WorldBounds().center);
            MoveTo(p1, () => {
                SoundBank.Play("doors/winch2.wav", WorldBounds().center);
                MoveTo(p2, () => {
                    SoundBank.Play("doors/drclos4.wav", WorldBounds().center);
                    busy = false; ent.FireTargets(activator);
                    if (!ent.HasFlag(OPEN_ONCE) && wait >= 0) StartCoroutine(CloseLater());
                });
            });
        }

        System.Collections.IEnumerator CloseLater() {
            yield return new WaitForSeconds(wait);
            while (PlayerIntersects(0.1f)) yield return new WaitForSeconds(0.5f);
            busy = true;
            MoveTo(p1, () => MoveTo(pos0, () => { busy = false; open = false; }));
        }

        public void TakeDamage(DamageInfo info) { if (ent.HasFlag(YES_SHOOT) || health > 0) Activate(info.attacker); }
    }


    // -------------------------------------------------------------------------------------------------
    public class FuncPlat : Mover, IActivatable {
        bool atTop, busy; float returnAt = -1; BoxCollider field; float nextTouch;

        protected override void Awake() {
            base.Awake();
            speed = ent.GetFloat("speed", 150f) * QuakeUnits.Scale;
            float height = ent.Has("height") ? ent.GetFloat("height") * QuakeUnits.Scale : Size.y - 8f * QuakeUnits.Scale;
            pos1 = transform.position;              // top (as placed)
            pos2 = pos1 - Vector3.up * height;      // bottom
            SetSounds(ent.GetInt("sounds", 1), out moveSound, out stopSound, "plat");
            // trigger field above the plat (covers the top surface)
            var b = WorldBounds();
            var fgo = new GameObject("plat_field"); fgo.transform.SetParent(transform, true); fgo.layer = LayerMask.NameToLayer("Trigger");
            var center = new Vector3(b.center.x, b.max.y + 0.6f, b.center.z);
            var size = new Vector3(Mathf.Max(0.5f, b.size.x - 50f * QuakeUnits.Scale), 1.3f, Mathf.Max(0.5f, b.size.z - 50f * QuakeUnits.Scale));
            fgo.transform.position = center;
            field = fgo.AddComponent<BoxCollider>(); field.isTrigger = true; field.size = size;
            fgo.AddComponent<PlatField>().plat = this;
            if (string.IsNullOrEmpty(ent.TargetName)) { transform.position = pos2; atTop = false; }
            else atTop = true;
        }

        public void Touched(GameObject who) {
            if (who.GetComponent<Player>() == null) return;
            if (busy) return;
            if (!atTop) GoUp(); else returnAt = Time.time + 1f;
        }

        public void Activate(GameObject activator) {
            if (busy) return;
            if (atTop) GoDown(); else GoUp();
        }

        void GoUp() { busy = true; MoveTo(pos1, () => { busy = false; atTop = true; returnAt = Time.time + 3f; }); }
        void GoDown() { busy = true; returnAt = -1; MoveTo(pos2, () => { busy = false; atTop = false; }); }

        protected override void Update() {
            base.Update();
            // the field moves with us; keep it above the plat
            if (atTop && !busy && returnAt > 0 && Time.time > returnAt) {
                if (PlayerOnTop()) { returnAt = Time.time + 1f; return; }
                GoDown();
            }
        }

        bool PlayerOnTop() {
            var p = Player.Instance; if (p == null) return false;
            var b = WorldBounds(); b.Expand(new Vector3(0, 2.5f, 0));
            var cc = p.motor.cc;
            return b.Intersects(new Bounds(p.Center, new Vector3(cc.radius * 2, cc.height, cc.radius * 2)));
        }
    }


    // -------------------------------------------------------------------------------------------------
    public class FuncButton : Mover, IActivatable, IDamageable {
        bool pressed, busy; float wait; float health; public bool IsAlive => true; string pressSound; float nextTouch;

        protected override void Awake() {
            base.Awake();
            speed = ent.GetFloat("speed", 40f) * QuakeUnits.Scale; wait = ent.GetFloat("wait", 1f);
            float lip = ent.GetFloat("lip", 4f); health = ent.GetFloat("health", 0);
            var dir = ent.MoveDir(Vector3.up);
            float dist = Mathf.Abs(Vector3.Dot(Size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z)))) - lip * QuakeUnits.Scale;
            pos1 = transform.position; pos2 = pos1 + dir * dist;
            switch (ent.GetInt("sounds", 0)) { case 1: pressSound = "buttons/switch21.wav"; break; case 2: pressSound = "buttons/switch02.wav"; break; case 3: pressSound = "buttons/switch04.wav"; break; default: pressSound = "buttons/airbut1.wav"; break; }
            if (health <= 0) {
                var b = WorldBounds(); b.Expand(0.35f);
                var fgo = new GameObject("button_field"); fgo.transform.SetParent(transform, true); fgo.transform.position = b.center; fgo.layer = LayerMask.NameToLayer("Trigger");
                var bc = fgo.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = b.size;
                fgo.AddComponent<ButtonField>().button = this;
            }
        }

        public void Touched(GameObject who) {
            if (who.GetComponent<Player>() == null || Time.time < nextTouch) return;
            nextTouch = Time.time + 0.3f;
            Press(who);
        }

        public void Activate(GameObject activator) => Press(activator);

        void Press(GameObject activator) {
            if (pressed || busy) return;
            pressed = true; busy = true;
            SoundBank.Play(pressSound, WorldBounds().center);
            ent.FireTargets(activator);
            MoveTo(pos2, () => {
                busy = false;
                if (wait >= 0) StartCoroutine(Release());
            });
        }

        System.Collections.IEnumerator Release() {
            yield return new WaitForSeconds(wait);
            busy = true;
            MoveTo(pos1, () => { busy = false; pressed = false; });
        }

        public void TakeDamage(DamageInfo info) { if (health > 0) { health -= info.amount; if (health <= 0) { health = ent.GetFloat("health", 0); Press(info.attacker); } } }
    }


    // -------------------------------------------------------------------------------------------------
    /// <summary>func_train: follows path_corner entities.</summary>
    public class FuncTrain : Mover, IActivatable {
        QEntity currentCorner; bool started; float dmg;

        protected override void Awake() {
            base.Awake();
            speed = ent.GetFloat("speed", 100f) * QuakeUnits.Scale; dmg = ent.GetFloat("dmg", 2f);
            SetSounds(ent.GetInt("sounds", 0), out moveSound, out stopSound, "train");
            pos1 = transform.position;
        }

        void Start() {
            var first = QEntity.FindByTargetName(ent.Target);
            if (first == null || first.Count == 0) { enabled = false; return; }
            currentCorner = first[0];
            // Quake: train origin = corner.origin - mins
            var b = WorldBounds();
            transform.position += currentCorner.Origin - b.min;
            if (string.IsNullOrEmpty(ent.TargetName)) StartCoroutine(BeginAfter(0.1f));
        }

        System.Collections.IEnumerator BeginAfter(float t) { yield return new WaitForSeconds(t); Activate(null); }

        public void Activate(GameObject activator) {
            if (started) return;
            started = true; Next();
        }

        void Next() {
            if (currentCorner == null) return;
            var nextList = QEntity.FindByTargetName(currentCorner.Target);
            if (nextList == null || nextList.Count == 0) return;
            var next = nextList[0];
            var b = WorldBounds();
            var dest = transform.position + (next.Origin - b.min);
            MoveTo(dest, () => {
                currentCorner = next;
                float wait = next.GetFloat("wait", 0);
                if (wait < 0) return; // stop forever
                StartCoroutine(WaitThenNext(Mathf.Max(0.05f, wait)));
            });
        }

        System.Collections.IEnumerator WaitThenNext(float t) { yield return new WaitForSeconds(t); Next(); }
    }
}
