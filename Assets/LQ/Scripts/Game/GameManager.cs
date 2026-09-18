using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LQ {
    /// <summary>Persistent game state: player stats across levels, level loading, intermission, death/restart, save game.</summary>
    public class GameManager : MonoBehaviour {
        public static GameManager Instance { get; private set; }
        public PlayerStats stats = new PlayerStats();
        PlayerStats levelEntryStats;             // for restarting the level after death
        public int skill = 1; public int runes;
        public float fov = 75f;
        public string currentMap; public float levelStartTime;
        public int levelKills, levelSecrets;
        public bool loading; public bool intermission;
        public const string MenuScene = "Menu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }

        void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            LoadSettings();
            if (GetComponent<HUD>() == null) gameObject.AddComponent<HUD>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) {
            currentMap = s.name;
            if (s.name == MenuScene) { GameInput.uiBlocked = true; Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; }
        }

        public void LoadSettings() {
            GameInput.mouseSensitivity = PlayerPrefs.GetFloat("sens_mouse", 2f);
            GameInput.touchSensitivity = PlayerPrefs.GetFloat("sens_touch", 0.25f);
            GameInput.invertY = PlayerPrefs.GetInt("invert_y", 0) == 1;
            fov = PlayerPrefs.GetFloat("fov", 75f);
            AudioListener.volume = PlayerPrefs.GetFloat("volume", 1f);
        }

        public void SaveSettings() {
            PlayerPrefs.SetFloat("sens_mouse", GameInput.mouseSensitivity); PlayerPrefs.SetFloat("sens_touch", GameInput.touchSensitivity);
            PlayerPrefs.SetInt("invert_y", GameInput.invertY ? 1 : 0); PlayerPrefs.SetFloat("fov", fov); PlayerPrefs.SetFloat("volume", AudioListener.volume);
            PlayerPrefs.Save();
        }

        // ---------------- flow ----------------
        public void NewGame(string firstMap = "start") {
            stats.ResetForNewGame(); runes = 0;
            StartCoroutine(LoadLevel(firstMap));
        }

        public bool HasSave => PlayerPrefs.HasKey("save_map");

        public void Continue() {
            if (!HasSave) { NewGame(); return; }
            var json = PlayerPrefs.GetString("save_stats", "");
            if (!string.IsNullOrEmpty(json)) stats = JsonUtility.FromJson<PlayerStats>(json);
            runes = PlayerPrefs.GetInt("save_runes", 0);
            StartCoroutine(LoadLevel(PlayerPrefs.GetString("save_map")));
        }

        void SaveGame(string map) {
            PlayerPrefs.SetString("save_map", map);
            PlayerPrefs.SetString("save_stats", JsonUtility.ToJson(stats));
            PlayerPrefs.SetInt("save_runes", runes);
            PlayerPrefs.Save();
        }

        public void ChangeLevel(string map, bool showIntermission) {
            if (loading) return;
            StartCoroutine(ChangeLevelRoutine(map, showIntermission));
        }

        IEnumerator ChangeLevelRoutine(string map, bool showIntermission) {
            loading = true;
            var p = Player.Instance;
            if (p != null) { p.motor.noclipInput = true; }
            if (showIntermission) {
                intermission = true;
                float t = Time.time - levelStartTime;
                var info = LevelInfo.Current;
                HUD.Instance.ShowIntermission(info != null ? info.message : currentMap, t, levelKills, info != null ? info.monsterCount : 0, levelSecrets, info != null ? info.secretCount : 0);
                SoundBank.Play2D("misc/talk.wav");
                float shown = Time.unscaledTime;
                while (Time.unscaledTime - shown < 1.5f) yield return null;
                while (!HUD.Instance.intermissionDismissed && Time.unscaledTime - shown < 30f) yield return null;
                HUD.Instance.HideIntermission();
                intermission = false;
            }
            stats.ResetKeysForLevel();
            yield return LoadLevel(map);
        }

        public IEnumerator LoadLevel(string map) {
            loading = true;
            GameInput.uiBlocked = true;
            HUD.Instance?.ShowLoading(map);
            yield return null;
            if (!Application.CanStreamedLevelBeLoaded(map)) {
                Debug.LogError("Map scene not in build: " + map);
                HUD.Instance?.HideLoading();
                if (map != MenuScene) { SceneManager.LoadScene(MenuScene); }
                loading = false; yield break;
            }
            var op = SceneManager.LoadSceneAsync(map);
            while (!op.isDone) yield return null;
            yield return null; // let LevelSetup.Start run
            HUD.Instance?.HideLoading();
            loading = false;
        }

        public void OnLevelStarted(LevelSetup level) {
            levelStartTime = Time.time; levelKills = 0; levelSecrets = 0;
            levelEntryStats = JsonUtility.FromJson<PlayerStats>(JsonUtility.ToJson(stats));
            SaveGame(currentMap);
            GameInput.uiBlocked = false; Time.timeScale = 1;
            if (!GameInput.IsMobile) Cursor.lockState = CursorLockMode.Locked;
            HUD.Instance?.OnLevelStart(level.info.message);
            var music = Resources.Load<AudioClip>("music/track" + (Random.Range(2, 12)).ToString("00"));
            HUD.Instance?.PlayMusic(music);
        }

        public void OnMonsterKilled(Monster m) { levelKills++; stats.kills++; }
        public void OnSecretFound() { levelSecrets++; stats.secrets++; }

        public void OnPlayerDied() {
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine() {
            yield return new WaitForSeconds(1.5f);
            HUD.Instance.ShowDeath();
            while (!HUD.Instance.deathDismissed) yield return null;
            HUD.Instance.HideDeath();
            RestartLevel();
        }

        public void RestartLevel() {
            if (levelEntryStats != null) stats = JsonUtility.FromJson<PlayerStats>(JsonUtility.ToJson(levelEntryStats));
            if (stats.health <= 0) stats.health = 100;
            StartCoroutine(LoadLevel(currentMap));
        }

        public void ToMainMenu() {
            Time.timeScale = 1; GameInput.uiBlocked = true;
            StartCoroutine(LoadLevel(MenuScene));
        }

        public void Pause(bool paused) {
            Time.timeScale = paused ? 0 : 1;
            GameInput.uiBlocked = paused;
            if (!GameInput.IsMobile) Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.Escape) && currentMap != MenuScene && !loading && !intermission) HUD.Instance?.TogglePause();
        }
    }
}
