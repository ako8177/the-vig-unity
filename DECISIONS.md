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
- **U11 2026-10-15: Unity is the game; the TS game is retired for new work** (owner: "i want to focus
  unity"). `../the-vig` stays online as it is but gets no new features or rule changes. So, per U4,
  rule changes now land here only: the golden traces stay as a regression snapshot of the ported
  rules, and when a rule changes on purpose the affected traces are regenerated from the C# sim (a C#
  trace writer gets built the first time that's needed) with the reason written here. U5's bug is
  fixed here alone, with its traces regenerated. The repo is public: github.com/ako8177/the-vig-unity.
- **U12 2026-10-15: U5 fixed** (owner: "fix"). The Boon wheel's charm slot now draws only from charms
  with no unlock or an unlocked one; the Insulated Kit can't come off it before its Collector dies.
  No golden trace ever landed on that slot with the Kit locked, so the traces are unchanged; a new
  rule check in SimCheck spins the slot 600 times locked and unlocked (and fails with the bug put back).
- **U13 2026-10-15: The people are Blender-built rigid parts, posed by code.** Each family is a
  re-runnable script (`art/people/build_collector.py`, `build_crowd.py`, on `art/blender_kit.py`) run in
  Blender 5.2 through the MCP, exporting FBX to `Assets/Vig/Art/People/Resources/People/` (a Resources
  folder, so the runtime finds the meshes with nothing hand-wired; `Editor/ArtImport.cs` sets the import:
  meshes only, no materials, no rig). The look is U2's, not the TS pixel kit's: smooth lofts, bevelled
  boxes, weighted normals, ~9k triangles a person. Shapes are written in Unity's frame and a part's
  origin is its joint; a part's meshes come split by role (`head`, `head__dark`, `coll_head__eye`) and
  `People.cs` gives each role a URP Lit material (the Collector's eyes: his tier's neon). The crowd keeps
  the TS crowd's joints, measures and poses (npcs.ts), with each joint's X and Y turn negated for
  Unity's frame; people who sit where the room has no seat bring one (slot stools, card chairs, the
  piano bench), and the piano now faces its pianist (its keys were on the far side). **The Collector**
  sits in his chair facing you, long-armed (0.88 m to the wrist, so from his chair his hands rest on the
  felt), with legs under the table; he breathes, nods when he speaks, reaches for his pull, convulses
  at a live lever and slumps with his eyes going out when he dies; the next nights' Collectors are the
  same figure with their own eyes (`Collectors.Of(idx).Eyes`, TS #81). **Derivative flag (CLAUDE.md):**
  a glowing-eyed figure seated opposite you is what TS #28 flagged as Buckshot Roulette's Dealer; TS #30
  moved him to stand in the ring, and the owner's #80 brought the seated duel back (flagged there). This
  builds #80's scene as the task asked; his look (top hat, gaunt long skull, violet slits, waistcoat,
  long pale fingers) is the TS game's own. STYLE.md's "Don't" line citing #28 predates #80.
