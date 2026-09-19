# TESTING.md — how to test the game without a phone

Three layers, cheapest first. Run them in this order before every push / Cloud Build.

| Layer | Needs | Time | Catches |
|---|---|---|---|
| 1. Compile check | Unity Editor, headless | ~1 min | C# errors, missing scripts |
| 2. Headless playtest bot | Unity Editor, headless (no GPU) | ~1.5 min per map | falls, instant deaths, missing colliders, broken lifts/doors/triggers, NullReferences at spawn |
| 2b. Rendered playtest (Xvfb+llvmpipe) | Unity Editor + Xvfb + qemu wrapper (AGENTS.md §9) | ~4 min per map | missing textures, fullbright, invisible models, texture scale |
| 3. Device test | Android phone + APK from Cloud Build / GitHub Release | 30–40 min build | rendering, touch controls, performance |

All commands assume Unity 2022.3.62f3 at `$UNITY` and the project checked out at `$PROJ`.
The Editor needs a licence: pass `-username … -password …` (Personal licence is enough).

---

## 1. Compile check

```bash
$UNITY -batchmode -nographics -username "$U" -password "$P" -projectPath "$PROJ" -logFile log.txt -quit
grep "error CS" log.txt          # must print nothing
grep "Exiting batchmode successfully" log.txt
```

Also grep `Script attached to .* is missing` — it means a MonoBehaviour lives in a file whose
name differs from the class name (see AGENTS.md → "Unity rule that bit us once").

If the log says "Invalid ILPostProcessor configuration" with zero `CS` errors: stale
`Unity`, `Unity.ILPP.Runner`, `UnityAutoQuitter`, `UnityPackageMan` processes from a previous
run are alive. Kill them, delete `/tmp/ilpp.sock-*` and `$PROJ/Temp/UnityLockfile`, rerun.

## 2. Headless playtest bot (`PlaytestBot`)

The bot loads a map in Editor **play mode** without a screen, spawns the real player, and
logs where the player is every 0.5 s, when they die, and why. Everything runs through the
normal `GameManager.LoadLevel` → `LevelSetup` → `Player` path, so the physics/entity logic
tested is exactly what ships in the APK. Rendering is not tested (no GPU).

```bash
LQ_BOT=1 LQ_BOT_MAP=lq_e3m1 LQ_BOT_SECONDS=40 LQ_BOT_SCRIPT=walk \
$UNITY -batchmode -nographics -username "$U" -password "$P" -projectPath "$PROJ" \
  -executeMethod LQ.EditorTools.LQBuildPipeline.BotPlay -logFile log_bot.txt
grep "\[Bot\]" log_bot.txt
```

Do **not** pass `-quit` — the bot exits the Editor itself (`EditorApplication.Exit`, exit
code 0 = finished, 2 = no GameManager). `BotPlay` imports the map scene if it does not exist
yet (`LQ_BOT_REIMPORT=1` forces a re-import after changing the import pipeline), rebuilds the
menu scene and enters play mode.

Environment variables:

| Var | Default | Meaning |
|---|---|---|
| `LQ_BOT=1` | — | required, enables the bot |
| `LQ_BOT_MAPS` | — | comma list, e.g. `lq_e1m1,lq_e2m1,lq_e3m1` — plays every map in turn in ONE Editor session (imports missing scenes first) and prints a `SUMMARY` at the end. Preferred: ~1 min startup instead of 1 min per map. |
| `LQ_BOT_MAP` | `lq_e1m1` | single map (used when `LQ_BOT_MAPS` is empty) |
| `LQ_BOT_SECONDS` | 30 | how long to play |
| `LQ_BOT_SCRIPT` | `idle` | `idle` = stand still (tests spawn, lifts, triggers); `walk` = walk forward, turn, jump (tests edges, doors, falls) |
| `LQ_BOT_DT` | — | fixed frame time, e.g. `0.12` (8 fps) or `0.33` (3 fps) simulates a slow phone. Mover/carry bugs only show up here. |
| `LQ_BOT_PROBE=1` | — | with every log line also raycast forward/back/left/right/up/down and print what wall is hit (finds missing colliders) |

Log lines:

```
[Bot] boot map=lq_e3m1 script=walk seconds=40 captureDt=0
[Bot] level loaded: lq_e3m1
[Bot] probe (0,0,1):2.8m worldspawn | (0,0,-1):2.0m func_door | …      ← LQ_BOT_PROBE or once per scene
[Bot] t=0.5 q=(0.0,508.5,-1335.4) u=(0.0,15.9,-41.7) hp=100 grounded=True vel=(0.0,-1.0,10.0) floor=0.13m Collider00000 water=0
[Bot] DIED #1 at (…) scene=… lastPos=… minY=… NO FLOOR below | trigger:trigger_hurt#12
[Bot] MAP lq_e3m1 done deaths=0 finalScene=lq_e3m1 minY=0.03     ← one per map
[Bot] MAP lq_e9m9 FAILED to load                                  ← scene missing / LoadLevel error
[Bot] restart ok after 1.2s | restart FAILED                      ← after a death; FAILED = RestartLevel bug
[Bot] SUMMARY
lq_e1m1: deaths=0 finalScene=lq_e1m1
lq_e3m1: deaths=1 first: (…) NO FLOOR below | trigger:trigger_hurt#12 finalScene=lq_e3m1
[Bot] done deaths=1 finalScene=lq_e3m1 minY=0.03
```

