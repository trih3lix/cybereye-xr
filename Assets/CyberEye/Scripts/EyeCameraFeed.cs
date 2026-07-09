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
    bool _permRequested, _capturing, _gotFrame, _dumped;
    float _retryT, _fpsT, _noFrameT;
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
        if (_capturing) _noFrameT = 0f;   // first Eye frame can be seconds out; don't trip the watchdog immediately
        CyberLog.Info("EYE", _capturing ? "StartCapture OK (Eye connected)" : "StartCapture FAILED (Eye not attached / busy)");
        // Quiet re-arms: only toast the very first successful start, and failures.
        // (Field report: watchdog restarts re-toasted "CONNECT OPTIC" in a cycle.)
        if (hud)
        {
            if (_capturing && !_everStarted) hud.SetStatus("OPTIC SCANNING");
            else if (!_capturing) hud.SetStatus("CONNECT OPTIC - XREAL EYE");
        }
        if (_capturing) _everStarted = true;
    }

    bool _everStarted;

    // Guarded StopCapture: XREAL's StopCapture can throw if the session already tore the camera down
    // (backgrounded / Eye unplugged); never let that break our re-arm or teardown paths.
    void StopCaptureSafe()
    {
        try { if (_cam != null && _cam.IsCapturing) _cam.StopCapture(); }
        catch (System.Exception e) { CyberLog.Warn("EYE", "StopCapture threw: " + e.Message); }
    }

    void OnFrame()
    {
        var yuv = _cam.GetYUVFormatTextures();
        if (yuv == null || yuv[0] == null || _yuvMat == null) return;
        _yuvMat.SetTexture("_UTex", yuv[1]);
        _yuvMat.SetTexture("_VTex", yuv[2]);
        Graphics.Blit(yuv[0], _rgbRT, _yuvMat);   // YUV planes -> RGB RenderTexture
        _newFrames++; _noFrameT = 0f;
        if (!_gotFrame)
        {
            _gotFrame = true;
            if (hud) hud.SetStatus("OPTIC ONLINE");
            CyberLog.Info("EYE", "first real frame -> detector feed live");
            if (Debug.isDebugBuild && !_dumped) { _dumped = true; DumpFirstFrame(); }
        }
        var r = _cam.GetResolution();
        if (r.x != _res.x || r.y != _res.y) _res = r;
    }

    // C-2 evidence hook: one-shot debug dump of the raw RGB feed RT (exactly what the detector consumes,
    // pre-letterbox) so it can be adb-pulled to validate channel order + gamma on device. Dev builds only,
    // fully guarded so a readback/encode/IO hiccup can never break the feed.
    void DumpFirstFrame()
    {
        try
        {
            var prev = RenderTexture.active;
            var tex = new Texture2D(_rgbRT.width, _rgbRT.height, TextureFormat.RGBA32, false);
            RenderTexture.active = _rgbRT;
            tex.ReadPixels(new Rect(0, 0, _rgbRT.width, _rgbRT.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var png = ImageConversion.EncodeToPNG(tex);
            Destroy(tex);
            var path = System.IO.Path.Combine(Application.persistentDataPath, "feed_dump.png");
            System.IO.File.WriteAllBytes(path, png);
            Debug.Log("[CyberEye] feed dump: " + path);
        }
        catch (System.Exception e) { CyberLog.Warn("EYE", "feed dump failed: " + e.Message); }
    }

    void Update()
    {
        if (!_capturing)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _retryT += Time.unscaledDeltaTime;
            if (_retryT >= 4f) { _retryT = 0f; if (Permission.HasUserAuthorizedPermission(Permission.Camera)) TryStart(); }
#endif
            return;
        }

        // Frame-timeout watchdog. The Eye is sporadic — field logs show healthy rates
        // as low as 0.1 frames/s (one frame per 10s), so the old 10s threshold tore
        // down a WORKING camera in a cycle: StopCapture -> StartCapture reopen is the
        // heavyweight hitch users felt as periodic chop, and each cycle re-toasted
        // "CONNECT OPTIC". 30s means a real stall (unplug, session drop, driver hang).
        _noFrameT += Time.unscaledDeltaTime;
        if (_noFrameT >= 30f)
        {
            CyberLog.Warn("EYE", "no Eye frames for 30s -> dropping capture to re-arm");
            StopCaptureSafe();
            _capturing = false; _gotFrame = false; _noFrameT = 0f;
            // no HUD toast here: the re-arm usually succeeds silently; TryStart toasts on failure
            return;
        }

        _fpsT += Time.unscaledDeltaTime;
        if (_fpsT >= 2f) { CyberLog.Info("EYE", $"raw new-frames={_newFrames / _fpsT:F1}/s res={_res.x}x{_res.y}"); _newFrames = 0; _fpsT = 0f; }
    }

    // The XREAL session releases the Eye when the app backgrounds. Release our capture on pause (StartCapture
    // otherwise latches _capturing=true forever, so the feed would never recover after a background/resume),
    // then let Update's retry loop re-arm on resume.
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            if (_capturing || _gotFrame)
            {
                StopCaptureSafe();
                _capturing = false; _gotFrame = false;
                CyberLog.Info("EYE", "paused -> capture released");
            }
        }
        else
        {
            _retryT = 2f; _noFrameT = 0f;   // force the retry loop to re-arm capture on the next Update
            CyberLog.Info("EYE", "resumed -> re-arming capture");
        }
    }

    void OnDestroy()
    {
        if (_cam != null) _cam.OnRGBCameraUpdate -= OnFrame;
        StopCaptureSafe();
        if (_rgbRT != null) _rgbRT.Release();
    }
}
