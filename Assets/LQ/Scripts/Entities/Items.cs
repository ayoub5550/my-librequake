using UnityEngine;

namespace LQ {
    /// <summary>Pickups: health, armor, ammo, weapons, keys, powerups. Point entities with a model and a trigger box.</summary>
    public class Item : MonoBehaviour {
        QEntity ent; string kind; bool taken;
        public static bool IsItemClass(string cn) => cn.StartsWith("item_") || cn.StartsWith("weapon_");

        public static Item Attach(GameObject go, QEntity ent) {
            var it = go.AddComponent<Item>();
            it.ent = ent; it.kind = ent.classname;
            it.Setup();
            return it;
        }

        void Setup() {
            gameObject.layer = LayerMask.NameToLayer("Trigger");
            // drop to floor
            var pos = transform.position;
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit, 50f, Combat.WorldMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            transform.position = pos;
            // Quake item bbox: 32x32x56 (weapons/ammo) or 32x32x56 for health boxes
            var bc = gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true;
            bc.center = new Vector3(0, 0.9f, 0); bc.size = new Vector3(1.4f, 1.8f, 1.4f);
            var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            SpawnModel();
        }

        void SpawnModel() {
            int wt = LevelInfo.WorldType;
            string mdl = null, brush = null; int skin = 0; bool rotate = false;
            switch (kind) {
                case "item_health": brush = ent.HasFlag(1) ? "b_bh10" : ent.HasFlag(2) ? "b_bh100" : "b_bh25"; break;
                case "item_shells": brush = ent.HasFlag(1) ? "b_shell1" : "b_shell0"; break;
                case "item_spikes": brush = ent.HasFlag(1) ? "b_nail1" : "b_nail0"; break;
                case "item_rockets": brush = ent.HasFlag(1) ? "b_rock1" : "b_rock0"; break;
                case "item_cells": brush = ent.HasFlag(1) ? "b_batt1" : "b_batt0"; break;
                case "item_armor1": mdl = "progs/armor.mdl"; skin = 0; break;
                case "item_armor2": mdl = "progs/armor.mdl"; skin = 1; break;
                case "item_armorInv": mdl = "progs/armor.mdl"; skin = 2; break;
                case "item_artifact_super_damage": mdl = "progs/quaddama.mdl"; rotate = true; break;
                case "item_artifact_invulnerability": mdl = "progs/invulner.mdl"; rotate = true; break;
                case "item_artifact_invisibility": mdl = "progs/invisibl.mdl"; rotate = true; break;
                case "item_artifact_envirosuit": mdl = "progs/suit.mdl"; rotate = true; break;
                case "item_key1": mdl = wt == 0 ? "progs/w_s_key.mdl" : wt == 1 ? "progs/m_s_key.mdl" : "progs/b_s_key.mdl"; rotate = true; break;
                case "item_key2": mdl = wt == 0 ? "progs/w_g_key.mdl" : wt == 1 ? "progs/m_g_key.mdl" : "progs/b_g_key.mdl"; rotate = true; break;
                case "item_sigil": mdl = "progs/end" + (ent.spawnflags == 2 ? 2 : ent.spawnflags == 4 ? 3 : ent.spawnflags == 8 ? 4 : 1) + ".mdl"; rotate = true; break;
                default:
                    int w = WeaponDefs.FromClassname(kind);
                    if (w >= 0) mdl = WeaponDefs.Get(w).worldModel;
                    break;
            }
            if (brush != null) {
                var prefab = Resources.Load<GameObject>("brushmodels/" + brush);
                if (prefab != null) {
                    var go = Instantiate(prefab, transform);
                    go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
                    foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                } else {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(cube.GetComponent<Collider>());
                    cube.transform.SetParent(transform, false); cube.transform.localScale = new Vector3(1f, 0.6f, 1f); cube.transform.localPosition = new Vector3(0, 0.3f, 0);
                    cube.GetComponent<Renderer>().material.color = kind == "item_health" ? Color.red : Color.yellow;
                }
            } else if (mdl != null) {
                var holder = new GameObject("model").transform; holder.SetParent(transform, false);
                holder.localRotation = Quaternion.Euler(0, -90f, 0);
                var a = MdlAnimator.Create(holder, mdl);
                if (a != null) {
                    a.SetSkin(skin);
                    if (a.model.frames.Length > 1) a.Play("all", true);
                    var prop = holder.gameObject.AddComponent<MdlProp>(); prop.rotate = rotate || (a.model.flags & MdlModel.EF_ROTATE) != 0;
                    if (prop.rotate) holder.localPosition = new Vector3(0, 0.6f, 0);
                }
            }
        }

