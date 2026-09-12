using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Ostrov.HexworldEditor
{
    /// <summary>
    /// Собирает игровой Canvas сцены Assets/Scenes/Hexworld.unity кодом:
    /// панель ресурсов, шапку хода, панели фаз, лог событий и экран победы.
    /// Запуск: меню Ostrov/Scene/Build Hexworld UI или
    /// -executeMethod Ostrov.HexworldEditor.OstrovUiSetup.RunAll
    /// Скрипт идемпотентен: старый объект UI сносится и собирается заново.
    /// </summary>
    public static class OstrovUiSetup
    {
        /// <summary>Имя корневого объекта интерфейса на сцене.</summary>
        public const string UiRootName = "UI";

        /// <summary>Куда кладётся шрифт интерфейса.</summary>
        const string FontDir = "Assets/Art/Fonts";

        /// <summary>Ассет шрифта интерфейса.</summary>
        const string FontAssetPath = FontDir + "/Ostrov_UI_SDF.asset";

        /// <summary>Исходные шрифты, из которых собирается ассет. Нужна кириллица.</summary>
        static readonly string[] SourceFonts =
        {
            "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
        };

        // --- Размеры макета, отсчитанные от эталонного экрана 1920x1080 ---

        const float PanelWidth = 500f;
        const float LogWidth = 440f;
        const float LogHeight = 300f;
        const float Margin = 24f;

        /// <summary>Шрифт, найденный на время сборки.</summary>
        static TMP_FontAsset _font;

        [MenuItem("Ostrov/Scene/Import TMP Resources")]
        public static void ImportTmpResources()
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(SourceFonts[0]) != null)
            {
                Debug.Log("[OstrovUiSetup] TMP Essential Resources уже на месте");
                return;
            }

            string packagePath = FindTmpEssentialsPackage();
            if (packagePath == null)
                throw new Exception("Не найден TMP Essential Resources.unitypackage в пакете com.unity.ugui");

            // AssetDatabase.ImportPackage откладывает работу на следующий тик
            // редактора, а в batch-режиме его не будет. Внутренний
            // ImportPackageImmediately распаковывает пакет сразу.
            MethodInfo immediate = FindImportPackageImmediately();
            if (immediate != null)
                immediate.Invoke(null, new object[] { packagePath });
            else
                AssetDatabase.ImportPackage(packagePath, false);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[OstrovUiSetup] TMP Essential Resources импортированы из " + packagePath);
        }

        /// <summary>Ищет .unitypackage со шрифтами TMP внутри пакета ugui.</summary>
        static string FindTmpEssentialsPackage()
        {
            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
            if (info == null || string.IsNullOrEmpty(info.resolvedPath))
                return null;

            string path = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            return File.Exists(path) ? path : null;
        }

        /// <summary>Находит внутренний синхронный распаковщик .unitypackage.</summary>
        static MethodInfo FindImportPackageImmediately()
        {
            foreach (Type type in typeof(AssetDatabase).Assembly.GetTypes())
            {
                MethodInfo method = type.GetMethod(
                    "ImportPackageImmediately",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

                if (method != null)
                    return method;
            }

            return null;
        }

        [MenuItem("Ostrov/Scene/Build Hexworld UI")]
        public static void RunAll()
        {
            try
            {
                BuildUi();
                Debug.Log("[OstrovUiSetup] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovUiSetup] FAILED: " + e);
                throw;
            }
        }

        /// <summary>Открывает сцену, пересобирает Canvas и сохраняет сцену.</summary>
        public static void BuildUi()
        {
            _font = EnsureFont();

            var scene = EditorSceneManager.OpenScene(OstrovSceneSetup.ScenePath, OpenSceneMode.Single);

            var runner = UnityEngine.Object.FindAnyObjectByType<HexworldGameRunner>();
            if (runner == null)
                throw new Exception("На сцене нет HexworldGameRunner");

            // Идемпотентность: сносим предыдущий интерфейс целиком.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == UiRootName)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject canvasGo = BuildCanvas();
            var uiRoot = canvasGo.AddComponent<HexworldUiRoot>();
            Transform parent = canvasGo.transform;

            HexworldResourceBar resourceBar = BuildResourceBar(parent);
            TMP_Text aiText = BuildAiBar(parent);
            HexworldTurnHeader header = BuildTurnHeader(parent);
            HexworldDicePanel dicePanel = BuildDicePanel(parent, uiRoot);
            HexworldBuildPanel buildPanel = BuildBuildPanel(parent, uiRoot);
            HexworldCombatPanel combatPanel = BuildCombatPanel(parent, uiRoot);
            HexworldEventLog log = BuildEventLog(parent);
            BuildCameraHint(parent);
            HexworldVictoryScreen victory = BuildVictoryScreen(parent, uiRoot);

            Wire(resourceBar, "_opponentText", aiText);

            Wire(uiRoot,
                "_runner", runner,
                "_resourceBar", resourceBar,
                "_header", header,
                "_dicePanel", dicePanel,
                "_buildPanel", buildPanel,
                "_combatPanel", combatPanel,
                "_log", log,
                "_victory", victory);

            AttachCameraGuards(canvasGo, runner.CameraRig);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OstrovSceneSetup.ScenePath))
                throw new Exception("Не удалось сохранить сцену " + OstrovSceneSetup.ScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log("[OstrovUiSetup] UI собран в " + OstrovSceneSetup.ScenePath);
        }

        // ------------------------------------------------------------------
        // Canvas
        // ------------------------------------------------------------------

        static GameObject BuildCanvas()
        {
            var go = new GameObject(UiRootName, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ------------------------------------------------------------------
        // Панель ресурсов
        // ------------------------------------------------------------------

        static HexworldResourceBar BuildResourceBar(Transform parent)
        {
            GameObject panel = Panel("ResourceBar", parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(600f, 110f));

            GameObject chips = Child("Chips", panel.transform);
            Stretch(Rect(chips), 10f, 10f, 10f, 10f);

            var layout = chips.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            GameObject template = Panel("ChipTemplate", chips.transform, HexworldUiTheme.PanelSoftColor);
            var chip = template.AddComponent<HexworldResourceChip>();

            TMP_Text caption = Label("Caption", template.transform, "Еда", 14f, TextAlignmentOptions.Center,
                HexworldUiTheme.MutedColor);
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Band(caption.rectTransform, 5f, 18f, 2f);

            TMP_Text value = Label("Value", template.transform, "0", 30f, TextAlignmentOptions.Center,
                HexworldUiTheme.TextColor);
            value.fontStyle = FontStyles.Bold;
            Band(value.rectTransform, 23f, 38f, 2f);

            TMP_Text delta = Label("Delta", template.transform, string.Empty, 15f, TextAlignmentOptions.Center,
                HexworldUiTheme.GoodColor);
            Band(delta.rectTransform, 62f, 20f, 2f);

            Wire(chip,
                "_caption", caption,
                "_value", value,
                "_delta", delta,
                "_background", template.GetComponent<Image>());

            template.SetActive(false);

            var bar = panel.AddComponent<HexworldResourceBar>();
            Wire(bar, "_chipRoot", Rect(chips), "_chipTemplate", chip);
            return bar;
        }

        static TMP_Text BuildAiBar(Transform parent)
        {
            GameObject panel = Panel("AiBar", parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(600f, 58f));

            TMP_Text text = Label("Text", panel.transform, string.Empty, 18f, TextAlignmentOptions.Right,
                HexworldUiTheme.TextColor);
            Stretch(text.rectTransform, 16f, 16f, 6f, 6f);
            return text;
        }

        // ------------------------------------------------------------------
        // Шапка хода
        // ------------------------------------------------------------------

        static HexworldTurnHeader BuildTurnHeader(Transform parent)
        {
            GameObject panel = Panel("TurnHeader", parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(0.5f, 1f), new Vector2(0f, -Margin), new Vector2(620f, 146f));

            GameObject stripeGo = Panel("Stripe", panel.transform, HexworldUiTheme.PlayerColor);
            Stretch(Rect(stripeGo), new Vector2(0f, 1f), new Vector2(1f, 1f), 0f, 0f, 0f, 0f);
            Rect(stripeGo).sizeDelta = new Vector2(0f, 7f);

            TMP_Text turn = Label("TurnText", panel.transform, "Ход 1", 31f, TextAlignmentOptions.Center,
                HexworldUiTheme.TextColor);
            turn.fontStyle = FontStyles.Bold;
            Band(turn.rectTransform, 16f, 40f);

            TMP_Text phase = Label("PhaseText", panel.transform, "Фаза: Урожай", 21f, TextAlignmentOptions.Center,
                HexworldUiTheme.AccentColor);
            Band(phase.rectTransform, 56f, 28f);

            TMP_Text hint = Label("HintText", panel.transform, string.Empty, 17f, TextAlignmentOptions.Top,
                HexworldUiTheme.MutedColor);
            hint.textWrappingMode = TextWrappingModes.Normal;
            Band(hint.rectTransform, 88f, 50f, 18f);

            GameObject badge = Panel("AiBadge", panel.transform, HexworldUiTheme.AiColor);
            Place(Rect(badge), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(360f, 46f));
            Rect(badge).pivot = new Vector2(0.5f, 1f);

            TMP_Text badgeText = Label("Text", badge.transform, "ХОДИТ ПРОТИВНИК", 20f,
                TextAlignmentOptions.Center, Color.white);
            badgeText.fontStyle = FontStyles.Bold;
            Stretch(badgeText.rectTransform, 8f, 8f, 4f, 4f);
            badge.SetActive(false);

            var header = panel.AddComponent<HexworldTurnHeader>();
            Wire(header,
                "_turnText", turn,
                "_phaseText", phase,
                "_hintText", hint,
                "_sideStripe", stripeGo.GetComponent<Image>(),
                "_aiBadge", badge);
            return header;
        }

        // ------------------------------------------------------------------
        // Панель кубиков
        // ------------------------------------------------------------------

        static HexworldDicePanel BuildDicePanel(Transform parent, HexworldUiRoot root)
        {
            GameObject panel = ActionPanel("DicePanel", parent);
            Caption(panel.transform, "УРОЖАЙ");

            GameObject grid = Child("DiceGrid", panel.transform);
            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(110f, 106f);
            gridLayout.spacing = new Vector2(8f, 8f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;

            GameObject dieGo = Panel("DieTemplate", grid.transform, HexworldUiTheme.DieColor);
            var dieButton = MakeButton(dieGo);
            var dieView = dieGo.AddComponent<HexworldDieView>();

            TMP_Text source = Label("Source", dieGo.transform, "Castle", 15f, TextAlignmentOptions.Center,
                HexworldUiTheme.DieTextColor);
            Band(source.rectTransform, 6f, 20f, 4f);

            TMP_Text face = Label("Face", dieGo.transform, "1 Еда", 18f, TextAlignmentOptions.Center,
                HexworldUiTheme.DieTextColor);
            face.fontStyle = FontStyles.Bold;
            face.textWrappingMode = TextWrappingModes.Normal;
            Band(face.rectTransform, 26f, 52f, 3f);

            TMP_Text state = Label("State", dieGo.transform, string.Empty, 13f, TextAlignmentOptions.Center,
                HexworldUiTheme.DieTextColor);
            Band(state.rectTransform, 80f, 20f, 3f);

            Wire(dieView,
                "_button", dieButton,
                "_background", dieGo.GetComponent<Image>(),
                "_sourceText", source,
                "_faceText", face,
                "_stateText", state);
            dieGo.SetActive(false);

            TMP_Text info = Body(panel.transform, "Info", 50f);

            GameObject row = ButtonRow(panel.transform);
            TMP_Text rerollLabel;
            Button reroll = TextButton(row.transform, "RerollButton", "Реролл", out rerollLabel);
            TMP_Text confirmLabel;
            Button confirm = TextButton(row.transform, "ConfirmButton", "Подтвердить", out confirmLabel);
            Accent(confirm);

            var dicePanel = panel.AddComponent<HexworldDicePanel>();
            Wire(dicePanel,
                "_root", root,
                "_dieRoot", Rect(grid),
                "_dieTemplate", dieView,
                "_rerollButton", reroll,
                "_rerollLabel", rerollLabel,
                "_confirmButton", confirm,
                "_infoText", info);

            UnityEventTools.AddVoidPersistentListener(reroll.onClick, dicePanel.Reroll);
            UnityEventTools.AddVoidPersistentListener(confirm.onClick, dicePanel.Confirm);
            return dicePanel;
        }

        // ------------------------------------------------------------------
        // Панель строительства
        // ------------------------------------------------------------------

        static HexworldBuildPanel BuildBuildPanel(Transform parent, HexworldUiRoot root)
        {
            GameObject panel = ActionPanel("BuildPanel", parent);
            Caption(panel.transform, "СТРОИТЕЛЬСТВО");

            GameObject rows = Child("Rows", panel.transform);
            var rowsLayout = rows.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 6f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            GameObject rowGo = Panel("RowTemplate", rows.transform, HexworldUiTheme.ButtonColor);
            Element(rowGo, 60f);
            Button rowButton = MakeButton(rowGo);
            var buildButton = rowGo.AddComponent<HexworldBuildButton>();

            TMP_Text name = Label("Name", rowGo.transform, "Cottage", 19f, TextAlignmentOptions.Left,
                HexworldUiTheme.TextColor);
            Band(name.rectTransform, 0f, 0.55f, 14f, 4f, 7f, 24f);

            TMP_Text cost = Label("Cost", rowGo.transform, "3 Дер", 17f, TextAlignmentOptions.Right,
                HexworldUiTheme.AccentColor);
            Band(cost.rectTransform, 0.55f, 1f, 4f, 14f, 8f, 22f);

            TMP_Text hint = Label("Hint", rowGo.transform, string.Empty, 15f, TextAlignmentOptions.Left,
                HexworldUiTheme.MutedColor);
            hint.overflowMode = TextOverflowModes.Ellipsis;
            Band(hint.rectTransform, 0f, 1f, 14f, 14f, 33f, 21f);

            Wire(buildButton,
                "_button", rowButton,
                "_background", rowGo.GetComponent<Image>(),
                "_nameText", name,
                "_costText", cost,
                "_hintText", hint);
            rowGo.SetActive(false);

            TMP_Text status = Body(panel.transform, "Status", 44f);

            GameObject row = ButtonRow(panel.transform);
            TMP_Text endLabel;
            Button end = TextButton(row.transform, "EndBuildButton", "Закончить строительство", out endLabel);
            Accent(end);

            var buildPanel = panel.AddComponent<HexworldBuildPanel>();
            Wire(buildPanel,
                "_root", root,
                "_buttonRoot", Rect(rows),
                "_buttonTemplate", buildButton,
                "_endButton", end,
                "_statusText", status);

            UnityEventTools.AddVoidPersistentListener(end.onClick, buildPanel.EndBuild);
            return buildPanel;
        }

        // ------------------------------------------------------------------
        // Панель боя
        // ------------------------------------------------------------------

        static HexworldCombatPanel BuildCombatPanel(Transform parent, HexworldUiRoot root)
        {
            GameObject panel = ActionPanel("CombatPanel", parent);
            Caption(panel.transform, "БОЙ");

            GameObject info = Panel("TargetBlock", panel.transform, HexworldUiTheme.PanelSoftColor);
            Element(info, 104f);
            TMP_Text target = Label("TargetText", info.transform, string.Empty, 17f, TextAlignmentOptions.TopLeft,
                HexworldUiTheme.TextColor);
            target.textWrappingMode = TextWrappingModes.Normal;
            Stretch(target.rectTransform, 12f, 12f, 8f, 8f);

            GameObject soldierRow = Child("SoldierRow", panel.transform);
            Element(soldierRow, 46f);

            TMP_Text soldierCaption = Label("Caption", soldierRow.transform, "Солдаты", 18f,
                TextAlignmentOptions.Left, HexworldUiTheme.MutedColor);
            Slot(soldierCaption.rectTransform, 0f, 96f, 30f);

            TMP_Text minusLabel;
            Button minus = TextButton(soldierRow.transform, "MinusButton", "−", out minusLabel);
            Slot(Rect(minus.gameObject), 100f, 44f, 40f);

            Slider slider = BuildSlider(soldierRow.transform);
            Slot(Rect(slider.gameObject), 150f, 194f, 24f);

            TMP_Text plusLabel;
            Button plus = TextButton(soldierRow.transform, "PlusButton", "+", out plusLabel);
            Slot(Rect(plus.gameObject), 352f, 44f, 40f);

            TMP_Text count = Label("Count", soldierRow.transform, "1", 24f, TextAlignmentOptions.Right,
                HexworldUiTheme.AccentColor);
            count.fontStyle = FontStyles.Bold;
            Slot(count.rectTransform, 402f, 62f, 34f);

            TMP_Text result = Body(panel.transform, "ResultText", 48f);

            GameObject row = ButtonRow(panel.transform);
            TMP_Text attackLabel;
            Button attack = TextButton(row.transform, "AttackButton", "Атаковать", out attackLabel);
            Accent(attack);
            TMP_Text endLabel;
            Button endTurn = TextButton(row.transform, "EndTurnButton", "Закончить ход", out endLabel);

            var combatPanel = panel.AddComponent<HexworldCombatPanel>();
            Wire(combatPanel,
                "_root", root,
                "_targetText", target,
                "_soldierSlider", slider,
                "_soldierText", count,
                "_minusButton", minus,
                "_plusButton", plus,
                "_attackButton", attack,
                "_endTurnButton", endTurn,
                "_resultText", result);

            UnityEventTools.AddIntPersistentListener(minus.onClick, combatPanel.AddSoldiers, -1);
            UnityEventTools.AddIntPersistentListener(plus.onClick, combatPanel.AddSoldiers, 1);
            UnityEventTools.AddVoidPersistentListener(attack.onClick, combatPanel.Attack);
            UnityEventTools.AddVoidPersistentListener(endTurn.onClick, combatPanel.EndTurn);
            return combatPanel;
        }

        static Slider BuildSlider(Transform parent)
        {
            GameObject go = Child("SoldierSlider", parent);
            var slider = go.AddComponent<Slider>();

            GameObject background = Panel("Background", go.transform, HexworldUiTheme.PanelSoftColor);
            Stretch(Rect(background), 0f, 0f, 0f, 0f);

            GameObject fillArea = Child("Fill Area", go.transform);
            Stretch(Rect(fillArea), 4f, 4f, 0f, 0f);

            GameObject fill = Panel("Fill", fillArea.transform, HexworldUiTheme.PlayerColor);
            Stretch(Rect(fill), 0f, 0f, 0f, 0f);

            GameObject handleArea = Child("Handle Slide Area", go.transform);
            Stretch(Rect(handleArea), 10f, 10f, 0f, 0f);

            GameObject handle = Panel("Handle", handleArea.transform, HexworldUiTheme.AccentColor);
            Rect(handle).sizeDelta = new Vector2(20f, 0f);

            slider.fillRect = Rect(fill);
            slider.handleRect = Rect(handle);
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            slider.minValue = 1f;
            slider.maxValue = 5f;
            slider.value = 1f;
            return slider;
        }

        // ------------------------------------------------------------------
        // Лог событий
        // ------------------------------------------------------------------

        static HexworldEventLog BuildEventLog(Transform parent)
        {
            GameObject panel = Panel("EventLog", parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(LogWidth, LogHeight));

            TMP_Text caption = Label("Caption", panel.transform, "СОБЫТИЯ", 17f, TextAlignmentOptions.Left,
                HexworldUiTheme.MutedColor);
            caption.fontStyle = FontStyles.Bold;
            Band(caption.rectTransform, 10f, 22f, 14f);

            GameObject viewport = Child("Viewport", panel.transform);
            Stretch(Rect(viewport), 10f, 10f, 38f, 10f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = Child("Content", viewport.transform);
            Stretch(Rect(content), new Vector2(0f, 1f), new Vector2(1f, 1f), 0f, 0f, 0f, 0f);
            Rect(content).pivot = new Vector2(0.5f, 1f);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 3f;
            contentLayout.padding = new RectOffset(4, 4, 2, 2);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text line = Label("LineTemplate", content.transform, "…", 16f, TextAlignmentOptions.TopLeft,
                HexworldUiTheme.TextColor);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.gameObject.SetActive(false);

            var scroll = panel.AddComponent<ScrollRect>();
            scroll.content = Rect(content);
            scroll.viewport = Rect(viewport);
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var log = panel.AddComponent<HexworldEventLog>();
            Wire(log,
                "_content", Rect(content),
                "_lineTemplate", line,
                "_scrollRect", scroll,
                "_maxLines", 50);
            return log;
        }

        static void BuildCameraHint(Transform parent)
        {
            GameObject panel = Panel("CameraHint", parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(820f, 34f));

            TMP_Text hint = Label("Text", panel.transform,
                "Камера: правая кнопка — сдвиг · средняя или Q/E — поворот · колесо — приближение",
                16f, TextAlignmentOptions.Center, HexworldUiTheme.TextColor);
            Stretch(hint.rectTransform, 10f, 10f, 4f, 4f);
        }

        // ------------------------------------------------------------------
        // Экран победы
        // ------------------------------------------------------------------

        static HexworldVictoryScreen BuildVictoryScreen(Transform parent, HexworldUiRoot root)
        {
            GameObject holder = Child("VictoryScreen", parent);
            Stretch(Rect(holder), 0f, 0f, 0f, 0f);
            var victory = holder.AddComponent<HexworldVictoryScreen>();

            GameObject overlay = Panel("Panel", holder.transform, new Color(0.02f, 0.03f, 0.05f, 0.78f));
            Stretch(Rect(overlay), 0f, 0f, 0f, 0f);

            GameObject card = Panel("Card", overlay.transform, HexworldUiTheme.PanelSoftColor);
            Place(Rect(card), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 380f));
            Rect(card).pivot = new Vector2(0.5f, 0.5f);

            TMP_Text title = Label("Title", card.transform, "ПОБЕДА", 56f, TextAlignmentOptions.Center,
                HexworldUiTheme.PlayerColor);
            title.fontStyle = FontStyles.Bold;
            Band(title.rectTransform, 44f, 70f, 24f);

            TMP_Text reason = Label("Reason", card.transform, string.Empty, 22f, TextAlignmentOptions.Top,
                HexworldUiTheme.TextColor);
            reason.textWrappingMode = TextWrappingModes.Normal;
            Band(reason.rectTransform, 130f, 96f, 40f);

            TMP_Text restartLabel;
            Button restart = TextButton(card.transform, "RestartButton", "Новая партия", out restartLabel);
            Accent(restart);
            Place(Rect(restart.gameObject), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 60f));

            Wire(victory,
                "_root", root,
                "_panel", overlay,
                "_titleText", title,
                "_reasonText", reason);

            UnityEventTools.AddVoidPersistentListener(restart.onClick, victory.Restart);

            overlay.SetActive(false);
            return victory;
        }

        // ------------------------------------------------------------------
        // Блокировка камеры под курсором
        // ------------------------------------------------------------------

        static void AttachCameraGuards(GameObject canvas, HexworldCameraRig rig)
        {
            string[] names = { "ResourceBar", "AiBar", "TurnHeader", "DicePanel", "BuildPanel", "CombatPanel", "EventLog" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform target = canvas.transform.Find(names[i]);
                if (target == null)
                    continue;

                var guard = target.gameObject.AddComponent<HexworldCameraGuard>();
                Wire(guard, "_cameraRig", rig);
            }
        }

        // ------------------------------------------------------------------
        // Кирпичики
        // ------------------------------------------------------------------

        /// <summary>Панель фазы: правый нижний угол, высота по содержимому.</summary>
        static GameObject ActionPanel(string name, Transform parent)
        {
            GameObject panel = Panel(name, parent, HexworldUiTheme.PanelColor);
            Place(Rect(panel), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(PanelWidth, 200f));

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return panel;
        }

        static TMP_Text Caption(Transform parent, string text)
        {
            TMP_Text caption = Label("Caption", parent, text, 18f, TextAlignmentOptions.Left,
                HexworldUiTheme.AccentColor);
            caption.fontStyle = FontStyles.Bold;
            Element(caption.gameObject, 24f);
            return caption;
        }

        static TMP_Text Body(Transform parent, string name, float height)
        {
            TMP_Text text = Label(name, parent, string.Empty, 17f, TextAlignmentOptions.TopLeft,
                HexworldUiTheme.MutedColor);
            text.textWrappingMode = TextWrappingModes.Normal;
            Element(text.gameObject, height);
            return text;
        }

        static GameObject ButtonRow(Transform parent)
        {
            GameObject row = Child("Buttons", parent);
            Element(row, 52f);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        static Button TextButton(Transform parent, string name, string text, out TMP_Text label)
        {
            GameObject go = Panel(name, parent, HexworldUiTheme.ButtonColor);
            Button button = MakeButton(go);

            label = Label("Label", go.transform, text, 19f, TextAlignmentOptions.Center, HexworldUiTheme.TextColor);
            label.textWrappingMode = TextWrappingModes.Normal;
            Stretch(label.rectTransform, 8f, 8f, 2f, 2f);
            return button;
        }

        /// <summary>Красит кнопку в цвет игрока, чтобы главное действие бросалось в глаза.</summary>
        static void Accent(Button button)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = HexworldUiTheme.ButtonActiveColor;
        }

        static Button MakeButton(GameObject go)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.65f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            return button;
        }

        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Panel(string name, Transform parent, Color color)
        {
            GameObject go = Child(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        static TMP_Text Label(
            string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
        {
            GameObject go = Child(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            if (_font != null)
                label.font = _font;

            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            return label;
        }

        static void Element(GameObject go, float preferredHeight)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            element.flexibleHeight = 0f;
        }

        static RectTransform Rect(GameObject go)
        {
            return (RectTransform)go.transform;
        }

        /// <summary>Ставит прямоугольник в угол или на край экрана.</summary>
        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Растягивает прямоугольник по всему родителю с отступами.</summary>
        static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            Stretch(rt, Vector2.zero, Vector2.one, left, right, top, bottom);
        }

        /// <summary>Растягивает прямоугольник по заданным якорям с отступами.</summary>
        static void Stretch(
            RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, float left, float right, float top, float bottom)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Горизонтальная полоса внутри панели: отступ сверху и высота.</summary>
        static void Band(RectTransform rt, float top, float height, float side = 14f)
        {
            Band(rt, 0f, 1f, side, side, top, height);
        }

        /// <summary>
        /// Полоса внутри панели на части ширины. Прямоугольник прижат к верху
        /// родителя, поэтому высота задаётся напрямую и ничего не обрезается.
        /// </summary>
        static void Band(
            RectTransform rt, float anchorLeft, float anchorRight, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(anchorLeft, 1f);
            rt.anchorMax = new Vector2(anchorRight, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Ячейка фиксированной ширины в ряду, по левому краю.</summary>
        static void Slot(RectTransform rt, float x, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Прописывает приватные сериализуемые поля компонента парами «имя, значение».</summary>
        static void Wire(Component target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                var path = (string)pairs[i];
                SerializedProperty property = so.FindProperty(path);
                if (property == null)
                    throw new Exception("Нет поля " + path + " у " + target.GetType().Name);

                object value = pairs[i + 1];
                if (value is UnityEngine.Object reference)
                    property.objectReferenceValue = reference;
                else if (value is int number)
                    property.intValue = number;
                else if (value is float real)
                    property.floatValue = real;
                else if (value is bool flag)
                    property.boolValue = flag;
                else if (value is string text)
                    property.stringValue = text;
                else if (value == null)
                    property.objectReferenceValue = null;
                else
                    throw new Exception("Не знаю, как записать " + value.GetType().Name + " в " + path);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Шрифт
        // ------------------------------------------------------------------

        /// <summary>
        /// Отдаёт шрифт интерфейса. Ассет динамический: глифы кириллицы
        /// дорисовываются в атлас по мере надобности, поэтому русский текст
        /// виден и в редакторе, и в сборке.
        /// </summary>
        static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            Font source = null;
            for (int i = 0; i < SourceFonts.Length && source == null; i++)
                source = AssetDatabase.LoadAssetAtPath<Font>(SourceFonts[i]);

            if (source == null)
            {
                Debug.LogWarning("[OstrovUiSetup] Исходный шрифт не найден, беру шрифт TMP по умолчанию. "
                    + "Сначала выполните Ostrov/Scene/Import TMP Resources.");
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                throw new Exception("Не удалось собрать шрифт из " + AssetDatabase.GetAssetPath(source));

            asset.name = "Ostrov_UI_SDF";

            Directory.CreateDirectory(Path.GetFullPath(FontDir));
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = "Ostrov_UI Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.material != null)
            {
                asset.material.name = "Ostrov_UI Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Debug.Log("[OstrovUiSetup] Шрифт интерфейса собран: " + FontAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }
    }
}
