# Wizard Bullet Hell

A tiny Unity prototype: drag to move the wizard, spells fire automatically, survive waves of imps and their bullet patterns.

## Open and play

1. Install Unity Hub and a Unity 6 editor (any 6000.x is fine) with the **Android Build Support** and/or **iOS Build Support** modules.
2. In Unity Hub: **Add > Add project from disk** and pick this `wizard-bullet-hell` folder. If Hub asks about the editor version, pick the Unity 6 version you have.
3. The first open takes a few minutes. A setup script then creates `Assets/Scenes/Main.unity` and opens it.
4. In the **Game** view, set the aspect dropdown to a portrait size (e.g. 1080x1920).
5. Press **Play**. Click and drag with the mouse to move.

Everything is built from code at Play time (`GameBootstrap.cs`), so the scene stays empty apart from the camera.

## On a phone

- **Android:** File > Build Profiles (or Build Settings) > Android > Switch Platform, plug in the phone with USB debugging on, then **Build And Run**.
- **iOS:** Switch to iOS, **Build**, open the generated Xcode project on a Mac, set your signing team, and run on the device.

## How it works

| Script | Role |
| --- | --- |
| `GameBootstrap` | Creates the `GameManager` when Play starts |
| `GameManager` | Camera, score, best score, game over and restart, HUD |
| `Player` | Relative touch/mouse drag movement, auto-fire, 5 HP with i-frames, tiny hitbox (white dot) |
| `Enemy` | Imps that fly in, hover, and fire Fan, Aimed, Ring or Spiral patterns |
| `WaveSpawner` | Next wave when the screen is clear; new patterns unlock by wave 4; boss every 5th wave |
| `Projectile` | Spells and enemy bullets, circle-overlap collision (no physics engine) |
| `SpriteFactory` | Generates placeholder circle, glow and triangle sprites |

Tuning numbers (fire rate, bullet speed, health, wave sizes) are constants at the top of each script.
