// Marks, charms, upgrades, the Collector's wheels, the Circuit's rules, the bar's drinks, the
// Collectors and their endings, and the tables' timings. Every number names its DESIGN.md section.
using System.Collections.Generic;
using System.Linq;

namespace Vig.Sim.Data
{
    // ------------------------------------------------------------------ marks (DESIGN.md §3)

    public sealed class MarkDef
    {
        public string Id, Name, Text, Derivation;
        /// <summary>hits the whole team</summary>
        public bool Team;
        /// <summary>seconds; 0 = instant (Tax)</summary>
        public double DurS;
    }

    public static class Marks
    {
        public static readonly MarkDef[] All =
        {
            new MarkDef { Id = "hilo", Name = "Marked", Team = false, DurS = 30, Text = "-5% ON YOUR HI-LO PAYOUTS PER STACK", Derivation = "DESIGN.md §3: -5% to that player's mid payouts for 30 s, stacking to 10" },
            new MarkDef { Id = "colddeck", Name = "Cold Deck", Team = true, DurS = 45, Text = "ALL PAYOUTS -10%", Derivation = "DESIGN.md §3: Cold Deck, all payouts -10% for 45 s" },
            new MarkDef { Id = "sticky", Name = "Sticky Floor", Team = true, DurS = 45, Text = "WALK SPEED -30%", Derivation = "DESIGN.md §3: Sticky Floor, move speed -30% for 45 s" },
            new MarkDef { Id = "blackout", Name = "Blackout", Team = true, DurS = 45, Text = "ODDS HIDDEN ON EVERY TABLE", Derivation = "DESIGN.md §3: Blackout, odds hidden on every table for 45 s (and it says so)" },
            new MarkDef { Id = "tax", Name = "Tax", Team = true, DurS = 0, Text = "5% OF THE BANK, TAKEN NOW", Derivation = "DESIGN.md §3: Tax, 5% of the bank, immediately" },
            new MarkDef { Id = "pegged", Name = "Pegged", Team = false, DurS = 30, Text = "-5% ON YOUR PEG DROP PAYOUTS PER STACK", Derivation = "DESIGN.md §11: the center slot, -5% to that player's Peg Drop payouts for 30 s, stacking to 10, like Hi-Lo's" },
        };

        /// <summary>The personal marks that stack and tax their own table (Hi-Lo's, Peg Drop's).</summary>
        public static string StackingMarkOf(string table) => table == "hilo" ? "hilo" : table == "pegdrop" ? "pegged" : null;
        static readonly Dictionary<string, MarkDef> byId = All.ToDictionary(m => m.Id);
        public static MarkDef ById(string id) => byId[id];

        /// <summary>Hi-Lo's mark: -5% a stack, at most 10, on Hi-Lo itself (DECISIONS #39).</summary>
        public const double HiloPerStack = 0.05;
        public const int HiloMaxStacks = 10;
        public static readonly string[] HiloTables = { "hilo" };
        public const string HiloDerivation = "DESIGN.md §3: -5% a stack on your Hi-Lo payouts, stacking to 10";
        /// <summary>Cold Deck's payout factor, Sticky Floor's speed factor, Tax's share (DESIGN.md §3).</summary>
        public const double ColdDeck = 0.9, Sticky = 0.7, Tax = 0.05;
        /// <summary>The Wheel of Marks draws one of these four, equally likely (DESIGN.md §3).</summary>
        public static readonly string[] TeamMarks = { "colddeck", "sticky", "blackout", "tax" };
    }

    // ------------------------------------------------------------------ charms and upgrades (DESIGN.md §5)

    public sealed class CharmDef
    {
        public string Id, Name, Derivation;
        public int Price;
        /// <summary>the systems it touches: table | circuit | collector | lives (one is enough: DECISIONS #85)</summary>
        public string[] Systems;
        public string[] Text;
        /// <summary>locked until you kill this Collector (Collectors index) at his game, in any run</summary>
        public int? Unlock;
    }

