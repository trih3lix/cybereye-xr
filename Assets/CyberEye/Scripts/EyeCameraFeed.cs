using UnityEngine;
using UnityEngine.Android;
using Unity.XR.XREAL;

// Option A (optical see-through): raw Eye RGB frames -> RGB RenderTexture used ONLY as the Detector input.
// Not displayed (the One Pro shows the real world optically). Frames are sporadic on this hardware
// (~keyframe rate), so detection updates occasionally -> fits a cyberpunk "scan/lock" cadence.
public class EyeCameraFeed : MonoBehaviour
{
    [SerializeField] HudController hud;

    XREALRGBCameraTexture _cam;
    RenderTexture _rgbRT;
    Material _yuvMat;
    bool _permRequested, _capturing, _gotFrame;
    float _retryT, _fpsT;
    int _newFrames;
    Vector2Int _res = new Vector2Int(-1, -1);

    // RGB feed for the Detector; null until the first real frame arrives (so we never detect on black).
    public Texture PreviewTex => _gotFrame ? _rgbRT : null;

    void Start()
    {
        var sh = Shader.Find("CyberEye/YUVtoRGB");
        if (sh == null) CyberLog.Err("EYE", "CyberEye/YUVtoRGB shader missing");
        _yuvMat = new Material(sh);
        _rgbRT = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        _cam = XREALRGBCameraTexture.CreateSingleton();
        _cam.OnRGBCameraUpdate += OnFrame;
        CyberLog.Info("EYE", "raw-path init (detector feed only); requesting CAMERA");
        TryStart();
    }

    void TryStart()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            if (!_permRequested) { _permRequested = true; Permission.RequestUserPermission(Permission.Camera); CyberLog.Info("EYE", "CAMERA requested"); }
            return;
        }
#endif
        _capturing = _cam.StartCapture();
        CyberLog.Info("EYE", _capturing ? "StartCapture OK (Eye connected)" : "StartCapture FAILED (Eye not attached / busy)");
        if (hud) hud.SetStatus(_capturing ? "OPTIC SCANNING" : "CONNECT OPTIC - XREAL EYE");
    }

    void OnFrame()
    {
        var yuv = _cam.GetYUVFormatTextures();
        if (yuv == null || yuv[0] == null || _yuvMat == null) return;
        _yuvMat.SetTexture("_UTex", yuv[1]);
        _yuvMat.SetTexture("_VTex", yuv[2]);
        Graphics.Blit(yuv[0], _rgbRT, _yuvMat);   // YUV planes -> RGB RenderTexture
        _newFrames++;
        if (!_gotFrame) { _gotFrame = true; if (hud) hud.SetStatus("OPTIC ONLINE"); CyberLog.Info("EYE", "first real frame -> detector feed live"); }
        var r = _cam.GetResolution();
        if (r.x != _res.x || r.y != _res.y) _res = r;
    }

    void Update()
    {
        if (!_capturing)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _retryT += Time.unscaledDeltaTime;
            if (_retryT >= 2f) { _retryT = 0f; if (Permission.HasUserAuthorizedPermission(Permission.Camera)) TryStart(); }
#endif
            return;
        }
        _fpsT += Time.unscaledDeltaTime;
        if (_fpsT >= 2f) { CyberLog.Info("EYE", $"raw new-frames={_newFrames / _fpsT:F1}/s res={_res.x}x{_res.y}"); _newFrames = 0; _fpsT = 0f; }
    }

    void OnDestroy()
    {
        if (_cam != null) { _cam.OnRGBCameraUpdate -= OnFrame; if (_cam.IsCapturing) _cam.StopCapture(); }
        if (_rgbRT != null) _rgbRT.Release();
    }
}
