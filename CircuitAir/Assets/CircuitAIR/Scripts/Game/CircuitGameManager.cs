using UnityEngine;

namespace CircuitAIR
{
    public enum GamePhase
    {
        /// <summary>Tela inicial (Iniciar, Níveis, Recordes, Como jogar).</summary>
        Menu,
        /// <summary>O jogador tocou em Iniciar, mas o QR Code ainda não foi encontrado.</summary>
        WaitingForCircuit,
        /// <summary>Circuito na mesa; falta encaixar a lupa no fio perto da base verde.</summary>
        WaitingForStart,
        /// <summary>Partida valendo: o tempo está correndo.</summary>
        Playing,
        Paused,
        Finished
    }

    public enum ErrorKind
    {
        Touch,
        LeftWire
    }

    /// <summary>
    /// Regras do Circuit AIR (requisitos funcionais 2 a 10 do planejamento):
    /// iniciar, contar o tempo, contar os toques da lupa no fio, detectar quando a lupa sai do fio,
    /// identificar a chegada, mostrar o resultado e reiniciar.
    /// </summary>
    public sealed class CircuitGameManager : MonoBehaviour
    {
        public static CircuitGameManager Instance { get; private set; }

        [SerializeField] private CircuitARController arController;
        [SerializeField] private LupaController lupa;
        [SerializeField] private CircuitUI ui;
        [SerializeField] private CircuitFeedback feedback;

        [Header("Regras")]
        [Tooltip("Tempo mínimo entre dois erros (s), para um único toque não contar várias vezes.")]
        [SerializeField, Min(0f)] private float errorCooldownSeconds = 0.35f;
        [Tooltip("Ao voltar para o fio, quanto (m) à frente do ponto onde saiu ainda é aceito.")]
        [SerializeField, Min(0f)] private float rejoinToleranceMeters = 0.03f;
        [Tooltip("Tamanho máximo (m) de cada passo da verificação entre dois quadros (evita 'atravessar' o fio).")]
        [SerializeField, Min(0.0005f)] private float maxStepMeters = 0.003f;
        [Tooltip("Avanço máximo (m) ao longo do fio entre duas verificações. Mais que isso é um 'pulo' e conta como sair do fio.")]
        [SerializeField, Min(0.005f)] private float maxJumpMeters = 0.02f;
        [Tooltip("Velocidade máxima (m/s) para percorrer o fio. Mais rápido que isso é tratado como 'pulo'.")]
        [SerializeField, Min(0.05f)] private float maxSpeedMetersPerSecond = 0.8f;

        private float frameAdvanceLimit = float.MaxValue;
        private float lastEvaluationTime;

        private int levelIndex;
        private GamePhase phase = GamePhase.Menu;
        private GamePhase phaseBeforePause;
        private float elapsedSeconds;
        private int touches;
        private int exits;
        private bool offWire;
        private bool wasTouching;
        private float progress;
        private float maxProgress;
        private float lastErrorTime = -100f;
        private bool hasPreviousRing;
        private Vector3 previousCenter;
        private Vector3 previousNormal;
        private bool lastTouching;
        private bool lastThreaded;
        private bool newRecord;

        public GamePhase Phase => phase;
        public int LevelIndex => levelIndex;
        public CircuitLevel Level => CircuitLevels.Get(levelIndex);
        public float ElapsedSeconds => elapsedSeconds;
        public int Errors => touches + exits;
        public int Touches => touches;
        public int Exits => exits;
        public bool OffWire => offWire;
        public bool NewRecord => newRecord;
        public bool HasCircuit => arController != null && arController.HasCircuit;
        public bool LupaVisible => lupa != null && lupa.HasPose;
        public LupaMode LupaMode => lupa != null ? lupa.Mode : LupaMode.PhysicalMarker;
        public LupaController Lupa => lupa;
        public bool IsTouching => lastTouching;
        public bool IsOnWire => lastThreaded;

