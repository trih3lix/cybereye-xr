using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// M5+M6: neon target boxes on detections (IoU-tracked) + a fixed cyberpunk "scan readout" dossier panel for
// the primary (highest-confidence) locked target, populated from BiometricProfileGenerator (deterministic
// per track id, so it stays stable while locked). Optical see-through: black = transparent, neon shows.
public class TargetOverlay : MonoBehaviour
{
    [SerializeField] Detector detector;
    [SerializeField] float distance = 3.5f;
    [SerializeField] int maxBoxes = 8;

    readonly ObjectTracker _tracker = new ObjectTracker();
    readonly Dictionary<int, BiometricProfileGenerator.Profile> _profiles = new();
    Renderer[] _boxes;
    Material[] _mats;
    Camera _cam;                 // cached head camera (Camera.main); resolved once, re-acquired only if lost
    Transform _dossierRoot;      // dossier canvas root, parented to the camera once it resolves
    bool _parented;
    int _lastInfer = -1, _panelId = -1;

    // dossier panel
    Text _dTitle, _dBody;

    // exposed for AudioDirector (M7)
    public int PrimaryId => _panelId;
    public int TrackCount => _tracker != null ? _tracker.Tracks.Count : 0;

    static Color ClassColor(int id)
    {
        if (id == 0) return new Color(0f, 1f, 0.9f);
        if (id == 15 || id == 16) return new Color(1f, 0.2f, 0.8f);
        return new Color(1f, 0.85f, 0f);
    }

    void Start()
    {
        _cam = Camera.main;
        var sh = Shader.Find("CyberEye/TargetBox");
        if (sh == null) CyberLog.Err("GLOW", "CyberEye/TargetBox shader missing");
        _boxes = new Renderer[maxBoxes];
        _mats = new Material[maxBoxes];
        for (int i = 0; i < maxBoxes; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Target" + i;
            var col = q.GetComponent<Collider>(); if (col) Destroy(col);
            _mats[i] = new Material(sh);
            _boxes[i] = q.GetComponent<Renderer>();
            _boxes[i].material = _mats[i];
            _boxes[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _boxes[i].receiveShadows = false;
            _boxes[i].enabled = false;
        }
        BuildDossierPanel();
        // AppBootController expects Camera.main can be NULL at Start (XR rig not resolved yet). Parent now
        // if it is up; otherwise defer + retry in Update so the overlays never orphan at the world origin.
        TryParentToCamera();
        if (_cam == null) CyberLog.Warn("GLOW", "MainCamera=NULL at Start; deferring overlay parenting");
        CyberLog.Info("GLOW", $"target overlay init (boxes={maxBoxes})");
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
        if (_dossierRoot != null) _dossierRoot.SetParent(p, false);
        _parented = true;
        CyberLog.Info("GLOW", "overlay parented to head camera");
    }

    void BuildDossierPanel()
    {
        // world-space canvas (identity rotation, like the M1 HUD which rendered correctly), lower-left of view.
        // Parented to the camera later by TryParentToCamera; the local transform below is head-relative.
        var go = new GameObject("Dossier", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(760, 300);
        rt.localScale = Vector3.one * 0.0016f;
        rt.localPosition = new Vector3(-0.62f, -0.42f, 1.6f);
        rt.localRotation = Quaternion.identity;
        _dossierRoot = go.transform;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _dTitle = MkText(go.transform, font, 40, new Vector2(0, 120), new Color(1f, 0.2f, 0.8f), TextAnchor.UpperLeft);
        _dBody  = MkText(go.transform, font, 28, new Vector2(0, 78),  new Color(0f, 1f, 0.9f),  TextAnchor.UpperLeft);
        _dBody.rectTransform.sizeDelta = new Vector2(740, 240);
        _dTitle.gameObject.SetActive(false); _dBody.gameObject.SetActive(false);
    }

    Text MkText(Transform parent, Font font, int size, Vector2 pos, Color c, TextAnchor a)
    {
        var go = new GameObject("t", typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.color = c; t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(740, 100); rt.anchoredPosition = pos;
        return t;
    }

    void Update()
    {
        if (detector == null || _boxes == null) return;
        if (detector.InferenceId != _lastInfer) { _lastInfer = detector.InferenceId; _tracker.Update(detector.Detections, Time.time); }
        _tracker.Age(Time.time);

        if (_cam == null) _parented = false;            // camera lost / never resolved -> (re)acquire below
        if (!_parented) { TryParentToCamera(); if (!_parented) return; }
        var cam = _cam;
        float worldH = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldW = worldH * Mathf.Max(cam.aspect, 1f);

        var tracks = _tracker.Tracks;
        ObjectTracker.Track primary = null;
        for (int i = 0; i < _boxes.Length; i++)
        {
            if (i < tracks.Count)
            {
                var tr = tracks[i];
                if (primary == null || tr.conf > primary.conf) primary = tr;
                float cx = tr.box.x + tr.box.width * 0.5f, cy = tr.box.y + tr.box.height * 0.5f;
                var t = _boxes[i].transform;
                t.localPosition = new Vector3((cx - 0.5f) * worldW, -(cy - 0.5f) * worldH, distance);
                t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                t.localScale = new Vector3(Mathf.Max(0.06f, tr.box.width * worldW), Mathf.Max(0.06f, tr.box.height * worldH), 1f);
                _mats[i].SetColor("_Color", ClassColor(tr.classId));
                _boxes[i].enabled = true;
            }
            else _boxes[i].enabled = false;
        }
        UpdateDossier(primary);
    }

    void UpdateDossier(ObjectTracker.Track primary)
    {
        bool show = primary != null;
        _dTitle.gameObject.SetActive(show);
        _dBody.gameObject.SetActive(show);
        if (!show) { _panelId = -1; return; }
        if (!_profiles.TryGetValue(primary.id, out var p))
        {
            p = BiometricProfileGenerator.ForTrack(primary.id, primary.classId);
            _profiles[primary.id] = p;
        }
        if (primary.id != _panelId)
        {
            _panelId = primary.id;
            _dTitle.text = $"[ {p.title} ]  //TGT-{primary.id:000}";
            _dTitle.color = ClassColor(primary.classId);
            _dBody.text = $"{p.name}\n{p.line1}\n{p.line2}\n{p.stat}\n> {p.fact}";
            CyberLog.Info("DOSSIER", $"TGT-{primary.id} {p.title}: {p.name} | {p.stat}");
        }
    }
}
