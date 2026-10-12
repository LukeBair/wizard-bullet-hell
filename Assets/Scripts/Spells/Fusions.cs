using System.Collections.Generic;

public enum Fusion { ThermalShock, Overload, Shatter, Conduit, Wildfire, Bramble }

// Cross-element bonuses. Only the Elements upgrade system unlocks these: raise two elements to
// level 2 and their fusion turns on. Spells check Fusions.Has() when they hit.
public static class Fusions
{
    public class Def
    {
        public Fusion Id;
        public string Name;
        public Element A, B;
        public string Effect;
    }

    public static readonly Def[] All =
    {
        new Def { Id = Fusion.ThermalShock, Name = "Thermal Shock", A = Element.Fire, B = Element.Frost, Effect = "Fire deals double damage to chilled foes" },
        new Def { Id = Fusion.Overload, Name = "Overload", A = Element.Fire, B = Element.Storm, Effect = "Lightning sets foes on fire" },
        new Def { Id = Fusion.Shatter, Name = "Shatter", A = Element.Frost, B = Element.Arcane, Effect = "Arcane hits chill foes" },
        new Def { Id = Fusion.Conduit, Name = "Conduit", A = Element.Storm, B = Element.Arcane, Effect = "Arcane hits can arc to a nearby foe" },
        new Def { Id = Fusion.Wildfire, Name = "Wildfire", A = Element.Nature, B = Element.Fire, Effect = "Wisps set foes on fire" },
        new Def { Id = Fusion.Bramble, Name = "Bramble", A = Element.Nature, B = Element.Frost, Effect = "Frost Nova leaves thorns that bleed" },
    };

    static readonly HashSet<Fusion> active = new HashSet<Fusion>();

    public static bool Has(Fusion f) => active.Contains(f);
    public static void Unlock(Fusion f) => active.Add(f);
    public static void Reset() => active.Clear();
    public static IEnumerable<Fusion> Active => active;
}
