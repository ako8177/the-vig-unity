# Ideas — The Vig (Unity)

The owner asked (2026-10-16): "remember its a gambling game. what makes gambling addictive. what makes
gambling fun. make the game grabbing and engaging. i want to see a lot of ideas from you."

This is a game with no real money in it, ever (CLAUDE.md). That's what lets it use the hooks that make
gambling grab people as *game design*: in a casino they extract money; here they're the fun, and
the game is honest about its odds while it uses them.

## What makes gambling grab people, and how The Vig uses each one

| The hook | What it is | How The Vig uses it (honestly) |
|---|---|---|
| **Variable rewards** | wins come at unpredictable times and sizes; the brain chases the next one | every table pays on a schedule you can't predict; win sizes scale up (win → big win → jackpot, with escalating sound and light) |
| **Near misses** | almost winning feels like progress and keeps you playing | the Bandit says "SO CLOSE" only when two sevens really landed on a line (built). Real slots fake these; we never do |
| **Illusion of control** | throwing the dice yourself, picking numbers, timing a cash-out, feels like skill | you throw the craps and Hi-Lo dice (real physics), you call hi/lo, red/black, pick the roulette bet, time Crash, trace wires. The odds still print exactly |
| **Losses disguised as wins** | a slot pays back less than the bet with fanfare | we don't: celebrations scale with *net* winnings, and a 0.4x peg slot is called what it is |
| **Chasing losses / sunk cost** | "one more to win it back" | Red Rover makes the life-risking call pay 4x: the chase is an explicit, priced decision. Let It Ride is the same itch, fair (EV 1.0) |
| **Escalation** | stakes and tension rise over a session | the quota rises every night, floors double the stakes, the Collector waits at 0:00, the lights drop |
| **Sensory feedback** | lights, chimes, coins, the room reacting | neon, bloom, chaser bulbs, coin cascades, the crowd cheering your big wins and groaning your big losses, the bank rolling up (built) |
| **Social proof** | other people winning makes you want to play | the crowd plays the slots and tables around you and reacts to your table (people pass); a progressive pot glowing on the carousel |
| **Streaks and momentum** | "I'm hot" | Hot Hand (+10% a win in a row), streak banners "ON A ROLL / ON FIRE" (built) |
| **Time distortion** | casinos hide clocks | the opposite: the clock is huge and the night ends. Pressure comes from the clock, not from losing track of it |
| **Collections and unlocks** | the next charm, the next floor | 15 charms, a nightly catalogue to draft from, charms unlocked by killing Collectors, a second floor with new games |
| **Dread** | risk you can feel | a crash past 5x costs a life; Red Line's third loss; snake eyes; the Circuit's levers; every one printed before you commit |

## Built in this pass (2026-10-16)

- Clickable tables: chips, your bet, the table's moves as buttons, the odds card always on screen.
- Let It Ride: double or nothing on any win's profit, up to 3 times.
- The progressive pot on the Bandit, glowing on the carousel's tower.
- Real dice: physics throws that land on the sim's faces; you throw them.
- Crash: a live, rising multiplier with a life on the line past 5x.
- Roulette with a real ball, Craps with a point, Peg Drop with a ball down the pegs.
- A nightly charm catalogue with a paid reroll; 10 new charms with real synergies.
- Sound: wins that escalate, coins, the crowd, the 0:00 bell, the lounge loop.
- Streak banners, honest near misses, the bank counting up.

## Next: the best ideas, roughly in order of fun per hour of work

**At the tables**
1. **Side bets** (each with its odds printed): Twenty-One "Perfect Pairs" (a pair in your first two
   cards pays 6x-25x), Hi-Lo "call the exact number" (5x), Roulette splits and corners.
2. **NPC players at your table** who play their own hands from the same shoe (Twenty-One) and react to the
   dealer's cards: the table feels alive and card counting gets real. Needs the sim to deal them.
3. **High-roller rooms**: a velvet rope that opens once you've wagered $X tonight, with a bigger version of
   a table (Hi-Lo at 3x pays, double the mark).
4. **The Bandit's bonus round**: three scatter stars start 5 free pulls at 2x (with its derivation).
5. **A roulette wheel you can push**: hold the button to spin harder (cosmetic: the result is the sim's).
6. **Dice you pick up**: drag and flick to throw (cosmetic power and direction: the faces are the sim's).

**Between nights**
7. **Challenges on the computer**: three bounties a night ("win 3 high-table rounds", "ride a win twice",
   "cash Crash past 4x") that pay markers. A reason to play a table you'd skip.
8. **A loan shark at the bar**: borrow $2,000 now, owe $3,000 at 0:00 (or a life). Debt as a second clock.
9. **Comps**: lose five in a row and the house sends a free drink (the casino's real trick, here a
   visible rule with a derivation).
10. **The skill tree** (waiting on the owner since TS DECISIONS #84): spend leftover markers between runs.

**The Collector**
11. **Each Collector gets his own game** (planned in the TS data): the Auditor plays Red Line's cards against
    you, the Receiver plays a dice duel.
12. **Talking back**: dialogue choices at 0:00 that change his odds slightly ("I'll double the quota if you
    give me the first pull").

**Floors and the run**
13. **Floor 3**: case opening (with rarity odds printed), a big-six wheel of fortune, baccarat.
14. **Run modifiers on the title screen**: "high roller" (stakes x3, quota x2), "blackout" (odds hidden,
    markers x2: the one sanctioned exception to printed odds, and it says so).
15. **Daily seed**: everyone plays the same night; your bank is the score.

**Feel**
16. **Chips on the felt**: your bet as a real stack of 3D chips, pushed out on the bet, raked in or paid out.
17. **The dealer's hands**: the NPC dealer slides the cards out of the shoe and flips them.
18. **Screen shake and slow motion** on a deadly outcome (snake eyes, the crash past 5x).
19. **The crowd gathers**: a big bet pulls railbirds over to watch; a jackpot brings the room.
20. **Music that follows the night**: the lounge loop thins as 0:00 nears and a low drone takes over.

**Co-op (Milestone 3 of the TS plan; the sim is ready)**
21. **One bank, many hands**: two to four players, host-authoritative, the command log over the network.
22. **Arguments as a mechanic**: the team votes to pay or challenge; a ghost can still trace a wire.
