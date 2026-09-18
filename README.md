# LibreQuake — Unity Android port

<p dir="rtl">

## بالعربية

إعادة بناء لعبة **LibreQuake** (المحتوى الحر البديل لـ Quake) كلعبة أندرويد كاملة على محرك **Unity 2022.3 LTS**.

**ما يحتويه المستودع:**

- `Assets/LQ/Scripts` — كود اللعبة (C#): اللاعب، الأسلحة، الوحوش والذكاء الاصطناعي، الكيانات (الأبواب، المصاعد، المفاتيح، الأزرار، الأسرار، الانتقال بين المستويات)، الواجهة (HUD)، القائمة الرئيسية، وأزرار اللمس للأندرويد.
- `Assets/LQ/Shaders` — شيدرات عالم Quake، النماذج، السوائل، السماء، الجزيئات.
- `Assets/LQ/Textures` و `Assets/LQ/Resources` — موارد LibreQuake الكاملة (الخامات، نماذج `.mdl`، الأصوات، رسوميات الواجهة، لوحة الألوان).
- `MapSources/*.map` — مصادر كل خرائط LibreQuake (صيغة TrenchBroom) — تُستورد آلياً إلى مشاهد Unity.
- `Assets/LQ/Editor/LQBuildPipeline.cs` — خط الإنتاج: استيراد الخامات → النماذج → الخرائط → بناء APK.
- `.github/workflows/build-android.yml` — بناء APK آلي على GitHub Actions.
- `tools/` — سكربتات Python لتحضير الموارد من مستودع LibreQuake الأصلي.

**أزرار اللمس (أندرويد):** عصا تحكم يسارية للحركة، سحب على يمين الشاشة للنظر، أزرار FIRE و JUMP، تبديل السلاح `<` `>`، وزر إيقاف. الأزرار تتكيّف مع حجم الشاشة ومنطقة الأمان (Safe Area).

**كيف تبني APK:**

0. (مرة واحدة) انسخ الملف `ci/build-android.yml` إلى `.github/workflows/build-android.yml` عبر واجهة GitHub (Add file → Create new file).
1. أضف سرّين في إعدادات المستودع (Settings → Secrets → Actions): `UNITY_EMAIL` و `UNITY_PASSWORD` (حساب Unity شخصي).
2. افتح تبويب **Actions → Build Android APK → Run workflow**. اترك حقل `maps` فارغاً لبناء كل الخرائط، أو اكتب مثلاً `lq_e1m1,start` لبناء سريع.
3. بعد انتهاء العمل ستجد `LibreQuake.apk` في **Artifacts** وفي صفحة **Releases**.

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
| `Assets/LQ/Shaders` | Quake-style world / model / liquid / sky / sprite / particle shaders |
| `Assets/LQ/Textures`, `Assets/LQ/Resources` | Complete LibreQuake assets: textures, `.mdl` models, sounds, HUD graphics, palette |
| `MapSources/*.map` | Every LibreQuake map source (TrenchBroom / Valve 220) — imported into Unity scenes automatically |
| `Assets/LQ/Editor/LQBuildPipeline.cs` | Build pipeline: import textures → brush models → maps → build APK |
| `.github/workflows/build-android.yml` | CI: builds the APK on GitHub Actions and publishes it as an artifact + release |
| `tools/` | Python scripts that stage assets from the upstream LibreQuake repo |

### Touch controls

Left virtual joystick (move), drag on the right half (look), **FIRE**, **JUMP**, weapon `<` / `>`, pause. Layout scales with screen size and respects the Android safe area. Bluetooth/USB gamepads and keyboard also work.

### Building the APK

**GitHub Actions (recommended)**

0. (once) copy `ci/build-android.yml` to `.github/workflows/build-android.yml` (GitHub web UI → Add file → Create new file — the file lives in `ci/` because the bot account that pushed this repo is not allowed to create workflow files).
1. Add repository secrets `UNITY_EMAIL` and `UNITY_PASSWORD` (a Unity Personal account).
2. **Actions → Build Android APK → Run workflow.** Leave `maps` empty for all maps, or e.g. `lq_e1m1,start` for a quick build.
3. Download `LibreQuake.apk` from the run's **Artifacts** or the **Releases** page.

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
