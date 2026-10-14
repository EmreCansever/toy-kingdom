using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ToyKingdom.EditorTools
{
    /// <summary>
    /// Telefona kurulacak APK: ToyKingdom > Android APK oluştur.
    /// Çıktı proje klasöründe Builds/ToyKingdom.apk, özet Builds/build_log.txt.
    /// </summary>
    public static class AndroidBuild
    {
        const string OutDir = "Builds";

        static bool HasNdk()
        {
            string dir = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/NDK");
            return Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0;
        }

        [MenuItem("ToyKingdom/Android APK oluştur")]
        public static void Build()
        {
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();

            PlayerSettings.companyName = "EmreCansever";
            PlayerSettings.productName = "Toy Kingdom";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.emrecansever.toykingdom");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            // IL2CPP (64 bit) Android NDK ister; NDK kurulu değilse Mono ile 32 bit derlenir (çoğu telefonda çalışır)
            bool ndk = HasNdk();
            log.AppendLine("NDK: " + (ndk ? "var, IL2CPP ARM64+ARMv7" : "yok, Mono ARMv7"));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ndk ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = ndk ? AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7 : AndroidArchitecture.ARMv7;

            // yatay oyun: iki yatay yöne döner, dikeye dönmez
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = new[]
            {
                "Assets/ToyKingdom/Generated/Scenes/Map.unity",
                "Assets/ToyKingdom/Generated/Scenes/Battle.unity",
            };
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, "ToyKingdom.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            log.AppendLine("Başladı: " + System.DateTime.Now);
            BuildReport report = null;
            try { report = BuildPipeline.BuildPlayer(opts); }
            catch (System.Exception e) { log.AppendLine("İSTİSNA: " + e); }

            if (report != null)
            {
                var s = report.summary;
                log.AppendLine("Sonuç: " + s.result);
                log.AppendLine("Süre: " + s.totalTime);
                log.AppendLine("Boyut (MB): " + (s.totalSize / 1048576f).ToString("0.0"));
                log.AppendLine("Hata: " + s.totalErrors + "  Uyarı: " + s.totalWarnings);
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            log.AppendLine("[" + step.name + "] " + m.content);
            }
            log.AppendLine("Bitti: " + System.DateTime.Now);
            File.WriteAllText(Path.Combine(OutDir, "build_log.txt"), log.ToString());
            Debug.Log("[AndroidBuild]\n" + log);
        }
    }
}
