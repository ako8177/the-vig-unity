// The floor-1 tables as typed data. Every number carries a derivation naming the DESIGN.md section it
// comes from. The sim's table modules (Tables/) play them; the panels and signs print them.
using System.Collections.Generic;
using System.Linq;

namespace Vig.Sim.Data
{
    public static class Tier
    {
        public const string Low = "low", Mid = "mid", High = "high", Coll = "coll";
    }

    public sealed class BetLimits
    {
        public readonly int Min, Max, Step, Std;
        public BetLimits(int min, int max, int step, int std) { Min = min; Max = max; Step = step; Std = std; }
    }

    public sealed class TableDef
    {
        public string Id, Name, Sign, Tier;
        /// <summary>what losing costs, which is also the sign's loss icon: coin | mark | skull</summary>
        public string Loss;
        /// <summary>the stakes on floor 1 (DECISIONS #85): any step from min to max; std is what the bots play</summary>
        public BetLimits Bet;
        /// <summary>seconds per round with one player seated (the pace bots play at this rate)</summary>
        public double RoundS;
        /// <summary>seconds from the call until the result lands (Feel)</summary>
        public double ResolveS;
        public double WinP;
        /// <summary>total return per $1 on a win; null where it varies by outcome</summary>
        public double? Pays;
        public double Rtp;
        public string LossText, PaysText, HowText, Derivation;
    }

    public static class Tables
    {
        public static readonly TableDef[] All =
        {
            new TableDef
            {
                Id = "bandit", Name = "The Bandit", Sign = "BANDIT", Tier = Data.Tier.Low, Loss = "coin", Bet = new BetLimits(50, 250, 50, 100), RoundS = 2.5, ResolveS = Feel.BanditS,
                WinP = 0.2586, Pays = null, Rtp = 0.9504, PaysText = "8 LINES, CORNERS, THE GRID", LossText = "THE BET",
                HowText = "PULL. 9 CELLS, EACH ONE STOP OF 10.",
                Derivation = "DESIGN.md §3: low tier; 3x3, 8 lines 10/3/1/0.3x, corners 3x, whole grid 50x over 4^9 weighted grids → RTP 0.9504; -$5 a round",
            },
            new TableDef
            {
                Id = "twentyone", Name = "Twenty-One", Sign = "21", Tier = Data.Tier.Low, Loss = "coin", Bet = new BetLimits(50, 400, 50, 200), RoundS = 8, ResolveS = 0.6,
                WinP = 0.43, Pays = null, Rtp = 0.9921, PaysText = "1:1, NATURAL 3:2", LossText = "THE BET (MORE IF YOU DOUBLE OR SPLIT)",
                HowText = "6 DECKS. HIT, STAND, DOUBLE, SPLIT. DEALER HITS SOFT 17.",
                Derivation = "DESIGN.md §3: low tier; 6-deck shoe, H17, peek, double any two (and after a split), split once, 3:2 → basic strategy RTP 0.9921 (endless-shoe DP; 400k shoe hands measure 0.9929)",
            },
            new TableDef
            {
                Id = "hilo", Name = "Hi-Lo Dice", Sign = "HI-LO", Tier = Data.Tier.Mid, Loss = "mark", Bet = new BetLimits(100, 500, 100, 200), RoundS = 3, ResolveS = Feel.DiceS,
                WinP = 0.5, Pays = 2.4, Rtp = 1.2, PaysText = "2.4x", LossText = "THE BET + 1 MARK",
                HowText = "ONE DIE. CALL HI (4-6) OR LO (1-3).",
                Derivation = "DESIGN.md §3: mid tier, p 0.50 x 2.4 = RTP 1.20, +$40 a round; 3 marks still pay, 4 lose",
            },
            new TableDef
            {
                Id = "wheel", Name = "Wheel of Marks", Sign = "WHEEL", Tier = Data.Tier.Mid, Loss = "mark", Bet = new BetLimits(100, 600, 100, 300), RoundS = 5, ResolveS = Feel.WheelS,
                WinP = 0.48, Pays = null, Rtp = 1.16, PaysText = "BY COLOR, UP TO 6x", LossText = "THE BET + 1 TEAM MARK",
                HowText = "RED 1.5x, PURPLE 2.5x, GOLD 6x, BLACK HALF BACK.",
                Derivation = "DESIGN.md §3: mid tier, 25 slots: 8 red 1.5x, 3 purple 2.5x, 1 gold 6x, 7 black 0.5x, 6 marks 0 = 29/25 → RTP 1.16, +$48 a round; ahead on 12 of 25",
            },
            new TableDef
            {
                Id = "redline", Name = "Red Line", Sign = "RED LINE", Tier = Data.Tier.High, Loss = "skull", Bet = new BetLimits(250, 1250, 250, 500), RoundS = 6, ResolveS = Feel.CardS,
                WinP = 0.5, Pays = 2.5, Rtp = 1.25, PaysText = "2.5x", LossText = "THE BET; 3 LOSSES IN A ROW COST A LIFE",
                HowText = "CALL RED OR BLACK. ONE CARD, FRESH DECK.",
                Derivation = "DESIGN.md §3: high tier, p 0.50 x 2.5 = RTP 1.25, +$125 a round; a three-loss streak costs a life (31% in 6 rounds)",
            },
            new TableDef
            {
                Id = "deadman", Name = "Dead Man's Hand", Sign = "DEAD MAN", Tier = Data.Tier.High, Loss = "skull", Bet = new BetLimits(500, 2500, 500, 1000), RoundS = 4, ResolveS = Feel.DeadmanS,
                WinP = 0.25, Pays = 6, Rtp = 1.5, PaysText = "6x", LossText = "THE BET; SNAKE EYES COSTS A LIFE",
                HowText = "TWO DICE. DOUBLES OR 10+ WIN. 1-1 COSTS A LIFE.",
                Derivation = "DESIGN.md §3: high tier, p 9/36 = 0.25 x 6 = RTP 1.50, +$500 a round; snake eyes (1/36) costs a life",
            },
            // ---- floor 2 (DESIGN.md §11): its stakes are written at floor 1's scale; FloorStakes doubles them
            new TableDef
            {
                Id = "roulette", Name = "Roulette", Sign = "ROULETTE", Tier = Data.Tier.Low, Loss = "coin", Bet = new BetLimits(50, 250, 50, 100), RoundS = 7, ResolveS = Feel.RouletteS,
                WinP = 18 / 37.0, Pays = null, Rtp = 36 / 37.0, PaysText = "EVEN 2x, DOZEN 3x, NUMBER 36x", LossText = "THE BET",
                HowText = "ONE ZERO. RED, BLACK, ODD, EVEN, LOW, HIGH, A DOZEN OR ONE NUMBER.",
                Derivation = "DESIGN.md §11: low tier; 37 pockets, every bet pays 36/37 of fair → RTP 0.973 on all of them; even money wins 18 of 37",
            },
            new TableDef
            {
                Id = "craps", Name = "Craps", Sign = "CRAPS", Tier = Data.Tier.Low, Loss = "coin", Bet = new BetLimits(50, 250, 50, 100), RoundS = 9, ResolveS = Feel.CrapsS,
                WinP = 244 / 495.0, Pays = 2, Rtp = 488 / 495.0, PaysText = "PASS LINE 1:1", LossText = "THE BET",
                HowText = "COME OUT: 7 OR 11 WINS, 2 3 12 LOSE. ELSE HIT THE POINT AGAIN BEFORE A 7.",
                Derivation = "DESIGN.md §11: low tier; the pass line wins 244 of 495 → x2 = RTP 0.986",
            },
            new TableDef
            {
                Id = "pegdrop", Name = "Peg Drop", Sign = "PEG DROP", Tier = Data.Tier.Mid, Loss = "mark", Bet = new BetLimits(100, 500, 100, 200), RoundS = 4, ResolveS = Feel.PegS,
                WinP = 74 / 256.0, Pays = null, Rtp = 300 / 256.0, PaysText = "EDGES 20x, 4x, 1.6x, 0.7x, CENTER 0.4x", LossText = "THE CENTER SLOT: +1 MARK",
                HowText = "DROP THE BALL. 8 ROWS OF PEGS, 9 SLOTS.",
                Derivation = "DESIGN.md §11: mid tier; 8 rows, slot k has C(8,k)/256; 20/4/1.6/0.7/0.4 → 300/256 = RTP 1.172; ahead 74 of 256",
            },
            new TableDef
            {
                Id = "crash", Name = "Crash", Sign = "CRASH", Tier = Data.Tier.High, Loss = "skull", Bet = new BetLimits(250, 1250, 250, 500), RoundS = 7, ResolveS = 0.5,
                WinP = 0.4152, Pays = null, Rtp = 1.2457, PaysText = "CASH OUT AT ANY x", LossText = "THE BET; A CRASH PAST 5x COSTS A LIFE",
                HowText = "IT CLIMBS FROM 1x. CASH OUT BEFORE IT CRASHES.",
                Derivation = "DESIGN.md §11: high tier; P(reach x) = x^-0.8, so cashing at 3x wins 41.5% for RTP 3^0.2 = 1.246, and it rises the longer you ride",
            },
        };

