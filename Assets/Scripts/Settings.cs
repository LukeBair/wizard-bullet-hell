using UnityEngine;

// Player options from the Settings screen, saved on the device. Defaults live under "settings" in
// tuning.json; once the player changes one, their choice is kept.
public static class Settings
{
    const string VolumeKey = "set_volume", HapticsKey = "set_haptics", ShakeKey = "set_shake", NumbersKey = "set_numbers";

    const string WizardKey = "set_wizard";
    static readonly string[] WizardSheets = { "player_sheet", "player_sheet_64", "player_sheet_128" };
    static readonly string[] WizardLabels = { "Original", "64px 3D", "128px 3D" };

    // Which character art to use (Dusk art): 0 = the original sprites, 1 and 2 = the 3D-rendered 64px
    // and 128px sheets for the wizard and enemies.
    public static int Wizard
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(WizardKey, 0), 0, WizardSheets.Length - 1);
        set { PlayerPrefs.SetInt(WizardKey, value % WizardSheets.Length); PlayerPrefs.Save(); }
    }

    public static string WizardSheet => WizardSheets[Enemy3DSharp && Wizard != 0 ? 2 : Wizard];
    public static string WizardLabel => WizardLabels[Wizard];

    // The Character art setting covers enemies too: Original keeps their old sprites, and the 3D options
    // use the 3D-rendered eight-facing enemy sheets at 64px or 128px frames.
    public static bool Enemy3D => Wizard != 0;
    public static bool Enemy3DSharp => Wizard == 2 || (Wizard != 0 && PixelDensity.On);   // the pixel size tuner resamples from the 128px renders

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
