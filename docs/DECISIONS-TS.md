<!-- The TypeScript game's decision log, frozen at commit 292a1e1 (2026-10-15). Derivation strings in Assets/Vig/Sim/Data cite these numbers ("DECISIONS #85"). New decisions go in ../DECISIONS.md as U1, U2, ... -->

# Decisions — The Vig

Every call and why, newest at the bottom. Format: #N date, decision, why, what it rules out.

- #1 2026-10-12: The tier rule: RTP rises with the tier and the price of losing rises from money to marks to lives; low tables are RTP < 1. Why: PLAN pillar 1 (safety is a slow loss); rules out any safe table that can make quota alone.
- #2 2026-10-12: The sim is command-driven and seeded from the first commit; co-op (Trystero, WebRTC, no server) is Milestone 3. Why: GitHub Pages has no server; a command log makes co-op a transport problem. Rules out input-driven sim code.
- #3 2026-10-12: Four currencies: bank, markers, lives, souls. Marks and boons are statuses. A fifth needs an entry here.
- #4 2026-10-12: Odds are always printed on every table before a bet. Why: pillar 5. Rules out hidden house edges.
- #5 2026-10-12: No real-money anything, ever. Not negotiable.
- #6 2026-10-12: First person, 640x360, bloom before the pixel pass. Why: THREEJS-NOTES; this game earns bloom (neon).
- #7 2026-10-12: Palette and neon treatment: the Milestone 0 test ran on three casts (#21); the owner picked **TanoPal 48** (#29). STYLE.md written.

## Owner's answers before Milestone 0 (2026-10-12)

Asked at the start of the M0 session; the owner chose to go straight into M0 and fold these into DESIGN.md at the start of Milestone 1 (HANDOFF.md, first task). Until then DESIGN.md still shows the old wording; these entries win.

- #8 2026-10-12: **A blood-table "death" costs one life; you become a ghost only at 0 lives.** Red Line's third loss in a row and Dead Man's 1-1 each take a life. Why: with "death = instant ghost" and "a solo ghost ends the run" (ROADMAP M1), DESIGN.md §8 pace test 3 can't pass (dying on ≥ 8/20 seeds caps making night 1 at 12/20 < 14/20), and a single Red Line burst ends a solo run about a third of the time. Rules out instant-ghost blood tables. DESIGN.md §3, §4 and §8 test 3 to be rewritten for it.
- #9 2026-10-12: **Overclock's ×2 window carries into the first 60 s of the next night, and Marker Magnet joins the slice as a fourth charm.** Why: all three slice charms fire only inside the Circuit, which happens only at 0:00 and only if you gamble, so during the night's six minutes of tables they do nothing, and Overclock's 60 s opened after the tables had closed (pillar 3, "charms cross the room"). Marker Magnet runs the other way, from the tables to the Circuit (high-table wins give double markers; Circuit inspections cost a marker instead of $100), and §6 already prices it. DESIGN.md §5 to list it with a price and derivation.
- #10 2026-10-12: **After paying the quota, the bank carries over with a $1,000 floor.** Whatever is left starts the next night; under $1,000 it is raised to $1,000; the +$500 upgrade adds on top. Why: overshooting pays off in money as well as markers, and a team that paid exactly the quota can still place bets. Rules out a reset to $1,000 and a bare carry-over. DESIGN.md §2 to say so, with nights 2 and 3 re-derived.
- #11 2026-10-12: Small fixes Claude proposed and the owner didn't object to, to make at the start of M1 with the rest: PLAN.md's table numbers synced to DESIGN.md's (Hi-Lo 2.4x / RTP 1.20, Red Line 2.5x / 1.25, marks -5% for 30 s); Red Line's three-losses risk over 6 rounds is 31%, not 37% (exact: 1 - 44/64; two bursts about 53%, not 60%; the lesson holds); Dead Man's Hand's 1-1 check before the win roll makes its real RTP 35/36 x 1.50 = 1.458, which would fail the 0.5% RTP test, so its dice get a defined mechanic whose p and RTP are exact; Red Line's loss streak is per player and survives standing up (otherwise stand after two losses and the death never comes).

## Milestone 0 (2026-10-12)

