using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// M1 HUD + R2 polish: owns the scene canvas furniture.
//  • Boot banner (scene-wired legacy Text title/status): shows the FICTIONAL disclaimer,
//    then types "NIGHT CITY OS" with a block cursor, flashes SYSTEMS NOMINAL behind a
//    scanline sweep, holds ~2s, and EXITS the center of view — 2-frame glitch, 0.8s fade;
//    the title parks top-center, small, at 40% alpha as a persistent brand mark (field
//    report: the boot text used to sit in the middle of the user's vision forever).
//  • Status line: post-boot it is a transient toast in the top-center slot — fades in on
//    SetStatus, holds 4s, fades out. Identical repeats (the optic retry loop) are deduped.
//  • Event feed (TMP): newest line lands bottom-right with a 2-frame glitch-jitter, older
//    lines fade up and out. During boot, SetStatus calls are routed here so subsystem
//    chatter can never overwrite the legal disclaimer.
// Additive optics: black = transparent; thin bright strokes only, center kept clear.
public class HudController : MonoBehaviour
{
    [SerializeField] Text title;
    [SerializeField] Text status;

    enum Phase { Boot, Exiting, Live }
    Phase _phase = Phase.Boot;

    // boot-exit choreography (canvas coords: 600x400, center origin)
    static readonly Vector2 ParkPos  = new Vector2(0f, 172f);  // brand-mark slot (top-center)
    static readonly Vector2 ToastPos = new Vector2(0f, 128f);  // toast slot (just below it)
    const float ParkScale = 0.42f, ParkAlpha = 0.40f;
    Coroutine _bootSeq;
    RectTransform _sweepLine;    // boot-complete scanline (runtime-built, hidden)

    // status toasts (post-boot)
    const float ToastIn = 0.15f, ToastHold = 4f, ToastOut = 0.5f, ToastRepeatCd = 8f;
    Coroutine _toast;
    string _toastText;
    float _toastHoldUntil, _toastAgainAt;
    string _lastBootRoute;

    // event feed
    const int FeedLines = 4;
    const float FeedLife = 7f;          // seconds until a line is fully faded
    readonly List<TMP_Text> _feed = new();
    readonly List<float> _born = new();
    readonly List<Vector2> _basePos = new();
    int _glitchFrames;

    AudioDirector _audio;        // boot cinematic sfx hooks (optional)

    void Awake()
    {
        if (title) title.text = "NIGHT CITY OS";
        if (status) status.text = "> BOOTING…";
        CyberLog.Info("HUD", "BOOTING…");
        // build in Awake (not Start) so other components' Start-time SetStatus calls
        // already have a live feed to land in
        BuildFeed();
        BuildSweep();
        _audio = FindFirstObjectByType<AudioDirector>();
    }

    // ───────────────────────────── boot flow (AppBootController) ─────────────────────────────

    // Direct write to the center banner — reserved for the boot/disclaimer flow.
    public void SetBootText(string t, string s)
    {
        if (title) title.text = t;
        if (status) status.text = "> " + s;
        CyberLog.Info("HUD", $"boot text: {t} / {s}");
    }

    // Boot-complete cinematic: type the OS title in, sweep, hold, then clear the center.
    public void BeginPostBoot()
    {
        if (_phase != Phase.Boot) return;
        if (title == null || status == null) { _phase = Phase.Live; return; }
        _phase = Phase.Exiting;
        _bootSeq = StartCoroutine(PostBootRoutine());
    }

