# Code Review Handoff — cybereye-xr (2026-07-08)

> **Status — 2026-07-08 evening:** C-3, C-5, C-6, C-7, C-8, C-10, C-15 fixed in working tree; C-2 instrumented (first-live-frame PNG dump, logcat marker `[CyberEye] feed dump:`); pending on-device validation.

## For the next agent — read this first

This is the healthiest of the four XR repos — it compiles clean (batchmode-verified 2026-07-08 from `C:\Users\jslade\CyberEyeXR` at repo HEAD) and has already shipped to the Beam Pro (`com.jslade.cybereye` installed). Verified-correct: YOLO decode graph, shader always-include config, XR loader wiring. The work is quality/correctness hardening, not resurrection.

Recommended order:
1. **C-2** live color/gamma validation (gates the core feature; needs on-device PNG dump)
2. **C-3** phantom test-image detections
3. **C-4** letterboxing
4. **C-5 / C-6** boot-race + camera lifecycle
5. **C-7** teardown race
6. **C-8** hardcoded script paths
7. Low batch (C-9..C-15)
8. Enhancements — world-locking (#4) and tracker smoothing (#5) are the biggest UX wins

**C-1** (AGPL model) is a decision item for the user before ANY public distribution.

## Environment (verified 2026-07-08)

- **Original working copy:** `C:\Users\jslade\CyberEyeXR` (repo HEAD + dirty `VISUAL_CHECKLIST.md`, warm Library, prior builds in `Builds/`).
- **Build from there until C-8 is fixed** — the build scripts hardcode that path (C-8).
- **Unity:** 6000.0.78f1.
- **Device:** Beam Pro X4000, Android 14, arm64, adb serial `R4LM49L11862C2`.
- **Launch:** the app must be launched from the nebulaOS MR launcher for the Eye camera. A plain `adb` launch = flat 2D + test-image fallback (see C-3).
- **Eye feed:** delivers ~1 frame / 3–5 s by firmware — this is a hardware limit, not a bug.
- **XREAL SDK source:** at `C:\Users\jslade\CyberEyeXR\LocalPackages\com.xreal.xr-src` (git-ignored, 323 MB). Fresh clones need the `.tgz` from developer.xreal.com + the AGP patch per README.
- **gh token:** lacks `workflow` scope — CI YAML (enhancement #10) must be added via the GitHub web UI.
- **Local skills:** `xreal-sdk-dev` / `xreal-ar-dev`.

## Issue map

| Finding | GitHub issue |
| --- | --- |
| C-1 (High, licensing) | [#1](https://github.com/trih3lix/cybereye-xr/issues/1) |
| C-2 (High) | [#2](https://github.com/trih3lix/cybereye-xr/issues/2) |
| C-3 (High) | [#3](https://github.com/trih3lix/cybereye-xr/issues/3) |
| C-4 (Medium) | [#4](https://github.com/trih3lix/cybereye-xr/issues/4) |
| C-5 (Medium) | [#5](https://github.com/trih3lix/cybereye-xr/issues/5) |
| C-6 (Medium) | [#6](https://github.com/trih3lix/cybereye-xr/issues/6) |
| C-7 (Medium) | [#7](https://github.com/trih3lix/cybereye-xr/issues/7) |
| C-8 (Medium) | [#8](https://github.com/trih3lix/cybereye-xr/issues/8) |
| C-9..C-15 (Low/trivial checklist) | [#9](https://github.com/trih3lix/cybereye-xr/issues/9) |
| Enhancement roadmap | [#10](https://github.com/trih3lix/cybereye-xr/issues/10) |
| Meta tracking (start here) | [#11](https://github.com/trih3lix/cybereye-xr/issues/11) |

## Full findings

### C-1 — High — licensing — `Assets/CyberEye/Models/yolov8n.onnx` (+ repo root, no LICENSE)
**Problem:** Ships Ultralytics YOLOv8n weights — **AGPL-3.0**. No LICENSE file in repo. AGPL copyleft attaches on conveying the combined work (APK distribution or public repo).
**Failure scenario:** Any public store listing or APK distribution requires open-sourcing the whole app under AGPL or an Ultralytics Enterprise license; distributing without either is a violation. A public flip of the repo does the same.
**Fix plan:** OK for private dev now. Before release: swap to permissive detector (YOLOX-Nano Apache-2.0, or NanoDet) — decode differs (YOLOX needs grid-decode + objectness; rework FunctionalGraph in `Detector.Awake`). Add LICENSE + THIRD-PARTY-NOTICES. Already acknowledged in README.md:20, SUBMISSION_CHECKLIST.md:19-20.

### C-2 — High — camera/ml — `CyberEyeYUV.shader:44-49`; `Detector.cs:114-115` (UNCERTAIN)
**Problem:** Shader assembles `half3 rgb = half3(b, g, r)` (R/B swapped) then `pow(saturate(rgb), 2.2)` (gamma→linear), mirroring the XREAL sample (docs/XREAL_API_REFERENCE.md:36) — tuned for display, not for feeding a network. The only accuracy datapoint ("4 people @0.89", README.md:11) was measured on `test_detect.jpg` loaded directly as Texture — bypassing this shader entirely. Live-feed channel order + gamma into the YOLO tensor never validated. YOLOv8 trained on sRGB RGB [0,1]; BGR and/or linear-light input degrades (mid-tones 0.5→0.22 under 2.2 linearize) or breaks detection.
**Failure scenario:** On-glasses detection misses/misclassifies objects the test-image benchmark says it would find; silent accuracy loss.
**Fix plan:** Dump one live RGB RT to PNG on device (adb pull), eyeball channels/brightness. If inverted, output `half3(r,g,b)`. Ensure tensor gets sRGB [0,1] — drop the pow(2.2) for the detector's copy or create `_rgbRT` with correct sRGB flags and let `TextureConverter.ToTensor` handle it. Consider dedicated detector-input material separate from display grade.

### C-3 — High — bug/ux — `Detector.cs:91-96, 100-101`; `EyeCameraFeed.cs:21`
**Problem:** `Source()` returns `testTexture` (bundled 4-person JPEG) whenever live feed is null, and `Update` runs inference on any non-null source even with `preferTestImage=false`. At boot, before the Eye's first frame (sporadic, seconds-delayed), `PreviewTex` is null → detector runs on test_detect.jpg → 4 phantom "person" boxes + fictional dossiers on nothing.
**Failure scenario:** User boots on glasses and sees 4 target boxes locked onto nothing until the first live frame; again on feed stalls before first frame. Also: plain adb launch (no MR/Eye) permanently shows phantom detections.
**Fix plan:** Only fall back to `testTexture` when `preferTestImage` is true; otherwise return null (Update's null early-out suppresses detection). Or clear `m_Results` when source is test image and not explicitly preferred.

### C-4 — Medium — ml — `Detector.cs:83, 114`
**Problem:** No letterboxing. `_InputRT` is 640×640; `Graphics.Blit(src, m_InputRT)` stretches the ~1280×720 feed into a square. YOLOv8 expects aspect-preserving resize+pad. Output boxes come back in squished space and are re-stretched to `cam.aspect` in `TargetOverlay.Update:95-96` — placement distorted relative to optical view.
**Fix plan:** Letterbox: scale = 640/max(w,h), blit into centered rect with black padding, record pad/scale, un-letterbox normalized output coords before display.

### C-5 — Medium — xr/bug — `TargetOverlay.cs:36, 46, 54, 61-62`
**Problem:** In `Start`, boxes parented to `Camera.main` only if non-null, dossier canvas likewise, NO retry. `AppBootController.cs:17-19` explicitly anticipates `Camera.main == NULL` at Start ("XR rig not resolved yet"). If Start beats rig resolution → quads/canvas never parented; `Update` sets `localPosition` on world-space objects → overlays orphaned at world origin.
**Failure scenario:** On an unlucky boot, target boxes + dossier float at world origin and never track the head.
**Fix plan:** Cache `Camera.main`; if null in Start, defer creation/parenting until Update finds it (mirror `HudOverlayController.SizeToFov` 6-retry pattern); re-parent if it appears late.

### C-6 — Medium — xr — `EyeCameraFeed.cs` (no lifecycle hooks)
**Problem:** No `OnApplicationPause`/`OnApplicationFocus`. Once `StartCapture()` succeeds, `_capturing` latches true forever; Update retry loop (`:64-71`) only fires while `!_capturing`. App backgrounds → XREAL session releases camera → no release-on-pause or reacquire-on-resume.
**Failure scenario:** After background/resume or Eye disconnect, feed permanently dead (frames stop, `_capturing` true, no recovery); camera possibly held while backgrounded.
**Fix plan:** `OnApplicationPause(bool)`: pause → `StopCapture()`, `_capturing=false`; resume → re-run `TryStart()`. Add frame-timeout (no `OnFrame` for N s) → re-arm.

### C-7 — Medium — bug — `Detector.cs:107-130, 164-169`
**Problem:** `RunInferenceAsync` fire-and-forget (`_ = RunInferenceAsync()`); `OnDisable` disposes `m_Worker`/`m_Input`/`m_InputRT` synchronously. In-flight `ReadbackAndCloneAsync` can still run against disposing native resources. try/catch catches managed exceptions but native tensors/worker mid-readback is a teardown hazard.
**Fix plan:** `CancellationToken`/`m_Disposed` flag checked after each await; null-guard worker before scheduling; track/await the in-flight task in `OnDisable` before disposing.

### C-8 — Medium — build/hygiene — `scripts/build.ps1:9`, `deploy.ps1:10`, `dev-loop.ps1:14`, `gen_audio.py:4`
**Problem:** Scripts hardcode `$Project = "C:\Users\jslade\CyberEyeXR"` + absolute Unity path; `gen_audio.py` writes to that absolute path. Any other clone location builds/deploys the wrong (or nonexistent) directory.
**Fix plan:** Derive `$Project` from script location (`Split-Path $PSScriptRoot -Parent`); accept `-Project`/`-Unity` overrides; make gen_audio.py repo-relative.

### C-9 — Low — ml — `Detector.cs:74`
`iou`/`confidence` captured into FunctionalGraph at Awake — runtime inspector changes don't affect NMS; only the redundant `conf < confidence` post-filter (`:144`) responds and can only tighten. Document, rebuild worker on change, or pass thresholds as graph inputs.

### C-10 — Low — perf — `TargetOverlay.cs:93`, `HudOverlayController.cs:35`
`Camera.main` every frame (tag search). Cache once, refresh only if null.

### C-11 — Low — leak — `TargetOverlay.cs:15,47`; `EyeCameraFeed.cs:27`
`_profiles` dict never pruned (one entry per track ID ever). Runtime-created `_mats[]` and `_yuvMat` never Destroyed. Unbounded over long sessions. Prune on track age-out; destroy materials in OnDestroy.

### C-12 — Low — build — `ProjectSettings.asset:181`; `AndroidManifest.xml:6-8`
`AndroidTargetSdkVersion: 0` (auto) — set explicit 34+. `RECORD_AUDIO`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION` declared but unused → Play data-safety friction. Remove until recording ships (enhancement 8) or keep documented rationale (PRIVACY.md).

### C-13 — Low — hygiene — `yolov8n.onnx` (6.44 MB plain git, no .gitattributes)
Under GitHub's warn threshold, tolerable; with ambiance.wav (706 KB) + icons it bloats history. If more models arrive, add Git LFS for `*.onnx`.

### C-14 — Low — hygiene — `Assets/Samples/XREAL XR Plugin/3.1.0/**`
Vendored SDK sample scripts/scenes committed and compiled though Main.unity uses none. Consider removing the Samples folder.

### C-15 — Trivial — bug — `BiometricProfileGenerator.cs:8`
`Last[]` entry `" DELACROIX"` leading space; masked by `.Trim()` at `:45` but fauna path and future reuse don't trim. Fix the data.

## Verified correct

- **YOLOv8 decode math (`Detector.cs:58-78`) is correct** — channel slicing `[0,0..4]`/`[0,4..]` of `(1,84,8400)`, `toCorners` 4×4 matrix (center-xywh → normalized corners), ArgMax/ReduceMax over 80 classes, NMS on normalized corners, `IndexSelect` back onto pixel-space `boxCoords`, `/640` in `ParseInto`. NMS box format transpose-invariant. Input tensor + RTs allocated once; readback tensors `using`-scoped; async `Awaitable` + `m_Busy` guard keeps inference off render thread.
- **Config sanity (all pass):** URP bound; all 5 CyberEye shaders + 3 TMP shaders in committed `m_AlwaysIncludedShaders` (+ `BuildScript.EnsureTmpShadersIncluded` defensive re-add). XREALXRLoader active Android loader, InitManagerOnStart=1. Main Camera tagged. IL2CPP + ARM64 + minSdk 29, GLES3 forced at build, Linear color. Main.unity = build scene 0 with full CyberEyeApp component set. Sentis/Inference Engine 2.6.1 matches `Unity.InferenceEngine` API usage. onnx.meta imports via ScriptedImporter (ModelAsset) correctly.
- **Compile check (2026-07-08):** batchmode open of `C:\Users\jslade\CyberEyeXR` (at repo HEAD) = 0 compile errors, clean exit.
- **Secrets:** none tracked; .gitignore excludes `*.keystore`, `scripts/local.properties` (read by `BuildScript.ConfigureReleaseSigning`).

## Enhancement roadmap

Ordered by value (also tracked in [#10](https://github.com/trih3lix/cybereye-xr/issues/10)):

1. **Validate + fix live color/gamma path (C-2).** Highest ROI — gates the core feature. One-shot debug save of feed RT to PNG on device; standardize detector input as sRGB RGB [0,1].
2. **Letterbox preprocessing (C-4).** Aspect-preserving 640 resize+pad; un-letterbox outputs. `scale=640/max(w,h); rect=centered; store (scale,padX,padY)`; in ParseInto `x=(cx-padX)/(w*scale)`.
3. **Swap to permissive model (removes C-1 blocker).** YOLOX-Nano (Apache-2.0) or NanoDet; or INT8/quantized Sentis model for latency+heat. Parse stays; graph decode changes.
4. **World-lock detections via 6DoF pose.** Capture head pose at inference; project detection ray to fixed distance (or raycast AR plane) into world space; freeze world position while track "locked." Head-locked floating boxes → boxes that stick to real objects. Big perceived-quality jump given sparse frames.
5. **Temporal smoothing in tracker.** ObjectTracker only Lerps position 0.5 on match. Add EMA on size + constant-velocity predictor (or lightweight Kalman) so boxes glide during multi-second gaps instead of snapping.
6. **Runtime class-filter + confidence UI.** person/animal toggles + confidence slider via stubbed `SettingsController` (wire to deferred volume-key bridge). Pair with C-9 so thresholds re-drive NMS.
7. **Thermal/battery telemetry HUD.** PerfGuard already reads `getThermalHeadroom`; add battery + fps readout and tiered cadence policy (10/6/3 Hz by thermal band).
8. **Recording/screenshot mode.** Manifest already declares MEDIA_PROJECTION; screencapture module included — HUD-composite capture for demos; justifies declared permissions (ties C-12).
9. **On-device smoke test.** Headless flag: run detector on test_detect.jpg, assert "≥4 person @>0.8", exit code; call from verify.ps1 as regression gate.
10. **CI (manual).** Actions workflow for compile-check/smoke test on self-hosted Unity runner. gh token lacks workflow scope — YAML via web UI.
11. **Backend warmup + selection.** Dummy Schedule/readback at init (pre-compile kernels, avoid first-inference hitch); expose BackendType override / GPUCompute→CPU fallback log (computed at Detector.cs:80).
12. **Use actual capture resolution.** EyeCameraFeed hardcodes 1280×720 RT (`:28`) though `GetResolution()` available (`:58`); size RT from real frame.
13. **LICENSE + THIRD-PARTY-NOTICES.** Prerequisite for distribution (Unity, XREAL SDK terms, model license).
14. **Graceful shader/camera fallbacks.** Guard `new Material(Shader.Find(...))` null (fall back Sprites/Default); combine with C-5 re-parent fix.
