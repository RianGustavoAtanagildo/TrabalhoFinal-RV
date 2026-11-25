using UnityEngine;

namespace CircuitAIR
{
    /// <summary>Resultado da consulta "argola da lupa x fio".</summary>
    public struct RingQuery
    {
        /// <summary>Menor distância entre o eixo do fio e a circunferência central da argola (m).</summary>
        public float MinDistance;

        /// <summary>Índice do ponto do fio mais próximo da argola.</summary>
        public int NearestIndex;

        /// <summary>Verdadeiro quando o fio atravessa o interior da argola (a lupa está "no fio").</summary>
        public bool Threaded;

        /// <summary>Posição ao longo do fio (m) onde o fio atravessa a argola. Válido só se <see cref="Threaded"/>.</summary>
        public float ThreadDistance;

        /// <summary>Distância entre o centro da argola e o ponto onde o fio a atravessa (m).</summary>
        public float CrossingOffset;
    }

    /// <summary>
    /// Geometria do jogo: detecta se a argola da lupa encosta no fio e se o fio passa por dentro dela.
    /// É analítico (não usa a física da Unity), então funciona igual em qualquer taxa de quadros
    /// e pode ser testado sem rodar a cena.
    /// </summary>
    public static class WireRingMath
    {
        /// <summary>Distância de um ponto até a circunferência (centro c, normal n, raio R).</summary>
        public static float DistanceToCircle(Vector3 point, Vector3 center, Vector3 normal, float radius)
        {
            Vector3 d = point - center;
            float height = Vector3.Dot(d, normal);
            Vector3 radial = d - height * normal;
            float radialDistance = radial.magnitude - radius;
            return Mathf.Sqrt(height * height + radialDistance * radialDistance);
        }

        /// <summary>
        /// Consulta a argola contra o fio. Todos os vetores devem estar no mesmo espaço do <paramref name="path"/>.
        /// <paramref name="progressHint"/> escolhe, se o fio cruzar a argola mais de uma vez,
        /// o cruzamento mais perto do progresso atual do jogador.
        /// </summary>
        public static RingQuery Query(WirePath path, Vector3 center, Vector3 normal, float radius, float progressHint)
        {
            normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.forward;
            Vector3[] points = path.Points;
            float[] distances = path.Distances;

            var result = new RingQuery
            {
                MinDistance = float.MaxValue,
                NearestIndex = 0,
                Threaded = false,
                ThreadDistance = 0f,
                CrossingOffset = float.MaxValue
            };

            float bestHintDelta = float.MaxValue;
            float previousSide = Vector3.Dot(points[0] - center, normal);

            for (int i = 0; i < points.Length; i++)
            {
                float distance = DistanceToCircle(points[i], center, normal, radius);
                if (distance < result.MinDistance)
                {
                    result.MinDistance = distance;
                    result.NearestIndex = i;
                }

                if (i == 0)
                    continue;

                float side = Vector3.Dot(points[i] - center, normal);
                bool crosses = (previousSide <= 0f && side > 0f) || (previousSide > 0f && side <= 0f);
                if (crosses)
                {
                    float f = previousSide / (previousSide - side);
                    Vector3 crossing = Vector3.Lerp(points[i - 1], points[i], f);
                    float offset = (crossing - center).magnitude;
                    if (offset < radius)
                    {
                        float along = Mathf.Lerp(distances[i - 1], distances[i], f);
                        float hintDelta = Mathf.Abs(along - progressHint);
                        if (hintDelta < bestHintDelta)
                        {
                            bestHintDelta = hintDelta;
                            result.Threaded = true;
                            result.ThreadDistance = along;
                            result.CrossingOffset = offset;
                        }
                    }
                }

                previousSide = side;
            }

            return result;
        }

        /// <summary>
        /// Verdadeiro quando a argola encosta no fio: a distância entre os eixos é menor
        /// que a soma do raio do fio com a espessura da argola.
        /// </summary>
        public static bool IsTouching(in RingQuery query, float wireRadius, float ringTubeRadius)
        {
            return query.MinDistance <= wireRadius + ringTubeRadius;
        }
    }
}
