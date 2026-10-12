// Global feel knobs, editable in the tuner (global.*).
public static class Tuning
{
    // Multiplies every movement speed: the wizard, enemies, knockback and all projectiles.
    public static float Pace => Tune.F("global.pace", 0.75f);

    // Multiplies every spell cooldown and enemy attack interval (lower = casts more often).
    public static float CastRate => Tune.F("global.castRate", 0.85f);
}
