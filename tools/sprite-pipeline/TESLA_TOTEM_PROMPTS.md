# Tesla Totem art prompts

For the Tesla Totem spell (Storm element, `TeslaTotem` in `Assets/Scripts/Spells/NewSpells.cs`). It plants a
stationary tower that zaps nearby foes. Same workflow as the other painted spell effects in `SPELLS.md`:
generate on pure black, key out by brightness, shrink to the game's pixel density. The spell is currently
code-drawn (a diamond plus a glow, and `SpellVfx.Spark` for the beam), so these replace those.

## Look

Dusk pixel art, after 20 Minutes Till Dawn. Strict 7-colour palette (the game snaps to it, so prompt for these
hues and expect the cleanup pass to finish the job):

| Colour | Hex | Use here |
|---|---|---|
| Ground | `#1d1a2b` | Deepest shadow, outlines, the black background |
| Navy | `#2b2f4a` | Tower body shadow side |
| Sage | `#6f9b7a` | Moss and weathering on the stone |
| Deep sage | `#3e5a4e` | Stone mid-tone |
| Cream | `#f0d2bc` | Highlights, the electric core |
| Coral | `#e5555f` | Used sparingly: heated runes at tier III |
| Wine | `#8c3446` | Not used (boss robes only) |

Storm lightning in code is yellow-white (`Element.Storm`). For the painted version, make lightning **cream
(`#f0d2bc`) cores with sage-teal edges**, so it stays inside the palette. Tint to storm yellow in code if wanted.

Size: a tower is about 0.5 / 0.75 / 1.0 world units wide at tier I / II / III (the code scales the body 1x / 1.5x / 2x
of 0.5), so about 14 px / 20 px / 27 px wide at 27 px per unit. Draw them a little taller than wide, standing
upright, seen from a slightly raised top-down angle like the characters.

## Prompt ending (use on every prompt)

> single glowing magic effect, centered, isolated on a pure black background, no character, no hands,
> no ground, no text, bright saturated core fading to soft glowing edges, dark fantasy game VFX,
> painterly, square 1:1

For the towers, add "chunky pixel art, limited palette" in front of it, and tell the generator **no ground and no
shadow on the floor** (the game draws its own shadow).

## 1. Tower body

One image each, tower centred, full height visible.

**Tier I: Tesla Totem.** A small, crude standing stone totem about waist high, weathered dark teal-green stone with
moss, a single carved spiral rune on the front glowing cream, a tiny blue-white spark floating above its tip.
Chunky pixel art, limited palette of dark navy, deep green, sage green and pale cream, upright, three-quarter top-down.

**Tier II: Tesla Coil.** The same totem, taller and broader, now with a pale-cream metal ring wrapped round
it and two short antennae curling from the top, a brighter glowing rune with small cream sparks jumping between the
antennae, faint cream light spilling onto the stone. Chunky pixel art, limited palette of dark navy, deep green,
sage green and pale cream, upright, three-quarter top-down.

**Tier III: Storm Spire.** A tall stone spire with three stacked glowing rune bands, crackling cream-white lightning
arcing between four floating stone shards that orbit its tip, a bright overcharged core at the top, one coral-red
heated rune near the base to hint it will detonate. Chunky pixel art, limited palette of dark navy, deep green,
sage green, pale cream and a touch of coral red, upright, three-quarter top-down, imposing.

## 2. Idle glow (optional loop)

The rune pulsing. Ask for a 6s video, 1:1, then cut a loop (same as the rune effects).

> The [tier] stone totem standing still on pure black, its carved rune slowly pulsing brighter and dimmer in cream
> light, tiny sparks drifting up, camera locked, no movement of the totem itself, seamless loop feel, chunky pixel
> art, limited dark palette, isolated on a pure black background.

## 3. Lightning arc / zap beam

Used by `SpellVfx.Spark(from, to, colour)`: a short bolt between a tower and a foe. One image or video each.

**Single arc (tier I).** A single jagged bolt of lightning going left to right across the frame, thin bright cream-white
core with a pale teal-green glow, three small forks, small bright start and end points, horizontal, centred.
Isolated on pure black, chunky pixel art, limited palette, no background.

**Multi-arc (tier II, hits 3 foes).** Three jagged lightning bolts leaving one point on the left and fanning out to
the right, each ending in a small bright flash, cream-white cores with teal-green glow, isolated on pure black,
chunky pixel art, limited palette.

**Thunderclap (tier III zap).** A vertical bolt of cream-white lightning striking down onto a round patch of ground,
a wide ring of crackling electricity spreading outward from the impact, small stone-dust particles, isolated on pure
black, chunky pixel art, limited palette, top-down, symmetrical.

**Detonation (tier III expiry).** A tower bursting apart in a ring of cream-white lightning, stone shards flying
outward in all directions, a bright flash at the centre fading to teal-green sparks, isolated on pure black, chunky
pixel art, limited palette, top-down, complete circular burst with room at the edges.

## 4. Planting and crumbling (optional)

**Plant.** Short ring of cream light rising from the ground as a small stone totem pushes up through it, dust motes,
isolated on pure black, chunky pixel art, limited palette.

**Crumble.** A weathered stone totem cracking and falling into pieces as its glow fades to nothing, small dust cloud,
isolated on pure black, chunky pixel art, limited palette.

## File names for wiring

Follow the existing convention (`vfx_<element>_...` in `Assets/Resources/Art/Dusk/`):

| File | Piece |
|---|---|
| `vfx_totem_1.png`, `vfx_totem_2.png`, `vfx_totem_3.png` | Tower body per tier |
| `vfx_totem_idle_<n>.png` | Optional pulse loop frames |
| `vfx_storm_arc_<n>.png` | Single-arc variants (head on the left, like the bolts) |
| `vfx_storm_clap_<n>.png` | Thunderclap frames |
| `vfx_totem_boom_<n>.png` | Tier III detonation frames |

Nothing loads these yet. `TeslaTotem.Plant` in `NewSpells.cs` currently builds the tower from a diamond and a glow, so
wiring means swapping in `vfx_totem_<tier>` there and adding a cutter script modelled on
`blades_from_video.py`.
