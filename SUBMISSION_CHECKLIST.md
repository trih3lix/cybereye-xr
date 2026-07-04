# CyberEye — Release / Submission Checklist

App: **CyberEye** · `com.jslade.cybereye` · v0.1.0 · Unity 6000.0.78f1 + XREAL SDK 3.1.0 · XREAL One Pro + Eye on Beam Pro (Android 14, arm64).

## Distribution reality (read first)
There is **no XREAL app-submission portal** today — Nebula (the old store/launcher) was discontinued and pulled from Google Play (~Apr 2026); XREAL says Beam Pro devs can ignore it. The Beam Pro runs standard Android: distribution = **sideload APK** or **Google Play**. This checklist targets a store-grade, sideloadable, Play-ready APK.

## ✅ Done
- [x] **Signed release APK** — `Builds/CyberEye-release.apk`, signed with `cybereye-release.keystore` (config in git-ignored `scripts/local.properties`).
- [x] **App icon** — neon "CyberEye" adaptive/legacy icon (`IconSetup` applies every build).
- [x] **Identity** — package `com.jslade.cybereye`, product "CyberEye", version 0.1.0.
- [x] **Build config** — IL2CPP, ARM64-only, minSdk 29, targetSdk (auto/34), OpenGLES3, HDR off (XREAL-required).
- [x] **Permissions declared + rationale** — CAMERA, RECORD_AUDIO, FOREGROUND_SERVICE(_MEDIA_PROJECTION) (see `Assets/Plugins/Android/AndroidManifest.xml` + `PRIVACY.md`).
- [x] **Privacy policy** — `PRIVACY.md` (on-device only, no upload, no PII).
- [x] **FICTIONAL / ENTERTAINMENT disclaimer** — shown in-app at boot (6s) before the experience; dossier data is randomly generated (ethical requirement — overlays fake data on real people).
- [x] **Stability** — headless logcat verified: detection, dossiers, audio, perf-guard run with no crash/ANR.
- [x] **Audio assets** — self-synthesized (CC0), no third-party samples.

## ⛔ Blockers before a PUBLIC store listing
1. **Detection model license (AGPL-3.0).** Ships with `yolov8n` (Ultralytics, AGPL-3.0). AGPL requires open-sourcing the app or an Ultralytics Enterprise license. **Swap to YOLOX-Nano (Apache-2.0)** — code path is model-agnostic except the output decode (YOLOX needs grid-decode + objectness; YOLOv8 doesn't). Deferred per owner decision (2026-07-03): finish packaging now, resolve before public release.

## ⚠️ Known limitations / validate later
- **Eye live-video is hardware-limited.** The One Pro Eye RGB feed hands off only ~1 frame/3–5s via the SDK (`Rgb error Acquire Frame -3`, both raw + capture paths, 6DoF + 3DoF) — a native/firmware limit below the C# API. App uses **optical see-through** (real world through lenses) + neon HUD overlay; detection rides the sporadic feed ("scan/lock" cadence). Not our bug; possibly XREAL Eye firmware / nebulaOS SeeThroughManager contention — worth an XREAL support ticket.
- **Performance:** ~13–15 fps in dev build while inferencing; `PerfGuard` adaptively throttles detection + watches thermals. Re-measure on the **release** build (should be higher) and tune `inferenceInterval`.
- **R8/minify:** NOT enabled — avoided stripping XREAL AAR classes. Optional size optimization; enable with keep-rules for `ai.nreal.*`, `com.xreal.*`, `Unity.InferenceEngine`, then re-verify.
- **Content rating:** dystopian/fictional-surveillance theme → likely Teen. Set rating + data-safety form if listing on Google Play.
- **MR launch:** must be launched from the nebulaOS launcher for MR mode + camera; plain `adb`/2D launch runs flat with no Eye.

## Distribution options
- **Sideload:** `adb install -r Builds/CyberEye-release.apk` (or copy to Beam Pro, open via Files).
- **Google Play:** create listing, complete data-safety (declare: no data collected/shared), content rating, upload AAB (switch `buildAppBundle=true`). Resolve the AGPL blocker first.
