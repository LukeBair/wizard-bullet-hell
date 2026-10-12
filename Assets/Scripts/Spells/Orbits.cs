using UnityEngine;

// A spell whose orbiters circle the wizard on shared rings.
public interface IOrbiter
{
    int OrbitRings { get; }
}

// All orbiting spells share one set of evenly spaced rings around the wizard, handed out
// in the order the spells were learned. Runes on a spell change only its own orbiters,
// never where its ring sits, so the rings stay evenly spaced.
public static class Orbits
{
    public static float Radius(Spell spell, int ring)
    {
        int slot = ring;
        if (spell.Owner != null)
            foreach (var s in spell.Owner.Spells)
            {
                if (s == spell) break;
                if (s is IOrbiter o) slot += o.OrbitRings;
            }
        return Tune.F("orbits.radius", 1.5f) + Tune.F("orbits.spacing", 0.85f) * slot;
    }

    // Echo adds another set of orbiters on the same ring, spinning the other way.
    // Sets alternate direction; sets going the same way are spread evenly between each other.
    public static float Angle(float spin, int ring, int set, int sets, int index, int count)
    {
        bool forward = (ring + set) % 2 == 0;
        int sameWay = forward ? (sets + 1) / 2 : sets / 2;
        float gap = 360f / Mathf.Max(1, count);
        float phase = index * gap + (set / 2) * gap / Mathf.Max(1, sameWay) + ring * 30f;
        return (forward ? spin : -spin) + phase;
    }
}
