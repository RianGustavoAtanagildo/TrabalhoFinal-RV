using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CircuitAIR
{
    /// <summary>
    /// Interface do Circuit AIR, montada por código ao iniciar (tela de 1080x1920, retrato).
    /// Telas: menu (Iniciar, Níveis, Recordes, Como jogar), placar da partida (tempo, erros, fase,
    /// andamento e mensagens), pausa e resultado final (RF9 e RF10).
    /// </summary>
    public sealed class CircuitUI : MonoBehaviour
    {
        public enum ToastKind { Info, Good, Bad }

        [SerializeField] private TMP_FontAsset font;

        [Header("Cores")]
        [SerializeField] private Color accent = new Color(0.11f, 0.47f, 1f, 1f);
        [SerializeField] private Color accentLight = new Color(0.33f, 0.68f, 1f, 1f);
        [SerializeField] private Color panelColor = new Color(0.03f, 0.05f, 0.10f, 0.80f);
        [SerializeField] private Color buttonColor = new Color(0.10f, 0.13f, 0.20f, 0.88f);
        [SerializeField] private Color good = new Color(0.35f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color bad = new Color(1f, 0.38f, 0.32f, 1f);
        [SerializeField] private Color gold = new Color(1f, 0.82f, 0.29f, 1f);

        private CircuitGameManager game;
        private Sprite roundedSprite;
        private RectTransform safeArea;
        private Rect appliedSafeArea;
        private bool built;

        private GameObject titleGroup, scanHint, menuGroup, hudGroup;
        private GameObject levelsPanel, recordsPanel, howToPanel, pausePanel, resultsPanel, calibrationPanel;
        private TMP_Text calibrationStatus;
        private TMP_Text scanText, timerText, errorsText, levelText, messageText, lupaModeText, levelButtonSub;
        private TMP_Text resultTime, resultErrors, resultRecord, recordsBody;
        private TMP_Text[] levelRecordTexts;
        private Image progressFill, messageBackground;
        private Button nextLevelButton;
        private string toastText;
        private ToastKind toastKind;
        private float toastUntil;

        public void Initialize(CircuitGameManager manager)
        {
            game = manager;
            if (!built)
                Build();
            OnPhaseChanged();
        }

        public void ShowToast(string text, ToastKind kind, float seconds = 1.8f)
        {
            toastText = text;
            toastKind = kind;
            toastUntil = Time.unscaledTime + seconds;
        }

        public void OnPhaseChanged()
        {
            if (!built || game == null)
                return;

            GamePhase phase = game.Phase;
            bool inMatch = phase == GamePhase.WaitingForCircuit || phase == GamePhase.WaitingForStart ||
                           phase == GamePhase.Playing || phase == GamePhase.Paused;

            titleGroup.SetActive(phase == GamePhase.Menu);
            menuGroup.SetActive(phase == GamePhase.Menu);
            hudGroup.SetActive(inMatch);
            pausePanel.SetActive(phase == GamePhase.Paused);
            resultsPanel.SetActive(phase == GamePhase.Finished);
            if (phase != GamePhase.Menu)
            {
                levelsPanel.SetActive(false);
                recordsPanel.SetActive(false);
                howToPanel.SetActive(false);
                calibrationPanel.SetActive(false);
            }

            if (phase == GamePhase.Finished)
                FillResults();
            if (phase == GamePhase.Menu)
                RefreshMenuTexts();
        }

        private void Update()
        {
            if (!built || game == null)
                return;

            ApplySafeArea();
            GamePhase phase = game.Phase;

            scanHint.SetActive((phase == GamePhase.Menu || phase == GamePhase.WaitingForCircuit) && !AnyModalOpen());
            if (!game.HasCircuit)
                scanText.text = "Aponte a câmera para o QR Code e posicione o circuito na mesa.";
            else if (game.LupaMode == LupaMode.PhysicalMarker && !game.LupaVisible)
                scanText.text = "Circuito posicionado! Mostre o marcador da lupa e toque em INICIAR.";
            else
                scanText.text = "Circuito posicionado! Toque em INICIAR.";

            if (calibrationPanel.activeSelf)
                RefreshCalibration();

            if (hudGroup.activeSelf)
            {
                timerText.text = CircuitGameManager.FormatTime(game.ElapsedSeconds);
                errorsText.text = game.Errors.ToString();
                errorsText.color = game.Errors == 0 ? Color.white : bad;
                levelText.text = $"{game.LevelIndex + 1}/{CircuitLevels.Count}";
                progressFill.fillAmount = game.Progress01;

                bool toast = Time.unscaledTime < toastUntil;
                messageText.text = toast ? toastText : PhaseHint();
                Color tint = toast ? ToastColor(toastKind) : Color.white;
                messageText.color = tint;
                messageBackground.color = toast && toastKind == ToastKind.Bad
                    ? new Color(0.35f, 0.03f, 0.03f, 0.85f)
                    : panelColor;
            }
        }

        private string PhaseHint()
        {
            switch (game.Phase)
            {
                case GamePhase.WaitingForCircuit:
                    return "Procurando o QR Code... aponte a câmera para ele.";
                case GamePhase.WaitingForStart:
                    if (!game.LupaVisible)
                        return "Mostre o marcador da lupa para a câmera.";
                    return "Encaixe a argola da lupa no fio, dentro do anel verde, para começar.";
                case GamePhase.Playing:
                    if (game.OffWire)
                        return "Você saiu do fio! Volte ao anel amarelo.";
                    if (!game.LupaVisible)
                        return "A lupa sumiu da câmera!";
                    return "Leve a lupa até a bandeira sem encostar no fio.";
                case GamePhase.Paused:
                    return "Jogo pausado.";
                default:
                    return string.Empty;
            }
        }

        private Color ToastColor(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Good: return good;
                case ToastKind.Bad: return new Color(1f, 0.85f, 0.82f, 1f);
                default: return Color.white;
            }
        }

        private bool AnyModalOpen() => levelsPanel.activeSelf || recordsPanel.activeSelf || howToPanel.activeSelf || calibrationPanel.activeSelf;

        // ------------------------------------------------------------------
        // Montagem
        // ------------------------------------------------------------------

        private void Build()
        {
            if (font == null)
                font = TMP_Settings.defaultFontAsset;
            roundedSprite = CreateRoundedSprite();

            var canvasObject = new GameObject("Interface (Canvas)");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            safeArea = NewRect("Área segura", canvasObject.transform);
            Stretch(safeArea);
            ApplySafeArea();

            BuildTitle();
            BuildScanHint();
            BuildHud();
            BuildMenu();
            levelsPanel = BuildLevelsPanel();
            recordsPanel = BuildRecordsPanel();
            howToPanel = BuildHowToPanel();
            pausePanel = BuildPausePanel();
            resultsPanel = BuildResultsPanel();
            calibrationPanel = BuildCalibrationPanel();

            built = true;
        }

        private void BuildTitle()
        {
            var group = NewRect("Título", safeArea);
            Place(group, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1000f, 220f));
            titleGroup = group.gameObject;

            var title = Label(group, "<i>CIRCUIT</i> <color=#4DA8FF><i>AIR</i></color>", 112, Color.white, TextAlignmentOptions.Center, true);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 140f));
            var subtitle = Label(group, "Jogo do fio em realidade aumentada", 38, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, false);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1000f, 60f));
        }

        private void BuildScanHint()
        {
            var pill = Panel("Dica do QR Code", safeArea, panelColor);
            Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -365f), new Vector2(960f, 130f));
            scanHint = pill.gameObject;
            scanText = Label(pill.rectTransform, string.Empty, 36, Color.white, TextAlignmentOptions.Center, false);
            Inset(scanText.rectTransform, 30f, 10f);
        }

        private void BuildHud()
        {
            var hud = NewRect("Placar", safeArea);
            Stretch(hud);
            hudGroup = hud.gameObject;

            Button pause = MakeButton(hud, "II", null, buttonColor, () => game.Pause(), 46);
            Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(95f, -95f), new Vector2(130f, 130f));
            pause.GetComponent<Image>().pixelsPerUnitMultiplier = 0.62f;

            timerText = StatBox(hud, "Tempo", new Vector2(345f, -95f), 330f);
            errorsText = StatBox(hud, "Erros", new Vector2(655f, -95f), 250f);
            levelText = StatBox(hud, "Fase", new Vector2(925f, -95f), 250f);

            var track = Panel("Andamento", hud, new Color(1f, 1f, 1f, 0.18f));
            Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(1000f, 18f));
            track.pixelsPerUnitMultiplier = 4f;
            progressFill = Panel("Preenchimento", track.rectTransform, accentLight);
            Stretch(progressFill.rectTransform);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            messageBackground = Panel("Mensagem", hud, panelColor);
            Place(messageBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(1000f, 120f));
            messageText = Label(messageBackground.rectTransform, string.Empty, 38, Color.white, TextAlignmentOptions.Center, true);
            Inset(messageText.rectTransform, 24f, 8f);
        }

        private TMP_Text StatBox(RectTransform parent, string caption, Vector2 position, float width)
        {
            var box = Panel(caption, parent, panelColor);
            Place(box.rectTransform, new Vector2(0f, 1f), position, new Vector2(width, 130f));
            var captionText = Label(box.rectTransform, caption, 28, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Top, false);
            Place(captionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(width, 40f));
            var value = Label(box.rectTransform, "0", 52, Color.white, TextAlignmentOptions.Center, true);
            Place(value.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 47f), new Vector2(width, 70f));
            return value;
        }

        private void BuildMenu()
        {
            var group = NewRect("Menu", safeArea);
            Stretch(group);
            menuGroup = group.gameObject;

            Button lupaMode = MakeButton(group, string.Empty, null, buttonColor, ToggleLupaMode, 34);
            Place((RectTransform)lupaMode.transform, new Vector2(0.5f, 0f), new Vector2(-160f, 610f), new Vector2(660f, 110f));
            lupaModeText = lupaMode.GetComponentInChildren<TMP_Text>();
            Button calibrate = MakeButton(group, "Ajustar lupa", null, buttonColor, OpenCalibration, 36);
            Place((RectTransform)calibrate.transform, new Vector2(0.5f, 0f), new Vector2(340f, 610f), new Vector2(300f, 110f));

            Button start = MakeButton(group, "INICIAR", null, accent, () => game.StartChallenge(), 64);
            Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0f, 440f), new Vector2(980f, 190f));

            float y = 225f;
            Button levels = MakeButton(group, "Níveis", "Fase 1", buttonColor, () => OpenModal(levelsPanel), 40);
            Place((RectTransform)levels.transform, new Vector2(0.5f, 0f), new Vector2(-333f, y), new Vector2(313f, 190f));
            levelButtonSub = levels.transform.Find("Subtítulo").GetComponent<TMP_Text>();

            Button records = MakeButton(group, "Recordes", "Seu melhor tempo", buttonColor, () => OpenModal(recordsPanel), 40);
            Place((RectTransform)records.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(313f, 190f));

            Button howTo = MakeButton(group, "Como jogar", "Instruções", buttonColor, () => OpenModal(howToPanel), 40);
            Place((RectTransform)howTo.transform, new Vector2(0.5f, 0f), new Vector2(333f, y), new Vector2(313f, 190f));
        }

        private GameObject BuildLevelsPanel()
        {
            RectTransform body = Modal("Níveis", "Escolha a fase", 1150f, out GameObject root);
            levelRecordTexts = new TMP_Text[CircuitLevels.Count];
            for (int i = 0; i < CircuitLevels.Count; i++)
            {
                int index = i;
                CircuitLevel level = CircuitLevels.Get(i);
                Button button = MakeButton(body, level.Name, level.Description, buttonColor, () =>
                {
                    game.SelectLevel(index);
                    RefreshMenuTexts();
                    root.SetActive(false);
                }, 42);
                Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -235f - i * 230f), new Vector2(860f, 210f));
                var record = Label((RectTransform)button.transform, string.Empty, 28, gold, TextAlignmentOptions.Bottom, false);
                Place(record.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(820f, 40f));
                levelRecordTexts[i] = record;
            }
            AddCloseButton(body, root, "Voltar");
            return root;
        }

        private GameObject BuildRecordsPanel()
        {
            RectTransform body = Modal("Recordes", "Recordes", 1000f, out GameObject root);
            recordsBody = Label(body, string.Empty, 38, Color.white, TextAlignmentOptions.TopLeft, false);
            Place(recordsBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(840f, 640f));

            Button clear = MakeButton(body, "Apagar recordes", null, new Color(0.35f, 0.08f, 0.08f, 0.9f), () =>
            {
                CircuitRecords.ClearAll(CircuitLevels.Count);
                RefreshMenuTexts();
            }, 34);
            Place((RectTransform)clear.transform, new Vector2(0.5f, 0f), new Vector2(-215f, 105f), new Vector2(410f, 110f));
            Button back = MakeButton(body, "Voltar", null, accent, () => root.SetActive(false), 38);
            Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(215f, 105f), new Vector2(410f, 110f));
            return root;
        }

        private GameObject BuildHowToPanel()
        {
            RectTransform body = Modal("Como jogar", "Como jogar", 1420f, out GameObject root);
            var text = Label(body,
                "1. Imprima a folha de marcadores em tamanho real: QR Code de 10 cm e bandeirinha da lupa de 6 cm.\n" +
                "2. Dobre a bandeirinha (desenhos para fora), cole num papelão e prenda <b>em pé no topo do aro</b>, " +
                "atravessada (de lado para o aro), sem fita por cima dos desenhos.\n" +
                "3. Coloque o QR Code na mesa e o celular num suporte a 50–60 cm, com o circuito inteiro na tela. " +
                "O circuito fica ancorado: a mão ou a lupa podem passar por cima do QR Code.\n" +
                "4. Se o contorno azul não ficar em cima do aro real, use <b>Ajustar lupa</b>.\n" +
                "5. Toque em <b>INICIAR</b>, encaixe o aro no fio dentro do <color=#59F28C>anel verde</color> e leve até a bandeira.\n\n" +
                "<b>Erros:</b> cada toque no fio conta 1 erro (o fio pisca e o celular vibra). " +
                "Sair do fio também conta 1 erro: volte ao <color=#FFD24A>anel amarelo</color> para continuar. " +
                "Mantenha a bandeirinha virada para a câmera (qualquer um dos dois lados serve).\n\n" +
                "<b>Sem lupa física?</b> Toque em \"Lupa: física\" para usar a lupa virtual, presa ao celular.",
                35, Color.white, TextAlignmentOptions.TopLeft, false);
            Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -665f), new Vector2(860f, 1110f));
            AddCloseButton(body, root, "Entendi");
            return root;
        }

        private GameObject BuildPausePanel()
        {
            RectTransform body = Modal("Pausa", "Pausado", 640f, out GameObject root);
            Button resume = MakeButton(body, "Continuar", null, accent, () => game.Resume(), 44);
            Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(760f, 130f));
            Button restart = MakeButton(body, "Reiniciar", null, buttonColor, () => game.RestartChallenge(), 44);
            Place((RectTransform)restart.transform, new Vector2(0.5f, 1f), new Vector2(0f, -345f), new Vector2(760f, 130f));
            Button menu = MakeButton(body, "Menu", null, buttonColor, () => game.GoToMenu(), 44);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0f, -495f), new Vector2(760f, 130f));
            return root;
        }

        private GameObject BuildResultsPanel()
        {
            RectTransform body = Modal("Resultado", "Circuito concluído!", 1080f, out GameObject root);

            var timeCaption = Label(body, "Tempo final", 34, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center, false);
            Place(timeCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(840f, 50f));
            resultTime = Label(body, "00:00.00", 110, Color.white, TextAlignmentOptions.Center, true);
            Place(resultTime.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(840f, 140f));
            resultErrors = Label(body, string.Empty, 42, Color.white, TextAlignmentOptions.Center, false);
            Place(resultErrors.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(840f, 130f));
            resultRecord = Label(body, string.Empty, 40, gold, TextAlignmentOptions.Center, true);
            Place(resultRecord.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -495f), new Vector2(840f, 70f));

            Button restart = MakeButton(body, "Reiniciar", null, accent, () => game.RestartChallenge(), 44);
            Place((RectTransform)restart.transform, new Vector2(0.5f, 0f), new Vector2(0f, 415f), new Vector2(760f, 130f));
            nextLevelButton = MakeButton(body, "Próxima fase", null, buttonColor, () => game.NextLevel(), 44);
            Place((RectTransform)nextLevelButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 265f), new Vector2(760f, 130f));
            Button menu = MakeButton(body, "Menu", null, buttonColor, () => game.GoToMenu(), 44);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0f, 115f), new Vector2(760f, 130f));
            return root;
        }

        private void FillResults()
        {
            resultTime.text = CircuitGameManager.FormatTime(game.ElapsedSeconds);
            resultErrors.text = $"<b>{Plural(game.Errors, "erro", "erros")}</b>\n" +
                                $"<size=32><color=#FFFFFFB3>{Plural(game.Touches, "toque", "toques")} no fio · " +
                                $"{Plural(game.Exits, "saída", "saídas")} do fio</color></size>";
            resultErrors.color = game.Errors == 0 ? good : Color.white;

            if (game.NewRecord)
            {
                resultRecord.text = "Novo recorde!";
            }
            else
            {
                int level = game.LevelIndex;
                resultRecord.text = $"Recorde: {CircuitGameManager.FormatTime(CircuitRecords.BestTime(level))} · {Plural(CircuitRecords.BestErrors(level), "erro", "erros")}";
            }

            nextLevelButton.gameObject.SetActive(game.LevelIndex < CircuitLevels.Count - 1);
        }

        private void RefreshMenuTexts()
        {
            if (game == null)
                return;

            if (levelButtonSub != null)
                levelButtonSub.text = $"Fase {game.LevelIndex + 1} de {CircuitLevels.Count}";
            if (lupaModeText != null)
            {
                lupaModeText.text = game.LupaMode == LupaMode.Virtual
                    ? "Lupa: <b>virtual</b> (presa ao celular)\n<size=26><color=#FFFFFFA0>toque para usar a lupa física</color></size>"
                    : "Lupa: <b>física</b> (com marcador)\n<size=26><color=#FFFFFFA0>toque para usar a lupa virtual</color></size>";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < CircuitLevels.Count; i++)
            {
                CircuitLevel level = CircuitLevels.Get(i);
                string record = CircuitRecords.HasRecord(i)
                    ? $"{CircuitGameManager.FormatTime(CircuitRecords.BestTime(i))} · {Plural(CircuitRecords.BestErrors(i), "erro", "erros")}"
                    : "ainda sem recorde";
                builder.Append(i == game.LevelIndex ? "<color=#4DA8FF>" : "<color=#FFFFFF>")
                       .Append("<b>").Append(level.Name).Append("</b></color>\n")
                       .Append("   ").Append(record).Append("\n\n");
                if (levelRecordTexts != null && levelRecordTexts[i] != null)
                    levelRecordTexts[i].text = CircuitRecords.HasRecord(i) ? "Recorde: " + record : string.Empty;
            }
            if (recordsBody != null)
                recordsBody.text = builder.ToString();
        }

        // ------------------------------------------------------------------
        // Ajuste (calibração) da lupa física
        // ------------------------------------------------------------------

        private const float CalibrationStep = 0.005f;
        private const float RadiusStep = 0.0025f;

        private GameObject BuildCalibrationPanel()
        {
            // Fica na parte de baixo e sem escurecer a tela: o jogador precisa ver a lupa na câmera.
            Image card = Panel("Ajustar lupa", safeArea, new Color(0.03f, 0.05f, 0.10f, 0.88f));
            Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(1040f, 820f));
            RectTransform body = card.rectTransform;

            var title = Label(body, "Ajustar lupa física", 50, Color.white, TextAlignmentOptions.Center, true);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(980f, 70f));
            var help = Label(body,
                "Segure a lupa com a bandeirinha virada para a câmera e use os botões até o contorno azul ficar em cima do aro de verdade. " +
                "As direções seguem a bandeirinha: <b>Subir</b> = para o topo dela.",
                28, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, false);
            Place(help.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(980f, 100f));
            calibrationStatus = Label(body, string.Empty, 30, Color.white, TextAlignmentOptions.Center, true);
            Place(calibrationStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(980f, 80f));

            CalibrationButton(body, "Esquerda", -335f, -320f, () => Nudge(new Vector3(-CalibrationStep, 0f, 0f)));
            CalibrationButton(body, "Subir", 0f, -320f, () => Nudge(new Vector3(0f, 0f, CalibrationStep)));
            CalibrationButton(body, "Direita", 335f, -320f, () => Nudge(new Vector3(CalibrationStep, 0f, 0f)));
            CalibrationButton(body, "Aproximar", -335f, -440f, () => Nudge(new Vector3(0f, -CalibrationStep, 0f)));
            CalibrationButton(body, "Descer", 0f, -440f, () => Nudge(new Vector3(0f, 0f, -CalibrationStep)));
            CalibrationButton(body, "Afastar", 335f, -440f, () => Nudge(new Vector3(0f, CalibrationStep, 0f)));
            CalibrationButton(body, "Aro menor", -335f, -560f, () => game.Lupa.NudgePhysicalRadius(-RadiusStep));
            CalibrationButton(body, "Girar 90°", 0f, -560f, () => game.Lupa.RotateMarkerTilt());
            CalibrationButton(body, "Aro maior", 335f, -560f, () => game.Lupa.NudgePhysicalRadius(RadiusStep));
            CalibrationButton(body, "Padrão", -335f, -680f, () => game.Lupa.ResetCalibration());

            Button done = MakeButton(body, "Pronto", null, accent, CloseCalibration, 42);
            Place((RectTransform)done.transform, new Vector2(0.5f, 1f), new Vector2(167f, -680f), new Vector2(650f, 105f));

            card.gameObject.SetActive(false);
            return card.gameObject;
        }

        private void CalibrationButton(RectTransform parent, string text, float x, float y, UnityAction action)
        {
            Button button = MakeButton(parent, text, null, buttonColor, action, 36);
            Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(315f, 105f));
        }

        private void Nudge(Vector3 delta)
        {
            if (game.Lupa != null)
                game.Lupa.NudgeMarkerOffset(delta);
        }

        private void RefreshCalibration()
        {
            LupaController lupa = game.Lupa;
            if (lupa == null)
                return;
            Vector3 o = lupa.MarkerOffset * 100f;
            string seen = lupa.HasPose
                ? "<color=#59F28C>Marcador visto</color>"
                : "<color=#FF6A5E>Marcador não visto: mostre a lupa para a câmera</color>";
            calibrationStatus.text = seen + "\n<size=26><color=#FFFFFFC0>" +
                $"aro Ø {lupa.PhysicalRingRadius * 200f:0.0} cm · X {o.x:0.0} · Y {o.y:0.0} · Z {o.z:0.0} cm · giro {lupa.MarkerTilt:0}°" +
                "</color></size>";
        }

        private void OpenCalibration()
        {
            if (game.LupaMode != LupaMode.PhysicalMarker)
                game.SetLupaMode(LupaMode.PhysicalMarker);
            RefreshMenuTexts();
            calibrationPanel.SetActive(true);
            menuGroup.SetActive(false);
            titleGroup.SetActive(false);
        }

        private void CloseCalibration()
        {
            calibrationPanel.SetActive(false);
            OnPhaseChanged();
        }

        private void ToggleLupaMode()
        {
            game.SetLupaMode(game.LupaMode == LupaMode.Virtual ? LupaMode.PhysicalMarker : LupaMode.Virtual);
            RefreshMenuTexts();
        }

        private void OpenModal(GameObject panel)
        {
            RefreshMenuTexts();
            panel.SetActive(true);
        }

        private static string Plural(int value, string one, string many) => value == 1 ? $"1 {one}" : $"{value} {many}";

        // ------------------------------------------------------------------
        // Ajudantes de layout
        // ------------------------------------------------------------------

        private RectTransform Modal(string objectName, string title, float height, out GameObject root)
        {
            var dim = Panel(objectName, safeArea, new Color(0f, 0f, 0f, 0.55f));
            dim.sprite = null;
            Stretch(dim.rectTransform);
            root = dim.gameObject;

            var card = Panel("Cartão", dim.rectTransform, new Color(0.04f, 0.06f, 0.12f, 0.95f));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, height));

            var titleText = Label(card.rectTransform, title, 58, Color.white, TextAlignmentOptions.Center, true);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 80f));

            root.SetActive(false);
            return card.rectTransform;
        }

        private void AddCloseButton(RectTransform body, GameObject root, string text)
        {
            Button close = MakeButton(body, text, null, accent, () => root.SetActive(false), 42);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(760f, 120f));
        }

        private Button MakeButton(RectTransform parent, string text, string subtitle, Color color, UnityAction onClick, float fontSize)
        {
            Image background = Panel("Botão " + text, parent, color);
            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            var label = Label(background.rectTransform, text, fontSize, Color.white, TextAlignmentOptions.Center, true);
            label.name = "Texto";
            Stretch(label.rectTransform);
            if (!string.IsNullOrEmpty(subtitle))
            {
                label.rectTransform.offsetMin = new Vector2(10f, 0f);
                label.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                label.alignment = TextAlignmentOptions.Bottom;
                var sub = Label(background.rectTransform, subtitle, fontSize * 0.68f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Top, false);
                sub.name = "Subtítulo";
                Stretch(sub.rectTransform);
                sub.rectTransform.anchorMax = new Vector2(1f, 0.42f);
                sub.rectTransform.offsetMin = new Vector2(10f, 0f);
                sub.rectTransform.offsetMax = new Vector2(-10f, 0f);
            }
            else
            {
                Inset(label.rectTransform, 16f, 6f);
            }
            return button;
        }

        private Image Panel(string objectName, Transform parent, Color color)
        {
            RectTransform rect = NewRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.4f;
            image.color = color;
            return image;
        }

        private TMP_Text Label(Transform parent, string text, float size, Color color, TextAlignmentOptions alignment, bool bold)
        {
            RectTransform rect = NewRect("Texto", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void Inset(RectTransform rect, float horizontal, float vertical)
        {
            Stretch(rect);
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        /// <summary>
        /// Posiciona um elemento: <paramref name="center"/> é o centro do elemento em relação à âncora,
        /// em pixels da tela de referência (1080x1920).
        /// </summary>
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 center, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private void ApplySafeArea()
        {
            if (safeArea == null)
                return;
            Rect area = Screen.safeArea;
            if (area == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0)
                return;
            appliedSafeArea = area;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Gera um retângulo branco de cantos arredondados (fatiado em 9 partes) para os painéis.</summary>
        private static Sprite CreateRoundedSprite()
        {
            const int size = 96;
            const float radius = 40f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CircuitAIR_Rounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            const float border = radius + 4f;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
