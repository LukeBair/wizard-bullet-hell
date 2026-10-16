# Spell effects pipeline

Spells don't go through the 3D pipeline. They're light, fire, smoke and energy with no solid surface,
so TRELLIS has nothing to model. Instead each element gets a few painted images that the game animates
in code. Arcane Missile is the first spell done this way.

## 1. Make the images

One image per piece, **on pure black** (or already cut out), full effect visible, centred, no hands,
characters, ground or text. Ask for a bright core fading to soft edges. Per element:

| Piece | File | What it is |
|---|---|---|
| Projectile (1–4 variants) | `vfx_<element>_bolt_<n>.png` | The thing in flight. **Head on the left**, trail to the right. |
| Ring | `vfx_<element>_ring.png` | The impact: a round burst that spins outward and fades. Must be complete, not cut off at the edges. |
| Burst (optional) | `vfx_<element>_burst.png` | A quick flash under the ring. Only if the image is a complete burst. |

Prompt template (end every prompt with this):

> single glowing magic effect, centered, isolated on a pure black background, no character, no hands,
> no ground, no text, bright saturated core fading to soft glowing edges, dark fantasy game VFX,
> painterly, square 1:1

Element names in code: `arcane, fire, frost, storm, nature, water, wind, light, shadow, earth`.

## 2. Prepare them for the game

Shrink them to the game's pixel density, so they're chunky like the characters, not smooth HD:

- **About 27 px per world unit.** A bolt about 1.3 units long is about 36 px; a ring about 2 units is about 56 px.
- **24-colour palette, no dithering.**
- **Stepped alpha** (0 / 128 / 255), so the edges stay soft but pixel-crisp.
- If the image is on black rather than cut out, alpha = brightest channel and colour = colour ÷ alpha
  (un-premultiply), so the glow fades out instead of turning grey.

The snippet that did this for arcane is in the commit "Arcane effects scaled down to the game's pixel
density"; it reads straight from `~/Downloads/segments-*` and writes to `Assets/Resources/Art/Dusk/`.

## 3. Wire the spell up

`Assets/Scripts/Art/SpellVfx.cs` does the animating:

- `SpellVfx.Skin(projectile, Element, length)`: swaps the plain orb for a random `bolt` variant that
  flies head first along its velocity and flickers slightly (`Projectile.SetSkin`).
- `SpellVfx.Impact(position, Element, size)`: plays the burst (if present) and the ring, which scales
  up, spins and fades.
- Add the element to `SpellVfx.Key` (it maps `Element` to the file prefix; only `arcane` exists so far).
- In the spell's code, call `Skin` right after `Shoot(...)` and `Impact` in `OnHit`. See `ArcaneMissile`
  in `Assets/Scripts/Spells/Spells.cs`: `Skin(p, Element, 1.3f * Area)` and `Impact(pos, Element, 0.7f * Area)`.

Effects only show with the 3D Character art settings; with Original the spells keep their orbs.

## What else makes them look right

- **Spell light:** every player projectile already carries a 2D point light in its colour, so it tints
  the ground, enemies and props it passes (`lighting.spellRadius`, `lighting.spellIntensity`).
- **Bloom** picks up the bright cores (Resources/Render/PostFX).
- **Depth sorting:** player spells sort with characters and props, so they pass behind trees.
  Enemy bullets always draw on top.
- Effects are unlit sprites (they glow on their own) at sorting order 86, above characters.

## Not every spell is a projectile

Beams, auras, orbiting wisps and ground effects (Frost Nova, Overgrowth, Cyclone...) will need their own
hook: usually a looping sprite on the effect's object instead of `Skin`, and the same `Impact` for
bursts. Decide per spell when its art comes in.
