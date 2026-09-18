# AGENTS.md — guide for developers and AI agents continuing this project

This file explains how the repository is organised, how it was built so far, and how to
continue. It is written for both humans and coding agents. Keep it up to date when you
change the pipeline.

---

## 1. What this project is

A rebuild of **LibreQuake** (libre Quake content) as a native **Android** game on
**Unity 2022.3.62f3 (LTS)**. Nothing of the original Quake engine is used: the game logic
is re-implemented in C# (`Assets/LQ/Scripts`), and the original LibreQuake data
(`.map` sources, PNG textures, `.mdl` models, `.wav` sounds) is imported by an Editor
pipeline into Unity scenes and assets.

Status (2026-09-18):

- Complete C# runtime: player (FPS + touch), 8 weapons, monsters/AI, doors, plats,
  buttons, triggers, secrets, level change, HUD, main menu, Android touch controls.
- Build pipeline imports any subset of the 75 LibreQuake maps.
- **First Android APK built successfully on Unity Cloud Build (Build Automation)**,
  build target `Android`, e1m1 only (`LQ_MAPS=lq_e1m1`), 43 MB, IL2CPP arm64-v8a + armeabi-v7a.
- Full-game (all maps) build not yet completed — see §6.

## 2. Repository map

| Path | What it is |
| --- | --- |
| `Assets/LQ/Scripts/` | Runtime C# (assembly `LQ.Runtime`, namespace `LQ`). Sub-folders: `Core` (palette, MDL/SPR loaders, combat), `Player`, `Weapons`, `Monsters`, `Entities` (doors, plats, triggers, items, liquids), `Game` (GameManager, LevelSetup, DemoRunner), `UI` (HUD, TouchControls, MainMenu). |
| `Assets/LQ/Editor/LQBuildPipeline.cs` | The whole import/build pipeline (namespace `LQ.EditorTools`). Menu `LibreQuake/…` and static entry points for `-executeMethod`. |
| `Assets/LQ/Shaders/` | Quake-style world, model, liquid, sky, sprite, particle shaders. |
| `Assets/LQ/Textures/` (2538 PNG), `Assets/LQ/Resources/` | LibreQuake assets (textures, models, sounds, HUD gfx, `palette.lmp`). Licence: `LICENSE-LibreQuake-assets.txt`. |
| `MapSources/*.map` | All 75 LibreQuake maps (TrenchBroom, Valve 220 format). Input for the importer. |
| `Assets/LQ/Generated/`, `Assets/LQ/Scenes/` | **Generated, git-ignored.** Meshes, materials and `.unity` scenes produced by the importer. Rebuild them, never commit them. |
| `tools/mdl.py`, `tools/stage_assets.py` | Python scripts that stage assets from the upstream LibreQuake repo (`python3 tools/stage_assets.py /path/to/LibreQuake .`). |
| `ci/build-android.yml` | Optional GitHub Actions workflow (game-ci). Not active — copy to `.github/workflows/` if you want Actions instead of Cloud Build. |
| `README.md` | User-facing readme (Arabic + English). |

Third-party: the `.map` → mesh importer is **Scopa** (`radiatoryang/scopa`, pinned commit
`1a31fb8df9825abe722542f063017a129e3bf9ba`, see `Packages/manifest.json`).

## 3. Build pipeline (`LQBuildPipeline`)

Static methods, all callable from the `LibreQuake` menu or `-executeMethod`:

| Method | Purpose |
| --- | --- |
| `ImportAll` | Import all maps in `MapSources` → scenes in `Assets/LQ/Scenes`. Slow (hours for 75 maps on 1 core; the importer is single-threaded). |
| `ImportSelected` | Import only the maps listed in env var `LQ_MAPS` (comma separated, default `lq_e1m1`). |
| `BuildAndroid` | Configure PlayerSettings (package `com.ayoub.librequake`, IL2CPP, ARM64+ARMv7, minSdk 23) and build `Builds/LibreQuake.apk` from `Menu` + all imported scenes. |
| `BuildAll` / `BuildSelected` | Import (all / `LQ_MAPS`) then `BuildAndroid`. |
| `CloudPreExport` | **Pre-Export method for Unity Cloud Build.** Runs `ConfigurePlayerSettings()` and imports maps (`LQ_MAPS` or all), then sets the scene list. Logs `[LQ] CloudPreExport done. Scenes: …`. |
| `BuildLinuxDemo` | Linux player used to record gameplay video with `DemoRunner` (`-lqdemo`). |

Conventions the importer relies on:

- Unity position = Quake `(x, z, y) / 32`; yaw → `Quaternion.Euler(0, -angle + 90, 0)`.
- Scopa bug: leave `nonsolidEntities` empty (a null `colliderResults` NRE otherwise) and
  strip colliders from `*illusionary*` entities in post-processing.
