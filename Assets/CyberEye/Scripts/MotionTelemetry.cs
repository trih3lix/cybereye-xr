using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 6DoF ambience: a heading tape across the top of the HUD (with markers for the
// world pins), a walking-speed ticker, and a glitch burst on fast head turns —
// the HUD smears and flashes for a beat, like the optics can't keep up. All
// driven purely by the head pose; ~2 string rebuilds per second.
public sealed class MotionTelemetry : MonoBehaviour
{
    [SerializeField] Transform hudCanvas;     // head-locked HUD canvas (text parents here)
    [SerializeField] TargetPins pins;         // optional: pin bearings appear on the tape
    [SerializeField] float glitchAngularSpeed = 240f;   // deg/s of head yaw that triggers a glitch

    TMP_Text _tape, _motion;
    Camera _cam;
    Vector3 _lastPos, _canvasHome;
    float _lastYaw, _speedSmoothed;
    float _uiT, _glitchLeft;
    readonly List<float> _bearings = new List<float>(8);
    readonly StringBuilder _sb = new StringBuilder(64);
    static readonly string[] Cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
            _lastPos = _cam.transform.position;
            _lastYaw = Yaw(_cam.transform);
        }
        if (_tape == null)
        {
            if (hudCanvas == null) return;
            BuildTexts();
            _canvasHome = hudCanvas.localPosition;
        }

        // Speed from head translation (smoothed); yaw rate for the glitch trigger.
        Vector3 pos = _cam.transform.position;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float speed = (pos - _lastPos).magnitude / dt;
        _speedSmoothed = Mathf.Lerp(_speedSmoothed, Mathf.Min(speed, 4f), dt * 3f);
        _lastPos = pos;

        float yaw = Yaw(_cam.transform);
        float yawRate = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / dt;
        _lastYaw = yaw;
        if (yawRate > glitchAngularSpeed && _glitchLeft <= 0f)
        {
            _glitchLeft = 0.22f;
            CyberLog.Info("FX", $"glitch burst (yaw rate {yawRate:0}deg/s)");
        }

        UpdateGlitch(dt);

        _uiT += dt;
        if (_uiT < 0.45f) return;
        _uiT = 0f;

        _tape.text = ComposeTape(yaw);
        _motion.text = _speedSmoothed < 0.15f
            ? "STATIONARY"
            : $"VEL {_speedSmoothed:0.0} M/S";
    }

    static float Yaw(Transform t)
    {
        Vector3 f = t.forward;
        f.y = 0f;
        return f.sqrMagnitude < 1e-4f ? 0f : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // "<   NW · 312°   >" with '¦' markers where world pins sit inside a ±30° window.
    string ComposeTape(float yaw)
    {
        float heading = (yaw + 360f) % 360f;
        string card = Cardinals[Mathf.RoundToInt(heading / 45f) % 8];

        _sb.Length = 0;
        _sb.Append("<  ");

        if (pins != null)
        {
            pins.CollectBearings(_bearings);
            int left = 0, right = 0;
            foreach (float b in _bearings)
            {
                float rel = Mathf.DeltaAngle(heading, (b + 360f) % 360f);
                if (Mathf.Abs(rel) <= 30f) _sb.Append('¦');       // in view band
                else if (rel < 0f) left++;
                else right++;
            }
            if (left > 0) _sb.Insert(0, new string('·', Mathf.Min(left, 3)));
            _sb.Append("  ").Append(card).Append(' ').Append(Mathf.RoundToInt(heading)).Append('°').Append("  ");
            if (right > 0) _sb.Append(new string('·', Mathf.Min(right, 3)));
        }
        else
        {
            _sb.Append(card).Append(' ').Append(Mathf.RoundToInt(heading)).Append('°');
        }

        _sb.Append("  >");
        return _sb.ToString();
    }

    void UpdateGlitch(float dt)
    {
        if (_glitchLeft <= 0f || hudCanvas == null) return;
        _glitchLeft -= dt;
        if (_glitchLeft <= 0f)
        {
            hudCanvas.localPosition = _canvasHome;
            if (_tape != null) _tape.color = CyberPalette.Dim;
            return;
        }
        // Sub-pixel HUD smear + hot magenta tape while the "optics recalibrate".
        hudCanvas.localPosition = _canvasHome + new Vector3(
            (Random.value - 0.5f) * 0.008f,
            (Random.value - 0.5f) * 0.006f,
            0f);
        if (_tape != null) _tape.color = CyberPalette.Magenta;
    }

    void BuildTexts()
    {
        // Tape: very top edge, ABOVE the parked NIGHT CITY OS title. Ticker: bottom
        // edge — the title parks at ~y 170 post-boot, which is what clipped the
        // previous placements (field: "tape is clipped by other text/graphics").
        _tape = MakeText("CompassTape", new Vector2(0, 192), 15, CyberPalette.Dim);
        _motion = MakeText("MotionTicker", new Vector2(0, -188), 11, CyberPalette.Dim);
    }

    TMP_Text MakeText(string name, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(hudCanvas, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 28);
        return t;
    }
}
