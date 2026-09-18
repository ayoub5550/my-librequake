#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scopa;
using Sledge.Formats.Map.Objects;
using Mesh = UnityEngine.Mesh;
using Path = System.IO.Path;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace LQ.EditorTools {
    /// <summary>Editor pipeline: textures -> materials, .map -> scenes (with baked vertex lighting + QEntity data), brushmodels -> prefabs, Android build.
    /// Run from the menu (LibreQuake/...) or in batch mode: -executeMethod LQ.EditorTools.LQBuildPipeline.BuildAll</summary>
    public static class LQBuildPipeline {
        const string TexDir = "Assets/LQ/Textures";
        const string MatDir = "Assets/LQ/Materials";
        const string SceneDir = "Assets/LQ/Scenes";
        const string GenDir = "Assets/LQ/Generated";
        const string BrushPrefabDir = "Assets/LQ/Resources/brushmodels";
        const string MapSourceDir = "MapSources";

        static Dictionary<string, Material> materialsByQuakeName;

        // ------------------------------------------------------------------ entry points
        [MenuItem("LibreQuake/1. Import Textures + Materials")]
        public static void ImportTextures() {
            EnsureLayers();
            EnsureFolders();
            var files = Directory.GetFiles(TexDir, "*.png");
            int i = 0;
            foreach (var f in files) {
                var path = f.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.maxTextureSize != 1024 || imp.mipmapEnabled == false)) {
                    imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 1024;
                    imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.isReadable = false; imp.npotScale = TextureImporterNPOTScale.None; imp.alphaIsTransparency = false;
                    imp.SaveAndReimport();
                }
                if (++i % 200 == 0) Debug.Log($"textures {i}/{files.Length}");
            }
            // HUD / resources textures: point filter, no compression
            foreach (var f in Directory.GetFiles("Assets/LQ/Resources/hud", "*.png")) {
                var imp = AssetImporter.GetAtPath(f.Replace('\\', '/')) as TextureImporter;
                if (imp != null && (imp.filterMode != FilterMode.Point || imp.mipmapEnabled)) { imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.alphaIsTransparency = true; imp.npotScale = TextureImporterNPOTScale.None; imp.SaveAndReimport(); }
            }
            AssetDatabase.SaveAssets();
            BuildMaterials();
            Debug.Log("ImportTextures done: " + files.Length);
        }

        [MenuItem("LibreQuake/2. Import Brush Models (items)")]
        public static void ImportBrushModels() {
            LoadMaterials();
            EnsureFolders();
            foreach (var map in Directory.GetFiles(MapSourceDir, "b_*.map")) {
                var name = Path.GetFileNameWithoutExtension(map);
                if (name.EndsWith("_lite")) continue;
                ImportBrushModel(map, name);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("LibreQuake/3. Import All Maps")]
        public static void ImportAllMaps() {
            LoadMaterials();
            var maps = MapList();
            int i = 0;
            foreach (var m in maps) { Debug.Log($"=== map {++i}/{maps.Count}: {m}"); ImportMap(m); }
            BuildMenuScene();
            UpdateBuildScenes();
        }

        [MenuItem("LibreQuake/Import E1M1 only (test)")]
        public static void ImportTestMap() { LoadMaterials(); ImportMap("lq_e1m1"); BuildMenuScene(); UpdateBuildScenes(); }

        [MenuItem("LibreQuake/4. Build Android APK")]
        public static void BuildAndroid() {
            ConfigurePlayerSettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/LibreQuake.apk", target = BuildTarget.Android, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"BUILD RESULT: {report.summary.result} size={report.summary.totalSize} errors={report.summary.totalErrors} time={report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) {
                foreach (var step in report.steps) foreach (var msg in step.messages) if (msg.type == LogType.Error || msg.type == LogType.Exception) Debug.LogError(msg.content);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Linux x86_64 player used on CI to record the demo video (run with -lqdemo).</summary>
        public static void BuildLinuxDemo() {
            EnsureAudioEnabled();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory("Builds/Linux");
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/Linux/LibreQuake.x86_64", target = BuildTarget.StandaloneLinux64, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"BUILD RESULT (Linux): {report.summary.result} errors={report.summary.totalErrors} time={report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) {
                foreach (var step in report.steps) foreach (var msg in step.messages) if (msg.type == LogType.Error || msg.type == LogType.Exception) Debug.LogError(msg.content);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Everything, for batch mode.</summary>
        public static void BuildAll() {
            try {
                ImportTextures();
                ImportBrushModels();
                ImportAllMaps();
                BuildAndroid();
            } catch (Exception e) {
                Debug.LogError("BuildAll failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        /// <summary>Import only the maps in LQ_MAPS, then build the APK (batch, quick CI test).</summary>
        public static void BuildSelected() {
            try { ImportSelected(); BuildAndroid(); }
            catch (Exception e) { Debug.LogError("BuildSelected failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }

        /// <summary>Import step only (batch).</summary>
        public static void ImportAll() {
            try { ImportTextures(); ImportBrushModels(); ImportAllMaps(); }
            catch (Exception e) { Debug.LogError("ImportAll failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }

        /// <summary>Import only the maps listed in the LQ_MAPS environment variable (comma separated), for quick tests.</summary>
        public static void ImportSelected() {
            try {
                ImportTextures(); ImportBrushModels(); LoadMaterials();
                var list = (Environment.GetEnvironmentVariable("LQ_MAPS") ?? "lq_e1m1").Split(',');
                foreach (var m in list) ImportMap(m.Trim());
                BuildMenuScene(); UpdateBuildScenes();
            } catch (Exception e) { Debug.LogError("ImportSelected failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }

        public static List<string> MapList() {
            var order = new List<string>();
            foreach (var m in Directory.GetFiles(MapSourceDir, "*.map").OrderBy(x => x)) {
                var n = Path.GetFileNameWithoutExtension(m);
                if (n.StartsWith("b_") || n == "dev" || n.StartsWith("lqdm") || n == "start_e0") continue;
                order.Add(n);
            }
            return order;
        }

        // ------------------------------------------------------------------ project setup
        static void EnsureFolders() {
            foreach (var d in new[] { MatDir, SceneDir, GenDir, BrushPrefabDir, "Assets/Resources", "Assets/Resources/Scopa" })
                if (!AssetDatabase.IsValidFolder(d)) { Directory.CreateDirectory(d); AssetDatabase.Refresh(); }
        }

        public static void EnsureLayers() {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var wanted = new Dictionary<int, string> { { 8, "Player" }, { 9, "Trigger" }, { 10, "Monster" } };
            foreach (var kv in wanted) {
                var sp = layers.GetArrayElementAtIndex(kv.Key);
                if (sp.stringValue != kv.Value) sp.stringValue = kv.Value;
            }
            tagManager.ApplyModifiedProperties();
            // Player layer must not collide with Trigger? (triggers must detect player) -> keep default matrix, but monsters shouldn't push the player around
            Physics.IgnoreLayerCollision(8, 10, false);
        }

        /// <summary>Make sure "Disable Unity Audio" is off in the shipped player (it is switched on only for headless import runs).</summary>
        static void EnsureAudioEnabled() {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (objs == null || objs.Length == 0) return;
            var so = new SerializedObject(objs[0]);
            var prop = so.FindProperty("m_DisableAudio");
            if (prop != null && prop.boolValue) { prop.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); Debug.Log("AudioManager: re-enabled Unity audio for the player build"); }
        }

        static void ConfigurePlayerSettings() {
            EnsureAudioEnabled();
            PlayerSettings.companyName = "Ayoub Teke";
            PlayerSettings.productName = "LibreQuake";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.ayoub.librequake");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3, UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.startInFullscreen = true; PlayerSettings.Android.renderOutsideSafeArea = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
            EditorUserBuildSettings.buildAppBundle = false;
            QualitySettings.vSyncCount = 0;
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ materials
        public static string QuakeToAssetName(string quakeTex) => quakeTex.ToLowerInvariant().Replace("*", "star_").Replace("+", "plus_");

        static void BuildMaterials() {
            materialsByQuakeName = new Dictionary<string, Material>();
            var fallback = Shader.Find("Unlit/Texture");
            var world = Shader.Find("LQ/World") ?? fallback; var liquid = Shader.Find("LQ/Liquid") ?? fallback; var sky = Shader.Find("LQ/Sky") ?? fallback;
            if (world == fallback) Debug.LogWarning("LQ shaders not found - using fallback Unlit/Texture (local headless mode)");
            var files = Directory.GetFiles(TexDir, "*.png");
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try {
                foreach (var f in files) {
                    var assetName = Path.GetFileNameWithoutExtension(f);
                    var matPath = $"{MatDir}/{assetName}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
                    if (tex == null) continue;
                    Shader sh = assetName.StartsWith("star_") ? liquid : assetName.StartsWith("sky") ? sky : world;
                    bool dirty = false;
                    if (mat == null) { mat = new Material(sh) { name = assetName }; AssetDatabase.CreateAsset(mat, matPath); n++; dirty = true; }
                    if (mat.shader != sh) { mat.shader = sh; dirty = true; }
                    if (mat.mainTexture != tex) { mat.mainTexture = tex; dirty = true; }
                    if (assetName.StartsWith("star_") && mat.HasProperty("_Alpha")) {
                        float a = assetName.Contains("lava") ? 0.9f : assetName.Contains("slime") ? 0.85f : assetName.Contains("tele") ? 1f : 0.65f;
                        if (Mathf.Abs(mat.GetFloat("_Alpha") - a) > 0.001f) { mat.SetFloat("_Alpha", a); dirty = true; }
                    }
                    if (dirty) EditorUtility.SetDirty(mat);
                }
            } finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log("materials created: " + n);
            LoadMaterials();
        }

        static void LoadMaterials() {
            materialsByQuakeName = new Dictionary<string, Material>();
            if (!AssetDatabase.IsValidFolder(MatDir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir })) {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null) continue;
                var quake = mat.name.Replace("star_", "*").Replace("plus_", "+");
                materialsByQuakeName[quake] = mat;
            }
            if (materialsByQuakeName.Count == 0) { BuildMaterials(); }
        }

        static Material FindMaterial(string quakeTex) {
            var key = quakeTex.ToLowerInvariant();
            if (materialsByQuakeName.TryGetValue(key, out var m)) return m;
            // liquids sometimes referenced without frame prefix variants; try stripping animation prefix (+0name -> +name)
            if (key.StartsWith("+") && key.Length > 2 && char.IsDigit(key[1])) { if (materialsByQuakeName.TryGetValue("+0" + key.Substring(2), out m)) return m; }
            return null;
        }

        // ------------------------------------------------------------------ map import
        static ScopaMapConfig MakeConfig(MapFile map) {
            var settings = ScopaProjectSettings.Get();
            settings.cullTextures = new List<string> { "trigger", "skip", "hint", "nodraw", "null", "clip", "origin" }; // keep sky faces (LQ/Sky shader)
            settings.mergeToWorld = new List<string> { "func_group", "func_detail" };
            settings.nonsolidEntities = new List<string>(); // Scopa NREs on nonsolid ents (null colliderResults); colliders stripped in PostProcessIllusionary
            settings.triggerEntities = new List<string> { "trigger", "func_water" };
            settings.staticEntities = new List<string> { "worldspawn", "func_wall", "func_water", "func_illusionary" };
            settings.addScopaEntityComponent = true;
            settings.defaultTexSize = 64;
            EditorUtility.SetDirty(settings);
            ScopaProjectSettings.Recache();

            var cfg = new ScopaMapConfig {
                scalingFactor = QuakeUnits.Scale, defaultSmoothingAngle = -1, removeHiddenFaces = true, addTangents = false, addLightmapUV2 = false,
                findMaterials = false, colliderMode = ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider,
                castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
            };
            // material overrides for every texture used in this map
            var used = new HashSet<string>();
            foreach (var solid in map.Worldspawn.FindAll().OfType<Solid>()) foreach (var f in solid.Faces) used.Add(f.TextureName);
            var overrides = new List<ScopaMapConfig.MaterialOverride>();
            int missing = 0;
            foreach (var t in used) {
                var mat = FindMaterial(t);
                if (mat == null) { missing++; continue; }
                overrides.Add(new ScopaMapConfig.MaterialOverride(t, mat));
            }
            if (missing > 0) Debug.LogWarning($"{missing} textures without material (default material used)");
            cfg.materialOverrides = overrides.ToArray();
            return cfg;
        }

        /// <summary>Pre-pass on the parsed map: liquids -> func_water entities, remove skip/hint-only brushes, sky brushes -> func_sky, fix detail variants.</summary>
        static void PrepassMap(MapFile map) {
            var world = map.Worldspawn;
            var liquidEnts = new Dictionary<string, Entity>();
            var skyEnt = new Entity { ClassName = "func_sky", Properties = new Dictionary<string, string>() };
            void Process(Entity ent, bool isWorld) {
                for (int i = ent.Children.Count - 1; i >= 0; i--) {
                    var child = ent.Children[i];
                    if (child is Entity ce) {
                        var cn = ce.ClassName ?? "";
                        if (cn == "func_detail_illusionary") ce.ClassName = "func_illusionary";
                        if (cn == "func_detail_fence") ce.ClassName = "func_detail_wall";
                        Process(ce, false);
                        continue;
                    }
                    if (!(child is Solid s) || s.Faces.Count == 0) continue;
                    var names = s.Faces.Select(f => f.TextureName.ToLowerInvariant()).ToList();
                    bool allUtility = names.All(n => n == "skip" || n == "hint" || n == "hintskip" || n.EndsWith("skip") && n.StartsWith("*") || n == "trigger" && isWorld);
                    if (allUtility && (isWorld || (ent.ClassName ?? "").StartsWith("func_"))) { ent.Children.RemoveAt(i); continue; }
                    bool anyLiquid = names.Any(n => n.StartsWith("*") && !n.EndsWith("skip"));
                    if (anyLiquid && (isWorld || (ent.ClassName ?? "").StartsWith("func_group") || (ent.ClassName ?? "").StartsWith("func_detail"))) {
                        var key = names.First(n => n.StartsWith("*"));
                        var type = LiquidVolume.FromTextureName(key).ToString();
                        if (!liquidEnts.TryGetValue(type, out var le)) {
                            le = new Entity { ClassName = "func_water", Properties = new Dictionary<string, string> { { "_liquid", key } } };
                            liquidEnts[type] = le;
                        }
                        ent.Children.RemoveAt(i); le.Children.Add(s);
                        continue;
                    }
                    bool allSky = names.All(n => n.StartsWith("sky"));
                    if (allSky && isWorld) { ent.Children.RemoveAt(i); skyEnt.Children.Add(s); continue; }
                }
            }
            Process(world, true);
            foreach (var le in liquidEnts.Values) world.Children.Add(le);
            if (skyEnt.Children.Count > 0) world.Children.Add(skyEnt);
        }

        public static void ImportMap(string mapName) {
            var mapPath = Path.Combine(MapSourceDir, mapName + ".map");
            if (!File.Exists(mapPath)) { Debug.LogError("missing " + mapPath); return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mapFile = ScopaCore.ParseMap(mapPath, new ScopaMapConfig());
            PrepassMap(mapFile);
            var cfg = MakeConfig(mapFile);
            var root = ScopaCore.BuildMapIntoGameObject(mapName, mapFile, cfg, out var meshList);
            Debug.Log($"{mapName}: built geometry in {sw.Elapsed.TotalSeconds:F1}s, {meshList.Count} meshes");

            // mesh assets container
            var genPath = $"{GenDir}/{mapName}_meshes.asset";
            AssetDatabase.DeleteAsset(genPath);
            var container = ScriptableObject.CreateInstance<MeshContainer>();
            AssetDatabase.CreateAsset(container, genPath);

            // entity data -> QEntity
            var worldProps = new Dictionary<string, string>();
            var lights = new List<BakedLight>();
            var allScopa = root.GetComponentsInChildren<ScopaEntity>(true);
            foreach (var se in allScopa) {
                var data = se.entityData;
                var q = se.gameObject.AddComponent<QEntity>();
                q.classname = data.ClassName; q.spawnflags = data.SpawnFlags;
                foreach (var kv in data.Properties) { q.keys.Add(kv.Key); q.values.Add(kv.Value); }
                q.isBrushEntity = se.GetComponentInChildren<MeshFilter>() != null || se.GetComponentInChildren<Collider>() != null;
                if (data.ClassName == "worldspawn") foreach (var kv in data.Properties) worldProps[kv.Key] = kv.Value;
                if (data.ClassName.StartsWith("light")) lights.Add(BakedLight.FromEntity(q));
                UnityEngine.Object.DestroyImmediate(se);
            }
            // point entity objects that are not used at runtime can be dropped (lights, info_null...) but keep light_* with models & targetnames
            foreach (var q in root.GetComponentsInChildren<QEntity>(true)) {
                if (q.classname == "light" || q.classname == "light_globe" || (q.classname.StartsWith("light") && string.IsNullOrEmpty(q.TargetName) && !q.classname.Contains("torch") && !q.classname.Contains("flame") && !q.classname.Contains("fluoro"))) {
                    if (!q.isBrushEntity) UnityEngine.Object.DestroyImmediate(q.gameObject);
                } else if (q.classname == "info_null" || q.classname == "info_intermission" || q.classname == "func_group") {
                    if (!q.isBrushEntity) UnityEngine.Object.DestroyImmediate(q.gameObject);
                }
            }

            // triggers & liquids: colliders as triggers, on the Trigger layer; sky: leave solid
            foreach (var q in root.GetComponentsInChildren<QEntity>(true)) {
                if (q.classname.StartsWith("trigger") || q.classname == "func_water") {
                    foreach (var c in q.GetComponentsInChildren<Collider>()) { c.isTrigger = true; c.gameObject.layer = 9; }
                    foreach (var r in q.GetComponentsInChildren<Renderer>()) if (q.classname.StartsWith("trigger")) r.enabled = false;
                    q.gameObject.layer = 9;
                } else if (q.classname.Contains("illusionary")) {
                    // non-solid brush entities: strip colliders (Scopa's nonsolid keyword path crashes, so we do it here)
                    foreach (var c in q.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                }
            }

            // lighting
            Physics.SyncTransforms();
            var sunCount = BakeLighting(root, lights, worldProps, mapName);
            Debug.Log($"{mapName}: lighting baked ({lights.Count} lights, sun={sunCount}) at {sw.Elapsed.TotalSeconds:F1}s");

            // save meshes into the container
            int saved = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh)) { mf.sharedMesh.name = mf.gameObject.name; AssetDatabase.AddObjectToAsset(mf.sharedMesh, container); saved++; }
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh != null && !AssetDatabase.Contains(mc.sharedMesh)) { AssetDatabase.AddObjectToAsset(mc.sharedMesh, container); saved++; }
            AssetDatabase.SaveAssets();

            // level objects
            var level = new GameObject("Level");
            var info = level.AddComponent<LevelInfo>(); info.mapName = mapName;
            worldProps.TryGetValue("message", out info.message);
            if (worldProps.TryGetValue("worldtype", out var wt)) int.TryParse(wt, out info.worldtype);
            level.AddComponent<LevelSetup>();
            SetupRenderSettings(worldProps);

            var scenePath = $"{SceneDir}/{mapName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"{mapName}: saved scene ({saved} meshes) in {sw.Elapsed.TotalSeconds:F1}s");
        }

        static void SetupRenderSettings(Dictionary<string, string> worldProps) {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.21f, 0.2f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            if (worldProps.TryGetValue("_fog", out var fog) || worldProps.TryGetValue("fog", out fog)) {
                var parts = fog.Split(' ');
                if (parts.Length >= 1 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0) {
                    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = d * 32f * 0.08f;
                    var col = new Color(0.3f, 0.3f, 0.3f);
                    if (parts.Length >= 4) col = new Color(P(parts[1]), P(parts[2]), P(parts[3]));
                    RenderSettings.fogColor = col * 0.6f;
                }
            }
            var camBg = new GameObject("SceneLighting"); UnityEngine.Object.DestroyImmediate(camBg);
        }

        static float P(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0;

        // ------------------------------------------------------------------ brush models
        static void ImportBrushModel(string mapPath, string name) {
            var mapFile = ScopaCore.ParseMap(mapPath, new ScopaMapConfig());
            var cfg = MakeConfig(mapFile);
            cfg.colliderMode = ScopaMapConfig.ColliderImportMode.BoxAndConvex;
            var root = ScopaCore.BuildMapIntoGameObject(name, mapFile, cfg, out var meshList);
            foreach (var se in root.GetComponentsInChildren<ScopaEntity>(true)) UnityEngine.Object.DestroyImmediate(se);
            // Quake brush models are authored with their min corner near the origin; item origin = model center bottom. Recentre so the local origin is bottom-centre.
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0) {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                var offset = new Vector3(b.center.x, b.min.y, b.center.z);
                foreach (Transform t in root.transform) t.position -= offset;
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>()) foreach (var m in mr.sharedMaterials) { }
            // fullbright-ish white vertex colours so the World shader shows them lit
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) {
                var mesh = mf.sharedMesh; var cols = new Color[mesh.vertexCount]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0.45f, 0.45f, 0.45f, 1); mesh.colors = cols;
            }
            var genPath = $"{GenDir}/bm_{name}_meshes.asset";
            AssetDatabase.DeleteAsset(genPath);
            var container = ScriptableObject.CreateInstance<MeshContainer>(); AssetDatabase.CreateAsset(container, genPath);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh)) AssetDatabase.AddObjectToAsset(mf.sharedMesh, container);
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh != null && !AssetDatabase.Contains(mc.sharedMesh)) AssetDatabase.AddObjectToAsset(mc.sharedMesh, container);
            AssetDatabase.SaveAssets();
            var prefabPath = $"{BrushPrefabDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("brush model prefab: " + prefabPath);
        }

        // ------------------------------------------------------------------ menu scene & build list
        static void BuildMenuScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("MainMenu"); go.AddComponent<MainMenu>();
            var cam = new GameObject("MenuCamera"); cam.tag = "MainCamera"; var c = cam.AddComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black; cam.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Menu.unity");
        }

        static void UpdateBuildScenes() {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene($"{SceneDir}/Menu.unity", true) };
            foreach (var f in Directory.GetFiles(SceneDir, "*.unity").OrderBy(x => x)) {
                var p = f.Replace('\\', '/');
                if (p.EndsWith("/Menu.unity")) continue;
                list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("build scenes: " + list.Count);
        }

        // ------------------------------------------------------------------ lighting
        class BakedLight {
            public Vector3 pos; public float light = 300f, wait = 1f; public int delay; public Color color = Color.white;
            public static BakedLight FromEntity(QEntity q) {
                var l = new BakedLight { pos = q.Origin, light = q.GetFloat("light", 300f), wait = Mathf.Max(0.01f, q.GetFloat("wait", 1f)), delay = q.GetInt("delay", 0) };
                if (q.classname.Contains("torch")) l.light = q.GetFloat("light", 200f);
                if (QuakeUnits.TryParseVector3(q.Get("_color"), out var c)) {
                    if (c.x > 1.01f || c.y > 1.01f || c.z > 1.01f) c /= 255f;
                    l.color = new Color(c.x, c.y, c.z);
                } else if (q.classname.Contains("torch") || q.classname.Contains("flame")) l.color = new Color(1f, 0.75f, 0.45f);
                else if (q.classname.Contains("fluoro")) l.color = new Color(0.85f, 0.9f, 1f);
                return l;
            }
            /// <summary>Contribution in 0..~2 range (1 = Quake 255).</summary>
            public float Intensity(float distUnits) {
                switch (delay) {
                    case 1: return light * 128f / Mathf.Max(1f, distUnits * wait) / 255f;
                    case 2: return light * 128f * 128f / Mathf.Max(1f, distUnits * distUnits * wait * wait) / 255f;
                    case 3: return light / 255f;
                    case 4: return Mathf.Max(0, light - distUnits * wait) / 255f;
                    default: return Mathf.Max(0, light - distUnits * wait) / 255f;
                }
            }
            public float Range => delay == 0 || delay == 4 ? light / wait : delay == 3 ? 100000f : light * 4f / wait;
        }

        static int BakeLighting(GameObject root, List<BakedLight> lights, Dictionary<string, string> world, string mapName) {
            float sunlight = 0, sunlight2 = 0; Vector3 sunDir = Vector3.down; Color sunColor = Color.white, sun2Color = Color.white;
            if (world.TryGetValue("_sunlight", out var s)) sunlight = P(s);
            if (world.TryGetValue("_sunlight2", out var s2)) sunlight2 = P(s2);
            if (QuakeUnits.TryParseVector3(world.TryGetValue("_sun_mangle", out var sm) ? sm : (world.TryGetValue("_sunlight_mangle", out sm) ? sm : ""), out var mangle)) {
                float yaw = mangle.x * Mathf.Deg2Rad, pitch = mangle.y * Mathf.Deg2Rad;
                var q = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(pitch), Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch)); // Quake dir the sun shines along
                sunDir = QuakeUnits.ToUnityDir(q.x, q.y, q.z).normalized;
            }
            if (QuakeUnits.TryParseVector3(world.TryGetValue("_sunlight_color", out var sc) ? sc : "", out var scv)) { if (scv.x > 1.01f) scv /= 255f; sunColor = new Color(scv.x, scv.y, scv.z); }
            if (QuakeUnits.TryParseVector3(world.TryGetValue("_sunlight2_color", out var sc2) ? sc2 : "", out var scv2)) { if (scv2.x > 1.01f) scv2 /= 255f; sun2Color = new Color(scv2.x, scv2.y, scv2.z); }
            float minlight = world.TryGetValue("_minlight", out var ml) ? P(ml) / 255f : 0f;
            var ambient = new Color(0.04f, 0.04f, 0.045f) + Color.white * minlight;

            // sky colliders must not block sun rays
            var skyColliders = new List<Collider>();
            foreach (var q in root.GetComponentsInChildren<QEntity>(true)) if (q.classname == "func_sky") skyColliders.AddRange(q.GetComponentsInChildren<Collider>());
            foreach (var c in skyColliders) c.enabled = false;
            Physics.SyncTransforms();
            int mask = ~LayerMask.GetMask("Trigger");

            var hemi = new[] { Vector3.up, (Vector3.up + Vector3.right).normalized, (Vector3.up - Vector3.right).normalized, (Vector3.up + Vector3.forward).normalized, (Vector3.up - Vector3.forward).normalized };
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            int totalVerts = 0;
            foreach (var mf in filters) {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                var q = mf.GetComponentInParent<QEntity>();
                bool isSky = q != null && q.classname == "func_sky";
                bool isLiquid = q != null && q.classname == "func_water";
                if (q != null && q.classname.StartsWith("trigger")) continue;
                if (isSky) continue;
                if (mesh.vertexCount < 65000 && !isLiquid) SubdivideMesh(mesh, 3.0f, 60000);
                var verts = mesh.vertices; var normals = mesh.normals; var cols = new Color[verts.Length];
                var l2w = mf.transform.localToWorldMatrix;
                bool moverEntity = q != null && (q.classname.StartsWith("func_door") || q.classname == "func_plat" || q.classname == "func_button" || q.classname == "func_train");
                var myColliders = moverEntity ? mf.GetComponentInParent<QEntity>().GetComponentsInChildren<Collider>() : null;
                if (myColliders != null) foreach (var c in myColliders) c.enabled = false; // movers: don't self-shadow (they're often in a slot)
                for (int i = 0; i < verts.Length; i++) {
                    var wp = l2w.MultiplyPoint3x4(verts[i]);
                    var n = normals != null && normals.Length == verts.Length ? l2w.MultiplyVector(normals[i]).normalized : Vector3.up;
                    var origin = wp + n * 0.02f;
                    var acc = ambient;
                    foreach (var L in lights) {
                        var to = L.pos - wp; float distU = to.magnitude * 32f;
                        if (distU > L.Range) continue;
                        float ndl = Vector3.Dot(n, to.normalized);
                        if (ndl <= 0 && !isLiquid) continue;
                        float inten = L.Intensity(distU) * Mathf.Clamp01(isLiquid ? 1f : ndl * 0.5f + 0.5f);
                        if (inten < 0.004f) continue;
                        if (Physics.Raycast(origin, to.normalized, to.magnitude - 0.03f, mask, QueryTriggerInteraction.Ignore)) continue;
                        acc += L.color * inten;
                    }
                    if (sunlight > 0) {
                        float ndl = Vector3.Dot(n, -sunDir);
                        if (ndl > 0 && !Physics.Raycast(origin, -sunDir, 2000f, mask, QueryTriggerInteraction.Ignore)) acc += sunColor * (sunlight / 255f) * ndl;
                    }
                    if (sunlight2 > 0) {
                        int open = 0;
                        foreach (var h in hemi) { if (Vector3.Dot(n, h) <= 0) { continue; } if (!Physics.Raycast(origin, h, 2000f, mask, QueryTriggerInteraction.Ignore)) open++; }
                        acc += sun2Color * (sunlight2 / 255f) * (open / (float)hemi.Length) * 0.8f;
                    }
                    // store half so the shader can go to 2x
                    cols[i] = new Color(Mathf.Clamp01(acc.r * 0.5f), Mathf.Clamp01(acc.g * 0.5f), Mathf.Clamp01(acc.b * 0.5f), 1f);
                }
                if (myColliders != null) foreach (var c in myColliders) c.enabled = true;
                mesh.colors = cols;
                totalVerts += verts.Length;
            }
            foreach (var c in skyColliders) c.enabled = true;
            Debug.Log($"{mapName}: lit {totalVerts} vertices");
            return sunlight > 0 ? 1 : 0;
        }

        /// <summary>Splits long edges so vertex lighting has enough resolution. Duplicates vertices (no welding).</summary>
        static void SubdivideMesh(Mesh mesh, float maxEdge, int maxVerts) {
            var v = new List<Vector3>(mesh.vertices); var uv = new List<Vector2>(mesh.uv); var n = new List<Vector3>(mesh.normals);
            bool hasUv = uv.Count == v.Count, hasN = n.Count == v.Count;
            var tris = new List<int>(mesh.triangles);
            var outTris = new List<int>(tris.Count * 2);
            var stack = new Stack<(int, int, int)>();
            for (int i = 0; i < tris.Count; i += 3) stack.Push((tris[i], tris[i + 1], tris[i + 2]));
            float maxSq = maxEdge * maxEdge;
            int Mid(int a, int b) {
                v.Add((v[a] + v[b]) * 0.5f);
                if (hasUv) uv.Add((uv[a] + uv[b]) * 0.5f);
                if (hasN) n.Add((n[a] + n[b]).normalized);
                return v.Count - 1;
            }
            while (stack.Count > 0) {
                var (a, b, c) = stack.Pop();
                float ab = (v[a] - v[b]).sqrMagnitude, bc = (v[b] - v[c]).sqrMagnitude, ca = (v[c] - v[a]).sqrMagnitude;
                float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
                if (longest <= maxSq || v.Count > maxVerts) { outTris.Add(a); outTris.Add(b); outTris.Add(c); continue; }
                if (longest == ab) { int m = Mid(a, b); stack.Push((a, m, c)); stack.Push((m, b, c)); }
                else if (longest == bc) { int m = Mid(b, c); stack.Push((a, b, m)); stack.Push((a, m, c)); }
                else { int m = Mid(c, a); stack.Push((a, b, m)); stack.Push((m, b, c)); }
            }
            mesh.Clear();
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); if (hasUv) mesh.SetUVs(0, uv); if (hasN) mesh.SetNormals(n);
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateBounds();
        }
    }

}
#endif
