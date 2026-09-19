# LibreQuake — Unity Android port

<p dir="rtl">

## بالعربية

إعادة بناء لعبة **LibreQuake** (المحتوى الحر البديل لـ Quake) كلعبة أندرويد كاملة على محرك **Unity 2022.3 LTS**.

**ما يحتويه المستودع:**

- `Assets/LQ/Scripts` — كود اللعبة (C#): اللاعب، الأسلحة، الوحوش والذكاء الاصطناعي، الكيانات (الأبواب، المصاعد، المفاتيح، الأزرار، الأسرار، الانتقال بين المستويات)، الواجهة (HUD)، القائمة الرئيسية، وأزرار اللمس للأندرويد.
- `Assets/LQ/Resources/Shaders` — شيدرات عالم Quake، النماذج، السوائل، السماء، الجزيئات.
- `Assets/LQ/Textures` و `Assets/LQ/Resources` — موارد LibreQuake الكاملة (الخامات، نماذج `.mdl`، الأصوات، رسوميات الواجهة، لوحة الألوان).
- `MapSources/*.map` — مصادر كل خرائط LibreQuake (صيغة TrenchBroom) — تُستورد آلياً إلى مشاهد Unity.
- `Assets/LQ/Editor/LQBuildPipeline.cs` — خط الإنتاج: استيراد الخامات → النماذج → الخرائط → بناء APK.
- `ci/build-android.yml` — (اختياري) بناء APK على GitHub Actions. البناء الحالي يتم عبر **Unity Cloud Build**.
- `AGENTS.md` — دليل للمطورين والوكلاء الذكيين لفهم المشروع ومواصلته.
- `TESTING.md` — كيفية اختبار اللعبة بدون هاتف: فحص الترجمة، بوت اللعب الآلي بدون شاشة (`PlaytestBot`)، وقائمة فحص الجهاز.
- `tools/` — سكربتات Python لتحضير الموارد من مستودع LibreQuake الأصلي.

**أزرار اللمس (أندرويد):** عصا تحكم يسارية للحركة، سحب على يمين الشاشة للنظر، أزرار FIRE و JUMP، تبديل السلاح `<` `>`، وزر إيقاف. الأزرار تتكيّف مع حجم الشاشة ومنطقة الأمان (Safe Area).

**كيف تبني APK (Unity Cloud Build — الطريقة المعتمدة):**

1. المشروع مرتبط بـ Unity Cloud Build عبر SSH (مفتاح Deploy Key للقراءة فقط على هذا المستودع).
2. هدف البناء `Android` مضبوط مسبقاً: Unity 2022.3.62f3، Pre-Export `LQ.EditorTools.LQBuildPipeline.CloudPreExport`، متغير البيئة `LQ_MAPS` يحدد الخرائط (فارغ = كل الخرائط).
3. من **Build history → Build**. أول APK (e1m1) استغرق 25 دقيقة. حمّل `Android.apk` من قائمة التنزيل.

التفاصيل الكاملة وخطوات المتابعة في `AGENTS.md`.

**البناء محلياً (Unity Editor 2022.3.62f3 + Android module):** افتح المشروع، ثم من القائمة `LibreQuake` نفّذ الخطوات 1 → 2 → 3 ثم `Build Android APK`، أو من الطرفية:

```
Unity -batchmode -nographics -quit -projectPath . -buildTarget Android -executeMethod LQ.EditorTools.LQBuildPipeline.BuildAll
```

</p>

---

## English

A full rebuild of **LibreQuake** (the libre Quake content replacement) as a native **Android** game on **Unity 2022.3 LTS**.

### What's in the repo

| Path | Contents |
| --- | --- |
| `Assets/LQ/Scripts` | Game code (C#): player controller, weapons, monsters & AI, entities (doors, plats, buttons, triggers, secrets, level changes), HUD, main menu, Android touch controls |
| `Assets/LQ/Resources/Shaders` | Quake-style world / model / liquid / sky / sprite / particle shaders |
| `Assets/LQ/Textures`, `Assets/LQ/Resources` | Complete LibreQuake assets: textures, `.mdl` models, sounds, HUD graphics, palette |
| `MapSources/*.map` | Every LibreQuake map source (TrenchBroom / Valve 220) — imported into Unity scenes automatically |
| `Assets/LQ/Editor/LQBuildPipeline.cs` | Build pipeline: import textures → brush models → maps → build APK |
| `ci/build-android.yml` | Optional GitHub Actions workflow. Current builds run on **Unity Cloud Build** |
| `AGENTS.md` | Guide for developers / AI agents: architecture, pipeline, Cloud Build setup, next steps |
| `TESTING.md` | How to test without a phone: compile check, headless playtest bot (`PlaytestBot`), device checklist |
| `tools/` | Python scripts that stage assets from the upstream LibreQuake repo |

### Touch controls

Left virtual joystick (move), drag on the right half (look), **FIRE**, **JUMP**, weapon `<` / `>`, pause. Layout scales with screen size and respects the Android safe area. Bluetooth/USB gamepads and keyboard also work.

### Building the APK

**Unity Cloud Build (current, working)**

1. The Unity Cloud project pulls this repo over SSH (read-only deploy key).
2. Build target `Android`: Unity 2022.3.62f3, Pre-Export method `LQ.EditorTools.LQBuildPipeline.CloudPreExport`, env var `LQ_MAPS` selects the maps (empty = all).
3. **Build history → Build**; download `Android.apk` when it finishes (e1m1-only build: ~25 min).

See `AGENTS.md` for the full setup, the REST endpoint and what to do next.

**GitHub Actions (optional, untested)**: copy `ci/build-android.yml` to `.github/workflows/`, add secrets `UNITY_EMAIL` / `UNITY_PASSWORD`, run *Build Android APK*.

**Locally** (Unity 2022.3.62f3 with the Android module): open the project and use the `LibreQuake` menu steps 1 → 2 → 3 → *Build Android APK*, or:

```
Unity -batchmode -nographics -quit -projectPath . -buildTarget Android -executeMethod LQ.EditorTools.LQBuildPipeline.BuildAll
```

Set `LQ_MAPS=lq_e1m1,start` and use `BuildSelected` to build a subset.

### Re-staging assets from upstream

```
git clone https://github.com/lavenderdotpet/LibreQuake /tmp/LibreQuake
python3 tools/stage_assets.py /tmp/LibreQuake . [--music]
```

### Technical notes

- Coordinates: Unity `(x, y, z) = Quake (x, z, y) / 32`.
- `.map` geometry is built with [Scopa](https://github.com/radiatoryang/scopa) (pinned commit in `Packages/manifest.json`); the pipeline pre-processes brushes (skip/hint removal, liquids → `func_water`, sky → separate entity) and converts entities to `QEntity` components.
- `.mdl` models are parsed at runtime (`MdlLoader`) from `Resources/progs/*.mdl.bytes` using the Quake palette; animations are driven by `MdlAnimator` and `Resources/anims.json`.
- Static lighting is approximated from the map's `light` entities and baked into vertex colours.
- Build: IL2CPP, ARM64 + ARMv7, minSdk 23, package `com.ayoub.librequake`.

### Licence

Game code in this repository: MIT. LibreQuake assets are redistributed under their own licence — see `LICENSE-LibreQuake-assets.txt`.