    IEnumerator PostBootRoutine()
    {
        var tRt = (RectTransform)title.transform;
        var sRt = (RectTransform)status.transform;
        Vector2 tHome = tRt.anchoredPosition, sHome = sRt.anchoredPosition;

        // 1) type the OS title char-by-char with a block cursor + occasional 2-frame jitter
        const string osName = "NIGHT CITY OS";
        status.text = "";
        for (int i = 1; i <= osName.Length; i++)
        {
            title.text = i < osName.Length ? osName.Substring(0, i) + "▉" : osName;
            if ((i % 4) == 1) tRt.anchoredPosition = tHome + new Vector2(Random.Range(-3f, 3f), 0f);
            yield return new WaitForSeconds(0.033f);
            tRt.anchoredPosition = tHome;
        }
        status.text = "> SYSTEMS NOMINAL";
        CyberLog.Info("HUD", "SYSTEMS NOMINAL");

        // 2) quick scanline sweep down the whole canvas + scan sfx
        if (_sweepLine != null)
        {
            if (_audio) _audio.PlayScanSfx();
            _sweepLine.gameObject.SetActive(true);
            const float dur = 0.28f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                _sweepLine.anchoredPosition = new Vector2(0f, Mathf.Lerp(200f, -200f, t / dur));
                yield return null;
            }
            _sweepLine.gameObject.SetActive(false);
        }

        // 3) let SYSTEMS NOMINAL land, then leave the center of view
        yield return new WaitForSeconds(2f);

        // 4) 2-frame glitch-jitter + glitch sfx
        if (_audio) _audio.PlayGlitchSfx();
        for (int f = 0; f < 2; f++)
        {
            float dx = f == 0 ? 4f : -4f;
            tRt.anchoredPosition = tHome + new Vector2(dx, 0f);
            sRt.anchoredPosition = sHome + new Vector2(-dx, 0f);
            yield return null;
        }
        tRt.anchoredPosition = tHome;
        sRt.anchoredPosition = sHome;