        /// <summary>Andamento no circuito, de 0 a 1.</summary>
        public float Progress01
        {
            get
            {
                CircuitWire circuit = arController != null ? arController.Circuit : null;
                if (circuit == null || circuit.FinishDistance <= circuit.StartZoneEnd)
                    return 0f;
                return Mathf.Clamp01(Mathf.InverseLerp(circuit.StartGuideDistance, circuit.FinishDistance, maxProgress));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Há mais de um CircuitGameManager na cena. Mantenha apenas um.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            levelIndex = Mathf.Clamp(PlayerPrefs.GetInt("circuitair.level", 0), 0, CircuitLevels.Count - 1);
            if (lupa != null)
                lupa.SetMode((LupaMode)PlayerPrefs.GetInt("circuitair.lupaMode", (int)LupaMode.PhysicalMarker));
            ApplyLevel();
            if (ui != null)
                ui.Initialize(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- Ações chamadas pelos botões ----------

        /// <summary>Botão "Iniciar" (RF2): começa o desafio.</summary>
        public void StartChallenge()
        {
            elapsedSeconds = 0f;
            touches = 0;
            exits = 0;
            offWire = false;
            wasTouching = false;
            progress = 0f;
            maxProgress = 0f;
            lastErrorTime = -100f;
            hasPreviousRing = false;
            newRecord = false;

            CircuitWire circuit = arController != null ? arController.Circuit : null;
            if (circuit != null)
            {
                circuit.HideRejoinMarker();
                circuit.SetStartGuideVisible(true);
            }

            if (HasCircuit)
                EnterWaitingForStart();
            else
                SetPhase(GamePhase.WaitingForCircuit);
        }

        /// <summary>Botão "Reiniciar" (RF10).</summary>
        public void RestartChallenge() => StartChallenge();

        public void NextLevel()
        {
            SelectLevel((levelIndex + 1) % CircuitLevels.Count);
            StartChallenge();
        }

        public void SelectLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, CircuitLevels.Count - 1);
            PlayerPrefs.SetInt("circuitair.level", levelIndex);
            ApplyLevel();
        }

        public void SetLupaMode(LupaMode mode)
        {
            if (lupa == null)
                return;
            lupa.SetMode(mode);
            lupa.SetLevel(Level);
            PlayerPrefs.SetInt("circuitair.lupaMode", (int)mode);
            hasPreviousRing = false;
        }

        public void Pause()
        {
            if (phase != GamePhase.Playing && phase != GamePhase.WaitingForStart && phase != GamePhase.WaitingForCircuit)
                return;
            phaseBeforePause = phase;
            SetPhase(GamePhase.Paused);
        }

        public void Resume()
        {
            if (phase != GamePhase.Paused)
                return;
            hasPreviousRing = false;
            SetPhase(phaseBeforePause);
        }

        public void GoToMenu()
        {
            CircuitWire circuit = arController != null ? arController.Circuit : null;
            if (circuit != null)
            {
                circuit.HideRejoinMarker();
                circuit.SetStartGuideVisible(true);
            }
            if (arController != null)
                arController.SetLocked(false);
            if (lupa != null)
                lupa.SetFeedback(false, false);
            SetPhase(GamePhase.Menu);
        }

        // ---------- Laço do jogo ----------

        private void Update()
        {
            if (phase == GamePhase.WaitingForCircuit && HasCircuit)
                EnterWaitingForStart();

            if (phase == GamePhase.WaitingForStart || phase == GamePhase.Playing)
                EvaluateLupa();

            if (phase == GamePhase.Playing)
                elapsedSeconds += Time.deltaTime;
        }

        private void EvaluateLupa()
        {
            CircuitWire circuit = arController != null ? arController.Circuit : null;
            if (circuit == null || lupa == null)
            {
                hasPreviousRing = false;
                return;
            }

            // Lupa física fora da câmera (ou posição antiga): não avalia. Ao reaparecer, recomeça a verificação
            // do zero, sem ligar a posição antiga à nova (o que poderia "cortar" o fio e gerar erro falso).
            if (!lupa.HasFreshPose)
            {
                if (Time.time - lastEvaluationTime > 0.15f)
                    hasPreviousRing = false;
                return;
            }

            Transform space = circuit.transform;
            Vector3 center = space.InverseTransformPoint(lupa.RingCenter);
            Vector3 normal = space.InverseTransformDirection(lupa.RingNormal).normalized;
            float radius = lupa.RingRadius;
            float touchDistance = circuit.Level.WireRadius + lupa.RingTubeRadius;

            // Verifica em passos pequenos entre o quadro anterior e o atual, para um movimento
            // rápido não "atravessar" o fio sem ser detectado.
            int steps = 1;
            if (hasPreviousRing)
            {
                float move = Vector3.Distance(previousCenter, center);
                float turn = Vector3.Angle(previousNormal, normal);
                steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(move / maxStepMeters, turn / 4f)), 1, 24);
            }

            // Quanto a lupa pode avançar ao longo do fio desde a última verificação (velocidade de mão humana).
            // Se a câmera perdeu a lupa por um instante, conta o tempo que ela ficou sem ser vista.
            float sinceLast = Mathf.Clamp(Time.time - lastEvaluationTime, Time.deltaTime, 2f);
            frameAdvanceLimit = progress + maxSpeedMetersPerSecond * sinceLast + maxJumpMeters;
            lastEvaluationTime = Time.time;

            bool anyTouching = false;
            bool threaded = false;
            for (int s = 1; s <= steps; s++)
            {
                float f = s / (float)steps;
                Vector3 c = hasPreviousRing ? Vector3.Lerp(previousCenter, center, f) : center;
                Vector3 n = hasPreviousRing ? Vector3.Slerp(previousNormal, normal, f) : normal;
                RingQuery query = WireRingMath.Query(circuit.Path, c, n, radius, offWire ? maxProgress : progress);
                bool touching = query.MinDistance <= touchDistance;
                anyTouching |= touching;
                threaded = query.Threaded;
                Process(circuit, query, touching);
                if (phase != GamePhase.Playing && phase != GamePhase.WaitingForStart)
                    break;
            }

            previousCenter = center;
            previousNormal = normal;
            hasPreviousRing = true;
            lastTouching = anyTouching;
            lastThreaded = threaded && !offWire;
            lupa.SetFeedback(anyTouching && phase == GamePhase.Playing, lastThreaded);
        }

