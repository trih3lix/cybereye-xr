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
    [SerializeField] float pinDepthMeters = 2.2f;   // no depth sensor: assume arm+ distance
    [SerializeField] float pinLifeSeconds = 90f;
    [SerializeField] int maxPins = 6;

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
            var ray = _cam.ViewportPointToRay(new Vector3(
                box.x + box.width * 0.5f,
                1f - (box.y + box.height * 0.5f),   // detector boxes are top-left origin
                0f));
            SpawnPin(ray.origin + ray.direction * pinDepthMeters, overlay.LockedPrimaryClassId);
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
