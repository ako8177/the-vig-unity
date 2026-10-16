<!-- Carried over from the TypeScript game (github.com/ako8177/the-vig, commit 292a1e1) on 2026-10-15. The sim and data in Assets/Vig/Sim implement it exactly (tools/golden). Where the Unity look differs from STYLE.md, ../DECISIONS.md (U2) says how. -->

# Style — The Vig

The art bible. Owned by the art director (`.claude/agents/art-director.md`); written 2026-10-12
after the Milestone 0 neon-and-palette test (DECISIONS #7, #21, #29). If a rule here is wrong,
propose the change and write it in DECISIONS.md; don't drift.

## The look in one line

A velvet trap under hot neon: a dark room of smoke-grey walls, violet carpet and orange-lacquered
wood, where the only honest light is the neon telling you what each table costs.

## Palette: TanoPal 48

[TanoPal 48](https://lospec.com/palette-list/tanopal-48), 48 colors, the whole game. Every texture
is painted in it, the HUD and the panels draw in it, and the pixel pass snaps the bloomed frame to
it (`src/render3d/pixelpass.ts`: a 64³ lookup table, 4×4 Bayer dither of ±1.8% on the 640×360 grid).
A new color is a proposal in DECISIONS.md, never a quiet addition.

**Roles, not hexes.** Code and build scripts name a role (`C.low`, `C.felt`, `C.brass`); the cast in
`src/data/palette.ts` says which TanoPal color plays it. To recolor, change the cast, not the art.

| Role | Hex | Used for |
|---|---|---|
| ink / night / smoke / ash / dust / bone / white | 1f1d1e 303232 645c5a 8c9687 96918c c8c8b5 ffffff | darks, UI frames, text |
| wall / wallHi | 303232 645c5a | the walls' flock paper |
| carpet / carpetLo | 410966 1f1d1e | the floor |
| felt / feltLo | 303232 1f1d1e | table felt: charcoal, never green (lit TanoPal felt-green snaps to the low tier's neon) |
| baize / baizeHi / baizeLo | 005a20 287d08 024637 | the dice trays on the Hi-Lo and Dead Man's panels only: a dark green the owner asked for, unlit in 2D (DECISIONS #60) |
| wood / woodHi / woodLo | 642808 c86900 511806 | rails, wainscot, cabinets |
| brass / brassLo / gold | f8d280 c86900 f8d280 | fittings, money in the HUD |
| soul / soulLo | 32fad2 0dc8d9 | ghosts and souls |
| marker / markerLo | 6e8ef0 5555e6 | markers, the cage's strip |
| blood | ac0a48 | lives lost, overloads, snake eyes, and the red card suits (DECISIONS #37) |
| cream / indigo / indigoHi | f7c3a0 1a0070 3c1e8e | THE FLOOR: damask walls, the medallion carpet (DECISIONS #65) |
| pink / pinkHi / teal | d7198c fa5adc 05826c | the Lounge's coves, the crowd's coats, slot chase lights; never on a panel's odds |

## The tiers own their colors

The tier colors are the game's most important pixels (pillar 5). Each tier is a base, a hot core
and a low shade; they are used everywhere the tier appears (neon rim, sign, panel badge, HUD,
the seat prompt) and **nothing else in the room may glow in a tier's hue**.

| Tier | Base | Hi (core) | Lo | Loss glyph |
|---|---|---|---|---|
| **low** | `00e60f` green | `c4ff91` | `08b428` | coin (loses money) |
| **mid** | `f0ff00` yellow | `ffff8e` | `f8a004` | target (you are marked) |
| **high** | `f5320a` red-orange | `f5916e` | `af000a` | skull (can kill) |
| **Collector** | `be5aff` violet | `e1b6ff` | `801cc0` | skull |

Mid is cast yellow, not TanoPal's amber `f8a004`: from 10 m that amber and the high tier's red both
read orange (the test's evidence: `e2e/out/palette/`). The tier glyph backs up the color for
colorblind players (and Milestone 8 adds colorblind glyph options).

## Light and glow

- **Lights**: one dim hemisphere (wallHi over carpetLo, 2.2) and at most 8 point lights: one warm
  lamp (`ffe2b8`) 2.95 m over each table, a violet one over the Collector, a warm one over the bar.
  No shadows; blob decals under tables and avatars when they arrive.
- **The ceiling takes no light** (it's black up there; a lit ceiling blooms haze over the signs).
- **Neon** is a tier's base color scaled until its brightest channel is exactly 1.0, never past it
  (past it the hue clips to white). It is a `MeshBasicMaterial`, and it is on the **GLOW layer**.
- **Bloom is selective**: only GLOW meshes feed it (neon strips, signs, the Collector's eyes, the
  cage strip); everything else is black in that pass so it still occludes. Strength 0.3, radius
  0.05. Never let a panel, a lit surface or the HUD bloom. Halos snap to TanoPal's greys: that smoke
  is part of the look; if a halo ever changes a tier's apparent hue, the halo is wrong.
- **Panels** are unlit, a hair under full (`0xeaeaea`), never on the GLOW layer.

## Pixels and density

- 640×360 internal, whole-number upscale, letterboxed. A fractional scale only when the best whole
  number would waste the window (under 85% of it) or the player picks FILL WINDOW (DECISIONS #35);
  PIXEL-PERFECT keeps whole numbers always. Never out of aspect.
- **Textures**: 64 or 128 px, `NearestFilter`, no mipmaps, sRGB. Floor and walls at **32 px a
  meter** (a 64 px tile is 2 m); felt, wood and brass tiles at 32 px a meter on their surfaces.
  Edges of a tile quieter than its center; check every tile 2×2 for seams.
- **Signs**: 96×32 px (the Collector's 128×32) at **34 texels a meter**: a 96 px sign is 2.8 m wide,
  about 1:1 on screen from 10 m. Dark board, a 2 px neon tube border (edge = tier base, 1 px core =
  tier hi, rounded corners), the 20 px loss glyph on the left, the sign name in the game's font in
  the tier's hi color, a rule, and the tier word below in the base color. Hung 2.95 m up (the near,
  low-tier signs at 3.4 m so they clear the mid signs behind them from the door).
- **Panels**: 320×180 canvases; the seated eye sits 1.25 m from a 1.28 × 0.72 m screen, so a panel
  lands at about 1:1 in the 640×360 view.
- **The font** is the game's bitmap font (`src/ui/font.ts`): cap height 7 px, at scale 1 or 2 only.
  Signs letter in it too (the build script reads its glyphs).

## Glyphs

Coin (with the square hole) = money; target = a mark; skull = death; ghost = a soul; diamond chip =
a marker; heart = a life; bolt = a circuit. Same shapes in the font (HUD, panels, prompts) and on
the signs. Each must not read as a letter or digit at 7 px (the first mark and marker read as "0").

## Pipeline

- **Pixel art: the pixel-art-studio skill** (owner's instruction, DECISIONS #22). Every texture,
  sign and icon is drawn pixel by pixel by a build script in `pixel-art/<slug>/build.py` that
  renders into `public/tex/<cast>/`; it reads roles from `src/data/palette.ts`, glyphs from
  `src/ui/font.ts` and sign data from `src/data/tables.ts`. The skill's loop is mandatory: build,
  render, **look**, critique against its checklist, fix; two passes at least. Never hand-edit a PNG.
- **Models: by script through the Blender MCP**, `art/<family>/build_<asset>.py` → `.glb`; families:
  room, tables, props, avatar, the Collector; `art/people/` is the people's kit (the crowd, the
  pit boss and the Collector: parts modelled at their joints, one tint times a vertex mask,
  flat-shaded; DECISIONS #86); `art/tables/` is the six game tables (one part per room material
  round each footprint, so they merge into the room's own draws; their neon, screens and signs stay
  in code) and the stools, `art/props/` the crowd's chips, cards and dice (one tint times a mask,
  instanced); both build on `art/blender_kit.py` (DECISIONS #87). Low-poly, rigid parts, no skeleton. Budgets: ≤ 1.5k
  triangles a table, ≤ 40k on screen, ≤ 80 draw calls (static meshes are merged per material; share
  materials), 15 MB total.
- **PixelLab** only when the owner asks; any spend goes in PROGRESS.md.
- **Proof in the game, through the bloom and the snap**: `npm run shots` (door, seated, the
  Collector), `node e2e/palette.mjs` (each tier's sign from 10 m), `npm run screens` (HUD and panels,
  overlap audit). An asset that is right in its preview and wrong in the room is wrong.

## Don't

- Don't use a tier's hue for anything but that tier: not felt, not "good news" text, not HUD bars.
  (The dice trays' dark baize is the one green that isn't the low tier: far darker, panels only.)
  Marks may wear the mid tier's yellow: they are the mid tables' price.
- Don't overdrive neon past 1.0 or add a luminance-threshold bloom.
- Don't put panel text, the HUD or lit surfaces on the GLOW layer.
- Don't scale anything by a fraction, rotate pixel art, or mix pixel densities on one surface.
- Don't copy a reference. The Collector is not a Dealer across a table (DECISIONS #28): in Milestone
  5 he deals from the center of the ring, standing; his violet eyes stay.
- Nothing from Buckshot Roulette, CloverPit or Gamble With Your Friends: not their props, layouts,
  UI frames or color schemes.
