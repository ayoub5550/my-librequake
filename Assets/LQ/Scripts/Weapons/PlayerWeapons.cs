using UnityEngine;

namespace LQ {
    /// <summary>Weapon selection, firing and the first-person view model.</summary>
    public class PlayerWeapons : MonoBehaviour {
        Player player; PlayerStats stats => player.stats;
        Transform viewModelRoot; MdlAnimator viewModel; int viewModelId = -1;
        float nextFire; bool wasFiring; float axeSwing;
        LightningBeam beam;
        public int Current => stats.currentWeapon;
        public bool switching;

        void Start() {
            player = GetComponent<Player>();
            var cam = player.cam.transform;
            viewModelRoot = new GameObject("ViewModel").transform;
            viewModelRoot.SetParent(cam, false);
            viewModelRoot.localPosition = new Vector3(0, -0.02f, 0.0f);
            viewModelRoot.localRotation = Quaternion.Euler(0, -90f, 0); // Quake models face +X
            ShowViewModel(stats.currentWeapon);
        }

        void ShowViewModel(int id) {
            if (viewModel != null && viewModelId == id) return;
            if (viewModel != null) Destroy(viewModel.gameObject);
            viewModelId = id;
            viewModel = MdlAnimator.Create(viewModelRoot, WeaponDefs.Get(id).viewModel);
            if (viewModel != null) {
                viewModel.gameObject.layer = LayerMask.NameToLayer("Player");
                // keep it in front of the camera but avoid clipping into walls: render on top via shader queue tweak
                var mr = viewModel.GetComponent<MeshRenderer>();
                foreach (var m in mr.sharedMaterials) if (m != null) m.renderQueue = 2450;
            }
        }

        public void SelectWeapon(int id, bool onlyIfBetter = false) {
            if (!stats.HasWeapon(id)) return;
            if (onlyIfBetter && id < stats.currentWeapon && HasAmmoFor(stats.currentWeapon)) return;
            if (onlyIfBetter && (id == WeaponDefs.RocketLauncher || id == WeaponDefs.GrenadeLauncher) && id < stats.currentWeapon) return;
            stats.currentWeapon = id;
            ShowViewModel(id);
            nextFire = Mathf.Max(nextFire, Time.time + 0.15f);
        }

        bool HasAmmoFor(int id) {
            var d = WeaponDefs.Get(id);
            return !d.ammoType.HasValue || stats.ammo[(int)d.ammoType.Value] >= d.ammoPerShot;
        }

        void CycleWeapon(int dir) {
            int id = stats.currentWeapon;
            for (int i = 0; i < WeaponDefs.Count; i++) {
                id = (id + dir + WeaponDefs.Count) % WeaponDefs.Count;
                if (stats.HasWeapon(id) && HasAmmoFor(id)) { SelectWeapon(id); return; }
            }
        }

        int BestWeapon() {
            for (int id = WeaponDefs.Count - 1; id >= 0; id--) {
                if (id == WeaponDefs.GrenadeLauncher || id == WeaponDefs.RocketLauncher) continue; // Quake W_BestWeapon prefers direct weapons
                if (stats.HasWeapon(id) && HasAmmoFor(id)) return id;
            }
            if (stats.HasWeapon(WeaponDefs.RocketLauncher) && HasAmmoFor(WeaponDefs.RocketLauncher)) return WeaponDefs.RocketLauncher;
            if (stats.HasWeapon(WeaponDefs.GrenadeLauncher) && HasAmmoFor(WeaponDefs.GrenadeLauncher)) return WeaponDefs.GrenadeLauncher;
            return WeaponDefs.Axe;
        }

        public void OnDeath() { if (viewModel) viewModel.gameObject.SetActive(false); if (beam) beam.Stop(); }

