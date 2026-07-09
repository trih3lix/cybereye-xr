using System.Collections;
using UnityEngine;

// M1 boot + M9 disclaimer: logs environment/rig state to logcat (CYBEREYE[BOOT]), shows a prominent
// FICTIONAL/ENTERTAINMENT disclaimer for a few seconds, then hands the HUD its post-boot cinematic
// (HudController.BeginPostBoot): the banner types in, holds ~2s, then glitches + fades out of the
// center of view (R2 field report: the boot text used to block the user's vision permanently).
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

        // FICTIONAL disclaimer (ethics + store requirement) — owns the center undisturbed;
        // HudController routes any other SetStatus into the event feed until boot ends.
        if (hud) hud.SetBootText("FICTIONAL // ENTERTAINMENT", "ALL DOSSIER DATA IS RANDOMLY GENERATED - NOT REAL");
        CyberLog.Info("BOOT", $"disclaimer shown ({disclaimerSeconds}s)");
        yield return new WaitForSeconds(disclaimerSeconds);

        if (hud) hud.BeginPostBoot();   // NIGHT CITY OS types in, SYSTEMS NOMINAL, then the center clears
        CyberLog.Info("BOOT", "boot complete");
    }
}
