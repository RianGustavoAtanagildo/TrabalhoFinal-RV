using UnityEngine;

namespace CircuitAIR
{
    /// <summary>Configuração de uma fase (circuito).</summary>
    [System.Serializable]
    public sealed class CircuitLevel
    {
        public string Name;
        public string Description;

        [Tooltip("Raio da argola da lupa (centro do aro), em metros. Menor = mais difícil.")]
        public float RingRadius;

        [Tooltip("Raio do fio, em metros.")]
        public float WireRadius;

        [Tooltip("Raio das curvas do fio, em metros.")]
        public float CornerRadius;

        [Tooltip("Pontos do fio, em metros, no espaço do QR Code: X = direita, Y = para cima, Z = para longe do jogador.")]
        public Vector3[] Points;

        public CircuitLevel(string name, string description, float ringRadius, float wireRadius, float cornerRadius, params Vector3[] points)
        {
            Name = name;
            Description = description;
            RingRadius = ringRadius;
            WireRadius = wireRadius;
            CornerRadius = cornerRadius;
            Points = points;
        }
    }

    /// <summary>
    /// As três fases do Circuit AIR. O primeiro e o último ponto ficam na mesa (Y = 0): são as bases
    /// de início (verde) e de chegada (bandeira). Medidas pensadas para um QR Code impresso com 10 cm.
    /// </summary>
    public static class CircuitLevels
    {
        public static readonly CircuitLevel[] All =
        {
            new CircuitLevel(
                "Fase 1 · Aquecimento",
                "Curvas suaves pela mesa.",
                ringRadius: 0.030f, wireRadius: 0.0030f, cornerRadius: 0.040f,
                new Vector3(-0.16f, 0.000f, 0.00f),
                new Vector3(-0.16f, 0.070f, 0.00f),
                new Vector3(-0.08f, 0.070f, 0.00f),
                new Vector3(-0.03f, 0.070f, 0.07f),
                new Vector3( 0.04f, 0.070f, 0.07f),
                new Vector3( 0.09f, 0.070f, 0.00f),
                new Vector3( 0.16f, 0.070f, 0.00f),
                new Vector3( 0.16f, 0.000f, 0.00f)),

            new CircuitLevel(
                "Fase 2 · Ondas",
                "O fio sobe e desce.",
                ringRadius: 0.024f, wireRadius: 0.0030f, cornerRadius: 0.035f,
                new Vector3(-0.17f, 0.000f, -0.02f),
                new Vector3(-0.17f, 0.060f, -0.02f),
                new Vector3(-0.10f, 0.060f, -0.02f),
                new Vector3(-0.05f, 0.110f,  0.01f),
                new Vector3( 0.00f, 0.110f,  0.07f),
                new Vector3( 0.05f, 0.060f,  0.09f),
                new Vector3( 0.10f, 0.060f,  0.04f),
                new Vector3( 0.12f, 0.110f, -0.01f),
                new Vector3( 0.17f, 0.110f, -0.01f),
                new Vector3( 0.17f, 0.000f, -0.01f)),

            new CircuitLevel(
                "Fase 3 · Desafio",
                "Zigue-zague em 3D.",
                ringRadius: 0.019f, wireRadius: 0.0028f, cornerRadius: 0.028f,
                new Vector3(-0.18f, 0.000f, -0.03f),
                new Vector3(-0.18f, 0.060f, -0.03f),
                new Vector3(-0.12f, 0.060f, -0.03f),
                new Vector3(-0.09f, 0.120f,  0.02f),
                new Vector3(-0.04f, 0.120f,  0.06f),
                new Vector3( 0.00f, 0.065f,  0.10f),
                new Vector3( 0.05f, 0.065f,  0.10f),
                new Vector3( 0.07f, 0.120f,  0.04f),
                new Vector3( 0.07f, 0.120f, -0.03f),
                new Vector3( 0.12f, 0.070f, -0.05f),
                new Vector3( 0.18f, 0.070f,  0.02f),
                new Vector3( 0.18f, 0.000f,  0.02f)),
        };

        public static int Count => All.Length;

        public static CircuitLevel Get(int index)
        {
            return All[Mathf.Clamp(index, 0, All.Length - 1)];
        }
    }
}
