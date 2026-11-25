using UnityEngine;

namespace CircuitAIR
{
    /// <summary>
    /// Monta o circuito de uma fase: fio luminoso, bases de início e chegada, bandeira,
    /// anel-guia de início e marcador para voltar ao fio. Fica no espaço do QR Code
    /// (X = direita, Y = para cima, Z = para longe do jogador).
    /// </summary>
    public sealed class CircuitWire : MonoBehaviour
    {
        private const float SampleSpacing = 0.002f;
        private const float BaseRadius = 0.022f;
        private const float BaseHeight = 0.006f;

        private CircuitTheme theme;
        private MeshRenderer wireRenderer;
        private Transform startGuide;
        private Transform rejoinMarker;
        private float flashUntil;
        private bool flashing;

        public CircuitLevel Level { get; private set; }
        public WirePath Path { get; private set; }

        /// <summary>Até onde (m ao longo do fio) a lupa pode ser encaixada para começar.</summary>
        public float StartZoneEnd { get; private set; }

        /// <summary>Ponto (m ao longo do fio) que conta como chegada.</summary>
        public float FinishDistance { get; private set; }

        /// <summary>Ponto (m ao longo do fio) onde fica o anel-guia de início.</summary>
        public float StartGuideDistance { get; private set; }

        public void Build(CircuitLevel level, CircuitTheme circuitTheme)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            Level = level;
            theme = circuitTheme;
            Path = WirePath.FromControlPoints(level.Points, level.CornerRadius, SampleSpacing);

            float[] control = Path.ControlPointDistances;
            StartGuideDistance = control[1] + 0.035f;
            StartZoneEnd = control[1] + 0.07f;
            FinishDistance = control[control.Length - 2] - 0.01f;

            wireRenderer = CreateMesh("Fio", TubeMesh.Build(Path.Points, level.WireRadius, 12), theme ? theme.Wire : null);
            if (theme != null && theme.WireGlow != null)
                CreateMesh("Brilho do fio", TubeMesh.Build(Path.Points, level.WireRadius * 1.9f, 10, 2), theme.WireGlow);

            Vector3 first = level.Points[0];
            Vector3 last = level.Points[level.Points.Length - 1];
            CreateBase("Base de início", first, theme ? theme.StartBase : null);
            Transform finish = CreateBase("Base de chegada", last, theme ? theme.FinishBase : null);
            CreateFlag(finish);

            startGuide = CreateRingMarker("Anel-guia de início", theme ? theme.StartGuide : null, level.RingRadius * 1.05f);
            PlaceRingMarker(startGuide, StartGuideDistance);
            rejoinMarker = CreateRingMarker("Marcador de volta ao fio", theme ? theme.RejoinMarker : null, level.RingRadius * 1.05f);
            rejoinMarker.gameObject.SetActive(false);
        }

        /// <summary>Pisca o fio em vermelho quando a lupa encosta.</summary>
        public void Flash(float seconds = 0.3f)
        {
            if (wireRenderer == null || theme == null || theme.WireHit == null)
                return;
            flashUntil = Time.unscaledTime + seconds;
            if (!flashing)
            {
                wireRenderer.sharedMaterial = theme.WireHit;
                flashing = true;
            }
        }

        public void SetStartGuideVisible(bool visible)
        {
            if (startGuide != null)
                startGuide.gameObject.SetActive(visible);
        }

        public void ShowRejoinMarker(float distance)
        {
            if (rejoinMarker == null)
                return;
            PlaceRingMarker(rejoinMarker, Mathf.Clamp(distance, 0f, Path.Length));
            rejoinMarker.gameObject.SetActive(true);
        }

        public void HideRejoinMarker()
        {
            if (rejoinMarker != null)
                rejoinMarker.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (flashing && Time.unscaledTime >= flashUntil)
            {
                flashing = false;
                if (wireRenderer != null && theme != null)
                    wireRenderer.sharedMaterial = theme.Wire;
            }

            // Anéis-guia pulsam devagar para chamar a atenção.
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.06f;
            if (startGuide != null && startGuide.gameObject.activeSelf)
                startGuide.localScale = Vector3.one * pulse;
            if (rejoinMarker != null && rejoinMarker.gameObject.activeSelf)
                rejoinMarker.localScale = Vector3.one * pulse;
        }

        private MeshRenderer CreateMesh(string objectName, Mesh mesh, Material material)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return meshRenderer;
        }

        private Transform CreateBase(string objectName, Vector3 position, Material material)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(position.x, 0f, position.z);

            var disc = CreateMesh("Disco", TubeMesh.Disc(BaseRadius, BaseHeight), material);
            disc.transform.SetParent(root, false);

            var halo = CreateMesh("Halo", TubeMesh.Torus(BaseRadius * 1.25f, 0.0018f, 40, 8), material);
            halo.transform.SetParent(root, false);
            halo.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }

        private void CreateFlag(Transform finishBase)
        {
            Vector3 top = new Vector3(0.012f, 0.055f, 0.012f);
            var pole = CreateMesh("Mastro", TubeMesh.Build(new[] { new Vector3(0.012f, BaseHeight, 0.012f), top }, 0.0012f, 8), theme ? theme.FinishBase : null);
            pole.transform.SetParent(finishBase, false);

            var flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flag.name = "Bandeira";
            Destroy(flag.GetComponent<Collider>());
            flag.transform.SetParent(finishBase, false);
            flag.transform.localPosition = top + new Vector3(0.014f, -0.009f, 0f);
            flag.transform.localScale = new Vector3(0.028f, 0.018f, 1f);
            var flagRenderer = flag.GetComponent<MeshRenderer>();
            flagRenderer.sharedMaterial = theme ? theme.Flag : null;
            flagRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Transform CreateRingMarker(string objectName, Material material, float radius)
        {
            var marker = CreateMesh(objectName, TubeMesh.Torus(radius, 0.0016f, 48, 8), material);
            return marker.transform;
        }

        private void PlaceRingMarker(Transform marker, float distance)
        {
            marker.localPosition = Path.PointAt(distance);
            marker.localRotation = Quaternion.LookRotation(Path.TangentAt(distance), Vector3.up);
        }
    }
}
