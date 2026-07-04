# CyberEye — Visual/Audio checks (need the glasses / your eyes+ears)

The dev loop verifies builds/installs/logcat headlessly, but these need you wearing the One Pro
(Eye attached), launching **CyberEye from nebulaOS** (MR mode — adb launches are 2D-only).

| # | Milestone | What to confirm | Status |
|---|-----------|-----------------|--------|
| 1 | M1 | "NIGHT CITY OS" HUD renders (cyan/magenta) | ✅ CONFIRMED |
| 2 | M2 | **Eye feed — HARDWARE-LIMITED.** Confirmed via logs: the Eye RGB feed throws `Rgb error Acquire Frame -3` in BOTH SDK paths (raw + capture) and BOTH tracking modes (6DoF + 3DoF). HEVC decoder runs at 30fps but only ~1 frame/3–5s successfully hands off (likely keyframes only) — a native/firmware limit below the C# API, not our code. nebulaOS also runs its own SeeThroughManager camera (possible contention). | ⛔ BLOCKED (decision needed — see below) |
| 3 | M3 | Cyberpunk filter on the feed: scanlines, chromatic aberration, neon cyan/magenta grade, vignette, glitch, scan-sweep. (If the feed looks solid magenta = shader error — tell me.) | ⏳ BUILT — verify look |
| 4 | M4 | Object detection PROVEN (yolov8n/Inference Engine detected 4 persons on the test image, GPUCompute, stable). Live on-device is sporadic (raw feed ~keyframe rate). | ✅ pipeline verified |
| 5 | M2/M3 Option A | Neon HUD overlay over the REAL WORLD (through the lenses, no video base): scanlines, moving scan-sweep, neon edge-glow, center crosshair. | ⏳ verify |

## DECISION NEEDED — the Eye live-video feed
Tried extensively: raw YUV path, capture/VideoCapture path, 6DoF, 3DoF, dev + release builds, freed
memory. All hit the same native `Acquire Frame -3` → ~1 frame/3–5s. This is below the SDK API; not
fixable from our C#. Options (pick when you're ready — the loop keeps building everything else):
  - **A. Lean into optical see-through (RECOMMENDED).** The One Pro already shows the real world through
    the lenses. Skip the fullscreen video base; render neon glow + dossiers + HUD + a screen-space
    cyberpunk vignette/scanline grade directly over the optical view. Detection still runs on whatever
    camera frames arrive (even 1/s is fine for stationary targets). Plays to the hardware; looks great.
  - **B. Keep the low-rate video** as a stylized "surveillance flicker" aesthetic (the lag becomes a
    feature) behind the HUD.
  - **C. Investigate hardware** — check Eye firmware / nebulaOS see-through settings / contact XREAL.
- Everything else (detection, glow, dossiers, audio, filter-as-grade) works regardless of this choice.
- Launch reminder: **nebulaOS launcher → CyberEye** (not adb) for MR; adb launch = 2D (used for headless
  detection tests, which DO work in 2D since inference is pure GPU compute).