        void OnTriggerEnter(Collider other) {
            if (taken) return;
            var p = other.GetComponentInParent<Player>();
            if (p == null || p.IsDead) return;
            if (Pickup(p)) {
                taken = true;
                Destroy(gameObject);
            }
        }

        bool Pickup(Player p) {
            string sound = "weapons/lock4.wav"; string msg = null; bool ok;
            switch (kind) {
                case "item_health":
                    if (ent.HasFlag(1)) { ok = p.GiveHealth(15, false, true); sound = "items/r_item1.wav"; msg = "You receive 15 health"; }
                    else if (ent.HasFlag(2)) { ok = p.GiveHealth(100, true, false); sound = "items/r_item2.wav"; msg = "You receive 100 health"; }
                    else { ok = p.GiveHealth(25, false, false); sound = "items/health1.wav"; msg = "You receive 25 health"; }
                    break;
                case "item_shells": ok = p.GiveAmmo(AmmoType.Shells, ent.HasFlag(1) ? 40 : 20); msg = "You got the shells"; break;
                case "item_spikes": ok = p.GiveAmmo(AmmoType.Nails, ent.HasFlag(1) ? 50 : 25); msg = "You got the nails"; break;
                case "item_rockets": ok = p.GiveAmmo(AmmoType.Rockets, ent.HasFlag(1) ? 10 : 5); msg = "You got the rockets"; break;
                case "item_cells": ok = p.GiveAmmo(AmmoType.Cells, ent.HasFlag(1) ? 12 : 6); msg = "You got the cells"; break;
                case "item_armor1": ok = p.GiveArmor(ArmorType.Green, 100); sound = "items/armor1.wav"; msg = "You got armor"; break;
                case "item_armor2": ok = p.GiveArmor(ArmorType.Yellow, 150); sound = "items/armor1.wav"; msg = "You got armor"; break;
                case "item_armorInv": ok = p.GiveArmor(ArmorType.Red, 200); sound = "items/armor1.wav"; msg = "You got armor"; break;
                case "item_artifact_super_damage": p.GivePowerup("quad"); ok = true; sound = "items/damage.wav"; msg = "Quad Damage!"; break;
                case "item_artifact_invulnerability": p.GivePowerup("pent"); ok = true; sound = "items/protect.wav"; msg = "Pentagram of Protection!"; break;
                case "item_artifact_invisibility": p.GivePowerup("ring"); ok = true; sound = "items/inv1.wav"; msg = "Ring of Shadows!"; break;
                case "item_artifact_envirosuit": p.GivePowerup("suit"); ok = true; sound = "items/suit.wav"; msg = "Biosuit!"; break;
                case "item_key1": p.GiveKey(false); ok = true; sound = KeySound(); msg = "You got the silver key"; break;
                case "item_key2": p.GiveKey(true); ok = true; sound = KeySound(); msg = "You got the gold key"; break;
                case "item_sigil": ok = true; sound = "misc/runekey.wav"; msg = "You got the rune!"; GameManager.Instance.runes |= Mathf.Max(1, ent.spawnflags); break;
                default: {
                    int w = WeaponDefs.FromClassname(kind);
                    if (w < 0) return false;
                    ok = p.GiveWeapon(w); sound = "weapons/pkup.wav"; msg = "You got the " + WeaponDefs.Get(w).name;
                    break;
                }
            }
            if (!ok) return false;
            SoundBank.Play2D(sound);
            if (msg != null) HUD.Print(msg);
            ent.FireTargets(p.gameObject);
            return true;
        }

