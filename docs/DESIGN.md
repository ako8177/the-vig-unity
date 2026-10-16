<!-- Carried over from the TypeScript game (github.com/ako8177/the-vig, commit 292a1e1) on 2026-10-15. The sim and data in Assets/Vig/Sim implement it exactly (tools/golden). Where the Unity look differs from STYLE.md, ../DECISIONS.md (U2) says how. -->

# The Vig — Design model

Every number derives from this file. These are **starting hypotheses for the slice**, chosen
from the model and tuned by play and by the pace tests, never by looking at another game's
numbers. Change a number → write the new derivation here first.

## 1. Units and the one rule

- Tick 1/60 s. A night is **360 s**. Money in whole dollars. Lives are integers per player:
  **3 each** at the start of a run. The slice is **three nights** on floor 1; surviving the
  third Collector wins it. **Since U15 (2026-10-16) a run is two floors: three nights on floor 1,
  then the stairs down to floor 2 (§11) for nights 4-6; surviving the sixth Collector wins it.**
- **RTP** (return to player) is the expected payout per dollar bet. **The rule: RTP rises with
  the tier, and the price of losing rises from money → marks → lives.** Low < 1.0; mid > 1.0
  with marks; high ≫ 1.0 with death.
- Every random outcome comes from the seeded RNG through the command log, so a night can be
  replayed from inputs.
- **Walking 3 m/s; holding Shift sprints at ×1.7 = 5.1 m/s** (DECISIONS #73: the owner found the
  walk slow on the three-room floor). The main floor's 28 m is under 6 s at a sprint, the Lounge to
  the Back Room ≈ 3 s. The pace bots sprint every walk, so the night's arithmetic assumes it.

## 2. The quota

