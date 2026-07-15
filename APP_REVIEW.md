# CyberEye — Multi-Perspective App Review (2026-07-13)

Reviewer pass covering four lenses: engineering, competitive market, product management, and
end-user. Scope: the 16 gameplay scripts in `Assets/CyberEye/Scripts`, the 3 editor scripts,
build/deploy tooling, shader/asset/manifest surface, and committed docs. Cross-checked against
the team's own `CODE_REVIEW_HANDOFF.md` (2026-07-08). **No build was run** — deferred until these
suggestions are implemented.

---

## Part 1 — Code Review (Engineer's Lens)

**Headline:** a genuinely well-engineered, field-hardened codebase. Prior findings **C-3, C-4, C-5,
C-6, C-7, C-8, C-10, C-15 are confirmed fixed** in the current tree, which has advanced past that
review (capture-pose reprojection, world pins, adaptive GPU slicing, motion telemetry). What
follows is what's **still open** plus **new issues** in the post-handoff code.

### 🔴 Critical / release-blocking

- **C-1 — YOLOv8n is AGPL-3.0.** `Assets/CyberEye/Models/yolov8n.onnx` ships Ultralytics weights;
  copyleft attaches on conveying the app (APK distribution or public repo), forcing full
  open-source or an Enterprise license. No `LICENSE` in repo. Fine for private dev; **hard gate
  before any release.** *Fix:* swap to YOLOX-Nano / NanoDet (Apache-2.0) — decode changes in
  `Detector.Awake`'s FunctionalGraph, `ParseInto` mostly stays; add `LICENSE` + `THIRD-PARTY-NOTICES`.

- **C-2 — Live color/gamma into the detector is unvalidated, and it gates the core feature.**
  `CyberEyeYUV.shader` does a BGR swap + `pow(rgb, 2.2)` tuned for *display*, then feeds that RT to
  YOLO — which expects sRGB RGB in [0,1]. The only accuracy datapoint ("4 people @0.89") was on a
  directly-loaded JPEG that **bypasses this shader**. Risk: silent recall loss on the live feed.
  *Fix:* the hook exists (`EyeCameraFeed.DumpFirstFrame` → `feed_dump.png` on dev build / `dump_feed`
  marker) — pull it, eyeball channels/brightness, give the detector its own sRGB input material.
  Highest-ROI fix in the project.

### 🟠 Moderate

- **NEW — Eye↔display FOV mismatch → brackets systematically misplaced.** `TargetOverlay.Update`
  (`:276`) maps the detection's normalized position through the **display** camera frustum
  (`cam.fieldOfView`, ~46–50° on One Pro), but the bbox is in the **Eye camera** image space (~72°
  HFOV — hardcoded as `EyeHFovDeg = 72f` in `TargetPins.cs:19`). Lateral placement is scaled by
  ≈`tan(25°)/tan(36°)≈0.64`, so brackets sit too close to center and drift worse toward the
  periphery; the box won't land on the object as seen through the glasses. Same error in the
  `TargetPins` ray. This is the *spatial* sibling of C-2. *Fix:* derive the direction from the Eye
  intrinsics (`EyeHFovDeg/EyeVFovDeg`), or calibrate the Eye→display transform once. Note the
  inconsistency — `TargetPins` knows the Eye FOV; `TargetOverlay` ignores it. Verify on-device
  against a known object at the frame edge.

- **NEW — Inference watchdog can double-run and race on shared tensors.** `Detector.cs:132`
  force-clears `m_Busy=false` after a 12 s "wedged" inference but does not cancel the in-flight
  `RunInferenceAsync`. If that task was merely stalled and later resumes, a second inference starts
  concurrently — both touch the shared `m_Input`/`m_InputRT`/worker and call `ParseInto`, risking
  tensor corruption or a native crash. Rare but real. *Fix:* generation counter / `CancellationToken`
  so a timed-out run's continuation no-ops.

### 🟡 Minor