After 2 deaths on the same map the bot moves on to the next map.

`q` = Quake units (x, z-up, y) exactly as in the `.map` source, so you can look the position
up in TrenchBroom; `u` = Unity metres; `floor` = distance to the ground under the player
and the collider hit. On death the diagnosis lists `NO FLOOR below` (fell out of the world)
and any trigger volumes within 1 m (`trigger_hurt` = kill pit / slime).

What to run before a build (the "smoke set"):

```bash
for s in idle walk; do
  LQ_BOT=1 LQ_BOT_MAPS=start,lq_e1m1,lq_e2m1,lq_e3m1,lq_e4m1 LQ_BOT_SCRIPT=$s LQ_BOT_SECONDS=20 \
    $UNITY … -executeMethod LQ.EditorTools.LQBuildPipeline.BotPlay -logFile log_smoke_${s}.txt
done
grep -H -A50 "SUMMARY" log_smoke_*.txt; grep -H "DIED\|FAILED\|NullReference\|MissingComponent" log_smoke_*.txt
```

Full sweep (all 40 SP maps, ~25 min incl. first import): pass the whole `MapList()` in
`LQ_BOT_MAPS` (`grep -o '"lq_[a-z0-9_]*"' Assets/LQ/Editor/LQBuildPipeline.cs`).

Any `DIED` on `idle` is a bug (spawn falls / lift bugs). A `DIED` on `walk` needs a look at
the position: walking into slime/lava is legitimate, falling through a lift or a wall is not.
`MissingComponentException` / `NullReferenceException` during `LevelSetup.Start` are always
bugs — they abort entity setup and the player never spawns (`[Bot] t=… no player`).

Bugs found with the bot so far (all fixed, see git log):

- `GetComponent<T>() ?? AddComponent<T>()` never adds the component in the Editor (fake-null
  object) → `MissingComponentException` in `Mover.Awake`/`MdlAnimator`, no player spawned.
  Use `gameObject.GetOrAdd<T>()` (`Core/QuakeUnits.cs`).
- `Mover.CarryPlayer` lost the rider at low fps (a 1.5 m/s lift moves 0.5 m per frame at
  3 fps; the cast reached 0.35 m). Cast now reaches `0.35 + |delta|`.
- e3m1 start lift: the front of the platform is open; walking forward drops 17 m into the
  slime `trigger_hurt` → instant death right after entering Episode 3. Riders are now kept over
  the platform while a lift moves vertically (guard rail in `Mover.CarryPlayer`).

Full sweep, 2026-09-19 (`LQ_BOT_SCRIPT=walk`, 12 s per map, all 40 SP maps + `start`): every map
loads and spawns a player; 3 deaths, all legitimate (lava in `lq_e0m8` and `lq_e1m7`, knights in
`lq_e2m2`). No `MissingComponent`/`NullReference`. Things that look like bugs but are not:

- `finalScene` differs from the map name for `lq_e1m6`, `lq_e0m9`, `lq_e3m7`, `lq_e4m2`, `lq_e4m6`,
  `lq_e4m7`, `lq_e4m8` (and `lq_e2m1`, `lq_end`): these maps are **placeholder stubs in LibreQuake
  0.09-beta** — one room, ~18 entities, exit slipgate 14 m in front of the spawn (see the
  `.map` sizes: 50–65 KB vs 0.5–8 MB for real maps). The walk script simply walks into the exit.
  Nothing to fix on our side; upstream LibreQuake has to finish those maps.
- `SoundBank: missing …` for every clip: the headless Editor has audio disabled, so
  `Resources.Load<AudioClip>` returns null. The clips are in the APK (Cloud Build log lists all
  228 `.wav`). The warning is now suppressed in batchmode.

GPU-less sandboxes (no working `UnityShaderCompiler`): the first import of a map whose materials
were never hashed crashes the Editor with `Fatal Error! Shader compiler initialization error` inside
`Material.GetTexture` (Scopa reads `material.mainTexture` for the texture size). Workaround used in
the sandbox, NOT committed: embed Scopa as a local package (`Packages/com.radiatoryang.scopa`, copy of
`Library/PackageCache/com.radiatoryang.scopa@1a31fb8df9`, delete its `Runtime/Ica_Normal_Tools/IcaUtils/Tests`
folder, point `Packages/manifest.json` at `file:com.radiatoryang.scopa`) and in `ScopaMesh.cs` replace the
`matOverride.material.mainTexture` reads with a helper that, under `#if UNITY_EDITOR`, reads the
`_MainTex` reference through `SerializedObject(mat).FindProperty("m_SavedProperties.m_TexEnvs")`.
On a normal machine (Cloud Build, a dev PC) none of this is needed.