    public sealed class UpgradeDef
    {
        public string Id, Name, Text, Derivation;
        public int Price;
    }

    public static class Charms
    {
        public static readonly CharmDef[] All =
        {
            new CharmDef { Id = "luckyfuse", Name = "Lucky Fuse", Price = 3, Systems = new[] { "circuit", "collector", "lives" },
                Text = new[] { "WIN A CIRCUIT AFTER TAKING", "A SHOCK IN IT: +1 LIFE" },
                Derivation = "DESIGN.md §5 (DECISIONS #53, #80): win the Circuit having taken a shock in it, +1 life; price 3" },
            new CharmDef { Id = "overclock", Name = "Overclock", Price = 4, Systems = new[] { "circuit", "table" },
                Text = new[] { "SURVIVE A SHOCK: STAKES AND", "WINS x2, FIRST 60 S OF THE NIGHT" },
                Derivation = "DESIGN.md §5 (DECISIONS #9, #54, #80): stakes x2 (so wins x2, odds unchanged) for the next night's first 60 s after surviving a shock; price 4" },
            new CharmDef { Id = "leadapron", Name = "Lead Apron", Price = 3, Systems = new[] { "table", "circuit", "lives" },
                Text = new[] { "THE FIRST LIFE YOU WOULD LOSE", "EACH NIGHT COSTS NOTHING" },
                Derivation = "DESIGN.md §5 (DECISIONS #53): the first life lost each night (blood table, Circuit or shortfall) costs nothing; price 3" },
            new CharmDef { Id = "magnet", Name = "Marker Magnet", Price = 3, Systems = new[] { "table", "circuit" },
                Text = new[] { "HIGH-TABLE WINS: x2 MARKERS", "DEAD LEVERS IN THE CIRCUIT: x2 MARKERS" },
                Derivation = "DESIGN.md §5 (DECISIONS #9, #80): double markers from high-table wins and from dead levers you pull; price 3" },
            new CharmDef { Id = "insulated", Name = "Insulated Kit", Price = 4, Systems = new[] { "circuit", "table" }, Unlock = 0,
                Text = new[] { "THE CIRCUIT: TRACE 2 WIRES FREE", "RUBBER SOLES: STICKY FLOOR CAN'T SLOW YOU" },
                Derivation = "DESIGN.md §5 (DECISIONS #81): unlocked by killing the first Collector at the Circuit; two free traces a duel (you win 76%, not 50%); the team mark Sticky Floor doesn't slow its wearer; price 4" },
            // ---- the second set (DESIGN.md §12, U16)
            new CharmDef { Id = "hothand", Name = "Hot Hand", Price = 4, Systems = new[] { "table" },
                Text = new[] { "EACH WIN IN A ROW AT A TABLE:", "+10% ON ITS PAYOUTS (UP TO +50%)" },
                Derivation = "DESIGN.md §12: +10% a win in a row at the same table, capped at +50%; a loss or a new table resets; price 4" },
            new CharmDef { Id = "redrover", Name = "Red Rover", Price = 3, Systems = new[] { "table", "lives" },
                Text = new[] { "RED LINE: THE CALL THAT COULD", "COST A LIFE PAYS 4x, NOT 2.5x" },
                Derivation = "DESIGN.md §12: at two losses in a row (one from a life) a Red Line win pays 4x; the danger call becomes the tempting one; price 3" },
            new CharmDef { Id = "counter", Name = "Card Counter", Price = 3, Systems = new[] { "table" },
                Text = new[] { "TWENTY-ONE: SEE THE COUNT;", "A NATURAL PAYS 2:1" },
                Derivation = "DESIGN.md §12: naturals pay 2:1 (RTP about +0.023) and the screen shows the running count; price 3" },
            new CharmDef { Id = "snake", Name = "Snake Charmer", Price = 5, Systems = new[] { "table", "lives" }, Unlock = 1,
                Text = new[] { "DEAD MAN'S HAND: SNAKE EYES", "WINS 6x INSTEAD OF A LIFE" },
                Derivation = "DESIGN.md §12: snake eyes becomes a win (10 of 36 x 6 = RTP 1.67, no life); unlocked by killing the second Collector; price 5" },
            new CharmDef { Id = "interest", Name = "Compound Interest", Price = 4, Systems = new[] { "collector" },
                Text = new[] { "EVERY NEW NIGHT: +10% OF", "THE BANK (UP TO +$600)" },
                Derivation = "DESIGN.md §12: +10% of the bank at the start of each night after the first, at most $600, once for the team; price 4" },
            new CharmDef { Id = "comp", Name = "Comp Card", Price = 2, Systems = new[] { "table" },
                Text = new[] { "DRINKS ARE ON THE HOUSE;", "THEIR EDGE LASTS TWICE AS LONG" },
                Derivation = "DESIGN.md §12: drinks cost $0 and their edge runs 2x its buzz; price 2" },
            new CharmDef { Id = "seven", Name = "Lucky Seven", Price = 3, Systems = new[] { "table" },
                Text = new[] { "THE BANDIT: 7 7 7 PAYS 25x", "(NOT 10x)" },
                Derivation = "DESIGN.md §12: a 7-line pays 25x; the Bandit's RTP goes 0.950 → 1.070 (4^9 grids enumerated); price 3" },
            new CharmDef { Id = "foot", Name = "Rabbit's Foot", Price = 4, Systems = new[] { "table", "lives" },
                Text = new[] { "ONCE A NIGHT: A LOSING ROLL AT", "HI-LO OR DEAD MAN'S IS ROLLED AGAIN" },
                Derivation = "DESIGN.md §12: the first losing Hi-Lo or Dead Man's roll each night is rerolled and the reroll stands; price 4" },
            new CharmDef { Id = "chute", Name = "Parachute", Price = 4, Systems = new[] { "table", "lives" },
                Text = new[] { "CRASH: A CRASH HANDS HALF YOUR", "BET BACK AND NEVER COSTS A LIFE" },
                Derivation = "DESIGN.md §12: a crash returns half the stake and its life cost is gone; price 4" },
            new CharmDef { Id = "thumb", Name = "Croupier's Thumb", Price = 3, Systems = new[] { "table" },
                Text = new[] { "ROULETTE: ONE NUMBER", "PAYS 45x (NOT 36x)" },
                Derivation = "DESIGN.md §12: a straight-up number pays 45x, RTP 45/37 = 1.22 on numbers; price 3" },
        };
        static readonly Dictionary<string, CharmDef> byId = All.ToDictionary(c => c.Id);
        public static CharmDef ById(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;

        /// <summary>Overclock's window (s of table time) and factor (DESIGN.md §5).</summary>
        public const double OverclockS = 60, OverclockFactor = 2;

        /// <summary>Permanent across runs (DESIGN.md §5).</summary>
        public static readonly UpgradeDef[] Upgrades =
        {
            new UpgradeDef { Id = "slot4", Name = "Fourth Slot", Price = 6, Text = "A FOURTH CHARM SLOT", Derivation = "DESIGN.md §5: a fourth charm slot, 6 markers" },
            new UpgradeDef { Id = "bank500", Name = "Float", Price = 4, Text = "+$500 BANK EVERY NIGHT", Derivation = "DESIGN.md §5: +$500 starting bank per night, 4 markers" },
            new UpgradeDef { Id = "hilo20", Name = "Short Memory", Price = 4, Text = "HI-LO MARKS LAST 20 S", Derivation = "DESIGN.md §5: Hi-Lo marks last 20 s, 4 markers" },
        };
        static readonly Dictionary<string, UpgradeDef> upById = Upgrades.ToDictionary(u => u.Id);
        public static UpgradeDef UpgradeById(string id) => id != null && upById.TryGetValue(id, out var u) ? u : null;
    }

