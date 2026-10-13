# Run telemetry

Every run the game plays is logged here by the tuner server (`tools/tuner/server.js`), one file per run.
The files are not in git. Switches are under `telemetry` in the tuner.

- `runs.json`: one summary row per run, newest first (outcome, duration, level, bosses, kills, damage taken, killer, spells). Start here.
- `<run>.jsonl`: the full log for one run. Run ids are `YYYYMMDD-HHMMSS-xxxx` (UTC start time).

## Line format

JSON Lines: one JSON object per line, in order. Every line has:

- `t`: seconds of play since the run started (level-up screens and pauses don't count)
- `ev`: the event type

The first line is `run_start`. A finished run ends with `death`, `win` or `quit`, then `summary`. A run without `summary` was cut off (app closed) or is still going.

| ev | Fields | When |
| --- | --- | --- |
| `run_start` | `run`, `date` (UTC ISO), `device`, `platform`, `version`, `tuning` (where tuning came from), `style` (art style), `bosses` (bosses per run), `zones` (biome names in order), `tomesOwned`, `evolutionsOwned` (e.g. `"Fireball 2"`), `gold`, `maxHealth` | Run starts |
| `zone` | `zone` (1-based), `name` | Run start and each zone change |
| `pick` | `level`, `took` (`start_tome`, `rune`, `tome`, `evolve`, `rest`), `offered` (`{runes, tome, evolve}`, or `{tomes}` for the start), plus `rune`, `spell`, `sockets` (runes in that spell after), `resonance`, `tier` as they apply | A level-up or start choice is made |
| `levelup` | `level` (the new level) | XP reaches the next level (the pick for it may come later) |
| `evolve` | `spell`, `tier` (new tier), `name` | A spell evolves |
| `spawn` | `id`, `enemy`, `tier` (`Chaff`, `Medium`), `cause` (`trickle`, `medium`, `pack`, `swarm`, `summon` by a boss, `split` from a Mother Slime), `hp` | An enemy appears (only with `telemetry.enemyEvents`) |
| `kill` | `id`, `enemy`, `life` (seconds alive) | An enemy dies (only with `telemetry.enemyEvents`) |
| `swarm` | `threat`, `count` | A swarm arrives |
| `boss_spawn` | `id`, `enemy`, `hp` | A boss arrives |
| `boss_kill` | `id`, `enemy`, `fight` (seconds), `damageTaken`, `hitsTaken` (by the wizard during the fight), `hp` (wizard's, after) | A boss dies |
| `hurt` | `amount`, `src` (enemy name), `kind` (`contact`, `bullet`, `hazard`), `hp` (after the hit) | The wizard takes damage |
| `blocked` | `by` (`veil`, `ward`), `src` | A hit was absorbed |
| `tick` | `hp`, `maxHp`, `level`, `xp`, `xpToNext`, `chaff`, `medium` (alive), `bullets` (enemy bullets in flight), `gems` (on the ground), `threat`, `healthScale`, `dealt` (damage dealt since the last tick), `healed` (HP healed since the last tick), `fps` (average since the last tick), `fpsMin` (the slowest frame, as fps) | Every `telemetry.snapshotEvery` seconds |
| `death` | `killer`, `level`, `boss` (if a boss was up), `alive` (enemies) | The wizard dies |
| `win` | `level` | The last boss dies |
| `quit` | `level`, `hp` | The run is ended from the pause menu (Quit or Restart) |
| `summary` | `run`, `outcome` (`death`, `win`, `quit`), `duration`, `level`, `bossesDefeated`, `threat`, `healthScale`, `kills`, `score`, `gold` (paid out), `killer`, `spells` (`[{id, name, tier, level, runes: {Rune: count}}]`; rune counts include power kept through evolutions), `enemies` (`{name: {tier, spawned, killed, despawned, avgLife, hitsOnPlayer, damageToPlayer}}`), `damageTaken` (`{total, hits, blocked, bySource, byKind}`), `picks` (`{runes: {Rune: n}, tomes, evolves, rests}`), `aliveAtEnd` (`{name: count}`), `damageDealt` (`{total, bySource, killsBySource}`; sources are spell ids plus Burn, Thorns, Steam, Shatter, Firebugs, Hex Swarm, Bug Burst, Enthrall), `healing` (`{total, bySource}`; spell ids plus Siphon, Boss kill, Drop, Rest, Revive), `xp` (`{dropped, collected, levelTimes}`: play time each level was reached, from level 2), `combos` (`{name: times}`), `fps` (`{avg, worstTick}`) | Last line |

Enemy `id`s tie `spawn`, `kill`, `boss_spawn` and `boss_kill` lines together. Enemies cleared by a zone change count as `despawned` in the summary and have no `kill` line.

## Reading it

```bash
jq -s 'map(select(.ev=="summary"))' tools/tuner/telemetry/*.jsonl      # every finished run's summary
jq -c 'select(.ev=="hurt")' tools/tuner/telemetry/<run>.jsonl           # every hit in one run
```
