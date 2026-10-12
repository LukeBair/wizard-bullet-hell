# Wizard Bullet Hell

A Unity prototype of a top-down wizard survival bullet hell: roam a forest clearing, spells cast on their own, and level-ups make them stronger. Three upgrade systems are built side by side so they can be compared.

## Open and play

1. Install Unity Hub and a Unity 6 editor (any 6000.x is fine) with the **Android Build Support** and/or **iOS Build Support** modules.
2. In Unity Hub: **Add > Add project from disk** and pick this `wizard-bullet-hell` folder. If Hub asks about the editor version, pick the Unity 6 version you have.
3. The first open takes a few minutes. A setup script then creates `Assets/Scenes/Main.unity` and opens it.
4. In the **Game** view, set the aspect dropdown to a portrait size (e.g. 1080x1920).
5. Press **Play**, pick an upgrade system on the title screen, then drag anywhere (or use WASD) to move.

Everything is built from code at Play time (`GameBootstrap.cs`), so the scene stays empty apart from the camera.

## On a phone

- **Android:** File > Build Profiles (or Build Settings) > Android > Switch Platform, plug in the phone with USB debugging on, then **Build And Run**.
- **iOS:** Switch to iOS, **Build**, open the generated Xcode project on a Mac, set your signing team, and run on the device.

## Upgrade systems (pick one on the title screen)

All three use the same five spells (Arcane Missile, Fireball, Frost Nova, Chain Lightning, Forest Wisps) and the same eight runes (Power, Haste, Split, Pierce, Area, Echo, Ember, Frost), so only the way you choose differs. Best survival time is saved per system.

| System | How it plays |
| --- | --- |
| **Spell Draft** | Genre baseline. Pick 1 of 3 cards: a new spell, one rune on a spell you own, or a wizard perk. |
| **Rune Sockets** | Take a rune stone, then tap which spell to socket it into; each spell previews what that rune would do to it. 4 sockets per spell, and three matching runes in one spell Resonate for a free extra copy. Spell tomes appear every other level. |
| **Element Fusion** | Only pick an element. Each element owns one spell with a fixed upgrade path, and any two elements at level 2 unlock a fusion (Fire + Frost = Thermal Shock, Storm + Arcane = Conduit, ...). |

## Enemies (Whispering Forest)

| Tier | Enemy | Behaviour |
| --- | --- | --- |
| Chaff | Moss Slime | Slow melee swarm |
| Chaff | Dusk Bat | Fast, weaving melee; arrives in swarms every 45s |
| Medium | Thornback Wolf | Telegraphed lunge |
| Medium | Goblin Hexer | Keeps distance, fires aimed fans |
| Medium | Rogue Apprentice | Rival wizard: blinks next to you and fires rings |
| Medium | Mother Slime | Slow blob that splits into three slimes when killed |
| Medium | Spore Lobber* | Lobs spore bombs: a warning circle fills, then it bursts |
| Medium | Pixie Trickster* | Teleports aside when your spells get close; fires weaving shots |
| Medium | Root Burrower* | Tunnels underground (can't be hit), then erupts under you with a bullet ring |
| Boss | Elder Treant | Leaf rings, summons slimes, aimed volleys |
| Boss | The Hedge Witch | Spiral barrages, aimed fans, blinks |

\* Only after the first swarm. Each swarm (every 45s) raises the Threat level: more chaff, mediums in packs, bigger swarms with mediums mixed in. Enemy health steps up +25% per swarm and +100% per boss killed (bosses scale at half rate). A boss arrives every 2:30, alternating. New settings (wizarding school, caves) are a new `Biome` plus an `EnemyRoster`.

## How it works

| Script | Role |
| --- | --- |
| `GameBootstrap` | Creates the `GameManager` when Play starts |
| `GameManager` | Title screen, camera follow, level-up flow, game over, HUD |
| `Player` | Free movement, health, XP and levels, owns the spell list |
| `Joystick` | Floating touch joystick (WASD in the editor) |
| `World/Arena`, `World/Biome` | Forest clearing with tree obstacles; per-setting colours and roster |
| `Enemies/Enemy`, `EnemyRoster`, `Director` | Enemy behaviours, the forest roster, and time-based spawning |
| `Spells/Spell`, `Spells`, `Fusions` | Spell base class with rune modifiers, the five spells, element fusions |
| `Upgrades/*` | The three upgrade systems and a small OnGUI card kit |
| `Projectile`, `XpGem`, `Fx`, `SpriteFactory` | Bullets and spells, XP drops, effects, placeholder shapes |

For soak testing in the editor, set `GameManager.Autoplay = true` (the wizard kites on its own and upgrades are picked at random), or press **L** during a run for an instant level-up.
