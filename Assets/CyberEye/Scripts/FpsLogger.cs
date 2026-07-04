using UnityEngine;

// Periodic FPS logger. Emits a CYBEREYE[FPS] line every `interval` seconds so the loop can assert
// the app is rendering and track the performance budget (target: >= display refresh) headlessly.
public class FpsLogger : MonoBehaviour
{
    [SerializeField] float interval = 2f;
    float _elapsed;
    int _frames;

    void Update()
    {
        _frames++;
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= interval)
        {
            float fps = _frames / _elapsed;
            CyberLog.Info("FPS", $"{fps:F1} fps ({_frames} frames / {_elapsed:F2}s)");
            _frames = 0;
            _elapsed = 0f;
        }
    }
}
