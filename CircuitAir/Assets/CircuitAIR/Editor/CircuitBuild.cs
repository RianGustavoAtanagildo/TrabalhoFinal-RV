using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CircuitAIR.EditorTools
{
    /// <summary>
    /// Gera o APK do Circuit AIR para Android.
    /// Menu: CircuitAIR/3. Gerar APK (Android). O arquivo sai em CircuitAir/Builds/Android/CircuitAIR.apk.
    /// </summary>
    public static class CircuitBuild
    {
        public const string ApkPath = "Builds/Android/CircuitAIR.apk";

        [MenuItem("CircuitAIR/3. Gerar APK (Android)")]
        static void BuildAndroidMenu()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUtility.DisplayDialog("Circuit AIR",
                        "A plataforma ativa não é Android. Trocar agora? (a Unity vai reimportar os recursos; depois use o menu de novo)",
                        "Trocar para Android", "Cancelar"))
                    return;
                SwitchToAndroid();
                return;
            }

            var result = BuildAndroid();
            EditorUtility.DisplayDialog("Circuit AIR", result, "OK");
            if (result.StartsWith("OK"))
                EditorUtility.RevealInFinder(Path.GetFullPath(ApkPath));
        }

        /// <summary>Troca a plataforma ativa para Android. Retorna uma mensagem de status.</summary>
        public static string SwitchToAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                return "ERRO: o módulo Android da Unity não está instalado (ou o Editor precisa ser reaberto depois da instalação).";
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                return "OK: a plataforma já é Android.";
            var ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            return ok ? "OK: plataforma trocada para Android." : "ERRO: não foi possível trocar a plataforma para Android.";
        }

        /// <summary>Gera o APK. Retorna "OK: ..." ou "ERRO: ...".</summary>
        public static string BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                return "ERRO: o módulo Android da Unity não está instalado (ou o Editor precisa ser reaberto depois da instalação).";

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                return "ERRO: nenhuma cena na lista de build. Rode CircuitAIR/2. Configurar projeto e cena.";

            // Sem o ARBackgroundRendererFeature no renderizador do Android, o celular mostra uma cor lisa no lugar da câmera.
            var setupReport = new System.Text.StringBuilder();
            CircuitProjectSetup.SetupArBackground(setupReport);
            Debug.Log("[CircuitBuild] " + setupReport.ToString().Trim());

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                var errors = report.steps
                    .SelectMany(step => step.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => m.content)
                    .Take(10);
                return $"ERRO: build {summary.result} com {summary.totalErrors} erro(s).\n" + string.Join("\n", errors);
            }

            var sizeMb = new FileInfo(ApkPath).Length / (1024.0 * 1024.0);
            return $"OK: {Path.GetFullPath(ApkPath)} ({sizeMb:0.0} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalWarnings} aviso(s)).";
        }

        /// <summary>Para linha de comando: unity build --target Android --execute-method CircuitAIR.EditorTools.CircuitBuild.BuildAndroidBatch</summary>
        public static void BuildAndroidBatch()
        {
            var result = BuildAndroid();
            Debug.Log("[CircuitBuild] " + result);
            if (Application.isBatchMode)
                EditorApplication.Exit(result.StartsWith("OK") ? 0 : 1);
        }
    }
}