- #12 2026-10-12: **three.js pinned at 0.186.1** (@types/three 0.186.0), the latest release on npm on 2026-10-12 (published 2026-09-24). The other tools are pinned to the exact versions Sprocket & Sprout runs on this PC: vite 5.4.10, vitest 2.1.4, typescript 5.6.3, playwright 1.48.2, electron 33.4.11, electron-builder 25.1.8, jsfxr 1.4.1. Why: proven together on this machine; newer majors (vite 8, vitest 5, TypeScript 7) bring nothing M0 needs. Exact pins, no carets.
- #13 2026-10-12: The e2e harness drives the **installed Chrome** (`channel: 'chrome'`, Chrome 154) with `--use-angle=swiftshader --enable-unsafe-swiftshader --ignore-gpu-blocklist`; WebGL2 comes up as "ANGLE (Google, Vulkan 1.3.0 (SwiftShader Device (Subzero)))". Why: Windows refuses to run Playwright's bundled Chromium on this PC ("Permission denied"). From Claude's tool sandbox the launch also needs the sandbox off (`spawn UNKNOWN` otherwise).
- #14 2026-10-12: **Movement and collision live in the sim** (`src/sim/systems/movement.ts`), not in render3d: the client sends `move` with a world-space direction in thousandths and its yaw in milliradians; the sim does no trigonometry (only + - * / sqrt, which are exact in IEEE doubles), so peers integrate identical numbers in co-op. Mouse look is client-side and never waits on the sim. Why: Sticky Floor (a team mark) changes walking speed, and other players must see where you are; trig functions can differ between JS engines. A `move` goes out only when the intent changes.
- #15 2026-10-12: **Selective bloom**: only meshes on the GLOW layer (neon strips, signs, the Collector's eyes, the cage strip) feed UnrealBloomPass; the rest of the scene is drawn black in that pass so it still occludes. Strength 0.3, radius 0.05. Why: a luminance threshold can't work with these colors: pure red and violet neon have lower luminance (0.25, 0.34) than white panel text, so a threshold either skipped the red tables or haloed the odds card. Rules out threshold bloom.
- #16 2026-10-12: **Neon is each tier's base color scaled until its brightest channel is exactly 1.0**, never past it. Why: at 3.2x the cores clipped to white (the violet ring rendered white); the hue is the message. The glow comes from the bloom, not from overdriving the color.
- #17 2026-10-12: **The pixel pass** snaps the bloomed, sRGB-encoded 640x360 frame to the active palette through a 64³ lookup table (a 4096x64 texture built at startup with a "redmean" distance), after a 4x4 Bayer dither of ±1.8% on the low-res grid, then draws into the letterbox at a whole-number scale with texel-center sampling. Why: one texture read a pixel is cheap under SwiftShader (30-42 fps measured); a 64-color loop a pixel would not be.
- #18 2026-10-12: **The room's layout**: low tables nearest the door toward the center (Bandit -4, 2.5; Twenty-One 4, 2.5), mid out by the side walls (Hi-Lo -8, -2; Wheel 8, -2), high at the back flanking the Collector (Red Line -4.5, -5.5; Dead Man's 4.5, -5.5), the Collector at the center, the bar on the west wall, the cage on the east. All seven signs are inside the 91° view from the door and none hides another. Why: the first layout put the low tables 72° to the side, out of view; walking in, the whole question should be in front of you (DESIGN.md §9 q1), and the deeper you walk the worse it costs. Signs are 34 texels a meter (a 96 px sign is 2.8 m wide: about 1:1 on screen at 10 m); the near (low) signs hang at 3.4 m so they clear the mid signs behind them.
- #19 2026-10-12: **The seat prompt carries the table's odds line** in its tier color ("MID  WIN 50%  PAYS 2.4x  LOSE $200 + 1 MARK"), built from `src/data/tables.ts`. Why: standing at a table, its 320x180 panel is minified to unreadable pixels; pillar 5 wants tier, odds and loss cost in one glance before you commit, and sitting shouldn't be required to read them.
- #20 2026-10-12: **Glyphs**: mark = a target (you are marked), marker = a diamond chip, soul = a ghost; the coin keeps its square hole; skull for the blood tables. Same shapes in the font (HUD, panels) and on the signs. Why: the first mark and marker glyphs both read as a "0" at 7 px and the soul read as a spade.
- #21 2026-10-12: **The neon-and-palette test** rendered three casts (`e2e/out/palette/`): Resurrect 64 (warm; but under bloom its low-tier green drifts to lime and reads as amber from 10 m), TanoPal 48 (pure neon green; but its amber f8a004 and red f5320a both read orange from 10 m, and the room turns cold grey with a purple carpet), and **Resurrect 64 with the low tier cast teal** (30e1b9; teal blooms toward cyan, never yellow; all four tiers distinct at 10 m; souls move to pale bone, markers keep blue). Claude recommends the teal cast; the owner picks (#7). The palette is a *cast*: UI and textures name roles (`C.low`, `C.felt`), the cast says which palette color plays each, so a pick is a one-line change.
- #22 2026-10-12: **Art is made with the pixel-art-studio skill** (owner's instruction, this session): `pixel-art/vig-room/build.py` draws every texture, sign and loss icon pixel by pixel and renders each palette cast into `public/tex/<cast>/`; it reads the casts from `src/data/palette.ts` and the glyphs from `src/ui/font.ts`, so the signs letter in the HUD's font. PixelLab spend: 0. Blender-built models replace the placeholder geometry in Milestone 1 step 10.
- #23 2026-10-12: The ceiling takes no light (a basic material at half brightness). Why: the table lamps hang 1 m under it and their hot spots bloomed an olive haze over the signs.
- #24 2026-10-12: **Static meshes are merged per material** at build time (one draw each); screens stay separate for their panels; materials are shared by (texture, tint). Draw calls 107 → 31 (budget 80).
- #25 2026-10-12: The seated camera sits 1.25 m out along the dealer screen's normal; the screen is 1.28 x 0.72 m, so the 320x180 panel lands at about 1:1 in the 640x360 view. The table's side neon strips were removed: from the seat they ran at your eye and bloomed over the panel.
- #26 2026-10-12: Dev server on port 5180 (strict), so it never collides with another project's 5173. `.claude/launch.json` has it as "vig".
- #27 2026-10-12: M0 seats you only at Hi-Lo (`OPEN_TABLES` in `src/sim/systems/tables.ts`), the one table with a panel. Every table's sign, tier color and loss glyph are already there.
- #28 2026-10-12: **Derivative flag (CLAUDE.md standing instruction): the Collector's glowing eyes above a high-backed chair across the table resemble Buckshot Roulette's Dealer** (a figure seated opposite you in the dark, eyes lit). THREEJS-NOTES rule 5 names "the Collector's eyes" among the things that bloom, so M0 built them (two violet bars above his chair, `src/render3d/room.ts`); the flag is open for the owner. Options: keep the eyes but drop the across-the-table framing in Milestone 5's Collector (he stands, circles, deals from the center, the team spread round the ring, which PLAN's Circuit already implies), or drop the eyes and let his mask catch the violet light instead.
- #29 2026-10-12: **The owner picked TanoPal 48** over Claude's recommendation (Resurrect 64 with a teal low tier), knowing its amber and red both read orange from 10 m. Claude then cast the **mid tier yellow** (`f0ff00`, hi `ffff8e`, lo `f8a004`) instead of TanoPal's amber `f8a004`, and re-ran the 10 m test: green, yellow, red-orange and violet are all distinct. Why: pillar 5 (pick a table by its sign alone); a cast change inside the picked palette, reversible in one line of `src/data/palette.ts`. The Resurrect 64 casts and their textures were deleted; STYLE.md records the cast. Rules out amber as the mid neon in this palette.
- #30 2026-10-12: **#28 resolved by the owner: keep the Collector's eyes, reframe him in Milestone 5**: he does not sit across the table from you; he stands and deals the Circuit from the center of the ring, the team around it. Why: the eyes are specified (THREEJS-NOTES rule 5); the seated-opposite framing is what read as Buckshot Roulette's Dealer. The M0 placeholder chair stays until M5.
- #31 2026-10-12: **Table felt is charcoal** (`303232`), not green; the odds card's RETURN line is white; the HUD's bank bar is gold (bone when the quota is met). Why: in TanoPal, felt green under a table lamp snapped to the low tier's neon green on every table, and the UI used tier hues for non-tier meanings; STYLE.md: a tier's hue belongs to that tier only.

