using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TavlaAI.Editor
{
    /// <summary>
    /// Faz 5 Bolum 6.12: batchmode dogrulama giris noktalari.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>
        /// Yalnizca derleme dogrulamasi. Batchmode'da <c>-executeMethod</c> ile cagrilir;
        /// derleme hatalari varsa Unity sifirdan farkli cikis kodu dondurur.
        /// </summary>
        public static void CompileOnly()
        {
            Debug.Log("[TavlaAI] CompileOnly: derleme tamamlandi.");
        }

        /// <summary>Sahneyi yeniden uretir (idempotent).</summary>
        public static void BuildScene()
        {
            SceneBuilder.Build();
        }

        /// <summary>Windows x64 Standalone derlemesi (kapsam: yalnizca Windows x64).</summary>
        public static void BuildWindows64()
        {
            const string output = "Build/Tavla3D.exe";
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[TavlaAI] Build result: " + report.summary.result +
                      " (" + report.summary.totalErrors + " hata)");
        }
    }
}
