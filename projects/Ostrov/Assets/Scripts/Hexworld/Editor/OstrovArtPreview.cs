using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ostrov.HexworldEditor
{
    /// <summary>
    /// Временная проверка арта: раскладывает все префабы в ряд и рендерит PNG.
    /// Сцена создаётся в памяти и не сохраняется на диск.
    /// Путь для PNG берётся из переменной окружения OSTROV_PREVIEW_PNG.
    /// </summary>
    public static class OstrovArtPreview
    {
        static readonly string[] TilePrefabs =
        {
            "Assets/Prefabs/Tiles/Tile_Grass.prefab",
            "Assets/Prefabs/Tiles/Tile_Forest.prefab",
            "Assets/Prefabs/Tiles/Tile_Stone.prefab",
            "Assets/Prefabs/Tiles/Tile_Rubble.prefab",
        };

        static readonly string[] BuildingPrefabs =
        {
            "Assets/Prefabs/Buildings/Bld_Cottage.prefab",
            "Assets/Prefabs/Buildings/Bld_Farm.prefab",
            "Assets/Prefabs/Buildings/Bld_Quarry.prefab",
            "Assets/Prefabs/Buildings/Bld_Church.prefab",
            "Assets/Prefabs/Buildings/Bld_Barracks.prefab",
            "Assets/Prefabs/Buildings/Bld_Castle.prefab",
            "Assets/Prefabs/Buildings/Bld_Monument.prefab",
        };

        [MenuItem("Ostrov/Art/Render Preview")]
        public static void RenderPreview()
        {
            try
            {
                var outPath = Environment.GetEnvironmentVariable("OSTROV_PREVIEW_PNG");
                if (string.IsNullOrEmpty(outPath))
                    outPath = Path.GetFullPath("Temp/ostrov_art_preview.png");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.46f, 0.48f, 0.54f);
                RenderSettings.skybox = null;

                var lightGo = new GameObject("Sun");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.color = new Color(1f, 0.97f, 0.9f);
                // Свет спереди-сверху-справа, чтобы передние стены были освещены.
                lightGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

                const float step = 2.0f;
                var spawned = new List<GameObject>();

                // Ряд 1: тайлы.
                for (int i = 0; i < TilePrefabs.Length; i++)
                    spawned.Add(Spawn(TilePrefabs[i], new Vector3(i * step, 0f, 0f)));

                // Ряд 2: здания, каждое стоит на травяном тайле (проверка пивотов).
                for (int i = 0; i < BuildingPrefabs.Length; i++)
                {
                    var pos = new Vector3(i * step, 0f, 3.2f);
                    spawned.Add(Spawn(TilePrefabs[0], pos));
                    spawned.Add(Spawn(BuildingPrefabs[i], pos));
                }

                float maxX = Mathf.Max((TilePrefabs.Length - 1) * step, (BuildingPrefabs.Length - 1) * step);
                var center = new Vector3(maxX * 0.5f, 0f, 1.6f);

                var camGo = new GameObject("PreviewCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 3.6f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.18f, 0.22f, 0.28f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                // Низкий угол обзора: видны и стены, и верх тайлов; ловятся вывернутые нормали.
                camGo.transform.position = center + new Vector3(0f, 6.5f, -11f);
                camGo.transform.LookAt(center);

                const int width = 1800;
                const int height = 760;
                cam.aspect = (float)width / height;

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                rt.antiAliasing = 4;
                rt.Create();

                cam.targetTexture = rt;

                // Прогревочный кадр: экземпляр SRP создаётся лениво при первом рендере.
                // Без него SubmitRenderRequest не поддерживается и картинка выходит без цветов URP.
                cam.Render();

                bool submitted = false;
                try
                {
                    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                    if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                    {
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
                        submitted = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OstrovArtPreview] SubmitRenderRequest не сработал: " + e.Message);
                }

                if (!submitted)
                    Debug.LogWarning("[OstrovArtPreview] SubmitRenderRequest недоступен, рендер через Camera.Render — " +
                                     "цвета URP-материалов в PNG могут быть неверными.");

                if (!submitted)
                    cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                cam.targetTexture = null;
                rt.Release();

                var dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(outPath, tex.EncodeToPNG());

                Debug.Log($"[OstrovArtPreview] Объектов в сцене: {spawned.Count}. PNG: {outPath}");

                // Убираем временную сцену — на диск она не попадала.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.Log("[OstrovArtPreview] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovArtPreview] FAILED: " + e);
                throw;
            }
        }

        static GameObject Spawn(string prefabPath, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new Exception("Префаб не найден: " + prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position;

            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                        Debug.LogError($"[OstrovArtPreview] {prefabPath}: пустой материал на {r.name}");
                }
            }

            return go;
        }
    }
}
