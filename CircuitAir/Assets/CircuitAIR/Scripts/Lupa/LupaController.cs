using UnityEngine;

namespace CircuitAIR
{
    public enum LupaMode
    {
        /// <summary>A lupa virtual fica presa na frente da câmera: o jogador move o celular.</summary>
        Virtual = 0,

        /// <summary>A lupa física (real) é reconhecida pelo marcador LupaMarker preso nela. Modo principal.</summary>
        PhysicalMarker = 1
    }

    /// <summary>
    /// A lupa do jogo. Informa ao jogo onde está a argola (centro, direção e raio) em coordenadas do mundo.
    ///
    /// Modo físico (padrão): a câmera reconhece a bandeirinha presa em pé no topo do aro real (marcador
    /// LupaMarker na frente e LupaMarkerVerso atrás); a argola fica num deslocamento fixo em relação ao
    /// marcador. Esse deslocamento é calibrado na tela "Ajustar lupa" e salvo no aparelho. Um contorno
    /// virtual aparece sobre o aro real (vermelho ao encostar, verde no fio).
    ///
    /// Modo virtual: o modelo Lupa_CircuitAIR fica preso na frente da câmera e o jogador move o celular.
    /// </summary>
    public sealed class LupaController : MonoBehaviour
    {
        [SerializeField] private CircuitTheme theme;
        [SerializeField] private Camera arCamera;
        [SerializeField] private LupaMode mode = LupaMode.PhysicalMarker;

        [Header("Lupa física (marcador LupaMarker)")]
        [Tooltip("Centro do aro em relação ao centro do marcador, em metros, no espaço da imagem: X = direita da imagem, Y = saindo da imagem, Z = topo da imagem.")]
        [SerializeField] private Vector3 markerToRingOffset = DefaultMarkerOffset;
        [Tooltip("Giro do aro em torno do eixo vertical da imagem (graus). 90 = bandeirinha perpendicular ao aro (padrão); 0 = marcador colado no mesmo plano do aro.")]
        [SerializeField] private float markerToRingTilt = DefaultMarkerTilt;
        [Tooltip("Raio do aro real até o meio da parede, em metros.")]
        [SerializeField] private float physicalRingRadius = DefaultPhysicalRadius;
        [Tooltip("Meia espessura da parede do aro real, em metros.")]
        [SerializeField] private float physicalTubeRadius = DefaultPhysicalTubeRadius;
        [Tooltip("Meia largura da faixa do aro real (ao longo do eixo), em metros. 0 para aro de arame.")]
        [SerializeField] private float physicalHalfDepth = DefaultPhysicalHalfDepth;
        [Tooltip("Tempo (s) que a lupa continua valendo depois que o marcador some da câmera (tremida, desfoque).")]
        [SerializeField] private float markerGraceSeconds = 0.3f;
        [Tooltip("Suavização do movimento do marcador (maior = segue mais rápido, menor = mais estável).")]
        [SerializeField] private float markerSmoothing = 30f;

        [Header("Lupa virtual (presa à câmera)")]
        [Tooltip("Posição do centro da argola em relação à câmera, em metros (X direita, Y cima, Z frente).")]
        [SerializeField] private Vector3 cameraOffset = DefaultCameraOffset;
        [Tooltip("Inclina a argola para cima, em graus, para ficar alinhada a um fio horizontal quando o celular aponta para a mesa.")]
        [SerializeField, Range(0f, 60f)] private float ringTiltDegrees = 20f;
        [Tooltip("Direção do cabo vista pela câmera (para baixo e à direita, como na mão do jogador).")]
        [SerializeField] private Vector3 handleDirection = new Vector3(0.45f, -1f, -0.3f);