    // ------------------------------------------------------------------ the Collector's wheels (DESIGN.md §7)

    public sealed class SlotDef
    {
        public string Id, Label, Text, Derivation;
        public SlotDef(string id, string label, string text, string derivation) { Id = id; Label = label; Text = text; Derivation = derivation; }
    }

    public static class Wheels
    {
        public static readonly SlotDef[] Boons =
        {
            new SlotDef("life", "+1 LIFE", "EVERYONE +1 LIFE", "DESIGN.md §7: Boon, everyone +1 life"),
            new SlotDef("cash", "+$1,000", "+$1,000 TO THE BANK", "DESIGN.md §7: Boon, +$1,000"),
            new SlotDef("markers", "+3 MARKERS", "+3 MARKERS", "DESIGN.md §7: Boon, +3 markers"),
            new SlotDef("charm", "A CHARM", "A RANDOM CHARM (FULL SLOTS: +3 MARKERS)", "DESIGN.md §7: Boon, a random charm"),
            new SlotDef("discount", "QUOTA -20%", "NEXT NIGHT'S QUOTA -20%", "DESIGN.md §7: Boon, next quota -20%"),
            new SlotDef("immune", "CLEAN SLATE", "MARKS CLEARED; NO MARKS NEXT NIGHT", "DESIGN.md §7: Boon, all marks cleared and mark-immune next night"),
        };