        /// <summary>The tables of each floor (DESIGN.md §11).</summary>
        public static string[] OnFloor(int floor) => floor >= 2
            ? new[] { "roulette", "craps", "pegdrop", "hilo", "crash", "redline" }
            : new[] { "bandit", "twentyone", "hilo", "wheel", "redline", "deadman" };

        static readonly Dictionary<string, TableDef> byId = All.ToDictionary(t => t.Id);
        public static TableDef ById(string id) => id != null && byId.TryGetValue(id, out var t) ? t : null;

        /// <summary>The Collector's table at the back of the back room (violet; DESIGN.md §6, §7).</summary>
        public const string CollectorSign = "COLLECTOR";

        /// <summary>How a casino level scales every table's stakes (DECISIONS #85): floor 1 plays them as written.</summary>
        public static readonly Dictionary<int, Num> FloorStakes = new Dictionary<int, Num>
        {
            { 1, new Num(1, "DESIGN.md §3 (DECISIONS #85): floor 1 plays the stakes as written") },
            { 2, new Num(2, "DESIGN.md §11: floor 2 doubles every stake, old games and new") },
        };

        public static BetLimits LimitsOf(TableDef t, int floor = 1)
        {
            int k = FloorStakes.TryGetValue(floor, out var n) ? n.I : 1;
            return new BetLimits(t.Bet.Min * k, t.Bet.Max * k, t.Bet.Step * k, t.Bet.Std * k);
        }
    }
}
