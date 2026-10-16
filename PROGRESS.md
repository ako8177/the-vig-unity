# Progress — The Vig (Unity)

## 2026-10-15: the port begins (U1-U10)

**Done**

- **The sim, ported exactly** (`Assets/Vig/Sim`, ~3,600 lines of C# from ~3,500 of TS): the RNG
  (mulberry32, bit for bit), state, commands, events, rules, the Circuit, all ten systems, the six
  table modules with their exact odds (the Bandit's 4^9 grids, Twenty-One's basic-strategy DP), and
  all the data (economy, tables, marks, charms, upgrades, wheels, circuit rules, drinks, Collectors,
  endings, floor 1's layout and colliders, the house). Every derivation string carried over.
- **The checks** (`tools/SimCheck`, `dotnet run -c Release`): 16 golden traces from the TS sim match
  tick for tick (a one-number change is caught on the tick it lands); replay determinism; data
  validation (derivations, enumerated RTPs within 0.5%, the tier rule, cross-references, stand points
  clear of colliders). 129 checks, all green.
- **Floor 1 in the new look** (`Assets/Vig/Client`): the main floor (cream damask, the indigo
  medallion carpet, cream marble columns, chandeliers, banks of slot cabinets with reel windows and
  chaser bulbs, the carousel and its jackpot tower, the velvet-roped aisle and red runner, the bar
  with its back bar of bottles, the cage, the roulette and card tables), the lounge (violet flock,
  booths, piano, craps rail) and the back room (planks, stained plaster, bare bulbs, poker table, the
  Collector's round table and high-backed chair). Every game table: felt or baize, a padded leather
  rail, lacquered apron, brass pedestals, the tier's neon trim, a pendant lamp with shadows, a stool,
  its neon sign, and its screen. Lights: coves, downlights, pendants, chandeliers, signs. Post: bloom,
  Neutral tonemapping, grading, vignette, grain.
- **Playable**: walk (WASD, Shift to run, mouse look), sit (E), bet at all six tables by keyboard with
  the odds always printed on each table's screen (tier, win %, pays, RTP, what losing costs; marks and
  payout factors shown), drinks at the bar (1-4), call the night at the Collector's table; at 0:00 the
  lights drop and you're pulled to his table: pay, or the Circuit (pull, trace), his death, the
  Boon/Bane wheel, the summary, the next night, the ending. HUD: clock, bank/quota, night, marks,
  markers, lives, prompts, toasts, banners.
- **Tools**: `tools/unity.ps1 shots|setup|build`; 15 review shots (`shots/`, regenerated, not in git);
  a Windows build (`Builds/Windows/TheVig.exe`, 103 MB) with a `-vigshot` self-test.

**Not ported yet** (the TS game has them)

- The house (the run starts at the casino door: `GameOpts.Home = false`); the computer shop by post,
  the mailbox, the car. The sim supports it; there's no scene.
- The table animations (dice on the baize, the shuffle and flip, the wheel turning, rolling reels, the
  deal card by card) and the win celebrations; the Wheel of Marks has no wheel yet, Twenty-One's table
  isn't a half-moon.
- People: the crowd of 52, the dealers, the pit boss, the family, the Collector himself (his chair is
  empty). The TS game's Blender people (`../the-vig/public/models/people.glb`) are low-poly for the
  pixel look; they want rebuilding for this one.
- Sound: the lounge music, every synthesized effect, the spoken lines.
- The title, how-to-play, settings, pause, results screens; saving settings and meta (upgrades, best
  floor, unlocked charms); the drunk camera; the cage (charms come by post from the house).
- Co-op (Milestone 3 in the TS plan; the sim is ready for it).

PixelLab spend: 0.
