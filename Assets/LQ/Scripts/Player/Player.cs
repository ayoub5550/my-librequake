using System;
using UnityEngine;

namespace LQ {
    public enum ArmorType { None, Green, Yellow, Red }
    public enum AmmoType { Shells, Nails, Rockets, Cells }

    /// <summary>Player state that persists between levels (health, armor, ammo, weapons, keys).</summary>
    [Serializable]
    public class PlayerStats {
        public int health = 100, maxHealth = 100;
        public int armor; public ArmorType armorType;
        public int[] ammo = { 25, 0, 0, 0 };
        public static readonly int[] MaxAmmo = { 100, 200, 100, 100 };
        public int weapons = (1 << 0) | (1 << 1); // bit per WeaponId: axe, shotgun
        public int currentWeapon = 1;
        public bool silverKey, goldKey;
        public float quadUntil, pentUntil, ringUntil, suitUntil;
        public int kills, secrets;

        public bool HasWeapon(int id) => (weapons & (1 << id)) != 0;
        public bool HasQuad => Time.time < quadUntil;
        public bool HasPent => Time.time < pentUntil;
        public bool HasRing => Time.time < ringUntil;
        public bool HasSuit => Time.time < suitUntil;
        public float ArmorFactor => armorType == ArmorType.Green ? 0.3f : armorType == ArmorType.Yellow ? 0.6f : armorType == ArmorType.Red ? 0.8f : 0f;

        public void ResetForNewGame() {
            health = 100; maxHealth = 100; armor = 0; armorType = ArmorType.None;
            ammo = new[] { 25, 0, 0, 0 }; weapons = 3; currentWeapon = 1;
            silverKey = goldKey = false; quadUntil = pentUntil = ringUntil = suitUntil = 0; kills = 0; secrets = 0;
        }
        public void ResetKeysForLevel() { silverKey = goldKey = false; quadUntil = pentUntil = ringUntil = suitUntil = 0; }
    }

    /// <summary>The player entity: takes damage, picks up items, dies.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class Player : MonoBehaviour, IDamageable {
        public static Player Instance { get; private set; }
        public PlayerStats stats => GameManager.Instance.stats;
        public Camera cam;
        public PlayerMotor motor;
        public PlayerWeapons weapons;
        public bool IsAlive => stats.health > 0;
        public bool IsDead => !IsAlive;
        public event Action<DamageInfo> OnDamaged;
        public float damageFlash;   // for HUD
        public float pickupFlash;
        float painSoundTime, nextLiquidDamage;
        public int waterLevel;      // 0 none, 1 feet, 2 waist, 3 eyes
        public LiquidType liquidType;
        float airFinished = float.PositiveInfinity;
        float megaHealthTick;

