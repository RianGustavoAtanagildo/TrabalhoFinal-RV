using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CircuitAIR
{
    /// <summary>
    /// Rastreamento de imagens com AR Foundation:
    /// - "CircuitQR": posiciona o circuito virtual sobre o QR Code impresso.
    /// - "LupaMarker" e "LupaMarkerVerso": frente e verso da bandeirinha presa no topo do aro da lupa física.
    ///   Qualquer um dos dois lados que a câmera enxergar serve.
    ///
    /// Para o circuito ficar estável (requisito não funcional g), ele segue o QR Code com suavização
    /// enquanto o jogador ainda não começou, e fica travado numa âncora AR durante a partida.
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public sealed class CircuitARController : MonoBehaviour
    {
        [Header("AR Foundation")]
        [SerializeField] private ARTrackedImageManager trackedImageManager;
        [Tooltip("Opcional. Com ele, o circuito fica preso a uma âncora AR durante a partida.")]
        [SerializeField] private ARAnchorManager anchorManager;
        [Tooltip("Câmera AR. Se vazio, é procurada na cena.")]
        [SerializeField] private ARCameraManager cameraManager;
        [Tooltip("Usa a configuração de câmera de maior resolução: o ARCore acha marcadores pequenos (como o da lupa) de mais longe.")]
        [SerializeField] private bool preferHighResolutionCamera = true;

        [Header("Nomes das imagens na biblioteca (maiúsculas importam)")]
        [SerializeField] private string circuitImageName = "CircuitQR";
        [SerializeField] private string lupaImageName = "LupaMarker";
        [SerializeField] private string lupaBackImageName = "LupaMarkerVerso";

        /// <summary>
        /// O verso da bandeirinha, visto de trás, tem a direita e a normal invertidas em relação à frente
        /// (mesmo topo): é a frente girada 180° em torno do eixo vertical da imagem (Z).
        /// </summary>
        public static readonly Quaternion BackToFront = Quaternion.Euler(0f, 0f, 180f);

        [Header("Circuito")]
        [SerializeField] private CircuitTheme theme;
        [SerializeField] private LupaController lupa;
        [Tooltip("Deslocamento do circuito em relação ao centro do QR Code, em metros.")]
        [SerializeField] private Vector3 circuitOffset = Vector3.zero;
        [Tooltip("Quanto maior, mais rápido o circuito acompanha o QR Code (antes da partida).")]
        [SerializeField, Min(0.1f)] private float followSmoothing = 10f;

        private Transform circuitRoot;
        private CircuitWire circuit;
        private CircuitLevel pendingLevel;
        private Pose targetPose;
        private bool hasCircuitPose;
        private bool locked;
        private ARAnchor anchor;
        private int anchorRequest;
        private bool lupaFrontTracked;
        private bool lupaBackTracked;
        private bool cameraConfigured;

        /// <summary>Disparado na primeira vez que o QR Code é encontrado.</summary>
        public event Action CircuitPlaced;

        public bool HasCircuit => hasCircuitPose && circuit != null;
        public CircuitWire Circuit => circuit;
        public TrackingState CircuitTrackingState { get; private set; } = TrackingState.None;
        public bool LupaMarkerTracked => lupaFrontTracked || lupaBackTracked;

        /// <summary>Configuração de câmera escolhida (texto para diagnóstico), ou vazio.</summary>
        public string CameraConfigurationInfo { get; private set; } = "";

        private void Awake()
        {
            if (trackedImageManager == null)
                trackedImageManager = GetComponent<ARTrackedImageManager>();
            if (anchorManager == null)
                anchorManager = GetComponent<ARAnchorManager>();
            if (cameraManager == null)
                cameraManager = FindFirstObjectByType<ARCameraManager>();

            circuitRoot = new GameObject("Circuito (AR)").transform;
            circuitRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (trackedImageManager != null)
                trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
            else
                Debug.LogError("CircuitARController precisa de um ARTrackedImageManager.", this);
        }

        private void OnDisable()
        {
            if (trackedImageManager != null)
                trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
        }

        /// <summary>Cria (ou recria) o circuito da fase. Se o QR Code ainda não foi visto, ele aparece quando for encontrado.</summary>
        public void BuildLevel(CircuitLevel level)
        {
            pendingLevel = level;
            if (circuit == null)
            {
                var circuitObject = new GameObject("Circuito");
                circuitObject.transform.SetParent(circuitRoot, false);
                circuitObject.transform.localPosition = circuitOffset;
                circuit = circuitObject.AddComponent<CircuitWire>();
            }
            circuit.Build(level, theme);
        }

        /// <summary>Trava o circuito onde está (durante a partida) ou volta a seguir o QR Code.</summary>
        public void SetLocked(bool value)
        {
            if (locked == value)
                return;
            locked = value;

            if (locked)
            {
                CreateAnchorAsync();
            }
            else
            {
                circuitRoot.SetParent(null, true);
                RemoveAnchor();
            }
        }

        /// <summary>Cria uma âncora AR onde o circuito está; o AR ajusta a âncora se o mapa do ambiente for corrigido.</summary>
        private async void CreateAnchorAsync()
        {
            if (anchorManager == null || !anchorManager.enabled || anchorManager.subsystem == null || !anchorManager.subsystem.running)
                return;

            int request = ++anchorRequest;
            Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(new Pose(circuitRoot.position, circuitRoot.rotation));
            if (this == null || !result.status.IsSuccess() || result.value == null)
                return;

            // A partida pode ter terminado enquanto a âncora era criada.
            if (!locked || request != anchorRequest)
            {
                anchorManager.TryRemoveAnchor(result.value);
                return;
            }

            circuitRoot.SetParent(null, true);
            RemoveAnchor();
            anchor = result.value;
            // Se o AR remover a âncora sozinho, o circuito não pode ser destruído junto.
            anchor.destroyOnRemoval = false;
            circuitRoot.SetParent(anchor.transform, true);
        }

        private void RemoveAnchor()
        {
            if (anchor == null)
                return;
            if (anchorManager != null && anchorManager.subsystem != null && anchorManager.subsystem.running)
                anchorManager.TryRemoveAnchor(anchor);
            if (anchor != null)
                Destroy(anchor.gameObject);
            anchor = null;
        }

        private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (ARTrackedImage image in args.added)
                HandleImage(image);
            foreach (ARTrackedImage image in args.updated)
                HandleImage(image);
            foreach (var removed in args.removed)
            {
                ARTrackedImage image = removed.Value;
                if (image == null)
                    continue;
                if (image.referenceImage.name == circuitImageName)
                    CircuitTrackingState = TrackingState.None;
                else if (image.referenceImage.name == lupaImageName)
                    lupaFrontTracked = false;
                else if (image.referenceImage.name == lupaBackImageName)
                    lupaBackTracked = false;
            }
        }

        private void HandleImage(ARTrackedImage image)
        {
            string imageName = image.referenceImage.name;
            bool tracking = image.trackingState == TrackingState.Tracking;
            var pose = new Pose(image.transform.position, image.transform.rotation);

            if (imageName == circuitImageName)
            {
                CircuitTrackingState = image.trackingState;
                if (!tracking)
                    return; // Mantém a última posição boa: o circuito não some se o QR sair da tela.

                targetPose = pose;
                if (!hasCircuitPose)
                {
                    hasCircuitPose = true;
                    circuitRoot.SetPositionAndRotation(pose.position, pose.rotation);
                    circuitRoot.gameObject.SetActive(true);
                    if (circuit == null && pendingLevel != null)
                        BuildLevel(pendingLevel);
                    CircuitPlaced?.Invoke();
                }
            }
            else if (imageName == lupaImageName)
            {
                lupaFrontTracked = tracking;
                if (lupa != null)
                    lupa.SetMarkerPose(pose, tracking);
            }
            else if (imageName == lupaBackImageName)
            {
                lupaBackTracked = tracking;
                // Converte a pose do verso na pose equivalente da frente (mesmo lugar, virada 180°).
                if (lupa != null)
                    lupa.SetMarkerPose(new Pose(pose.position, pose.rotation * BackToFront), tracking);
            }
        }

        /// <summary>
        /// Pede ao ARCore/ARKit a imagem de câmera de maior resolução (com pelo menos 30 quadros/s) e foco
        /// automático. Com a configuração padrão (640x480 no ARCore), um marcador de 6 cm a meio metro
        /// ocupa poucos pixels e quase não é encontrado.
        /// </summary>
        private void ConfigureCamera()
        {
            // No Editor (XR Simulation) as "configurações" seguem o tamanho da janela Game; travar uma delas
            // deixaria a câmera simulada com outro formato. Só vale no celular.
            if (cameraConfigured || !preferHighResolutionCamera || cameraManager == null || Application.isEditor)
                return;
            var subsystem = cameraManager.subsystem;
            if (subsystem == null || !subsystem.running)
                return;
            cameraConfigured = true;

            cameraManager.autoFocusRequested = true;
            if (cameraManager.descriptor == null || !cameraManager.descriptor.supportsCameraConfigurations)
                return;

            try
            {
                using (var configurations = cameraManager.GetConfigurations(Unity.Collections.Allocator.Temp))
                {
                    if (!configurations.IsCreated || configurations.Length == 0)
                        return;

                    XRCameraConfiguration best = configurations[0];
                    long bestPixels = -1;
                    foreach (XRCameraConfiguration configuration in configurations)
                    {
                        int fps = configuration.framerate ?? 30;
                        if (fps < 30)
                            continue;
                        long pixels = (long)configuration.width * configuration.height;
                        if (pixels > bestPixels)
                        {
                            best = configuration;
                            bestPixels = pixels;
                        }
                    }
                    if (bestPixels < 0)
                        return;

                    XRCameraConfiguration? current = cameraManager.currentConfiguration;
                    if (!current.HasValue || current.Value != best)
                        cameraManager.currentConfiguration = best;
                    CameraConfigurationInfo = $"{best.width}x{best.height}";
                    Debug.Log($"CircuitAIR: câmera em {best.width}x{best.height} ({best.framerate} fps) para achar o marcador da lupa.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("CircuitAIR: não foi possível mudar a configuração da câmera: " + exception.Message);
            }
        }

        private void Update()
        {
            ConfigureCamera();

            if (!hasCircuitPose || locked)
                return;

            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            circuitRoot.SetPositionAndRotation(
                Vector3.Lerp(circuitRoot.position, targetPose.position, t),
                Quaternion.Slerp(circuitRoot.rotation, targetPose.rotation, t));
        }

        private void OnDestroy()
        {
            if (circuitRoot != null)
                Destroy(circuitRoot.gameObject);
        }
    }
}