        // 5) fade the status out while the title shrinks up into its brand-mark park slot
        const float fade = 0.8f;
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fade);
            tRt.anchoredPosition = Vector2.Lerp(tHome, ParkPos, k);
            tRt.localScale = Vector3.one * Mathf.Lerp(1f, ParkScale, k);
            SetTextAlpha(title, Mathf.Lerp(1f, ParkAlpha, k));
            SetTextAlpha(status, 1f - k);
            yield return null;
        }
        FinalizeExit(tRt, sRt);
        _bootSeq = null;
    }

    void FinalizeExit(RectTransform tRt, RectTransform sRt)
    {
        tRt.anchoredPosition = ParkPos;
        tRt.localScale = Vector3.one * ParkScale;
        SetTextAlpha(title, ParkAlpha);
        sRt.anchoredPosition = ToastPos;
        SetTextAlpha(status, 0f);
        _phase = Phase.Live;
        CyberLog.Info("HUD", "boot banner parked — center view clear");
    }

    void OnDisable()
    {
        // this component owns only its own coroutines (boot cinematic + toast)
        StopAllCoroutines();
        _toast = null;
        _bootSeq = null;
        if (_phase == Phase.Exiting && title && status)
            FinalizeExit((RectTransform)title.transform, (RectTransform)status.transform);
        else if (_phase == Phase.Live && status)
            SetTextAlpha(status, 0f);   // never leave a half-faded toast behind
    }

    // ───────────────────────────── status / title API ─────────────────────────────

    // Boot: routed to the event feed (the center line belongs to the disclaimer).
    // Live: transient top-center toast — fade in, hold 4s, fade out.
    public void SetStatus(string s)
    {
        CyberLog.Info("HUD", s);
        if (status == null) return;
        if (_phase != Phase.Live)
        {
            if (s != _lastBootRoute) { _lastBootRoute = s; PushEvent(s, CyberPalette.Dim); }
            return;
        }
        // identical text while visible or within cooldown: ignore (the optic retry loop
        // repeats "CONNECT OPTIC" every 2s; it re-toasts at most every ~8s)
        if (s == _toastText && (_toast != null || Time.time < _toastAgainAt)) return;
        _toastText = s;
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine("> " + s));
    }

    IEnumerator ToastRoutine(string line)
    {
        _toastAgainAt = Time.time + ToastRepeatCd;
        status.text = line;
        _toastHoldUntil = Time.time + ToastHold;
        for (float t = 0f; t < ToastIn; t += Time.deltaTime)
        {
            SetTextAlpha(status, t / ToastIn);
            yield return null;
        }
        SetTextAlpha(status, 1f);
        while (Time.time < _toastHoldUntil) yield return null;
        for (float t = 0f; t < ToastOut; t += Time.deltaTime)
        {
            SetTextAlpha(status, 1f - t / ToastOut);
            yield return null;
        }
        SetTextAlpha(status, 0f);
        _toastAgainAt = Time.time + ToastRepeatCd;
        _toast = null;
    }

    public void SetTitle(string s)
    {
        if (title) title.text = s;
    }

    // Push a line into the event feed (used by TargetOverlay on lock events and free for
    // any subsystem). Newest at the bottom; older lines shift up and fade with age.
    public void PushEvent(string msg, Color color)
    {
        if (_feed.Count == 0) return;   // feed unavailable (no canvas)
        // shift content up: line[0] oldest … line[n-1] newest
        for (int i = 0; i < FeedLines - 1; i++)
        {
            _feed[i].text = _feed[i + 1].text;
            _feed[i].color = _feed[i + 1].color;
            _born[i] = _born[i + 1];
        }
        var newest = _feed[FeedLines - 1];
        newest.text = "» " + msg;
        newest.color = color;
        _born[FeedLines - 1] = Time.time;
        _glitchFrames = 2;
        CyberLog.Info("FEED", msg);
    }

    // ───────────────────────────── runtime construction ─────────────────────────────

    void BuildFeed()
    {
        var parent = status != null ? status.transform.parent : transform;
        for (int i = 0; i < FeedLines; i++)
        {
            var go = new GameObject("feed" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = 19;
            t.alignment = TextAlignmentOptions.BottomRight;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.characterSpacing = 2f;
            t.raycastTarget = false;
            t.text = "";
            var rt = (RectTransform)go.transform;
            // Pin to the canvas's bottom-right corner with edge anchors: the scene HUD
            // canvas is only 600x400, so center-relative offsets below -200 rendered
            // OUTSIDE the canvas — in world space right on top of the dossier panel
            // (the "text writing over itself in the frame" report). Edge anchoring is
            // canvas-size-proof and keeps the feed clear of the dossier's world slot.
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(300, 24);
            rt.anchoredPosition = new Vector2(-14, 12 + (FeedLines - 1 - i) * 26);
            _feed.Add(t);
            _born.Add(-999f);
            _basePos.Add(rt.anchoredPosition);
        }
    }

    void BuildSweep()
    {
        if (status == null) return;              // no canvas -> nothing to sweep
        var go = new GameObject("bootSweep", typeof(Image));
        go.transform.SetParent(status.transform.parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(CyberPalette.Cyan.r, CyberPalette.Cyan.g, CyberPalette.Cyan.b, 0.55f);
        img.raycastTarget = false;
        _sweepLine = (RectTransform)go.transform;
        _sweepLine.sizeDelta = new Vector2(600f, 3f);
        go.SetActive(false);
    }

    // ───────────────────────────── frame update ─────────────────────────────

    void Update()
    {
        UpdateFeed();
    }

    void UpdateFeed()
    {
        if (_feed.Count == 0) return;
        float now = Time.time;
        for (int i = 0; i < FeedLines; i++)
        {
            float age = now - _born[i];
            if (age > FeedLife || _feed[i].text.Length == 0) { SetAlpha(_feed[i], 0f); continue; }
            SetAlpha(_feed[i], Mathf.Clamp01(1f - age / FeedLife));
        }
        // glitch-jitter the newest entry for its first two frames
        var newestRt = (RectTransform)_feed[FeedLines - 1].transform;
        if (_glitchFrames > 0)
        {
            _glitchFrames--;
            newestRt.anchoredPosition = _basePos[FeedLines - 1] + new Vector2((_glitchFrames % 2 == 0) ? 3f : -3f, 0);
        }
        else newestRt.anchoredPosition = _basePos[FeedLines - 1];
    }

    static void SetAlpha(TMP_Text t, float a)
    {
        var c = t.color; c.a = a; t.color = c;
    }

    static void SetTextAlpha(Text t, float a)
    {
        var c = t.color; c.a = a; t.color = c;
    }
}
