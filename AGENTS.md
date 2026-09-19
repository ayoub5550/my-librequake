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

Status (2026-09-19):

- Complete C# runtime: player (FPS + touch), 8 weapons, monsters/AI, doors, plats,
  buttons, triggers, secrets, level change, HUD, main menu, Android touch controls.
- Build pipeline imports any subset of the 75 LibreQuake maps.
- **Android APKs built on Unity Cloud Build (Build Automation)**, target `Android`:
  - Build #1: e1m1 only (`LQ_MAPS=lq_e1m1`), 43 MB, 25 min.
  - Build #2: **full game, all 40 single-player maps** (`LQ_MAPS` empty), 148 MB, 33 min,
    published as GitHub Release `v0.1.0`. Deathmatch (`lqdm*`), `dev`, `start_e0` and the
    `b_*` item-box maps are excluded on purpose by `MapList()`.
- **Bug found on device after build #2: monsters, weapons, items, sprites and particles were
  invisible.** Cause: `LQ/Model`, `LQ/Sprite`, `LQ/Particle` (and the `Unlit/Texture`
  fallback) are only referenced at runtime via `Shader.Find`, so Unity stripped them from the
  player (the build log's "Serialized binary data for shader …" lines listed only World/Liquid/Sky).
  Fix: shaders moved to `Assets/LQ/Resources/Shaders/` (Resources are always included) and
  pinned in `GraphicsSettings.m_AlwaysIncludedShaders` (also enforced by
  `EnsureShadersIncluded()` in `ConfigurePlayerSettings`).
  - Build #4 (commit `bacdbe6`, 29 min): **fix verified in the build log** — all six LQ shaders
    are now serialized. Published as GitHub Release `v0.1.1` (148 MB). Also ships bigger touch
    buttons (FIRE 190 px, JUMP 110 px, WPN +/-) and aim-while-firing. Device test: weapons and
    monsters render ✅, but the EASY/NORMAL/HARD portals in the `start` map did not teleport.
  - Build #5 (commit `c578317`, 38 min): **start-map portal fix**, Release `v0.1.2`. Cause was a
    registry race: `LevelSetup.Awake` called `QEntity.ClearRegistry()` after entities from the
    same scene had already registered, so `trigger_teleport` could not resolve its `target`.
    Now `PruneRegistry()` only drops destroyed entries, `FindByTargetName` falls back to
    `FindObjectsOfType<QEntity>`, `TriggerTeleport` logs a warning when no destination exists,
    `TriggerBase` falls back to a BoxCollider (and forces MeshColliders convex) and the prepass
    keeps `*tele*` brushes solid. Awaiting device test.
  - Build #6 (commit `de46cd1`, 32 min): Release `v0.1.3`. Contents: `func_episodegate` is
    removed unless the player owns that episode's rune and `func_bossgate` is removed once all
    four runes are owned (Quake QC semantics — before this every EPISODE entrance in `start`
    looked closed); new entities `trap_spikeshooter`/`trap_shooter` (`TrapSpikeshooter`),
    `misc_fireball`, `air_bubbles`, `event_lightning`; `monster_ogre_marksman`; per-skill
    spawnflag filtering (NOT_IN_EASY 256 / NORMAL 512 / HARD 1024 → entity destroyed after the
    player start is resolved). After this an audit of all 40 SP maps against `LevelSetup`,
    `Item` and `MonsterDefs` shows no unhandled gameplay classnames — only `func_group`
    (editor grouping), `path_corner` (consumed by `func_train`) and `viewthing` remain.
    Awaiting device test.
  - Build #7 (commit `b7df71f`, 29 min, 2026-09-19): Release `v0.1.4` (148 MB). Contents: all
    headless-playtest fixes (`GetOrAdd`, `CarryPlayer`, lift guard rail, Quake-accurate lava
    damage, particle reset, multi-map `PlaytestBot`). Build log verified: all six `LQ/*` shaders
    serialized, 228 .wav sounds. **Free Cloud Build minutes are now essentially exhausted
    (~190/200 used) until the monthly reset.** Awaiting device test.
- **Headless playtesting works** (2026-09-19): `PlaytestBot` + `LQBuildPipeline.BotPlay` run the
  real game in Editor play mode without a GPU and log the player's position/health/deaths.
  Full instructions in **`TESTING.md`** — run the smoke set before every push. First findings,
  all fixed in the commit after `924093b`:
  - `GetComponent<T>() ?? AddComponent<T>()` is broken in the Editor (fake-null) → use
    `gameObject.GetOrAdd<T>()`. In the Editor this aborted `LevelSetup.Start` so no player spawned.
  - `Mover.CarryPlayer` never detected the rider: `Physics.SphereCast` ignores colliders the
    sphere starts inside, and the cast started at the feet. Lifts only "worked" because the
    door's bounds-push branch shoved the player down. Cast now starts `radius + 0.1` above the
    feet and reaches `0.45 + |delta|` (slow phones move 0.5 m per frame).
  - **Episode 3 instant death** (owner report, reproduced by the bot): the `lq_e3m1` start lift
    is open at the front; one step forward while it descends = 17 m fall into the slime
    `trigger_hurt`. The map is like that in LibreQuake too. Fix: guard rail in `Mover` — while a
    lift moves vertically the rider is clamped over the platform and pulled back if they step
    onto a passing ledge (the window trim at z=488). Jumping off is still possible (as in Quake).
- **Full bot sweep of all 40 SP maps (2026-09-19)**: all load, no exceptions, deaths only from
  lava/monsters (TESTING.md §2 has the details). Found on the way: lava damage was 5× weaker than
  Quake (now `10 × waterlevel` every 0.2 s), `Effects.Particles` set `duration` on an auto-playing
  system (warning spam, burst could be lost → now stopped first). **9 maps are one-room placeholder
  stubs in LibreQuake 0.09-beta itself**: `lq_e1m6`, `lq_e2m1`, `lq_e3m7`, `lq_e4m2`, `lq_e4m6`,
  `lq_e4m7`, `lq_e4m8`, `lq_e0m9`, `lq_end` — spawn room + exit slipgate, no monsters. This is what
  the owner sees as "levels with missing content"; it can only be fixed upstream (or by pulling a
  newer LibreQuake release into the import pipeline).
- Lesson: never clear a static registry in `Awake` of a scene object — other objects' `Awake`
  order is undefined; prune instead.
- Lesson for future work: **anything loaded with `Shader.Find`/`Resources.Load` must live under
  a `Resources/` folder or be referenced by a serialized asset**, otherwise it is stripped.

## 2. Repository map

| Path | What it is |
| --- | --- |
| `Assets/LQ/Scripts/` | Runtime C# (assembly `LQ.Runtime`, namespace `LQ`). Sub-folders: `Core` (palette, MDL/SPR loaders, combat), `Player`, `Weapons`, `Monsters`, `Entities` (doors, plats, triggers, items, liquids), `Game` (GameManager, LevelSetup, DemoRunner), `UI` (HUD, TouchControls, MainMenu). |
| `Assets/LQ/Editor/LQBuildPipeline.cs` | The whole import/build pipeline (namespace `LQ.EditorTools`). Menu `LibreQuake/…` and static entry points for `-executeMethod`. |
| `Assets/LQ/Resources/Shaders/` | Quake-style world, model, liquid, sky, sprite, particle shaders (in Resources so they are never stripped). |
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

### Local compile check (no Cloud Build minutes)
The Unity Editor can compile the scripts headless (player builds also work once §9 is set up): `Unity -batchmode -nographics -username … -password … -projectPath
<project> -logFile log.txt -quit`. Success = exit code 0 and "Exiting batchmode successfully";
`grep "error CS" log.txt` lists compile errors. If the log says "Invalid ILPostProcessor
configuration … Scripts have compiler errors" with zero `CS` errors, stale
`Unity.ILPP.Runner`/`Unity` processes from a previous run are the cause — kill them, delete
`/tmp/ilpp.sock-*` and `Temp/UnityLockfile`, rerun. Always run this before pushing.

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
  In the GPU-less gVisor sandbox this only works with the qemu shader-compiler wrapper and
  `LQ_KEEP_AUDIO_DISABLED=1` — see §9 (validated 2026-09-19).

## 6. What to do next (priority order)

0. **Device-test v0.1.7** (= v0.1.6 fixes + brighter dark areas). Rendered sweep of 14 maps on
   2026-09-19 (start, e1m1-3, e2m2-4, e3m1-3, e4m1,3,4): 0 deaths after the mover fix; visuals OK.
0z. **v0.1.6 notes** — it fixes the two bugs the owner reported most: (a) every mover with a
   move sound (`sounds` 1–4 doors, lifts, trains) teleported to its bounds centre the moment it
   started moving, because `SoundBank.Loop` attached the AudioSource to the mover itself and the
   code then set `moveSrc.transform.position` (e3m1 start lift slid 44 m → player fell into the
   pit); (b) `trigger_teleport` did the same with its hum → portals stopped teleporting. Both were
   invisible to the headless bot because Editor audio was disabled — **now that audio works in the
   sandbox (§9.3) every bot run exercises the audio code paths too; keep it that way.**
0a. **Device-test v0.1.5** (first local sandbox build, lighting restored) and collect concrete bug
   reports (map name + what happened). Builds are now local (§9, ~2 min incremental) — no need
   to wait for Cloud Build minutes. The full 40-map bot sweep already passed (TESTING.md §2); re-run the smoke
   set before every push.
0b. **Placeholder maps**: check newer LibreQuake releases (https://github.com/lavenderdotpet/LibreQuake)
   for finished versions of the 9 stub maps listed in §1 and re-import them (`MapSources/`).
1. **Verify build #6 (v0.1.3) on a device**: EPISODE gates in `start` open only with runes,
   traps fire, fireballs/bubbles/lightning appear, skill filtering matches the original game.
   If something is invisible, check the Cloud Build log for "Serialized binary data for shader"
   lines — every `LQ/*` shader must appear there.
2. **Play-test and collect device bugs** (owner reports after v0.1.3: "many levels still have
   problems", Episode 3 death fixed above; the touch-button complaint is still unspecified — ask
   for a screen recording). Reproduce every report with the bot first (TESTING.md). Use `adb logcat -s Unity` on the device; every runtime problem logs with the
   `[LQ]`/`MdlLoader`/`Monster` prefixes. Fix, push to `main`, press Build on the `Android`
   target (≈33 min, free tier ≈ 200 min/month — check remaining minutes first).
3. If the full import ever exceeds the budget, build episode by episode
   (`LQ_MAPS=start,lq_e1m1,…,lq_e1m8`).
4. Known code TODOs: `Monster.NoiseAt` should ignore monsters with `AmbushMarker`; the
   skill multiplier in `GameManager` is not applied yet; no save/load between sessions;
   music (`--music` staging option) is disabled by default to keep the APK small.
5. Gameplay video: rendered screenshots now work in the sandbox (§9.2 / TESTING.md §2b); a full video still needs a device or a Linux player: run a Linux/Windows player with `-lqdemo -lqdemo-map lq_e1m1 -lqdemo-frames DIR`
   (`DemoRunner` dumps `f%05d.png` at 30 fps) and encode with ffmpeg.

## 7. Working conventions

- Git: the sandbox filesystem is slow — run long git ops in the background and never two at
  once (`index.lock`). Pushing needs the authenticated GitHub helper, plain `git push` has no credentials.
- Cloud Build minutes are scarce (free tier 200/month, ≈190 used after build #7 — no more builds until the monthly reset): batch several
  fixes per build.
- Commit source only; never commit `Library/`, `Builds/`, `Assets/LQ/Generated`, `Assets/LQ/Scenes`, APKs.
- Keep `README.md` (Arabic + English) in sync with build instructions.
- Licences: code MIT (`LICENSE`); LibreQuake assets under their own licence
  (`LICENSE-LibreQuake-assets.txt`). Do not add proprietary Quake data.
- Language: the repository owner communicates in Arabic; code, comments and this file are in English.

## 9. Sandbox recipe — rendering AND local APK builds without GPU/root (2026-09-19)

The dev sandbox (gVisor kernel `4.19.0-gvisor`, 17 cores, no GPU, no root) used to fail
every player build and every rendered run with **"Shader compiler initialization error
0x80000004"**. Root cause: `UnityShaderCompiler` crashes in `PESetupFS()` →
`D3DCompilerWrapper::Initialize` because gVisor does not honour `arch_prctl(ARCH_SET_FS/GS)`
the way the Windows-PE loader inside the compiler expects. Same class of bug the `my-gpu`
repo hit with Wine. Fix = run only that one binary under **qemu user-mode (TCG)**; the Editor
itself runs natively.

### 9.1 One-time setup (≈2 min, no root)

Ready-made copies of every file below live in `tools/sandbox/` (`schedfix.c`, `run_unity.sh`,
`UnityShaderCompiler.wrapper.sh`).

```bash
# 1. qemu-user-static without root: download the Debian package and unpack it
mkdir -p /work/qemu/sysroot && cd /work/qemu
apt-get download qemu-user-static            # Debian 7.2.x is fine
dpkg -x qemu-user-static_*.deb sysroot       # → sysroot/usr/bin/qemu-x86_64-static

# 2. wrap the shader compiler (Editor = /work/unity/editor)
cd /work/unity/editor/Editor/Data/Tools
mv UnityShaderCompiler UnityShaderCompiler.real
cat > UnityShaderCompiler <<'SH'
#!/bin/sh
# gVisor sandbox: native compiler crashes in PESetupFS (arch_prctl FS/GS). Run it under qemu user-mode.
exec /work/qemu/sysroot/usr/bin/qemu-x86_64-static "$(dirname "$0")/UnityShaderCompiler.real" "$@"
SH
chmod +x UnityShaderCompiler

# 3. Editor launcher used everywhere below
cat > /work/unity/run_unity.sh <<'SH'
#!/bin/sh
export LD_LIBRARY_PATH=/work/unity/libs/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH
export HOME=${HOME:-/work/unity/home}
export LD_PRELOAD=/work/unity/shim/libschedfix.so${LD_PRELOAD:+:$LD_PRELOAD}   # FMOD fix, see §9.3
exec /work/unity/editor/Editor/Unity "$@"
SH
# 4. FMOD shim (source also in tools/sandbox/schedfix.c)
mkdir -p /work/unity/shim && cp tools/sandbox/schedfix.c /work/unity/shim/ && gcc -shared -fPIC -O2 -o /work/unity/shim/libschedfix.so /work/unity/shim/schedfix.c
chmod +x /work/unity/run_unity.sh
```

Verify: `xvfb-run -a -s "-screen 0 1280x720x24" ./run_unity.sh -batchmode -force-glcore
-projectPath /work/lqunity -quit -logFile log_gfx.txt` → log shows `OpenGL 4.5 … llvmpipe`,
no `Shader compiler initialization error`, exit 0. Shader compilation is ~5–10× slower under
TCG but the whole project compiles.

### 9.2 Rendered playtest (screenshots)

See TESTING.md §2b. Needs Xvfb + Mesa llvmpipe (`swrast_dri.so`) and
`LP_NUM_THREADS=<cores>` (llvmpipe hangs at 720p without it — credit: `my-gpu/docs/GAMING.md`).
Use `-force-glcore`, never `-nographics`. `PlaytestBot` writes PNGs when `LQ_BOT_SHOTS` is set.

### 9.3 Local Android APK build

Android SDK/NDK/OpenJDK ship inside the Editor
(`Editor/Data/PlaybackEngines/AndroidPlayer/`, 5.2 GB) — nothing else to install.

```bash
cd /work/unity && rm -f build.exit /work/lqunity/Temp/UnityLockfile
LQ_REIMPORT_SOUNDS=1 ./run_unity.sh -batchmode -nographics   # LQ_REIMPORT_SOUNDS only needed once -username "$U" -password "$P" \
  -projectPath /work/lqunity -buildTarget Android \
  -executeMethod LQ.EditorTools.LQBuildPipeline.BuildAndroid -quit -logFile /work/unity/log_build.txt
grep "BUILD RESULT\|error CS\|Fatal" /work/unity/log_build.txt   # APK → /work/lqunity/Builds/LibreQuake.apk
```

* `LQ_KEEP_AUDIO_DISABLED=1` is only a fallback for sandboxes *without* the `libschedfix.so`
  preload (see below): it leaves `m_DisableAudio: 1` so `EnsureAudioEnabled()` cannot abort the
  Editor, and the APK is then made audible with `tools/apk_enable_audio.py` (§9.4).
* **Audio / FMOD (`libschedfix.so`).** Root cause found 2026-09-19: gVisor returns 0 for
  `sched_get_priority_min/max(SCHED_FIFO)`, so glibc's `pthread_attr_setschedparam()` fails with
  EINVAL and FMOD aborts — Editor: "Unable to initialize any audio device (even nosound)",
  FSBTool: "Internal error from FMOD sub-system" → 0 sounds in the APK. Fix: a 10-line
  `LD_PRELOAD` shim that turns realtime-priority requests into no-ops (source in TESTING.md §2c,
  built with `gcc -shared -fPIC -O2 -o libschedfix.so schedfix.c`). `run_unity.sh` exports
  `LD_PRELOAD=/work/unity/shim/libschedfix.so`. With it the Editor imports/plays audio normally,
  so `LQ_KEEP_AUDIO_DISABLED` and `tools/apk_enable_audio.py` are no longer needed (kept as fallback).
  After installing the shim run once with `LQ_REIMPORT_SOUNDS=1` (clips cached as "failed" are
  not re-imported otherwise). Check: `unzip -l LibreQuake.apk | grep -c '\.resource$'` → 228.
* IL2CPP compiles ~1000 C++ objects per ABI; a full build took **10 min 40 s** on 17 cores
  (Build local #2, 2026-09-19).
* **Material repair.** Materials generated while the shaders could not compile point at the
  built-in `Unlit/Texture` (fileID 10752) → fullbright levels, static water/sky. This is what
  shipped in v0.1.0–v0.1.4. `LoadMaterials()` now repairs them automatically whenever
  `LQ/World` exists (also from `BuildAndroid`); menu *LibreQuake → Repair materials* does it by hand.
  Verify: `grep -l "fileID: 10752" Assets/LQ/Materials/*.mat | wc -l` → 0.
* Run one Unity instance at a time; stale `Unity.ILPP.Runner`/`UnityShaderCompiler` processes
  → kill them and delete `/tmp/ilpp.sock-*` before retrying.

### 9.4 Re-enabling audio in a locally built APK (fallback only)

`tools/apk_enable_audio.py in.apk out.apk` patches `assets/bin/Data/globalgamemanagers`
(AudioManager `m_DisableAudio` → 0) with UnityPy, re-zips, then `zipalign` + `apksigner`
(build-tools from the bundled SDK, debug keystore generated with the bundled `keytool`).
If the build was made on Cloud Build the APK already has audio — skip this.
