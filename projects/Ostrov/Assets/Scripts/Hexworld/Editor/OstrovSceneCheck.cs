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
    /// Дымовая проверка сцены Hexworld: открывает сцену, запускает партию,
    /// прогоняет несколько ходов обоими игроками и рендерит PNG.
    /// Запуск: меню Ostrov/Scene или
    /// -executeMethod Ostrov.HexworldEditor.OstrovSceneCheck.RunAll
    /// Каталог для PNG берётся из переменной окружения OSTROV_SHOT_DIR.
    /// </summary>
    public static class OstrovSceneCheck
    {
        const int Width = 1600;
        const int Height = 1000;
        const int Seed = 20240915;
        const int MaxTurns = 24;

        [MenuItem("Ostrov/Scene/Check Hexworld Scene")]
        public static void RunAll()
        {
            try
            {
                Run();
                Debug.Log("[OstrovSceneCheck] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovSceneCheck] FAILED: " + e);
                throw;
            }
        }

        static void Run()
        {
            var outDir = Environment.GetEnvironmentVariable("OSTROV_SHOT_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath("Temp/ostrov-shots");
            Directory.CreateDirectory(outDir);

            EditorSceneManager.OpenScene(OstrovSceneSetup.ScenePath, OpenSceneMode.Single);

            var runner = UnityEngine.Object.FindAnyObjectByType<HexworldGameRunner>();
            if (runner == null)
                throw new Exception("На сцене нет HexworldGameRunner");
            if (runner.BoardView == null)
                throw new Exception("У HexworldGameRunner не назначен HexBoardView");
            if (runner.CameraRig == null)
                throw new Exception("У HexworldGameRunner не назначен HexworldCameraRig");

            runner.StartNewGame(Seed);

            var game = runner.Game;
            if (game == null)
                throw new Exception("Партия не создана");
            if (!game.IsStarted)
                throw new Exception("Партия не запущена");

            int tileViews = runner.BoardView.TileViewCount;
            if (tileViews != game.Board.TileCount)
                throw new Exception($"Тайлов на сцене {tileViews}, а на доске {game.Board.TileCount}");
            if (tileViews != 37)
                throw new Exception("Ожидалось 37 тайлов, получено " + tileViews);

            int castles = CountBuildingViews(runner);
            if (castles != 2)
                throw new Exception("Ожидалось 2 замка в начале партии, получено " + castles);

            int ownedRims = CountOwnedTileViews(runner);
            int ownedTiles = game.Board.GetOwnedTiles(0).Count + game.Board.GetOwnedTiles(1).Count;
            if (ownedRims != ownedTiles)
                throw new Exception($"Ободков владения {ownedRims}, а своих тайлов на доске {ownedTiles}");
            if (ownedRims < 2)
                throw new Exception("Ободков владения слишком мало: " + ownedRims);

            var camera = runner.CameraRig.Camera;
            if (camera == null)
                throw new Exception("У рига нет камеры");

            runner.CameraRig.ApplyImmediate();
            Capture(camera, Path.Combine(outDir, "01_start.png"));

            // Подсветка наведения и выбора — проверяем, что жёлтый ободок виден.
            HexTileView hovered = runner.BoardView.GetTileView(new HexCoord(0, 0));
            HexTileView selected = runner.BoardView.GetTileView(new HexCoord(-1, 0));
            if (hovered != null)
                hovered.SetHovered(true);
            if (selected != null)
                selected.SetSelected(true);
            runner.BoardView.SetValidTargets(new[] { new HexCoord(1, -1), new HexCoord(0, 1) });
            Capture(camera, Path.Combine(outDir, "02_highlight.png"));
            if (hovered != null)
                hovered.SetHovered(false);
            if (selected != null)
                selected.SetSelected(false);
            runner.BoardView.ClearValidTargets();

            // Обоими игроками играет ИИ: так проверяется, что вью переживает
            // стройку, бой, катастрофы и уход тайлов в Эфир.
            var players = new[]
            {
                new HexworldAiPlayer(game, 0),
                new HexworldAiPlayer(game, 1),
            };

            int turns = 0;
            int aiTurns = 0;
            while (!game.IsGameOver && turns < MaxTurns)
            {
                int current = game.CurrentPlayerIndex;
                HexworldPhase before = game.CurrentPhase;
                players[current].PlayTurn();

                if (current == 1)
                    aiTurns++;

                if (!game.IsGameOver && game.CurrentPlayerIndex == current && game.CurrentPhase == before)
                    throw new Exception("Ход игрока " + current + " не сдвинул фазу");

                turns++;

                if (turns == 6)
                {
                    runner.CameraRig.ApplyImmediate();
                    Capture(camera, Path.Combine(outDir, "03_turn6.png"));
                }
            }

            if (aiTurns < 3)
                throw new Exception("ИИ сыграл слишком мало ходов: " + aiTurns);

            int buildings = CountBuildingViews(runner);
            int owned = CountOwnedTileViews(runner);
            if (buildings < 4)
                throw new Exception("После " + turns + " ходов на доске всего " + buildings + " зданий");

            runner.CameraRig.ApplyImmediate();
            Capture(camera, Path.Combine(outDir, "04_late.png"));

            // Тот же вид сбоку: видно, что остров парит и здания стоят на тайлах.
            Capture(camera, Path.Combine(outDir, "05_close.png"), 30f, 9f);

            VerifyViewMatchesBoard(runner);

            Debug.Log(string.Format(
                "[OstrovSceneCheck] seed={0} тайлов={1} ходов={2} ходов ИИ={3} зданий={4} своих тайлов={5} " +
                "gameOver={6} winner={7} reason={8} PNG={9}",
                Seed, tileViews, turns, aiTurns, buildings, owned,
                game.IsGameOver, game.Winner, game.WinReason, outDir));
        }

        /// <summary>Сверяет, что каждый тайл на экране показывает то же, что тайл в модели.</summary>
        static void VerifyViewMatchesBoard(HexworldGameRunner runner)
        {
            HexworldGame game = runner.Game;
            var problems = new List<string>();

            foreach (HexTile tile in game.Board.Tiles)
            {
                HexTileView view = runner.BoardView.GetTileView(tile.Coord);
                if (view == null)
                {
                    problems.Add("нет вью для " + tile.Coord);
                    continue;
                }

                if (tile.IsVoided)
                {
                    if (!view.IsVoided)
                        problems.Add(tile.Coord + ": тайл ушёл в Эфир, а вью нет");
                    continue;
                }

                if (view.Terrain != tile.Terrain)
                    problems.Add($"{tile.Coord}: местность {view.Terrain} вместо {tile.Terrain}");

                if (view.Owner != tile.Owner)
                    problems.Add($"{tile.Coord}: владелец {view.Owner} вместо {tile.Owner}");

                HexBuildingType expected = tile.HasBuilding ? tile.Building.Type : HexBuildingType.None;
                if (view.BuildingType != expected)
                    problems.Add($"{tile.Coord}: здание {view.BuildingType} вместо {expected}");
            }

            if (problems.Count > 0)
                throw new Exception("Вью разошлось с моделью: " + string.Join("; ", problems));
        }

        static int CountBuildingViews(HexworldGameRunner runner)
        {
            int count = 0;
            foreach (HexTileView view in runner.BoardView.TileViews)
            {
                if (view.BuildingType != HexBuildingType.None)
                    count++;
            }

            return count;
        }

        static int CountOwnedTileViews(HexworldGameRunner runner)
        {
            int count = 0;
            foreach (HexTileView view in runner.BoardView.TileViews)
            {
                if (view.Owner != HexworldConfig.NeutralOwner && !view.IsVoided)
                    count++;
            }

            return count;
        }

        static void Capture(Camera camera, string path)
        {
            Capture(camera, path, float.NaN, float.NaN);
        }

        /// <summary>Рендерит кадр камерой сцены, при желании под другим углом и с другого расстояния.</summary>
        static void Capture(Camera camera, string path, float pitch, float distance)
        {
            Vector3 savedPosition = camera.transform.position;
            Quaternion savedRotation = camera.transform.rotation;

            if (!float.IsNaN(pitch))
            {
                Vector3 pivot = camera.transform.parent == null
                    ? Vector3.zero
                    : camera.transform.parent.position;
                Quaternion rotation = Quaternion.Euler(pitch, 30f, 0f);
                camera.transform.SetPositionAndRotation(
                    pivot - (rotation * Vector3.forward * distance), rotation);
            }

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            rt.Create();

            float savedAspect = camera.aspect;
            camera.aspect = (float)Width / Height;
            camera.targetTexture = rt;

            // Прогревочный кадр: экземпляр SRP создаётся лениво при первом рендере.
            camera.Render();

            bool submitted = false;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    submitted = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OstrovSceneCheck] SubmitRenderRequest не сработал: " + e.Message);
            }

            if (!submitted)
                camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            camera.aspect = savedAspect;
            rt.Release();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);

            Debug.Log("[OstrovSceneCheck] PNG: " + path);
        }
    }
}
