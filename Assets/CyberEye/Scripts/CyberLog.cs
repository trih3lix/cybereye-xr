using UnityEngine;

// Structured logging for the dev loop. Debug.Log ALWAYS runs first (tag "Unity", message contains
// "[CYBEREYE]") so verify.ps1 can grep it; the native android.util.Log (tag "CYBEREYE") is a best-effort
// extra, created lazily inside try/catch so a JNI hiccup can NEVER break callers (an earlier static-init
// AndroidJavaClass field threw at class load and silently killed every controller that logged).
public static class CyberLog
{
    public const string TAG = "CYBEREYE";

    public static void Info(string sub, string msg) { Debug.Log($"[{TAG}][{sub}] {msg}");        Native("i", sub, msg); }
    public static void Warn(string sub, string msg) { Debug.LogWarning($"[{TAG}][{sub}] {msg}"); Native("w", sub, msg); }
    public static void Err(string sub, string msg)  { Debug.LogError($"[{TAG}][{sub}] {msg}");   Native("e", sub, msg); }

    static void Native(string level, string sub, string msg)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { using (var log = new AndroidJavaClass("android.util.Log")) log.CallStatic<int>(level, TAG, $"[{sub}] {msg}"); }
        catch { /* best-effort only */ }
#endif
    }
}