**The easy curve (U15, the owner's call, 2026-10-16):** "change the deadlines to raise only by a
couple hundred so i can get some easy playthroughs to test new games." The quota now rises **+$200
a night** from night 1's $3,000: $3,000, $3,200, $3,400 on floor 1, $3,600, $3,800, $4,000 on floor 2
(a Boon's -20% still applies, to the nearest $10). It is the default; the curve below (×1.5) stays in
the game as **house rules** on the title screen. Night 1 is unchanged, so the floor-1 lesson holds
(safe-only fails night 1: it still needs +$1,500 from a $1,500 bank in 360 s); what goes is the
escalation that made nights 2-3 force blood. That's deliberate while the owner tests the new games;
when the run is balanced again, the curve between the two is the owner's to pick (CLAUDE.md, "ask
before").

**The easy curve's bank floor (U26): $2,000.** Measured (§8, 100 seeds, a sensible mid rotation that
always pays): with the old $1,000 floor the easy curve let only **10%** of runs walk out of all six nights,
because after paying, the bank fell back to $1,000 and each night asked +$2,200 or more from scratch: the
+$200 steps barely mattered. With a $2,000 floor the night after a paid quota asks about +$1,200 and **39%**
of runs walk out (42% with high-table bursts when behind), while night 1 is untouched (68% made; safe-only
3%). House rules keep the $1,000 floor and the ×1.5 curve: nobody walks out on the mid rotation alone.

Night 1 **$3,000**, then ×1.5: $4,500, $6,750, $10,125… Start bank **$1,500** (DECISIONS #40;
it was $1,000, and the pace bots went broke before the night's drift could show). A lost
Collector gamble adds a random +10–40% on top of the ×1.5 for the next night. Shortfall:
**one life per $2,000 short** (DECISIONS #55; it was $1,000, and 23% of sensible runs died on
night 1 before seeing the Collector), rounded up, taken from the player with the most lives (ties:
team picks; solo: you), and the Collector takes the whole bank. Overshoot pays **1 marker per
$500 over** (whole $500s, counted before the quota leaves the bank).

**The bank carries over (DECISIONS #10).** After the quota is paid, whatever is left starts the
next night; under $1,000 it is raised to $1,000 (the floor), and the +$500 upgrade adds on top.
After a shortfall the bank is empty, so the next night starts at the floor. The quota still
rises ×1.5 after a shortfall: the curve is per night, not per payment.

**Nights 2 and 3, re-derived.** A team that paid night 1 exactly starts night 2 at $1,000 and
needs **+$3,500** in 360 s; night 3 needs **+$5,750**. Measured (§8, the pace bots, which pay
walking time and every mark): a solo Hi-Lo/Wheel rotation earns **≈ +$2,000 a night** (σ ≈ $1,700),
and adding two Red Line bursts **≈ +$3,400** (σ ≈ $2,600). So night 2 asks for the bursts plus a
carried cushion, and night 3 asks for more than one player can expect: the high tables, an
Overclock window, the Boon, or the overshoot carried from night 2. Overshooting is worth twice:
markers now, and money tomorrow. That is the escalation: night 1 forces mid risk, night 3 forces
blood. Measured on 200 seeds (§8): a sensible solo bot that always pays dies on night 1 6.5% of
the time and survives all three nights 38% of the time. **Open questions for the owner** (the quota
curve and the win condition are theirs): whether a lost gamble's +10–40% is too steep (it is why
paying beats gambling, §7), and whether being short on the last night with lives to spare should
still count as walking out (today it does: DECISIONS #56).

## 3. Tables (floor 1) and their derivations

Rates assume one player at the table continuously.

| Table | Tier | Bet | Round | Win p | Pays | RTP | EV/round | EV/min | Losing costs |
|---|---|---|---|---|---|---|---|---|---|
| The Bandit | low | $100 | 2.5 s | 0.259 (0.294 incl. money back) | 3×3 pattern table below | 0.9504 | −$5 | −$120 | the bet |
| Twenty-One | low | $100–300 | 8 s | ≈0.43 (push ≈0.09) | 2× (1:1), 2.5× natural (3:2); double and split stake more | 0.992 | −$1.60 at $200 | −$12 | the bet (or the doubled / split stakes) |
| Hi-Lo Dice | mid | $200 | 3 s | 0.50 | 2.4× | 1.20 | +$40 | +$800 (unmarked) | the bet + 1 **mark** |
| Wheel of Marks | mid | $300 | 5 s | 0.48 | 0.5× / 1.5× / 2.5× / 6× by color | 1.16 | +$48 | +$580 | the bet + 1 **team mark** (6 of 25); black returns half |
| Red Line | high | $500 | 6 s | 0.50 | 2.5× | 1.25 | +$125 | +$1,250 | the bet; **3 losses in a row cost a life** |
| Dead Man's Hand | high | $1,000 | 4 s | 0.25 | 6× | 1.50 | +$500 | +$7,500 | the bet; **snake eyes (1/36) costs a life** |

"Pays" is the total returned on a win, stake included. Every table is a finite set of equally
likely outcomes from the seeded RNG, so its RTP is exact by enumeration (the data test checks
it), and a 100k-round seeded run must land within 4 standard errors of it.

**Stakes (DECISIONS #85).** You pick your bet: floor 1's limits are Bandit $50–250, Twenty-One
$50–400, Hi-Lo $100–500, Wheel $100–600, Red Line $250–1,250, Dead Man's $500–2,500, each in its
own steps; the table's standard stake (the "Bet" column above) is what the pace bots play. RTP
doesn't depend on the stake; variance does: bots betting the maximum make night 1 less often
(54% vs 66% for the mid rotation). A deeper floor scales every limit.

**The mechanics** (one honest random draw each; the odds printed are the odds):
- **The Bandit** (DECISIONS #75, the owner's 3×3): nine cells, each an independent stop of a
  10-stop strip (one 7, two ★, three BAR, four blanks). Every pattern that hits pays, summed. On
  each of the **8 lines** (3 rows, 3 columns, 2 diagonals): 7 7 7 **10×** (p 0.001 a line), ★ ★ ★
  **3×** (0.008), BAR BAR BAR **1×** (0.027), any three symbols **0.3×** (0.18); the **four corners**
  alike **3×** (Σp⁴ = 0.0098); **the whole grid** alike **50×** (the jackpot, ≈ 1 in 50,000 pulls,
  all BARs being most of it). Lines: 8 × (0.010 + 0.024 + 0.027 + 0.054) = 0.920; corners 0.029;
  the grid 0.001 → enumerated over the 4⁹ weighted grids, **RTP 0.9504**; ahead on 25.9% of pulls,
  something back on 29.4%. No line pays more than 10× ($1,000): the low-tier ceiling of
  DECISIONS #38 holds for everything but the 1-in-50,000 grid. The grid size is data (`GRID`): a
  better casino's machine can be 5×5 or 6×6 with its own derivation here.
- **Twenty-One** (DECISIONS #76, the owner's "real card odds"): a **6-deck shoe** (312 cards)
  shuffled by the sim's RNG and dealt without replacement, a fresh shuffle when fewer than 78 are
  left at the start of a hand; the dealer hits soft 17 and peeks (a dealer natural ends the hand
  before you act); a natural pays 3:2; ties push. You may **hit, stand, double** on any first two
  cards (after a split too) and **split a pair once** (any two ten-value cards count; split aces
  take one card each; 21 after a split is not a natural). Basic strategy's return, by dynamic
  programming over an endless shoe with every option, is **0.9921**; 400,000 hands from the real
  shoe measure 0.9929 (σ ≈ 0.002): the shoe's depletion is a whisker in the player's favor, and
  the shoe count is printed on the felt, so a counter can lean on it. Win ≈ 0.43, push ≈ 0.09. It
  was 0.9736 (endless shoe, hit/stand only): the low tier still bleeds (−$1.60 a $200 hand),
  just slowly, as a real one does; safe-only still makes night 1 8% of the time (§8 test 1).
- **Hi-Lo Dice**: one die; call HI (4–6) or LO (1–3). p = 1/2 exactly, pays 2.4×.
- **Wheel of Marks** (DECISIONS #74, the owner's colors): 25 slots, **8 red 1.5×, 3 purple 2.5×,
  1 gold 6×, 7 black 0.5× (half the bet back), 6 marks 0 + a team mark**. Σ = 12 + 7.5 + 6 + 3.5 =
  29 → **RTP 29/25 = 1.16**; ahead on 12 of 25 (p 0.48). Per $1 the σ is 1.28 (it was 0.72 at a flat
  1.6× on 18 of 25): the same mid edge, but no longer the floor's one reliable earner. Team marks
  come on 24% of spins (was 28%).
- **Red Line**: call RED or BLACK; one card from a fresh deck. p = 1/2 exactly, pays 2.5×.
- **Dead Man's Hand**: two dice. **Snake eyes (1–1) costs a life** and the bet; **any other
  double or a total of 10+ wins 6×** (2–2, 3–3, 4–4, 5–5, 6–6, 4–6, 6–4, 5–6, 6–5: 9 of 36);
  everything else loses the bet. p = 9/36 = 0.25 × 6 = **RTP 1.50 exactly**. (Old wording: 5× at
  p 0.30 with the 1–1 checked before a separate win roll, which made the real RTP 35/36 × 1.5 =
  1.458; DECISIONS #11.)

**Marks (Hi-Lo):** −5% to *that player's* **Hi-Lo** payouts, stacking to 10 (DECISIONS #39: they
used to tax both mid tables, which made the Wheel worth ≈ $1.00 a dollar to a marked player and
the rotation below earn +$590 a night, not a minute); each new stack
resets the timer to 30 s, and the whole mark falls off 30 s after your last Hi-Lo loss. Hi-Lo
breaks even at 1.20 × (1 − m) = 1 → m = 16.7%: **3 stacks still pay (+2%), 4 stacks lose (−4%)**.
At 3 s a round and 50% losses you earn a mark every 6 s, so a Hi-Lo burst is profitable for
about **5 rounds (15 s, ~+$150 expected)**, then the player should move. Marks make the mid
tables a rotation, not a camp.

**Team marks (Wheel):** one of four, equally likely: Cold Deck (all payouts −10%, 45 s),
Sticky Floor (move speed −30%, 45 s), Blackout (odds hidden on every table, 45 s; the panels
say so), Tax (5% of the bank, taken at once; no timer). The same mark again resets its timer.
28% of rounds → ~3.4 team marks a minute from one player at the Wheel. The Wheel is money that
costs your friends something.

**Blood (DECISIONS #8): a blood-table "death" costs one life; you become a ghost only at 0
lives.** Red Line's third loss in a row and Dead Man's snake eyes each take a life. The Red Line
streak is **per player and survives standing up** (otherwise you'd stand after two losses and
the death would never come); a win resets it, and so does the life it takes.

**Red Line's sentence:** with p(loss) = 1/2, the chance of a three-loss streak within n rounds
is 1 − a(n)/2ⁿ, where a(n) counts the win/loss strings with no three losses in a row
(1, 2, 4, 7, 13, 24, 44, 81, 149, 274, 504): **0.125 at n = 3, 0.3125 at n = 6 (1 − 44/64),
0.508 at n = 10**. A **six-round burst (36 s) is +$750 expected at a 31% risk of losing a life**.
That's the table's sentence, and it's printed on the felt.

**Dead Man's Hand:** 2.8% chance of losing a life per roll, 34% per minute of rolling (15 rolls).
Bank variance is the other killer: at $1,000 a roll a $1,000 bank is broke on one loss (75%). It
exists for the last minute of a night you're about to fail.

**Why night 1 forces risk.** You need +$1,500 in 360 s. Low tables are negative. Hi-Lo bursts
alone (+$150 per 15 s then 30 s off) average ~+$200/min → +$1,200 → **short**. Hi-Lo bursts
rotated with the Wheel ≈ +$600/min on paper, **≈ +$330/min measured** (walking 16 m between them,
Cold Deck, waits) → makes it about 2 nights in 3, at the cost of team marks. Two Red Line bursts
(+$1,500 expected) on top, once the bank can take three $500 losses (≥ $2,000), make it about
3 nights in 4 and cost a life on Red Line about 3 nights in 10 (§8, 200 seeds). The model says: a night-1 team spreads across
Hi-Lo, Wheel and one brave Red Line player.

## 4. Souls

Blood tables, overloads and shortfalls take **lives**; a player at **0 lives** is a **ghost**
(DECISIONS #8): can walk, watch, talk, and **inspect one circuit per Circuit round**; cannot bet
or buy. **Solo, a ghost ends the run** (no one is left to buy you back); the results screen says
what the soul rule would have offered. **Buyback: 50% of the current quota** ($1,500 on night 1),
from the bank, by any living player at the cage. A ghost at the end of a night stays a ghost
(their lives don't refill) and the Collector counts a ghost as 0 lives for shortfalls.

## 5. The cage: markers and charms

**The house (DECISIONS #79).** A run starts at home and comes home after every Collector. Charms
and upgrades are **ordered from the computer at home**, paid in markers at once, and arrive in the
mailbox out front **6 s after the last order** (long enough to walk from the desk to the curb,
short enough never to wait on); you carry them in with E at the mailbox (a charm to whoever
ordered it, an upgrade to the run), then take the car (E) to the casino's door, where the 6:00
clock starts. **Nothing runs at home**: no clock, no marks, no buzz. A charm in the post holds
its slot; what's left in the mailbox waits for the next time you're home (the car asks first).
So the shopping beat moves from the middle of a night to between nights: markers earned tonight
(overshoot, high-table wins) are spent on tomorrow's loadout, and a mid-night charm buy is gone.
The cage keeps soul buybacks. The pace bots play nights back to back without the house (it costs
no clock time, so the night arithmetic is unchanged); a sim test plays the house flow.

A run starts with **6 markers** (DECISIONS #85). **Markers** come from: a Red Line win (+1), a Dead Man's Hand win (+2), each safe circuit you
activate (+1), each $500 over quota (+1). Markers are the team's, like the bank. Slice charms
(each player has **3 charm slots**; a charm is bought once and worn by the buyer):

| Charm | Price | Table side | Circuit / Collector side |
|---|---|---|---|
| Lucky Fuse | 3 | — (lives) | **win a Circuit after surviving an overload in it: +1 life** |
| Overclock | 4 | **your stakes, and so your wins, ×2 for 60 s** of table play | …starting when you survive an overload |
| Lead Apron | 3 | **the first life you would lose each night** (blood table, Circuit or shortfall) **costs nothing** | (the same: an overload, too) |
| Marker Magnet | 3 | **high-table wins give double markers** | **inspections cost 1 marker instead of $100** |

Revised after the Milestone 1 review (DECISIONS #53, #54): as first written, Lucky Fuse made every
overload free, Lead Apron + Lucky Fuse made the Circuit a life generator (+0.6 lives a game), and
Overclock doubled the whole return (the Bandit returned 1.90 a dollar for a minute). Now:
Overclock doubles the stake, so the expected win doubles and the odds don't; Lead Apron reaches
onto the floor (it changes whether you'll take a Red Line burst); and Lucky Fuse pays only for an
overload survived on the way to beating him. A revealed overload is dead to the team (§6), so no
charm can be farmed by activating a known overload. "Survive an overload" means activating an
overloaded circuit and still having a life after it. **Overclock's window counts floor time
(DECISIONS #9)**: the Circuit is played at 0:00 when the tables are closed, so the 60 s run from
the next night's first second. **Marker Magnet** runs
the other way, from the tables into the Circuit: a Red Line burst (≈3 wins) pays for it, and
it turns the team's two inspections from $200 into 2 markers. Price derivation: 3 markers ≈ one
Red Line burst's wins plus one safe circuit; Overclock costs one more because its window is worth
≈ +$600 on a mid rotation (+$1,250 on Red Line).

Derived check (§8 test 5): Lead Apron takes the Circuit's life cost to ≈ 0 for any line (0.16 →
0.00 lives a game), which is what makes gambling cheap for the player who wears it.

**Upgrades** (permanent across runs, bought with markers at the cage, saved with the settings):
a fourth charm slot (6), +$500 starting bank every night (4), Hi-Lo marks last 20 s (4).

## 6. The Circuit (floor 1 rules): the lever duel

The owner's game (DECISIONS #80). Under the Collector's table: **10 levers, 3 wired live** to a
battery, shuffled by the seeded RNG, the wires knotted so you can't follow one. You and he
**take turns pulling one**, you first. **A dead lever pays the puller's side** (you: **+$300 and
+1 marker**; Marker Magnet makes it 2). **A live lever shocks whoever pulled it.** **Your second
shock ends it: a life, and the quota is paid** (Lead Apron saves the life, not the duel). **His
second shock kills him: you keep the quota.** He picks a lever **at random** (the owner: "not
simply up from 1"); so do you, unless the **Insulated Kit** gives you **two free wire traces** a
duel (a trace shows one lever live or dead; it doesn't use your turn; a lever you traced live
you only pull with nothing else left).

Measured (400 games, tests/pace.test.ts test 5): **pulling at random wins 51%** (the model: a coin
flip, 50%); **with the Kit 73%** (model 76%); the Bane's fourth live lever: model **42%**. A
duel costs you 0.49 lives on average (0 with the Lead Apron). No information, no positions: the
decision is whether to sit down at all, and the Kit is the only lever on the odds.

## 7. The Collector's choice and the wheels

Made quota → **Pay** (quota leaves the bank; next ×1.5) or **Challenge him** (DECISIONS #80): the
duel, nothing paid up front. **Win: he dies, the quota stays in your bank** (its overshoot still
pays markers), and you spin the **Boon wheel**. **Lose: a life, the quota is paid, the Bane
wheel.** The lost gamble's +10–40% on the next quota is **gone** (the owner's call). The wheels are
real wheels now: six wedges, a pointer, a spin that slows onto the slot the sim drew.

**The Collectors (DECISIONS #81).** The one you kill is replaced the next night: THE COLLECTOR,
THE AUDITOR, THE RECEIVER (data/collectors.ts; one model with other eyes for now). Each will have
his own signature game; only the Circuit exists, so every one plays it. **The first time you kill
a Collector, his charm joins the pool for every run after** (the first's: the Insulated Kit; the
computer shows locked charms and who guards them). **A run's ending** counts the Collectors you
killed before walking out: none, PAID IN FULL; one or two, AN EMPTY CHAIR; three, THE HOUSE IS
YOURS.

Boon (6 slots): everyone +1 life · +$1,000 · +3 markers · a random charm (from the pool) · next
quota −20% · all marks cleared and the team is mark-immune next night.

Bane (6 slots, all last the next night): Cold Deck all night · one random living player −1
life now · blood tables kill on **two** losses · Hi-Lo marks last 60 s · soul buyback 75% (solo:
the next bank starts $500 lower, DECISIONS #51) · the Circuit has **4 live levers** (42%).

**The last night** has no tomorrow for a wheel to touch (DECISIONS #50): won, he dies, you keep
the quota and the house's ledger gives a free permanent upgrade; lost, a life and the quota.

**Measured (§8 test 6): challenging now beats paying.** Always challenging scores 2.01 (nights +
lives/10) and survives all three nights 37% of the time; always paying 1.57 and 12%. A won duel is
worth the whole quota, and at 50% that outweighs a life plus the Bane. **Open for the owner**: if
paying should stay a real choice, the levers are the duel's odds (a fourth live lever: 42%), what a
win keeps (half the quota), or what a loss costs. Nothing changed until the owner says.

## 8. Derived checks (pace tests, 200 seeds)

The bots (tests/bots.ts) play through commands and pay walking time at 3 m/s, and wait out each
table's animation (src/data/feel.ts, inside its round time). 200 seeds since the
Milestone 1 review: 20 gave a lucky sample (80% where 200 say 64%). Thresholds are rates
(DECISIONS #42, #57); the last run's numbers are in brackets.

1. **Safe-only** bot (Bandit + Twenty-One at minimum stakes) makes night 1 at most 15% of the time
   and ends the night poorer than it started on average [4%; mean bank $1,239 from $1,500].
   Luck can carry a low table for a night now and then; expectation never does.
2. **Mid-rotation** bot (Hi-Lo bursts of ≤ 5 rounds, out at 3 stacks, the Wheel while marked)
   makes night 1 at least 50% of the time with ≥ 3 team marks suffered on average [66%; 3.9].
3. **Red Line burst** bot (mid rotation; up to two six-round bursts when it is **behind**: past the
   night's halfway mark, short of the quota, the bank holding $2,000) makes night 1 at least 60% of
   the time and more often than the mid rotation alone, and a night that bursts loses a life on Red
   Line at least 20% of the time [69% vs 66%; 31 of 200 nights burst, 32% of them lost a life]. It
   used to burst as soon as the bank allowed, on pace or not, and counted lives over all nights;
   with sprinting and the swingier Wheel that bot gambled away nights the rotation would have made
   (64% vs 66%). The lesson stands as a rule a player can follow: the blood table is for the night
   you're losing, and a burst costs a life about one time in three (DECISIONS #73).
4. **Dead Man's** bot from a $1,000 bank goes broke at least 30% of the time [78%].
5. **Circuit** (400 games, the lever duel): pulling at random wins 40–60%, the Insulated Kit's
   traces at least 65%, and Lead Apron takes the life cost to ≈ 0 [51% at 0.49 lives; the Kit 73%;
   the apron 0.00].
6. **Collector's choice** (re-derived, DECISIONS #80): always challenging must survive at least
   as well as always paying, and the charm-based decision bot stay within 0.15 of paying
   [challenge 2.01, decide 1.60, pay 1.57]. A won duel keeps the quota, so the challenge is now
   the strong play; whether that's right is the owner's question (§7).
7. **Ghost**: a two-player run where one becomes a ghost in the first minute still makes night 1
   at least 30% of the time when the survivor buys the soul back once the bank holds
   quota × 0.5 + $1,000. A lower bound: the bot doesn't play the bought-back player [45%].

## 10. The bar (DECISIONS #67)

The main floor's bar sells drinks for **cash from the bank**: Draft Beer $40 (25 s of buzz), Dry
Martini $120 (50 s), Double Whiskey $200 (80 s), The Collector's Own $500 (150 s). Buzz stacks to
**180 s** and counts down in floor time; at **90 s** you are as drunk as the screen gets (it swims
and doubles, the camera sways, your walk wanders; a MILD setting softens it). In the slice a drink
buys nothing but the feeling: it is a way to spend money that isn't a bet, which makes it the
cheapest bad decision in the house.

**Each drink's edge** (DECISIONS #77, the owner: "add small buffs to drinking alcohol to give a
reason to drink"): while its own buzz runs, payouts at one tier's tables are multiplied. The rule:
**played at that tier for the whole buzz, the edge about pays the drink back**, so a drink is a
tempo call (walk to the bar, then play the tier it favors, swimming), never free money.
- **Every drink but the house special lasts a minute** (DECISIONS #83, the owner), the edges
  unchanged, so the cheap ones now pay back more than they cost:
- Draft Beer $40, 60 s, **low +6%**: 24 Bandit pulls return ≈ $2,280; 6% ≈ **$137**.
- Dry Martini $120, 60 s, **mid +4%**: ~20 Hi-Lo rounds return ≈ $4,800 (at 1.2), minus marks;
  4% ≈ **$190**.
- Double Whiskey $200, 60 s, **high +3%**: ~10 Red Line rounds return ≈ $6,250; 3% ≈ **$190**
  (Dead Man's Hand returns more, and kills more).
- The Collector's Own $500, 150 s, **every table +5%**: a mixed floor turns over ≈ $10,000 in
  150 s; 5% ≈ **$500**.
The same drink again restarts its edge; different drinks' edges multiply. The panels print the
factor ("YOUR PAYOUTS NOW x1.06"); the bar prints each edge and how long it has left. The
odds printed on the felt stay the base odds: an edge is the player's, like a mark.

## 9. What the slice must answer

1. Does the room feel like one question (how much risk does this quota force), or like six
   minigames?
2. Does a charm make you play the Circuit differently?
3. Is "pay or gamble" a real argument with a friend, or an obvious pick?
4. Can you read a table's tier, odds and loss cost before you commit, in first person, in one
   glance?

If any answer is no, fix the model here before Milestone 3.


## 11. Floor 2 (U15, U23)

**The structure.** Nights 4-6 are on floor 2. It is floor 1's shell (the same three rooms, bar, cage
and the Collector's table) dressed new: THE GOLD ROOM (black and gold art deco), THE MIRROR ROOM,
THE FURNACE (brick and embers). Every stake is **doubled** (`Tables.FloorStakes[2] = 2`), old games and
new. The Bandit's progressive starts over at $5,000 on the new floor. The tier rule holds on the floor:

| Table | Tier | Bet (×2 on floor 2) | Pays | RTP | Losing costs |
|---|---|---|---|---|---|
| Roulette | low | $50-$250, std $100 | even 2x, dozen 3x, number 36x | 36/37 = **0.973** | the bet |
| Craps (pass line) | low | $50-$250, std $100 | 1:1 | 244/495 × 2 = **0.986** | the bet |
| Peg Drop | mid | $100-$500, std $200 | 20x / 4x / 1.6x / 0.7x / 0.4x | 300/256 = **1.172** | the center slot: a mark |
| Hi-Lo (again) | mid | ×2 | 2.4x | 1.20 | the bet + a mark |
| Crash | high | $250-$1,250, std $500 | cash out at any x | **1.246** at 3x | the bet; a crash past 5x costs a life |
| Red Line (again) | high | ×2 | 2.5x | 1.25 | 3 losses in a row cost a life |

**Roulette.** One zero, 37 pockets. Every bet pays 36/37 of fair odds, so every bet returns $0.973:
the classic slow loss, and the game where the choice is only how wild you want it (18 of 37 at 2x,
or 1 of 37 at 36x). Croupier's Thumb (§12) makes a single number pay 45x = 1.216.

**Craps.** The pass line only. Come-out: 7 or 11 wins (8/36), 2, 3, 12 lose (4/36), anything else is
the point; then roll until the point (win) or a 7 (lose). P(win) = 8/36 + Σ over points p(point) ×
p(point)/(p(point)+p(7)) = 244/495 = 0.49293 → RTP 0.9859. The stake rides every roll; at 0:00 a bet on
a point is handed back (the table closes, nobody loses to the clock). You throw the dice yourself.

**Peg Drop.** 8 rows of pegs; the ball goes left or right at each (the sim's 8 coin flips), so slot k
is reached with C(8,k)/256: 1, 8, 28, 56, 70, 56, 28, 8, 1. Payouts 20x, 4x, 1.6x, 0.7x, 0.4x (symmetric):
RTP = (2×20 + 16×4 + 56×1.6 + 112×0.7 + 70×0.4)/256 = 300/256 = 1.172. Ahead on 74 of 256 (29%): a mid
table that pays like a lottery at the edges. Its price is mid's: landing dead center (70/256) marks you
(Pegged: -5% on your Peg Drop payouts a stack, 30 s, stacking to 10, as Hi-Lo's).

**Crash.** The multiplier climbs as e^(0.28 s) (2x at 2.5 s, 5x at 5.7 s, 10x at 8.2 s) and crashes at a
point drawn so that P(it reaches x) = x^-0.8 (from one draw u: crash = u^(-1/0.8), capped at 1000x). Cash
out at x and you're paid x times the stake. EV at a target x is x × x^-0.8 = x^0.2: **it rises the longer
you ride** (2x 1.15, 3x 1.25, 5x 1.38, 10x 1.58), which is what makes greed the right call on average
and the wrong one in the moment, so the price has to bite: **a crash past 5x costs a life**. Riding for
10x loses a life on 5^-0.8 - 10^-0.8 = 11.8% of rounds. The printed RTP is the 3x line (1.246, the
table's "standard play"), and the screen prints the win chance at the auto cash-out you set. A win pays
+1 marker (as Red Line). This is the game where the owner's "make it grabbing" lives: a real-time
decision with the odds printed and the dread rising on screen.

**The Circuit, floor 2.** 12 levers, 4 live, a dead lever pays $600 and a marker. Measured over 200k
random duels: you win **45%** (63% with the Insulated Kit pulling known-dead levers first; 44% with the
Bane's extra live lever). The Bane is now "one more live lever than the floor's" (floor 1 unchanged: 4 of
10, 42%).

## 12. The second set of charms, the catalogue, letting it ride, the progressive (U16-U18, U25)

**Charms** (markers, from the house computer). Each touches at least one system (DECISIONS #85):

| Charm | Price | Does | Derived |
|---|---|---|---|
| Hot Hand | 4 | +10% payouts per win in a row at the same table, to +50% | at Hi-Lo a 3-streak pays 2.4 × 1.3 = 3.12x; the reset is the leash |
| Red Rover | 3 | Red Line's life-risking call (two losses in a row) pays 4x not 2.5x | that call's EV goes 1.25 → 2.0, at the price of the life: the danger call becomes the tempting one |
| Card Counter | 3 | naturals pay 2:1; the screen shows the count | naturals are ~4.75% of hands: +0.024 RTP, 0.992 → ~1.016 |
| Snake Charmer | 5 | Dead Man's snake eyes wins 6x instead of a life (locked: kill the 2nd Collector) | 10/36 × 6 = RTP 1.667 and no death: the run-defining unlock |
| Compound Interest | 4 | +10% of the bank at each new night, max $600 | a $3,000 carry is +$300 a night: the money-printer engine |
| Comp Card | 2 | drinks free; their edge lasts twice as long | a Martini's +4% runs 120 s for $0 |
| Lucky Seven | 3 | the Bandit's 7-line pays 25x not 10x | 8 lines × (1/10)^3 = 0.8% a pull × 15 extra = +0.12: the 4^9 grids enumerate to 1.070 |
| Rabbit's Foot | 4 | once a night, a losing Hi-Lo or Dead Man's roll is rolled again | worth one coin flip on the night's first loss |
| Parachute | 4 | a crash returns half the stake and never costs a life | Crash at 10x without the life risk: the greed charm |
| Croupier's Thumb | 3 | roulette's single number pays 45x | 45/37 = 1.216 on numbers: a low table turned into a long shot |

**The catalogue (U17).** The computer shows **4 charms a night**, drawn from the pool (locked charms
excluded), redrawn when you come home; **a new catalogue costs 1 marker**. With 15 charms, which ones you
see is part of the run (the roguelike's draft), and the reroll is the first gamble of every night.

**Let it ride (U18).** After any round that comes out ahead, its **profit** can ride: double or nothing
on a fair coin (50%, 2x, or lose the profit), up to 3 times, within 8 s. EV exactly 1.0: it changes no
table's RTP and every tier keeps its place; what it adds is the choice to turn a small win into a story.
The odds are printed on the offer.

**The progressive (U25).** Every Bandit pull adds 2% of its stake to a pot that starts at $5,000 per floor;
the whole grid alike (about 1 in 52,000 pulls) pays its 50x plus the pot. A run sees a few hundred pulls,
so the pot grows by a few hundred dollars and is won almost never: it is there to be looked at, glowing on
the carousel's tower, and it changes the Bandit's RTP by under 0.001.


## 8b. The run of two floors, measured (U26; tools/SimCheck `pace`, `lessons`)

The TS bots ported to C# (`tools/SimCheck/Pace.cs`): they sprint to a table, play each round at its
pace, and always pay the Collector. 100 seeds, 2026-10-16:

| Strategy | Night 1 made | Nights made of 6 | Walks out | Lives lost at tables |
|---|---|---|---|---|
| Easy curve: safe-only (Bandit, Twenty-One / roulette, craps at minimum) | 3% | 0.1 | 0% | 0 |
| Easy curve: mid rotation (Hi-Lo bursts; the Wheel / Peg Drop while marked) | 68% | 3.4 | 39% | 0 |
| Easy curve: mid + high bursts when behind | 68% | 3.4 | 42% | 0.21 |
| House rules: mid rotation | 68% | 1.1 | 0% | 0 |
| House rules: mid + high bursts | 68% | 1.3 | 0% | 0.39 |

Floor 2 alone (nights 4-6, from a $1,500 bank, stakes ×2): safe-only (roulette red, craps) 0% of night 4;
the floor-2 rotation (Hi-Lo, Peg Drop) 56%, walking out 70%; Crash alone at an auto cash-out 1.5x-5x makes
night 4 25-31% (its rounds are long and the stake is a third of the bank) and at 10x 10%, losing 0.32 lives a
run to the crash past 5x. The lessons checked on every run (SimCheck `lessons`): safe-only makes night 1
(and floor 2's night 4) at most 15%; the mid rotation makes night 1 at least 50%; on the easy curve it walks
out at least 25%, on house rules at most 10%; the floor-2 rotation makes night 4 at least 40%; riding Crash
to 10x costs more lives than cashing at 2x.
