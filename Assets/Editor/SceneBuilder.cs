using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/Main.unity programmatically. Uses the AR Foundation rig
// (AR Session + XR Origin (Mobile AR)) via menu commands — the SAME rig the working xreal-ha-dashboard
// builds, which presents correctly on the One Pro. (The VR-style "XR Interaction Hands Setup" left the
// glasses stuck on the Unity splash.) HUD canvas is head-locked (child of the AR camera).
// Invoke: Unity -batchmode -quit -projectPath <p> -buildTarget Android -executeMethod SceneBuilder.BuildMainScene
public static class SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void BuildMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // AR Foundation rig via the exact menu items the dashboard uses.
        bool s = EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session");
        bool o = EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)");
        Debug.Log($"[CYBEREYE-SCENE] AR Session menu={s}, XR Origin (Mobile AR) menu={o}");

        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogError("[CYBEREYE-SCENE] no camera after creating XR Origin (Mobile AR) - menu items may have failed");
            EditorApplication.Exit(3);
            return;
        }

        // Head-locked world-space HUD canvas ~1.5 m in front of the AR camera.
        var canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(cam.transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var crt = (RectTransform)canvasGO.transform;
        crt.sizeDelta = new Vector2(600, 400);
        crt.localScale = Vector3.one * 0.0015f;
        crt.localPosition = new Vector3(0f, 0f, 1.5f);
        crt.localRotation = Quaternion.identity;

        // Cyberpunk type: Share Tech Mono (OFL) drives BOTH text systems — the TMP
        // default font asset (all runtime TMP text inherits it) and the two legacy
        // Text elements.
        var cyberTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/CyberEye/Fonts/ShareTechMono-Regular.ttf");
        var cyberTmp = EnsureCyberTmpFont(cyberTtf);
        ApplyDefaultTmpFont(cyberTmp);

        var font = cyberTtf != null ? cyberTtf : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title  = CreateText(canvasGO.transform, "Title",  "NIGHT CITY OS", 54, new Vector2(0, 110), font, new Color(0f, 1f, 0.9f));
        var status = CreateText(canvasGO.transform, "Status", "> BOOTING",      34, new Vector2(0, 10),  font, new Color(1f, 0.25f, 0.85f));

        // Fullscreen head-locked neon HUD overlay (Option A): CyberEye/HudOverlay drawn over the optical view.
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "HudOverlay";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(cam.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, 6f);
        var quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        var app  = new GameObject("CyberEyeApp");
        var hud  = app.AddComponent<HudController>();
        var boot = app.AddComponent<AppBootController>();
        app.AddComponent<FpsLogger>();
        var eye  = app.AddComponent<EyeCameraFeed>();
        var overlay = app.AddComponent<HudOverlayController>();
        Wire(hud,  "title",  title);
        Wire(hud,  "status", status);
        Wire(boot, "hud",    hud);
        Wire(eye,  "hud",         hud);
        Wire(overlay, "overlayQuad", quadRenderer);

        // M4 detector: live feed when available, else the bundled test photo (headless-verifiable).
        var det = app.AddComponent<Detector>();
        var modelObj = AssetDatabase.LoadMainAssetAtPath("Assets/CyberEye/Models/yolov8n.onnx");
        var testTex  = AssetDatabase.LoadAssetAtPath<Texture>("Assets/CyberEye/Models/test_detect.jpg");
        if (modelObj == null) Debug.LogError("[CYBEREYE-SCENE] yolov8n.onnx ModelAsset not found (import failed?)");
        Wire(det, "modelAsset",  modelObj);
        Wire(det, "testTexture", testTex);
        Wire(det, "eyeFeed",     eye);

        // M5: neon target boxes on detections (IoU-tracked).
        var targets = app.AddComponent<TargetOverlay>();
        Wire(targets, "detector", det);

        // M7: cyberpunk audio (ambiance + reactive SFX).
        var audio = app.AddComponent<AudioDirector>();
        Wire(audio, "overlay",   targets);
        Wire(audio, "ambiance",  AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CyberEye/Audio/ambiance.wav"));
        Wire(audio, "lockSfx",   AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CyberEye/Audio/lock.wav"));
        Wire(audio, "scanSfx",   AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CyberEye/Audio/scan.wav"));
        Wire(audio, "glitchSfx", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CyberEye/Audio/glitch.wav"));
        Wire(audio, "alertSfx",  AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CyberEye/Audio/alert.wav"));

        // R7: 6DoF effects — world-anchored target pins + heading tape / velocity
        // ticker / head-turn glitch bursts.
        var pins = app.AddComponent<TargetPins>();
        Wire(pins, "overlay", targets);
        Wire(pins, "detector", det);
        var telemetry = app.AddComponent<MotionTelemetry>();
        Wire(telemetry, "hudCanvas", canvasGO.transform);
        Wire(telemetry, "pins", pins);

        // M8: perf/thermal guard + minimal vol-key settings.
        var perf = app.AddComponent<PerfGuard>();
        Wire(perf, "detector", det);
        var settings = app.AddComponent<SettingsController>();
        Wire(settings, "overlay", overlay);
        Wire(settings, "hud", hud);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log($"[CYBEREYE-SCENE] saved {ScenePath} as build scene 0 (camera={cam.name})");
        EditorApplication.Exit(0);
    }

    /// <summary>Headless TMP font asset from the bundled TTF (created once, committed).
    /// Dynamic atlas population so any glyph (·¦°→ etc.) rasterizes at runtime.</summary>
    static TMPro.TMP_FontAsset EnsureCyberTmpFont(Font ttf)
    {
        const string assetPath = "Assets/CyberEye/Fonts/ShareTechMono SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        if (ttf == null) { Debug.LogWarning("[CYBEREYE-SCENE] ShareTechMono TTF missing — keeping default TMP font"); return null; }

        var fa = TMPro.TMP_FontAsset.CreateFontAsset(ttf, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            TMPro.AtlasPopulationMode.Dynamic);
        if (fa == null) { Debug.LogError("[CYBEREYE-SCENE] TMP font asset creation FAILED"); return null; }
        fa.name = "ShareTechMono SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        if (fa.material != null)
        {
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        if (fa.atlasTexture != null)
        {
            fa.atlasTexture.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[CYBEREYE-SCENE] created " + assetPath);
        return fa;
    }

    /// <summary>Point TMP_Settings.defaultFontAsset at the cyber font so every runtime
    /// TMP element (feed, dossier, chips, pins, tape) inherits it with zero rewiring.</summary>
    static void ApplyDefaultTmpFont(TMPro.TMP_FontAsset fa)
    {
        if (fa == null) return;
        var settings = AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (settings == null) { Debug.LogWarning("[CYBEREYE-SCENE] TMP Settings.asset not found — default font unchanged"); return; }
        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null) { Debug.LogWarning("[CYBEREYE-SCENE] TMP Settings has no m_defaultFontAsset property"); return; }
        prop.objectReferenceValue = fa;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[CYBEREYE-SCENE] TMP default font -> ShareTechMono SDF");
    }

    static Text CreateText(Transform parent, string name, string text, int size, Vector2 anchoredPos, Font font, Color color)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text; t.font = font; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(560, 120);
        rt.anchoredPosition = anchoredPos;
        return t;
    }

    static void Wire(Object comp, string field, Object value)
    {
        var so = new SerializedObject(comp);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