## Milestone 1 (2026-10-12, new owner: ako8177)

The repo moved from ycgNem to ako8177; the new owner asked for a playtest critic agent and a genuinely playable slice. A user-level `indie-dev-critic` agent (in `~/.claude/agents/`, so it serves every project) plays the build, scores playability and lists bugs with repro steps; its findings are fixed and re-checked each pass.

- #32 2026-10-12: **Twenty-One's rules**: an endless shoe (every card an independent draw), hit or stand only, the dealer hits soft 17, a natural pays 3:2, ties push. Return with the best hit/stand play is **0.9736**, computed exactly by dynamic programming (`src/sim/tables/twentyone.ts`); the table prints 0.974. Why: DESIGN.md said "0.97"; these are the common rules that land there, and an endless shoe makes the odds exact and the sim stateless between hands. Standing up (or 0:00) mid-hand stands on it.
- #33 2026-10-12: **Two commands beyond CLAUDE.md's list**: `fortune` (spin the Collector's Boon/Bane wheel) and `ready` (done reading the night's summary). Why: both moments change state and must be in the command log for co-op and replays; `spin` already belongs to the tables system.
- #34 2026-10-12: **A bought-back soul returns with one life.** Why: DESIGN.md §4 didn't say; one life keeps buyback a rescue, not a refill.
- #35 2026-10-12: **Screen scaling: whole numbers when they fill ≥ 85% of the window, else fit exactly** (one scale on both axes; never out of aspect). Settings: AUTO (default), PIXEL-PERFECT (STYLE.md's strict rule), FILL WINDOW. Why: the critic's F1: a 1366×768 laptop got a 640×360 picture (26% of the window) and small windows squashed. Amends STYLE.md "never a fractional scale" to "never by default when a whole number nearly fits".
- #36 2026-10-12: The glow (bloom) pass renders at half size and finds its dark meshes once, not every frame. Why: the critic's F8 measured it at a third of the frame under SwiftShader.
- #37 2026-10-12: **Red card suits are drawn in `blood`** (STYLE.md reserved it for deaths). Why: TanoPal's other reds are the high tier's; a tier hue may not mean anything else.
- #38 2026-10-12: **The Bandit's pay table is 12× / 7× / 6× / 3× / 1×** (was 75× / 25× / 5× / 2× / 1×), RTP still exactly 0.950. Why: three stars at 25× ($2,500) hit in about a third of nights of pulling, and the safe-only bot made night 1 on 9% of nights; pillar 1 says a low table bleeds. Top line now $1,200.
- #39 2026-10-12: **Hi-Lo marks tax Hi-Lo payouts only** (DESIGN.md said "mid-table payouts"). Why: measured, a marked player's Wheel returned ≈ $1.00 a dollar, so the Hi-Lo/Wheel rotation earned +$590 a night where DESIGN.md's own arithmetic counted +$600 a minute; with the change it earns ≈ +$2,000 a night, and "go spin the Wheel while your marks clear" is the rotation the design describes. Rules out marks that close both mid tables.
- #40 2026-10-12: **The start bank is $1,500** (was $1,000). Why: from $1,000, with $300 Wheel spins and $500 Red Line hands, 42% of mid rotations and 48% of Red Line nights went broke before the drift could show (100 seeds); at $1,500 it is 17% and 21%, and safe-only still fails. The quota curve is untouched (it is the owner's to change).
- #41 2026-10-12: **The Collector uses what he sees only to avoid revealed overloads** (DESIGN.md §6 as written). Tried and rejected: also taking the safes the team reveals; his turn falls between your inspect and your next move, so every safe you found was his and the best play became never inspecting (0/20 wins for any inspecting line). The rule is a field (`sees`) in `src/data/circuit.ts` for later floors.
- #42 2026-10-12: **Pace-test thresholds revised from the first measurements** (DESIGN.md §8 has old → new and the latest numbers): safe-only fails ≥ 17/20 and loses on average (was ≥ 19/20: luck can carry a low table for one night, expectation can't); mid rotation ≥ 10/20 (was 12); Red Line ≥ 13/20 and a life ≥ 5/20, with bursts waiting for a $2,000 bank (was 14 and 8); the Circuit measured on 100 games, inspecting once 30–65%, and the Lead Apron flip measured in lives (one game is near a coin flip); the ghost test ≥ 6/20 as a lower bound (the bot doesn't play the revived player).
- #43 2026-10-12: **At 0:00 shortfall takes the whole bank as well as the lives, and the quota still rises ×1.5.** Why: DESIGN.md §2 said "then the night is over" without saying what happens to the money; the curve is per night.
- #44 2026-10-12: **Marks and Overclock windows count table time only**: they pause at the Collector's table. Lives cap at 5. The cage is used from a stand point at its window (1.8 m); it is open while the clock runs, and the clock keeps running while you shop.
- #45 2026-10-12: The Bandit's screen is the glass on its cabinet front (it was inside the cabinet), and its neon moved to the crown and the kick; the Collector's screen lies on his felt at 0.6 rad so his eyes stay in view above it. Why: seen in the screenshots: the seat put the camera inside the cabinet, and the cabinet neon bloomed across the glass (#25's lesson).
- #46 2026-10-12: **Upgrades, settings, the run record and the lessons seen are saved in localStorage** (`vig.meta`, `vig.settings`), and the game plays without storage. Lesson cards show once per browser. The blood-table card shows the first time you sit at a high table (ROADMAP's "first blood-table warning", before the risk rather than after).
- #47 2026-10-12: The e2e helper drives Chrome if installed, else Edge, else Playwright's Chromium (this PC has no Chrome). `npm run serve:e2e` serves on 5181 without hot reload so a sweep survives saves (the critic's F11).
- #48 2026-10-12: **Blender models (ROADMAP M1 step 10) are deferred**: no Blender MCP on this machine. The placeholder geometry stays; ROADMAP allows it ("placeholder models are fine").

