# Wizard Bullet Hell

A Unity prototype of a top-down wizard survival bullet hell: roam a forest, academy or cave, spells cast on their own, and level-ups socket runes into them.

## Open and play

1. Install Unity Hub and a Unity 6 editor (any 6000.x is fine) with the **Android Build Support** and/or **iOS Build Support** modules.
2. In Unity Hub: **Add > Add project from disk** and pick this `wizard-bullet-hell` folder. If Hub asks about the editor version, pick the Unity 6 version you have.
3. The first open takes a few minutes. A setup script then creates `Assets/Scenes/Main.unity` and opens it.
4. In the **Game** view, set the aspect dropdown to a portrait size (e.g. 1080x1920).
5. Press **Play**, tap Play on the title screen, then drag anywhere (or use WASD) to move.

Everything is built from code at Play time (`GameBootstrap.cs`), so the scene stays empty apart from the camera.

## On a phone

- **Android:** File > Build Profiles (or Build Settings) > Android > Switch Platform, plug in the phone with USB debugging on, then **Build And Run**.
- **iOS:** Switch to iOS, **Build**, open the generated Xcode project on a Mac, set your signing team, and run on the device.

## Rune Sockets

Each level-up you take a rune stone (Split and Echo are rare) and tap the spell to socket it into; each spell previews what that rune would do to it. Every spell has 4 sockets, and three matching runes in one spell Resonate for a free extra copy. Spell tomes appear every other level (up to 5 spells). XP needed per level grows, with an extra x1.25 every 5 levels.

**Runes:** Potency (more effective, varies by spell), Haste, Split, Pierce, Area, Echo, Ember (burn), Frost (chill), Curse (marked foes take +50% from their next hit, stacking to 6x), Infest (foes that die within 1s burst into bugs that hurt neighbours), Bless (kills may drop a big XP gem).

**Spells:** Arcane Missile, Fireball, Frost Nova, Chain Lightning, Forest Wisps, Rejuvenate (periodic heal), Overgrowth (vines trap foes; trapped foes can't hurt you by touch), Ethereal Veil (ignore the next hit and phase out), Tidal Lance (water beam that soaks foes), Cyclone (whirlwind that drags foes in), Enthrall (the toughest non-boss foe fights for you).

**Evolution:** when one of the first five spells has all 4 sockets full, an Evolve card can appear. Evolving clears that spell's runes for a stronger form, up to tier III (e.g. Lightning: single bolt with splash -> chains -> heavy strike on the toughest foe, then chains).

**Combos:** burning + chilled = Steam Burst; wet + lightning = Conductive (+50%); wet + frost = Deep Freeze; infested + burning = Firebugs; infested + cursed = Hex Swarm.

## Enemies

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
| Boss | The Hedge Witch | Spiral barrages, aimed fans and homing orbs, blinks |
| Boss | Headmaster Vexley | Orbiting spellbooks fire volleys, 3x3 "detention" grids with one safe cell, slow rings with a gap |
| Boss | Crystal Golem | Long telegraphed charges that leave crystal shards hanging, ground slams, marching spike lines |

\* Only after the first swarm. Pressure builds in stages: before the first swarm (45s) it's a light trickle with a rare basic medium; after it, mediums appear sometimes; after each boss kill they come often and in packs. Swarms grow each time. Enemy health steps up +25% per swarm and +100% per boss killed (bosses scale at half rate). A boss arrives every 2:30; nothing else spawns until it dies. Each run picks a random setting (Whispering Forest, Arcane Academy, Crystal Caves), which decides the first boss. New settings (wizarding school, caves) are a new `Biome` plus an `EnemyRoster`.

## Tuning editor

Every number in the game (spell damage and cooldowns, rune rarity and effects, enemy stats, XP curve, drops, spawn pacing) lives in `Assets/Resources/tuning.json`. Edit it with the tuner page:

```bash
node tools/tuner/server.js
```

then open http://localhost:8787 (or double-click `tools/tuner/start.command`). Change values, press Save, and start a new run: the game (editor, or the phone on the same Wi-Fi) loads the newest values from the Mac on the title screen and after each death, so no rebuild is needed. The file is also bundled into every build. Changed values are highlighted with a reset button; you can add values and add rows to lists like `drops.extra` (bonus drops per kill). `tools/tuner/tuning.defaults.json` holds the original values.

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
| `Upgrades/*` | Rune Sockets level-up flow and a small OnGUI card kit |
| `Tune`, `Tuning` | Reads tuning.json (live from the Mac tuner when reachable) |
| `Projectile`, `XpGem`, `Fx`, `SpriteFactory` | Bullets and spells, XP drops, effects, placeholder shapes |

For soak testing in the editor, set `GameManager.Autoplay = true` (the wizard kites on its own and upgrades are picked at random), or press **L** during a run for an instant level-up.
