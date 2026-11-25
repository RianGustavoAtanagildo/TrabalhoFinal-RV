using System.Collections.Generic;
using UnityEngine;

namespace CircuitAIR
{
    /// <summary>
    /// Caminho do fio do circuito: uma polilinha densa e uniforme (em metros),
    /// gerada a partir dos pontos de controle de uma fase, com cantos arredondados.
    /// Classe pura (sem MonoBehaviour) para poder ser testada no Test Runner.
    /// </summary>
    public sealed class WirePath
    {
        /// <summary>Pontos amostrados ao longo do fio, no espaço local do circuito.</summary>
        public Vector3[] Points { get; }

        /// <summary>Distância acumulada (comprimento de arco) de cada ponto, em metros.</summary>
        public float[] Distances { get; }

        /// <summary>Comprimento total do fio, em metros.</summary>
        public float Length { get; }

        /// <summary>Comprimento de arco aproximado de cada ponto de controle.</summary>
        public float[] ControlPointDistances { get; }

        private WirePath(Vector3[] points, float[] distances, float[] controlPointDistances)
        {
            Points = points;
            Distances = distances;
            Length = distances[distances.Length - 1];
            ControlPointDistances = controlPointDistances;
        }

        /// <summary>
        /// Cria o caminho a partir dos pontos de controle. Cada canto interno é substituído
        /// por uma curva (Bézier quadrática) de raio aproximado <paramref name="cornerRadius"/>.
        /// </summary>
        public static WirePath FromControlPoints(IList<Vector3> controlPoints, float cornerRadius, float sampleSpacing)
        {
            if (controlPoints == null || controlPoints.Count < 2)
                throw new System.ArgumentException("O fio precisa de pelo menos dois pontos de controle.");

            sampleSpacing = Mathf.Max(0.0005f, sampleSpacing);
            var rounded = new List<Vector3>(controlPoints.Count * 16) { controlPoints[0] };

            for (int i = 1; i < controlPoints.Count - 1; i++)
            {
                Vector3 a = controlPoints[i - 1];
                Vector3 b = controlPoints[i];
                Vector3 c = controlPoints[i + 1];
                float lengthIn = Vector3.Distance(a, b);
                float lengthOut = Vector3.Distance(b, c);
                Vector3 dirIn = lengthIn > 1e-6f ? (b - a) / lengthIn : Vector3.zero;
                Vector3 dirOut = lengthOut > 1e-6f ? (c - b) / lengthOut : Vector3.zero;

                float radius = Mathf.Min(cornerRadius, lengthIn * 0.45f, lengthOut * 0.45f);
                if (radius <= 1e-5f || Vector3.Dot(dirIn, dirOut) > 0.9995f)
                {
                    rounded.Add(b);
                    continue;
                }

                Vector3 start = b - dirIn * radius;
                Vector3 end = b + dirOut * radius;
                const int cornerSteps = 14;
                for (int k = 0; k <= cornerSteps; k++)
                {
                    float t = k / (float)cornerSteps;
                    float u = 1f - t;
                    rounded.Add(u * u * start + 2f * u * t * b + t * t * end);
                }
            }

            rounded.Add(controlPoints[controlPoints.Count - 1]);

            // Reamostragem uniforme.
            var points = new List<Vector3>(256) { rounded[0] };
            var distances = new List<float>(256) { 0f };
            float travelled = 0f;
            float nextSample = sampleSpacing;

            for (int i = 0; i < rounded.Count - 1; i++)
            {
                Vector3 p0 = rounded[i];
                Vector3 p1 = rounded[i + 1];
                float segment = Vector3.Distance(p0, p1);
                if (segment < 1e-7f)
                    continue;

                while (travelled + segment >= nextSample)
                {
                    float f = (nextSample - travelled) / segment;
                    points.Add(Vector3.Lerp(p0, p1, f));
                    distances.Add(nextSample);
                    nextSample += sampleSpacing;
                }

                travelled += segment;
            }

            if (travelled - distances[distances.Count - 1] > sampleSpacing * 0.05f)
            {
                points.Add(rounded[rounded.Count - 1]);
                distances.Add(travelled);
            }
            else
            {
                points[points.Count - 1] = rounded[rounded.Count - 1];
                distances[distances.Count - 1] = travelled;
            }

            Vector3[] pointArray = points.ToArray();
            float[] distanceArray = distances.ToArray();

            var controlDistances = new float[controlPoints.Count];
            for (int i = 0; i < controlPoints.Count; i++)
                controlDistances[i] = distanceArray[NearestIndex(pointArray, controlPoints[i])];
            controlDistances[0] = 0f;
            controlDistances[controlPoints.Count - 1] = distanceArray[distanceArray.Length - 1];

            return new WirePath(pointArray, distanceArray, controlDistances);
        }

        /// <summary>Ponto do fio na distância <paramref name="distance"/> (em metros) a partir do início.</summary>
        public Vector3 PointAt(float distance)
        {
            if (distance <= 0f)
                return Points[0];
            if (distance >= Length)
                return Points[Points.Length - 1];

            int high = System.Array.BinarySearch(Distances, distance);
            if (high >= 0)
                return Points[high];

            high = ~high;
            int low = high - 1;
            float f = Mathf.InverseLerp(Distances[low], Distances[high], distance);
            return Vector3.Lerp(Points[low], Points[high], f);
        }

        /// <summary>Direção (tangente) do fio na distância informada.</summary>
        public Vector3 TangentAt(float distance)
        {
            float step = Mathf.Max(0.002f, Length * 0.002f);
            Vector3 a = PointAt(distance - step);
            Vector3 b = PointAt(distance + step);
            Vector3 d = b - a;
            return d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
        }

        private static int NearestIndex(Vector3[] points, Vector3 target)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = (points[i] - target).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
