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

    public static void Reset() => seen.Clear();

    public static void Trigger(Interaction i, Vector2 at)
    {
        if (seen.Add(i)) GameManager.Instance.Toast($"Combo: {Name(i)}!");
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
