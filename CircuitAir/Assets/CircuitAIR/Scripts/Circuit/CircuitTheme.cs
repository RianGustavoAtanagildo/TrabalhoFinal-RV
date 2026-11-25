using UnityEngine;

namespace CircuitAIR
{
    /// <summary>
    /// Materiais e malhas usados pelo circuito e pela lupa. Fica num asset
    /// (Assets/CircuitAIR/CircuitAIR_Theme.asset) para a equipe trocar cores sem mexer no código.
    /// </summary>
    [CreateAssetMenu(menuName = "CircuitAIR/Tema", fileName = "CircuitAIR_Theme")]
    public sealed class CircuitTheme : ScriptableObject
    {
        [Header("Fio")]
        public Material Wire;
        public Material WireGlow;
        public Material WireHit;

        [Header("Bases e marcadores")]
        public Material StartBase;
        public Material FinishBase;
        public Material Flag;
        public Material StartGuide;
        public Material RejoinMarker;

        [Header("Lupa")]
        [Tooltip("Malha da lupa (aro + cabo). Submalha 0 = aro de metal, submalha 1 = cabo.")]
        public Mesh LupaMesh;
        public Material LupaMetal;
        public Material LupaHandle;

        [Tooltip("Contorno luminoso desenhado sobre o aro da lupa física.")]
        public Material LupaOverlay;

        [Tooltip("Raio do centro do aro na malha da lupa, em metros (aro de 88 a 100 mm no modelo).")]
        public float LupaMeshRingRadius = 0.094f;

        [Tooltip("Meia espessura do aro na malha, em metros.")]
        public float LupaMeshTubeRadius = 0.006f;
    }
}
