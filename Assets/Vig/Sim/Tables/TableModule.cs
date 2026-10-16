// The table module interface: each table a module with Odds, Resolve, LossCost. Start takes the stake
// and draws the outcome from the sim's RNG; the tables system holds it back ResolveS seconds (the dice
// roll, the reels spin) and then calls Resolve to pay it out and apply its price: a mark, a team mark,
// a life.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim.Tables
{
    public struct Odds
    {
        /// <summary>expected return per $1 bet, exact (enumerated)</summary>
        public double Rtp;
        /// <summary>chance a round comes out ahead</summary>
        public double WinP;
    }

    /// <summary>What Start came to: an outcome to hold back, nothing yet (the round goes on), or a refusal.</summary>
    public struct StartResult
    {
        public Outcome Outcome;
        public string Refusal;
        public static StartResult Of(Outcome o) => new StartResult { Outcome = o };
        public static StartResult Refuse(string why) => new StartResult { Refusal = why };
        public static readonly StartResult Pending = new StartResult();
    }

    public abstract class TableModule
    {
        public abstract string Id { get; }
        public TableDef Def => Data.Tables.ById(Id);
        /// <summary>the commands it answers (besides sit/stand)</summary>
        public abstract string[] Cmds { get; }
        /// <summary>the exact odds, by enumerating every outcome</summary>
        public abstract Odds Odds();
        /// <summary>what losing costs, as printed</summary>
        public string LossCost() => Def.LossText;
        public abstract StartResult Start(Game g, PlayerState p, Command cmd);
        /// <summary>The held-back result lands: charge its price (marks, lives, markers).</summary>
        public virtual void Resolve(Game g, PlayerState p, Outcome o) { }
        /// <summary>how long this outcome is held before it lands (s); null: the table's ResolveS</summary>
        public virtual double? HoldS(Outcome o) => null;

        /// <summary>The stake asked for at this table (none: the standard), checked against its limits on
        /// this floor and rounded to its step; or why not (DECISIONS #85).</summary>
        public static bool BetOf(Game g, TableDef def, double? amount, out int bet, out string why)
        {
            var l = Data.Tables.LimitsOf(def, g.State.Floor);
            why = null;
            bet = 0;
            if (amount == null) { bet = l.Std; return true; }
            double am = amount.Value;
            if (double.IsNaN(am) || double.IsInfinity(am)) { why = "no such bet"; return false; }
            double a = Js.Round(am / l.Step) * l.Step;
            if (a < l.Min || a > l.Max) { why = $"bet ${l.Min}-${l.Max}"; return false; }
            bet = (int)a;
            return true;
        }

        /// <summary>Take a stake from the bank, or say why not.</summary>
        public static string Stake(Game g, int amount)
        {
            if (g.State.Bank < amount) return "the bank is short";
            g.State.Bank -= amount;
            return null;
        }

        /// <summary>Checks the bet, doubles it under Overclock and takes it; or why not.</summary>
        protected StartResult? TakeStake(Game g, PlayerState p, Command cmd, out int bet)
        {
            bet = 0;
            if (!BetOf(g, Def, cmd.Amount, out int b, out string why)) return StartResult.Refuse(why);
            bet = Rules.StakeFor(p, b);
            var no = Stake(g, bet);
            if (no != null) return StartResult.Refuse(no);
            return null;
        }

        public static Card DrawCard(Game g) { int r = g.Rng.Int(1, 13); int s = g.Rng.Int(0, 3); return new Card(r, s); }
        public static readonly string[] Rank = { "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        public static bool IsRed(Card c) => c.S < 2;

        public static readonly TableModule[] All = { new Bandit(), new TwentyOne(), new HiLo(), new WheelOfMarks(), new RedLine(), new DeadMan(), new Roulette(), new Craps(), new PegDrop(), new Crash() };
        static readonly Dictionary<string, TableModule> byId = All.ToDictionary(m => m.Id);
        public static TableModule ById(string id) => id != null && byId.TryGetValue(id, out var m) ? m : null;
    }

    // ------------------------------------------------------------------ The Bandit (low)

    /// <summary>A 3x3 grid (DECISIONS #75). Each of the 9 cells is an independent stop of a 10-stop strip
    /// (one 7, two stars, three BARs, four blanks). Every pattern that hits pays, summed: on each of the 8
    /// lines three 7s 10x, three stars 3x, three BARs 1x, any three symbols 0.3x; the four corners alike
    /// 3x; the whole grid alike 50x. 4^9 weighted grids enumerate to RTP 0.9504 (DESIGN.md §3).</summary>
    public sealed class Bandit : TableModule
    {
        public const int Blank = 0, Bar = 1, Star = 2, Seven = 3;
        public static readonly int[] Strip = { Seven, Star, Bar, Blank, Star, Bar, Blank, Bar, Blank, Blank };
        public const int Rows = 3, Cols = 3, Cells = Rows * Cols;
        public static readonly int[][] Lines = BuildLines();
        public static readonly int[] Corners = { 0, Cols - 1, Cells - Cols, Cells - 1 };
        public const double PayLine7 = 10, PayLineStar = 3, PayLineBar = 1, PayLineAny = 0.3, PayCorners = 3, PayFull = 50;

        static int[][] BuildLines()
        {
            var o = new List<int[]>();
            for (int r = 0; r < Rows; r++) o.Add(Enumerable.Range(0, Cols).Select(c => r * Cols + c).ToArray());
            for (int c = 0; c < Cols; c++) o.Add(Enumerable.Range(0, Rows).Select(r => r * Cols + c).ToArray());
            if (Rows == Cols)
            {
                o.Add(Enumerable.Range(0, Rows).Select(i => i * Cols + i).ToArray());
                o.Add(Enumerable.Range(0, Rows).Select(i => i * Cols + (Cols - 1 - i)).ToArray());
            }
            return o.ToArray();
        }

        public sealed class Hit { public int[] Cells; public string Name; public double X; }

        /// <summary>Every pattern a grid hits, and the total it returns per $1 bet.</summary>
        public static double GridPays(IReadOnlyList<int> g, List<Hit> hits = null, double seven = PayLine7)
        {
            hits = hits ?? new List<Hit>();
            foreach (var line in Lines)
            {
                int a = g[line[0]], b = g[line[1]], c = g[line[2]];
                if (a == Blank || b == Blank || c == Blank) continue;
                bool same = a == b && b == c;
                if (same && a == Seven) hits.Add(new Hit { Cells = line, Name = "7 7 7", X = seven });
                else if (same && a == Star) hits.Add(new Hit { Cells = line, Name = "THREE STARS", X = PayLineStar });
                else if (same) hits.Add(new Hit { Cells = line, Name = "THREE BARS", X = PayLineBar });
                else hits.Add(new Hit { Cells = line, Name = "THREE SYMBOLS", X = PayLineAny });
            }
            int c0 = g[Corners[0]];
            if (c0 != Blank && Corners.All(i => g[i] == c0)) hits.Add(new Hit { Cells = Corners, Name = "CORNERS", X = PayCorners });
            if (g[0] != Blank && g.All(v => v == g[0])) hits.Add(new Hit { Cells = Enumerable.Range(0, g.Count).ToArray(), Name = "THE WHOLE GRID", X = PayFull });
            double sum = 0;
            foreach (var h in hits) sum += h.X;
            return Js.Round(sum * 1000) / 1000;
        }

        /// <summary>A grid's result in one line, as printed.</summary>
        public static string GridText(List<Hit> hits, int ret)
        {
            if (hits.Count == 0) return "NOTHING";
            if (hits.Any(h => h.Name == "THE WHOLE GRID")) return $"THE WHOLE GRID: ${ret}";
            if (hits.Count == 1) return $"{hits[0].Name}: ${ret}";
            return $"{hits.Count} HITS: ${ret}";
        }

        public override string Id => "bandit";
        public override string[] Cmds => new[] { "spin" };

        static Odds? exact;
        public override Odds Odds()
        {
            // every grid of symbols, weighted by how many strip stops show each (4^9 = 262,144)
            if (exact.HasValue) return exact.Value;
            var w = new double[4];
            foreach (var s in Strip) w[s]++;
            int n = Strip.Length;
            var grid = new int[Cells];
            double rtp = 0, winP = 0;
            int total = 1 << (2 * Cells);
            for (int k = 0; k < total; k++)
            {
                int r = k; double pr = 1;
                for (int i = 0; i < Cells; i++) { grid[i] = r % 4; r /= 4; pr *= w[grid[i]] / n; }
                double x = GridPays(grid);
                rtp += pr * x;
                if (x > 1) winP += pr;
            }
            exact = new Odds { Rtp = rtp, WinP = winP };
            return exact.Value;
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "spin") return StartResult.Refuse("pull the lever");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            var grid = new List<int>();
            for (int i = 0; i < Cells; i++) grid.Add(Strip[g.Rng.Int(0, Strip.Length - 1)]);
            var hits = new List<Hit>();
            double x = GridPays(grid, hits, p.Charms.Contains("seven") ? 25 : PayLine7);
            double factor = Rules.PayoutFactor(g, p, Id);
            int ret = Js.RoundInt(bet * x * factor);
            // the progressive: every pull feeds it; the whole grid alike takes it (DESIGN.md §12)
            var s = g.State;
            s.Pot += Js.RoundInt(bet * Economy.PotShare.V);
            bool jackpot = hits.Any(h => h.Name == "THE WHOLE GRID");
            int? pot = null;
            if (jackpot)
            {
                pot = s.Pot;
                ret += s.Pot;
                s.Pot = Economy.PotSeed.I;
            }
            return StartResult.Of(new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = ret > bet, Factor = factor, Reels = grid,
                Jackpot = jackpot, Pot = pot, Text = GridText(hits, ret),
            });
        }
    }

    // ------------------------------------------------------------------ Hi-Lo Dice (mid)

    /// <summary>One die, call HI (4-6) or LO (1-3); p = 1/2, pays 2.4x; a loss marks you (-5% on your Hi-Lo
    /// payouts a stack). DESIGN.md §3.</summary>
    public sealed class HiLo : TableModule
    {
        public override string Id => "hilo";
        public override string[] Cmds => new[] { "call" };

        public override Odds Odds()
        {
            double ret = 0;
            foreach (var call in new[] { "hi", "lo" })
                for (int d = 1; d <= 6; d++) if ((call == "hi") == (d >= 4)) ret += Def.Pays.Value;
            return new Odds { Rtp = ret / 12, WinP = 0.5 };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "call" || (cmd.Call != "hi" && cmd.Call != "lo")) return StartResult.Refuse("call hi or lo");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            int die = g.Rng.Int(1, 6);
            bool win = (cmd.Call == "hi") == (die >= 4);
            bool foot = false;
            if (!win && Rules.UseFoot(p)) { foot = true; die = g.Rng.Int(1, 6); win = (cmd.Call == "hi") == (die >= 4); }
            double factor = Rules.PayoutFactor(g, p, Id);
            int ret = win ? Js.RoundInt(bet * Def.Pays.Value * factor) : 0;
            return StartResult.Of(new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = win, Factor = factor, Dice = new List<int> { die }, Call = cmd.Call,
                Text = (foot ? "THE FOOT ROLLS AGAIN. " : "") + (win ? $"{die}: {cmd.Call.ToUpperInvariant()} WINS ${ret}" : $"{die}: MARKED"),
            });
        }

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (!o.Win && Rules.AddMark(g, "hilo", p.Id)) o.Mark = "hilo";
        }
    }

    // ------------------------------------------------------------------ The Wheel of Marks (mid)

    /// <summary>25 colored slots, each paying its own multiple: red 1.5x, purple 2.5x, one gold 6x, black
    /// half back, and 6 mark slots pay nothing and hand the whole team a random mark. Ahead on 12 of 25,
    /// RTP 29/25 = 1.16. DESIGN.md §3 (DECISIONS #74).</summary>
    public sealed class WheelOfMarks : TableModule
    {
        public static readonly Dictionary<string, double> Pays = new Dictionary<string, double>
        {
            { "red", 1.5 }, { "black", 0.5 }, { "purple", 2.5 }, { "gold", 6 }, { "mark", 0 },
        };

        /// <summary>The 25 slots in order round the wheel.</summary>
        public static readonly string[] Layout =
        {
            "gold", "red", "black", "mark", "red", "purple", "black", "red", "mark", "black", "red", "purple", "black",
            "mark", "red", "black", "red", "mark", "black", "purple", "red", "mark", "black", "red", "mark",
        };
        public static int Slots => Layout.Length;

        public override string Id => "wheel";
        public override string[] Cmds => new[] { "spin" };

        public override Odds Odds() => new Odds
        {
            Rtp = Layout.Sum(c => Pays[c]) / Slots,
            WinP = Layout.Count(c => Pays[c] > 1) / (double)Slots,
        };

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "spin") return StartResult.Refuse("spin the wheel");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            int slot = g.Rng.Int(0, Slots - 1);
            string color = Layout[slot];
            string mark = color == "mark" ? g.Rng.Pick(Marks.TeamMarks) : null;
            double factor = Rules.PayoutFactor(g, p, Id);
            int ret = Js.RoundInt(bet * Pays[color] * factor);
            string text = color == "mark" ? "A MARK FOR THE TEAM"
                : color == "black" ? $"BLACK: HALF BACK, ${ret}"
                : $"{color.ToUpperInvariant()} {Js.Num(Pays[color])}x: ${ret}";
            return StartResult.Of(new Outcome { Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = ret > bet, Factor = factor, Slot = slot, Mark = mark, Text = text });
        }

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (o.Mark != null && !Rules.AddMark(g, o.Mark, -1)) o.Text = "MARK BLOCKED (CLEAN SLATE)";
        }
    }

    // ------------------------------------------------------------------ Red Line (high)

    /// <summary>Call RED or BLACK, one card from a fresh deck; p = 1/2, pays 2.5x. Three losses in a row (two
    /// under the Bane) cost a life; the streak is yours and survives standing up. A win: +1 marker (2 with
    /// Marker Magnet). DESIGN.md §3, §5; DECISIONS #8, #11.</summary>
    public sealed class RedLine : TableModule
    {
        public static int StreakLimit(Game g) => g.State.Mods.TwoLoss ? Wheels.TwoLoss : 3;

        public override string Id => "redline";
        public override string[] Cmds => new[] { "call" };

        public override Odds Odds()
        {
            double ret = 0;
            foreach (var call in new[] { "red", "black" })
                for (int s = 0; s < 4; s++) if ((call == "red") == (s < 2)) ret += Def.Pays.Value;
            return new Odds { Rtp = ret / 8, WinP = 0.5 };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "call" || (cmd.Call != "red" && cmd.Call != "black")) return StartResult.Refuse("call red or black");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            var card = DrawCard(g);
            bool win = (cmd.Call == "red") == IsRed(card);
            double factor = Rules.PayoutFactor(g, p, Id);
            // Red Rover: the call that could cost a life pays 4x (DESIGN.md §12)
            double pays = p.Charms.Contains("redrover") && p.RedStreak == StreakLimit(g) - 1 ? 4 : Def.Pays.Value;
            int ret = win ? Js.RoundInt(bet * pays * factor) : 0;
            return StartResult.Of(new Outcome { Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = win, Factor = factor, Card = card, Call = cmd.Call, Text = win ? $"WINS ${ret}" : "LOSES" });
        }

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (o.Win)
            {
                p.RedStreak = 0;
                int n = Economy.MarkersRedline.I * (p.Charms.Contains("magnet") ? 2 : 1);
                Rules.AddMarkers(g, n, "red line win");
                o.Markers = n;
            }
            else
            {
                p.RedStreak++;
                int limit = StreakLimit(g);
                if (p.RedStreak >= limit)
                {
                    p.RedStreak = 0;
                    o.LifeLost = true;
                    o.Text = $"{limit} IN A ROW: A LIFE";
                    Rules.LoseLife(g, p, "red line");
                }
                else if (p.RedStreak == limit - 1) g.Emit(new SimEvent { T = "warning", Player = p.Id, Table = "redline" });
            }
            o.Streak = p.RedStreak;
        }
    }

    // ------------------------------------------------------------------ Dead Man's Hand (high)

    /// <summary>Two dice. Snake eyes (1-1) costs a life and the bet; any other double or a total of 10+ wins
    /// 6x (9 of 36); everything else loses the bet. p = 0.25 x 6 = RTP 1.50. A win: +2 markers (4 with
    /// Marker Magnet). DESIGN.md §3, §5.</summary>
    public sealed class DeadMan : TableModule
    {
        public static bool IsSnake(int a, int b) => a == 1 && b == 1;
        public static bool Wins(int a, int b) => !IsSnake(a, b) && (a == b || a + b >= 10);

        public override string Id => "deadman";
        public override string[] Cmds => new[] { "spin" };

        public override Odds Odds()
        {
            int wins = 0;
            for (int a = 1; a <= 6; a++) for (int b = 1; b <= 6; b++) if (Wins(a, b)) wins++;
            return new Odds { Rtp = wins * Def.Pays.Value / 36, WinP = wins / 36.0 };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "spin") return StartResult.Refuse("roll the dice");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            int d0 = g.Rng.Int(1, 6), d1 = g.Rng.Int(1, 6);
            bool charmer = p.Charms.Contains("snake");
            bool Won(int a, int b) => Wins(a, b) || (charmer && IsSnake(a, b));
            bool win = Won(d0, d1);
            bool foot = false;
            if (!win && Rules.UseFoot(p)) { foot = true; d0 = g.Rng.Int(1, 6); d1 = g.Rng.Int(1, 6); win = Won(d0, d1); }
            double factor = Rules.PayoutFactor(g, p, Id);
            int ret = win ? Js.RoundInt(bet * Def.Pays.Value * factor) : 0;
            bool snake = IsSnake(d0, d1);
            return StartResult.Of(new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = win, Factor = factor, Dice = new List<int> { d0, d1 },
                Text = (foot ? "THE FOOT ROLLS AGAIN. " : "") + (win && snake ? $"SNAKE EYES, CHARMED: ${ret}" : win ? $"WINS ${ret}" : snake ? "SNAKE EYES: A LIFE" : "LOSES"),
            });
        }

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (o.Win)
            {
                int n = Economy.MarkersDeadman.I * (p.Charms.Contains("magnet") ? 2 : 1);
                Rules.AddMarkers(g, n, "dead man's win");
                o.Markers = n;
            }
            else if (IsSnake(o.Dice[0], o.Dice[1]) && !p.Charms.Contains("snake"))
            {
                o.LifeLost = true;
                Rules.LoseLife(g, p, "dead man's hand");
            }
        }
    }
}