- **Residual native leak in `EyeCameraFeed` (partial C-11).** `OnDestroy` (`:187`) releases `_rgbRT`
  but never `Destroy()`s the wrapper, and `_yuvMat` is never destroyed. Other components were fixed;
  this one wasn't.
- **`new Material(Shader.Find(...))` unguarded against null** (`EyeCameraFeed:30`, `HudOverlayController:20`,
  `TargetOverlay:80`). Logs on null but still constructs `new Material(null)` → exception. A
  stripped/renamed shader would hard-crash rather than degrade. (Handoff enhancement #14.)
- **C-9 — NMS thresholds baked into the compiled graph at `Awake`.** Runtime `confidence`/`iou`
  changes don't re-drive NMS; blocks a confidence slider until thresholds are graph inputs.
- **C-12 — manifest hygiene.** `AndroidTargetSdkVersion: 0` (set explicit 34+); `RECORD_AUDIO` /
  `FOREGROUND_SERVICE*` declared-but-unused → Play data-safety friction (drop until recording ships).
- **Tracker has no temporal prediction (roadmap #5).** `ObjectTracker` only `Lerp`s 0.5 on match; a
  moving object's bracket freezes then snaps between sparse frames. EMA on size + constant-velocity
  predictor would let boxes glide.

### ✅ Verified correct (credit — don't reopen)
- YOLO decode graph (transpose → ReduceMax/ArgMax → NMS → IndexSelect) is correct.
- Perf discipline is textbook: `ScheduleIterable` amortized ~8 layers/frame + async readback +
  `m_Busy` guard + boot warmup (pre-compiles kernels). Frame-serial gating avoids re-inferring stale
  Eye pixels.
- Capture-pose reprojection addresses the "targeting offset" field report.
- Privacy posture strong: on-device only, fictional dossiers behind a boot disclaimer, feed-dump
  gated behind dev/marker, no secrets tracked.
- Additive-optics UX discipline (black=transparent, edge-anchored canvases, center-clear) followed
  precisely; all critical assets present (TMP Settings, SDF font, 5 shaders, model) → "invisible HUD"
  risk mitigated.
- Build tooling is repo-relative (C-8 fixed) — builds correctly from this machine's path.

### Architecture & maintainability
- Clean component-per-concern; clear `Detector → ObjectTracker → TargetOverlay → {TargetPins,
  AudioDirector, HUD}` flow over read-only surfaces; `PerfGuard` as a cross-cutting throttle; single
  programmatic scene (`SceneBuilder` → committed `Main.unity`). Appropriate for the app's size.
- **Maintainability: excellent** — nearly every non-obvious decision carries a "why + field report"
  comment; consistent `CyberLog` tags with grep-able logcat markers. **Main gap: no automated tests**
  (roadmap #9's headless smoke test is the right first step).
- **Cross-cutting product finding: zero user interactivity.** `SettingsController` is a stub, volume
  keys aren't wired, no gaze-dwell (the platform's proven input). CyberEye is currently a passive
  "look and it overlays" experience — no class filter, dossier dismiss, intensity control, or pause.

---

## Part 2 — Competitive Landscape (Market Lens)

CyberEye sits at the intersection of **on-device vision detection** and **smart-glasses AR**. Almost
every comparable product is aimed at **utility** (answer questions, read text, assist); CyberEye is
aimed at **entertainment/aesthetic**. That gap is the whole story.

| Product | Platform | Display | Vision capability | On-device | Continuous HUD | Interactivity | Purpose | Price |
|---|---|---|---|---|---|---|---|---|
| **CyberEye** | XREAL One Pro + Beam Pro | Optical see-through | On-device YOLOv8n object detection (COCO subset) | ✅ Yes | ✅ Yes (sparse feed) | ❌ None yet | Entertainment / sci-fi demo | Free (dev) |
| **Meta Ray-Ban / Oakley Meta** | Own glasses | ❌ None (audio) | Cloud "look and ask" multimodal AI | ❌ Cloud | ❌ On-demand snapshot | Voice | Capture + AI assistant | $299 |
| **Google Lens** | Phone | Phone screen (AR text) | Cloud image recognition + OCR/translate | ❌ Cloud | ❌ Point-and-shoot | Touch | Visual search / utility | Free |
| **Envision Glasses** | Google Glass EE2 | Monocular | Text/scene/object/face recognition | Mixed | ❌ On-command | Touchpad/voice | Accessibility (low-vision) | $800+ |
| **Seeing AI / Be My Eyes** | Phone | Phone screen | Scene/text/product; live human help | ❌ Cloud | ❌ On-command | Touch/voice | Accessibility | Free |
| **XREAL AURA + Gemini** (upcoming) | XREAL glasses | 70° optical see-through | Gemini "understands what you see" | ❌ Cloud | Conversational | Voice | AI assistant (platform-native) | TBD |

**Per-competitor takeaways (praise / complaints):**
- **Meta Ray-Ban** — praised as the most *practical* smart glasses (discreet capture, useful AI,
  comfort, $299). Complaints: **no AR display** (answers are audio-only), camera weak in low light,
  ~4 h battery, and a **serious privacy backlash** (EFF warning, NOYB cease-and-desist, courthouse
  bans, recording-LED workarounds, face-recognition fears). Cloud-dependent.
- **Google Lens** — praised: reliable identification, free. Complaints: **internet-dependent**,
  accuracy varies with image quality, phone-bound (not a hands-free HUD).
- **Envision Glasses** — praised for text reading. Complaints: **object recognition is mixed**
  ("objects the device should have no issue with were total non-runners"), scene descriptions
  "basic," and it's **$800+**.
- **Seeing AI / Be My Eyes** — free and capable, but **phone-only** and (Seeing AI) iOS-only.
- **XREAL AURA + Gemini** — the platform owner is moving toward **cloud Gemini** "understands what
  you see" assistance. This is the strategic backdrop: the default XREAL vision experience will be a
  cloud AI assistant, not a stylized on-device HUD.

**Table-stakes CyberEye lacks** (vs the field): capture & share, a natural-language "what is this?"
query, text reading/translation, and broad object understanding. *But* CyberEye deliberately isn't a
utility app — treat these as optional, not obligations.

**Genuine differentiation opportunities:**
1. **On-device, zero-cloud, low-latency, private** — directly answers the biggest complaints about
   Meta (privacy) and Lens (internet dependency). Already true; **message it loudly**.
2. **Stylized "augmented perception" is an empty category.** Searches for a shipping cyberpunk
   detection HUD on real optical-see-through glasses return **design assets, not products** — evidence
   the niche is unoccupied.
3. **It actually shows you something in your view** (optical HUD overlay) — unlike Meta's display-less,
   audio-only model.
4. **The "put these on and feel like you're in a film" demo** — the experience that *sells glasses*,
   which none of the utility players deliver.

**Evidence caveat:** the smart-glasses app category is immature and the "cyberpunk HUD app" niche is
essentially empty as a shipping-product category — competitive signal there is thin by nature, not by
oversight.

### Sources
- [Meta AI Glasses](https://www.meta.com/ai-glasses/) · [Ray-Ban Meta (Wikipedia)](https://en.wikipedia.org/wiki/Ray-Ban_Meta) · [EFF: Think Twice Before Buying Meta's Ray-Bans](https://www.eff.org/deeplinks/2026/03/think-twice-buying-or-using-metas-ray-bans) · [Fast Company: controversies](https://www.fastcompany.com/91571430/the-many-controversies-of-metas-ai-glasses) · [Moor Insights review](https://moorinsightsstrategy.com/research-notes/ray-ban-meta-smart-glasses-review-better-cooler-and-more-useful-than-ever/)
- [XREAL nebulaOS 2.0 (Android Central)](https://www.androidcentral.com/gaming/virtual-reality/xreals-nebulaos-2-0-update-for-the-beam-pro-is-crucial-this-is-what-the-huge-patch-brings) · [XREAL AURA](https://www.xreal.com/us/aura) · [XREAL SDK 3.0 notes](https://docs.xreal.com/Release%20Note/XREAL%20SDK%203.0.0)
- [Google Lens (Wikipedia)](https://en.wikipedia.org/wiki/Google_Lens) · [Google Lens on Play](https://play.google.com/store/apps/details?id=com.google.ar.lens&hl=en)
- [Seeing AI vs Envision (SaaSHub)](https://www.saashub.com/compare-seeing-ai-vs-envision-glasses) · [Smart glasses for vision impairment 2025](https://www.specialneeds.com/articles/assistive-tech/vision/smart-glasses-for-vision-impairment-in-2025-meta-ray-ban-vs-envision-vs-specialized-options/) · [Are the Envision Glasses any good? (Vision Ireland)](https://vi.ie/are-the-envision-glasses-any-good/)

---

## Part 3 — Product Manager Perspective

**Core value proposition:** *"Put on your XREAL glasses and see the world through a cyberpunk
'augmented perception' HUD — on-device, private, and cinematic."* The current build **partly**
delivers it: the boot cinematic, neon HUD grade, lock-on target brackets, fictional dossiers, world
pins, and reactive audio absolutely sell the fantasy. What holds it back: (a) detection may not work
well on the *live* feed (C-2), (b) there's **nothing to do** (no interactivity), and (c) you **can't
capture or share** the one thing this app is built to produce — a jaw-dropping moment.

**Strategic call:** don't chase the utility players (Meta/Gemini/Lens) — they'll win "what is this?"
with cloud LLMs. Win the lane they can't be bothered with: **a polished, private, shareable
entertainment/demo experience** that makes the hardware feel magic.

**Prioritized roadmap (impact × effort):**

| # | Feature | Impact | Effort | Rationale |
|---|---|---|---|---|
| 1 | **Validate/fix live color-gamma path (C-2)** | 🔥 High | Low | Table stakes — the core detection must work on the live feed, not just the test JPEG. Hook already exists. |
| 2 | **Capture & share the HUD moment** | 🔥 High | Med–High | The single biggest lever for an entertainment app; Meta's whole appeal is capture+share. Manifest already declares `MEDIA_PROJECTION`. **See scope risk below.** |
| 3 | **Basic interactivity (gaze-dwell + phone-tap)** — select target, filter classes, freeze/dismiss dossier, FX intensity | 🔥 High | Med | Turns passive into engaging; uses the platform's *proven* inputs; wires the existing `SettingsController` stub (pair with C-9). |
| 4 | **Finish world-lock + tracker smoothing** | High | Med | Reprojection is already in; add EMA/velocity so boxes stick to objects and glide between sparse frames. Big perceived-quality jump. |
| 5 | **Swap to Apache model (C-1)** | High (strategic) | Med | Unblocks *any* public/demo distribution or a public repo. |
| 6 | **Themed modes / progression** (threat-scan, bio-scan, escalating alerts) | Med | Med–High | Replay value beyond the first-run novelty. |
| 7 | **Onboarding for nebulaOS launch + "scanning" clarity** | Med | Low | Removes the #1 first-run failure (flat 2D launch) and the "is it broken?" feeling on sparse frames. |
| 8 | **Thermal/battery HUD + graceful degradation** | Low–Med | Low | `PerfGuard` already reads thermal headroom; surface it for demo reliability. |

**Scope/complexity risks:**
- **Capture is not as simple as a screenshot.** On optical see-through, a display framebuffer grab
  shows the HUD over black — *not what the user saw*. Real "share" needs the HUD composited over a
  camera frame (the Beam Pro's dual 50 MP cameras, or the Eye feed). That's a real mini-project, and
  worth messaging honestly.
- **Eye→display calibration** (the moderate finding) is hardware-specific and fiddly — time-box it;
  "good enough" alignment may satisfy the demo bar.
- **Model swap** reworks the decode graph (YOLOX grid+objectness) — contained but not trivial.

---

## Part 4 — End-User Perspective

**Persona — "Kai," an AR enthusiast and gadget show-off.** Owns the XREAL One Pro + Beam Pro, loves
sci-fi, wants to *feel* like they're in Cyberpunk/Ghost in the Shell and make friends say "whoa, let
me try." Not a productivity or accessibility user.

**First-use walkthrough (narrated):**
> I sideload CyberEye and tap it — and get a flat 2D window on the phone with boxes stuck on a stock
> photo. Confusing. *(Turns out I had to launch it from the nebulaOS launcher in the glasses; nothing
> told me that.)* Second try, from nebula: the lenses light up — **"FICTIONAL // ENTERTAINMENT"**,
> then **NIGHT CITY OS** types in, a scanline sweep, "SYSTEMS NOMINAL," and the banner slides out of
> my view. *Genuinely cool.* Corner brackets frame my vision like a viewfinder; a radar ping pulses
> while it "scans." I look at my friend — a beat later a magenta box snaps onto them with a lock chirp
> and a glitchy **CITIZEN DOSSIER**: a fake name, a Neon Heights address, "Organ lien active." We both
> laugh. A world-pin floats next to them reading `ORGANIC 01 · 2.3M`.
>
> Then… that's kind of it. I can't point at the *thing I want* scanned. I can't freeze the dossier to
> read it before it retargets. The detection sometimes goes quiet for seconds and I wonder if it broke.
> And the moment I most want — to **record this and send it to my group chat** — there's no way to
> capture it. The novelty is huge for 3 minutes; then I've seen it.

**What would delight:** the boot cinematic, the lock-on chirp + dossier reveal, world pins that stay
put as you walk, the "I'm in a movie" feeling. These already land.

**What would make them churn:** the flat-2D first launch, zero control, sparse/uncertain detection,
repetitive dossiers, and — above all — **not being able to share it**.

**5 quality-of-life improvements (lived-experience, not technical):**
1. **Let me capture & share** a clip/photo of the HUD moment. For a show-off app this is #1.
2. **Let me aim.** A gaze reticle or phone-ray to say "scan *that* one," and a way to **freeze/pin a
   dossier** so I can actually read it before it swaps.
3. **Never look dead.** When frames are sparse, make "scanning" feel deliberate (progress, a manual
   "SCAN" trigger) so I don't think it crashed.
4. **Tell me how to start it right.** A one-line "launch from nebulaOS for MR mode" on the 2D screen
   kills the worst first-run moment.
5. **More variety.** Rotate dossier themes / add a "threat escalation" mode so the 4th look still
   surprises me.

---

## Part 5 — Top 5 Overall Recommendations (Synthesis)

Ranked by leverage across all four lenses:

1. **Prove the core actually works on the live feed (C-2).** Everything else is theater if on-glasses
   detection is degraded. It's the highest-ROI fix, the validation hook already exists, and it's a
   table-stakes gate from every lens. *(Engineering + PM + User.)*
2. **Ship capture & share.** The biggest product lever for an entertainment/demo app and CyberEye's
   clearest edge over display-less Meta — but budget for the optical-see-through compositing reality
   (HUD over a Beam Pro camera frame, not a bare screenshot). *(PM + User + Market.)*
3. **Add real interactivity via gaze-dwell + phone-tap.** Wire the `SettingsController` stub: aim/select
   a target, filter classes, freeze/dismiss dossiers, adjust intensity. Turns a 3-minute novelty into
   something you engage with, using the platform's *proven* inputs. *(User + PM + Engineering.)*
4. **Lean into the differentiation and finish the spatial polish.** Own "private, on-device, stylized
   augmented perception" (vs Meta's privacy backlash and Lens's cloud dependency); finish world-lock +
   tracker smoothing and correct the Eye↔display FOV mapping so brackets truly sit on objects. *(Market
   + Engineering.)*
5. **Clear the release blockers: Apache-licensed model (C-1) + the manifest/leak/watchdog cleanups.**
   Swap YOLOv8n→YOLOX/NanoDet, add `LICENSE`/notices, set target SDK, prune unused perms, fix the
   `EyeCameraFeed` leak and the watchdog double-run race — so a public demo or repo flip is safe.
   *(Engineering + PM.)*