- **U14 2026-10-15: At the Collector's table the camera looks up at him.** The sim's look point is the
  screen lying on the felt, which framed only his chair's back. The client keeps the sim's eye but looks
  0.22 m higher and 0.4 m further (`Play.CollectorView`, also the shot tool's 13-15): the screen sits in
  the lower half of the view, readable, and he fills the upper. Presentation only; the sim is unchanged.
- **U15 2026-10-16: The easy curve, and a run of two floors** (owner: "change the deadlines to raise
  only by a couple hundred so i can get some easy playthroughs to test new games"; "fine tune the first
  and second floors"). The quota is the owner's lever (CLAUDE.md, "ask before"): it now rises **+$200 a
  night** from $3,000 by default, and the old ×1.5 stays as "house rules" on the title screen
  (DESIGN.md §2). A run is floor 1 (nights 1-3) then floor 2 (nights 4-6); the ending comes after night 6.
  The title screen can start on floor 2 (night 4) to test its games straight away.
- **U16 2026-10-16: Ten new charms** (owner: "introduce new charms"): Hot Hand, Red Rover, Card Counter,
  Snake Charmer (locked: kill the second Collector), Compound Interest, Comp Card, Lucky Seven, Rabbit's
  Foot, Parachute, Croupier's Thumb (DESIGN.md §12). Our names and numbers; CloverPit stays the
  inspiration for items that bend the odds, not their items (TS PLAN).
- **U17 2026-10-16: The computer sells a nightly catalogue of 4**, redrawn each time you come home; a new
  catalogue costs 1 marker. 15 charms made the full list a menu; four make it a draft.
- **U18 2026-10-16: Let it ride.** After a winning round its profit can ride, double or nothing at 50%,
  up to 3 times within 8 s. EV exactly 1.0, so no table's RTP or tier moves; odds printed on the offer.
  **Derivative flag (CLAUDE.md):** a gamble-your-winnings button is a fixture of UK fruit machines (the
  "gamble" feature), a genre convention rather than one game's mechanic; ours is a fair coin, printed.
- **U19 2026-10-16: The seated UI** (owner: "setting bets is confusing i still want an interactable ui when
  you sit to play"). Sitting frees the cursor and brings up a bar: the odds card (always printed), chips
  that add to your bet, CLEAR / MAX, each table's moves as buttons, Crash's live cash-out and auto,
  roulette's layout, the ride offer; the Collector's choice, levers and wheel as buttons too. The keys
  still work. Seated, the camera looks down past the table's screen to the felt.
- **U20 2026-10-16: Real dice** (owner: "real die rolling"). Each throw is simulated in PhysX in a lab under
  the room and the dice are turned by a cube symmetry so the sim's faces come up: a real roll, the sim's
  result (Client/DiceLab.cs). Used by Hi-Lo, Dead Man's Hand and Craps, thrown from your side. The click
  of each die is timed to its physical impacts. `Physics.simulationMode` is Script (the room has no
  other physics).
- **U21 2026-10-16: Every sound is synthesized at load** (Client/Sfx.cs): no audio files to license or
  ship. Wins escalate (chime, fanfare, coins and the crowd), the 0:00 bell, the shock, the rocket and
  its crash, a murmuring room and a lounge loop that duck at midnight.
- **U22 2026-10-16: The house is built** (owner: "get the house built"), from the same data the sim
  collides with: living room, kitchen, the desk and its computer (the catalogue), the family, the yard,
  the mailbox (its flag goes up when the post comes), the car. A run starts there by default (title
  screen option).
- **U23 2026-10-16: Floor 2's games** (DESIGN.md §11): Roulette and Craps (low), Peg Drop (mid), Crash
  (high), with Hi-Lo and Red Line back at double stakes, each with exact odds checked by SimCheck. Floor 2
  shares floor 1's shell; its look is its own (the Gold Room, the Mirror Room, the Furnace). The Bane's
  extra lever is now "+1 on the floor's count" (it was a flat 4, a no-op on floor 2's 4-of-12).
  **Derivative flags:** Crash and Peg Drop are genres from online casinos (Plinko's board, the crash
  multiplier), not one game; ours carry our own numbers, and Crash's life-past-5x is ours.
- **U24 2026-10-16: Near misses are honest.** The Bandit says "SO CLOSE" only when two sevens really
  landed on a line, and celebrations scale with net winnings (a 0.4x return is never dressed as a win).
  Real machines fake both; the not-list's "no odds hidden" extends to "no feelings faked".
- **U25 2026-10-16: The Bandit's progressive**: 2% of every pull feeds a pot ($5,000 a floor to start),
  the whole grid alike takes it; it glows on the carousel's tower. RTP effect under 0.001 (DESIGN.md §12).
- **U26 2026-10-16: The easy curve gets a $2,000 bank floor, and the run's balance is measured.** The pace
  bots (TS tests/bots.ts) are ported to C# and play the run of two floors (`tools/SimCheck/Pace.cs`). They
  showed the easy curve wasn't: 10% of sensible runs walked out, because each paid night reset the bank to
  $1,000 and the +$200 steps were noise next to that. The bank floor isn't on CLAUDE.md's "ask before"
  list; the quota curve (which is) is unchanged. With a $2,000 floor on the easy curve 39% walk out and night
  1 is untouched; house rules keep $1,000. The lessons are checks now (DESIGN.md §8b).
- **U27 2026-10-16: Pause, settings, results, and your bet as chips on the felt.** Esc pauses the night (the
  sim stops); settings are mouse sensitivity, volume and music, kept in PlayerPrefs. A run ends on a results
  screen (the ending, the night reached, rounds and win rate, won/lost, biggest win, lives, marks, markers,
  Collectors killed, your best night) with ANOTHER RUN and THE TITLE. At the tables whose pieces aren't
  already the bet (Hi-Lo, the Wheel, Dead Man's Hand, roulette, craps, Crash) your stake is a stack of
  chips in casino colors on the felt; a win's payout stack drops in beside it, a loss is raked away.
  Presentation only. The craps screen shows the live point and what rides on it.
- **U28 2026-10-16: The build loads baked textures, and keeps its post-processing.** Drawing every procedural
  texture pixel by pixel at boot took over 90 s in the player (about 4 s in the editor). The build step now
  draws them in the editor and saves them to `Resources/VigTex` (21 textures, git-ignored, rebuilt on every
  build); the player loads those, and the editor always draws fresh, so a changed texture is never stale.
  Boot is now about 1.3 s. URP's "strip unused post-processing variants" was removing the Bloom shader
  from builds (the volume is made at runtime, so URP saw no use of it); both strip flags are off.