        [Header("Cores de resposta")]
        [SerializeField] private Color normalColor = new Color(0.35f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color touchColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color onWireColor = new Color(0.4f, 1f, 0.55f, 0.95f);

        /// <summary>Posição padrão da argola virtual em relação à câmera (m).</summary>
        public static readonly Vector3 DefaultCameraOffset = new Vector3(0f, -0.02f, 0.15f);

        // Aro da equipe: 10 cm de diâmetro por fora, parede de 2 mm, faixa de 1,5 cm de largura.
        public const float DefaultPhysicalRadius = 0.049f;
        public const float DefaultPhysicalTubeRadius = 0.001f;
        public const float DefaultPhysicalHalfDepth = 0.0075f;

        /// <summary>Lado do marcador impresso da lupa (frente e verso), em metros.</summary>
        public const float MarkerSizeMeters = 0.06f;

        /// <summary>Margem branca da bandeirinha entre a borda de baixo do marcador e o topo do aro, em metros.</summary>
        public const float FlagBottomMarginMeters = 0.01f;

        /// <summary>
        /// Padrão: bandeirinha em pé no topo do aro, com o topo da imagem para cima. O centro do aro fica
        /// abaixo do centro do marcador: raio externo (5 cm) + margem (1 cm) + meio marcador (3 cm).
        /// </summary>
        public static readonly Vector3 DefaultMarkerOffset =
            new Vector3(0f, 0f, -(DefaultPhysicalRadius + DefaultPhysicalTubeRadius + FlagBottomMarginMeters + MarkerSizeMeters * 0.5f));

        /// <summary>Padrão: bandeirinha perpendicular ao aro, para ficar de frente para a câmera com o fio indo da esquerda para a direita.</summary>
        public const float DefaultMarkerTilt = 90f;

        /// <summary>Rotação do aro em relação ao marcador: giro em torno do eixo vertical da imagem (Z da imagem).</summary>
        public static Quaternion MarkerToRingRotation(float tilt) => Quaternion.Euler(0f, 0f, tilt);

        // "lupa2": a calibração salva com o marcador antigo (5 cm, no cabo) não vale mais.
        private const string PrefsPrefix = "circuitair.lupa2.";

        private Transform model;
        private MeshRenderer modelRenderer;
        private Transform overlay;
        private MeshRenderer overlayRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float levelRingRadius = 0.03f;
        private Pose markerPose;
        private bool hasMarkerPose;
        private float lastMarkerTime = -100f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public LupaMode Mode => mode;

        /// <summary>Raio do centro do aro, em metros.</summary>
        public float RingRadius => mode == LupaMode.Virtual ? levelRingRadius : physicalRingRadius;

        /// <summary>Meia espessura do aro, em metros (usada para saber se encostou no fio).</summary>
        public float RingTubeRadius
        {
            get
            {
                if (mode == LupaMode.PhysicalMarker)
                    return Mathf.Max(0.001f, physicalTubeRadius);
                float ratio = theme != null && theme.LupaMeshRingRadius > 0f ? theme.LupaMeshTubeRadius / theme.LupaMeshRingRadius : 0.064f;
                return Mathf.Max(0.0015f, RingRadius * ratio);
            }
        }

        /// <summary>Meia largura da faixa do aro (ao longo do eixo), em metros. 0 na lupa virtual.</summary>
        public float RingHalfDepth => mode == LupaMode.PhysicalMarker ? Mathf.Max(0f, physicalHalfDepth) : 0f;

        /// <summary>Verdadeiro quando o jogo sabe onde a lupa está.</summary>
        public bool HasPose => mode == LupaMode.Virtual
            ? arCamera != null
            : hasMarkerPose && Time.time - lastMarkerTime <= markerGraceSeconds;

        /// <summary>
        /// Verdadeiro quando a posição da lupa é atual (marcador visto agora há pouco). O jogo só avalia toques
        /// com posição atual: com a posição "congelada" de um marcador perdido, um salto ao reaparecer viraria erro falso.
        /// </summary>
        public bool HasFreshPose => mode == LupaMode.Virtual
            ? arCamera != null
            : hasMarkerPose && Time.time - lastMarkerTime <= 0.12f;

        /// <summary>Quanto tempo (s) o marcador ficou sem ser visto (0 se está sendo visto).</summary>
        public float SecondsSinceMarker => Mathf.Max(0f, Time.time - lastMarkerTime);

        public Vector3 RingCenter => transform.position;
        public Vector3 RingNormal => transform.up;

        public Vector3 MarkerOffset => markerToRingOffset;
        public float MarkerTilt => markerToRingTilt;
        public float PhysicalRingRadius => physicalRingRadius;

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;
            propertyBlock = new MaterialPropertyBlock();
            LoadCalibration();
            BuildModel();
            BuildOverlay();
            ApplyMode();
        }

        public void SetCamera(Camera cameraToFollow)
        {
            arCamera = cameraToFollow;
            ApplyMode();
        }

        public void SetMode(LupaMode newMode)
        {
            mode = newMode;
            ApplyMode();
        }

        public void SetLevel(CircuitLevel level)
        {
            levelRingRadius = level.RingRadius;
            ApplyScale();
        }

        // ---------- Calibração da lupa física ----------

