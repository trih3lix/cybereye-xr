using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 6DoF showcase: every locked target drops a WORLD-ANCHORED pin — a floating
// designation + live distance readout that stays put in the room as the wearer
// walks around (head tracking does the anchoring; no ARAnchor needed for a demo
// pin). Pins expire after a while so the room never clutters.
public sealed class TargetPins : MonoBehaviour
{
    [SerializeField] TargetOverlay overlay;
    [SerializeField] Detector detector;             // capture-time head pose for the ray
    [SerializeField] float fallbackDepthMeters = 2.2f;
    [SerializeField] float pinLifeSeconds = 90f;
    [SerializeField] int maxPins = 6;

    // Approximate Eye-camera FOV for monocular size-prior depth (precision matters
    // less than getting VARIED, plausible distances instead of a constant 2.2 m).
    const float EyeHFovDeg = 72f, EyeVFovDeg = 45f;

    /// <summary>Characteristic largest extent (m) per COCO class — the size prior
    /// behind depth-from-bbox. depth = extent / (2·tan(angularExtent/2)).</summary>
    static float ExtentPrior(int id) => id switch
    {
        0 => 1.70f,             // person
        14 => 0.30f,            // bird
        15 => 0.50f,            // cat
        16 => 0.80f,            // dog
        39 => 0.27f,            // bottle
        40 => 0.22f,            // wine glass
        41 => 0.12f,            // cup
        42 => 0.20f, 43 => 0.25f, 44 => 0.18f,   // fork, knife, spoon
        45 => 0.20f,            // bowl
        46 => 0.20f, 47 => 0.08f, 48 => 0.15f, 49 => 0.08f,   // banana, apple, sandwich, orange
        56 => 0.90f,            // chair
        57 => 2.00f,            // couch
        58 => 0.50f,            // potted plant
        59 => 2.00f,            // bed
        60 => 1.50f,            // dining table
        62 => 0.90f,            // tv
        63 => 0.35f,            // laptop
        64 => 0.11f, 65 => 0.18f, 66 => 0.45f, 67 => 0.15f,   // mouse, remote, keyboard, phone
        68 => 0.50f, 69 => 0.70f, 70 => 0.30f,                 // microwave, oven, toaster
        71 => 0.60f, 72 => 1.70f,                              // sink, refrigerator
        73 => 0.25f, 74 => 0.30f, 75 => 0.30f, 76 => 0.20f,   // book, clock, vase, scissors
        77 => 0.35f, 78 => 0.25f, 79 => 0.20f,                 // teddy, hair drier, toothbrush
        _ => 0.5f
    };

    static float DepthFromBox(int classId, Rect box)
    {
        float angDeg = Mathf.Max(box.width * EyeHFovDeg, box.height * EyeVFovDeg);
        if (angDeg < 0.5f) return -1f;
        float depth = ExtentPrior(classId) / (2f * Mathf.Tan(angDeg * 0.5f * Mathf.Deg2Rad));
        return Mathf.Clamp(depth, 0.5f, 7f);
    }

    sealed class Pin
    {
        public GameObject Go;
        public TMP_Text Label;
        public string Name;
        public int ClassId;
        public float Born;
    }

    readonly List<Pin> _pins = new List<Pin>();
    Camera _cam;
    int _lastPinnedTrackId = -1;
    int _serial;
    float _refreshT;

    void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        // Drop a pin the moment a NEW track reaches full lock. The bbox center +
        // the head camera pose give a good-enough ray; depth is assumed.
        if (overlay != null && overlay.HasLockedPrimary &&
            overlay.LockedPrimaryTrackId != _lastPinnedTrackId)
        {
            _lastPinnedTrackId = overlay.LockedPrimaryTrackId;
            Rect box = overlay.LockedPrimaryBox;

            // Ray direction from the bbox center — rebased onto the CAPTURE-time head
            // pose (the head has moved since that frame; using the current pose put
            // pins wherever the user happened to be looking when inference finished).
            var ray = _cam.ViewportPointToRay(new Vector3(
                box.x + box.width * 0.5f,
                1f - (box.y + box.height * 0.5f),   // detector boxes are top-left origin
                0f));
            Vector3 origin = ray.origin;
            Vector3 dir = ray.direction;
            if (detector != null && detector.HasCapturePose)
            {
                Vector3 dirLocal = Quaternion.Inverse(_cam.transform.rotation) * ray.direction;
                dir = detector.CaptureRotation * dirLocal;
                origin = detector.CapturePosition;
            }

            // Depth from the class-size prior (a cup fills the box only up close; a
            // couch that fills it is far) — no more constant 2.2 m readouts.
            float depth = DepthFromBox(overlay.LockedPrimaryClassId, box);
            if (depth <= 0f) depth = fallbackDepthMeters;

            SpawnPin(origin + dir * depth, overlay.LockedPrimaryClassId);
        }

        // Billboard + distance tick at ~5 Hz; expire old pins (fade the last 5 s).
        _refreshT += Time.deltaTime;
        bool tick = _refreshT >= 0.2f;
        if (tick) _refreshT = 0f;

        for (int i = _pins.Count - 1; i >= 0; i--)
        {
            var p = _pins[i];
            float age = Time.time - p.Born;
            if (age >= pinLifeSeconds) { Destroy(p.Go); _pins.RemoveAt(i); continue; }

            var t = p.Go.transform;
            Vector3 toCam = t.position - _cam.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-4f)
                t.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);

            if (tick)
            {
                float dist = Vector3.Distance(t.position, _cam.transform.position);
                float fade = Mathf.Clamp01((pinLifeSeconds - age) / 5f);
                var c = CyberPalette.ForClass(p.ClassId);
                c.a = 0.9f * fade;
                p.Label.color = c;
                p.Label.text = $"+ {p.Name}\n{dist:0.0}M";
            }
        }
    }

    void SpawnPin(Vector3 worldPos, int classId)
    {
        while (_pins.Count >= maxPins) { Destroy(_pins[0].Go); _pins.RemoveAt(0); }

        _serial++;
        var go = new GameObject($"Pin {_serial}");
        go.transform.position = worldPos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = 0.65f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(1.6f, 0.4f);

        string name = $"{CyberPalette.ClassWord(classId)} {_serial:00}";
        _pins.Add(new Pin { Go = go, Label = tmp, Name = name, ClassId = classId, Born = Time.time });
        CyberLog.Info("PIN", $"world pin '{name}' at {worldPos} (class {classId})");
    }

    /// <summary>Bearings (degrees, world yaw) of live pins — consumed by the compass tape.</summary>
    public void CollectBearings(List<float> into)
    {
        into.Clear();
        if (_cam == null) return;
        Vector3 eye = _cam.transform.position;
        foreach (var p in _pins)
        {
            Vector3 d = p.Go.transform.position - eye;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) continue;
            into.Add(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }
    }

    void OnDestroy()
    {
        foreach (var p in _pins) if (p.Go != null) Destroy(p.Go);
        _pins.Clear();
    }
}
