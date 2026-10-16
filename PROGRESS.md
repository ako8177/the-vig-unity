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
  clear of colliders); a rule check on the Boon wheel's locked charms (U12). 132 checks, all green.
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
- People still missing: the pit boss who carries a dead Collector out, the family at the house; the
  smokers' smoke, the crowd's chatter and spoken lines (see "the people" below for what's done).
- Sound: the lounge music, every synthesized effect, the spoken lines.
- The title, how-to-play, settings, pause, results screens; saving settings and meta (upgrades, best
  floor, unlocked charms); the drunk camera; the cage (charms come by post from the house).
- Co-op (Milestone 3 in the TS plan; the sim is ready for it).

PixelLab spend: 0.

## 2026-10-15: the people (U13, U14)

**Done**

- **The Collector** in his high-backed chair across his round table, facing you, eyes burning his tier's
  violet (neon, so they bloom): a gaunt long skull, a top hat with a violet band, a high open collar, a
  frock coat over a violet waistcoat, long arms and long pale fingers resting on the felt, legs under
  the table. He breathes and looks about; he nods when 0:00 comes and when he speaks at the Circuit;
  he leans in and reaches for his pull, convulses (eyes flickering) at a live lever, and slumps with
  his eyes going out when he dies; the next night's Collector has his own eyes. All from the sim's
  state and events (`People.Him`).
- **The crowd of 52** from `Layout.Floor1.Npcs`, each dressed from a stable hash (coat or dress, hair
  or a hat, skin, trousers, tie): slot players on stools pulling handles (the carousel's too), card
  and roulette players on chairs pushing chips, the dealers in bow ties and shirtsleeves (Twenty-One's
  deals as cards leave the shoe), the craps rail, the bartender pacing and polishing a glass, drinkers
  sipping, smokers with glowing cigarettes, the booths, the pianist on a bench (the piano turned to
  face him), railbirds who turn to watch when you sit at their table, walkers pacing the floor. They
  breathe, glance at you when you're close, cheer your wins (a big one brings the room in) and put
  their hands to their heads at a big loss.
- **The pipeline**: `art/blender_kit.py` + `art/people/build_collector.py`, `build_crowd.py` (run in
  Blender 5.2 via the MCP; re-runnable) → `Assets/Vig/Art/People/Resources/People/{Collector,Crowd}.fbx`
  (9k and 15k triangles; a person ~9k) → `Editor/ArtImport.cs` (import settings) → `Client/People.cs`.
- **The camera at the Collector's table** looks up across the screen to him (U14).
- **Shots**: `28_collector_floor` (him under normal light); the door, back room, bar, lounge and 13-15
  now have people.

PixelLab spend: 0.

## 2026-10-16: the big pass (U15-U25)

**Done**

- **Playable by mouse at every table** (U19): sit and the cursor comes free; the bar shows the odds card,
  chips (+ to your bet), CLEAR / MAX, the table's moves as buttons, roulette's betting layout, Crash's
  launch / auto / live cash-out, Let It Ride after wins, and at the Collector's table pay / challenge,
  the levers, the trace, the wheel, the next night.
- **Real dice** (U20) at Hi-Lo, Dead Man's Hand and Craps: PhysX throws from your side, landing on the
  sim's faces, their clicks timed to the impacts.
- **Floor 2** (U15, U23): nights 4-6, the Gold Room / Mirror Room / Furnace look, stakes doubled, and four
  new games in 3D: Roulette (the wheel turning, the ball running and dropping into its pocket), Craps
  (a point puck, every roll thrown), Peg Drop (the ball down 8 rows into its slot), Crash (a monitor
  drawing the climb, gold then red past 5x). Floor 2's Circuit: 12 levers, 4 live.
- **The easy curve** (U15): +$200 a night by default; house rules (x1.5) on the title screen.
- **Ten new charms** (U16) and the **nightly catalogue** at the house computer with a paid reroll (U17).
- **Let It Ride** (U18) and **the progressive** on the Bandit, glowing on the carousel (U25).
- **The house** (U22): living room, kitchen, the desk and computer, the family, the yard, the mailbox
  (flag up when the post comes), the car; the catalogue UI. A **title screen** with the run's options
  (curve, start floor, start at home); unlocked charms are kept between runs.
- **Sound** (U21): all synthesized: table sounds, escalating wins, coins, the crowd, the bell, the shock,
  Crash's rocket and blast, a room murmur and a lounge loop.
- **Hooks** (U24): streak banners, honest near misses, the bank rolling up and down.
- **Checks**: 242 green (golden traces regenerated from C# per U11, a fuzz through all ten games on both
  floors, floor-aware data checks); the Windows build runs its self-test clean.
- **IDEAS.md**: what makes gambling grab people and how The Vig uses each hook honestly, plus 22 next ideas.

**Not yet**

- Floor 2's crowd is floor 1's people at the new tables; the Collector is the same figure; dealers don't
  yet handle the new games' pieces.
- The house's furniture is plain blocks; the family is the crowd's kit.

## 2026-10-16: measured, paused, faster (U26-U28)

**Done**

- **The pace bots** (U26, `tools/SimCheck/Pace.cs`) play whole runs of both floors; the easy curve got a
  $2,000 bank floor after they showed it wasn't easy. Night 1: safe-only 3%, mid rotation 68%. Walks out:
  easy 39%, house rules 0%. Floor 2 night 4: mid 56%. All checks now (250 green).
- **Pause and settings** (U27): Esc stops the night; mouse sensitivity, volume, music, kept between sessions.
- **Results** (U27): the end of a run shows how far you got, what you won and lost, and your best night.
- **Chips on the felt** (U27): your stake as a real stack; the payout drops in on a win, a loss is raked.
- **Craps** shows the live point on its screen.
- **Boot** (U28): about 1.3 s in the build (was over 90 s); bloom works in the build again.
- Shots 45-47 (the chips, the pause menu, the results); the seated shots now place the toasts as the game
  does, above the bar.

**Not yet**

- Floor 2's crowd is floor 1's people at the new tables; the Collector is the same figure; dealers don't
  yet handle the new games' pieces.
- The house's furniture is plain blocks; the family is the crowd's kit.
- Side bets; NPC players at Twenty-One.
