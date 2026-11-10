# Sound system plan

Goal: make the busy parts of a run sound good (not just loud), keep the no-assets synth working, and let
real recorded files replace any sound without code changes.

## Built in this pass (`Assets/Scripts/Sfx.cs`)
| Piece | What it does | Tuner keys |
|---|---|---|
| Voice pool | 24 pooled `AudioSource`s (max 32), `Play()` instead of `PlayOneShot`, so voices can be stolen; no per-play allocation | `audio.voices` |
| Per-sound cap + cooldown | Each sound has a gap and max simultaneous voices; low-priority extras are dropped, others steal their own oldest | `audio.sound.<Sound>.gap/.max` |
| Dropped-play boost | Plays blocked by the cooldown make the next play up to ~50% louder, so a mass hit still feels big | built in |
| Priority tiers | Low (hits, deaths, gems, status), Normal (casts), High (hurt, level-up, boss, UI). A full pool steals the weakest voice only if the new sound is at least as important | `.priority` |
| Pitch and volume variation | Per-call pitch jitter (call-site) plus per-sound ±8-15% volume jitter | built in |
| Round-robin | 2-3 clip variants for the frequent sounds, never repeating back-to-back | built in |
| Buses | Software SFX and UI buses (and Music has its own) under `audio.volume` and the Settings slider; same behaviour as AudioMixer groups, no editor asset needed | `audio.bus.sfx/.ui` |
| Headroom and crowd gain | Master 0.8; gain falls as sqrt(6/active) beyond 6 simultaneous voices so 20 sounds sum like ~10 | `audio.headroom` |
| Ducking | High-priority sounds drop quiet sounds for 0.35 s, and long ones (roar, death, level-up, victory) also dip the music | `audio.duck.*` |
| Kill chains | Death volume falls with kill rate, plus one deep `KillBurst` thump when 7+ die within about 0.6 s | `audio.kill.*` |
| Gem chain | Gem pickups rise in pitch while they keep coming (up to 12 steps) | `audio.gemChain.*` |
| Screen position | Enemy hit/death take the enemy position: pan and distance fade; far off-screen quiet sounds skipped | `audio.spatial`, `audio.pan` |
| Startup cost | Synthesis spread across frames (3 ms budget per frame); sounds not yet built are skipped for a few frames | `audio.buildBudgetMs` |
| Imported audio | `Resources/Sfx/<SoundName>.wav` (or `_1`, `_2` variants) replaces the synth for that sound | n/a |
| Lifecycle | Stops voices on audio configuration change and on resume; `AudioListener.pause` on app pause | n/a |
| New sounds | KillBurst, Heal, Magnet, Lunge, Split, per-type deaths (slime, bat, beast), BossHit | `audio.sfx.*`, `audio.sound.*` |

Call-site hooks (small): enemy hit/death (position, boss hit, per-type death), wolf lunge, slime split,
heal, gem magnet, and `Music` multiplying its volume by `Sfx.MusicDuck`.

## Not built yet (ordered by value)
1. **Per-spell identity**: distinct casts for Overgrowth, Cyclone, Stone Spikes, Veil, Enthrall, Banish, Spectral Blades, Sunspots; tier II/III shimmer layers. Needs ears, so left to Luke and the spells session.
2. **Boss attack and telegraph sounds** for Treant, Witch, Headmaster (touches Enemy.cs boss code owned by another session).
3. **Element-specific hit sounds** (fire, frost, lightning, nature) chosen by damage type, via an optional `Sound` argument in `TakeDamage`.
4. **UI split**: back, error and card-pick sounds distinct from tap.
5. **Real AudioMixer**: add Master/SFX/UI/Music groups plus a Limiter and low-pass snapshot for pause; the bus code maps one to one.
6. **Real recorded or CC0 assets** (see RESEARCH.md): start with player hurt, level-up, boss roar, UI taps, explosions.
7. **Settings split** into separate Music and SFX sliders (needs UI).
8. Burn tick, chill expiry, curse, burrower emerge, pause/resume and run-start stings.

## Risks and checks
- Everything is by-ear tuned numbers; defaults are conservative. Compare with the previous build if it feels quieter (headroom 0.8 and crowd gain lower the peak).
- The synth builds over about 40+ frames after launch; sounds before that are skipped (the title Select tap is built first).
- Memory: about 90 clips, roughly 3 MB PCM; no per-frame allocations in the play path (the old path built a dictionary lookup and PlayOneShot per call).
- Confirm on device: ringer switch behaviour, phone-call and background resume, headphone unplug.
