using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ostrov.FloatingIslandEditor
{
    /// <summary>
    /// Собирает уровень «Летающий остров» с нуля: настройки рендера, профиль
    /// постобработки, материалы для моделей из Blender, саму сцену и запись в Build Settings.
    /// Раскладка острова и фона — в FloatingIslandSceneBuilder.Art.cs.
    /// Запуск: меню Ostrov/Scene/Build Floating Island Scene или
    /// -executeMethod Ostrov.FloatingIslandEditor.FloatingIslandSceneBuilder.RunAll
    /// Скриншоты: меню Ostrov/Scene/Capture Floating Island Shots или
    /// -executeMethod Ostrov.FloatingIslandEditor.FloatingIslandSceneBuilder.CaptureShots
    /// (каталог для PNG — переменная окружения FLOATING_ISLAND_SHOT_DIR).
    /// </summary>
    public static partial class FloatingIslandSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FloatingIsland.unity";

        const string SettingsDir = "Assets/Settings/FloatingIsland";

        const string SourcePipelinePath = "Assets/Settings/PC_RPAsset.asset";
        const string SourceRendererPath = "Assets/Settings/PC_Renderer.asset";
        const string PipelinePath = SettingsDir + "/FloatingIsland_RPAsset.asset";
        const string RendererPath = SettingsDir + "/FloatingIsland_Renderer.asset";
        const string ProfilePath = SettingsDir + "/FloatingIsland_VolumeProfile.asset";

        // Небо — плоский градиент (шейдер Ostrov/FloatingIsland/GradientSky).
        // Камера смотрит вниз под 27–57°, поэтому в кадре видна только часть
        // под горизонтом: светлая дымка сверху кадра и тёмная бездна снизу.
        static readonly Color SkyZenith = new Color(0.13f, 0.14f, 0.16f);
        static readonly Color SkyHorizon = new Color(0.44f, 0.455f, 0.48f);
        static readonly Color SkyMist = new Color(0.33f, 0.345f, 0.37f);
        static readonly Color SkyAbyss = new Color(0.15f, 0.16f, 0.175f);
        // Туман равен цвету неба у верхнего края кадра: дальние шпили тонут в нём.
        static readonly Color FogColor = SkyMist;

        // Масштаб: остров ~45 м в поперечнике, вместе со вторым островом и мостом ~100 м.
        const float BackdropHalfExtent = 260f;

        // Камера.
        const float CameraFocusHeight = 4f;
        const float CameraDistance = 112f;
        const float CameraMinDistance = 42f;
        const float CameraMaxDistance = 200f;
        const float CameraPanLimit = 55f;

        [MenuItem("Ostrov/Scene/Build Floating Island Scene")]
        public static void RunAll()
        {
            try
            {
                Build();
                Debug.Log("[FloatingIslandSceneBuilder] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[FloatingIslandSceneBuilder] FAILED: " + e);
                throw;
            }
        }

        [MenuItem("Ostrov/Scene/Capture Floating Island Shots")]
        public static void CaptureShots()
        {
            try
            {
                Capture();
                Debug.Log("[FloatingIslandSceneBuilder] CAPTURE DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[FloatingIslandSceneBuilder] CAPTURE FAILED: " + e);
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Сборка
        // ------------------------------------------------------------------

        static void Build()
        {
            EnsureFolder(SettingsDir);
            EnsureFolder(ArtMaterialDir);
            EnsureFolder("Assets/Scenes");

            var pipeline = CreatePipelineAsset();
            var profile = CreateVolumeProfile();
            var mats = CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment(mats.Sky);

            // --- Остров: всё, что внутри Visual, летит вместе ---
            var island = new GameObject("Island");
            var mover = island.AddComponent<IslandMover>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(island.transform, false);
            var bob = visual.AddComponent<FloatBob>();
            bob.Configure(0.35f, 11f, 0.25f, 0f);
            BuildIsland(visual.transform, mats);

            // --- Камера ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = FogColor;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 1500f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camGo.AddComponent<AudioListener>();
            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            camData.renderShadows = true;
            camData.dithering = true;
            camData.stopNaN = true;

            var controller = camGo.AddComponent<IsoCameraController>();
            controller.Target = island.transform;
            controller.Configure(CameraFocusHeight, CameraDistance, CameraMinDistance, CameraMaxDistance, CameraPanLimit);
            mover.ViewTransform = camGo.transform;
            controller.SnapToTarget();

            // --- Свет ---
            var lightGo = new GameObject("Directional Light");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.8f, 0.83f, 0.88f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowNormalBias = 0.6f;
            sun.shadowBias = 0.1f;
            lightGo.transform.rotation = Quaternion.Euler(34f, 118f, 0f);
            var lightData = lightGo.GetComponent<UniversalAdditionalLightData>();
            if (lightData == null)
                lightData = lightGo.AddComponent<UniversalAdditionalLightData>();
            lightData.softShadowQuality = SoftShadowQuality.High;
            RenderSettings.sun = sun;

            // --- Постобработка ---
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            var overrideGo = new GameObject("Pipeline Override");
            overrideGo.AddComponent<PipelineAssetOverride>().PipelineAsset = pipeline;

            // --- Окружение: фон, дальние скалы, туман ---
            var env = new GameObject("Environment");
            var backdropGo = new GameObject("Backdrop");
            backdropGo.transform.SetParent(env.transform, false);
            var wrapper = backdropGo.AddComponent<BackdropWrapper>();
            wrapper.Target = island.transform;
            wrapper.HalfExtent = BackdropHalfExtent;
            BuildBackdrop(backdropGo.transform);

            var mistMat = CreateMistMaterial();
            // Нижний слой прячет кончики базальтовых столбов (они уходят до -33 м).
            CreateMistLayer("Mist Below", env.transform, island.transform, mistMat, -40f,
                new Vector3(520f, 16f, 520f), new Vector2(50f, 100f), new Color(0.43f, 0.44f, 0.46f, 0.5f), 26f, 50f, 1600, 1.2f);
            // Средний слой: рваные клочья у скал, на полпути вниз.
            CreateMistLayer("Mist Mid", env.transform, island.transform, mistMat, -16f,
                new Vector3(200f, 10f, 200f), new Vector2(18f, 36f), new Color(0.42f, 0.44f, 0.47f, 0.22f), 3f, 40f, 160, 0.9f);
            // Редкие клочья над землёй острова.
            CreateMistLayer("Mist Wisps", env.transform, island.transform, mistMat, 2f,
                new Vector3(220f, 12f, 220f), new Vector2(12f, 28f), new Color(0.5f, 0.52f, 0.55f, 0.1f), 2.5f, 30f, 90, 1.4f);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new Exception("Не удалось сохранить сцену " + ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // Рендер: свой пайплайн-ассет с длинными тенями и сильным SSAO
        // ------------------------------------------------------------------

        static RenderPipelineAsset CreatePipelineAsset()
        {
            CopyFresh(SourceRendererPath, RendererPath);
            CopyFresh(SourcePipelinePath, PipelinePath);

            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<ScriptableRendererFeature>())
            {
                if (sub.GetType().Name != "ScreenSpaceAmbientOcclusion")
                    continue;
                var so = new SerializedObject(sub);
                SetFloat(so, "m_Settings.Intensity", 1.1f);
                SetFloat(so, "m_Settings.Radius", 0.65f);
                SetFloat(so, "m_Settings.DirectLightingStrength", 0.35f);
                SetFloat(so, "m_Settings.Falloff", 120f);
                SetInt(so, "m_Settings.Samples", 0);     // High
                SetInt(so, "m_Settings.BlurQuality", 0); // High
                SetBool(so, "m_Active", true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var pso = new SerializedObject(pipeline);
            var list = pso.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetInt(pso, "m_DefaultRendererIndex", 0);
            SetBool(pso, "m_SupportsHDR", true);
            SetInt(pso, "m_HDRColorBufferPrecision", 1); // 64 бита: меньше полос в тёмных градиентах
            SetInt(pso, "m_MSAA", 1);                    // MSAA выкл., сглаживание — SMAA на камере
            SetBool(pso, "m_RequireDepthTexture", true);
            SetFloat(pso, "m_ShadowDistance", 320f);
            SetInt(pso, "m_MainLightShadowmapResolution", 4096);
            SetInt(pso, "m_ShadowCascadeCount", 4);
            var split = pso.FindProperty("m_Cascade4Split");
            if (split != null)
                split.vector3Value = new Vector3(0.12f, 0.3f, 0.58f);
            SetBool(pso, "m_SoftShadowsSupported", true);
            SetInt(pso, "m_SoftShadowQuality", 3);
            SetInt(pso, "m_ColorGradingMode", 1);        // HDR-грейдинг
            SetInt(pso, "m_ColorGradingLutSize", 64);
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        static void CopyFresh(string src, string dst)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(dst) != null)
                AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(src, dst))
                throw new Exception($"Не удалось скопировать {src} -> {dst}");
        }

        // ------------------------------------------------------------------
        // Профиль Volume
        // ------------------------------------------------------------------

        static VolumeProfile CreateVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(ProfilePath) != null)
                AssetDatabase.DeleteAsset(ProfilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            // Холодный мрачный грейд, но зелень мха не глушим: она должна
            // светиться на фоне чёрного базальта, как на референсе.
            var adjust = Add<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.15f);
            adjust.contrast.Override(14f);
            adjust.colorFilter.Override(new Color(0.97f, 0.98f, 1f));
            adjust.saturation.Override(-6f);

            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(-3f);
            white.tint.Override(0f);

            var lgg = Add<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(0.98f, 0.99f, 1.02f, 0.01f));  // холодные тени
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
            lgg.gain.Override(new Vector4(1f, 1f, 1.01f, 0f));

            var split = Add<SplitToning>(profile);
            split.shadows.Override(new Color(0.485f, 0.5f, 0.52f));
            split.highlights.Override(new Color(0.52f, 0.51f, 0.49f));
            split.balance.Override(-20f);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));
            bloom.highQualityFiltering.Override(true);

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(new Color(0.02f, 0.025f, 0.035f));
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.5f);
            vignette.rounded.Override(false);

            // Плёночного зерна и хроматической аберрации нет: они спорят
            // с чистыми плоскими гранями low poly.

            // Мягкое размытие только дальнего фона: остров с мостом ближе 260 м при любом зуме.
            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(260f);
            dof.gaussianEnd.Override(650f);
            dof.gaussianMaxRadius.Override(1.1f);
            dof.highQualitySampling.Override(true);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // ------------------------------------------------------------------
        // Небо, свет окружения и туман
        // ------------------------------------------------------------------

        static void ConfigureEnvironment(Material sky)
        {
            RenderSettings.skybox = sky;
            // Свет окружения: холодное небо сверху, дымка сбоку, тёмная бездна снизу.
            // Подбрюшье острова остаётся почти чёрным, как базальт на референсе.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.27f, 0.29f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.19f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.055f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.2f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.0034f;
        }

        // ------------------------------------------------------------------
        // Туман: материал и текстура «клякса»
        // ------------------------------------------------------------------

        static Material CreateMistMaterial()
        {
            var tex = CreatePuffTexture();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_FI_Mist" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Мягкие частицы выключены: туман висит над пустой бездной, а с ними
            // слой пропадал при рендере в редакторе. Края гасит малая альфа.
            mat.SetFloat("_SoftParticlesEnabled", 0f);
            mat.SetFloat("_CameraFadingEnabled", 1f);
            mat.SetFloat("_CameraNearFadeDistance", 2f);
            mat.SetFloat("_CameraFarFadeDistance", 10f);
            mat.SetVector("_CameraFadeParams", new Vector4(2f, 1f / 8f, 0f, 0f));
            mat.EnableKeyword("_FADING_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return SaveOrReplace(mat, $"{ArtMaterialDir}/M_FI_Mist.mat");
        }

        /// <summary>Мягкая «клякса» облака: радиальный спад, изъеденный шумом.</summary>
        static Texture2D CreatePuffTexture()
        {
            const int Size = 128;
            string path = $"{ArtTextureDir}/T_FI_MistPuff.png";
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float fall = Mathf.Clamp01(1f - r);
                    fall = fall * fall * (3f - 2f * fall);
                    float n = 0f;
                    float amp = 0.5f;
                    float freq = 3f;
                    for (int o = 0; o < 4; o++)
                    {
                        n += Mathf.PerlinNoise(x / (float)Size * freq + 17.3f * o, y / (float)Size * freq + 5.1f * o) * amp;
                        amp *= 0.5f;
                        freq *= 2f;
                    }

                    float a = Mathf.Clamp01(fall * (0.35f + n * 1.1f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------
        // Туман из частиц
        // ------------------------------------------------------------------

        static void CreateMistLayer(string name, Transform parent, Transform target, Material mat, float y,
            Vector3 box, Vector2 size, Color color, float rate, float lifetime, int max, float drift)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<FollowTargetXZ>().Target = target;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-drift, -drift * 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-drift * 0.25f, drift * 0.25f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 4f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        // ------------------------------------------------------------------
        // Скриншоты для проверки
        // ------------------------------------------------------------------

        static void Capture()
        {
            var outDir = Environment.GetEnvironmentVariable("FLOATING_ISLAND_SHOT_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath("Temp/floating-island-shots");
            Directory.CreateDirectory(outDir);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var pipelineOverride = Object.FindAnyObjectByType<PipelineAssetOverride>();
            var controller = Object.FindAnyObjectByType<IsoCameraController>();
            if (controller == null)
                throw new Exception("На сцене нет IsoCameraController");
            if (controller.Target == null || controller.Target.name != "Island")
                throw new Exception("IsoCameraController не смотрит на Island");
            var mover = Object.FindAnyObjectByType<IslandMover>();
            if (mover == null || mover.transform.Find("Visual") == null)
                throw new Exception("Нет Island/Visual с IslandMover");

            foreach (var ps in Object.FindObjectsByType<ParticleSystem>())
            {
                ps.Simulate(40f, true, true);
                Debug.Log($"[FloatingIslandSceneBuilder] {ps.name}: {ps.particleCount} частиц");
            }

            var camera = controller.GetComponent<Camera>();
            var previous = QualitySettings.renderPipeline;
            if (pipelineOverride != null && pipelineOverride.PipelineAsset != null)
                QualitySettings.renderPipeline = pipelineOverride.PipelineAsset;
            try
            {
                var so = new SerializedObject(controller);
                var dist = so.FindProperty("_distance");
                float original = dist.floatValue;
                foreach (var (label, d) in new[] { ("default", original), ("near", so.FindProperty("_minDistance").floatValue), ("far", so.FindProperty("_maxDistance").floatValue) })
                {
                    dist.floatValue = d;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    controller.SnapToTarget();
                    Render(camera, Path.Combine(outDir, $"floating_island_{label}.png"));
                }

                dist.floatValue = original;
                so.ApplyModifiedPropertiesWithoutUndo();
                controller.SnapToTarget();
            }
            finally
            {
                QualitySettings.renderPipeline = previous;
            }

            Debug.Log("[FloatingIslandSceneBuilder] Скриншоты: " + outDir);
        }

        static void Render(Camera camera, string path)
        {
            const int Width = 1600;
            const int Height = 900;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            var prevTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.Render(); // второй кадр: прогреваются история и автоэкспозиция эффектов
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            camera.targetTexture = prevTarget;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log("[FloatingIslandSceneBuilder] PNG: " + path);
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null)
                Object.DestroyImmediate(c);
        }

        static Vector2 RandomOnTop(System.Random rng, float minR, float maxR)
        {
            float a = Range(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Range(rng, minR * minR, maxR * maxR));
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>
        /// Сохраняет ассет. Если ассет того же типа уже есть, переписывает его
        /// содержимое и сохраняет GUID, чтобы ссылки на него не ломались.
        /// </summary>
        static T SaveOrReplace<T>(T obj, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(obj);
                return existing;
            }

            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetFloat(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[FloatingIslandSceneBuilder] Нет свойства {prop} у {so.targetObject.name}");
                return;
            }

            p.floatValue = value;
        }

        static void SetInt(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[FloatingIslandSceneBuilder] Нет свойства {prop} у {so.targetObject.name}");
                return;
            }

            p.intValue = value;
        }

        static void SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[FloatingIslandSceneBuilder] Нет свойства {prop} у {so.targetObject.name}");
                return;
            }

            p.boolValue = value;
        }
    }
}