        public static readonly SlotDef[] Banes =
        {
            new SlotDef("colddeck", "COLD DECK", "COLD DECK ALL NEXT NIGHT", "DESIGN.md §7: Bane, Cold Deck all night"),
            new SlotDef("bleed", "-1 LIFE", "A LIVING PLAYER LOSES A LIFE NOW", "DESIGN.md §7: Bane, one random living player -1 life now"),
            new SlotDef("twoloss", "TWO LOSSES", "RED LINE TAKES A LIFE ON 2 LOSSES", "DESIGN.md §7: Bane, blood tables kill on two losses"),
            new SlotDef("longmarks", "LONG MARKS", "HI-LO MARKS LAST 60 S", "DESIGN.md §7: Bane, Hi-Lo marks last 60 s"),
            new SlotDef("dearsoul", "DEAR SOULS", "SOUL BUYBACK COSTS 75%", "DESIGN.md §7: Bane, soul buyback 75%; solo, -$500 off the next bank (DECISIONS #51)"),
            new SlotDef("overloads", "+1 LIVE LEVER", "THE CIRCUIT HAS ONE MORE LIVE LEVER", "DESIGN.md §7: Bane, one more live lever than the floor's (floor 1: 4 of 10, you win 42%: DECISIONS #80; floor 2: 5 of 12, 44%)"),
        };

        /// <summary>Solo, Dear Souls has no soul to price: it docks the next night's bank instead (DECISIONS #51).</summary>
        public const string DearSoulsSoloText = "SOLO: THE NEXT BANK STARTS $500 LOWER";
        public const int DearSoulsSoloDock = 500;

        public const int Cash = 1000, Markers = 3, TwoLoss = 2, BigOverloads = 4;
        public const double Discount = 0.2, DearSoul = 0.75, LongMarksS = 60;
        /// <summary>seconds the wheel spins before it lands (presentation pacing, mirrored in the sim)</summary>
        public const double SpinS = 3;
    }

    // ------------------------------------------------------------------ the Circuit (DESIGN.md §6; DECISIONS #80)

    public sealed class CircuitRules
    {
        public int Floor, Levers, Live, PlayerShocks, CollShocks, SafeBank, SafeMarkers, KitTraces;
        /// <summary>how long his turn takes to come, and longer after a shock (presentation pacing in the sim)</summary>
        public double CollectorTurnS, AfterShockS;
        /// <summary>measured by the pace test, printed at the choice</summary>
        public double OddsWin, OddsWithKit, OddsBane;
        public string Derivation;
    }