### After the Milestone 1 reviews (2026-10-12)

Two reviews of commit e2d040b: the playtest critic (`indie-dev-critic`: playability 5.5/10, no crashes or soft-locks, every payout matches its printed odds, 14 findings) and the project's `indie-critic` (design, against DESIGN.md §9). What changed, and why:

- #49 2026-10-12: **"Call it": a fifth new command, `callit`**, ends the night now from the Collector's table (solo; co-op will need the team's agreement). Why: the critic went broke in the first 10 seconds and had nothing to do for 5:51. The night's length is unchanged for anyone who plays it.
- #50 2026-10-12: **The last night's gamble**: a won Circuit takes a free permanent upgrade from "the house's ledger"; a lost one costs only what the Circuit cost (no Bane: there's no tomorrow to touch). Why: on night 3 every Boon and Bane touched a night that doesn't exist, and the "-1 LIFE" Bane could end a run whose quota was paid; gambling was pure downside.
- #51 2026-10-12: **Solo, the Dear Souls Bane docks the next night's bank $500** (with no soul to price, it was a blank on 1/6 of the wheel).
- #52 2026-10-12: **The Circuit is about positions**: the Collector takes the lowest-numbered circuit he trusts (it was "left alone longest"), a revealed overload is dead to the team (route him into it instead), the panel is 10 circuits (was 7: with this rule, 7 gave the team 37%; 10 gives 48%), and the Bane's "3 overloads" also gives him 3 fuses (with 2 he could always be trapped: 91%). Why: the design review flagged the Circuit as reading like Buckshot Roulette (hidden good/bad items with a known count, alternating turns, a paid peek, a life per bad one); a predictable opponent and positional traps are the part that game doesn't have. **Derivative flag stays open for the owner** (below).
- #53 2026-10-12: **Lead Apron: the first life you'd lose each night (anywhere) costs nothing. Lucky Fuse: win a Circuit after surviving an overload in it, +1 life.** Why: measured over 1,000 Circuits, Lucky Fuse made every overload free and Lead Apron + Lucky Fuse returned +0.6 lives a Circuit (+0.9 hunting overloads); "pay or gamble" was decided by holding them. Lead Apron now crosses to the floor (pillar 3).
- #54 2026-10-12: **Overclock doubles the stake, not the return.** Why: ×2 on the whole payout made the Bandit return $1.90 a dollar and Hi-Lo +$4,800 a minute for 60 s (DESIGN said "+$600"); doubling the stake doubles the expected win and keeps every printed RTP true.
- #55 2026-10-12: **A shortfall costs a life per $2,000** (was $1,000). Why: the review's 300-seed runs: 23% of sensible runs died on night 1, six minutes in, before the Collector's phase; at $2,000 it is 6.5% and the quota curve is untouched.
- #56 2026-10-12: **Open for the owner, not changed**: a shortfall on the last night with lives to spare still counts as walking out (the critic's F3: with 3 lives night 3's $6,750 really asks for $2,751+). Whether the stairs open only for those who pay is a win-condition call.
- #57 2026-10-12: **Pace tests run on 200 seeds (the Circuit on 400) and assert rates.** Why: the 20-seed numbers were a lucky draw (80% where 200 seeds say 64%). Test 5's "flip" is restated: under #52 looking at his next pick beats activating blind with or without Lead Apron, and the apron takes any line's life cost to ≈ 0. Test 6 passes only in its weak form: paying still beats gambling (2.09 vs 1.98 vs 1.35); the lever, the lost gamble's +10–40%, is the owner's (§2, §7).
- #58 2026-10-12: **Derivative flags raised by the design review, for the owner** (CLAUDE.md: flag, don't decide silently): the Circuit's frame against Buckshot Roulette (#52 moves it toward positions; the seated, glowing-eyed Collector is #30's open item and the M2 friend test happens before M5), and the word "charms" with an escalating-deadline shop against CloverPit (a rename, e.g. "comps" or "favors", is cheap now).
- #59 2026-10-12: Smaller fixes from the playtest: Bane-aware printed loss costs (Red Line's 2-loss streak, Hi-Lo's 60 s marks); Blackout hides the play areas' payouts too; Red Line prints "6 ROUNDS: 31% TO LOSE A LIFE", Dead Man's "1 IN 36 A ROLL"; the Circuit's rules on its screen every turn and its odds on the choice card; picks clear after a Circuit action; the wheel's result shows only once it lands; the night summary wraps cleanly; a shortfall that ends the run shows its arithmetic; lessons are marked seen when shown and blood/0:00/broke cut the queue; Esc to stand up pauses (no pointer lock without a click); seated toasts yield to banners; off-seat panels refresh once a second.