Extending the bot: `Assets/LQ/Scripts/Game/PlaytestBot.cs` — add a script name in
`Start()` (e.g. `shoot`, `usekeys`) and a coroutine that writes `GameInput.botMove` /
`GameInput.botLook` / `GameInput.touchFire` / `GameInput.touchJump`. Never write
`GameInput.touchMove` from a bot: `TouchControls` zeroes it every frame when hidden.

## 2b. Rendered playtest under Xvfb + llvmpipe (screenshots, no GPU)

The headless bot (§2) proves physics/triggers but sees nothing. A rendered run gives PNG
screenshots from the player camera — enough to spot missing textures, fullbright rooms,
invisible monsters, wrong texture scale. It works in the GPU-less gVisor sandbox
(17 cores, no root) thanks to two tricks borrowed from the `my-gpu` repo; see
AGENTS.md §9 for the one-time setup (`qemu` wrapper for `UnityShaderCompiler`).

```bash
cd /work/unity && rm -f bot.exit /work/lqunity/Temp/UnityLockfile
LP_NUM_THREADS=17 LQ_BOT=1 LQ_BOT_MAPS=lq_e1m1 LQ_BOT_SECONDS=20 LQ_BOT_SCRIPT=walk \
LQ_BOT_SHOTS=3 LQ_BOT_SHOT_DIR=/work/unity/shots \
xvfb-run -a -s "-screen 0 1280x720x24" ./run_unity.sh -batchmode -force-glcore \
  -username "$U" -password "$P" -projectPath /work/lqunity \
  -executeMethod LQ.EditorTools.LQBuildPipeline.BotPlay -logFile /work/unity/log_botN.txt
grep "\[Bot\] SHOT" /work/unity/log_botN.txt     # one line per PNG written
```

* `-force-glcore` (NOT `-nographics`) so a real GL 4.5 llvmpipe device is created.
* `LP_NUM_THREADS=<cores>` is mandatory — llvmpipe hangs at 720p without it.
* `LQ_BOT_SHOTS=<sec>` = screenshot interval, `LQ_BOT_SHOT_DIR` = output dir;
  files are `{map}_{t:000.0}s.png` (1280×720, camera only — the UI overlay is not captured).
* Run ONE Unity instance at a time. Expect ~3–4 min for a 20 s playtest of one map.
* Known visual findings so far (2026-09-19, e1m1): textures + shotgun render, but rooms are
  fullbright (no lightmaps yet) and some faces show an oversized texture scale.

## 2c. Sandbox tool wrappers (why builds worked only after these)

Two Unity helper binaries misbehave under gVisor; both are fixed with shell wrappers next to the
binary (rename original to `*.real`). Details and rationale: AGENTS.md §9.

```sh
# Editor/Data/Tools/FSBTool/FSBTool  (chmod +x)
#!/bin/sh
out=""; prev=""
for a in "$@"; do [ "$prev" = "-o" ] && out="$a"; prev="$a"; done
msg=$("$(dirname "$0")/FSBTool.real" "$@" 2>&1); rc=$?
if [ $rc -ne 0 ] && [ -n "$out" ] && [ -s "$out" ] && [ "$(head -c 4 "$out" 2>/dev/null)" = "FSB5" ]; then
  echo "FSBTool: ignored FMOD init error (sandbox), output ok: $out" >&2; exit 0
fi
printf '%s\n' "$msg"; exit $rc
```

Verify a build: `grep -c "FSBTool ERROR" log.txt` → 0 and the APK contains 228 `.resource` files.

## 3. Device test

1. Build on Unity Cloud Build (AGENTS.md §4) or download the latest GitHub Release APK.
2. Install: `adb install -r LibreQuake-Android-full.apk`.
3. Watch the log while playing: `adb logcat -s Unity`. Everything gameplay-related logs with
   `[LQ]`, `MdlLoader`, `Monster`, `SoundBank`, `TriggerTeleport` prefixes; a
   `MissingComponent`/`NullReference` there is a bug to reproduce with the bot first.
4. Checklist per release: menu → EASY/NORMAL/HARD portals teleport; every EPISODE gate in
   `start` is open (no runes yet) — the gates only close once you own that rune; weapons +
   monsters visible; touch: left half = move stick, right half = look, FIRE/JUMP/WPN buttons;
   Episode 1–4 first maps load and the start lift/doors work; level exit changes level.

## Map source analysis (no Unity needed)

`tools/` and the `.map` sources under `MapSources/` let you answer "what is at position X"
without opening Unity: entity classnames, keys and brush bounding boxes. Player positions
from the bot log (`q=`) are in the same coordinate system.