    public static class Circuit
    {
        public static readonly CircuitRules[] Rules =
        {
            new CircuitRules
            {
                Floor = 1, Levers = 10, Live = 3, PlayerShocks = 2, CollShocks = 2, SafeBank = 300, SafeMarkers = 1, KitTraces = 2,
                CollectorTurnS = 2.8, AfterShockS = 4.6, OddsWin = 0.5, OddsWithKit = 0.76, OddsBane = 0.42,
                Derivation = "DESIGN.md §6 (DECISIONS #80): 10 levers, 3 live, two shocks each, both pick at random: a coin flip (50%); the Insulated Kit's 2 traces 76%; the Bane's 4 live levers 42%",
            },
            new CircuitRules
            {
                Floor = 2, Levers = 12, Live = 4, PlayerShocks = 2, CollShocks = 2, SafeBank = 600, SafeMarkers = 1, KitTraces = 2,
                CollectorTurnS = 2.8, AfterShockS = 4.6, OddsWin = 0.45, OddsWithKit = 0.63, OddsBane = 0.44,
                Derivation = "DESIGN.md §6, §11: floor 2's panel, 12 levers, 4 live, a dead lever pays $600; you win 45% (63% with the Kit pulling known-dead levers first, 44% with the Bane's fifth live lever), 200k random duels",
            },
        };

        public static CircuitRules For(int floor) => Rules.FirstOrDefault(r => r.Floor == floor) ?? Rules[0];
    }

    // ------------------------------------------------------------------ the bar (DESIGN.md §10; DECISIONS #67, #77)

    public sealed class DrinkDef
    {
        public string Id, Name, Text, EdgeText, Derivation;
        public int Price;
        /// <summary>seconds of buzz it adds (of floor time)</summary>
        public double BuzzS;
        /// <summary>the edge: payouts at these tiers' tables x (1 + bonus), for BuzzS of floor time</summary>
        public double Bonus;
        public string[] Tiers;
    }

    public static class Drinks
    {
        public static readonly DrinkDef[] All =
        {
            new DrinkDef { Id = "beer", Name = "Draft Beer", Price = 40, BuzzS = 60, Text = "A LITTLE LOOSE", Bonus = 0.06, Tiers = new[] { Tier.Low }, EdgeText = "LOW TABLES PAY +6%",
                Derivation = "DESIGN.md §10 (DECISIONS #83): a minute of buzz; +6% low for 60 s ≈ $137 back at the Bandit for $40" },
            new DrinkDef { Id = "martini", Name = "Dry Martini", Price = 120, BuzzS = 60, Text = "THE ROOM GETS WARM", Bonus = 0.04, Tiers = new[] { Tier.Mid }, EdgeText = "MID TABLES PAY +4%",
                Derivation = "DESIGN.md §10 (DECISIONS #83): a minute of buzz; +4% mid for 60 s ≈ $190 back at Hi-Lo for $120" },
            new DrinkDef { Id = "whiskey", Name = "Double Whiskey", Price = 200, BuzzS = 60, Text = "THE FLOOR TILTS", Bonus = 0.03, Tiers = new[] { Tier.High }, EdgeText = "HIGH TABLES PAY +3%",
                Derivation = "DESIGN.md §10 (DECISIONS #83): a minute of buzz; +3% high for 60 s ≈ $190 back at Red Line for $200" },
            new DrinkDef { Id = "collector", Name = "The Collector's Own", Price = 500, BuzzS = 150, Text = "NOBODY REMEMBERS THE REST", Bonus = 0.05, Tiers = new[] { Tier.Low, Tier.Mid, Tier.High }, EdgeText = "EVERY TABLE PAYS +5%",
                Derivation = "DESIGN.md §10: past the ceiling in one glass; +5% everywhere for 150 s ≈ $500 back on a mixed floor" },
        };
        static readonly Dictionary<string, DrinkDef> byId = All.ToDictionary(d => d.Id);
        public static DrinkDef ById(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;

        /// <summary>The most buzz you can carry (s), and the buzz at which you're fully drunk (s).</summary>
        public const double BuzzMaxS = 180, BuzzFullS = 90;
        public const string BuzzDerivation = "DESIGN.md §10: buzz stacks to 180 s; 90 s of it is as drunk as the screen gets";
    }

