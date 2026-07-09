using UnityEngine;

// M7: cyberpunk audio — looping ambiance bed + reactive SFX: target-lock chirp on a new primary target,
// alert when the tracked count rises, periodic scan blip while locked. 2D (non-spatial) mix.
public class AudioDirector : MonoBehaviour
{
    [SerializeField] TargetOverlay overlay;
    [SerializeField] AudioClip ambiance, lockSfx, scanSfx, glitchSfx, alertSfx;
    [Range(0f, 1f)] public float ambianceVol = 0.35f, sfxVol = 0.7f;

    AudioSource _amb, _sfx;
    int _lastPrimary = -1, _lastCount = 0;
    float _scanT;

    void Start()
    {
        _amb = gameObject.AddComponent<AudioSource>();
        _amb.clip = ambiance; _amb.loop = true; _amb.spatialBlend = 0f; _amb.volume = ambianceVol; _amb.playOnAwake = false;
        if (ambiance) _amb.Play();
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.spatialBlend = 0f; _sfx.playOnAwake = false;
        CyberLog.Info("AUDIO", ambiance ? "director init (ambiance playing)" : "director init (no ambiance clip)");
    }

    void Update()
    {
        if (overlay == null) return;
        int p = overlay.PrimaryId, c = overlay.TrackCount;
        if (p >= 0 && p != _lastPrimary) { Play(lockSfx, "lock"); _lastPrimary = p; }
        if (c > _lastCount) Play(alertSfx, "alert");
        _lastCount = c;
        _scanT += Time.deltaTime;
        if (_scanT > 4f && c > 0) { _scanT = 0f; Play(scanSfx, "scan"); }
    }

    // R2: HUD boot-cinematic hooks (scanline sweep / glitch exit). Null-safe like every Play path.
    public void PlayScanSfx()   => Play(scanSfx, "scan(hud)");
    public void PlayGlitchSfx() => Play(glitchSfx, "glitch(hud)");

    void Play(AudioClip clip, string tag)
    {
        if (clip == null || _sfx == null) return;
        _sfx.PlayOneShot(clip, sfxVol);
        CyberLog.Info("AUDIO", "sfx " + tag);
    }
}
