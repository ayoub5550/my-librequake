using UnityEngine;

namespace LQ {
    public enum FireMode { Melee, Hitscan, Projectile, Beam }

    public class WeaponDef {
        public int id; public string name; public string viewModel; public string worldModel; public string classname;
        public AmmoType? ammoType; public int ammoPerShot; public int pickupAmmo;
        public FireMode mode; public float refire; public float damage; public int pellets; public Vector2 spread;
        public string fireSound; public float projectileSpeed; public string projectileModel; public float radius; public bool arc;
        public string hudIcon;
    }

    public static class WeaponDefs {
        public const int Axe = 0, Shotgun = 1, SuperShotgun = 2, Nailgun = 3, SuperNailgun = 4, GrenadeLauncher = 5, RocketLauncher = 6, Lightning = 7;

        static readonly WeaponDef[] defs = {
            new WeaponDef { id = 0, name = "Axe", viewModel = "progs/v_axe.mdl", mode = FireMode.Melee, refire = 0.5f, damage = 20, fireSound = "weapons/ax1.wav", hudIcon = null },
            new WeaponDef { id = 1, name = "Shotgun", viewModel = "progs/v_shot.mdl", worldModel = "progs/g_shot.mdl", classname = "weapon_shotgun", ammoType = AmmoType.Shells, ammoPerShot = 1, pickupAmmo = 25, mode = FireMode.Hitscan, refire = 0.5f, damage = 4, pellets = 6, spread = new Vector2(0.04f, 0.04f), fireSound = "weapons/guncock.wav", hudIcon = "inv_shotgun" },
            new WeaponDef { id = 2, name = "Double-barrelled Shotgun", viewModel = "progs/v_shot2.mdl", worldModel = "progs/g_shot.mdl", classname = "weapon_supershotgun", ammoType = AmmoType.Shells, ammoPerShot = 2, pickupAmmo = 5, mode = FireMode.Hitscan, refire = 0.7f, damage = 4, pellets = 14, spread = new Vector2(0.14f, 0.08f), fireSound = "weapons/shotgn2.wav", hudIcon = "inv_sshotgun" },
            new WeaponDef { id = 3, name = "Nailgun", viewModel = "progs/v_nail.mdl", worldModel = "progs/g_nail.mdl", classname = "weapon_nailgun", ammoType = AmmoType.Nails, ammoPerShot = 1, pickupAmmo = 30, mode = FireMode.Projectile, refire = 0.1f, damage = 9, projectileSpeed = 1000, projectileModel = "progs/spike.mdl", fireSound = "weapons/rocket1i.wav", hudIcon = "inv_nailgun" },
            new WeaponDef { id = 4, name = "Super Nailgun", viewModel = "progs/v_nail2.mdl", worldModel = "progs/g_nail2.mdl", classname = "weapon_supernailgun", ammoType = AmmoType.Nails, ammoPerShot = 2, pickupAmmo = 30, mode = FireMode.Projectile, refire = 0.1f, damage = 18, projectileSpeed = 1000, projectileModel = "progs/s_spike.mdl", fireSound = "weapons/spike2.wav", hudIcon = "inv_snailgun" },
            new WeaponDef { id = 5, name = "Grenade Launcher", viewModel = "progs/v_rock.mdl", worldModel = "progs/g_rock.mdl", classname = "weapon_grenadelauncher", ammoType = AmmoType.Rockets, ammoPerShot = 1, pickupAmmo = 5, mode = FireMode.Projectile, refire = 0.6f, damage = 120, radius = 160, projectileSpeed = 600, projectileModel = "progs/grenade.mdl", arc = true, fireSound = "weapons/grenade.wav", hudIcon = "inv_rlaunch" },
            new WeaponDef { id = 6, name = "Rocket Launcher", viewModel = "progs/v_rock2.mdl", worldModel = "progs/g_rock2.mdl", classname = "weapon_rocketlauncher", ammoType = AmmoType.Rockets, ammoPerShot = 1, pickupAmmo = 5, mode = FireMode.Projectile, refire = 0.8f, damage = 110, radius = 160, projectileSpeed = 1000, projectileModel = "progs/missile.mdl", fireSound = "weapons/sgun1.wav", hudIcon = "inv_srlaunch" },
            new WeaponDef { id = 7, name = "Thunderbolt", viewModel = "progs/v_light.mdl", worldModel = "progs/g_light.mdl", classname = "weapon_lightning", ammoType = AmmoType.Cells, ammoPerShot = 1, pickupAmmo = 15, mode = FireMode.Beam, refire = 0.1f, damage = 30, fireSound = "weapons/lstart.wav", hudIcon = "inv_lightng" },
        };

        public static WeaponDef Get(int id) => defs[Mathf.Clamp(id, 0, defs.Length - 1)];
        public static int Count => defs.Length;

        public static int FromClassname(string cn) {
            for (int i = 0; i < defs.Length; i++) if (defs[i].classname == cn) return i;
            return -1;
        }
    }
}