    // ------------------------------------------------------------------ the Collectors (DECISIONS #81)

    public sealed class CollectorDef
    {
        public string Id, Name, Game, Unlocks, Derivation;
        /// <summary>the tier color role his eyes burn: coll | high | soul</summary>
        public string Eyes;
        public string[] Intro;
    }

    public sealed class EndingDef
    {
        public string Id, Title, Text, Derivation;
        public int Killed;
    }

    public static class Collectors
    {
        public static readonly CollectorDef[] All =
        {
            new CollectorDef { Id = "collector", Name = "THE COLLECTOR", Game = "circuit", Unlocks = "insulated", Eyes = "coll", Intro = null,
                Derivation = "DESIGN.md §7 (DECISIONS #81): the first; beat him at the Circuit and the Insulated Kit joins the pool" },
            new CollectorDef { Id = "auditor", Name = "THE AUDITOR", Game = "circuit", Unlocks = null, Eyes = "high",
                Intro = new[] { "SO YOU ARE THE ONE.", "I REPLACE THE LAST COLLECTOR. I AM NOT AS PATIENT." },
                Derivation = "DESIGN.md §7 (DECISIONS #81): the second; his own game comes later (the Circuit for now)" },
            new CollectorDef { Id = "receiver", Name = "THE RECEIVER", Game = "circuit", Unlocks = null, Eyes = "soul",
                Intro = new[] { "TWO OF US, GONE.", "THE HOUSE SENT ME TO TIDY UP." },
                Derivation = "DESIGN.md §7 (DECISIONS #81): the third; his own game comes later (the Circuit for now)" },
        };

        public static CollectorDef Of(int idx) => All[System.Math.Min(idx, All.Length - 1)];

        public static readonly EndingDef[] Endings =
        {
            new EndingDef { Id = "paid", Title = "PAID IN FULL", Killed = 0, Text = "SIX NIGHTS, TWO FLOORS, EVERY QUOTA. THE DEBT IS PAID. THE STAIRS GO DOWN, NOT UP.", Derivation = "DESIGN.md §7 (DECISIONS #81): walked out, no Collector killed" },
            new EndingDef { Id = "chair", Title = "AN EMPTY CHAIR", Killed = 1, Text = "A COLLECTOR DIED AT YOUR TABLE. YOU WALK OUT RICHER, AND THE HOUSE KNOWS YOUR FACE.", Derivation = "DESIGN.md §7 (DECISIONS #81): walked out having killed one or two" },
            new EndingDef { Id = "house", Title = "THE HOUSE IS YOURS", Killed = 3, Text = "THREE CHAIRS, EMPTY. ON THE LAST ONE, THE KEYS TO THE VIG.", Derivation = "DESIGN.md §7 (DECISIONS #81): walked out having killed three" },
        };

        /// <summary>The run's ending: the one with the most kills you reached.</summary>
        public static EndingDef EndingFor(int killed) => Enumerable.Reverse(Endings).First(e => killed >= e.Killed);
    }

    // ------------------------------------------------------------------ the tables' show (presentation pacing)

    /// <summary>How long each round is in the air; the sim holds each result back this long.</summary>
    public static class Feel
    {
        public const double DiceS = 1.6, DeadmanS = 2.2, CardS = 2.1, WheelS = 4.2, BanditS = 1.8;
        /// <summary>floor 2: the ball round the wheel, the dice down the craps table, the ball down the pegs</summary>
        public const double RouletteS = 5.0, CrapsS = 1.9, PegS = 2.6;
        /// <summary>Crash's climb: the multiplier is e^(CrashRate x seconds), 2x at 2.5 s, 10x at 8.2 s</summary>
        public const double CrashRate = 0.28;
        /// <summary>Twenty-One: a card off the shoe every DealGap s (DECISIONS #83)</summary>
        public const double DealGap = 0.42;
        public const double CelebrateS = 2.2;
        public const int BigWin = 700, Jackpot = 4000;
    }
}
