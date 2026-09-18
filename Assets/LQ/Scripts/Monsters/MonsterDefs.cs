using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public enum AttackKind { Melee, Hitscan, Projectile, Beam, Suicide, None }

    public class MonsterDef {
        public string classname, model, headModel;
        public int health;
        public float speed;                 // m/s
        public bool big, flying, swimming;
        public float meleeDamage, meleeRange = 80f / 32f;
        public AttackKind ranged = AttackKind.None;
        public float rangedDamage; public int rangedCount = 1; public float rangedSpread; public string projectileModel; public float projectileSpeed; public float projectileRadius; public bool projectileArc, homing;
        public float rangedCooldown = 1.5f;
        public string sightSound, idleSound, painSound, deathSound, attackSound, meleeSound;
        public string[] attackAnims, meleeAnims;
        public float attackDelay = 0.4f;    // seconds into the attack anim when damage happens
        public int painAnimChance = 100;

        public static readonly Dictionary<string, MonsterDef> All = new Dictionary<string, MonsterDef>();

        static void Add(MonsterDef d) { All[d.classname] = d; }

        public static MonsterDef Get(string classname) {
            if (All.Count == 0) Init();
            All.TryGetValue(classname, out var d);
            return d;
        }

        static void Init() {
            Add(new MonsterDef { classname = "monster_army", model = "progs/soldier.mdl", headModel = "progs/h_guard.mdl", health = 30, speed = 3.4f,
                ranged = AttackKind.Hitscan, rangedDamage = 4, rangedCount = 4, rangedSpread = 0.1f, rangedCooldown = 1.2f, attackAnims = new[] { "shoot" }, attackDelay = 0.4f,
                sightSound = "soldier/sight1.wav", idleSound = "soldier/idle.wav", painSound = "soldier/pain1.wav", deathSound = "soldier/death1.wav", attackSound = "soldier/sattck1.wav" });
            Add(new MonsterDef { classname = "monster_dog", model = "progs/dog.mdl", headModel = "progs/h_dog.mdl", health = 25, speed = 6f,
                meleeDamage = 12, meleeAnims = new[] { "attack" }, attackDelay = 0.4f,
                sightSound = "dog/dsight.wav", idleSound = "dog/idle.wav", painSound = "dog/dpain1.wav", deathSound = "dog/ddeath.wav", meleeSound = "dog/dattack1.wav" });
            Add(new MonsterDef { classname = "monster_ogre", model = "progs/ogre.mdl", headModel = "progs/h_ogre.mdl", health = 200, speed = 3f, big = true,
                meleeDamage = 20, meleeAnims = new[] { "swing", "smash" }, meleeSound = "ogre/ogsawatk1.wav",
                ranged = AttackKind.Projectile, rangedDamage = 40, projectileModel = "progs/grenade.mdl", projectileSpeed = 600, projectileRadius = 40, projectileArc = true, rangedCooldown = 2f, attackAnims = new[] { "shoot" }, attackDelay = 0.3f,
                sightSound = "ogre/ogwake.wav", idleSound = "ogre/ogidle.wav", painSound = "ogre/ogpain1.wav", deathSound = "ogre/ogdth.wav", attackSound = "weapons/grenade.wav" });
            Add(new MonsterDef { classname = "monster_knight", model = "progs/knight.mdl", headModel = "progs/h_knight.mdl", health = 75, speed = 4f,
                meleeDamage = 12, meleeAnims = new[] { "attackb", "runattack" }, meleeSound = "knight/sword1.wav", attackDelay = 0.5f,
                sightSound = "knight/ksight.wav", idleSound = "knight/idle.wav", painSound = "knight/khurt.wav", deathSound = "knight/kdeath.wav" });
            Add(new MonsterDef { classname = "monster_zombie", model = "progs/zombie.mdl", headModel = "progs/h_zombie.mdl", health = 60, speed = 2.5f,
                ranged = AttackKind.Projectile, rangedDamage = 10, projectileModel = "progs/zom_gib.mdl", projectileSpeed = 600, projectileArc = true, rangedCooldown = 2.5f, attackAnims = new[] { "atta", "attb", "attc" }, attackDelay = 0.9f,
                sightSound = "zombie/z_idle.wav", idleSound = "zombie/z_idle1.wav", painSound = "zombie/z_pain.wav", deathSound = "zombie/z_gib.wav", attackSound = "zombie/z_shot1.wav", painAnimChance = 60 });
            Add(new MonsterDef { classname = "monster_wizard", model = "progs/wizard.mdl", headModel = "progs/h_wizard.mdl", health = 80, speed = 5f, flying = true,
                ranged = AttackKind.Projectile, rangedDamage = 9, rangedCount = 2, rangedSpread = 0.08f, projectileModel = "progs/w_spike.mdl", projectileSpeed = 600, rangedCooldown = 1.8f, attackAnims = new[] { "magatt" }, attackDelay = 0.5f,
                sightSound = "wizard/wsight.wav", idleSound = "wizard/widle1.wav", painSound = "wizard/wpain.wav", deathSound = "wizard/wdeath.wav", attackSound = "wizard/wattack.wav" });
            Add(new MonsterDef { classname = "monster_demon1", model = "progs/demon.mdl", headModel = "progs/h_demon.mdl", health = 300, speed = 6f, big = true,
                meleeDamage = 20, meleeAnims = new[] { "attacka" }, meleeSound = "demon/dhit2.wav", attackDelay = 0.4f,
                sightSound = "demon/sight2.wav", idleSound = "demon/idle1.wav", painSound = "demon/dpain1.wav", deathSound = "demon/ddeath.wav" });
            Add(new MonsterDef { classname = "monster_shambler", model = "progs/shambler.mdl", headModel = "progs/h_shams.mdl", health = 600, speed = 3.5f, big = true,
                meleeDamage = 40, meleeAnims = new[] { "smash", "swingr", "swingl" }, meleeSound = "shambler/smack.wav", attackDelay = 0.6f,
                ranged = AttackKind.Beam, rangedDamage = 10, rangedCount = 3, rangedCooldown = 2.5f, attackAnims = new[] { "magic" },
                sightSound = "shambler/ssight.wav", idleSound = "shambler/sidle.wav", painSound = "shambler/shurt2.wav", deathSound = "shambler/sdeath.wav", attackSound = "shambler/sattck1.wav", painAnimChance = 40 });
            Add(new MonsterDef { classname = "monster_hell_knight", model = "progs/hknight.mdl", headModel = "progs/h_hellkn.mdl", health = 250, speed = 3.5f, big = true,
                meleeDamage = 20, meleeAnims = new[] { "slice", "smash", "w_attack" }, meleeSound = "hknight/slash1.wav", attackDelay = 0.5f,
                ranged = AttackKind.Projectile, rangedDamage = 9, rangedCount = 5, rangedSpread = 0.15f, projectileModel = "progs/k_spike.mdl", projectileSpeed = 300, rangedCooldown = 2.5f, attackAnims = new[] { "magica", "magicb", "magicc" },
                sightSound = "hknight/sight1.wav", idleSound = "hknight/idle.wav", painSound = "hknight/pain1.wav", deathSound = "hknight/death1.wav", attackSound = "hknight/attack1.wav" });
            Add(new MonsterDef { classname = "monster_enforcer", model = "progs/enforcer.mdl", headModel = "progs/h_mega.mdl", health = 80, speed = 3.5f,
                ranged = AttackKind.Projectile, rangedDamage = 15, projectileModel = "progs/laser.mdl", projectileSpeed = 600, rangedCooldown = 1.5f, attackAnims = new[] { "attack" }, attackDelay = 0.5f,
                sightSound = "enforcer/sight1.wav", idleSound = "enforcer/idle1.wav", painSound = "enforcer/pain1.wav", deathSound = "enforcer/death1.wav", attackSound = "enforcer/enfire.wav" });
            Add(new MonsterDef { classname = "monster_fish", model = "progs/fish.mdl", health = 25, speed = 3f, swimming = true,
                meleeDamage = 6, meleeAnims = new[] { "attack" }, meleeSound = "fish/bite.wav", attackDelay = 0.4f,
                idleSound = "fish/idle.wav", deathSound = "fish/death.wav" });
            Add(new MonsterDef { classname = "monster_shalrath", model = "progs/shalrath.mdl", headModel = "progs/h_shal.mdl", health = 400, speed = 3f, big = true,
                ranged = AttackKind.Projectile, rangedDamage = 40, projectileModel = "progs/v_spike.mdl", projectileSpeed = 400, projectileRadius = 40, homing = true, rangedCooldown = 3f, attackAnims = new[] { "attack" }, attackDelay = 0.8f,
                sightSound = "shalrath/sight.wav", idleSound = "shalrath/idle.wav", painSound = "shalrath/pain.wav", deathSound = "shalrath/death.wav", attackSound = "shalrath/attack.wav" });
            Add(new MonsterDef { classname = "monster_tarbaby", model = "progs/tarbaby.mdl", health = 80, speed = 5f,
                ranged = AttackKind.Suicide, rangedDamage = 120, projectileRadius = 160, meleeRange = 1.2f, rangedCooldown = 0.5f, attackAnims = new[] { "jump" },
                sightSound = "blob/sight1.wav", deathSound = "blob/death1.wav", attackSound = "blob/land1.wav" });
            Add(new MonsterDef { classname = "monster_boss", model = "progs/boss.mdl", health = 3000, speed = 0f, big = true,
                ranged = AttackKind.Projectile, rangedDamage = 60, projectileModel = "progs/lavaball.mdl", projectileSpeed = 600, projectileRadius = 100, projectileArc = true, rangedCooldown = 3f, attackAnims = new[] { "attack" }, attackDelay = 1f,
                sightSound = "boss1/sight1.wav", painSound = "boss1/pain.wav", deathSound = "boss1/death.wav", attackSound = "boss1/throw.wav" });
            Add(new MonsterDef { classname = "monster_oldone", model = "progs/oldone.mdl", health = 40000, speed = 0f, big = true, ranged = AttackKind.None });
        }
    }
}
