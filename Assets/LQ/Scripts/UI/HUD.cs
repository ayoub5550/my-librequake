using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQ {
    /// <summary>Status bar, messages, touch controls, pause / death / intermission / loading screens. Lives on the GameManager.</summary>
    public class HUD : MonoBehaviour {
        public static HUD Instance { get; private set; }
        Canvas canvas; RectTransform root, gameRoot;
        Text healthText, armorText, ammoText, centerText, printText, levelNameText;
        Image face, armorIcon, ammoIcon, damageFlash, pickupFlash, keySilver, keyGold, crosshair;
        readonly List<Image> weaponIcons = new List<Image>();
        readonly Image[] powerupIcons = new Image[4];
        float centerUntil, printUntil;
        TouchControls touch;
        AudioSource music;
        // screens
        RectTransform pauseScreen, deathScreen, interScreen, loadingScreen;
        Text interText, loadingText;
        public bool intermissionDismissed, deathDismissed;
        bool paused;
        static string pendingCenter; static string pendingPrint;

        void Awake() {
            Instance = this;
            canvas = UIKit.CreateCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            root = canvas.GetComponent<RectTransform>();
            BuildGameHud();
            BuildScreens();
            touch = gameObject.AddComponent<TouchControls>();
            touch.Build(gameRoot);
            gameRoot.gameObject.SetActive(false);
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.volume = 0.35f; music.spatialBlend = 0;
        }

        // ---------------- build ----------------
        void BuildGameHud() {
            gameRoot = UIKit.Panel(root, "Game", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            damageFlash = UIKit.Panel(gameRoot, "DamageFlash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 0, 0, 0)).GetComponent<Image>();
            pickupFlash = UIKit.Panel(gameRoot, "PickupFlash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 0.9f, 0.5f, 0)).GetComponent<Image>();
            crosshair = UIKit.Image(gameRoot, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14), null, new Color(1f, 0.9f, 0.6f, 0.8f));
            crosshair.sprite = MakeCrosshair();

            // status bar (bottom centre)
            var bar = UIKit.Rect(gameRoot, "StatusBar", new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(640, 72));
            var barBg = bar.gameObject.AddComponent<Image>(); barBg.color = new Color(0, 0, 0, 0.45f); barBg.raycastTarget = false;
            // armor
            armorIcon = UIKit.Image(bar, "ArmorIcon", new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(48, 48), UIKit.HudSprite("sb_armor1"));
            armorText = UIKit.Text(bar, "Armor", new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(90, 60), "0", 40, TextAnchor.MiddleLeft);
            // face + health
            face = UIKit.Image(bar, "Face", new Vector2(0, 0.5f), new Vector2(170, 0), new Vector2(48, 48), UIKit.HudSprite("face1"));
            healthText = UIKit.Text(bar, "Health", new Vector2(0, 0.5f), new Vector2(224, 0), new Vector2(110, 60), "100", 44, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.7f));
            // ammo
            ammoIcon = UIKit.Image(bar, "AmmoIcon", new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(48, 48), UIKit.HudSprite("sb_shells"));
            ammoText = UIKit.Text(bar, "Ammo", new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(100, 60), "25", 40, TextAnchor.MiddleRight);
            // keys / powerups (above bar, left)
            keySilver = UIKit.Image(gameRoot, "KeySilver", new Vector2(0.5f, 0f), new Vector2(-300, 86), new Vector2(28, 28), UIKit.HudSprite("sb_key1"));
            keyGold = UIKit.Image(gameRoot, "KeyGold", new Vector2(0.5f, 0f), new Vector2(-268, 86), new Vector2(28, 28), UIKit.HudSprite("sb_key2"));
            string[] pw = { "sb_quad", "sb_invuln", "sb_invis", "sb_suit" };
            for (int i = 0; i < 4; i++) powerupIcons[i] = UIKit.Image(gameRoot, "PW" + i, new Vector2(0.5f, 0f), new Vector2(-230 + i * 34, 86), new Vector2(28, 28), UIKit.HudSprite(pw[i]));
            // weapon icons (above bar, right)
            for (int w = 1; w < WeaponDefs.Count; w++) {
                var img = UIKit.Image(gameRoot, "W" + w, new Vector2(0.5f, 0f), new Vector2(-60 + (w - 1) * 52, 86), new Vector2(48, 24), UIKit.HudSprite(WeaponDefs.Get(w).hudIcon));
                weaponIcons.Add(img);
            }
            // texts
            centerText = UIKit.Text(gameRoot, "Center", new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(900, 200), "", 30);
            printText = UIKit.Text(gameRoot, "Print", new Vector2(0, 1), new Vector2(14, -10), new Vector2(700, 40), "", 24, TextAnchor.UpperLeft);
            levelNameText = UIKit.Text(gameRoot, "LevelName", new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(700, 40), "", 22, TextAnchor.UpperCenter, new Color(1, 0.85f, 0.6f, 0.7f));
            // pause button (top right)
            var pauseBtn = UIKit.Button(gameRoot, "PauseBtn", new Vector2(1, 1), new Vector2(-12, -12), new Vector2(64, 48), "II", TogglePause, 26);
            pauseBtn.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        }

        Sprite MakeCrosshair() {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point;
            var px = new Color32[256];
            for (int i = 0; i < 256; i++) px[i] = new Color32(0, 0, 0, 0);
            for (int i = 2; i < 14; i++) { if (i > 5 && i < 10) continue; px[7 * 16 + i] = new Color32(255, 230, 150, 255); px[i * 16 + 7] = new Color32(255, 230, 150, 255); }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }

        void BuildScreens() {
            // pause
            pauseScreen = UIKit.Panel(root, "Pause", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.75f));
            pauseScreen.GetComponent<Image>().raycastTarget = true;
            UIKit.Text(pauseScreen, "Title", new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 60), "PAUSED", 44);
            UIKit.Button(pauseScreen, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(360, 56), "Resume", TogglePause);
            BuildOptions(pauseScreen, new Vector2(0, 40));
            UIKit.Button(pauseScreen, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(360, 56), "Restart level", () => { TogglePause(); GameManager.Instance.RestartLevel(); });
            UIKit.Button(pauseScreen, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(360, 56), "Main menu", () => { paused = false; pauseScreen.gameObject.SetActive(false); GameManager.Instance.ToMainMenu(); });
            pauseScreen.gameObject.SetActive(false);
            // death
            deathScreen = UIKit.Panel(root, "Death", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.3f, 0, 0, 0.5f));
            deathScreen.GetComponent<Image>().raycastTarget = true;
            UIKit.Text(deathScreen, "T", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 80), "You died", 54);
            UIKit.Button(deathScreen, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(360, 60), "Try again", () => deathDismissed = true);
            deathScreen.gameObject.SetActive(false);
            // intermission
            interScreen = UIKit.Panel(root, "Intermission", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.85f));
            interScreen.GetComponent<Image>().raycastTarget = true;
            var complete = UIKit.Image(interScreen, "Complete", new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(384, 48), UIKit.HudSprite("complete"));
            interText = UIKit.Text(interScreen, "T", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(800, 300), "", 32);
            UIKit.Button(interScreen, "Next", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(360, 60), "Continue", () => intermissionDismissed = true);
            interScreen.gameObject.SetActive(false);
            // loading
            loadingScreen = UIKit.Panel(root, "Loading", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 1));
            var conback = UIKit.HudSprite("conback");
            if (conback != null) { var bg = UIKit.Panel(loadingScreen, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0.6f)); bg.GetComponent<Image>().sprite = conback; }
            loadingText = UIKit.Text(loadingScreen, "T", new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(800, 80), "Loading...", 40);
            var ld = UIKit.Image(loadingScreen, "ld", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(288, 48), UIKit.HudSprite("loading"));
            loadingScreen.gameObject.SetActive(false);
        }

        void BuildOptions(Transform parent, Vector2 origin) {
            UIKit.Text(parent, "SensL", new Vector2(0.5f, 0.5f), origin + new Vector2(-180, 0), new Vector2(200, 40), "Look sensitivity", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Sens", new Vector2(0.5f, 0.5f), origin + new Vector2(100, 0), new Vector2(260, 28), 0.05f, 0.8f, GameInput.touchSensitivity, v => { GameInput.touchSensitivity = v; GameInput.mouseSensitivity = v * 8f; GameManager.Instance.SaveSettings(); });
            UIKit.Text(parent, "VolL", new Vector2(0.5f, 0.5f), origin + new Vector2(-180, -50), new Vector2(200, 40), "Volume", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Vol", new Vector2(0.5f, 0.5f), origin + new Vector2(100, -50), new Vector2(260, 28), 0f, 1f, AudioListener.volume, v => { AudioListener.volume = v; GameManager.Instance.SaveSettings(); });
            UIKit.Toggle(parent, "Invert", new Vector2(0.5f, 0.5f), origin + new Vector2(-180, -100), "Invert look", GameInput.invertY, v => { GameInput.invertY = v; GameManager.Instance.SaveSettings(); });
            UIKit.Text(parent, "FovL", new Vector2(0.5f, 0.5f), origin + new Vector2(-180, -150), new Vector2(200, 40), "Field of view", 22, TextAnchor.MiddleLeft);
            UIKit.Slider(parent, "Fov", new Vector2(0.5f, 0.5f), origin + new Vector2(100, -150), new Vector2(260, 28), 60f, 110f, GameManager.Instance.fov, v => { GameManager.Instance.fov = v; if (Player.Instance && Player.Instance.cam) Player.Instance.cam.fieldOfView = v; GameManager.Instance.SaveSettings(); });
        }

        // ---------------- public API ----------------
        public static void CenterPrint(string msg) {
            if (Instance == null) { pendingCenter = msg; return; }
            Instance.centerText.text = msg.Replace("\\n", "\n"); Instance.centerUntil = Time.unscaledTime + 3f + msg.Length * 0.03f;
        }

        public static void Print(string msg) {
            if (Instance == null) { pendingPrint = msg; return; }
            Instance.printText.text = msg; Instance.printUntil = Time.unscaledTime + 3f;
        }

        public void OnLevelStart(string levelName) {
            gameRoot.gameObject.SetActive(true);
            centerText.text = ""; printText.text = "";
            levelNameText.text = levelName ?? ""; levelNameUntil = Time.unscaledTime + 5f;
            if (!string.IsNullOrEmpty(levelName)) CenterPrint(levelName);
            touch.SetVisible(GameInput.IsMobile || TouchControls.forceShow);
        }
        float levelNameUntil;

        public void PlayMusic(AudioClip clip) {
            if (clip == null) { music.Stop(); return; }
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip; music.Play();
        }

        public void ShowLoading(string map) { loadingScreen.gameObject.SetActive(true); loadingText.text = "Loading " + map + "..."; gameRoot.gameObject.SetActive(false); }
        public void HideLoading() { loadingScreen.gameObject.SetActive(false); }

        public void ShowIntermission(string level, float time, int kills, int totalMonsters, int secrets, int totalSecrets) {
            intermissionDismissed = false;
            interScreen.gameObject.SetActive(true); gameRoot.gameObject.SetActive(false);
            int m = (int)(time / 60), s = (int)(time % 60);
            interText.text = $"{level}\n\nTime: {m}:{s:00}\nSecrets: {secrets} / {totalSecrets}\nKills: {kills} / {totalMonsters}";
        }
        public void HideIntermission() { interScreen.gameObject.SetActive(false); }

        public void ShowDeath() { deathDismissed = false; deathScreen.gameObject.SetActive(true); }
        public void HideDeath() { deathScreen.gameObject.SetActive(false); }

        public void TogglePause() {
            if (GameManager.Instance.loading || GameManager.Instance.currentMap == GameManager.MenuScene) return;
            paused = !paused;
            pauseScreen.gameObject.SetActive(paused);
            GameManager.Instance.Pause(paused);
            SoundBank.Play2D("misc/menu1.wav", 0.6f);
        }

        // ---------------- per frame ----------------
        void Update() {
            if (pendingCenter != null) { CenterPrint(pendingCenter); pendingCenter = null; }
            if (pendingPrint != null) { Print(pendingPrint); pendingPrint = null; }
            var gm = GameManager.Instance;
            bool inGame = gm != null && gm.currentMap != GameManager.MenuScene && !gm.loading;
            if (gameRoot.gameObject.activeSelf != (inGame && !gm.intermission)) gameRoot.gameObject.SetActive(inGame && !gm.intermission);
            if (!inGame) return;
            if (Time.unscaledTime > centerUntil) centerText.text = "";
            if (Time.unscaledTime > printUntil) printText.text = "";
            if (Time.unscaledTime > levelNameUntil) levelNameText.text = "";
            var p = Player.Instance; if (p == null) return;
            var st = p.stats;
            healthText.text = Mathf.Max(0, st.health).ToString();
            healthText.color = st.health <= 25 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.9f, 0.7f);
            armorText.text = st.armor.ToString();
            armorIcon.enabled = st.armorType != ArmorType.None;
            armorIcon.sprite = UIKit.HudSprite(st.armorType == ArmorType.Red ? "sb_armor3" : st.armorType == ArmorType.Yellow ? "sb_armor2" : "sb_armor1");
            var wd = WeaponDefs.Get(st.currentWeapon);
            if (wd.ammoType.HasValue) {
                ammoText.text = st.ammo[(int)wd.ammoType.Value].ToString(); ammoIcon.enabled = true;
                ammoIcon.sprite = UIKit.HudSprite(wd.ammoType.Value == AmmoType.Shells ? "sb_shells" : wd.ammoType.Value == AmmoType.Nails ? "sb_nails" : wd.ammoType.Value == AmmoType.Rockets ? "sb_rocket" : "sb_cells");
            } else { ammoText.text = ""; ammoIcon.enabled = false; }
            // face
            int faceIdx = st.health >= 100 ? 1 : st.health >= 80 ? 2 : st.health >= 60 ? 3 : st.health >= 40 ? 4 : 5;
            string faceName = st.HasQuad ? "face_quad" : st.HasPent ? "face_invul2" : st.HasRing ? "face_invis" : (p.damageFlash > 0.3f ? "face_p" + faceIdx : "face" + faceIdx);
            var fs = UIKit.HudSprite(faceName); if (fs != null) face.sprite = fs;
            for (int i = 0; i < weaponIcons.Count; i++) {
                int w = i + 1; bool has = st.HasWeapon(w);
                weaponIcons[i].enabled = has;
                var def = WeaponDefs.Get(w);
                weaponIcons[i].sprite = UIKit.HudSprite(w == st.currentWeapon ? "inv2_" + def.hudIcon.Substring(4) : def.hudIcon);
                weaponIcons[i].color = w == st.currentWeapon ? Color.white : new Color(1, 1, 1, 0.7f);
            }
            keySilver.enabled = st.silverKey; keyGold.enabled = st.goldKey;
            powerupIcons[0].enabled = st.HasQuad; powerupIcons[1].enabled = st.HasPent; powerupIcons[2].enabled = st.HasRing; powerupIcons[3].enabled = st.HasSuit;
            damageFlash.color = new Color(1f, 0.1f, 0.05f, p.damageFlash * 0.45f);
            float lava = p.liquidType == LiquidType.Lava && p.waterLevel >= 3 ? 0.4f : p.liquidType == LiquidType.Slime && p.waterLevel >= 3 ? 0.25f : p.waterLevel >= 3 ? 0.18f : 0;
            pickupFlash.color = p.waterLevel >= 3 ? (p.liquidType == LiquidType.Lava ? new Color(1f, 0.3f, 0f, lava) : p.liquidType == LiquidType.Slime ? new Color(0.1f, 0.6f, 0.1f, lava) : new Color(0.1f, 0.3f, 0.6f, lava)) : new Color(1f, 0.9f, 0.5f, p.pickupFlash * 0.3f);
            crosshair.enabled = p.IsAlive;
        }
    }
}
