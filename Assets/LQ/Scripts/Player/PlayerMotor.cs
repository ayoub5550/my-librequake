using UnityEngine;

namespace LQ {
    /// <summary>Quake-style first person movement (accelerate / friction / air control / swimming) on a CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour {
        // Quake constants converted to metres (32 units = 1 m)
        public float maxSpeed = 320f / 32f;
        public float accelerate = 10f;
        public float airAccelerate = 10f;
        public float airWishCap = 30f / 32f;
        public float friction = 4f;
        public float stopSpeed = 100f / 32f;
        public float gravity = 800f / 32f;
        public float jumpSpeed = 270f / 32f;
        public float waterSpeedFactor = 0.7f;
        public float eyeHeight = 46f / 32f;   // from feet

        public CharacterController cc;
        public Transform cameraPivot;
        public Vector3 velocity;
        public float yaw, pitch;
        public bool grounded;
        public int waterLevel; public LiquidType liquidType;
        public bool dead;
        public bool noclipInput;   // frozen (intermission)
        float lastJumpTime, landTime, fallSpeedAtLand;
        bool wasGrounded;
        float bobTime;
        public float bobAmount;
        Vector3 pushVelocity;   // from trigger_push / platforms

        void Awake() {
            cc = GetComponent<CharacterController>();
            cc.height = 56f / 32f; cc.radius = 16f / 32f; cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cc.stepOffset = 18f / 32f; cc.slopeLimit = 46f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            if (cameraPivot == null) {
                var p = new GameObject("CameraPivot").transform; p.SetParent(transform, false);
                cameraPivot = p;
            }
            cameraPivot.localPosition = new Vector3(0, eyeHeight, 0);
        }

        public void SetYawPitch(float y, float p) { yaw = y; pitch = p; ApplyRotation(); }

        void ApplyRotation() {
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public void AddVelocity(Vector3 v) { velocity += v; }
        public void SetPush(Vector3 v) { pushVelocity = v; }

        public void Teleport(Vector3 feetPos, float newYaw) {
            cc.enabled = false;
            transform.position = feetPos;
            yaw = newYaw; velocity = Vector3.zero;
            ApplyRotation();
            cc.enabled = true;
        }

        public void OnDeath() { dead = true; }

        void Update() {
            if (Time.timeScale == 0) return;
            float dt = Time.deltaTime;
            if (!dead && !noclipInput) {
                var look = GameInput.Look;
                yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            }
            ApplyRotation();

            UpdateWaterLevel();
            var move = (dead || noclipInput) ? Vector2.zero : GameInput.Move;
            var fwd = transform.forward; var right = transform.right;
            Vector3 wishDir; float wishSpeed;

            if (waterLevel >= 2 && !dead) {
                // swimming: move in view direction
                var camFwd = cameraPivot.forward;
                wishDir = camFwd * move.y + right * move.x;
                if (GameInput.JumpHeld) wishDir += Vector3.up;
                wishSpeed = Mathf.Min(wishDir.magnitude, 1f) * maxSpeed * waterSpeedFactor;
                if (wishDir.sqrMagnitude > 0.0001f) wishDir.Normalize();
                if (wishSpeed < 0.01f) velocity += Vector3.down * 60f / 32f * dt; // sink slowly
                // water friction
                float speed = velocity.magnitude;
                if (speed > 0) {
                    float newSpeed = Mathf.Max(0, speed - dt * speed * friction);
                    velocity *= newSpeed / speed;
                }
                Accelerate(wishDir, wishSpeed, accelerate, dt);
                grounded = cc.isGrounded;
            } else {
                wishDir = fwd * move.y + right * move.x;
                wishSpeed = Mathf.Min(wishDir.magnitude, 1f) * maxSpeed;
                if (wishDir.sqrMagnitude > 0.0001f) wishDir.Normalize();
                if (grounded) {
                    // jump
                    if (GameInput.JumpHeld && !dead && Time.time - lastJumpTime > 0.2f) {
                        velocity.y = jumpSpeed; grounded = false; lastJumpTime = Time.time;
                        SoundBank.Play2D("player/plyrjmp8.wav", 0.7f);
                    } else {
                        ApplyFriction(dt);
                        Accelerate(wishDir, wishSpeed, accelerate, dt);
                        velocity.y = -1f; // stick to ground
                    }
                } else {
                    Accelerate(wishDir, Mathf.Min(wishSpeed, airWishCap), airAccelerate, dt);
                    velocity.y -= gravity * dt;
                }
                if (waterLevel == 1 && GameInput.JumpHeld) velocity.y = Mathf.Max(velocity.y, jumpSpeed * 0.5f);
            }

            var delta = (velocity + pushVelocity) * dt;
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, 20f * dt);
            var flags = cc.Move(delta);
            bool nowGrounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
            if (nowGrounded && !wasGrounded && !dead) {
                // landing
                float fall = -velocity.y;
                if (fall > 650f / 32f) { Player.Instance?.TakeDamage(new DamageInfo { amount = 5, point = transform.position }); SoundBank.Play2D("player/land2.wav"); }
                else if (fall > 300f / 32f) SoundBank.Play2D("player/land.wav", 0.8f);
                landTime = Time.time;
            }
            if (nowGrounded && velocity.y < 0) velocity.y = -1f;
            grounded = nowGrounded; wasGrounded = nowGrounded;

            // view bob
            var hv = new Vector3(velocity.x, 0, velocity.z).magnitude;
            if (grounded && hv > 0.5f) bobTime += dt * hv * 1.2f;
            bobAmount = Mathf.Lerp(bobAmount, grounded ? Mathf.Sin(bobTime) * 0.03f * Mathf.Clamp01(hv / maxSpeed) : 0, dt * 10f);
            float targetEye = dead ? 0.25f : eyeHeight;
            var cp = cameraPivot.localPosition;
            cp.y = Mathf.Lerp(cp.y, targetEye + bobAmount, dead ? dt * 4f : 1f);
            cameraPivot.localPosition = cp;
            if (dead) { cameraPivot.localRotation = Quaternion.Euler(pitch, 0, Mathf.Lerp(cameraPivot.localEulerAngles.z > 180 ? cameraPivot.localEulerAngles.z - 360 : cameraPivot.localEulerAngles.z, 45f, dt * 3f)); }
        }

        void ApplyFriction(float dt) {
            var hv = new Vector3(velocity.x, 0, velocity.z);
            float speed = hv.magnitude;
            if (speed < 0.01f) { velocity.x = velocity.z = 0; return; }
            float control = speed < stopSpeed ? stopSpeed : speed;
            float drop = control * friction * dt;
            float newSpeed = Mathf.Max(0, speed - drop);
            velocity.x *= newSpeed / speed; velocity.z *= newSpeed / speed;
        }

        void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt) {
            float currentSpeed = Vector3.Dot(velocity, wishDir);
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0) return;
            float accelSpeed = Mathf.Min(accel * dt * wishSpeed, addSpeed);
            velocity += accelSpeed * wishDir;
        }

        void UpdateWaterLevel() {
            waterLevel = 0; liquidType = LiquidType.None;
            var feet = transform.position + Vector3.up * 0.1f;
            var t = LiquidVolume.TypeAt(feet);
            if (t == LiquidType.None) return;
            liquidType = t; waterLevel = 1;
            if (LiquidVolume.TypeAt(transform.position + Vector3.up * (cc.height * 0.5f)) != LiquidType.None) waterLevel = 2;
            if (LiquidVolume.TypeAt(cameraPivot.position) != LiquidType.None) waterLevel = 3;
        }
    }
}
