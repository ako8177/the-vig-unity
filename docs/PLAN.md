<!-- Carried over from the TypeScript game (github.com/ako8177/the-vig, commit 292a1e1) on 2026-10-15. The sim and data in Assets/Vig/Sim implement it exactly (tools/golden). Where the Unity look differs from STYLE.md, ../DECISIONS.md (U2) says how. -->

# The Vig — Plan

*Working title ("the vig" is the house's cut; here it grows every night). Rename freely; check
itch.io, Steam and the domain before the first public build. Written 2026-10-12, before any code.*

## The pitch

You're trapped in an underground casino. Every night the Collector wants more. You and your
friends gamble a shared bank up to the quota, buy charms that bend the odds, and then sit down
at the Collector's table for a game that can kill you. Make it and you go down a floor, where
the tables are worse and the money is better. Lose enough lives and the run ends.

**The one thing: safety is a slow loss.** Every table in the house has a tier, and the tiers
pay by how much you put on the line. Low-risk tables bleed you slowly (the house edge is real).
Mid-risk tables pay better but losing marks you with debuffs. High-risk tables pay best and
losing can kill you, and a dead friend's soul is bought back with the same bank you need for the
quota. The clock turns the whole room into one question: how much risk does this quota force?

## Feel references (one quality each)

- *Buckshot Roulette*: the dread of a turn-based probability game with items. Not its shotgun,
  its table, its items or its structure.
- *CloverPit*: charm synergies that turn a machine into a money printer. Not its slot machine,
  its charms or its debt curve.
- *Gamble With Your Friends*: four people in one casino making chaotic shared decisions. Not
  its machines, its quota numbers or its rooms.

## The removal test

> "If you removed risk tiers that trade money for lives, and charms that cross between the
> tables and the Collector's game, what's left is a casino minigame pack with a timer."

A feature earns its place only if it makes the risk choice sharper or connects the tables to the
Collector. Pace tests check it: a bot that only plays safe tables must fail night 1; the Circuit
must change what the best table is (a charm that pays after surviving an overload must make the
Circuit worth playing aggressively).

## Pillars

1. **Safety is a slow loss.** Low tables RTP < 1. Only risk has positive expectation. The quota
   curve is set so that a night cannot be made at the low tables alone.
2. **One bank, many hands.** The bank is shared. Anyone can win it, anyone can lose it. Souls
   are bought back from it. The game's best moments are arguments.
3. **Charms cross the room.** Every charm touches at least two of: a table tier, the Circuit,
   the Collector, lives/souls. A charm that only buffs one machine is rejected.
4. **The Collector is a choice, not a boss.** Pay the quota and move on, or gamble with him for
   a boon at the price of a worse tomorrow. Both are always available if you made quota.
5. **Readable dread.** Every risk is shown before you commit: the tier, the odds, what losing
   costs, who it costs. No hidden house edge beyond the one printed on the table.
6. **Small and proven before big.** Single player first, co-op second, floors third.

## The structure of a night (6:00 on the clock)

**Phase 1 — The floor.** Players spread out across the tables. Each table shows its tier, its
odds and its loss cost. Bets come out of the shared bank. Mid-risk losses hand out **marks**
(timed debuffs on a player or the team). High-risk losses can **kill**: each "death" costs a
life, and a player out of lives becomes a ghost, their soul held by the house; a buyback costs a
cut of the current quota.

**Phase 2 — The cage.** Markers (a second currency earned by surviving risk: high-table wins,
Circuit survivals) buy charms and permanent upgrades at the cage. Open all night; the choice is
when to stop gambling and shop.

**Phase 3 — The Collector.** At 0:00 everyone is pulled to the center table. Short of quota:
the Collector takes a life per $1,000 short, then the night is over. At or over quota: **pay**
(the quota leaves the bank; next quota ×1.5) or **gamble**: play **The Circuit** against the
Collector. Win: the quota is paid and you spin the **Boon wheel** (a team buff or a bonus).
Lose: the quota is paid, the next quota rises by a random 10–40% on top, and you spin the
**Bane wheel** (a team debuff that lasts the next night). Either way, down a floor.

**Phase 4 — Down a floor.** New tables, worse marks, stronger charms, a new Collector rule.

## The Circuit (the deadly game)

A panel of hidden circuits, some **safe**, some **overloaded**. On your turn: **inspect** one
(learn its state; costs time and a little bank), **reroute** (swap two circuits' positions
blind), or **activate** one. Safe activation: a reward (markers, bank, a charm proc). Overload:
the player takes a hit (a life) or the bank takes damage, chosen by the house rule of that
floor. Against the Collector you alternate turns; whoever is standing when the safe circuits
run out wins. Ghosts can inspect one circuit per round without activating, so a dead friend
still plays. Charms fire here: the whole point is that a table charm can change a Circuit turn
and a Circuit reward can change a table.

## Currencies (four is the ceiling)

| Currency | Kind | Comes from | Spent on |
|---|---|---|---|
| Bank ($) | shared stock | tables, Circuit rewards, Boon wheel | bets, inspections, soul buybacks, the quota |
| Markers | shared stock | high-table wins, Circuit survivals, overshooting quota | charms, upgrades at the cage |
| Lives | per player, 3 | start, Lucky charms, Boon wheel | lost to overloads, blood tables, shortfalls |
| Souls | per player, 0/1 | dying | buyback from the bank at 50% of the current quota |

Marks (debuffs) and boons are statuses, not currencies.

## Tables (first floor)

| Table | Tier | Bet | Pays | RTP | Losing costs |
|---|---|---|---|---|---|
| The Bandit (slots) | low | $100 | per reel table | 0.95 | the bet |
| Twenty-One | low | $100–300 | 1:1, 3:2 natural | 0.974 | the bet |
| Hi-Lo Dice | mid | $200 | 2.4× on a call | 1.20 | the bet + a **mark** (−5% mid payouts for 30 s, stacks) |
| The Wheel of Marks | mid | $300 | 1.6× | 1.152 | the bet + a random **team mark** |
| Red Line (cards) | high | $500 | 2.5× | 1.25 | the bet; **three losses in a row cost a life** |
| Dead Man's Hand (dice) | high | $1,000 | 6× | 1.50 | the bet; **snake eyes (1/36) costs a life** |

Numbers derive in `DESIGN.md`; the shape is the rule: RTP rises with the tier, and the price
of losing rises from money to marks to lives.

## Charms (first set, each touching two systems)

Lucky Fuse (survive an overload: +1 life), Overclock (survive an overload: payouts ×2 for 60 s),
Lead Apron (the first overload each night costs nothing), Marker Magnet (high-table wins give
double markers, Circuit inspections cost markers instead of bank), Dead Man's Insurance (a
death at a blood table refunds the bet to the bank), Loaded Dice (Hi-Lo and Dead Man's Hand
roll twice, take the better; the Circuit has one more overload), Short Fuse (every overload you
cause also damages the Collector; your marks last twice as long).

