using UnityEngine;

namespace CircuitAIR
{
    /// <summary>Recordes por fase, salvos no aparelho (PlayerPrefs).</summary>
    public static class CircuitRecords
    {
        private static string TimeKey(int level) => $"circuitair.best.{level}.time";
        private static string ErrorsKey(int level) => $"circuitair.best.{level}.errors";

        public static bool HasRecord(int level) => PlayerPrefs.HasKey(TimeKey(level));

        public static float BestTime(int level) => PlayerPrefs.GetFloat(TimeKey(level), -1f);

        public static int BestErrors(int level) => PlayerPrefs.GetInt(ErrorsKey(level), -1);

        /// <summary>
        /// Salva se for o melhor resultado da fase. Vale menos erros; com os mesmos erros, menor tempo.
        /// Retorna verdadeiro quando é um novo recorde.
        /// </summary>
        public static bool TrySave(int level, float seconds, int errors)
        {
            if (HasRecord(level))
            {
                int bestErrors = BestErrors(level);
                float bestTime = BestTime(level);
                bool better = errors < bestErrors || (errors == bestErrors && seconds < bestTime);
                if (!better)
                    return false;
            }

            PlayerPrefs.SetFloat(TimeKey(level), seconds);
            PlayerPrefs.SetInt(ErrorsKey(level), errors);
            PlayerPrefs.Save();
            return true;
        }

        public static void ClearAll(int levelCount)
        {
            for (int i = 0; i < levelCount; i++)
            {
                PlayerPrefs.DeleteKey(TimeKey(i));
                PlayerPrefs.DeleteKey(ErrorsKey(i));
            }
            PlayerPrefs.Save();
        }
    }
}
