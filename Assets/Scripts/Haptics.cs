using System.Runtime.InteropServices;
using UnityEngine;

// Phone haptics. Uses the iOS Taptic Engine through Plugins/iOS/Haptics.mm; falls back to the plain
// vibrate on Android (heavy events only) and does nothing in the editor.
public static class Haptics
{
    public enum Impact { Light, Medium, Heavy }
    public enum Notify { Success, Warning, Error }

    static float lastTime;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _HapticImpact(int style);
    [DllImport("__Internal")] static extern void _HapticNotify(int type);
    [DllImport("__Internal")] static extern void _HapticSelection();
#endif

    // Don't buzz constantly: light taps are throttled, stronger ones always go through.
    static bool Throttle(float gap)
    {
        float now = Time.unscaledTime;
        if (now - lastTime < gap) return true;
        lastTime = now;
        return false;
    }

    public static void Tap(Impact style)
    {
        if (Throttle(style == Impact.Light ? 0.08f : 0.02f)) return;
#if UNITY_IOS && !UNITY_EDITOR
        _HapticImpact((int)style);
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (style == Impact.Heavy) Handheld.Vibrate();
#endif
    }

    public static void Notification(Notify type)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _HapticNotify((int)type);
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    public static void Selection()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _HapticSelection();
#endif
    }
}