### The feel pass (2026-10-13, the owner's playtest)

The owner played the live slice: the tables felt like "clicking for odds", the room hummed with static, and wins didn't feel like wins. What changed:

- #60 2026-10-13: **Green felt for the dice: a dark baize tray** on the Hi-Lo and Dead Man's panels (`baize` 005a20, `baizeHi`, `baizeLo` in the cast). The owner's override of STYLE.md's "felt never green" (#31): the trays are 2D and unlit, far darker than the low tier's neon 00e60f, so no lamp can push them toward it. The 3D tables keep charcoal felt.
- #61 2026-10-13: **The room bed (looped brown noise + a 55 Hz hum) is gone**; it read as static. Two original lounge tracks ("Low Lights", "House Edge": swung eighths, brushes, walking bass, Rhodes, vibes) on the existing song player, ducking at 0:00; a distant machine paying out somewhere on the floor every 8–22 s. A MUSIC volume in settings.
- #62 2026-10-13: **Derivative flag (owner's request): "a wheel taking a lot of inspiration from Gamble With Your Friends."** PLAN.md's not-list rules out its machines and UI, so the quality taken is the spectacle (a big wheel everyone watches slow down), not its art or layout: a big-six wheel drawn for this game in the palette (amber mark wedges with the mark glyph, gold chips on paying wedges, brass pegs, a chasing lamp rim, a pointer at the top), its odds down the left. The owner can push further; this note is the line.
- #63 2026-10-13: **Every round is a small show, timed to its sound** (`src/data/feel.ts` holds the beats; panels animate and `src/app/sounds.ts` builds the sounds from it): Hi-Lo's die is rattled, thrown, bounces five times on the baize (a thud each) and settles (1.6 s); Dead Man's two dice land a beat apart (2.2 s); Red Line riffles, deals and flips (2.1 s); the wheel spins three turns with a peg clack per slot (4.2 s); the Bandit's reels scroll and clunk to a stop in turn (1.8 s). Each `resolveS` stays inside its table's round time, so DESIGN.md §3's rates hold (pace tests re-run). **Wins scale**: any win flashes the panel gold and pops the amount with sparkles and a chime; ≥ $700 net is a BIG WIN (banner, coin shower, fanfare); ≥ $4,000 or 7-7-7 is a JACKPOT (screen flash, a long coin shower, a camera shake). Losses flash the rim and thud; a lost life flashes blood with a heartbeat.
- #64 2026-10-13: **Playwright 1.48.2 → 1.63.0** (pinned exactly): Edge updated to 155 and no longer starts with 1.48's `--headless=old`. From Claude's tool sandbox the e2e scripts still need the sandbox off.

### A beautiful first floor (2026-10-13/14, the owner's direction)

The owner set a bigger plan (a house before the casino with a family, cars and a computer shop for
charms delivered by post; more floors unlocking new games and higher limits; separate rooms by
risk; a living crowd; a bar with drinks that make you drunk) and asked to start with a beautiful
first floor. Two standing rules bend to it, at the owner's word: **the milestone order** (this
work is Milestone 4/8-ish content ahead of the M2 gate) and **the not-list** (PLAN.md bars
borrowing Gamble With Your Friends' rooms and CloverPit's charms; the owner wants those as
inspirations: the ideas are taken, never their names, numbers or art).

