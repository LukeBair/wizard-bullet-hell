using UnityEngine;

// Player options from the Settings screen, saved on the device. Defaults live under "settings" in
// tuning.json; once the player changes one, their choice is kept.
public static class Settings
{
    const string VolumeKey = "set_volume", HapticsKey = "set_haptics", ShakeKey = "set_shake", NumbersKey = "set_numbers";

    const string WizardKey = "set_wizard";
    static readonly string[] WizardSheets = { "player_sheet", "player_sheet_64", "player_sheet_128" };
    static readonly string[] WizardLabels = { "Original", "64px 3D", "128px 3D" };

    // Which wizard sprite sheet to use (Dusk art): 0 = the original 48px sheet, 1 and 2 = the
    // 3D-rendered 64px and 128px sheets. Read when a run starts.
    public static int Wizard
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(WizardKey, 0), 0, WizardSheets.Length - 1);
        set { PlayerPrefs.SetInt(WizardKey, value % WizardSheets.Length); PlayerPrefs.Save(); }
    }

    public static string WizardSheet => WizardSheets[Wizard];
    public static string WizardLabel => WizardLabels[Wizard];

    const string EnemyArtKey = "set_enemy3d";

    // Enemies drawn with the 3D-rendered eight-facing sheets (enemy3d_<name>) where one exists, or the
    // original sprites. Applies to enemies spawned after it changes.
    public static bool Enemy3D
    {
        get => PlayerPrefs.GetInt(EnemyArtKey, 1) == 1;
        set => Set(EnemyArtKey, value);
    }

    const string BossSharpKey = "set_boss3d128";

    // Big 3D enemies (Treant, Witch, Headmaster) at 128px frames, or the lighter 64px copies.
    public static bool Boss3DSharp
    {
        get => PlayerPrefs.GetInt(BossSharpKey, 1) == 1;
        set => Set(BossSharpKey, value);
    }

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