        private void Process(CircuitWire circuit, in RingQuery query, bool touching)
        {
            if (phase == GamePhase.WaitingForStart)
            {
                if (query.Threaded && !touching && query.ThreadDistance <= circuit.StartZoneEnd)
                    BeginPlaying(circuit, query.ThreadDistance);
                wasTouching = touching;
                return;
            }

            if (phase != GamePhase.Playing)
                return;

            // RF5 e RF6: cada vez que a lupa encosta no fio conta um erro.
            // Fora do fio a saída já foi contada, então encostar para voltar não conta de novo.
            if (touching && !wasTouching && !offWire && Time.time - lastErrorTime >= errorCooldownSeconds)
                RegisterError(ErrorKind.Touch, circuit);
            wasTouching = touching;

            if (query.Threaded)
            {
                if (offWire)
                {
                    if (query.ThreadDistance <= maxProgress + rejoinToleranceMeters)
                    {
                        offWire = false;
                        progress = query.ThreadDistance;
                        circuit.HideRejoinMarker();
                        if (ui != null)
                            ui.ShowToast("De volta ao fio!", CircuitUI.ToastKind.Good);
                    }
                    return;
                }

                // Um salto grande para a frente no mesmo instante não é movimento de verdade
                // (a lupa "pulou" um trecho): conta como sair do fio e exige voltar ao ponto onde estava.
                if (query.ThreadDistance > progress + maxJumpMeters || query.ThreadDistance > frameAdvanceLimit)
                {
                    LeaveWire(circuit);
                    return;
                }

                progress = query.ThreadDistance;
                if (progress > maxProgress)
                    maxProgress = progress;

                // RF8: chegou ao final do circuito.
                if (progress >= circuit.FinishDistance)
                    Finish();
            }
            else if (!offWire)
            {
                LeaveWire(circuit);
            }
        }

        /// <summary>
        /// Objetivo b: a lupa saiu do fio. Conta um erro (a não ser que o toque que causou a saída
        /// já tenha sido contado agora) e mostra o anel amarelo onde o jogador precisa voltar.
        /// </summary>
        private void LeaveWire(CircuitWire circuit)
        {
            offWire = true;
            circuit.ShowRejoinMarker(Mathf.Min(progress, maxProgress));
            bool alreadyCounted = Time.time - lastErrorTime < 0.6f;
            if (!alreadyCounted)
                RegisterError(ErrorKind.LeftWire, circuit);
            else if (ui != null)
                ui.ShowToast("Você saiu do fio! Volte ao anel amarelo.", CircuitUI.ToastKind.Bad);
        }

        private void BeginPlaying(CircuitWire circuit, float startDistance)
        {
            progress = startDistance;
            maxProgress = startDistance;
            frameAdvanceLimit = startDistance + maxSpeedMetersPerSecond * Time.deltaTime + maxJumpMeters;
            lastEvaluationTime = Time.time;
            elapsedSeconds = 0f;
            circuit.SetStartGuideVisible(false);
            SetPhase(GamePhase.Playing);
            if (feedback != null)
                feedback.PlayStart();
            if (ui != null)
                ui.ShowToast("Valendo! Não encoste no fio.", CircuitUI.ToastKind.Good);
        }

        private void RegisterError(ErrorKind kind, CircuitWire circuit)
        {
            lastErrorTime = Time.time;
            if (kind == ErrorKind.Touch)
                touches++;
            else
                exits++;

            circuit.Flash();
            if (feedback != null)
                feedback.PlayError(kind);
            if (ui != null)
            {
                ui.ShowToast(kind == ErrorKind.Touch ? "Encostou no fio!" : "Você saiu do fio! Volte ao anel amarelo.",
                    CircuitUI.ToastKind.Bad);
            }
        }

        private void Finish()
        {
            newRecord = CircuitRecords.TrySave(levelIndex, elapsedSeconds, Errors);
            if (arController != null && arController.Circuit != null)
                arController.Circuit.HideRejoinMarker();
            if (lupa != null)
                lupa.SetFeedback(false, false);
            SetPhase(GamePhase.Finished);
            if (feedback != null)
                feedback.PlayFinish();
        }

        private void EnterWaitingForStart()
        {
            if (arController != null)
                arController.SetLocked(true);
            if (arController != null && arController.Circuit != null)
                arController.Circuit.SetStartGuideVisible(true);
            hasPreviousRing = false;
            SetPhase(GamePhase.WaitingForStart);
        }

        private void ApplyLevel()
        {
            CircuitLevel level = Level;
            if (arController != null)
                arController.BuildLevel(level);
            if (lupa != null)
                lupa.SetLevel(level);
            if (phase != GamePhase.Menu)
                GoToMenu();
        }

        private void SetPhase(GamePhase newPhase)
        {
            phase = newPhase;
            if (ui != null)
                ui.OnPhaseChanged();
        }

        /// <summary>Formata o tempo como 00:00.00.</summary>
        public static string FormatTime(float seconds)
        {
            int hundredths = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f));
            return $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}.{hundredths % 100:00}";
        }
    }
}