## Co-op

Two to four players online, host-authoritative, over **WebRTC with no server** (see
`CLAUDE.md` tech), which keeps GitHub Pages hosting. Single player is the same game with one
player and the same shared bank; the slice is single player and the architecture is built for
co-op from the first commit (the sim takes commands, never input; every roll comes from the
seeded RNG). Voice is not included; people use Discord.

## The owner's direction (2026-10-13)

The owner has set a bigger shape for the game; it is built in order, a floor at a time:

- **A house before the casino**: your family, a car or two, a computer where you order charms and
  items (CloverPit is the owner's inspiration for the charms: our own names and numbers); the order
  is delivered by post in front of the house; you pick it up and take it to the casino; the car
  out front takes you there.
- **Floors you unlock with money**: each introduces new games and brings back old ones at higher
  limits and more risk. Games planned include blackjack, roulette, craps, slots, case opening,
  a peg-drop board and a crash game.
- **Rooms by risk on every floor**: a bright main floor for the low tables, a room for the mid,
  an ominous back room for the high (floor 1 is built this way: DECISIONS #65).
- **A living casino**: a crowd that plays, watches you, drinks and smokes; a bar whose drinks
  make you drunk.

## The not-list

- Not Buckshot Roulette: no shotgun, no live/blank shells, no two-chair duel as the frame. The
  Circuit is a panel of circuits with inspect/reroute/activate and a ghost role.
- Not CloverPit: no slot-machine-only economy, no lever-pull core loop, no borrowed charm names
  or numbers. Our slots are one low table among many. (Its charms are the owner's inspiration
  for ours: the idea of items that bend the odds, not its items.)
- Not Gamble With Your Friends: no borrowed machines, quota numbers or UI. (A house before the
  casino and rooms by risk are the owner's inspiration from it: the idea, not its rooms.)
- No real-money anything, no purchases, no odds hidden from the player.
- No borrowed numbers: every RTP, quota and cost derives from `DESIGN.md`.
- Nothing past Milestone 1 before the Milestone 2 gate.

## Tech (decided — details in THREEJS-NOTES.md and CLAUDE.md)

TypeScript strict + Vite + Three.js, first person, low-poly with pixel textures and neon
emissives, 640×360 internal resolution through a pixel pass with a bloom pass before it (this
game earns bloom: it's a casino). Fixed 60 Hz pure, deterministic, command-driven sim with no
three import. The S&S engine layer (loop, RNG, input, audio, bitmap font, IMGUI for the 2D
overlays, e2e harness, deploy). Table UIs are 2D canvases drawn by the IMGUI kit and shown on
the table's screen as a CanvasTexture, so a slot machine's reels are the same code as the HUD.
Models by script through the Blender MCP. Co-op through Trystero (WebRTC) in Milestone 3.
