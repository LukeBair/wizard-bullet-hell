# Writes Assets/Resources/tuning.json with every tunable at its current in-code default.
# Run once to (re)create the file; after that, edit values with the tuner page instead.
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "tuning.json")

def enemy(health, speed, radius, contact, xp, **extra):
    d = {"Health": health, "Speed": speed, "Radius": radius, "ContactDamage": contact, "Xp": xp}
    d.update(extra)
    return d

caster = lambda fi, bullets, spread, bspeed, bdmg, rng: {"FireInterval": fi, "Bullets": bullets, "Spread": spread, "BulletSpeed": bspeed, "BulletDamage": bdmg, "PreferredRange": rng}

tuning = {
    "_about": "Every tunable in Wizard Bullet Hell. Keys starting with _ are notes. Spell values for evolved tiers use _t2 / _t3 suffixes.",
    "global": {
        "pace": 0.75, "castRate": 0.85,
        "_pace": "Multiplies all movement (wizard, enemies, projectiles).",
        "_castRate": "Multiplies every spell cooldown and enemy attack interval.",
    },
    "art": {"pxPerTexel": 2, "refHeight": 1080,
        "_pxPerTexel": "On-screen size of one art texel, in screen pixels, for the 3D-rendered wizard, enemies, props and ground (0 = off). Takes effect on the next run.",
        "_refHeight": "Screen height (px) the sizing assumes. 0 = the actual screen height (phones are ~2600px, so much finer)."},
    "vfx": {"lightningHitScale": 3, "fireballHitScale": 1, "frostHitScale": 1, "arcaneHitScale": 1, "natureHitScale": 1, "spikeHeight": 3, "waterHitScale": 1, "waterBeamWidth": 2.2, "waterBeamGrow": 0.25, "waterBeamFps": 30, "bladeSize": 3, "bladesHitScale": 1, "flameSize": 3, "sunSize": 2.7, "banishSize": 1, "rejuvSize": 3, "overgrowthSize": 1.15, "overgrowthTime": 0.8, "vineTrapSize": 1.6, "veilSize": 2.2, "cycloneSize": 1, "enthrallSize": 1.2, "mirrorSize": 2.5, "heartsSize": 1.2, "heartsFps": 24, "veilFps": 18, "veilAlpha": 0.55, "veilTime": 0.6,
        "_about": "Painted spell effects: multiplies each element's hit burst size (pixel density is kept). spikeHeight: painted stone spike height in spike radii. waterBeamWidth: painted Tidal Lance jet height in beam widths; waterBeamGrow: seconds for it to grow to full length; waterBeamFps: how fast the jet flows. bladeSize: painted spectral blade frame size in blade radii. flameSize: painted Comet Trail flame frame size in patch radii. sunSize: painted Sunspots frame size in pool radii. banishSize: multiplies the painted Banish vortex. rejuvSize/veilSize: painted Rejuvenate burst and Veil size in world units. overgrowthSize, cycloneSize: painted vines and whirlwind size in their radii; overgrowthTime: how long the big vine burst lasts; vineTrapSize: the vines round each trapped foe, in foe sizes. enthrallSize: charm sigil in foe sizes; heartsSize: charmed hearts column width in foe sizes, heartsFps its speed. mirrorSize: painted mirror crystal in shard thicknesses (crystals tile along the shard). veilFps/veilAlpha: Veil speed and opacity; veilTime: longest the Veil shows (seconds)."},
    "audio": {"volume": 1.0},
    "hud": {
        "fontScale": 1.2,
        "_fontScale": "Text size multiplier for the pixel font (Dusk and CRT); VT323 draws small for its point size.",
        "effectCounters": {"enabled": True, "size": 0.035, "offset": 0.6, "life": 0.9, "rise": 0.6,
            "_about": "The small x### tallies over combos and bug bursts. size = text scale (damage numbers are 0.05), offset = units above the trigger spot, life in seconds, rise in units/s."},
    },
    "fx": {
        "enabled": True, "maxParticles": 1500, "drag": 4, "highlightShare": 0.25,
        "_about": "Particle sparks, Dusk art style only. count = particles per effect, speed in units/s, size in world units (Dusk rounds to whole pixels), life in seconds.",
        "burst": {"perUnit": 2.5, "min": 2, "max": 24, "speed": 1.4, "size": 0.12, "life": 0.4},
        "ring": {"perUnit": 5, "max": 32, "speed": 3, "size": 0.1, "life": 0.35},
        "hit": {"count": 3, "maxPerFrame": 12, "cone": 60, "speed": 5, "size": 0.09, "life": 0.2},
        "bolt": {"count": 4, "speed": 5, "size": 0.08, "life": 0.18},
        "trail": {"rate": 30, "speed": 0.6, "size": 0.9, "life": 0.25},
        "death": {"count": 7, "mediumMultiplier": 1.8, "bossMultiplier": 6, "bossMotes": 40, "speed": 4, "size": 0.14, "life": 0.55},
        "pickup": {"count": 4, "speed": 2.5, "size": 0.08, "life": 0.3},
        "levelUp": {"count": 36, "speed": 5, "size": 0.14, "life": 0.9},
        "evolve": {"count": 60, "speed": 7, "size": 0.16, "life": 0.9},
        "combo": {"count": 18, "speed": 6, "size": 0.13, "life": 0.55},
        "bossShot": {"count": 2, "speed": 3, "size": 0.12, "life": 0.25},
        "slam": {"count": 30, "speed": 8, "size": 0.18, "life": 0.6},
        "status": {"burnRate": 8, "chillRate": 4, "wetRate": 3, "antRate": 10, "thornRate": 5, "size": 0.08, "life": 0.6},
        "ants": {"count": 24, "speed": 2.5, "size": 0.05, "life": 0.6},
        "crackle": {"perUnit": 10, "speed": 4, "size": 0.08, "life": 0.2},
        "frost": {"perUnit": 8, "speed": 3.5, "size": 0.1, "life": 0.45, "snowLife": 0.9},
        "heal": {"perHp": 1, "min": 6, "max": 30, "speed": 2.2, "size": 0.1, "life": 0.8},
    },
    "player": {"MaxHealth": 100, "MoveSpeed": 4.6, "PickupRadius": 2.2, "invulnTime": 0.6},
    "xp": {
        "base": 3, "perLevel": 2, "quadDivisor": 5, "stepEvery": 5, "stepMultiplier": 1.25,
        "_formula": "floor(base + perLevel*L + L^2/quadDivisor) * stepMultiplier^(L/stepEvery)",
    },
    "drops": {
        "xpMultiplier": 1.0,
        "extra": [
            {"name": "Lucky gem", "tier": "Any", "chance": 0.0, "xp": 10, "heal": 0},
            {"name": "Medium heal", "tier": "Medium", "chance": 0.0, "xp": 0, "heal": 10},
        ],
        "_extra": "Bonus drops rolled on every kill. tier: Any, Chaff, Medium or Boss. Add as many as you like.",
    },
    "spawning": {
        "maxEnemies": 200, "bossEvery": 150, "swarmEvery": 45, "firstMediumAt": 40,
        "chaffBase": 0.4, "chaffPerSecond": round(1 / 240, 5), "chaffPerThreat": 0.1, "chaffMax": 10,
        "mediumEveryStage0": 14, "mediumEveryStage1": 11, "pairChanceStage1": 0.3, "mediumEveryStage2": 6, "mediumEveryMin": 2.5, "maxPack": 5,
        "swarmBasePreBoss": 14, "swarmPerThreatPreBoss": 3, "swarmBase": 16, "swarmPerThreat": 6,
        "casterWeightPreBoss": 0.25,
        "healthPerSwarm": 0.25, "healthPerBossKill": 1.0, "bossHealthScaleRate": 0.5, "bossKillHeal": 30,
        "_chaff": "Chaff per second in a zone = (chaffBase + seconds in zone*chaffPerSecond) * (1 + chaffPerThreat*swarms in zone) + run.zoneRateStep*zones cleared, capped at chaffMax.",
    },
    "run": {
        "bosses": 4, "startZone": "", "startTome": "", "startTomeChoices": 3, "zoneShiftDelay": 3, "favoredWeight": 3,
        "_bosses": "Bosses per run. Each one (spawning.bossEvery apart) moves the run to a new zone; killing the last one wins. 0 = endless.",
        "_startZone": "Force the first zone of a run (testing). Empty = random.",
        "_startTome": "Force a starting tome to be offered (testing). Empty = random.",
        "_startTomeChoices": "How many starting tomes are offered at the start of a run.",
        "_zoneShiftDelay": "Seconds between a boss dying and the zone changing (time to grab the loot).",
        "_favoredWeight": "How much more often a zone's home medium enemies show up.",
        "zoneRampSeconds": 40, "zoneRampFactor": 0.75, "zoneFirstMedium": 8, "zoneRateStep": 2,
        "_zoneRateStep": "Extra chaff per second for each zone already cleared; every zone otherwise reuses the first zone's curve from its own start.",
        "_zoneRampSeconds": "A new zone starts empty; spawning ramps from 0 to full over this many seconds (second zone), times zoneRampFactor for each zone after that. zoneFirstMedium is the delay before the first medium enemy, scaled the same way.",
    },
    "meta": {
        "tomeCost": 100,
        "evolveCostT2": 150,
        "evolveCostT3": 300,
        "featBossesT2": 2,
        "goldPerKill": 0.1,
        "goldPerMedium": 1,
        "goldPerBoss": 25,
        "goldPerMinute": 5,
        "goldWin": 50,
        "_about": "Gold and the Grimoire. Gold per run = kills + bosses + minutes survived (+ goldWin on a win), kept even if you die. Arcane Missile, Fireball and Frost Nova tomes are free; the rest cost tomeCost. Evolution II needs its feat (beat featBossesT2 bosses with the spell in your build), III needs a win with the spell at tier II+.",
    },
    "telemetry": {
        "enabled": True, "enemyEvents": True, "snapshotEvery": 10, "flushEvery": 30,
        "_about": "Run logs sent to the tuner (Runs page; files in tools/tuner/telemetry). enabled = record runs at all. enemyEvents = one line per enemy spawn and kill (the summary always has per-enemy totals). snapshotEvery = seconds of play between tick lines (health, level, enemies alive). flushEvery = real seconds between uploads during a run.",
    },
    "runes": {
        "startSockets": 2, "socketsPerBoss": 1, "evolveAt": 4, "maxSpells": 5, "tier3Free": 1, "tier2Bosses": 0, "tier3Bosses": 1, "tomeEveryLevels": 2, "resonanceCount": 3,
        "evolveRetain": 0.75,
        "_tier3Free": "Most spells you can carry: maxSpells, less one for each tier III spell past tier3Free (5 spells with one tier III, 4 with two, 3 with three). Tier III is only offered when your spells fit.",
        "_tier3Bosses": "Boss kills needed before any spell can evolve to tier II (tier2Bosses) or tier III (tier3Bosses).",
        "_startSockets": "Rune sockets per spell at the start of a run; each boss kill adds socketsPerBoss. A full spell with at least evolveAt runes can evolve.",
        "_evolveRetain": "Share of a spell's rune power kept when it evolves (0-1). Fractions of Split/Pierce/Echo become a chance per cast.",
        "Potency": {"weight": 10},
        "Haste": {"weight": 10, "cooldownMultiplier": 0.85},
        "Split": {"weight": 2.5},
        "Pierce": {"weight": 10},
        "Area": {"weight": 10},
        "Echo": {"weight": 2.5},
        "Ember": {"weight": 10, "burnPerRune": 0.4, "duration": 2.5},
        "Frost": {"weight": 10, "chillBase": 1.0, "chillPerRune": 0.6},
        "Curse": {"weight": 10, "bonusPerRune": 0.5, "maxBonus": 5},
        "Infest": {"weight": 10, "burstPerRune": 0.5, "duration": 1.0, "burstRadius": 1.4},
        "Bless": {"weight": 10, "chancePerRune": 0.15, "maxChance": 0.8, "gemMultiplier": 4, "minGem": 20},
        "_weight": "Relative chance to be offered. Under 5 shows as RARE.",
    },
    "status": {
        "chillSlow": 0.5, "frostbiteMaxSlow": 0.6,
        "frostbiteTakenChaff": 1.0, "frostbiteTakenMedium": 0.55, "frostbiteTakenBoss": 0.2,
        "shatter": 0.3, "shatterBoss": 0.1, "rootBossFactor": 0.25,
    },
    "combos": {
        "conductiveMultiplier": 1.5, "steamBaseDamage": 10, "steamHealthFraction": 0.08, "steamMaxDamage": 60, "firebugsRadius": 2.0,
    },
    "spells": {
        "ArcaneMissile": {"damage": 12, "damage_t2": 12, "damage_t3": 14.5, "cooldown": 0.85, "count": 1, "speed": 11, "lifetime": 2,
                           "homing": 0, "homing_t2": 180, "homing_t3": 420, "followUpRange": 0.6, "potencyPerRune": 0.3, "areaPerRune": 0.2},
        "Fireball": {"damage": 20, "damage_t2": 30, "damage_t3": 30, "cooldown": 2.1, "count": 1, "radius": 1.0, "radius_t2": 1.8, "radius_t3": 1.8,
                      "speed": 7.5, "speed_t2": 6, "speed_t3": 6, "burnFactor": 0.25, "burnDuration": 2, "burnFactor_t2": 0.3, "burnDuration_t2": 2.5,
                      "burnFactor_t3": 0.3, "burnDuration_t3": 2.5, "shrapnelCount_t3": 6, "shrapnelLifetime_t3": 0.6, "shrapnelRadiusScale_t3": 0.5, "potencyPerRune": 0.25, "areaPerRune": 0.2},
        "FrostNova": {"damage": 10, "cooldown": 2.6, "count": 1, "radius": 2.0, "radius_t2": 2.4, "radius_t3": 2.8, "chill_t2": 1.8, "chill_t3": 1.8,
                       "push": 4, "pushPerPierce": 3, "frostbite_t3": 0.35, "frostbitePerFrostRune_t3": 0.25, "potencyPerRune": 0.2, "areaPerRune": 0.15},
        "ChainLightning": {"damage": 18, "cooldown": 1.5, "count": 1, "splash": 1.0, "splash_t2": 1.0, "splashDamage": 0.5, "splashDamage_t2": 0.5,
                            "jumps_t2": 2, "jumps_t3": 2, "jumpRange": 3.5, "chainFalloff": 0.9,
                            "strikeMultiplier_t3": 3, "strikeSplash_t3": 1.6, "strikeSplashDamage_t3": 1.2, "potencyPerRune": 0.3, "areaPerRune": 0.2},
        "Wisps": {"damage": 9, "cooldown": 1, "count": 2, "orbit": 1.7, "ringSpacing": 0.55, "wispSize": 0.28, "bodyScale": 1, "spinSpeed": 200, "hitEvery": 0.5,
                   "quietSeconds_t2": 3, "quietHeal_t2": 0.1, "hitHeal_t3": 0.01, "potencyPerRune": 0.3, "areaPerRune": 0.15},
        "Rejuvenate": {"damage": 0.06, "cooldown": 9, "count": 1, "maxHealthPerPierce": 10, "_damage": "Fraction of max health healed per pulse."},
        "Overgrowth": {"damage": 6, "cooldown": 5, "count": 1, "radius": 1.8, "rootTime": 2.2, "potencyPerRune": 0.25},
        "Veil": {"damage": 1.5, "cooldown": 12, "potencyPerRune": 0.25, "phasePerPotency": 0.05, "_damage": "Seconds of invulnerability."},
        "TidalLance": {"damage": 5, "cooldown": 5.5, "count": 1, "duration": 1.2, "length": 6.5, "width": 0.55, "_damage": "Per tick (about 8 ticks a second)."},
        "Cyclone": {"damage": 4, "cooldown": 6, "count": 1, "radius": 3, "duration": 2.5, "pull": 9, "distance": 4.5, "_damage": "Per tick (4 a second)."},
        "Enthrall": {"damage": 3, "cooldown": 9, "count": 1, "_damage": "Charm duration in seconds."},
    },
    "enemies": {
        "Moss Slime": enemy(20, 1.7, 0.35, 8, 1),
        "Dusk Bat": enemy(14, 3.1, 0.28, 6, 1),
        "Thornback Wolf": enemy(100, 2.4, 0.5, 15, 5),
        "Goblin Hexer": enemy(70, 1.8, 0.42, 8, 5, **caster(2.2, 3, 14, 4.5, 10, 6)),
        "Rogue Apprentice": enemy(85, 1.6, 0.42, 8, 7, **caster(3, 12, 20, 3.4, 10, 5)),
        "Spore Lobber": enemy(80, 1.3, 0.45, 8, 6, EarlyGame=False, **caster(2.8, 3, 20, 4.5, 18, 7)),
        "Pixie Trickster": enemy(41, 2.6, 0.3, 6, 6, EarlyGame=False, **caster(1.8, 3, 20, 4, 10, 5)),
        "Root Burrower": enemy(110, 3.6, 0.5, 15, 7, EarlyGame=False, Bullets=10, BulletSpeed=3.5, BulletDamage=20),
        "Mother Slime": enemy(90, 1.2, 0.65, 12, 4),
        "Elder Treant": enemy(2400, 1.1, 1.3, 25, 60, FireInterval=2.6, Bullets=18, BulletSpeed=3.2, BulletDamage=10),
        "The Hedge Witch": enemy(3200, 1.8, 0.9, 20, 80, **caster(0.12, 3, 20, 3.6, 10, 6)),
        "Headmaster Vexley": enemy(2800, 1.4, 0.9, 20, 70, **caster(2.4, 36, 20, 2.6, 10, 6.5)),
        "Crystal Golem": enemy(3600, 0.9, 1.4, 30, 80, FireInterval=3, Bullets=20, BulletSpeed=3, BulletDamage=14),
    },
}

