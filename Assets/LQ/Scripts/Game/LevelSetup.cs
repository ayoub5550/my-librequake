using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    /// <summary>Runs when a map scene loads: attaches gameplay behaviours to every QEntity by classname, spawns the player.</summary>
    public class LevelSetup : MonoBehaviour {
        public static LevelSetup Current;
        public LevelInfo info;
        public List<QEntity> playerStarts = new List<QEntity>();

        void Awake() {
            Current = this;
            info = GetComponent<LevelInfo>() ?? gameObject.AddComponent<LevelInfo>();
            // NOTE: do NOT call QEntity.ClearRegistry() here. Awake order inside a scene is not guaranteed, so
            // clearing here wiped entities that had already registered (teleport destinations, door targets...)
            // -> random "trigger does nothing" bugs. QEntity.OnDestroy keeps the registry clean across scene loads.
            QEntity.PruneRegistry();
        }

        void Start() {
            var entities = new List<QEntity>(FindObjectsOfType<QEntity>(true));
            var doors = new List<FuncDoor>();
            int monsters = 0, secrets = 0;
            foreach (var e in entities) {
                if (e == null) continue;
                var go = e.gameObject; var cn = e.classname;
                if (cn == "worldspawn") { info.message = e.Get("message", ""); info.worldtype = e.GetInt("worldtype", 0); continue; }
                if (cn == "info_player_start" || cn == "info_player_deathmatch" || cn == "info_player_coop" || cn == "info_player_start2") { playerStarts.Add(e); continue; }
                if (cn.StartsWith("monster_")) {
                    var def = MonsterDef.Get(cn);
                    if (def != null) {
                        var m = Monster.Attach(go, def); m.entityRef = new ScopaEntityRef { entity = e }; monsters++;
                        if (e.HasFlag(1)) m.gameObject.AddComponent<AmbushMarker>(); // AMBUSH: only wakes on sight, not noise
                        go.transform.rotation = Quaternion.Euler(0, e.Yaw, 0);
                        if (!string.IsNullOrEmpty(e.TargetName)) { /* spawned by trigger in Quake only for some mods; keep visible */ }
                    }
                    continue;
                }
                if (Item.IsItemClass(cn)) { Item.Attach(go, e); continue; }
                switch (cn) {
                    case "func_door": doors.Add(go.AddComponent<FuncDoor>()); break;
                    case "func_door_secret": go.AddComponent<FuncDoorSecret>(); break;
                    case "func_plat": go.AddComponent<FuncPlat>(); break;
                    case "func_button": go.AddComponent<FuncButton>(); break;
                    case "func_train": go.AddComponent<FuncTrain>(); break;
                    case "func_water": go.AddComponent<LiquidVolume>().type = LiquidVolume.FromTextureName(e.Get("_liquid", "*water")); break;
                    case "trigger_multiple": case "trigger_once": case "trigger_secret": case "trigger_onlyregistered":
                        go.AddComponent<TriggerMultiple>(); if (cn == "trigger_secret") secrets++; break;
                    case "trigger_relay": go.AddComponent<TriggerRelay>(); break;
                    case "trigger_counter": go.AddComponent<TriggerCounter>(); break;
                    case "trigger_teleport": go.AddComponent<TriggerTeleport>(); break;
                    case "trigger_changelevel": go.AddComponent<TriggerChangeLevel>(); break;
                    case "trigger_hurt": go.AddComponent<TriggerHurt>(); break;
                    case "trigger_push": go.AddComponent<TriggerPush>(); break;
                    case "trigger_setskill": go.AddComponent<TriggerSetSkill>(); break;
                    case "trigger_monsterjump": go.AddComponent<TriggerMonsterJump>(); break;
                    case "misc_explobox": case "misc_explobox2": ExploBox.Attach(go, e); break;
                    case "light_torch_small_walltorch": case "light_flame_large_yellow": case "light_flame_small_yellow": case "light_flame_small_white":
                        Flames.Attach(go, cn); AmbientSounds.Attach(go, cn); break;
                    case "func_wall": case "func_illusionary": case "func_episodegate": case "func_bossgate": case "func_detail_wall": case "func_detail_illusionary": break;
                    default:
                        if (cn.StartsWith("ambient_") || cn == "light_fluoro" || cn == "light_fluorospark") AmbientSounds.Attach(go, cn);
                        break;
                }
            }
            foreach (var d in doors) d.Link(doors);
            info.monsterCount = monsters; info.secretCount = secrets;
            SpawnPlayer();
            GameManager.Instance?.OnLevelStarted(this);
        }

        void SpawnPlayer() {
            QEntity start = null;
            foreach (var s in playerStarts) if (s.classname == "info_player_start") { start = s; break; }
            if (start == null && playerStarts.Count > 0) start = playerStarts[0];
            Vector3 pos = start != null ? start.Origin - Vector3.up * (24f / 32f) : Vector3.zero;
            float yaw = start != null ? start.Yaw : 0;
            var go = new GameObject("Player");
            go.layer = LayerMask.NameToLayer("Player");
            go.transform.position = pos + Vector3.up * 0.05f;
            var motor = go.AddComponent<PlayerMotor>();
            var player = go.AddComponent<Player>();
            var camGo = new GameObject("MainCamera"); camGo.tag = "MainCamera";
            camGo.transform.SetParent(motor.cameraPivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.fieldOfView = GameManager.Instance != null ? GameManager.Instance.fov : 75f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = ~LayerMask.GetMask("Trigger");
            camGo.AddComponent<AudioListener>();
            player.cam = cam;
            go.AddComponent<PlayerWeapons>();
            motor.SetYawPitch(yaw, 0);
            foreach (var l in FindObjectsOfType<AudioListener>()) if (l.gameObject != camGo) Destroy(l);
        }
    }

}
