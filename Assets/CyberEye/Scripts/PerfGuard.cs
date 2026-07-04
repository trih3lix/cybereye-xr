using UnityEngine;

// M8: performance/thermal guard. Watches FPS and throttles the detector's inference cadence when the frame
// rate sags (protects render smoothness + reduces heat); relaxes it again when there's headroom. Logs FPS +
// (best-effort) Android thermal headroom.
public class PerfGuard : MonoBehaviour
{
    [SerializeField] Detector detector;
    [SerializeField] int minInterval = 6;    // fastest detection cadence (frames)
    [SerializeField] int maxInterval = 24;    // most throttled

    float _t, _fps = 60f, _thermalT;
    int _frames;

    void Update()
    {
        _frames++; _t += Time.unscaledDeltaTime;
        if (_t >= 1f)
        {
            _fps = _frames / _t; _frames = 0; _t = 0f;
            if (detector != null)
            {
                if (_fps < 40f && detector.inferenceInterval < maxInterval)
                { detector.inferenceInterval += 2; CyberLog.Warn("PERF", $"fps={_fps:F0} -> throttle detect interval={detector.inferenceInterval}"); }
                else if (_fps > 55f && detector.inferenceInterval > minInterval)
                { detector.inferenceInterval -= 1; }
            }
        }
        _thermalT += Time.unscaledDeltaTime;
        if (_thermalT >= 5f) { _thermalT = 0f; LogThermal(); }
    }

    void LogThermal()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var act = up.GetStatic<AndroidJavaObject>("currentActivity");
            using var pm = act.Call<AndroidJavaObject>("getSystemService", "power");
            float headroom = pm.Call<float>("getThermalHeadroom", 10);   // API 30+; NaN if unavailable
            CyberLog.Info("PERF", $"fps={_fps:F0} thermalHeadroom={headroom:F2} detectInterval={(detector ? detector.inferenceInterval : -1)}");
        }
        catch { CyberLog.Info("PERF", $"fps={_fps:F0} detectInterval={(detector ? detector.inferenceInterval : -1)}"); }
#else
        CyberLog.Info("PERF", $"fps={_fps:F0}");
#endif
    }
}