        void Update() {
            if (player == null || player.IsDead || Time.timeScale == 0) return;
            if (GameInput.NextWeapon) CycleWeapon(1);
            if (GameInput.PrevWeapon) CycleWeapon(-1);
            int k = GameInput.WeaponKey(); if (k > 0) { int id = k - 1; if (stats.HasWeapon(id) && HasAmmoFor(id)) SelectWeapon(id); }

            bool fire = GameInput.Fire;
            var def = WeaponDefs.Get(stats.currentWeapon);
            if (def.mode == FireMode.Beam && beam != null && (!fire || !HasAmmoFor(def.id))) beam.Stop();
            if (fire && Time.time >= nextFire) {
                if (!HasAmmoFor(def.id)) { SelectWeapon(BestWeapon()); nextFire = Time.time + 0.3f; }
                else Fire(def);
            }
            wasFiring = fire;
            // view model sway / bob
            var m = GameInput.Move;
            var target = new Vector3(0, -0.02f + player.motor.bobAmount * 0.5f, 0);
            viewModelRoot.localPosition = Vector3.Lerp(viewModelRoot.localPosition, target, Time.deltaTime * 8f);
        }

        void Fire(WeaponDef def) {
            nextFire = Time.time + def.refire;
            if (def.ammoType.HasValue) stats.ammo[(int)def.ammoType.Value] -= def.ammoPerShot;
            var cam = player.cam.transform;
            var origin = cam.position; var aim = cam.forward;
            float mult = player.DamageMultiplier;
            if (viewModel != null) {
                // play the fire animation: first sequence with more than one frame, else all frames
                var seqName = viewModel.FindSequence("shot", "fire", "attack", "swing", "rock", "nail", "light", "axe", "frame", "shoot") ?? viewModel.CurrentSequence;
                if (seqName != null) viewModel.Play(seqName, false, null, def.mode == FireMode.Beam ? 20f : 10f);
                else viewModel.Play("all", false);
            }
            switch (def.mode) {
                case FireMode.Melee: {
                    SoundBank.Play2D(def.fireSound);
                    if (Physics.Raycast(origin, aim, out var hit, 64f / 32f, Combat.WorldMask, QueryTriggerInteraction.Ignore)) {
                        var d = hit.collider.GetComponentInParent<IDamageable>();
                        if (d != null && d.IsAlive) { d.TakeDamage(new DamageInfo { amount = def.damage * mult, attacker = gameObject, inflictor = gameObject, point = hit.point, direction = aim }); Effects.Blood(hit.point, aim); SoundBank.Play("weapons/ax1.wav", hit.point); }
                        else { Effects.Gunshot(hit.point, hit.normal); SoundBank.Play("player/axhit2.wav", hit.point); }
                    }
                    break;
                }
                case FireMode.Hitscan: {
                    SoundBank.Play2D(def.fireSound);
                    Effects.DynamicLight(origin + aim * 0.5f, new Color(1f, 0.8f, 0.5f), 4f, 0.1f);
                    for (int i = 0; i < def.pellets; i++) {
                        var dir = aim + cam.right * (Random.value * 2 - 1) * def.spread.x + cam.up * (Random.value * 2 - 1) * def.spread.y;
                        Combat.TraceAttack(origin, dir.normalized, 2048f / 32f, def.damage * mult, gameObject, out _);
                    }
                    WakeMonsters();
                    break;
                }
                case FireMode.Projectile: {
                    SoundBank.Play2D(def.fireSound);
                    var spawn = origin + aim * 0.4f + Vector3.down * 0.15f;
                    var vel = aim * def.projectileSpeed * QuakeUnits.Scale;
                    if (def.arc) vel += Vector3.up * 200f * QuakeUnits.Scale;
                    Projectile.Spawn(def, spawn, vel, gameObject, mult);
                    WakeMonsters();
                    break;
                }
                case FireMode.Beam: {
                    if (beam == null) { beam = gameObject.AddComponent<LightningBeam>(); }
                    if (!beam.Active) { SoundBank.Play2D(def.fireSound); beam.Start(cam); }
                    beam.Fire(origin, aim, 600f / 32f, def.damage * mult, gameObject);
                    if (player.waterLevel >= 2) { // discharge!
                        int cells = stats.ammo[(int)AmmoType.Cells]; stats.ammo[(int)AmmoType.Cells] = 0;
                        Combat.RadiusDamage(player.Center, 35f * cells, 35f * cells * QuakeUnits.Scale, gameObject, gameObject);
                        Effects.Explosion(player.Center); beam.Stop();
                    }
                    WakeMonsters();
                    break;
                }
            }
        }

        void WakeMonsters() { Monster.NoiseAt(transform.position, 1000f / 32f, gameObject); }
    }

    /// <summary>Thunderbolt beam: a stretched quad with the bolt texture plus per-0.1s hitscan damage.</summary>
}