- #65 2026-10-13: **Floor 1 is three rooms**: THE FLOOR (28 x 20 m, 5 m ceiling: the entrance, the
  low tables, the bar, the cage, banks of slots, a slot carousel round a progressive-jackpot tower,
  roulette and card tables, chandeliers, marble pillars, a roped aisle; an indigo-and-gold carpet,
  cream damask walls, a coffered ceiling), THE LOUNGE through the north-west arch (Hi-Lo, the
  Wheel; violet carpet, booths, a piano, a craps rail, pink coves, sconces) and THE BACK ROOM
  through the north-east arch (Red Line, Dead Man's Hand, the Collector; dark planks, grimy
  plaster, bare bulbs, smoke). The deeper you walk, the worse it costs. New palette roles for the
  floor (cream, indigo, pink, teal: none a tier's hue). Replaces DECISIONS #18's one-room layout.
- #66 2026-10-13: **A crowd of 52, presentation only** (never in the sim, so co-op needs nothing):
  slot players, table players and dealers, a bartender, drinkers, smokers (embers glow, smoke
  drifts), a pianist, walkers pacing clear lines, and railbirds by each table who turn to watch
  when you sit there, cheer your wins (a big one brings in everyone within 9 m) and groan at big
  losses. People who stand still are colliders (data/floors.ts); walkers aren't. One instanced draw.
- #67 2026-10-13: **The bar sells drinks for cash** (DESIGN.md §10): $40-$500, buzz that stacks to
  180 s and counts down in floor time; the picture swims and doubles, the camera sways, your walk
  wanders (input-side: the sim sees ordinary moves); a MILD setting softens it. A new command,
  `order`. A drink buys nothing but the feeling in the slice.
- #68 2026-10-14: **Performance under software WebGL** (THREEJS-NOTES rule 13, >= 15 fps headless):
  the bigger floor first ran at ~10 fps. Fixed by drawing only the rooms you can see (the room
  you're in, plus the one through an arch within 9 m; each room merged per material on its own),
  two point lights per room (lights off in rooms you can't see), merged meshes centered so the
  renderer sorts them front to back, a glow pass that blocks with the rooms' shells only, and
  no per-frame canvas uploads (the Collector's waiting screen redraws once a second, the crowd
  poses at 30 Hz, the slot screens 5 times a second). Now 19-20 fps, 52 draw calls, 23k triangles.
  Lesson recorded: a light-count test is meaningless while shaders recompile (frames drawn then are
  half empty), and a browser pane running the game in parallel halves SwiftShader's numbers.
- #69 2026-10-14: **The glow pass blocks with everything solid**, not the rooms' shells only (the
  owner: "the lights are covering bets... all bright lights are visible through everything"). The
  bloom of a sign or a table's rim neon drew over the panels, the furniture and the people. Each
  room's static opaque meshes are baked into one black blocker drawn only in the glow pass (one or
  two draw calls a room), and the crowd and the roulette disc are drawn black there. See-through
  things (the cage's grille, smoke) still let the glow through. Replaces #68's "shells only".
- #70 2026-10-14: **Twenty-One deals its cards one at a time, each with a sound** (the owner: "blackjack
  still doesnt have card audio"). The deal made no sound at all (no sim event fires when cards are
  dealt) and a hit was a 70 ms blip. Now the panel deals you, dealer, you, dealer (face down), a
  card every 0.14 s (`FEEL.deal.gap`), slides each hit in, turns the hole card over at the stand and
  then deals the dealer's draws, all inside the table's 0.6 s resolve; a snap-and-slap `card` sound
  and a `flip`. The verdict waits for the last card. Presentation only: no sim or number changed.
- #71 2026-10-14: **The crowd is a jointed figure** (the owner: "the npcs could get cleaned up
  better"): hips, torso, a head with eyes and hair that wraps it, upper arms, forearms and hands,
  thighs, shins and shoes (21 boxes, one instanced draw), posed by joint angles so table players
  rest their forearms on the rail, smokers bring a hand to the mouth, walkers bend the back knee.
  Dealers and the bartender wear a dark vest over white sleeves; card players bring a chair; slot
  players sit on their stools. Only people in a drawn room and not behind you are drawn. Measured
  A/B under SwiftShader, interleaved: about 5-10% of the frame (base 10.6-11.6 fps, new 9.8-11.1
  on this PC today; the same base read 21-24 in a faster window, so compare runs side by side).
- #72 2026-10-14: **Mouse look takes raw input and drops spikes** (the owner: "when i look around my
  mouse skips across the screen"). Chromium on Windows sends bogus jumps under pointer lock (the
  first move after locking, and lone steps of hundreds of pixels). The lock now asks for
  `unadjustedMovement` (raw counts, no OS acceleration; falls back where unsupported), and the input
  drops the first move after a lock and any step over 300 px that is also 6x the step before it, so
  a real flick, which ramps up, still turns you. Sensitivity may feel a touch different without
  Windows' acceleration: the settings slider covers it.
- #73 2026-10-14: **Shift sprints at x1.7 (5.1 m/s)** (the owner: "movement is slow as is"). The
  `move` command carries `run`; the pace bots sprint every walk. With walking cheaper, the old
  Red Line bot (burst whenever the bank allowed) no longer beat the mid rotation (64% vs 66%): it
  now bursts only when behind past halfway, and the life-risk check counts nights that burst
  (DESIGN.md §8 test 3, with the reasoning).
- #74 2026-10-14: **The Wheel of Marks pays by color** (the owner: multipliers on the colors, e.g.
  1.5 red, 0.5 black, 2.5 purple; "the wheel in mid risk is the only reliable way to make money").
  8 red 1.5x, 3 purple 2.5x, 1 gold 6x, 7 black 0.5x, 6 marks: RTP 1.16 (was 1.152), ahead 48% (was
  72%), about 1.8x the swing. The mid tier keeps its rule (RTP > 1, losing costs a mark); the owner
  chose to keep the mark slots. The hub reads what the pointer is on.
- #75 2026-10-14: **The Bandit is a 3x3 grid** (the owner: "a 3x3 and in better casinos will go to
  5x5 or 6x6 allowing more combos like cloverpit"). **Derivative flag** (CLAUDE.md): a slot grid
  whose patterns all pay is CloverPit's core; the owner asked for it by name. What we kept is the
  idea; the grid, the patterns (8 lines, corners, the whole grid), the strip and every number are
  derived here from the low-tier rule (RTP 0.9504 enumerated over 4^9 grids; no line over 10x), not
  from CloverPit, and the symbols are the Bandit's own. Grid size is data for later floors (owner
  picked 3x3 here, bigger grids later). The panel drops its rule line to fit the grid.
- #76 2026-10-14: **Twenty-One deals from a real 6-deck shoe, with double and split** (the owner:
  "emulate real card odds"; picked shoe + double + split). Cards without replacement, reshuffle at
  a quarter left, H17 with a peek, double any two (and after a split), split once, split aces one
  card. RTP 0.9921 by DP with every option (was 0.9736); 400k shoe hands measure 0.9929. The data
  test measures blackjack per $1 of the opening bet, the way every house edge is quoted. The shoe
  lives in the sim state (seeded, replayable); the panel shows two hands after a split and the
  shoe's depth.
