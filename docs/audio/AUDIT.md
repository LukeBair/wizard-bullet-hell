# Audio audit

Compared against `Assets/Scripts/Sfx.cs` and every `Sfx.Play(` call site at the time of the audit
(77 calls). Status: **fine** = distinct and appropriate, **placeholder** = plays but reuses a
generic sound or is crude, **missing** = nothing plays. Rate: how often it can fire in a busy run
(**H**igh = many per second, **M**edium = about once a second, **L**ow = once per run or per few minutes).

## Sound inventory today
34 synthesised sounds (`Sound` enum), one clip each, 20 `PlayOneShot` voices round robin (no
stealing, no priority), per-sound minimum gap, ±6% pitch jitter, per-call-site volume in
`audio.sfx.<name>`. No SFX/Music/UI buses, no ducking, no volume variation, no per-sound voice cap.

## Spells (cast sounds)
| Event | Plays now | Status | Rate |
|---|---|---|---|
| Arcane Missile cast (tiers I-III, extra missiles) | Missile | placeholder (same for every tier) | H |
| Fireball cast / explode | FireballCast / Explosion | placeholder | M / M |
| Frost Nova pulse | Nova | placeholder | M |
| Chain Lightning cast (+ each bounce) | Lightning on cast only | placeholder; bounces silent | M |
| Wisps orbit hit / dart / fire burst / storm burst | WispHit, Explosion, Lightning | placeholder | **H** (orbit hits) |
| Rejuvenate pulse / revive | Gem / LevelUp | placeholder (pulse reuses gem pickup, confusing) | M / L |
| Overgrowth cast, Cyclone cast, Stone Spikes cast | Burrow (all three) | placeholder, identical | M |
| Veil absorb, Enthrall cast, Banish cast, Spectral Blades cast | Teleport (all four) | placeholder, identical | M |
| Tidal Lance cast / pillar | FireballCast / Explosion | placeholder | M |
| Sunspots cast / burst | Gem / Explosion | placeholder (cast reuses gem) | M |
| Comet Trail drop | Comet | fine-ish | M |
| Mirror Ward reflect | WispHit | placeholder | M |
| Spell tier-up difference (II, III) | none | missing | L |
| Spell hit impact per element (fire, frost, lightning, arcane, nature) | one generic EnemyHit | missing | **H** |

## Status effects and combos
| Event | Plays now | Status | Rate |
|---|---|---|---|
| Burn / chill / wet / wither applied | Sizzle / Chill / Splash / Wither (first application only) | fine (short, rate limited) | M |
| Burn tick / chill expiry / curse | none | missing | M |
| Curse (rune/curse effect) | none | missing | L |
| Combo (steam, shatter, infest burst, generic combo) | Combo, Lightning, FireballCast, EnemyDie | placeholder | M |
| Rune effects: overload, static spark | Lightning, Spark | fine | M |

## Enemies
| Event | Plays now | Status | Rate |
|---|---|---|---|
| Any enemy hit | EnemyHit, one clip, 70 ms gap | placeholder (single clip, no variation, no position) | **H (highest)** |
| Chaff death (Moss Slime, Dusk Bat) | EnemyDie | placeholder (same for all types) | **H** |
| Medium death (Wolf, Hexer, Apprentice, Lobber, Pixie, Burrower, Mother Slime) | EnemyDie louder | placeholder (no per-type character) | M |
| Mass kills (kill chains) | each death separately, 50 ms gap | placeholder | **H** |
| Enemy projectile shoot (Hexer, Lobber, Pixie) | EnemyShoot | placeholder | M |
| Wolf lunge telegraph/lunge | none | missing | M |
| Apprentice blink (teleport) | Teleport | placeholder | M |
| Root Burrower burrow | Burrow | placeholder | M |
| Burrower emerge, Mother Slime split | none | missing | M |
| Enemy projectile hits player / hazard lands | Explosion (hazard only) | placeholder | M |
| Swarm event warning | Warning | placeholder | L |

## Bosses
| Event | Plays now | Status | Rate |
|---|---|---|---|
| Boss arrive | BossRoar | fine (one roar for all four bosses) | L |
| Boss death | BossDie | fine | L |
| Golem slam telegraph | Warning (the Golem only) | placeholder | L |
| Elder Treant, Hedge Witch, Headmaster Vexley attacks and telegraphs | none | **missing** | M |
| Boss hit | generic EnemyHit | placeholder (needs a heavier hit) | **H** |
| Boss phase change / enrage | none | missing | L |

## Player
| Event | Plays now | Status | Rate |
|---|---|---|---|
| Hurt | PlayerHurt | fine, but ungated at 30 ms: stacks on contact damage | M |
| Death | PlayerDie | fine | L |
| Low health heartbeat | LowHealth (2 s gap) | fine | M |
| Ward block / ward ready | WardBlock / WardReady | fine | M |
| Heal (drop heal, Rejuvenate) | none for drop heal | missing | M |
| Footsteps / movement | none | missing, probably not needed in a bullet hell | - |

## Pickups, progression, UI
| Event | Plays now | Status | Rate |
|---|---|---|---|
| XP gem pickup | Gem (40 ms gap, pitch jitter 0.15) | placeholder: constant pitch, no "chain" feel | **H** |
| Magnet pulls all gems | none | missing | L |
| Gold / coin | Coin | fine | M |
| Level up / fully built | LevelUp | fine (short arpeggio) | L |
| Upgrade card choose / rune pick / UI tap | Select (one sound for everything) | placeholder | M |
| UI back / close / disabled / error | Select (same) | placeholder | M |
| Purchase | Coin + LevelUp | fine | L |
| Rune socket / resonance, Evolve, Feat done | RuneSocket / Evolve / Feat | fine | L |
| Zone change | Warning | placeholder | L |
| Victory / Game over | Victory / PlayerDie (no separate game-over sting) | fine | L |
| Pause / resume, run start, loading | none | missing | L |
| Title screen / menu ambience | music only | fine | - |

## Biggest gaps (priority order)
1. **Voice management**, not a sound: hits, deaths, gems and orbit hits all hit hard at once; no stealing, priority, caps, ducking or kill-chain grouping.
2. **Hit sounds are one clip** with no element, round robin or position. This is the most frequent sound in the game.
3. **Gem pickups** don't chain pitch, which loses the satisfying cascade.
4. **Deaths** are identical for every enemy; no burst sound for mass kills.
5. **Spell identity**: 7 spells share Burrow/Teleport/Gem as their cast sound; tiers sound the same.
6. **Bosses**: three of four have no attack or telegraph sounds; no boss phase sounds.
7. **UI**: one sound for tap, back, error and card pick.
8. **Heal, magnet, burrower emerge, split, lunge** have nothing.
