using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ostrov.HexworldEditor
{
    /// <summary>
    /// Дымовая проверка интерфейса: открывает сцену, играет партию через методы
    /// панелей — те же, что висят на кнопках, — и снимает PNG каждой фазы и
    /// экрана победы. Запуск: меню Ostrov/Scene или
    /// -executeMethod Ostrov.HexworldEditor.OstrovUiCheck.RunAll
    /// Каталог для PNG берётся из переменной окружения OSTROV_SHOT_DIR.
    /// </summary>
    public static class OstrovUiCheck
    {
        const int Seed = 777001;
        const int MaxTurns = 60;

        [MenuItem("Ostrov/Scene/Check Hexworld UI")]
        public static void RunAll()
        {
            try
            {
                Run();
                Debug.Log("[OstrovUiCheck] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovUiCheck] FAILED: " + e);
                throw;
            }
        }

        static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("OSTROV_SHOT_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath("Temp/ostrov-ui-shots");
            Directory.CreateDirectory(outDir);

            EditorSceneManager.OpenScene(OstrovSceneSetup.ScenePath, OpenSceneMode.Single);

            var runner = UnityEngine.Object.FindAnyObjectByType<HexworldGameRunner>();
            if (runner == null)
                throw new Exception("На сцене нет HexworldGameRunner");

            var ui = UnityEngine.Object.FindAnyObjectByType<HexworldUiRoot>();
            if (ui == null)
                throw new Exception("На сцене нет HexworldUiRoot — соберите UI");

            var canvas = ui.GetComponent<Canvas>();
            if (canvas == null)
                throw new Exception("У HexworldUiRoot нет Canvas");

            ui.Initialize();
            runner.StartNewGame(Seed);

            HexworldGame game = runner.Game;
            if (game == null || !game.IsStarted)
                throw new Exception("Партия не запущена");

            Camera camera = runner.CameraRig.Camera;
            runner.CameraRig.ApplyImmediate();

            RequirePanels(ui, HexworldPhase.Harvest);

            // --- Урожай: помечаем кубик, перебрасываем, подтверждаем ---
            Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "01_harvest.png"));

            int marked = MarkFirstRerollableDie(ui);
            if (marked < 0)
                Debug.LogWarning("[OstrovUiCheck] Нет кубика, который можно перебросить");
            else
            {
                if (!ui.DicePanel.IsDieMarked(marked))
                    throw new Exception("Кубик " + marked + " не пометился");

                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "02_harvest_marked.png"));

                int before = game.RerollsRemaining;
                ui.DicePanel.Reroll();
                if (game.RerollsRemaining != before - 1)
                    throw new Exception("Реролл не потратил попытку");
            }

            // Кубик с черепом кликать нельзя.
            int skull = FindSkullDie(game);
            if (skull >= 0)
            {
                ui.DicePanel.ToggleDie(skull);
                if (ui.DicePanel.IsDieMarked(skull))
                    throw new Exception("Кубик с черепом пометился, а не должен");
            }

            ui.DicePanel.Confirm();
            if (game.CurrentPhase != HexworldPhase.Build)
                throw new Exception("Подтверждение урожая не открыло фазу строительства");

            // --- Строительство: выбираем здание и кликаем по тайлу ---
            RequirePanels(ui, HexworldPhase.Build);

            HexBuildingType picked = PickAffordableBuilding(ui);
            if (picked == HexBuildingType.None)
                Debug.LogWarning("[OstrovUiCheck] На первом ходу строить нечего");
            else
            {
                ui.BuildPanel.SelectBuilding(picked);
                if (ui.BuildPanel.SelectedBuilding != picked)
                    throw new Exception("Здание " + picked + " не выбралось");

                IReadOnlyList<HexCoord> targets = runner.InputController.ValidTargets;
                if (targets.Count == 0)
                    throw new Exception("Выбор здания не подсветил ни одного тайла");

                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "03_build.png"));

                HexCoord where = targets[0];
                int builtBefore = game.Board.CountBuildings(0, picked);
                ui.HandleTileClicked(where);
                if (game.Board.CountBuildings(0, picked) != builtBefore + 1)
                    throw new Exception("Клик по тайлу " + where + " не построил " + picked);
            }

            if (ui.BuildPanel.SelectedBuilding == HexBuildingType.None)
                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "03_build.png"));

            ui.BuildPanel.EndBuild();
            if (game.CurrentPhase != HexworldPhase.Combat)
                throw new Exception("Кнопка «Закончить строительство» не открыла фазу боя");

            // --- Бой: выбираем цель, двигаем солдат, атакуем ---
            RequirePanels(ui, HexworldPhase.Combat);

            IReadOnlyList<HexCoord> attackTargets = runner.InputController.ValidTargets;
            if (attackTargets.Count > 0)
            {
                ui.HandleTileClicked(attackTargets[0]);
                if (!ui.CombatPanel.Target.HasValue)
                    throw new Exception("Клик по тайлу не выбрал цель атаки");

                ui.CombatPanel.AddSoldiers(1);
                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "04_combat.png"));

                ui.CombatPanel.Attack();
                if (!game.CurrentPlayer.HasCapturedThisTurn)
                    throw new Exception("Кнопка «Атаковать» не провела бой");

                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "05_combat_result.png"));
            }
            else
            {
                Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "04_combat.png"));
                Debug.LogWarning("[OstrovUiCheck] На первом ходу атаковать некого");
            }

            // Запрещённые действия через панель должны молча ничего не делать.
            ui.CombatPanel.Attack();
            ui.DicePanel.Confirm();
            ui.BuildPanel.EndBuild();

            ui.CombatPanel.EndTurn();
            if (game.CurrentPlayerIndex != HexworldGameRunner.AiPlayerIndex)
                throw new Exception("Кнопка «Закончить ход» не передала ход противнику");

            // --- Партия до конца: ИИ ходит сам, человек — через панели ---
            var ai = new HexworldAiPlayer(game, HexworldGameRunner.AiPlayerIndex);
            int turns = 1;
            bool aiShot = false;
            bool manyDiceShot = false;

            while (!game.IsGameOver && turns < MaxTurns)
            {
                if (game.CurrentPlayerIndex == HexworldGameRunner.AiPlayerIndex)
                {
                    if (!aiShot)
                    {
                        // Ход ИИ: панели гаснут, в шапке загорается значок.
                        ui.RefreshAll();
                        runner.CameraRig.ApplyImmediate();
                        Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "08_ai_turn.png"));
                        aiShot = true;
                    }

                    ai.PlayTurn();
                }
                else
                {
                    if (!manyDiceShot
                        && game.CurrentPhase == HexworldPhase.Harvest
                        && game.Dice.Count >= 4)
                    {
                        ui.RefreshAll();
                        runner.CameraRig.ApplyImmediate();
                        Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "09_harvest_many.png"));
                        manyDiceShot = true;
                    }

                    PlayHumanTurnThroughUi(ui, runner, game);
                }

                turns++;
            }

            if (!game.IsGameOver)
                throw new Exception("Партия не кончилась за " + MaxTurns + " ходов");

            if (!ui.Victory.IsVisible)
                throw new Exception("Экран победы не появился");

            Shot(camera, canvas, 1920, 1080, Path.Combine(outDir, "06_victory.png"));

            // Рестарт: экран победы должен уйти, а партия начаться заново.
            ui.Victory.Restart();
            if (ui.Victory.IsVisible)
                throw new Exception("Экран победы не спрятался после рестарта");
            if (runner.Game == null || runner.Game.IsGameOver)
                throw new Exception("Рестарт не открыл новую партию");

            runner.CameraRig.ApplyImmediate();
            Shot(camera, canvas, 1280, 720, Path.Combine(outDir, "07_harvest_1280x720.png"));

            Debug.Log(string.Format(
                "[OstrovUiCheck] seed={0} ходов={1} победитель={2} причина={3} строк лога={4} PNG={5}",
                Seed, turns, game.Winner, game.WinReason, ui.Log.LineCount, outDir));
        }

        /// <summary>Играет ход человека, дёргая только методы панелей.</summary>
        static void PlayHumanTurnThroughUi(HexworldUiRoot ui, HexworldGameRunner runner, HexworldGame game)
        {
            int guard = 0;
            while (!game.IsGameOver
                   && game.CurrentPlayerIndex == HexworldGameRunner.HumanPlayerIndex
                   && guard++ < 8)
            {
                switch (game.CurrentPhase)
                {
                    case HexworldPhase.Harvest:
                        MarkFirstRerollableDie(ui);
                        ui.DicePanel.Reroll();
                        ui.DicePanel.Confirm();
                        break;

                    case HexworldPhase.Build:
                        BuildAsMuchAsPossible(ui, runner, game);
                        ui.BuildPanel.EndBuild();
                        break;

                    case HexworldPhase.Combat:
                        AttackIfPossible(ui, runner);
                        ui.CombatPanel.EndTurn();
                        break;

                    default:
                        return;
                }
            }
        }

        static void BuildAsMuchAsPossible(HexworldUiRoot ui, HexworldGameRunner runner, HexworldGame game)
        {
            for (int step = 0; step < 4; step++)
            {
                HexBuildingType picked = PickAffordableBuilding(ui);
                if (picked == HexBuildingType.None)
                    return;

                ui.BuildPanel.SelectBuilding(picked);
                IReadOnlyList<HexCoord> targets = runner.InputController.ValidTargets;
                if (targets.Count == 0)
                    return;

                ui.HandleTileClicked(targets[0]);
                ui.BuildPanel.ClearSelection();
            }
        }

        static void AttackIfPossible(HexworldUiRoot ui, HexworldGameRunner runner)
        {
            IReadOnlyList<HexCoord> targets = runner.InputController.ValidTargets;
            if (targets.Count == 0)
                return;

            ui.HandleTileClicked(targets[0]);
            ui.CombatPanel.SetSoldiers(99);
            ui.CombatPanel.Attack();
        }

        /// <summary>Ставит метку реролла на первый подходящий кубик и возвращает его номер.</summary>
        static int MarkFirstRerollableDie(HexworldUiRoot ui)
        {
            HexworldGame game = ui.Game;
            if (game == null)
                return -1;

            for (int i = 0; i < game.Dice.Count; i++)
            {
                if (!game.Dice[i].CanReroll)
                    continue;

                ui.DicePanel.ToggleDie(i);
                return i;
            }

            return -1;
        }

        static int FindSkullDie(HexworldGame game)
        {
            for (int i = 0; i < game.Dice.Count; i++)
            {
                if (game.Dice[i].IsSkull)
                    return i;
            }

            return -1;
        }

        /// <summary>Берёт первое здание, которое панель считает доступным.</summary>
        static HexBuildingType PickAffordableBuilding(HexworldUiRoot ui)
        {
            HexBuildingType[] order =
            {
                HexBuildingType.Monument,
                HexBuildingType.Church,
                HexBuildingType.Cottage,
                HexBuildingType.Barracks,
                HexBuildingType.Farm,
                HexBuildingType.Quarry,
            };

            for (int i = 0; i < order.Length; i++)
            {
                string reason;
                if (ui.BuildPanel.IsBuildingAvailable(order[i], out reason))
                    return order[i];
            }

            return HexBuildingType.None;
        }

        /// <summary>Проверяет, что на экране видна панель именно текущей фазы.</summary>
        static void RequirePanels(HexworldUiRoot ui, HexworldPhase phase)
        {
            bool dice = ui.DicePanel.gameObject.activeSelf;
            bool build = ui.BuildPanel.gameObject.activeSelf;
            bool combat = ui.CombatPanel.gameObject.activeSelf;

            bool ok = phase == HexworldPhase.Harvest ? dice && !build && !combat
                : phase == HexworldPhase.Build ? !dice && build && !combat
                : !dice && !build && combat;

            if (!ok)
                throw new Exception(string.Format(
                    "Фаза {0}, а видны панели: кубики={1} стройка={2} бой={3}", phase, dice, build, combat));
        }

        // ------------------------------------------------------------------
        // Снимок экрана вместе с интерфейсом
        // ------------------------------------------------------------------

        /// <summary>Эталонный экран, под который свёрстан интерфейс.</summary>
        static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        /// <summary>
        /// Рендерит кадр в PNG вместе с интерфейсом. В batch-режиме размер
        /// экрана редактора равен 640x480, поэтому Canvas на время съёмки
        /// переводится в World Space и растягивается ровно на 1920x1080 единиц —
        /// столько же даёт CanvasScaler на любом экране 16:9. Кадр 1280x720 —
        /// это та же вёрстка, отрисованная с меньшей плотностью пикселей.
        /// </summary>
        static void Shot(Camera camera, Canvas canvas, int width, int height, string path)
        {
            var rect = (RectTransform)canvas.transform;
            var scaler = canvas.GetComponent<CanvasScaler>();

            RenderMode savedMode = canvas.renderMode;
            Camera savedCamera = canvas.worldCamera;
            float savedAspect = camera.aspect;
            bool savedScaler = scaler != null && scaler.enabled;
            Vector3 savedPosition = rect.position;
            Quaternion savedRotation = rect.rotation;
            Vector3 savedScale = rect.localScale;

            if (scaler != null)
                scaler.enabled = false;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            camera.aspect = (float)width / height;

            const float distance = 1f;
            float frustumHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = frustumHeight / Reference.y;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Reference;
            rect.localScale = Vector3.one * scale;
            rect.rotation = camera.transform.rotation;
            rect.position = camera.transform.position + (camera.transform.forward * distance);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 2;
            rt.Create();
            camera.targetTexture = rt;

            RebuildUi(canvas);

            // Прогревочный кадр: SRP и атлас динамического шрифта создаются лениво.
            camera.Render();
            RebuildUi(canvas);
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            camera.aspect = savedAspect;
            rt.Release();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            canvas.renderMode = savedMode;
            canvas.worldCamera = savedCamera;
            rect.position = savedPosition;
            rect.rotation = savedRotation;
            rect.localScale = savedScale;

            if (scaler != null)
                scaler.enabled = savedScaler;

            Debug.Log(string.Format(
                "[OstrovUiCheck] PNG {0} ({1}x{2}, canvas {3}x{4})",
                path, width, height, rect.rect.width, rect.rect.height));
        }

        /// <summary>Прогоняет разметку и перерисовку интерфейса без кадра игры.</summary>
        static void RebuildUi(Canvas canvas)
        {
            var rect = (RectTransform)canvas.transform;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true, true);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }
}
