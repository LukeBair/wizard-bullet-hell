using System.Collections.Generic;
using UnityEngine;

public enum Interaction { Steam, Conductive, DeepFreeze, Firebugs, HexSwarm }

// Status effects that combine when they meet on the same enemy:
//   Steam       burning + chilled      -> scalding burst that hurts nearby foes
//   Conductive  wet + lightning        -> lightning deals +50%
//   Deep Freeze wet + frost            -> long chill and heavy frostbite
//   Firebugs    infested + burning     -> the bug burst is bigger and sets foes on fire
//   Hex Swarm   infested + cursed      -> the bug burst curses everything it hits
// The first time each one happens in a run, a toast names it so players learn the combos.
public static class Interactions
{
    static readonly HashSet<Interaction> seen = new HashSet<Interaction>();

    public static void Reset()
    {
        seen.Clear();
        EffectCounter.Reset();
    }

    public static void Trigger(Interaction i, Vector2 at)
    {
        if (seen.Add(i)) GameManager.Instance.Toast($"Combo: {Name(i)}!");
        Particles.Combo(i, at);
        Sfx.Play("combos.combo", Sound.Combo, 0.5f, 0.08f);
        EffectCounter.Count(Name(i), at, Tint(i));
    }

    // Text colour for the combo's x### counter.
    public static Color Tint(Interaction i)
    {
        switch (i)
        {
            case Interaction.Steam: return new Color(0.95f, 0.95f, 0.95f);
            case Interaction.Conductive: return new Color(1f, 0.95f, 0.4f);
            case Interaction.DeepFreeze: return new Color(0.7f, 0.95f, 1f);
            case Interaction.Firebugs: return new Color(1f, 0.6f, 0.2f);
            default: return new Color(0.8f, 0.45f, 1f);
        }
    }

    public static string Name(Interaction i)
    {
        switch (i)
        {
            case Interaction.Steam: return "Steam Burst";
            case Interaction.Conductive: return "Conductive";
            case Interaction.DeepFreeze: return "Deep Freeze";
            case Interaction.Firebugs: return "Firebugs";
            default: return "Hex Swarm";
        }
    }
}
