using UnityEngine;
using UnityEngine.UI;

namespace LQ {
    /// <summary>Main menu scene controller (builds its own UI).</summary>
    public class MainMenu : MonoBehaviour {
        Canvas canvas; RectTransform root, main, episodes, options;

        void Start() {
            GameInput.uiBlocked = true;
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("MenuCamera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.04f, 0.03f);
            canvas = UIKit.CreateCanvas("MenuCanvas", 5);
            root = canvas.GetComponent<RectTransform>();
            var bg = UIKit.Panel(root, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, 1));
            var conback = UIKit.HudSprite("conback"); if (conback != null) bg.GetComponent<Image>().sprite = conback;
            var shade = UIKit.Panel(root, "shade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.45f));
            UIKit.Text(root, "Title", new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 90), "LIBREQUAKE", 72, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.45f));
            UIKit.Text(root, "Sub", new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(900, 40), "Unity / Android edition", 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f, 0.8f));

            main = UIKit.Panel(root, "Main", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gm = GameManager.Instance;
            float y = 60;
            UIKit.Button(main, "New", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 58), "New Game", () => gm.NewGame("start")); y -= 70;
            var cont = UIKit.Button(main, "Continue", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 58), "Continue", () => gm.Continue()); y -= 70;
            cont.interactable = gm.HasSave;
            UIKit.Button(main, "Episodes", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 58), "Select Episode", () => Show(episodes)); y -= 70;
            UIKit.Button(main, "Options", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 58), "Options", () => Show(options)); y -= 70;
            UIKit.Button(main, "Quit", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(380, 58), "Quit", Application.Quit);

            episodes = UIKit.Panel(root, "Episodes", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            string[][] eps = { new[] { "Welcome to LibreQuake", "start" }, new[] { "Episode 1", "lq_e1m1" }, new[] { "Episode 2", "lq_e2m1" }, new[] { "Episode 3", "lq_e3m1" }, new[] { "Episode 4", "lq_e4m1" }, new[] { "Episode 0 (bonus)", "lq_e0m1" } };
            y = 120;
            foreach (var e in eps) {
                var map = e[1];
                var b = UIKit.Button(episodes, map, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 52), e[0], () => { gm.stats.ResetForNewGame(); gm.StartCoroutine(gm.LoadLevel(map)); }, 24);
                b.interactable = Application.CanStreamedLevelBeLoaded(map);
                y -= 62;
            }
            UIKit.Button(episodes, "Back", new Vector2(0.5f, 0.5f), new Vector2(0, y - 20), new Vector2(420, 52), "Back", () => Show(main), 24);
            episodes.gameObject.SetActive(false);

            options = UIKit.Panel(root, "Options", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildOptions(options);
            UIKit.Button(options, "Back", new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(380, 56), "Back", () => Show(main));
            options.gameObject.SetActive(false);

            UIKit.Text(root, "Credits", new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1200, 30), "Game data: LibreQuake project (BSD licence) - Engine: Unity - github.com/ayoub5550/my-librequake", 16, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.5f));
            HUD.Instance?.PlayMusic(Resources.Load<AudioClip>("music/track02"));
        }

        void BuildOptions(Transform parent) {
            var gm = GameManager.Instance;
            UIKit.Text(parent, "SensL", new Vector2(0.5f, 0.5f), new Vector2(-180, 120), new Vector2(200, 40), "Look sensitivity", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Sens", new Vector2(0.5f, 0.5f), new Vector2(100, 120), new Vector2(260, 28), 0.05f, 0.8f, GameInput.touchSensitivity, v => { GameInput.touchSensitivity = v; GameInput.mouseSensitivity = v * 8f; gm.SaveSettings(); });
            UIKit.Text(parent, "VolL", new Vector2(0.5f, 0.5f), new Vector2(-180, 60), new Vector2(200, 40), "Volume", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Vol", new Vector2(0.5f, 0.5f), new Vector2(100, 60), new Vector2(260, 28), 0f, 1f, AudioListener.volume, v => { AudioListener.volume = v; gm.SaveSettings(); });
            UIKit.Toggle(parent, "Invert", new Vector2(0.5f, 0.5f), new Vector2(-180, 0), "Invert look", GameInput.invertY, v => { GameInput.invertY = v; gm.SaveSettings(); });
            UIKit.Text(parent, "FovL", new Vector2(0.5f, 0.5f), new Vector2(-180, -60), new Vector2(200, 40), "Field of view", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Fov", new Vector2(0.5f, 0.5f), new Vector2(100, -60), new Vector2(260, 28), 60f, 110f, gm.fov, v => { gm.fov = v; gm.SaveSettings(); });
            UIKit.Toggle(parent, "Skill", new Vector2(0.5f, 0.5f), new Vector2(-180, -120), "Hard mode (more damage)", gm.skill >= 2, v => { gm.skill = v ? 2 : 1; });
        }

        void Show(RectTransform panel) {
            main.gameObject.SetActive(panel == main); episodes.gameObject.SetActive(panel == episodes); options.gameObject.SetActive(panel == options);
        }
    }
}
