using UnityEngine;

namespace CircuitAIR
{
    /// <summary>
    /// Sons e vibração do jogo. Os sons são gerados por código (sem arquivos de áudio),
    /// imitando o zumbido do "jogo do fio" quando a argola encosta.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class CircuitFeedback : MonoBehaviour
    {
        [SerializeField] private bool sound = true;
        [SerializeField] private bool vibration = true;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource source;
        private AudioClip buzz;
        private AudioClip leftWire;
        private AudioClip start;
        private AudioClip finish;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            const int rate = 44100;
            buzz = Tone("Zumbido", rate, 0.22f, t => Square(t * 140f) * 0.5f + Square(t * 210f) * 0.25f);
            leftWire = Tone("Saiu do fio", rate, 0.35f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 180f, t / 0.35f) * t));
            start = Tone("Início", rate, 0.30f, t => Mathf.Sin(2f * Mathf.PI * (t < 0.14f ? 660f : 990f) * t));
            finish = Tone("Chegada", rate, 0.60f, t => Mathf.Sin(2f * Mathf.PI * (t < 0.15f ? 523f : t < 0.30f ? 659f : t < 0.45f ? 784f : 1047f) * t));
        }

        public void PlayError(ErrorKind kind)
        {
            Play(kind == ErrorKind.Touch ? buzz : leftWire);
            Vibrate();
        }

        public void PlayStart() => Play(start);

        public void PlayFinish() => Play(finish);

        private void Play(AudioClip clip)
        {
            if (sound && clip != null)
                source.PlayOneShot(clip, volume);
        }

        private void Vibrate()
        {
            if (!vibration)
                return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;

        private static AudioClip Tone(string clipName, int rate, float seconds, System.Func<float, float> wave)
        {
            int samples = Mathf.CeilToInt(rate * seconds);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((seconds - t) / 0.05f);
                data[i] = wave(t) * envelope * 0.6f;
            }
            AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
