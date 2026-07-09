using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// M1 HUD: the cyberpunk boot banner + status line on the world-space canvas, plus a
// runtime-built scrolling event feed (TMP): newest line at the bottom, older lines fade
// up and out; new entries land with a 2-frame horizontal glitch-jitter.
// Scene wiring (title/status legacy Text) is untouched; the feed is created in code.
public class HudController : MonoBehaviour
{
    [SerializeField] Text title;
    [SerializeField] Text status;

    const int FeedLines = 4;
    const float FeedLife = 7f;          // seconds until a line is fully faded
    readonly List<TMP_Text> _feed = new();
    readonly List<float> _born = new();
    readonly List<Vector2> _basePos = new();
    int _glitchFrames;

    void Awake()
    {
        if (title) title.text = "NIGHT CITY OS";
        SetStatus("BOOTING…");
    }

    void Start()
    {
        BuildFeed();
    }

    public void SetStatus(string s)
    {
        if (status) status.text = "> " + s;
        CyberLog.Info("HUD", s);
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

    void BuildFeed()
    {
        var parent = status != null ? status.transform.parent : transform;
        for (int i = 0; i < FeedLines; i++)
        {
            var go = new GameObject("feed" + i, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = 22;
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.characterSpacing = 2f;
            t.raycastTarget = false;
            t.text = "";
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(700, 28);
            // stack upward from beneath the status line region
            rt.anchoredPosition = new Vector2(0, -210 - (FeedLines - 1 - i) * 30);
            _feed.Add(t);
            _born.Add(-999f);
            _basePos.Add(rt.anchoredPosition);
        }
    }

    void Update()
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
}