        static string KeySound() { int wt = LevelInfo.WorldType; return wt == 0 ? "misc/medkey.wav" : wt == 1 ? "misc/runekey.wav" : "misc/basekey.wav"; }
    }

    /// <summary>misc_explobox / misc_explobox2</summary>
    public class ExploBox : MonoBehaviour, IDamageable {
        float health = 20; public bool IsAlive => health > 0;
        public static void Attach(GameObject go, QEntity ent) {
            var eb = go.AddComponent<ExploBox>();
            var pos = go.transform.position;
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit, 50f, Combat.WorldMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            go.transform.position = pos;
            var prefab = Resources.Load<GameObject>("brushmodels/" + (ent.classname == "misc_explobox2" ? "b_exbox2" : "b_explob"));
            if (prefab != null) { var m = Instantiate(prefab, go.transform); m.transform.localPosition = Vector3.zero; }
            else { var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.transform.SetParent(go.transform, false); c.transform.localScale = new Vector3(1, 2, 1); c.transform.localPosition = new Vector3(0, 1, 0); }
            if (go.GetComponentInChildren<Collider>() == null) { var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1f, 0); bc.size = new Vector3(1, 2, 1); }
        }
        public void TakeDamage(DamageInfo info) {
            if (!IsAlive) return;
            health -= info.amount;
            if (health > 0) return;
            var c = transform.position + Vector3.up * 1f;
            Combat.RadiusDamage(c, 160, 160 * QuakeUnits.Scale, info.attacker, gameObject, gameObject);
            Effects.Explosion(c); SoundBank.Play("weapons/r_exp3.wav", c, 1f, 60f);
            GetComponent<QEntity>()?.FireTargets(info.attacker);
            Destroy(gameObject);
        }
    }

    /// <summary>ambient_* looping sounds.</summary>
    public static class AmbientSounds {
        public static void Attach(GameObject go, string classname) {
            string snd = null;
            switch (classname) {
                case "ambient_comp_hum": snd = "ambience/comp1.wav"; break;
                case "ambient_drone": snd = "ambience/drone6.wav"; break;
                case "ambient_drip": snd = "ambience/drip1.wav"; break;
                case "ambient_flouro_buzz": snd = "ambience/buzz1.wav"; break;
                case "ambient_suck_wind": snd = "ambience/suck1.wav"; break;
                case "ambient_swamp1": snd = "ambience/swamp1.wav"; break;
                case "ambient_swamp2": snd = "ambience/swamp2.wav"; break;
                case "ambient_thunder": snd = "ambience/thunder1.wav"; break;
                case "light_fluoro": snd = "ambience/fl_hum1.wav"; break;
                case "light_fluorospark": snd = "ambience/buzz1.wav"; break;
                case "light_torch_small_walltorch": case "light_flame_large_yellow": case "light_flame_small_yellow": case "light_flame_small_white": snd = "ambience/fire1.wav"; break;
            }
            if (snd != null) SoundBank.Loop(go, snd, 0.5f, 20f);
        }
    }

    /// <summary>Animated flame models for torch lights.</summary>
    public static class Flames {
        public static void Attach(GameObject go, string classname) {
            string mdl = classname == "light_torch_small_walltorch" ? "progs/flame.mdl" : "progs/flame2.mdl";
            var holder = new GameObject("model").transform; holder.SetParent(go.transform, false); holder.localRotation = Quaternion.Euler(0, -90f, 0);
            var a = MdlAnimator.Create(holder, mdl);
            if (a != null && a.model.frames.Length > 1) a.Play("all", true);
            if (a != null && classname == "light_flame_large_yellow") holder.localScale = Vector3.one * 2f;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = 6f; l.intensity = 0.8f; l.color = new Color(1f, 0.7f, 0.35f); l.shadows = LightShadows.None;
            go.AddComponent<Flicker>();
        }
    }

    public class Flicker : MonoBehaviour {
        Light l; float baseI;
        void Start() { l = GetComponent<Light>(); if (l) baseI = l.intensity; }
        void Update() { if (l) l.intensity = baseI * (0.85f + Mathf.PerlinNoise(Time.time * 6f, transform.position.x) * 0.3f); }
    }
}
