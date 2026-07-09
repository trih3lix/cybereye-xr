using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// M5+M6: neon target brackets on detections (IoU-tracked) + a cyberpunk "scan readout" dossier
// panel for the primary (highest-confidence) locked target, populated from
// BiometricProfileGenerator (deterministic per track id, stable while locked).
//
// Optical see-through: black = transparent; the design is thin bright strokes and a
// barely-there panel fill so the real world stays visible. Targets animate acquire→lock
// (loose breathing brackets → tight steady corners + reticle) which carries visual interest
// between the Eye's sparse (~1/3-5 s) detection updates.
public class TargetOverlay : MonoBehaviour
{
    [SerializeField] Detector detector;
    [SerializeField] float distance = 3.5f;
    [SerializeField] int maxBoxes = 8;
    [SerializeField] float lockDelay = 1.2f;      // seconds a track must persist to count as locked
    [SerializeField] float typeSpeed = 110f;      // dossier typewriter, chars/sec

    readonly ObjectTracker _tracker = new ObjectTracker();
    readonly Dictionary<int, BiometricProfileGenerator.Profile> _profiles = new();
    Renderer[] _boxes;
    Material[] _mats;
    float[] _lock;               // smoothed 0..1 lock state per slot
    Renderer _reticle;
    Material _reticleMat;
    Camera _cam;                 // cached head camera (Camera.main); resolved once, re-acquired only if lost
    Transform _dossierRoot;      // dossier canvas root, parented to the camera once it resolves
    bool _parented;
    int _lastInfer = -1, _panelId = -1;
    HudController _hud;          // event feed (optional, found once)

    // dossier panel (TMP)
    TMP_Text _dTitle, _dBody, _dFoot;
    RawImage _thumb;                 // last-lock snapshot of the target (field request)
    RenderTexture _thumbRT;
    RectTransform _sweep;
    Coroutine _typing, _sweeping;
    string _bodyFull = "";
    float _footTick;

    // R2: one-shot lock burst (Update-driven, no coroutine) + dossier micro-glitches
    Renderer _burst;
    Material _burstMat;
    float _burstT = 1f, _burstFrom, _burstTo;
    const float BurstDur = 0.25f;
    int _lastBurstId = -1;
    string _footClean = "";
    bool _footCorrupt;
    Color _titleCol;
    Vector2 _titleHome;
    int _ghostFrames;
    static readonly char[] GlitchChars = { '#', '%', '&', '@', '!', '?' };

    // exposed for AudioDirector (M7)
    public int PrimaryId => _panelId;
    public int TrackCount => _tracker != null ? _tracker.Tracks.Count : 0;

    // exposed for the HUD threat chip (R2): most frequent wanted class among live tracks,
    // ties broken by summed confidence; -1 when nothing is tracked.
    public int DominantClassId { get; private set; } = -1;

