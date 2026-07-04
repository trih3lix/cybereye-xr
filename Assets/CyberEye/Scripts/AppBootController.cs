using System.Collections;
using UnityEngine;

// M1 boot + M9 disclaimer: logs environment/rig state to logcat (CYBEREYE[BOOT]), then shows a prominent
// FICTIONAL/ENTERTAINMENT disclaimer for a few seconds before handing the HUD to the live experience.
// The disclaimer is required: the app overlays randomly-generated "dossier" data on real people via camera.
public class AppBootController : MonoBehaviour
{
    [SerializeField] HudController hud;
    [SerializeField] float disclaimerSeconds = 6f;

    IEnumerator Start()
    {
        CyberLog.Info("BOOT", $"CyberEye v{Application.version} unity={Application.unityVersion}");
        CyberLog.Info("BOOT", $"device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} gfx={SystemInfo.graphicsDeviceType}");

        var cam = Camera.main;
        if (cam) CyberLog.Info("BOOT", $"MainCamera={cam.name} fov={cam.fieldOfView:F0} clear={cam.clearFlags}");
        else     CyberLog.Warn("BOOT", "MainCamera=NULL (XR rig not resolved yet)");

        // FICTIONAL disclaimer (ethics + store requirement).
        if (hud)
        {
            hud.SetTitle("FICTIONAL // ENTERTAINMENT");
            hud.SetStatus("ALL DOSSIER DATA IS RANDOMLY GENERATED - NOT REAL");
        }
        CyberLog.Info("BOOT", $"disclaimer shown ({disclaimerSeconds}s)");
        yield return new WaitForSeconds(disclaimerSeconds);

        if (hud)
        {
            hud.SetTitle("NIGHT CITY OS");
            hud.SetStatus("SYSTEMS NOMINAL");
        }
        CyberLog.Info("BOOT", "boot complete");
    }
}
