using UnityEngine;

// Player options from the Settings screen, saved on the device. Defaults live under "settings" in
// tuning.json; once the player changes one, their choice is kept.
public static class Settings
{
    const string VolumeKey = "set_volume", HapticsKey = "set_haptics", ShakeKey = "set_shake", NumbersKey = "set_numbers";

    // Sound volume in steps of settings.volumeStep, 0 = muted.
    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, Tune.F("settings.volume", 1f));
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static bool Haptics
    {
        get => Get(HapticsKey, "settings.haptics");
        set => Set(HapticsKey, value);
    }

    public static bool ScreenShake
    {
        get => Get(ShakeKey, "settings.screenShake");
        set => Set(ShakeKey, value);
    }

    public static bool DamageNumbers
    {
        get => Get(NumbersKey, "settings.damageNumbers");
        set => Set(NumbersKey, value);
    }

    static bool Get(string key, string tunePath) =>
        PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) == 1 : Tune.B(tunePath, true);

    static void Set(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    // One volume step up (+1) or down (-1), in steps of settings.volumeStep.
    public static void StepVolume(int dir)
    {
        float step = Mathf.Max(0.05f, Tune.F("settings.volumeStep", 0.25f));
        Volume = Mathf.Clamp01(Mathf.Round(Volume / step + dir) * step);
    }
}
