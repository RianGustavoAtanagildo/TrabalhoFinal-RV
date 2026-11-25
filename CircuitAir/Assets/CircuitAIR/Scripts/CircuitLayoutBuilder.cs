using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a simple AR circuit in the tracked image's local XY plane.
/// Units are metres. Place this component on a circuit prefab, or leave the prefab field empty
/// in CircuitARController to generate this example circuit automatically.
/// </summary>
public class CircuitLayoutBuilder : MonoBehaviour
{
    [Header("Route (metres in the QR image plane)")]
    [SerializeField] private Vector2[] centerLinePoints =
    {
        new Vector2(-0.075f, -0.050f),
        new Vector2(-0.075f,  0.045f),
        new Vector2(-0.035f,  0.045f),
        new Vector2(-0.035f, -0.015f),
        new Vector2( 0.005f, -0.015f),
        new Vector2( 0.005f,  0.055f),
        new Vector2( 0.050f,  0.055f),
        new Vector2( 0.050f, -0.045f),
        new Vector2( 0.078f, -0.045f)
    };

    [Header("Geometry")]
    [SerializeField, Min(0.004f)] private float corridorHalfWidth = 0.012f;
    [SerializeField, Min(0.001f)] private float wallThickness = 0.003f;
    [SerializeField, Min(0.005f)] private float wallDepth = 0.025f;
    [SerializeField, Min(0f)] private float visualOffset = 0.004f;
    [SerializeField, Min(0.0005f)] private float lineWidth = 0.0025f;

    [Header("Appearance")]
    [SerializeField] private Color centerLineColor = new Color(0.12f, 0.72f, 1f, 1f);
    [SerializeField] private Color borderColor = new Color(0.25f, 0.88f, 1f, 1f);
    [SerializeField] private Color startColor = new Color(0.2f, 1f, 0.45f, 1f);
    [SerializeField] private Color finishColor = new Color(1f, 0.75f, 0.15f, 1f);

    private bool built;
    private Material lineMaterial;

    private void Awake()
    {
        Build();
    }

    /// <summary>Public so a scene setup script can rebuild the generated route after changing points.</summary>
    public void Build()
    {
        if (built)
            return;

        built = true;
        if (centerLinePoints == null || centerLinePoints.Length < 2)
        {
            Debug.LogError("CircuitLayoutBuilder precisa de pelo menos dois pontos no caminho.", this);
            return;
        }

        Vector3[] centerPoints = ToVector3(centerLinePoints, visualOffset);
        CreateLine("Caminho luminoso", centerPoints, centerLineColor, lineWidth);

        Vector2[] leftBorder = CalculateOffsetPolyline(centerLinePoints, corridorHalfWidth);
        Vector2[] rightBorder = CalculateOffsetPolyline(centerLinePoints, -corridorHalfWidth);

        for (int i = 0; i < centerLinePoints.Length - 1; i++)
        {
            CreateWallSegment(leftBorder[i], leftBorder[i + 1], "Parede esquerda " + i);
            CreateWallSegment(rightBorder[i], rightBorder[i + 1], "Parede direita " + i);
        }

        CreateEndpointMarker("Início", centerLinePoints[0], startColor, false);
        CreateEndpointMarker("Chegada", centerLinePoints[centerLinePoints.Length - 1], finishColor, true);
    }

    private Vector2[] CalculateOffsetPolyline(Vector2[] points, float offset)
    {
        Vector2[] result = new Vector2[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 normal;

            if (i == 0)
            {
                Vector2 direction = (points[1] - points[0]).normalized;
                normal = new Vector2(-direction.y, direction.x);
                result[i] = points[i] + normal * offset;
            }
            else if (i == points.Length - 1)
            {
                Vector2 direction = (points[i] - points[i - 1]).normalized;
                normal = new Vector2(-direction.y, direction.x);
                result[i] = points[i] + normal * offset;
            }
            else
            {
                Vector2 previousDirection = (points[i] - points[i - 1]).normalized;
                Vector2 nextDirection = (points[i + 1] - points[i]).normalized;
                Vector2 previousNormal = new Vector2(-previousDirection.y, previousDirection.x);
                Vector2 nextNormal = new Vector2(-nextDirection.y, nextDirection.x);
                Vector2 miter = (previousNormal + nextNormal).normalized;
                float denominator = Vector2.Dot(miter, nextNormal);

                if (miter.sqrMagnitude < 0.001f || Mathf.Abs(denominator) < 0.2f)
                    result[i] = points[i] + nextNormal * offset;
                else
                {
                    float miterLength = Mathf.Min(Mathf.Abs(offset / denominator), Mathf.Abs(offset) * 3f);
                    result[i] = points[i] + miter * Mathf.Sign(offset) * miterLength;
                }
            }
        }

        return result;
    }

    private void CreateWallSegment(Vector2 start, Vector2 end, string objectName)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.0001f)
            return;

        Vector2 direction = delta / length;
        GameObject wall = new GameObject(objectName);
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = new Vector3((start.x + end.x) * 0.5f,
                                                    (start.y + end.y) * 0.5f,
                                                    visualOffset);
        wall.transform.localRotation = Quaternion.FromToRotation(
            Vector3.right, new Vector3(direction.x, direction.y, 0f));

        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(length + wallThickness, wallThickness, wallDepth);

        CircuitZone zone = wall.AddComponent<CircuitZone>();
        zone.zoneType = CircuitZoneType.Wall;
    }

    private void CreateEndpointMarker(string objectName, Vector2 point, Color color, bool isFinish)
    {
        GameObject marker = new GameObject(objectName);
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(point.x, point.y, visualOffset + 0.0005f);

        float radius = corridorHalfWidth * 0.72f;
        List<Vector3> ring = new List<Vector3>(25);
        const int segments = 24;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            ring.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        CreateLineOnObject(marker.transform, "Anel visual", ring.ToArray(), color, lineWidth * 1.5f);

        if (isFinish)
        {
            SphereCollider finishCollider = marker.AddComponent<SphereCollider>();
            finishCollider.isTrigger = true;
            finishCollider.radius = radius;
            CircuitZone zone = marker.AddComponent<CircuitZone>();
            zone.zoneType = CircuitZoneType.Finish;
        }
    }

    private void CreateLine(string objectName, Vector3[] positions, Color color, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        CreateLineOnObject(lineObject.transform, objectName, positions, color, width);
    }

    private void CreateLineOnObject(Transform parent, string objectName, Vector3[] positions, Color color, float width)
    {
        GameObject lineObject = parent == transform ? new GameObject(objectName) : parent.gameObject;
        if (lineObject.transform.parent == null && lineObject.transform != transform)
            lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
            line = lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.startColor = color;
        line.endColor = color;
        line.material = GetLineMaterial();
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogWarning("Não foi encontrado um shader simples para desenhar o circuito. Configure um material no prefab.", this);
            return null;
        }

        lineMaterial = new Material(shader);
        lineMaterial.name = "CircuitAIR_RuntimeLineMaterial";
        return lineMaterial;
    }

    private static Vector3[] ToVector3(Vector2[] points, float z)
    {
        Vector3[] result = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
            result[i] = new Vector3(points[i].x, points[i].y, z);
        return result;
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}
