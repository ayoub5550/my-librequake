using UnityEngine;

namespace LQ {
    /// <summary>Pickups: health, armor, ammo, weapons, keys, powerups. Point entities with a model and a trigger box.</summary>

    /// <summary>misc_explobox / misc_explobox2</summary>

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

}
