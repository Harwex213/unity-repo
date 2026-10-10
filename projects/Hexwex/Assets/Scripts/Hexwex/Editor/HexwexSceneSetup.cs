using System.IO;
using Hexwex.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Hexwex.EditorTools
{
    /// <summary>
    /// Builds the island scene from scratch: the camera, the light, the island
    /// view, the globe, the battle level, the HUD, the learn guide, the main menu and the game root, with every reference wired. The scene is
    /// generated, not hand-edited, so running the menu item again is always safe
    /// and gives the same scene.
    /// </summary>
    public static class HexwexSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Island.unity";
        private const string MaterialPath = "Assets/Art/Materials/M_Surface.mat";
        private const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";
        private const string ThemePath = "Assets/UI/HudTheme.tss";
        private const string StyleSheetPath = "Assets/UI/Hud.uss";
        private const string MenuStyleSheetPath = "Assets/UI/Menu.uss";
        private const string GuideStyleSheetPath = "Assets/UI/Guide.uss";

        [MenuItem("Hexwex/Rebuild Island Scene")]
        public static void RebuildIslandScene()
        {
            // The style sheets may have been written a moment ago, outside the Editor.
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded after the new scene: opening a scene unloads assets nothing refers to.
            Material surface = EnsureSurfaceMaterial();
            PanelSettings panelSettings = EnsurePanelSettings();
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet == null)
            {
                throw new FileNotFoundException("The HUD style sheet did not load", StyleSheetPath);
            }

            StyleSheet menuStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(MenuStyleSheetPath);
            if (menuStyleSheet == null)
            {
                throw new FileNotFoundException("The menu style sheet did not load", MenuStyleSheetPath);
            }

            GameObject lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.28f);

            GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.47f, 0.66f, 0.86f);
            worldCamera.fieldOfView = 40f;
            worldCamera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            CameraRig rig = cameraObject.AddComponent<CameraRig>();

            GameObject islandObject = new GameObject("Island");
            IslandView islandView = islandObject.AddComponent<IslandView>();
            islandView.SurfaceMaterial = surface;

            GameObject globeObject = new GameObject("Globe");
            GlobeView globeView = globeObject.AddComponent<GlobeView>();
            globeView.SurfaceMaterial = surface;

            GameObject battleObject = new GameObject("Battle");
            BattleView battleView = battleObject.AddComponent<BattleView>();
            battleView.SurfaceMaterial = surface;

            GameObject hudObject = new GameObject("HUD");
            UIDocument document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            Hud hud = hudObject.AddComponent<Hud>();
            Assign(hud, "document", document);
            Assign(hud, "styleSheet", styleSheet);

            GameObject gameObject = new GameObject("Game");
            GameRoot game = gameObject.AddComponent<GameRoot>();
            Assign(game, "islandView", islandView);
            Assign(game, "globeView", globeView);
            Assign(game, "battleView", battleView);
            Assign(game, "hud", hud);
            Assign(game, "worldCamera", worldCamera);
            Assign(game, "cameraRig", rig);
            Assign(game, "sun", sun);

            GameObject menuObject = new GameObject("Menu");
            UIDocument menuDocument = menuObject.AddComponent<UIDocument>();
            menuDocument.panelSettings = panelSettings;
            // Above the HUD: the menu covers the whole game.
            menuDocument.sortingOrder = 10;
            MainMenu menu = menuObject.AddComponent<MainMenu>();
            Assign(menu, "document", menuDocument);
            Assign(menu, "hudStyleSheet", styleSheet);
            Assign(menu, "menuStyleSheet", menuStyleSheet);
            Assign(game, "mainMenu", menu);

            GameObject guideObject = new GameObject("Guide");
            UIDocument guideDocument = guideObject.AddComponent<UIDocument>();
            guideDocument.panelSettings = panelSettings;
            // Above the HUD and under the menu.
            guideDocument.sortingOrder = 5;
            LearnGuide guide = guideObject.AddComponent<LearnGuide>();
            Assign(guide, "document", guideDocument);
            Assign(guide, "hudStyleSheet", styleSheet);
            Assign(guide, "guideStyleSheet", LoadStyleSheet(GuideStyleSheetPath));
            Assign(game, "learnGuide", guide);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Hexwex: island scene rebuilt at " + ScenePath);
        }

        private static StyleSheet LoadStyleSheet(string path)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet == null)
            {
                throw new FileNotFoundException("The style sheet did not load", path);
            }

            return sheet;
        }

        /// <summary>One lit material for everything; each renderer takes its colour from a property block.</summary>
        private static Material EnsureSurfaceMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Surface" };
            material.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(material, MaterialPath);

            return material;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath));
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);

            return settings;
        }

        private static void Assign(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
