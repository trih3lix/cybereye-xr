using UnityEngine;

// M8 (stub): HUD FX intensity control. The One Pro's only app-reachable buttons are volume up/down
// (Android keycodes 24/25), but Unity's KeyCode has no VolumeUp/VolumeDown and capturing them needs a
// native onKeyDown override (see beam-pro notes) — deferred. For now FX intensity stays at full; Step()
// is exposed so a native key bridge or a settings UI can drive it later without a rebuild of this API.
public class SettingsController : MonoBehaviour
{
    [SerializeField] HudOverlayController overlay;
    [SerializeField] HudController hud;

    float _intensity = 1f;

    public void Step(float d)
    {
        _intensity = Mathf.Clamp01(_intensity + d);
        if (overlay) overlay.SetIntensity(_intensity);
        if (hud) hud.SetStatus($"FX {(_intensity * 100f):F0}%");
        CyberLog.Info("SET", $"HUD intensity={_intensity:F2}");
    }
}
