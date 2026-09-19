using System.Collections;
using System.Text;
using UnityEngine;

namespace LQ {
    /// <summary>
    /// Headless "virtual play-tester". Enabled with env LQ_BOT=1 (works in the Editor play mode too, see
    /// LQBuildPipeline.BotPlay). Loads LQ_BOT_MAP, then every 0.5 s logs the player's position, health, ground state
    /// and the nearest floor below the feet. Optional LQ_BOT_SCRIPT: "idle" (default) | "walk" (walk forward, turning
    /// a bit, jumping). Logs "[Bot] DIED ..." with a diagnosis when the player dies, and quits after LQ_BOT_SECONDS.
    /// Everything is written to the Unity log with the prefix [Bot] so it can be grepped from batch mode.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PlaytestBot : MonoBehaviour {
        string map = "lq_e3m1", script = "idle"; float seconds = 30f;
        bool running; Vector2 move; float yaw; int deaths; Vector3 lastPos; float minY = float.MaxValue;
        string lastScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            if (System.Environment.GetEnvironmentVariable("LQ_BOT") != "1") return;
            var go = new GameObject("PlaytestBot"); DontDestroyOnLoad(go);
            var b = go.AddComponent<PlaytestBot>();
            b.map = System.Environment.GetEnvironmentVariable("LQ_BOT_MAP") ?? b.map;
            b.script = System.Environment.GetEnvironmentVariable("LQ_BOT_SCRIPT") ?? b.script;
            float.TryParse(System.Environment.GetEnvironmentVariable("LQ_BOT_SECONDS") ?? "30", out b.seconds);
            // LQ_BOT_DT=0.1 simulates a slow phone (fixed 10 fps frame time) — many mover/physics bugs only show at low fps.
            if (float.TryParse(System.Environment.GetEnvironmentVariable("LQ_BOT_DT") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dt) && dt > 0) Time.captureDeltaTime = dt;
            Debug.Log($"[Bot] boot map={b.map} script={b.script} seconds={b.seconds} captureDt={Time.captureDeltaTime}");
        }

        public static bool Verbose => System.Environment.GetEnvironmentVariable("LQ_BOT_VERBOSE") == "1";

        IEnumerator Start() {
            yield return new WaitForSeconds(1.0f);
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[Bot] no GameManager"); Quit(2); yield break; }
            // LQ_BOT_MAPS=a,b,c runs several maps in one session (LQ_BOT_SECONDS each); falls back to LQ_BOT_MAP.
            var maps = (System.Environment.GetEnvironmentVariable("LQ_BOT_MAPS") ?? map).Split(',');
            var summary = new System.Text.StringBuilder();
            foreach (var mraw in maps) {
                var m = mraw.Trim(); if (m.Length == 0) continue;
                gm.stats.ResetForNewGame(); deaths = 0; minY = float.MaxValue; running = false;
                yield return gm.StartCoroutine(gm.LoadLevel(m));
                Debug.Log("[Bot] level loaded: " + gm.currentMap);
                if (gm.currentMap != m || Player.Instance == null) { Debug.LogError($"[Bot] MAP {m} FAILED to load (scene={gm.currentMap}, player={(Player.Instance != null)})"); summary.Append($"{m}: LOAD FAILED\n"); continue; }
                running = true;
                Coroutine walker = script == "walk" ? StartCoroutine(WalkScript()) : null;
                float t0 = Time.time, nextLog = 0, nextShot = Time.time + 1f; string firstDeath = null;
                while (Time.time - t0 < seconds) {
                    if (Time.time >= nextLog) { LogState(Time.time - t0); nextLog = Time.time + 0.5f; }
                    if (shotEvery > 0 && Time.time >= nextShot) { Screenshot(m, Time.time - t0); nextShot = Time.time + shotEvery; }
                    var p = Player.Instance;
                    if (p != null && p.IsDead) {
                        deaths++;
                        var diag = Diagnose(p);
                        Debug.Log($"[Bot] DIED #{deaths} at {Fmt(p.transform.position)} scene={gm.currentMap} t={Time.time - t0:F1} lastPos={Fmt(lastPos)} minY={minY:F2}  " + diag);
                        firstDeath ??= $"t={Time.time - t0:F1} at {Fmt(p.transform.position)} {diag}";
                        yield return new WaitForSeconds(2.5f);
                        if (deaths >= 2) break;
                        var old = p; gm.RestartLevel();   // LoadLevel is async: wait for the *new* player object
                        float w = 0; while (w < 15f && !(Player.Instance != null && Player.Instance != old && Player.Instance.IsAlive)) { w += Time.deltaTime; yield return null; }
                        Debug.Log($"[Bot] restart {(Player.Instance != null && Player.Instance != old && Player.Instance.IsAlive ? "ok" : "FAILED (no new living player)")} after {w:F1}s");
                        minY = float.MaxValue;
                    }
                    yield return null;
                }
                running = false; if (walker != null) StopCoroutine(walker);
                GameInput.botMove = Vector2.zero; GameInput.botLook = Vector2.zero;
                Debug.Log($"[Bot] MAP {m} done deaths={deaths} finalScene={gm.currentMap} minY={minY:F2}");
                summary.Append($"{m}: deaths={deaths}{(firstDeath != null ? " first: " + firstDeath : "")} finalScene={gm.currentMap}\n");
            }
            Debug.Log("[Bot] SUMMARY\n" + summary);
            Debug.Log($"[Bot] done deaths={deaths} finalScene={gm.currentMap} minY={minY:F2}");
            Quit(0);
        }

        // LQ_BOT_SHOTS=<seconds> saves a PNG of the player camera every N seconds to LQ_BOT_SHOT_DIR (default Shots/).
        // Needs a graphics device: run the Editor WITHOUT -nographics under Xvfb + llvmpipe (see TESTING.md §5).
        static float shotEvery = float.TryParse(System.Environment.GetEnvironmentVariable("LQ_BOT_SHOTS") ?? "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var se) ? se : 0f;
        void Screenshot(string mapName, float t) {
            var cam = Camera.main; if (cam == null) return;
            try {
                int w = 1280, h = 720;
                var rt = RenderTexture.GetTemporary(w, h, 24);
                var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
                var old = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
                var dir = System.Environment.GetEnvironmentVariable("LQ_BOT_SHOT_DIR") ?? "Shots"; System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, $"{mapName}_{t:000.0}s.png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
                Debug.Log("[Bot] SHOT " + path);
            } catch (System.Exception e) { Debug.LogWarning("[Bot] screenshot failed: " + e.Message); shotEvery = 0; }
        }

        void LogState(float t) {
            var p = Player.Instance; var gm = GameManager.Instance;
            if (p == null) { Debug.Log($"[Bot] t={t:F1} no player (scene={gm?.currentMap})"); return; }
            var pos = p.transform.position; lastPos = pos; if (pos.y < minY) minY = pos.y;
            if (gm.currentMap != lastScene) { Debug.Log($"[Bot] scene -> {gm.currentMap}"); lastScene = gm.currentMap; Probe(p); }
            string floor = "none";
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit, 200f, ~LayerMask.GetMask("Player", "Monster"), QueryTriggerInteraction.Ignore))
                floor = $"{hit.distance:F2}m {hit.collider.gameObject.name}";
            Debug.Log($"[Bot] t={t:F1} q={Fmt(pos / QuakeUnits.Scale)} u={Fmt(pos)} hp={p.stats.health} grounded={p.motor.grounded} vel={Fmt(p.motor.velocity)} floor={floor} water={p.waterLevel}");
            if (System.Environment.GetEnvironmentVariable("LQ_BOT_PROBE") == "1") Probe(p);
        }

        /// <summary>Horizontal raycasts around the player: what walls exist near the spawn (finds missing clip/sky colliders).</summary>
        void Probe(Player p) {
            var c = p.Center; var sb = new StringBuilder("[Bot] probe ");
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.down, Vector3.up }) {
                if (Physics.Raycast(c, d, out var h, 200f, ~LayerMask.GetMask("Player", "Monster"), QueryTriggerInteraction.Ignore)) {
                    var q = h.collider.GetComponentInParent<QEntity>();
                    sb.Append($"{d}:{h.distance:F1}m {(q ? q.classname : h.collider.gameObject.name)} | ");
                } else sb.Append($"{d}:none | ");
            }
            Debug.Log(sb.ToString());
        }

        string Diagnose(Player p) {
            var sb = new StringBuilder();
            var pos = p.transform.position;
            if (!Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, 500f, ~LayerMask.GetMask("Player", "Monster"), QueryTriggerInteraction.Ignore)) sb.Append("NO FLOOR below (fell out of world?) ");
            var hits = Physics.OverlapSphere(pos + Vector3.up * 0.9f, 1.0f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits) if (h.isTrigger) sb.Append("trigger:" + h.gameObject.name + " ");
            return sb.ToString();
        }

        IEnumerator WalkScript() {
            while (running) {
                move = new Vector2(0, 1); yaw = 0; yield return new WaitForSeconds(2f);
                yaw = 45f; yield return new WaitForSeconds(1f); yaw = 0;
                GameInput.touchJump = true; yield return new WaitForSeconds(0.2f); GameInput.touchJump = false;
                yield return new WaitForSeconds(1.5f);
                var p = Player.Instance;
                if (p != null && p.motor.velocity.sqrMagnitude < 0.01f && p.motor.grounded) { yaw = 120f; yield return new WaitForSeconds(1f); yaw = 0; }
            }
        }

        void Update() {
            if (running && !GameInput.uiBlocked) {
                GameInput.botMove = move;
                GameInput.botLook = new Vector2(yaw, 0) * Time.deltaTime;
            }
        }

        static string Fmt(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";

        static void Quit(int code) {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
