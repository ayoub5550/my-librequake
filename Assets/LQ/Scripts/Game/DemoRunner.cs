using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LQ {
    /// <summary>
    /// Headless gameplay demo used to record the showcase video on CI.
    /// Enabled with the command line flag "-lqdemo" (optional: -lqdemo-map NAME, -lqdemo-frames DIR, -lqdemo-seconds N).
    /// Drives GameInput like a touch player would, shows the Android touch HUD, and writes one PNG per frame at 30 fps.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class DemoRunner : MonoBehaviour {
        string map = "lq_e1m1"; string framesDir; float seconds = 60f; int frame;
        bool running; Vector2 move; float yaw, pitch; bool fire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-lqdemo") < 0 && System.Environment.GetEnvironmentVariable("LQ_DEMO") != "1") return;
            var go = new GameObject("DemoRunner"); DontDestroyOnLoad(go);
            var d = go.AddComponent<DemoRunner>();
            for (int i = 0; i < args.Length - 1; i++) {
                if (args[i] == "-lqdemo-map") d.map = args[i + 1];
                if (args[i] == "-lqdemo-frames") d.framesDir = args[i + 1];
                if (args[i] == "-lqdemo-seconds") float.TryParse(args[i + 1], out d.seconds);
            }
        }

        IEnumerator Start() {
            TouchControls.forceShow = true;
            if (!string.IsNullOrEmpty(framesDir)) { Directory.CreateDirectory(framesDir); Time.captureFramerate = 30; }
            yield return new WaitForSeconds(2.5f);                       // show the main menu
            var gm = GameManager.Instance;
            gm.stats.ResetForNewGame();
            yield return gm.StartCoroutine(gm.LoadLevel(map));
            yield return new WaitForSeconds(1f);
            running = true;
            StartCoroutine(Script());
            yield return new WaitForSeconds(seconds);
            Debug.Log($"[Demo] done, {frame} frames");
            Application.Quit();
        }

        IEnumerator Script() {
            // a little choreography: look around, walk forward, strafe, shoot, jump, switch weapon...
            yield return Look(0, 0, 1.0f);
            yield return Look(90, 0, 1.5f);
            yield return Look(-180, 0, 3f);
            yield return Look(90, 0, 1.5f);
            while (running) {
                yield return Walk(new Vector2(0, 1), 3f, 15f);
                yield return Shoot(1.2f);
                yield return Walk(new Vector2(0, 1), 2f, -35f);
                yield return Jump();
                yield return Walk(new Vector2(0.7f, 0.7f), 1.5f, 20f);
                yield return Shoot(0.8f);
                GameInput.touchNextWeapon = true; TouchControls.Instance?.DemoHold(">", true); yield return new WaitForSeconds(0.15f); TouchControls.Instance?.DemoHold(">", false);
                yield return Walk(new Vector2(0, 1), 2.5f, 40f);
                yield return Look(-70, 10, 1f);
                yield return Shoot(1.0f);
                yield return Look(0, -10, 0.5f);
                yield return Walk(new Vector2(-0.7f, 0.7f), 1.5f, -20f);
                var p = Player.Instance;
                if (p != null && !p.IsAlive) { yield return new WaitForSeconds(1.5f); GameManager.Instance.RestartLevel(); yield return new WaitForSeconds(2f); }
            }
        }

        IEnumerator Look(float dyaw, float dpitch, float dur) { yaw = dyaw / dur; pitch = dpitch / dur; yield return new WaitForSeconds(dur); yaw = 0; pitch = 0; }
        IEnumerator Walk(Vector2 dir, float dur, float turn) { move = dir; yaw = turn / dur; TouchControls.Instance?.DemoStick(dir, true); yield return new WaitForSeconds(dur); move = Vector2.zero; yaw = 0; TouchControls.Instance?.DemoStick(dir, false); }
        IEnumerator Shoot(float dur) { fire = true; TouchControls.Instance?.DemoHold("FIRE", true); yield return new WaitForSeconds(dur); fire = false; TouchControls.Instance?.DemoHold("FIRE", false); }
        IEnumerator Jump() { GameInput.touchJump = true; TouchControls.Instance?.DemoHold("JUMP", true); yield return new WaitForSeconds(0.25f); GameInput.touchJump = false; TouchControls.Instance?.DemoHold("JUMP", false); }

        void Update() {
            if (running && !GameInput.uiBlocked) {
                GameInput.botMove = move;
                GameInput.botLook = new Vector2(yaw, pitch) * Time.deltaTime;
                GameInput.touchFire = fire;
            }
        }

        void LateUpdate() {
            if (string.IsNullOrEmpty(framesDir)) return;
            StartCoroutine(Capture(frame++));
        }

        IEnumerator Capture(int idx) {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(framesDir, $"f{idx:D5}.png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
