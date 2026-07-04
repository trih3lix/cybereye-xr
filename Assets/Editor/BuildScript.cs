using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// Batchmode entry point for the CyberEye dev loop.
//   Dev build:     Unity -batchmode -quit -projectPath <p> -buildTarget Android -executeMethod BuildScript.PerformAndroidBuild -logFile <log>
//   Release build: ... same, plus -release  (unsigned-by-project-settings, minified)
// Exit codes: 0 = success, 1 = build failed, 2 = no scenes configured.
public static class BuildScript
{
    const string OutDir = "Builds";
    const string DevApk = "CyberEye.apk";
    const string RelApk = "CyberEye-release.apk";

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    // Enforce the proven XREAL recipe at build time (some of this is also baked into ProjectSettings,
    // but setting it here keeps every build correct regardless of Editor state).
    static void HardenXrPlayerSettings()
    {
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 }); // Vulkan renders broken on glasses
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, false);                        // avoid composition-layer tearing
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EnsureTmpShadersIncluded();
        Debug.Log("[CYBEREYE-BUILD] hardened: GLES3-only, mobileMT=off, IL2CPP/ARM64, TMP SDF shaders always-included");
    }

    // TMP text created at runtime would otherwise have its SDF shader stripped -> white-box glyphs.
    static void EnsureTmpShadersIncluded()
    {
        string[] names = { "TextMeshPro/Distance Field", "TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field SSD", "CyberEye/YUVtoRGB", "CyberEye/FeedUnlit", "CyberEye/CyberpunkFeed", "CyberEye/HudOverlay", "CyberEye/TargetBox" };
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) return;
        foreach (string n in names)
        {
            Shader sh = Shader.Find(n);
            if (sh == null) continue;
            bool present = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { present = true; break; }
            if (present) continue;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
        }
        so.ApplyModifiedProperties();
    }

    // Release signing from git-ignored scripts/local.properties (falls back to debug keystore if absent).
    static void ConfigureReleaseSigning()
    {
        const string lp = "scripts/local.properties";
        if (!File.Exists(lp)) { Debug.LogWarning("[CYBEREYE-BUILD] no scripts/local.properties -> debug keystore"); return; }
        var kv = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(lp))
        {
            int i = line.IndexOf('=');
            if (i > 0) kv[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
        }
        string Get(string k) => kv.TryGetValue(k, out var v) ? v : "";
        string sf = Get("RELEASE_STORE_FILE");
        if (!string.IsNullOrEmpty(sf) && File.Exists(sf))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = sf;
            PlayerSettings.Android.keystorePass = Get("RELEASE_STORE_PASSWORD");
            PlayerSettings.Android.keyaliasName = Get("RELEASE_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Get("RELEASE_KEY_PASSWORD");
            Debug.Log($"[CYBEREYE-BUILD] release signing: keystore={Path.GetFileName(sf)} alias={PlayerSettings.Android.keyaliasName}");
        }
        else Debug.LogWarning("[CYBEREYE-BUILD] RELEASE_STORE_FILE missing/not found -> debug keystore");
    }

    public static void PerformAndroidBuild()
    {
        bool release = Environment.GetCommandLineArgs().Contains("-release");

        // Scenes: -scene "a.unity,b.unity" overrides EditorBuildSettings (used by the dev loop before a scene is wired into build settings).
        string[] scenes;
        string sceneArg = GetArg("-scene");
        if (!string.IsNullOrEmpty(sceneArg))
            scenes = sceneArg.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        else
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[CYBEREYE-BUILD] FAILED: no enabled scenes in EditorBuildSettings. Add a scene before building.");
            EditorApplication.Exit(2);
            return;
        }

        Directory.CreateDirectory(OutDir);
        string apk = Path.Combine(OutDir, release ? RelApk : DevApk);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        HardenXrPlayerSettings();
        IconSetup.Apply();
        if (release) ConfigureReleaseSigning();

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = release ? BuildOptions.None
                              : (BuildOptions.Development | BuildOptions.AllowDebugging),
        };

        Debug.Log($"[CYBEREYE-BUILD] START release={release} scenes=[{string.Join(", ", scenes)}] out={apk} " +
                  $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)} " +
                  $"arch={PlayerSettings.Android.targetArchitectures} appId={PlayerSettings.applicationIdentifier}");

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[CYBEREYE-BUILD] RESULT={s.result} time={s.totalTime} sizeBytes={s.totalSize} errors={s.totalErrors} warnings={s.totalWarnings}");

        if (s.result == BuildResult.Succeeded)
        {
            Debug.Log($"[CYBEREYE-BUILD] SUCCESS apk={Path.GetFullPath(apk)}");
            EditorApplication.Exit(0);
        }
        else
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    Debug.LogError($"[CYBEREYE-BUILD] {step.name}: {msg.content}");
            Debug.LogError($"[CYBEREYE-BUILD] FAILED result={s.result}");
            EditorApplication.Exit(1);
        }
    }
}
