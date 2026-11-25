using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CircuitAIR.EditorTools
{
    /// <summary>
    /// Gera o app do Circuit AIR.
    /// - Android: menu CircuitAIR/3. Gerar APK (Android) → CircuitAir/Builds/Android/CircuitAIR.apk.
    /// - iOS: menu CircuitAIR/4. Gerar projeto do Xcode (iOS) → CircuitAir/Builds/iOS/CircuitAIR-Xcode
    ///   (o app do iPhone só sai desse projeto, num Mac com Xcode).
    /// </summary>
    public static class CircuitBuild
    {
        public const string ApkPath = "Builds/Android/CircuitAIR.apk";
        public const string XcodePath = "Builds/iOS/CircuitAIR-Xcode";

        [MenuItem("CircuitAIR/3. Gerar APK (Android)")]
        static void BuildAndroidMenu() => BuildFromMenu(BuildTargetGroup.Android, BuildTarget.Android, BuildAndroid, ApkPath);

        [MenuItem("CircuitAIR/4. Gerar projeto do Xcode (iOS)")]
        static void BuildIOSMenu() => BuildFromMenu(BuildTargetGroup.iOS, BuildTarget.iOS, BuildIOS, XcodePath);

        static void BuildFromMenu(BuildTargetGroup group, BuildTarget target, System.Func<string> build, string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (!EditorUtility.DisplayDialog("Circuit AIR",
                        $"A plataforma ativa não é {target}. Trocar agora? (a Unity vai reimportar os recursos; depois use o menu de novo)",
                        $"Trocar para {target}", "Cancelar"))
                    return;
                EditorUtility.DisplayDialog("Circuit AIR", SwitchTo(group, target), "OK");
                return;
            }

            var result = build();
            EditorUtility.DisplayDialog("Circuit AIR", result, "OK");
            if (result.StartsWith("OK"))
                EditorUtility.RevealInFinder(Path.GetFullPath(output));
        }

        /// <summary>Troca a plataforma ativa para Android. Retorna uma mensagem de status.</summary>
        public static string SwitchToAndroid() => SwitchTo(BuildTargetGroup.Android, BuildTarget.Android);

        /// <summary>Troca a plataforma ativa para iOS. Retorna uma mensagem de status.</summary>
        public static string SwitchToIOS() => SwitchTo(BuildTargetGroup.iOS, BuildTarget.iOS);

        static string SwitchTo(BuildTargetGroup group, BuildTarget target)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return $"ERRO: o módulo {target} da Unity não está instalado (ou o Editor precisa ser reaberto depois da instalação).";
            if (EditorUserBuildSettings.activeBuildTarget == target)
                return $"OK: a plataforma já é {target}.";
            var ok = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            return ok ? $"OK: plataforma trocada para {target}." : $"ERRO: não foi possível trocar a plataforma para {target}.";
        }

        /// <summary>Gera o APK. Retorna "OK: ..." ou "ERRO: ...".</summary>
        public static string BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            return Build(BuildTargetGroup.Android, BuildTarget.Android, ApkPath);
        }

        /// <summary>Gera o projeto do Xcode para iPhone. Retorna "OK: ..." ou "ERRO: ...".</summary>
        public static string BuildIOS() => Build(BuildTargetGroup.iOS, BuildTarget.iOS, XcodePath);

        static string Build(BuildTargetGroup group, BuildTarget target, string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return $"ERRO: o módulo {target} da Unity não está instalado (ou o Editor precisa ser reaberto depois da instalação).";

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                return "ERRO: nenhuma cena na lista de build. Rode CircuitAIR/2. Configurar projeto e cena.";

            // Sem o ARBackgroundRendererFeature no renderizador do celular, aparece uma cor lisa no lugar da câmera.
            var setupReport = new System.Text.StringBuilder();
            CircuitProjectSetup.SetupArBackground(setupReport);
            Debug.Log("[CircuitBuild] " + setupReport.ToString().Trim());

            string parent = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
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

            double sizeMb = File.Exists(output)
                ? new FileInfo(output).Length / (1024.0 * 1024.0)
                : Directory.GetFiles(output, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);
            return $"OK: {Path.GetFullPath(output)} ({sizeMb:0.0} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalWarnings} aviso(s)).";
        }

        /// <summary>Para linha de comando: unity build CircuitAir --target Android --execute-method CircuitAIR.EditorTools.CircuitBuild.BuildAndroidBatch</summary>
        public static void BuildAndroidBatch() => FinishBatch(BuildAndroid());

        /// <summary>Para linha de comando: unity build CircuitAir --target iOS --execute-method CircuitAIR.EditorTools.CircuitBuild.BuildIOSBatch</summary>
        public static void BuildIOSBatch() => FinishBatch(BuildIOS());

        static void FinishBatch(string result)
        {
            Debug.Log("[CircuitBuild] " + result);
            if (Application.isBatchMode)
                EditorApplication.Exit(result.StartsWith("OK") ? 0 : 1);
        }
    }
}