        void Awake() {
            Instance = this;
            motor = GetComponent<PlayerMotor>();
            weapons = GetComponent<PlayerWeapons>();
            gameObject.layer = LayerMask.NameToLayer("Player");
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update() {
            damageFlash = Mathf.MoveTowards(damageFlash, 0, Time.deltaTime * 2f);
            pickupFlash = Mathf.MoveTowards(pickupFlash, 0, Time.deltaTime * 3f);
            if (IsDead) return;
            // mega health decays 1/s down to 100
            if (stats.health > stats.maxHealth) {
                if (Time.time > megaHealthTick) { megaHealthTick = Time.time + 1f; stats.health--; }
            }
            LiquidUpdate();
        }

        void LiquidUpdate() {
            waterLevel = motor.waterLevel; liquidType = motor.liquidType;
            if (waterLevel >= 3 && liquidType != LiquidType.None) {
                if (airFinished == float.PositiveInfinity) airFinished = Time.time + 12f;
                if (Time.time > airFinished && Time.time > nextLiquidDamage) {
                    nextLiquidDamage = Time.time + 1f;
                    TakeDamage(new DamageInfo { amount = 2 + (Time.time - airFinished), attacker = null, point = transform.position });
                    SoundBank.Play2D("player/drown" + (UnityEngine.Random.value < 0.5f ? "1" : "2") + ".wav");
                }
            } else {
                if (airFinished != float.PositiveInfinity && waterLevel < 3 && liquidType != LiquidType.None) SoundBank.Play2D("player/gasp1.wav");
                airFinished = float.PositiveInfinity;
            }
            if (waterLevel > 0 && Time.time > nextLiquidDamage && !stats.HasSuit) {
                if (liquidType == LiquidType.Lava) { nextLiquidDamage = Time.time + 0.2f; TakeDamage(new DamageInfo { amount = 10 * waterLevel * 0.2f * 5f / 5f, point = transform.position }); }
                else if (liquidType == LiquidType.Slime) { nextLiquidDamage = Time.time + 1f; TakeDamage(new DamageInfo { amount = 4 * waterLevel, point = transform.position }); }
            }
        }

        public void TakeDamage(DamageInfo info) {
            if (IsDead) return;
            if (stats.HasPent) { SoundBank.Play2D("items/protect3.wav"); return; }
            float dmg = info.amount;
            // armor
            float save = Mathf.Ceil(stats.ArmorFactor * dmg);
            if (save >= stats.armor) { save = stats.armor; stats.armorType = ArmorType.None; }
            stats.armor -= (int)save;
            dmg -= save;
            int take = Mathf.CeilToInt(dmg);
            stats.health -= take;
            damageFlash = Mathf.Clamp01(damageFlash + take / 40f + 0.15f);
            OnDamaged?.Invoke(info);
            // knockback
            if (info.inflictor != null && info.attacker != gameObject) {
                var dir = (transform.position - info.inflictor.transform.position); dir.y = 0.4f;
                motor.AddVelocity(dir.normalized * (info.amount * 0.25f) * QuakeUnits.Scale * 8f);
            }
            if (stats.health <= 0) { Die(info); return; }
            if (Time.time > painSoundTime) {
                painSoundTime = Time.time + 0.5f;
                if (waterLevel >= 3) SoundBank.Play2D("player/drown1.wav");
                else if (liquidType == LiquidType.Lava && waterLevel > 0) SoundBank.Play2D("player/lburn" + (UnityEngine.Random.value < 0.5f ? "1" : "2") + ".wav");
                else SoundBank.Play2D("player/pain" + UnityEngine.Random.Range(1, 7) + ".wav");
            }
        }

        void Die(DamageInfo info) {
            stats.health = 0;
            SoundBank.Play2D(info.explosion && info.amount > 40 ? "player/gib.wav" : "player/death" + UnityEngine.Random.Range(1, 6) + ".wav");
            motor.OnDeath();
            weapons?.OnDeath();
            GameManager.Instance.OnPlayerDied();
        }

        // ---------- pickups ----------
        public bool GiveHealth(int amount, bool mega, bool rotten) {
            int max = mega ? 250 : stats.maxHealth;
            if (stats.health >= max) return false;
            stats.health = Mathf.Min(max, stats.health + amount);
            if (mega) megaHealthTick = Time.time + 5f;
            pickupFlash = 0.6f;
            return true;
        }

        public bool GiveArmor(ArmorType type, int value) {
            float newVal = value * (type == ArmorType.Green ? 0.3f : type == ArmorType.Yellow ? 0.6f : 0.8f);
            if (stats.armor * stats.ArmorFactor >= newVal) return false;
            stats.armorType = type; stats.armor = value; pickupFlash = 0.6f;
            return true;
        }

        public bool GiveAmmo(AmmoType type, int amount) {
            int i = (int)type;
            if (stats.ammo[i] >= PlayerStats.MaxAmmo[i]) return false;
            stats.ammo[i] = Mathf.Min(PlayerStats.MaxAmmo[i], stats.ammo[i] + amount);
            pickupFlash = 0.5f;
            return true;
        }

        public bool GiveWeapon(int weaponId) {
            bool had = stats.HasWeapon(weaponId);
            stats.weapons |= 1 << weaponId;
            var def = WeaponDefs.Get(weaponId);
            if (def.ammoType.HasValue) GiveAmmo(def.ammoType.Value, def.pickupAmmo);
            pickupFlash = 0.7f;
            if (!had) weapons?.SelectWeapon(weaponId, true);
            return true;
        }

        public void GiveKey(bool gold) { if (gold) stats.goldKey = true; else stats.silverKey = true; pickupFlash = 0.7f; }

        public void GivePowerup(string kind) {
            pickupFlash = 1f;
            switch (kind) {
                case "quad": stats.quadUntil = Time.time + 30f; break;
                case "pent": stats.pentUntil = Time.time + 30f; break;
                case "ring": stats.ringUntil = Time.time + 30f; break;
                case "suit": stats.suitUntil = Time.time + 30f; break;
            }
        }

        public bool HasKey(bool gold) => gold ? stats.goldKey : stats.silverKey;
        public Vector3 EyePosition => cam != null ? cam.transform.position : transform.position + Vector3.up * 1.4f;
        public Vector3 Center => transform.position + Vector3.up * 0.875f;
        public float DamageMultiplier => stats.HasQuad ? 4f : 1f;
    }
}
