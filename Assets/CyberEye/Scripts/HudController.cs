using UnityEngine;
using UnityEngine.UI;

// M1 HUD: the cyberpunk boot banner + a status line on the world-space canvas.
// Uses legacy uGUI Text (built-in font, no TMP-essentials import) for a reliable first render.
public class HudController : MonoBehaviour
{
    [SerializeField] Text title;
    [SerializeField] Text status;

    void Awake()
    {
        if (title) title.text = "NIGHT CITY OS";
        SetStatus("BOOTING…");
    }

    public void SetStatus(string s)
    {
        if (status) status.text = "> " + s;
        CyberLog.Info("HUD", s);
    }

    public void SetTitle(string s)
    {
        if (title) title.text = s;
    }
}