- #77 2026-10-14: **Drinks give a small edge** (the owner). Each drink multiplies one tier's
  payouts while its buzz runs: beer low +6%, martini mid +4%, whiskey high +3%, the Collector's Own
  every table +5%; sized so a whole buzz spent at that tier about pays the drink back (DESIGN.md
  §10). Replaces #67's "a drink buys nothing but the feeling". It is not a charm (no slot, no
  markers, one system plus the screen swimming), so pillar 3 doesn't bind it.
- #78 2026-10-14: **The crowd plays, and talks** (the owner: "lets get them actually playing, maybe
  some background noise emulating talking when passing by groups of people"). Presentation only:
  every decorative table with players runs its game on a loop (render3d/tableplay.ts): the card
  tables deal, hit, turn the hole card and pay or take; the back room's poker deals hole cards, a
  flop, a turn, a river and pushes the pot; the craps shooter throws two dice the length of the
  table; roulette fills its layout and a ball rides the wheel and drops. Four instanced draws for
  the floor; a table in an unseen room isn't drawn; cards and chips landing within 6 m are heard.
  The static chip columns on the felt are gone (the bets are live now). Craps players stand.
  The talk (app/chatter.ts) is synthesized, no recordings: each cluster of people gets two or
  three voices (a buzz through two vowel formants, syllables and phrases, the odd laugh), muffled,
  heard within 9 m, panned to where they stand, hushed at 0:00. On the SFX volume.
- #79 2026-10-14: **The house** (the owner's plan; the owner picked "the run starts there and you
  come home between nights"). A new sim phase `home` before each night: walk the house and yard
  (its own colliders, far from the casino's coordinates; only one of the two is drawn), order at
  the computer (the `buy` command, now into the post), `pickup` at the mailbox, `drive` at the
  car. The cage stops selling when the run has the house (buybacks stay). A new system `home`
  sits after `cage` in the fixed order (CLAUDE.md updated). The pace bots run without the house
  (`Game({ home: false })`, the default): it takes no clock time, so their arithmetic holds;
  tests/home.test.ts plays the flow. e2e's `enter()` drives through the real command path;
  `enter(true)` stays home. Lesson cards are now capped at 420 px and wrap, so they never cover
  the status column.
- #80 2026-10-14: **The Circuit is the owner's lever duel.** Ten levers, three live, turns, you
  first; he picks at random; two shocks each: yours costs a life and the quota, his kills him and
  you keep the quota. Reroute and inspect are gone (the owner chose "just pull"); tracing returns
  only through the Insulated Kit (#81). The lost gamble's +10-40% surcharge is gone (the owner's
  call). **Derivative flag (CLAUDE.md):** the owner asked for Buckshot Roulette's reveal by name:
  the table top turning over to show the game, turns at a deadly device, the dealer's banter, and
  electrocution as the price are that game's signature scene. DECISIONS #52 had moved the
  Circuit away from it (positions, his visible next pick); the owner's "just pull, he picks at
  random" moves it back. What stays ours: the levers, the knotted wires and battery, the quota
  you keep, the Collectors who die and are replaced, the charm pool they guard. Measured: 51% at
  random, 73% with the Kit; always challenging now beats paying (DESIGN.md §7, an open question).
- #81 2026-10-14: **Collectors die and are replaced; they guard charms; their deaths choose the
  ending.** A new `collector` in the sim state (who sits tonight, dead or not) and `killed`;
  `unlocked` charms come in from meta like upgrades. Roster, endings and lines in
  data/collectors.ts. The Insulated Kit (two free traces a duel; rubber soles: Sticky Floor can't
  slow you, its second system for pillar 3) is the first Collector's. The shop shows locked
  charms and who guards them; the title counts endings seen.
- #82 2026-10-14: **The Collector's scene** (the owner's cinematic). He sits across the table (a
  hatted figure with burning eyes, his color per Collector); his words type out with a
  synthesized murmur; at the choice you lean in to his screen, at his lines you sit back. Challenge
  him and the top lifts and turns over to the levers (render3d/duel.ts): ten levers, a battery
  that sparks, a junction box, the wires knotted. The duel's music (app/tension.ts) replaces the
  lounge's and ends with the duel. A live lever throws an arc: on you, the screen crackles with
  lightning and shakes and you cry out (app/voice.ts, kept moderate); on him, he convulses and cries
  out, and his comment follows. His second shock: he dies mid-sentence and slumps, eyes out. Your
  second: "...still breathing? Curious." and on to the punishment. Lesson cards wait while he
  talks; toasts rise from the middle so his words keep the bottom. The face-down screen keeps a
  plain button readout (the screens sweep, and anyone without the levers).
- #83 2026-10-15: **The owner's fourth round of notes.** The Bandit's reels roll into the result
  they show (each column's reel ends in its three results). The Wheel prints each wedge's
  multiple. Twenty-One deals a card every 0.42 s from a shoe drawn on the felt, the dealer at the
  table dealing each one, and the sim holds the result until the last card is down (a module may
  now say how long its outcome is held: `holdS`). A setting shows a win as all that came back
  (default) or only the gain. The crowd's talk is smoother, muffled babble (it beeped); the
  Collector and the pit boss speak aloud in the browser's own voices (a setting; the murmur is
  the fallback). The choice cards are reworded; the screen goes dark the moment you challenge
  (it showed the old grid). His death is slower, and then **the pit boss** comes through a staff
  door in the back room's far right corner, thanks you for playing and drags the body out; the
  sim holds a won duel 23.5 s for it. Going home fades your vision to black and you wake up at
  home. Hovering a mark, a charm, a drink's edge or the buzz (mouse free) says what it does. The
  booth NPCs sit on their bench (they sat in the table); seated card players sit back from the
  felt (their knees went through it). The beer, martini and whiskey last 60 s each (edges
  unchanged; the $500 drink untouched).
