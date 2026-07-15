# CyberEye — Work Plan (from APP_REVIEW Top 5)

Derived from `APP_REVIEW.md` (2026-07-13). Ordered by leverage. Effort: S (<½ day) / M (~1 day) /
L (multi-day). "Device-gated" = final verification needs a nebulaOS MR launch on the Beam Pro
(a human must launch from the in-glasses launcher for the Eye camera + MR session).

Existing issue map (from `CODE_REVIEW_HANDOFF.md`): C-1→#1, C-2→#2, C-9→#9-batch, enhancements→#10.

## Progress log
- **2026-07-14 — Delta review + fix wave + deployed (dev build, md5-verified, 2D sanity PASS).**
  Two-agent delta review of the uncommitted tree found: (a) `Detector` `m_InputRT`/`m_ScaleRT`
  defaulted to sRGB while `_rgbRT` went Linear — an implicit re-encode hop between the YUV shader
  and the tensor (WP-1 wasn't closed end-to-end); (b) `WarmupAsync` sat outside the WP-5d
  generation protocol — a >12s cold-start warmup could trip the watchdog and reopen the
  double-schedule race; (c) gaze-hold could engage ~0.24s before the optical lock ramp crossed
  0.85, desyncing the reticle/world-pin from the frozen dossier. All three fixed (both RTs now
  `RenderTextureReadWrite.Linear`; warmup joins the gen counter with a 40s wedge window that
  unlatches if the watchdog fires; HELD now also requires `_lock>0.85`). Review verified-sound:
  FOV math, glide, dwell state machine (track-id based, survives reshuffles), lifecycle fixes,
  all 5 shaders swept clean of GLSL-ES reserved words; XREAL recipe intact; `androidUseCustomKeystore:0`
  churn is expected (dev builds clear it; `-release` re-signs from env). **Queued items shipped:**
  dwell-progress ring (burst shader mode 2, `_Lock=1-progress` → ring converges tight+bright as the
  hold charges; runtime-built, no scene regen) and world-pin declutter (same-class pin within 0.9m
  refreshes in place — kills the CANINE 02/03 twin stack; different-class neighbors step up 0.4m
  rungs). Built headlessly (errors=0, 225s), installed md5-verified, 2D launch: clean boot,
  30 fps, warmup instant, zero exceptions. **On-glasses (nebulaOS) checks queued:** dwell ring
  visual + hold timing feel, pin declutter in a multi-object room, `feed_dump.png` recheck after
  the Linear-RT fix (confirm `_SwapRB=1` still correct), live detection count vs benchmark.
- **2026-07-13 — ON-DEVICE VALIDATION (dev build on Beam Pro + One Pro).** ✅ **WP-1 (C-2) confirmed:**
  `feed_dump.png` shows natural exposure + correct color (gamma fix good); **channel order correct at
  `_SwapRB=1` — no flip needed**. Live detection `[DET] person@0.88` on the Eye feed (matches the
  test-image benchmark). ✅ **GLES3 shader fix confirmed** (build `errors=0`; overlay can no longer fall
  back to the magenta error shader). ✅ MR session healthy (EXTERNAL 3840×1080, XR SessionManager bound,
  no errors/exceptions). ✅ **HUD framebuffer confirmed** (relaunch, `NRFakeActivity` = MR bound): full neon HUD
  renders — target brackets + diamond reticle, fauna dossier w/ live thumbnail, magenta world-pins, "6 SEATING"
  chip, compass tape, STATIONARY ticker, parked NIGHT CITY OS. **No magenta wash → GLES3 fix visually confirmed.**
  Brackets spread across the view (consistent with the 4a FOV fix); object-alignment (4a) + glide (4b) best judged
  live by the wearer. **NEW minor UX nit:** world-pin labels overlap on the left (CANINE 02 / FELINE 03 / 2.0M stack
  into each other + the dossier corner) — candidate follow-up (pin declutter / screen-space spread).

- **2026-07-13 — WP-1 (C-2) code done, compile-verified.** Removed the `pow(2.2)` linearize in
  `CyberEyeYUV.shader`; made the detector `_rgbRT` Linear (`EyeCameraFeed`); channel order is now a
  material toggle `_SwapRB` (default 1 = legacy BGR, zero regression). **On-device `feed_dump.png`
  validation pending** (needs a nebulaOS launch to decide `_SwapRB` 1 vs 0).
- **2026-07-13 — WP-5 quick cleanups done, compile-verified.** 5c leak (`EyeCameraFeed` now Destroys
  `_yuvMat`/`_rgbRT`), 5d watchdog double-run (`Detector` generation counter), 5e null-material guards
  (`EyeCameraFeed`/`HudOverlayController`/`TargetOverlay`), 5b partial (explicit `targetSdk=34` in
  `BuildScript`; permission pruning deferred — the capture SDK may require `RECORD_AUDIO`, rationale in
  `PRIVACY.md`).
- **2026-07-13 — WP-4 code done, compile-verified.** 4a Eye↔display FOV fix (`TargetOverlay` +
  `TargetPins` now size placement from the Eye's ~72×45° FOV via shared `TargetPins.EyeHFovDeg/…VFovDeg`);
  4b tracker smoothing (`ObjectTracker.Track.draw` glides toward each measurement, framerate-independent;
  velocity extrapolation intentionally skipped — overshoot risk on multi-second gaps). **On-device
  visual check pending.**
- **2026-07-13 — NEW BUG found & fixed during the build (not in the original review).** `CyberHudOverlay.shader`
  named a local `float line` — **`line` is reserved in GLSL ES**, so the GLES3 variant failed at build time
  and the whole neon overlay fell back to the magenta error shader **on-device** (the app forces GLES3).
  Editor-platform compiles (and the headless compile-opens) passed, so only a real Android build surfaced it.
  Renamed `line`→`sweepLine`. This is why the build step mattered.
- **2026-07-13 — WP-3 started: gaze-dwell HOLD (first interactivity).** `TargetOverlay` — dwell your gaze
  (~1.2s, 4° cone) on an in-view target to freeze its dossier (stops retargeting so you can read it); look
  away (>9°) to release. Gazed target "charges" toward the hold color for discoverability. Pure head-gaze
  vs the box directions already computed — no menu, no controller binding. Additive (default behavior when
  not holding). **On-device tuning likely** (cone/dwell timing). Next WP-3 slices: class-filter cycle,
  FX-intensity, phone-tap (XREALController TriggerButton). **Follow-ups queued:** world-pin declutter (nit),
  dwell-progress ring.
- **Next on-glasses session:** validate WP-4 already ✅ (looked good); test WP-3 gaze-hold; then WP-2
  (capture). WP-5a (model swap) before any public release.

---

## WP-1 — Live color/gamma correctness (C-2, issue #2) — 🔥 Top priority
**Why:** the core feature is silently unproven on the live feed; the only accuracy datapoint bypassed
the YUV→RGB shader. Highest ROI.
- [ ] 1a. Analyze `CyberEyeYUV.shader` — exact channel order + gamma applied to the detector RT.
- [ ] 1b. Guarantee YOLO input is **sRGB RGB in [0,1]** — correct channel order, no display-tuned
      linearize baked into the detector path.
- [ ] 1c. Implement: give the detector its own correct input (dedicated material/RT or a corrected
      branch) **without** changing the display grade (which is tuned for looks).
- [ ] 1d. Keep the on-device evidence hook (`dump_feed` marker / dev build → `feed_dump.png`).
- **Acceptance (device-gated):** `feed_dump.png` from a nebulaOS run shows natural color (skin tones,
  not blue/inverted, not crushed mid-tones); live-people detection count ≈ the test-image benchmark.
- **Effort:** S–M (code S; validation device-gated).

## WP-2 — Capture & share the HUD moment — 🔥 High
**Why:** the single biggest lever for an entertainment/demo app; the one thing users most want and
can't do; a clear edge over display-less Meta.
- [ ] 2a. Decide compositing approach — **a display screenshot = HUD over black**, not what the user
      saw. Composite HUD over a camera frame (Beam Pro dual cameras, or the Eye RT).
- [ ] 2b. Render HUD + camera frame → RenderTexture → PNG (v1) / MP4 (v2); save to gallery.
- [ ] 2c. Trigger via gaze-dwell or phone button (depends on WP-3 input).
- **Acceptance (device-gated):** a saved shareable image/clip showing the HUD over the real scene.
- **Effort:** L. **Risk:** compositing on optical see-through is a real mini-project.

## WP-3 — Interactivity: gaze-dwell + phone-tap — 🔥 High
**Why:** turns a 3-minute novelty into something you engage with; uses the platform's *proven* inputs;
wires the `SettingsController` stub.
- [ ] 3a. Gaze reticle + dwell selection (world-space follower, ~3.2° cone, 1.0–1.2 s dwell — per
      `xreal-sdk-dev` ar-ux-patterns).
- [ ] 3b. Phone-ray selector (XREALController `TriggerButton`) — per the fleet's PhoneRaySelector.
- [ ] 3c. Wire `SettingsController`: class-filter toggles, dossier freeze/dismiss, FX intensity.
- [ ] 3d. If adding a confidence slider, pair with C-9 (thresholds as graph inputs / rebuild worker).
- **Acceptance (device-gated):** user can select a target, freeze its dossier, toggle class filters.
- **Effort:** M.

## WP-4 — Differentiation + spatial polish — High
**Why:** own "private, on-device, stylized augmented perception"; make brackets actually sit on objects.
- [ ] 4a. Fix Eye↔display FOV mapping in `TargetOverlay.Update` (+ `TargetPins` ray) using the Eye
      intrinsics (`EyeHFovDeg/EyeVFovDeg`) instead of `cam.fieldOfView`.
- [ ] 4b. Tracker temporal smoothing: EMA on size + constant-velocity predictor so boxes glide between
      sparse frames.
- **Acceptance (device-gated for 4a):** brackets sit on a known object at the frame edge; boxes glide.
- **Effort:** M.

## WP-5 — Release blockers & cleanup — Engineering
**Why:** make a public demo / repo flip safe; kill the remaining hazards.
- [ ] 5a. Swap YOLOv8n → YOLOX-Nano/NanoDet (Apache-2.0); rework `Detector.Awake` decode graph; add
      `LICENSE` + `THIRD-PARTY-NOTICES` (C-1, issue #1).
- [ ] 5b. Manifest: set explicit `targetSdk` 34+; prune unused `RECORD_AUDIO`/`FOREGROUND_SERVICE*`
      or document (C-12).
- [ ] 5c. Fix `EyeCameraFeed` leak — `Destroy` `_yuvMat` and the `_rgbRT` wrapper (partial C-11).
- [ ] 5d. Fix `Detector` watchdog double-run — generation counter / cancellation so a timed-out run's
      continuation no-ops.
- [ ] 5e. Guard `new Material(Shader.Find(...))` against null (enhancement #14).
- **Acceptance:** clean headless build; no leaks; safe for distribution. **5c/5d/5e/5b verify headlessly.**
- **Effort:** 5a = M–L; 5b–5e = S.

---

### Suggested execution order
1. **WP-1** (core correctness) → 2. **WP-5c/5d/5e/5b** (quick, headless-verifiable cleanups) →
3. **WP-3** (interactivity, unblocks WP-2 trigger) → 4. **WP-2** (capture) → 5. **WP-4** (polish) →
6. **WP-5a** (model swap, before any public release).

Device-gated items are batched for the next on-glasses session; everything else is verifiable
headlessly (compile / logic).
