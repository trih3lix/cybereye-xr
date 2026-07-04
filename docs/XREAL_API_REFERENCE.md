# XREAL SDK 3.1.0 — API reference for CyberEye

Distilled from the SDK sample + runtime source (`LocalPackages/com.xreal.xr-src`). All runtime code is namespace **`Unity.XR.XREAL`**; samples are `Unity.XR.XREAL.Samples`.

## Camera rig
- Instantiate prefab **`Packages/com.xreal.xr/Runtime/Prefabs/XR Interaction Hands Setup.prefab`** (hand + controller input) or `XR Interaction Setup.prefab`.
- It nests Unity's **XR Origin (XR Rig)**: `XR Origin → Camera Offset → Main Camera` (tagged `MainCamera` = `Camera.main`). XREAL setup overrides camera: clearFlags=SolidColor(black), FOV=25.
- The rig prefab also brings the **EventSystem + XRI UI input**.

## HUD / UI
- **World-space uGUI Canvas** ~1.5 m in front of the rig, `localScale ≈ 0.001`, `RenderMode = WorldSpace`, no event camera.
- Add both `GraphicRaycaster` **and** `TrackedDeviceGraphicRaycaster` (the latter makes it clickable from gaze/controllers).
- HelloMR uses TextMeshPro; camera samples use legacy `UnityEngine.UI.Text` / `RawImage` (no TMP-essentials import needed — good for early milestones).

## RGB camera (XREAL Eye) — M2
```csharp
using Unity.XR.XREAL;
// start
var cam = XREALRGBCameraTexture.CreateSingleton();  // DontDestroyOnLoad GO
bool ok = cam.StartCapture();                        // false => Eye NOT connected / busy
// per frame
Texture2D[] yuv = cam.GetYUVFormatTextures();        // [0]=Y [1]=U [2]=V, Alpha8 planes (4:2:0)
if (yuv[0] != null) {
    rawImage.texture = yuv[0];                       // _MainTex = Y
    rawImage.material.SetTexture("_UTex", yuv[1]);
    rawImage.material.SetTexture("_VTex", yuv[2]);
}
// stop
cam.StopCapture();
```
- Detect Eye present: `XREALPlugin.IsHMDFeatureSupported(XREALSupportedFeature.XREAL_FEATURE_RGB_CAMERA)`, and `StartCapture()` returning false.
- **NOTE (source quirk):** internal plane fetch order is Y, **V, U** but `GetYUVFormatTextures()` returns them as [Y,U,V]; trust the sample wiring above (`_UTex`=index1, `_VTex`=index2).
- Resolution: simple path uses whatever the native frame reports. Low/Mid/High selection only exists in photo/video capture (`XREALVideoCaptureUtility.SupportedResolutions`).

## YUV→RGB shader
- **`Shader "Unlit/YUVTransRGB"`** at `LocalPackages/com.xreal.xr-src/Samples~/Camera Features/RGBCameraAndCapture/Materials/YUVTransRGB.shader` + material `YUVTexture.mat`. BT.601 conversion, outputs B,G,R order, then GammaToLinearSpace. Import the "Camera Features" sample (or copy this material) for M2.

## Permissions (M2)
- The XREAL package ships **no AndroidManifest.xml** — I must add `Assets/Plugins/Android/AndroidManifest.xml` with `CAMERA` (+ `RECORD_AUDIO`, `FOREGROUND_SERVICE_MEDIA_PROJECTION` for recording).
- Simple RGB path requests **no** runtime permission in code → request `CAMERA` at startup via `UnityEngine.Android.Permission.RequestUserPermission(Permission.Camera)`. (`adb install -g` auto-grants in dev.)
- XREAL's own `XREALAndroidPermissionsManager` (bridges Java `ai.nreal.sdk.UnityAndroidPermissions`) handles `RECORD_AUDIO` + `RequestScreenCapture()` (MediaProjection) — only needed for video capture, not live preview.

## Fullscreen video-see-through (M3)
- No per-eye camera blit exists in the SDK. Options: (a) large quad parented to `Camera.main` near the near-plane, sized to fill FOV, textured with the YUV material; or (b) fullscreen `RawImage` in a camera/world-space canvas. Apply the cyberpunk grade via a **URP fullscreen Renderer Feature** on top (post-processes the whole camera color target incl. the video + HUD).

## Build gotchas (from working `xreal-ha-dashboard`)
- URP **HDR OFF**, **OpenGLES3 only** (no Vulkan), Linear color, vSync 0.
- IL2CPP, **ARM64 only**, minSdk 29, APK (not AAB).
- The `com.xreal.xr-src` was AGP-namespace-patched (unique `nrsdk.pack.*` per AAR) — required for AGP 8 / Unity 6. Already applied in the copied `-src`.
- Force-include TMP SDF shaders in Always-Included Shaders if creating TMP text at runtime.
