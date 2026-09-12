using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace Ostrov.HexworldEditor
{
    /// <summary>
    /// Собирает играбельную сцену Assets/Scenes/Hexworld.unity из префабов и
    /// View-скриптов. Запуск: меню Ostrov/Scene или
    /// -executeMethod Ostrov.HexworldEditor.OstrovSceneSetup.RunAll
    /// </summary>
    public static class OstrovSceneSetup
    {
        /// <summary>Путь новой сцены.</summary>
        public const string ScenePath = "Assets/Scenes/Hexworld.unity";

        /// <summary>Путь старой сцены, которую надо убрать.</summary>
        const string OldScenePath = "Assets/Scenes/SampleScene.unity";

        const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        const string TileDir = "Assets/Prefabs/Tiles";
        const string BuildingDir = "Assets/Prefabs/Buildings";
        const string MaterialDir = "Assets/Art/Materials";

        /// <summary>Цвет неба за парящим островом.</summary>
        static readonly Color SkyColor = new Color(0.42f, 0.66f, 0.85f, 1f);

        [MenuItem("Ostrov/Scene/Build Hexworld Scene")]
        public static void RunAll()
        {
            try
            {
                BuildScene();
                Debug.Log("[OstrovSceneSetup] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovSceneSetup] FAILED: " + e);
                throw;
            }
        }

        /// <summary>Создаёт сцену, сохраняет её и прописывает в Build Settings.</summary>
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureLighting();

            // --- Камера ---
            var rigGo = new GameObject("CameraRig");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);

            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();

            var rig = rigGo.AddComponent<HexworldCameraRig>();

            // --- Свет ---
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // --- Post-processing ---
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (volume.sharedProfile == null)
                Debug.LogWarning("[OstrovSceneSetup] Профиль Volume не найден: " + VolumeProfilePath);

            // --- EventSystem: нужен, чтобы клики по UI не проходили сквозь доску ---
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            // --- Корень игры ---
            var rootGo = new GameObject("Hexworld");
            var tilesGo = new GameObject("Tiles");
            tilesGo.transform.SetParent(rootGo.transform, false);

            var boardView = rootGo.AddComponent<HexBoardView>();
            var inputController = rootGo.AddComponent<HexworldInputController>();
            var aiDriver = rootGo.AddComponent<HexworldAiTurnDriver>();
            var runner = rootGo.AddComponent<HexworldGameRunner>();

            WireCameraRig(rig, camGo.transform);
            WireBoardView(boardView, tilesGo.transform);
            WireInput(inputController, camera, boardView);
            WireRunner(runner, boardView, inputController, aiDriver, rig);

            // Стартовая рамка камеры, чтобы сцена и в редакторе смотрелась правильно.
            rig.Frame(Vector3.zero, (3f * HexLayout.HexRadius * 1.7320508f) + HexLayout.HexRadius);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ScenePath)));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new Exception("Не удалось сохранить сцену " + ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterInBuildSettings();
            RemoveOldScene();

            Debug.Log("[OstrovSceneSetup] Сцена собрана: " + ScenePath);
        }

        /// <summary>Мягкий градиентный ambient, без скайбокса — остров парит в пустоте.</summary>
        static void ConfigureLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.63f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.46f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.24f, 0.28f);
            RenderSettings.fog = false;
        }

        static void WireCameraRig(HexworldCameraRig rig, Transform cameraTransform)
        {
            var so = new SerializedObject(rig);
            so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            so.FindProperty("_pitch").floatValue = 55f;
            so.FindProperty("_yaw").floatValue = 30f;
            so.FindProperty("_distance").floatValue = 16f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireBoardView(HexBoardView boardView, Transform tileRoot)
        {
            var so = new SerializedObject(boardView);
            so.FindProperty("_tileRoot").objectReferenceValue = tileRoot;

            SetPrefab(so, "_library._grassTile", TileDir + "/Tile_Grass.prefab");
            SetPrefab(so, "_library._forestTile", TileDir + "/Tile_Forest.prefab");
            SetPrefab(so, "_library._stoneTile", TileDir + "/Tile_Stone.prefab");
            SetPrefab(so, "_library._rubbleTile", TileDir + "/Tile_Rubble.prefab");

            SetPrefab(so, "_library._castle", BuildingDir + "/Bld_Castle.prefab");
            SetPrefab(so, "_library._cottage", BuildingDir + "/Bld_Cottage.prefab");
            SetPrefab(so, "_library._farm", BuildingDir + "/Bld_Farm.prefab");
            SetPrefab(so, "_library._quarry", BuildingDir + "/Bld_Quarry.prefab");
            SetPrefab(so, "_library._church", BuildingDir + "/Bld_Church.prefab");
            SetPrefab(so, "_library._barracks", BuildingDir + "/Bld_Barracks.prefab");
            SetPrefab(so, "_library._monument", BuildingDir + "/Bld_Monument.prefab");

            SetMaterial(so, "_library._ownerPlayerMaterial", MaterialDir + "/M_OwnerPlayer.mat");
            SetMaterial(so, "_library._ownerAiMaterial", MaterialDir + "/M_OwnerAi.mat");
            SetMaterial(so, "_library._highlightMaterial", MaterialDir + "/M_Highlight.mat");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireInput(HexworldInputController input, Camera camera, HexBoardView boardView)
        {
            var so = new SerializedObject(input);
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_boardView").objectReferenceValue = boardView;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRunner(
            HexworldGameRunner runner,
            HexBoardView boardView,
            HexworldInputController input,
            HexworldAiTurnDriver aiDriver,
            HexworldCameraRig rig)
        {
            var so = new SerializedObject(runner);
            so.FindProperty("_boardView").objectReferenceValue = boardView;
            so.FindProperty("_inputController").objectReferenceValue = input;
            so.FindProperty("_aiDriver").objectReferenceValue = aiDriver;
            so.FindProperty("_cameraRig").objectReferenceValue = rig;
            so.FindProperty("_seed").intValue = 12345;
            so.FindProperty("_useRandomSeed").boolValue = true;
            so.FindProperty("_startOnPlay").boolValue = true;
            so.FindProperty("_aiPhaseDelay").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrefab(SerializedObject so, string path, string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                throw new Exception("Префаб не найден: " + assetPath);
            so.FindProperty(path).objectReferenceValue = asset;
        }

        static void SetMaterial(SerializedObject so, string path, string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (asset == null)
                throw new Exception("Материал не найден: " + assetPath);
            so.FindProperty(path).objectReferenceValue = asset;
        }

        /// <summary>Оставляет в Build Settings одну сцену — Hexworld под индексом 0.</summary>
        static void RegisterInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }

        /// <summary>Удаляет старую SampleScene вместе с .meta.</summary>
        static void RemoveOldScene()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OldScenePath) == null)
                return;

            if (AssetDatabase.DeleteAsset(OldScenePath))
                Debug.Log("[OstrovSceneSetup] Удалена старая сцена: " + OldScenePath);
            else
                Debug.LogWarning("[OstrovSceneSetup] Не удалось удалить " + OldScenePath);
        }
    }
}
