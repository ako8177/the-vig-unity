# Decisions — The Vig (Unity)

Every decision with its reason. The TS game's log (#1-#87) is frozen in `docs/DECISIONS-TS.md`.

- **U1 2026-10-15: The Vig moves to Unity, as a separate project** (owner's request: "turn all this
  into a unity project separate from this. i want cleaner graphics"). `../the-vig` stays as it is;
  this repo is new. The TS stack in its CLAUDE.md ("decided, don't relitigate") is the owner's to
  change, and they changed it.
- **U2 2026-10-15: The look is clean stylized neon** (owner's pick of three). URP at full resolution:
  no 640×360 pixel pass, no snap to TanoPal 48, no dither. Kept from STYLE.md: the palette *roles*
  (velvet carpets, damask, orange lacquer, brass, charcoal felt), the four tier colors exactly, and
  the rule that tier colors appear nowhere else. New: physically lit surfaces (URP Lit), procedural
  textures at 512-1024 px with normal maps, real lights (coves, downlights, pendants over every table,
  chandeliers, the neon signs themselves), bloom, Neutral tonemapping (ACES bleached the tier neon
  white), a little grain and vignette. Neon tubes and letters run at HDR intensities low enough to
  keep their hue.
- **U3 2026-10-15: Windows desktop first** (owner's pick). WebGL is installed for later; the Pages
  link stays on the TS game for now.
- **U4 2026-10-15: The port is exact, and the golden traces are its contract.** The C# sim was ported
  line for line from `src/sim` + `src/data` at 292a1e1 and lands on the TS sim's state (hash of a
  canonical dump) and events after every tick of 16 driven runs (solo and two-player, with and without
  the house, upgrades, unlocks; runs that win and runs that die). While both games exist, a rule change
  goes into both and the traces are regenerated. If the owner retires the TS game, say so here and the
  traces become a C#-generated regression snapshot.
- **U5 2026-10-15: A TS bug kept for parity, to fix in both.** The Boon wheel's "a charm" slot filters
  the pool with `!c.unlock`; the Insulated Kit's unlock index is 0, which JS reads as false, so the
  wheel can hand you the Kit before you've killed the Collector who unlocks it. The C# copies this
  (Systems.cs, `ApplyWheel`). The fix is `c.unlock === undefined` in TS and dropping `c.Unlock == 0`
  in C#, then regenerating the traces.
- **U6 2026-10-15: Coordinates.** The sim keeps the TS frame (right-handed, meters, -Z north, the
  entrance on +Z). Unity is left-handed, so a sim point (x, y, z) is drawn at Unity (x, y, -z): the
  lounge stays west and the back room east, as in the TS game. Movement commands are converted the
  same way (`Play.Walk`).
- **U7 2026-10-15: Keyboard controls at the tables, for now.** Seated: `[ ]` change the bet, and per
  table H/L (Hi-Lo), R/B (Red Line), SPACE (Bandit, Wheel, Dead Man's), SPACE/H/S/D/P (Twenty-One),
  X to stand. At the Collector: P pay, G challenge, 1-0 pull a lever (Shift: trace), SPACE spin,
  ENTER next. The TS game's clickable panels come back when the panels get their animations.
- **U8 2026-10-15: Built by code.** Every mesh (bevelled boxes, lathed legs and pedestals, tubes),
  material and texture is made at load from the layout data (`RoomBuilder`, `MeshKit`, `Textures`),
  as the TS game's render3d did, so the scene file holds one GameObject and the room can't drift from
  the colliders. Blender-made models replace pieces where they earn it (the people first).
- **U9 2026-10-15: Shader variants ship as material assets.** Runtime materials get their shaders
  from `Assets/Vig/Resources/VigShaders/*.mat`, which carry every keyword combination the code
  enables (normal map, emission, TMP glow), so a player build doesn't strip them (verified: the built
  player renders the same room as the editor).
- **U10 2026-10-15: Nothing new is derivative.** The port adds no mechanic, number, name or UI that
  wasn't in the TS game; its existing flags (TS #58, #62, #80) carry over unchanged.
