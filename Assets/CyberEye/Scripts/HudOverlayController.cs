using UnityEngine;

// Option A: the fullscreen camera-parented quad becomes the neon HUD grade (CyberEye/HudOverlay) drawn over
// the transparent optical view (black = transparent on the additive display). Sizes it to fill the FOV.
public class HudOverlayController : MonoBehaviour
{
    [SerializeField] Renderer overlayQuad;
    [SerializeField] float distance = 6f;
    int _resizes;
    float _t;
    Material _mat;

    public void SetIntensity(float v) { if (_mat != null) _mat.SetFloat("_Intensity", Mathf.Clamp01(v)); }

    void Start()
    {
        if (overlayQuad != null)
        {
            var sh = Shader.Find("CyberEye/HudOverlay");
            if (sh == null) CyberLog.Err("HUD", "CyberEye/HudOverlay shader missing (stripped?)");
            _mat = new Material(sh);
            overlayQuad.material = _mat;
        }
        SizeToFov();
        CyberLog.Info("HUD", "neon overlay init");
    }

    void Update()
    {
        if (_resizes < 6) { _t += Time.unscaledDeltaTime; if (_t >= 0.5f) { _t = 0f; _resizes++; SizeToFov(); } }
    }

    void SizeToFov()
    {
        var c = Camera.main;
        if (c == null || overlayQuad == null) return;
        float h = 2f * distance * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
        float w = h * Mathf.Max(c.aspect, 1f);
        var t = overlayQuad.transform;
        t.localPosition = new Vector3(0f, 0f, distance);
        t.localRotation = Quaternion.Euler(0f, 180f, 0f);
        t.localScale = new Vector3(w, h, 1f);
    }
}