        /// <summary>Move o aro em relação ao marcador (m, no espaço da imagem) e salva.</summary>
        public void NudgeMarkerOffset(Vector3 delta)
        {
            markerToRingOffset += delta;
            SaveCalibration();
        }

        /// <summary>Gira o aro 90° em relação ao marcador (para marcador preso "em pé" ou deitado) e salva.</summary>
        public void RotateMarkerTilt()
        {
            markerToRingTilt = Mathf.Repeat(markerToRingTilt + 90f, 360f);
            SaveCalibration();
        }

        /// <summary>Muda o raio do aro real (m) e salva.</summary>
        public void NudgePhysicalRadius(float delta)
        {
            physicalRingRadius = Mathf.Clamp(physicalRingRadius + delta, 0.008f, 0.08f);
            SaveCalibration();
            ApplyScale();
        }

        public void ResetCalibration()
        {
            markerToRingOffset = DefaultMarkerOffset;
            markerToRingTilt = DefaultMarkerTilt;
            physicalRingRadius = DefaultPhysicalRadius;
            SaveCalibration();
            ApplyScale();
        }

        private void LoadCalibration()
        {
            if (!PlayerPrefs.HasKey(PrefsPrefix + "radius"))
                return;
            markerToRingOffset = new Vector3(
                PlayerPrefs.GetFloat(PrefsPrefix + "x", markerToRingOffset.x),
                PlayerPrefs.GetFloat(PrefsPrefix + "y", markerToRingOffset.y),
                PlayerPrefs.GetFloat(PrefsPrefix + "z", markerToRingOffset.z));
            markerToRingTilt = PlayerPrefs.GetFloat(PrefsPrefix + "tilt", markerToRingTilt);
            physicalRingRadius = PlayerPrefs.GetFloat(PrefsPrefix + "radius", physicalRingRadius);
        }

        private void SaveCalibration()
        {
            PlayerPrefs.SetFloat(PrefsPrefix + "x", markerToRingOffset.x);
            PlayerPrefs.SetFloat(PrefsPrefix + "y", markerToRingOffset.y);
            PlayerPrefs.SetFloat(PrefsPrefix + "z", markerToRingOffset.z);
            PlayerPrefs.SetFloat(PrefsPrefix + "tilt", markerToRingTilt);
            PlayerPrefs.SetFloat(PrefsPrefix + "radius", physicalRingRadius);
            PlayerPrefs.Save();
        }

        // ---------- Rastreamento ----------

        /// <summary>Chamado pelo rastreamento de imagem quando o marcador da lupa física é visto.</summary>
        public void SetMarkerPose(Pose pose, bool tracking)
        {
            if (!tracking)
                return;

            // Depois de perder o marcador, usa direto a posição nova (sem "deslizar" da posição antiga).
            float sinceLastUpdate = Time.time - lastMarkerTime;
            if (!hasMarkerPose || sinceLastUpdate > 0.15f)
            {
                markerPose = pose;
            }
            else
            {
                // A suavização usa o intervalo entre leituras do marcador (não o tempo do quadro): o AR pode
                // atualizar a imagem bem menos vezes por segundo do que o app desenha, e aí o aro ficaria para trás.
                float t = 1f - Mathf.Exp(-markerSmoothing * Mathf.Max(sinceLastUpdate, Time.deltaTime));
                markerPose = new Pose(Vector3.Lerp(markerPose.position, pose.position, t),
                                      Quaternion.Slerp(markerPose.rotation, pose.rotation, t));
            }

            hasMarkerPose = true;
            lastMarkerTime = Time.time;
        }

        /// <summary>Muda a cor do aro: vermelho ao encostar, verde quando está no fio.</summary>
        public void SetFeedback(bool touching, bool onWire)
        {
            if (mode == LupaMode.PhysicalMarker)
            {
                if (overlayRenderer == null)
                    return;
                Color color = touching ? touchColor : onWire ? onWireColor : normalColor;
                overlayRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                overlayRenderer.SetPropertyBlock(propertyBlock);
                return;
            }

            if (modelRenderer == null || modelRenderer.sharedMaterials.Length == 0)
                return;
            Material metal = modelRenderer.sharedMaterials[0];
            Color baseColor = metal != null && metal.HasProperty(BaseColorId) ? metal.GetColor(BaseColorId) : Color.white;
            Color tint = touching ? touchColor : onWire ? Color.Lerp(baseColor, onWireColor, 0.35f) : baseColor;
            modelRenderer.GetPropertyBlock(propertyBlock, 0);
            propertyBlock.SetColor(BaseColorId, tint);
            modelRenderer.SetPropertyBlock(propertyBlock, 0);
        }

