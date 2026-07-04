using UnityEditor;
using UnityEngine;

// Sets the CyberEye neon-eye icon as the app icon. Called from BuildScript every build (idempotent).
// Uses the robust default+Android legacy icon path (no Android-extension dependency); best-effort adaptive.
public static class IconSetup
{
    const string Dir = "Assets/CyberEye/Icon/";

    public static void Apply()
    {
        var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "cybereye_icon.png");
        if (full == null) { Debug.LogWarning("[CYBEREYE-ICON] cybereye_icon.png not found; skipping"); return; }
#pragma warning disable 618
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { full }); // default (fallback for all)
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { full }); // Android legacy
#pragma warning restore 618
        Debug.Log("[CYBEREYE-ICON] applied neon-eye icon (default + Android)");
    }
}