# Evolution values for the utility spells.
EVOLUTIONS = {
    "Rejuvenate": {
        "damage_t2": 0.05,
        "damage_t3": 0.05,
        "cooldown_t2": 3,
        "cooldown_t3": 3,
        "reviveChargeTime_t3": 90,
        
        "_reviveChargeTime_t3": "Tier III: seconds to charge the revive (Haste runes shorten it)."
    },
    "Overgrowth": {
        "damage_t2": 6,
        "damage_t3": 6,
        "radius_t2": 1.8,
        "radius_t3": 1.8,
        "rootTime_t2": 2.2,
        "rootTime_t3": 2.2,
        "thornDps_t2": 8,
        "thornDps_t3": 8,
        "_thornDps_t2": "Damage per second to trapped foes; foes bumping a trapped foe take this much per bump."
    },
    "Veil": {
        "damage_t2": 3.5,
        "damage_t3": 3.5,
        "cooldown_t2": 12,
        "cooldown_t3": 4,
        "passDamage_t2": 60,
        "passDamage_t3": 60
    },
    "TidalLance": {
        "damage_t2": 5,
        "damage_t3": 5,
        "duration_t2": 1.2,
        "duration_t3": 1.2,
        "length_t2": 6.5,
        "length_t3": 6.5,
        "push": 5,
        "push_t2": 12,
        "push_t3": 12,
        "coneAngle_t2": 35,
        "coneAngle_t3": 35,
        "pillarEvery_t3": 5,
        "pillarDuration_t3": 2,
        "pillarRadius_t3": 1.3,
        "pillarDamage_t3": 1.5,
        "_coneAngle_t2": "Half-angle of the spray cone in degrees."
    },
    "Cyclone": {
        "duration_t2": 2.5,
        "duration_t3": 2.5,
        "travelSpeed_t2": 2.5,
        "travelSpeed_t3": 2.5,
        "tickEvery": 0.25,
        "tickEvery_t3": 0.125
    },
    "Enthrall": {
        "damage_t2": 3,
        "burstRadius_t2": 2.5,
        "burstRadius_t3": 2.5,
        "maxThralls_t3": 12,
        "_maxThralls_t3": "Tier III: most permanently charmed foes at once."
    }
}
for spell, values in EVOLUTIONS.items():
    tuning["spells"][spell].update(values)
tuning["status"]["thornBumpFactor"] = 1.0

with open(OUT, "w") as f:
    json.dump(tuning, f, indent=2)
    f.write("\n")
print("wrote", OUT)

# Keep a copy of the defaults so the tuner can show what changed and reset values.
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "tuning.defaults.json"), "w") as f:
    json.dump(tuning, f, indent=2)
    f.write("\n")