        private void LateUpdate()
        {
            if (mode != LupaMode.PhysicalMarker)
                return;

            bool visible = HasPose;
            if (overlay != null && overlay.gameObject.activeSelf != visible)
                overlay.gameObject.SetActive(visible);
            if (hasMarkerPose)
            {
                transform.SetPositionAndRotation(
                    markerPose.position + markerPose.rotation * markerToRingOffset,
                    markerPose.rotation * MarkerToRingRotation(markerToRingTilt));
            }
        }

        private void ApplyMode()
        {
            if (mode == LupaMode.Virtual)
            {
                if (arCamera != null)
                {
                    transform.SetParent(arCamera.transform, false);
                    transform.localPosition = cameraOffset;
                    transform.localRotation = VirtualRotation();
                }
                if (model != null)
                    model.gameObject.SetActive(true);
                if (overlay != null)
                    overlay.gameObject.SetActive(false);
            }
            else
            {
                transform.SetParent(null, true);
                if (model != null)
                    model.gameObject.SetActive(false);
                if (overlay != null)
                    overlay.gameObject.SetActive(HasPose);
            }

            ApplyScale();
        }

        /// <summary>
        /// Rotação da lupa virtual: o eixo Y do modelo (normal do aro) aponta para a frente da câmera,
        /// inclinado <see cref="ringTiltDegrees"/> para cima; o cabo (eixo -Z do modelo) vai para baixo.
        /// </summary>
        private Quaternion VirtualRotation()
        {
            Vector3 normal = Quaternion.Euler(-ringTiltDegrees, 0f, 0f) * Vector3.forward;
            Vector3 handle = handleDirection.sqrMagnitude > 1e-6f ? handleDirection.normalized : Vector3.down;
            handle = handle - Vector3.Dot(handle, normal) * normal;
            if (handle.sqrMagnitude < 1e-6f)
                handle = Vector3.down;
            handle.Normalize();
            return Quaternion.LookRotation(-handle, normal);
        }

        private void ApplyScale()
        {
            if (model != null)
            {
                float meshRadius = theme != null && theme.LupaMesh != null ? theme.LupaMeshRingRadius : 1f;
                model.localScale = Vector3.one * (levelRingRadius / meshRadius);
            }
            if (overlay != null)
            {
                // O toro tem tubo de 0,07 (raio 1). No eixo do aro (Z da malha) estica até a largura da faixa real.
                float axial = Mathf.Max(physicalRingRadius, physicalHalfDepth / 0.07f);
                overlay.localScale = new Vector3(physicalRingRadius, physicalRingRadius, axial);
            }
        }

        private void BuildModel()
        {
            var modelObject = new GameObject("Modelo da lupa (virtual)");
            model = modelObject.transform;
            model.SetParent(transform, false);

            var filter = modelObject.AddComponent<MeshFilter>();
            modelRenderer = modelObject.AddComponent<MeshRenderer>();
            modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (theme != null && theme.LupaMesh != null)
            {
                filter.sharedMesh = theme.LupaMesh;
                modelRenderer.sharedMaterials = new[] { theme.LupaMetal, theme.LupaHandle };
            }
            else
            {
                // Sem o modelo: argola simples de raio 1 (escalada pelo raio da fase), deitada no plano XZ.
                filter.sharedMesh = TubeMesh.Torus(1f, 0.064f, 48, 10);
                model.localRotation = Quaternion.Euler(90f, 0f, 0f);
                modelRenderer.sharedMaterials = new[] { theme != null ? theme.LupaMetal : null };
            }
        }

        /// <summary>Contorno luminoso desenhado sobre o aro da lupa real (modo físico).</summary>
        private void BuildOverlay()
        {
            var overlayObject = new GameObject("Contorno da lupa (física)");
            overlay = overlayObject.transform;
            overlay.SetParent(transform, false);
            overlayObject.AddComponent<MeshFilter>().sharedMesh = TubeMesh.Torus(1f, 0.07f, 64, 10);
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlayRenderer.sharedMaterial = theme != null ? (theme.LupaOverlay != null ? theme.LupaOverlay : theme.StartGuide) : null;
            // O toro é gerado no plano XY; deita no plano XZ para a normal ficar no eixo Y da lupa.
            overlay.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetFeedback(false, false);
        }
    }
}
