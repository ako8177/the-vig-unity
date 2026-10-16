<!-- Carried over from the TypeScript game (github.com/ako8177/the-vig, commit 292a1e1) on 2026-10-15. The sim and data in Assets/Vig/Sim implement it exactly (tools/golden). Where the Unity look differs from STYLE.md, ../DECISIONS.md (U2) says how. -->

# The Vig: every game, how it works (2026-10-15)

A reference for planning charms and skill-tree unlocks. The numbers are floor 1's. Every bet is
yours to pick between the table's minimum and maximum (deeper floors will raise both).
"Returns" is the average paid back per $1 bet (under $1 loses money over time).

## The casino floor: low risk (green). Losing costs only the bet.

**The Bandit** (slot machine). $50–$250 a pull, standard $100.
A 3×3 grid; each of the 9 cells lands on one stop of a 10-stop strip (one 7, two ★, three BAR,
four blanks). Every pattern that hits pays, added together. On each of the 8 lines (3 rows, 3
columns, 2 diagonals): 7 7 7 pays 10×, three ★ 3×, three BAR 1×, any three symbols 0.3×. Four
matching corners pay 3×. The whole grid alike pays 50× (the jackpot, about 1 in 50,000).
Wins 26% of pulls. Returns $0.95. A pull takes about 2.5 s.

**Twenty-One** (blackjack). $50–$400 a hand, standard $200.
A real 6-deck shoe, reshuffled when a quarter is left (the screen shows how many cards remain).
You and the dealer get two cards; one of his is face down. Hit, stand, double (any first two
cards, after a split too) or split a pair once (split aces get one card each). The dealer hits
soft 17 and checks for blackjack first. A win pays 1:1, a natural 3:2, a tie pushes.
Wins 43% of hands, pushes 9%. Returns $0.99 with perfect play.

## The lounge: mid risk (yellow). Losing costs the bet and a mark.

**Hi-Lo Dice**. $100–$500 a roll, standard $200.
One die. Call HI (4–6) or LO (1–3): an even chance. A win pays 2.4×. A loss also gives **you** a
mark: −5% on your Hi-Lo payouts per stack, up to 10 stacks, lasting 30 s after your last loss.
At 4 stacks Hi-Lo stops paying, so you rotate away. Returns $1.20 unmarked.

**The Wheel of Marks**. $100–$600 a spin, standard $300.
25 colored slots: 8 red pay 1.5×, 3 purple 2.5×, 1 gold 6×, 7 black give half your bet back,
6 mark slots pay nothing and give **the whole team** a mark: Cold Deck (−10% all payouts, 45 s),
Sticky Floor (−30% walk speed, 45 s), Blackout (odds hidden, 45 s) or Tax (5% of the bank, at once).
Ahead on 48% of spins. Returns $1.16.

## The back room: high risk (red). Losing can cost a life.

**Red Line**. $250–$1,250 a call, standard $500.
Call RED or BLACK; one card from a fresh deck: an even chance. A win pays 2.5×. **Three losses in
a row cost a life** (the streak is yours and survives standing up; a win resets it).
Returns $1.25. A six-call burst has about a 31% chance of costing a life.

**Dead Man's Hand**. $500–$2,500 a roll, standard $1,000.
Two dice. Any double or a total of 10+ wins 6× (9 of 36). **Snake eyes (1–1) costs a life**
(1 of 36). Everything else loses the bet. Returns $1.50.

High-table wins also pay markers (Red Line +1, Dead Man's +2).

## The Collector (0:00)

At 0:00 you're pulled to his table. Short of the quota: he takes a life per $2,000 short, and
the whole bank. Made it: **pay him**, or **challenge him**.

**The Circuit** (his game, the lever duel). The table turns over: 10 levers, 3 wired live to a
battery, the wires too tangled to follow. You and he take turns pulling one; you go first; he
picks at random. A dead lever pays you $300 and a marker. A live one shocks whoever pulled it.
**His second shock kills him**: you keep the quota (it never leaves your bank), spin the Boon
wheel, and the next night a new Collector sits down. **Your second shock**: you lose a life, pay
the quota, and spin the Bane wheel. It's a coin flip (51%); the Insulated Kit makes it 73%.

**The Boon wheel** (one of six): everyone +1 life · +$1,000 · +3 markers · a random charm ·
next quota −20% · all marks cleared and immune next night.

**The Bane wheel** (one of six, lasting the next night): Cold Deck all night · a living player
loses a life now · blood tables kill on two losses · Hi-Lo marks last 60 s · soul buyback 75%
(solo: next bank $500 lower) · the Circuit has 4 live levers.

**The Collectors.** The Collector, the Auditor, the Receiver (one model for now). Each will get
his own signature game. The first time you kill one, his charm is unlocked for every future run
(the Collector's: the Insulated Kit). A run's ending depends on how many you killed: none, PAID
IN FULL; one or two, AN EMPTY CHAIR; three, THE HOUSE IS YOURS.

## The bar (cash from the bank)

Draft Beer $40: low tables pay +6% for 60 s. Dry Martini $120: mid +4% for 60 s. Double Whiskey
$200: high +3% for 60 s. The Collector's Own $500: every table +5% for 150 s. Each also makes you
drunk (the room swims, your walk wanders).

## The house (before each night)

The computer sells charms for markers; the order arrives in the mailbox out front 6 s later; you
carry it in; the car takes you to the casino. A run starts with 6 markers.

## Currencies

The bank ($: bets, the quota, drinks) · lives (3 to start, up to 5) · markers (charms; from
overshooting the quota by $500s, high-table wins, dead levers) · souls (co-op buybacks).

## Charms today (markers)

Lucky Fuse 3: win a duel after taking a shock in it, +1 life. Overclock 4: survive a shock and
your stakes and wins are ×2 for the next night's first 60 s. Lead Apron 3: the first life you
would lose each night costs nothing. Marker Magnet 3: double markers from high tables and dead
levers. Insulated Kit 4 (locked until you kill the Collector): two free wire traces per duel;
Sticky Floor can't slow you.

## Skill points: ways to earn them (pick one or mix)

1. **Per night survived** (1) and **per Collector killed** (2): steady, rewards bold play.
2. **From the quota you overshoot**: 1 point per $2,000 over, across a run. Rewards big nights.
3. **Milestones**: first jackpot, first kill, first night at 1 life, each ending seen.
4. **Spending markers left at the end of a run**: 3 unspent markers become 1 point.