- Scenes get one `Level` GameObject with `LevelInfo` (map name, message, worldtype) and
  `LevelSetup` (spawns all `QEntity` behaviours at runtime).
- `ProjectSettings/AudioManager.asset` has `m_DisableAudio: 1` for headless import;
  `EnsureAudioEnabled()` flips it back before a player build.

### Unity rule that bit us once
Every `MonoBehaviour` that is serialized into a scene **must live in a file with the same
name as the class**, otherwise Unity records "missing script" in the build. All
MonoBehaviours are now one-class-per-file. Keep it that way (the compile check does not
catch this; only the build log warning `Script attached to '…' is missing` does).

## 4. Building the APK — Unity Cloud Build (current, working)

Project **LibreQuake** in Unity Cloud (org `11270707950591`, project id
`88a0a8eb-6e00-4626-b716-27b5af41c5f1`), owner account `ayoubteke12@gmail.com`.

- Source control: Git over SSH `git@github.com:ayoub5550/my-librequake.git`, branch `main`.
  The Cloud Build public key is installed as a **read-only deploy key** on this repo
  (Settings → Deploy keys). HTTPS is not supported by Cloud Build for GitHub without
  OAuth, so keep SSH.
- Build target `Android` (`buildtargetid` = `android`): Unity 2022.3.62f3, Windows 11 24H2,
  MICRO machine (free tier), bundle `com.ayoub.librequake`, auto-generated debug
  keystore, **Pre-Export method `LQ.EditorTools.LQBuildPipeline.CloudPreExport`**,
  environment variable `LQ_MAPS` (maps to import; empty/unset = all maps).
- Trigger: Build history → *Build* (or *Configurations → Android → Build*). Free tier:
  200 Windows build-minutes/month, 2 concurrent builds. e1m1-only build took **25 min**
  (≈ 11 min machine setup + 30 s import + 7 min player build + upload).
- Artifacts: `Android.apk` under the build's *Download* menu.
- Debug keystore is auto-generated: the APK is for sideloading/testing. For a Play Store
  release upload a real keystore under *Credentials*.

REST (same as the dashboard uses): `https://build-automation.services.api.unity.com/v2/orgs/{org}/projects/{project}/buildtargets/android/builds`
(bearer token from a logged-in dashboard session or a Unity Cloud API key).

## 5. Building the APK — alternatives

- **GitHub Actions**: `ci/build-android.yml` (game-ci/unity-builder). Needs repository
  secrets `UNITY_EMAIL`, `UNITY_PASSWORD` (+ `UNITY_LICENSE` for Personal) and the file
  copied to `.github/workflows/build-android.yml`. Never validated end-to-end.
- **Local**: Unity 2022.3.62f3 + Android Build Support (SDK/NDK/OpenJDK). Menu
  `LibreQuake` steps 1→2→3 → *Build Android APK*, or
  `Unity -batchmode -nographics -quit -projectPath . -buildTarget Android -executeMethod LQ.EditorTools.LQBuildPipeline.BuildAll`.
  Headless Linux sandboxes without GPU/root may crash on shader import — Cloud Build is
  the reliable path.

## 6. What to do next (priority order)

1. **Full build**: clear `LQ_MAPS` on the Cloud Build target (Configurations → Android →
   Advanced → Environment variables) and build. Watch the free-tier minute budget: import
   of all 75 maps is single-threaded and may take > 1 h. If it exceeds the budget, build
   episode by episode (`LQ_MAPS=start,lq_e1m1,…,lq_e1m8`) or cache `Assets/LQ/Scenes`.
2. **Play-test on a device** and fix what the log warns about (search the Cloud Build log
   for `[warning]` and `[LQ]`).
3. Known code TODOs: `Monster.NoiseAt` should ignore monsters with `AmbushMarker`; the
   skill multiplier in `GameManager` is not applied yet; no save/load between sessions;
   music (`--music` staging option) is disabled by default to keep the APK small.
4. Gameplay video: run a Linux/Windows player with `-lqdemo -lqdemo-map lq_e1m1 -lqdemo-frames DIR`
   (`DemoRunner` dumps `f%05d.png` at 30 fps) and encode with ffmpeg.

## 7. Working conventions

- Commit source only; never commit `Library/`, `Builds/`, `Assets/LQ/Generated`, `Assets/LQ/Scenes`, APKs.
- Keep `README.md` (Arabic + English) in sync with build instructions.
- Licences: code MIT (`LICENSE`); LibreQuake assets under their own licence
  (`LICENSE-LibreQuake-assets.txt`). Do not add proprietary Quake data.
- Language: the repository owner communicates in Arabic; code, comments and this file are in English.
