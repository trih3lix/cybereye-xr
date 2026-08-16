# CyberEye

A cyberpunk augmented-reality experience for **XREAL One Pro glasses + XREAL Eye** running on the **XREAL Beam Pro** (Android 14). Built in **Unity 6000.0.78f1** with the **XREAL SDK 3.1.0**.

Look at the world through the glasses and CyberEye overlays a neon "augmented perception" HUD: it recognizes people/animals with an on-device neural net, locks glowing target boxes onto them, and pops glitchy **fictional** biometric dossiers, backed by reactive cyberpunk audio.

> ⚠️ **FICTIONAL / ENTERTAINMENT ONLY.** All "dossier" data (names, addresses, DOB, scores, dystopian facts) is **randomly generated** and shown at boot with a disclaimer. It is not real and not derived from anyone in view. See [PRIVACY.md](PRIVACY.md) — all processing is on-device, nothing is uploaded.

## What works
- **Optical see-through** experience — you see the real world through the lenses; a neon HUD grade (scanlines, scan-sweep, edge-glow, crosshair) overlays it (`CyberEye/HudOverlay`).
- **On-device object detection** — YOLOv8n via Unity Inference Engine (`com.unity.ai.inference`), GPUCompute; detects person / dog / bird(duck) / cat (COCO). Verified detecting 4 people @0.89 on a test image.
- **Tracking + neon target boxes** locked onto detections (`TargetOverlay` + `CyberEye/TargetBox`).
- **Fictional dossiers** deterministically generated per tracked target (`BiometricProfileGenerator`).
- **Cyberpunk audio** — ambiance loop + lock/scan/alert/glitch SFX (self-synthesized, CC0).
- **Perf guard** — adaptively throttles detection + watches thermals to protect framerate.
- Signed release APK, neon app icon, boot disclaimer.

## Known limitations
- **Eye live-video is hardware-limited on this unit.** The RGB feed hands off only ~1 frame every few seconds via the SDK (`Rgb error Acquire Frame -3`, all paths/tracking modes) — a native/firmware limit below the C# API. So CyberEye uses **optical see-through** (not a fullscreen video base); detection rides the sporadic feed as a "scan/lock" cadence. See [SUBMISSION_CHECKLIST.md](SUBMISSION_CHECKLIST.md).
- **Model license:** ships with YOLOv8n (Ultralytics, **AGPL-3.0**). Fine for private/dev; **swap to YOLOX (Apache-2.0) before any closed-source public release.**
- Must be launched from the **nebulaOS launcher** on the Beam Pro for MR mode + camera (plain `adb` launch runs flat 2D with no Eye).

## Build & deploy
```powershell
# regenerate the scene (programmatic), then build + install:
scripts/dev-loop.ps1 -Serial 192.168.0.102:5555 -WifiIp 192.168.0.102        # dev build
scripts/dev-loop.ps1 -Release -Serial 192.168.0.102:5555 -WifiIp 192.168.0.102 # signed release
# then LAUNCH 'CyberEye' from nebulaOS on the Beam Pro (MR mode)
```
- `scripts/build.ps1` — Unity batchmode build (`BuildScript.PerformAndroidBuild`), enforces the XREAL recipe (GLES3, MT-off, IL2CPP/ARM64, HDR-off).
- `scripts/deploy.ps1` — install-only (adb); `-Launch2D` for a headless 2D boot/crash sanity check.
- `scripts/verify.ps1` — reads the `CYBEREYE` logcat tags to verify a run headlessly.
- Requires JDK 21, Android SDK (platform 34), Unity 6000.0.78f1 with Android build support.

## Restore the XREAL SDK (excluded from git)
`LocalPackages/` (~550 MB) is git-ignored. To build, restore it:
1. Place `com.xreal.xr-3.1.0.tgz` (XREAL SDK 3.1.0, from developer.xreal.com) in `LocalPackages/`.
2. Run `LocalPackages/patch-xreal-agp.sh` to extract + AGP-namespace-patch it into `LocalPackages/com.xreal.xr-src/` (required for AGP 8 / Unity 6).
3. `Packages/manifest.json` references it as `file:../LocalPackages/com.xreal.xr-src`.

## Layout
- `Assets/CyberEye/Scripts/` — app logic (Detector, EyeCameraFeed, TargetOverlay, BiometricProfileGenerator, AudioDirector, HudController/Overlay, PerfGuard, SettingsController, CyberLog).
- `Assets/CyberEye/Rendering/` — URP shaders (HudOverlay, TargetBox, YUVtoRGB, CyberpunkFeed).
- `Assets/CyberEye/Models/` — yolov8n.onnx + test image.
- `Assets/CyberEye/Audio/` — synthesized SFX. `Assets/CyberEye/Icon/` — app icon.
- `Assets/Editor/` — BuildScript, SceneBuilder (programmatic scene), IconSetup.
- `scripts/` — build/deploy/verify PowerShell loop + `gen_icon.py`.
- Docs: [SUBMISSION_CHECKLIST.md](SUBMISSION_CHECKLIST.md), [VISUAL_CHECKLIST.md](VISUAL_CHECKLIST.md), [docs/XREAL_API_REFERENCE.md](docs/XREAL_API_REFERENCE.md).

## License

**AGPL-3.0** — see [LICENSE](LICENSE).

The repo bundles `Assets/CyberEye/Models/yolov8n.onnx` (YOLOv8n, © Ultralytics), which is
itself **AGPL-3.0**; licensing this project AGPL-3.0 keeps redistribution clean. If you want
CyberEye under a permissive license, swap the detector for an Apache-2.0 model such as YOLOX
and relicense the remaining first-party code accordingly.

The XREAL SDK is **not** included and is not covered by this license — it is license-gated at
[developer.xreal.com](https://developer.xreal.com) and must be restored separately (see above).