    void Start()
    {
        _cam = Camera.main;
        _hud = FindFirstObjectByType<HudController>();
        var sh = Shader.Find("CyberEye/TargetBox");
        if (sh == null) CyberLog.Err("GLOW", "CyberEye/TargetBox shader missing");
        _boxes = new Renderer[maxBoxes];
        _mats = new Material[maxBoxes];
        _lock = new float[maxBoxes];
        for (int i = 0; i < maxBoxes; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Target" + i;
            var col = q.GetComponent<Collider>(); if (col) Destroy(col);
            _mats[i] = new Material(sh);
            _mats[i].SetFloat("_TOffset", i * 1.73f);   // de-sync the pulse between boxes
            _boxes[i] = q.GetComponent<Renderer>();
            _boxes[i].material = _mats[i];
            _boxes[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _boxes[i].receiveShadows = false;
            _boxes[i].enabled = false;
        }
        // rotating diamond reticle for the locked primary
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Reticle";
            var col = q.GetComponent<Collider>(); if (col) Destroy(col);
            _reticleMat = new Material(sh);
            _reticleMat.SetFloat("_Mode", 1f);
            _reticle = q.GetComponent<Renderer>();
            _reticle.material = _reticleMat;
            _reticle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _reticle.receiveShadows = false;
            _reticle.enabled = false;
        }
        // R2: one-shot lock-burst ring (shader mode 2), fired when a primary first fully locks
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "LockBurst";
            var col = q.GetComponent<Collider>(); if (col) Destroy(col);
            _burstMat = new Material(sh);
            _burstMat.SetFloat("_Mode", 2f);
            _burstMat.SetColor("_Color", CyberPalette.Locked);
            _burst = q.GetComponent<Renderer>();
            _burst.material = _burstMat;
            _burst.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _burst.receiveShadows = false;
            _burst.enabled = false;
        }
        BuildDossierPanel();
        // AppBootController expects Camera.main can be NULL at Start (XR rig not resolved yet). Parent now
        // if it is up; otherwise defer + retry in Update so the overlays never orphan at the world origin.
        TryParentToCamera();
        if (_cam == null) CyberLog.Warn("GLOW", "MainCamera=NULL at Start; deferring overlay parenting");
        CyberLog.Info("GLOW", $"target overlay init (boxes={maxBoxes})");
    }

    void OnDestroy()
    {
        if (_mats != null) foreach (var m in _mats) if (m != null) Destroy(m);
        if (_reticleMat != null) Destroy(_reticleMat);
        if (_thumbRT != null) { _thumbRT.Release(); _thumbRT = null; }
        if (_burstMat != null) Destroy(_burstMat);
    }

    // Parent the boxes + dossier to the head once Camera.main resolves (mirrors HudOverlayController's
    // SizeToFov camera-retry). Called from Start and, if the rig was not ready then, again from Update.
    void TryParentToCamera()
    {
        if (_parented) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        var p = _cam.transform;
        for (int i = 0; i < _boxes.Length; i++)
            if (_boxes[i] != null) _boxes[i].transform.SetParent(p, false);
        if (_reticle != null) _reticle.transform.SetParent(p, false);
        if (_burst != null) _burst.transform.SetParent(p, false);
        if (_dossierRoot != null) _dossierRoot.SetParent(p, false);
        _parented = true;
        CyberLog.Info("GLOW", "overlay parented to head camera");
    }

    // ───────────────────────────── dossier construction ─────────────────────────────

    void BuildDossierPanel()
    {
        // world-space canvas (identity rotation, like the M1 HUD which rendered correctly), lower-left of
        // view. Parented to the camera later by TryParentToCamera; local transform below is head-relative.
        var go = new GameObject("Dossier", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(600, 300);
        rt.localScale = Vector3.one * 0.00115f;  // R3: slimmer footprint (field report)
        // Inside the One Pro's visible window (~+/-0.70m x +/-0.40m at 1.6m):
        // panel spans x -0.66..0.18, y -0.36..0.06 — nothing clips at display edges.
        rt.localPosition = new Vector3(-0.24f, -0.15f, 1.6f);
        rt.localRotation = Quaternion.identity;
        _dossierRoot = go.transform;

        // barely-there grouping fill — enough to read as a panel, not enough to wash the view
        MkImage(go.transform, "bg", new Vector2(0, 0), new Vector2(600, 300), CyberPalette.PanelFill);
        // frame: thin bright strokes (4 strips) + a yellow header accent bar
        var frame = CyberPalette.Dim;
        MkImage(go.transform, "bT", new Vector2(0,  148), new Vector2(600, 3), frame);
        MkImage(go.transform, "bB", new Vector2(0, -148), new Vector2(600, 3), frame);
        MkImage(go.transform, "bL", new Vector2(-298, 0), new Vector2(3, 300), frame);
        MkImage(go.transform, "bR", new Vector2( 298, 0), new Vector2(3, 300), frame);
        MkImage(go.transform, "accent", new Vector2(-225, 148), new Vector2(140, 6), CyberPalette.Yellow);

        // fixed vertical slots — title / body / footer can never overlap (the old layout stacked
        // two overflowing rects on top of each other, which is exactly the "writes over itself" bug)
        _dTitle = MkTmp(go.transform, "title", 23, new Vector2(0, 116), new Vector2(560, 40));
        _dTitle.fontStyle = FontStyles.Bold;
        _dTitle.characterSpacing = 4f;
        _dTitle.textWrappingMode = TextWrappingModes.NoWrap;
        _dTitle.overflowMode = TextOverflowModes.Ellipsis;
        _titleHome = ((RectTransform)_dTitle.transform).anchoredPosition;   // ghost-offset restore point

        _dBody = MkTmp(go.transform, "body", 18, new Vector2(-56, -8), new Vector2(448, 196));
        _dBody.color = CyberPalette.Cyan;
        _dBody.lineSpacing = 6f;
        _dBody.textWrappingMode = TextWrappingModes.Normal;
        _dBody.overflowMode = TextOverflowModes.Ellipsis;   // never spill outside the panel

        _dFoot = MkTmp(go.transform, "foot", 14, new Vector2(0, -128), new Vector2(560, 26));
        _dFoot.color = CyberPalette.Dim;
        _dFoot.characterSpacing = 2f;
        _dFoot.textWrappingMode = TextWrappingModes.NoWrap;
        _dFoot.overflowMode = TextOverflowModes.Ellipsis;

        // target snapshot: a small photo of the tagged object captured at lock time so
        // the viewer can confirm the target after looking away (field request). Sits in
        // the panel's right column (the body text was narrowed to make room).
        _thumbRT = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
        var thumbFrame = MkImage(go.transform, "thumbFrame", new Vector2(230, 22), new Vector2(126, 126), CyberPalette.Dim);
        var thumbGo = new GameObject("thumb", typeof(RawImage));
        thumbGo.transform.SetParent(go.transform, false);
        _thumb = thumbGo.GetComponent<RawImage>();
        _thumb.texture = _thumbRT;
        _thumb.raycastTarget = false;
        var trt = (RectTransform)thumbGo.transform;
        trt.sizeDelta = new Vector2(120, 120);
        trt.anchoredPosition = new Vector2(230, 22);

        // retarget scan sweep (hidden until used)
        var sweepImg = MkImage(go.transform, "sweep", new Vector2(0, 140), new Vector2(588, 4), CyberPalette.Cyan);
        _sweep = (RectTransform)sweepImg.transform;
        sweepImg.gameObject.SetActive(false);

        _dossierRoot.gameObject.SetActive(false);
    }

    Image MkImage(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return img;
    }

    TMP_Text MkTmp(Transform parent, string name, float size, Vector2 pos, Vector2 rect)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = rect; rt.anchoredPosition = pos;
        return t;
    }

    // ───────────────────────────── frame update ─────────────────────────────

    void Update()
    {
        if (detector == null || _boxes == null) return;
        if (detector.InferenceId != _lastInfer) { _lastInfer = detector.InferenceId; _tracker.Update(detector.Detections, Time.time); }
        _tracker.Age(Time.time);
        UpdateDominantClass();   // before the parenting gate so the HUD chip is live regardless

        // R2: restore last frame's 1-frame dossier glitches (footer corruption / title ghost)
        if (_footCorrupt) { _footCorrupt = false; _dFoot.text = _footClean; }
        if (_ghostFrames > 0 && --_ghostFrames == 0)
        {
            _dTitle.color = _titleCol;
            ((RectTransform)_dTitle.transform).anchoredPosition = _titleHome;
        }

        if (_cam == null) _parented = false;            // camera lost / never resolved -> (re)acquire below
        if (!_parented) { TryParentToCamera(); if (!_parented) return; }
        var cam = _cam;
        float worldH = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldW = worldH * Mathf.Max(cam.aspect, 1f);

        var tracks = _tracker.Tracks;
        ObjectTracker.Track primary = null;
        int primarySlot = -1;
        for (int i = 0; i < _boxes.Length; i++)
        {
            if (i < tracks.Count)
            {
                var tr = tracks[i];
                if (primary == null || tr.conf > primary.conf) { primary = tr; primarySlot = i; }
                float cx = tr.box.x + tr.box.width * 0.5f, cy = tr.box.y + tr.box.height * 0.5f;
                var t = _boxes[i].transform;
                t.localPosition = new Vector3((cx - 0.5f) * worldW, -(cy - 0.5f) * worldH, distance);
                t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                t.localScale = new Vector3(Mathf.Max(0.06f, tr.box.width * worldW), Mathf.Max(0.06f, tr.box.height * worldH), 1f);

                // acquire→lock animation: target state from track age, smoothed per slot
                float target = (Time.time - tr.firstSeen) >= lockDelay ? 1f : 0f;
                _lock[i] = Mathf.MoveTowards(_lock[i], target, Time.deltaTime * 3.5f);
                var baseCol = CyberPalette.ForClass(tr.classId);
                _mats[i].SetColor("_Color", Color.Lerp(baseCol, CyberPalette.Locked, _lock[i] * 0.65f));
                _mats[i].SetFloat("_Lock", _lock[i]);
                _boxes[i].enabled = true;
            }
            else { _boxes[i].enabled = false; _lock[i] = 0f; }
        }

        // reticle rides the locked primary's center
        bool reticleOn = primary != null && primarySlot >= 0 && _lock[primarySlot] > 0.85f;
        if (_reticle != null)
        {
            _reticle.enabled = reticleOn;
            if (reticleOn)
            {
                float cx = primary.box.x + primary.box.width * 0.5f, cy = primary.box.y + primary.box.height * 0.5f;
                var t = _reticle.transform;
                t.localPosition = new Vector3((cx - 0.5f) * worldW, -(cy - 0.5f) * worldH, distance - 0.02f);
                t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                float s = Mathf.Clamp(Mathf.Min(primary.box.width * worldW, primary.box.height * worldH) * 0.30f, 0.04f, 0.12f);
                t.localScale = new Vector3(s, s, 1f);
                _reticleMat.SetColor("_Color", CyberPalette.Locked);
            }
        }

        // R2: one-shot thin ring burst the moment a target first reaches full lock —
        // fired at the reticle's position/size, expands ~2.6x over 250ms while the
        // shader fades it out (makes locks feel punchy; lock sfx already fires via
        // AudioDirector's PrimaryId watch).
        if (_burst != null)
        {
            if (reticleOn && _reticle != null && primary.id != _lastBurstId)
            {
                _lastBurstId = primary.id;
                var bt = _burst.transform;
                bt.localPosition = _reticle.transform.localPosition + new Vector3(0f, 0f, -0.01f);
                bt.localRotation = Quaternion.Euler(0f, 180f, 0f);
                float s0 = _reticle.transform.localScale.x;
                _burstFrom = s0 * 0.8f;
                _burstTo = s0 * 2.6f;
                _burstT = 0f;
                _burst.enabled = true;
                CyberLog.Info("GLOW", $"lock burst TGT-{primary.id:000}");
            }
            if (primary == null) _lastBurstId = -1;   // re-celebrate after a full drop
            if (_burst.enabled)
            {
                _burstT += Time.deltaTime;
                float k = _burstT / BurstDur;
                if (k >= 1f) _burst.enabled = false;
                else
                {
                    float s = Mathf.Lerp(_burstFrom, _burstTo, k);
                    _burst.transform.localScale = new Vector3(s, s, 1f);
                    _burstMat.SetFloat("_Lock", k);   // shader mode 2: radius + fade from progress
                }
            }
        }

        UpdateDossier(primary);

        // footer hex ticker keeps the panel alive between sparse Eye frames (~1.4 Hz rebuild, tiny string)
        if (_panelId >= 0 && Time.time >= _footTick)
        {
            _footTick = Time.time + 0.7f;
            RefreshFooter();
            // R2: occasionally corrupt 2 hex chars for exactly one frame (restored above)
            if (Random.value < 0.22f) CorruptFooterOneFrame();
        }
    }

    // R2: dominant class for the HUD threat chip. O(n²) over ≤ maxBoxes tracks, no allocs.
    void UpdateDominantClass()
    {
        var tracks = _tracker.Tracks;
        int best = -1; float bestScore = 0f;
        for (int i = 0; i < tracks.Count; i++)
        {
            int cid = tracks[i].classId;
            bool seen = false;
            for (int j = 0; j < i; j++) if (tracks[j].classId == cid) { seen = true; break; }
            if (seen) continue;
            int n = 0; float conf = 0f;
            for (int j = 0; j < tracks.Count; j++)
                if (tracks[j].classId == cid) { n++; conf += tracks[j].conf; }
            float score = n * 10f + conf;
            if (score > bestScore) { bestScore = score; best = cid; }
        }
        DominantClassId = best;
    }

    // ───────────────────────────── dossier content ─────────────────────────────

    void UpdateDossier(ObjectTracker.Track primary)
    {
        bool show = primary != null;
        if (_dossierRoot != null && _dossierRoot.gameObject.activeSelf != show)
            _dossierRoot.gameObject.SetActive(show);
        if (!show) { _panelId = -1; return; }
        if (primary.id == _panelId) return;

        // retarget: swap content once, typewriter-reveal the body, fire the scan sweep
        _panelId = primary.id;
        if (!_profiles.TryGetValue(primary.id, out var p))
        {
            p = BiometricProfileGenerator.ForTrack(primary.id, primary.classId);
            _profiles[primary.id] = p;
            PruneProfiles();
        }
        var col = CyberPalette.ForClass(primary.classId);
        _dTitle.text = $"[ {p.title} ]  //TGT-{primary.id:000}";
        _titleCol = Color.Lerp(col, CyberPalette.Locked, 0.5f);
        // R2: 1-frame magenta ghost offset on retarget (restored at the top of Update)
        _dTitle.color = CyberPalette.Magenta;
        ((RectTransform)_dTitle.transform).anchoredPosition = _titleHome + new Vector2(3f, -2f);
        _ghostFrames = 1;
        _bodyFull = $"{p.name}\n{p.line1}\n{p.line2}\n{p.stat}\n> {p.fact}";
        _conf = primary.conf;
        RefreshFooter();
        SnapshotThumb(primary);

        if (_typing != null) StopCoroutine(_typing);
        _typing = StartCoroutine(TypeBody());
        if (_sweeping != null) StopCoroutine(_sweeping);
        _sweeping = StartCoroutine(SweepOnce());

        _hud?.PushEvent($"TGT-{primary.id:000} ACQUIRED :: {p.title}", col);
        CyberLog.Info("DOSSIER", $"TGT-{primary.id} {p.title}: {p.name} | {p.stat}");
    }

    float _conf;

    void RefreshFooter()
    {
        int bars = Mathf.Clamp(Mathf.RoundToInt(_conf * 5f), 0, 5);
        var sb = new StringBuilder(64);
        sb.Append("CONF ");
        for (int i = 0; i < 5; i++) sb.Append(i < bars ? '▮' : '▯');
        sb.Append(' ').Append(Mathf.RoundToInt(_conf * 100f)).Append("%  ::  ");
        sb.Append(CyberPalette.HexTicker(_panelId * 7919 + (int)(Time.time * 1.43f)));
        _footClean = sb.ToString();
        _dFoot.text = _footClean;
    }

    // R2: swap 2 chars in the hex zone (back half) for glitch glyphs; Update restores
    // _footClean on the very next frame — pure text swap, no layout or material churn.
    void CorruptFooterOneFrame()
    {
        if (_footClean.Length < 8) return;
        var a = _footClean.ToCharArray();
        for (int n = 0; n < 2; n++)
            a[Random.Range(_footClean.Length / 2, _footClean.Length)] = GlitchChars[Random.Range(0, GlitchChars.Length)];
        _dFoot.text = new string(a);
        _footCorrupt = true;
    }

    // Single-owner typewriter: cancelled and restarted on every retarget, so reveals can
    // never interleave (the old per-event text writes raced and drew over each other).
    IEnumerator TypeBody()
    {
        _dBody.text = _bodyFull + "▉";
        _dBody.maxVisibleCharacters = 0;
        int total = _bodyFull.Length;
        float shown = 0f;
        while (shown < total)
        {
            shown += typeSpeed * Time.deltaTime;
            _dBody.maxVisibleCharacters = Mathf.Min(total, (int)shown) + 1; // +1 keeps the cursor visible
            yield return null;
        }
        // reveal done: blink the block cursor
        while (true)
        {
            _dBody.maxVisibleCharacters = total;      // cursor hidden
            yield return new WaitForSeconds(0.45f);
            _dBody.maxVisibleCharacters = total + 1;  // cursor shown
            yield return new WaitForSeconds(0.45f);
        }
    }

    IEnumerator SweepOnce()
    {
        _sweep.gameObject.SetActive(true);
        const float dur = 0.16f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float y = Mathf.Lerp(140f, -140f, t / dur);
            _sweep.anchoredPosition = new Vector2(0, y);
            yield return null;
        }
        _sweep.gameObject.SetActive(false);
    }

    // Copy the detection's padded bbox region out of the live feed into the thumbnail RT.
    // bbox is normalized, top-left origin (camera image space); GL UV origin is bottom-left.
    void SnapshotThumb(ObjectTracker.Track tr)
    {
        if (_thumb == null || _thumbRT == null || detector == null) return;
        var src = detector.UiSource;
        if (src == null) { _thumb.enabled = false; return; }

        const float pad = 0.15f;
        float w = Mathf.Clamp01(tr.box.width  * (1f + pad * 2f));
        float h = Mathf.Clamp01(tr.box.height * (1f + pad * 2f));
        float x = Mathf.Clamp01(tr.box.x - tr.box.width  * pad);
        float y = Mathf.Clamp01(tr.box.y - tr.box.height * pad);
        if (x + w > 1f) w = 1f - x;
        if (y + h > 1f) h = 1f - y;
        if (w < 0.01f || h < 0.01f) { _thumb.enabled = false; return; }

        var scale  = new Vector2(w, h);
        var offset = new Vector2(x, 1f - y - h);
        Graphics.Blit(src, _thumbRT, scale, offset);
        _thumb.enabled = true;
    }

    void PruneProfiles()
    {
        if (_profiles.Count < 32) return;
        var live = new HashSet<int>();
        foreach (var t in _tracker.Tracks) live.Add(t.id);
        var dead = new List<int>();
        foreach (var kv in _profiles) if (!live.Contains(kv.Key)) dead.Add(kv.Key);
        foreach (var id in dead) _profiles.Remove(id);
    }
}
