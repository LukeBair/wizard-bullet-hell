using UnityEngine;

// Builds the whole game from code when Play starts, so no scene or prefab wiring is needed.
static class GameBootstrap
{
    // Static lists survive between plays when domain reload is off, so clear them first.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Projectile.All.Clear();
        Enemy.All.Clear();
        XpGem.All.Clear();
        Interactions.Reset();
        Joystick.Blocked = false;
        GameManager.Autoplay = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<GameManager>() != null) return;
        new GameObject("Game").AddComponent<GameManager>();
    }
}