- #84 2026-10-15: **Markers buy charms only** (the owner): the marker upgrades leave the shop;
  ones already owned keep working. Lasting upgrades will come from a skill tree, to be designed
  with the owner (IDEAS.md has the charm and skill-tree proposals).
- #85 2026-10-15: **You pick your bet; charms may touch one system; a run starts with markers**
  (the owner). Every table has floor-1 limits and a step (Bandit $50-250, 21 $50-400, Hi-Lo
  $100-500, Wheel $100-600, Red Line $250-1,250, Dead Man $500-2,500); a deeper floor scales them
  (`FLOOR_STAKES`, floor 1 only so far). The panels have a stake picker; the commands carry the
  amount; no amount means the table's standard stake (the old fixed bet), so the pace bots are
  unchanged. Measured: bots that bet the maximum and change nothing else make night 1 less often
  (mid rotation 54% vs 66%; with Red Line 54% vs 69%): a bigger bet is a bigger swing, not a free
  win. The two-system rule for charms is dropped (CLAUDE.md, the data test). A run starts with 6
  markers (a charm or two, or saved). GAMES.md lists every game for planning charms and skills.

- #86 2026-10-14: **The people are Blender-built** (the owner: "redo all the player models using
  blender"; the Blender MCP is connected now, so #48's deferral ends for people). Nobody has a player
  avatar yet (co-op is M3; the slice draws no hands), so "player models" is read as everyone who
  plays or works the floor: the crowd and the family (`npcs.ts`), the pit boss and the Collector
  (`duel.ts`). `art/people/build_people.py` builds one kit of rigid parts in Blender, each modelled at
  its joint in three.js's frame, and exports `public/models/people.glb` (28 parts, ~150 KB);
  `preview_people.py` stands a few people up in Blender to judge them. Variety comes from the kit: a
  suit coat or a dress (with or without a skirt), a shirt and tie, a bow tie (staff) or a neckline,
  four hairstyles or a fedora. Each part is one tint (the person's colors, as before) times a vertex
  mask (eyes, mouth, tie, cuffs), flat-shaded so the facets catch the lamps. The crowd is one
  `BatchedMesh` (still one draw a pass; people out of sight are hidden per instance instead of
  re-packed); the Collector and the pit boss are meshes on their old pivots, so every animation is
  unchanged. `main.ts` loads the .glb before the app starts. Cost, A/B against HEAD from the door:
  draw calls 71 -> 69, triangles 20.6k -> 36.2k (budget 40k), fps within the machine's noise
  (HEAD 9-11, new 9-18 under SwiftShader). About 700 triangles a person: anything added to a person
  later has to come out of the head or hair first. Not derivative: plain low-poly people.

- #87 2026-10-14: **The game tables and the pieces are Blender-built** (ROADMAP M1 step 10, continuing
  #86). `art/tables/build_tables.py` builds each of the six game tables round its own footprint
  (floors.ts) and exports `public/models/tables.glb`: one object per material (`<table>_<material>`:
  wood, felt, brass, brassLo, woodLo, ink, night) that `room.ts` gives the room's own material, so
  they merge into draws the room already makes (no new draw calls; wood and felt carry UVs in meters
  at the room's densities). The tiers show in the furniture too: the floor's Twenty-One on legs, the
  lounge's tables on pedestals with rounded corners, the back room's on heavy plinths with a brass
  kick; each carries its game's thing in a back corner clear of the screen (a shoe, a dice cup, a
  token stack, a deck, a dice tray). The Bandit's cabinet has cheeks, a bezel and an arch topper; its
  painted face and chasing bulbs stay code like the bank's. **Kept exactly**: every neon rim, screen,
  frame, post and sign. One stool model (seat and post, 40 triangles; the cylinders were 64) seats
  every table, slot and the bar. `art/props/build_props.py` exports `props.glb`: a chip (8 sides,
  edge spots, no bottom: 22 triangles, was 32), a card with clipped corners (12) and a die with pips
  (54, eight at most on the floor), one tint times a vertex mask, instanced as before.
  `art/blender_kit.py` is the shared builder (faces wound by an outward hint, no normal recalc; UVs
  projected in meters); `tests/models.test.ts` checks each table against its footprint (±5 cm), its
  height and ≤ 1.5k triangles with its stool (they are 116-292), and the pieces' budgets. The
  Collector's round table, the decorative tables, the slot bank and the room stay code for now.
  A/B against HEAD (each view, same session, interleaved): draw calls unchanged everywhere; triangles
  door 36.4k -> 35.9k, floor 34.7k -> 34.3k, lounge 19.4k -> 19.6k, back room 19.9k -> 19.8k; seated
  at the Bandit 36.7k -> 36.3k, Twenty-One 34.4k -> 34.2k, Hi-Lo 8.9k -> 9.3k, Wheel 16.6k -> 16.9k,
  Red Line 19.1k -> 19.0k, Dead Man's 10.8k -> 10.9k (the floor's views get cheaper, as #86's budget
  note asked; the lounge pays a few hundred for its pedestals). `npm run perf` interleaved five times:
  HEAD 16.6/15.1/8.2/8.3/8.6 fps, new 15.4/8.2/9.2/8.3/9.5: the machine's swing, not the models. Not
  derivative: plain casino furniture.
