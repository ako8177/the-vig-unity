// Floor 2's games (DESIGN.md §11): Roulette and Craps (low), Peg Drop (mid), Crash (high). Same contract as
// floor 1's modules: Start takes the stake and draws from the sim's RNG; Resolve charges the price.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim.Tables
{
    // ------------------------------------------------------------------ Roulette (low)

    /// <summary>One zero, 37 pockets. Even-money calls (red, black, odd, even, low, high) pay 2x, a dozen 3x,
    /// one number 36x (45x with Croupier's Thumb): every bet returns 36/37 = 0.973.</summary>
    public sealed class Roulette : TableModule
    {
        public static readonly int[] Reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
        public static bool IsRed(int n) => Array.IndexOf(Reds, n) >= 0;

        /// <summary>The pockets in their order round a European wheel (the presentation spins this).</summary>
        public static readonly int[] Wheel = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };

        /// <summary>Does a call win on this pocket, and what does it return per $1 (stake included)?</summary>
        public static double Pays(string call, int n, bool thumb = false)
        {
            if (call == null) return -1;
            if (call.StartsWith("n") && int.TryParse(call.Substring(1), out int k) && k >= 0 && k <= 36) return n == k ? (thumb ? 45 : 36) : 0;
            if (n == 0) return call == "red" || call == "black" || call == "odd" || call == "even" || call == "low" || call == "high" || call == "d1" || call == "d2" || call == "d3" ? 0 : -1;
            switch (call)
            {
                case "red": return IsRed(n) ? 2 : 0;
                case "black": return !IsRed(n) ? 2 : 0;
                case "odd": return n % 2 == 1 ? 2 : 0;
                case "even": return n % 2 == 0 ? 2 : 0;
                case "low": return n <= 18 ? 2 : 0;
                case "high": return n >= 19 ? 2 : 0;
                case "d1": return n <= 12 ? 3 : 0;
                case "d2": return n >= 13 && n <= 24 ? 3 : 0;
                case "d3": return n >= 25 ? 3 : 0;
            }
            return -1;
        }

        public static string CallName(string call) =>
            call.StartsWith("n") ? "NUMBER " + call.Substring(1) : call == "d1" ? "1ST DOZEN" : call == "d2" ? "2ND DOZEN" : call == "d3" ? "3RD DOZEN" : call.ToUpperInvariant();

        public override string Id => "roulette";
        public override string[] Cmds => new[] { "call" };

        public override Odds Odds()
        {
            // every call returns the same: check two of them by enumeration
            double red = 0, num = 0, wins = 0;
            for (int n = 0; n <= 36; n++) { red += Pays("red", n); num += Pays("n17", n); if (Pays("red", n) > 1) wins++; }
            return new Odds { Rtp = (red / 37 + num / 37) / 2, WinP = wins / 37 };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "call" || Pays(cmd.Call, 1) < 0) return StartResult.Refuse("place a bet on the layout");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            int n = g.Rng.Int(0, 36);
            double x = Pays(cmd.Call, n, p.Charms.Contains("thumb"));
            double factor = Rules.PayoutFactor(g, p, Id);
            int ret = x > 0 ? Js.RoundInt(bet * x * factor) : 0;
            string pocket = n == 0 ? "0 GREEN" : $"{n} {(IsRed(n) ? "RED" : "BLACK")}";
            return StartResult.Of(new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = ret > bet, Factor = factor, Pocket = n, Call = cmd.Call,
                Text = ret > bet ? $"{pocket}: {CallName(cmd.Call)} WINS ${ret}" : $"{pocket}: {CallName(cmd.Call)} LOSES",
            });
        }
    }

    // ------------------------------------------------------------------ Craps (low)

    /// <summary>The pass line. The come-out roll: 7 or 11 wins even money, 2, 3 or 12 loses, anything else is the
    /// point; then roll until the point comes again (win) or a 7 (lose). Wins 244 of 495: RTP 0.986. The stake
    /// goes down on the come-out and rides every roll; at 0:00 a bet on a point comes back. A one-roll bet (the
    /// command's call: field, anycraps, eleven) can ride beside any throw (U34, DESIGN.md §16).</summary>
    public sealed class Craps : TableModule
    {
        public override string Id => "craps";
        public override string[] Cmds => new[] { "spin" };

        public override Odds Odds()
        {
            // exact: P(win) = P(7,11) + sum over points of P(point) x P(point before 7)
            var ways = new int[13];
            for (int a = 1; a <= 6; a++) for (int b = 1; b <= 6; b++) ways[a + b]++;
            double win = (ways[7] + ways[11]) / 36.0;
            foreach (int pt in new[] { 4, 5, 6, 8, 9, 10 }) win += ways[pt] / 36.0 * ways[pt] / (double)(ways[pt] + ways[7]);
            return new Odds { Rtp = win * Def.Pays.Value, WinP = win };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "spin") return StartResult.Refuse("roll the dice");
            var t = g.State.Tables[Id];
            // a one-roll bet rides beside this throw at the table minimum (U34)
            var one = cmd.Call == null ? null : SideBets.OneRollById(cmd.Call);
            if (cmd.Call != null && one == null) return StartResult.Refuse("no such side bet");
            int side = one == null ? 0 : one.Mins * Data.Tables.LimitsOf(Def, g.State.Floor).Min;
            int bet = t.PointBet;
            if (t.Point == 0)
            {
                if (side > 0 && BetOf(g, Def, cmd.Amount, out int b, out _) && g.State.Bank < Rules.StakeFor(p, b) + side)
                    return StartResult.Refuse("the bank can't cover the side bet too");
                var no = TakeStake(g, p, cmd, out bet);
                if (no.HasValue) return no.Value;
                t.PointBet = bet;
            }
            else if (side > 0 && g.State.Bank < side) return StartResult.Refuse("the bank can't cover the side bet");
            if (side > 0) Stake(g, side);
            int d0 = g.Rng.Int(1, 6), d1 = g.Rng.Int(1, 6), sum = d0 + d1;
            double factor = Rules.PayoutFactor(g, p, Id);
            int sideRet = side > 0 && one.Pays[sum] > 0 ? Js.RoundInt(side * (one.Pays[sum] + 1) * factor) : 0;
            string sideText = side == 0 ? "" : sideRet > 0 ? $" + {one.Name} ${sideRet}" : $" ({one.Name} LOSES)";
            Outcome Done(bool win, string text, int? lineRet = null)
            {
                int ret = lineRet ?? (win ? Js.RoundInt(bet * Def.Pays.Value * factor) : 0);
                var o = new Outcome { Table = Id, Player = p.Id, Bet = bet + side, Ret = ret + sideRet, Win = win, Factor = factor, Dice = new List<int> { d0, d1 }, Point = t.Point, Call = one?.Id, Text = (win ? $"{text}: ${ret}" : text) + sideText };
                // with a side bet riding, a win is what came out ahead (U24: no feelings faked)
                if (side > 0) o.Win = o.Ret > o.Bet;
                t.Point = 0;
                t.PointBet = 0;
                return o;
            }
            // a throw that leaves the line riding settles only the side bet: it lands like a round of its own
            StartResult Rolled(string text)
            {
                if (side == 0)
                {
                    g.Emit(new SimEvent { T = "roll", Player = p.Id, Table = Id, Dice = new List<int> { d0, d1 }, Point = t.Point, Text = text, Ticks = Rules.Ticks(Def.ResolveS) });
                    return StartResult.Pending;
                }
                return StartResult.Of(new Outcome
                {
                    Table = Id, Player = p.Id, Bet = side, Ret = sideRet, Win = sideRet > side, Factor = factor, Dice = new List<int> { d0, d1 }, Point = t.Point, Call = one.Id, LineRides = true,
                    Text = $"{sum}: {one.Name} {(sideRet > 0 ? $"WINS ${sideRet}" : "LOSES")}. THE POINT IS {t.Point}",
                });
            }
            if (t.Point == 0)
            {
                if (sum == 7 || sum == 11) return StartResult.Of(Done(true, $"{sum} ON THE COME-OUT WINS"));
                if (sum == 2 || sum == 3 || sum == 12)
                {
                    // Hot Shooter: the come-out's craps hand the bet back (DESIGN.md §15)
                    if (!p.Charms.Contains("shooter")) return StartResult.Of(Done(false, $"{sum}: CRAPS"));
                    return StartResult.Of(Done(false, $"{sum}: CRAPS, BUT THE HOT SHOOTER GETS THE BET BACK", bet));
                }
                t.Point = sum;
                return Rolled($"THE POINT IS {sum}");
            }
            if (sum == t.Point) return StartResult.Of(Done(true, $"{sum}: THE POINT"));
            if (sum == 7) return StartResult.Of(Done(false, "7: SEVEN OUT"));
            return Rolled($"{sum}. THE POINT IS {t.Point}");
        }
    }

    // ------------------------------------------------------------------ Peg Drop (mid)

    /// <summary>A ball drops through 8 rows of pegs into 9 slots: slot k with chance C(8,k)/256. The edges pay
    /// 20x, then 4x, 1.6x, 0.7x, and the center 0.4x and a mark (Pegged: -5% on your Peg Drop payouts a
    /// stack). RTP 300/256 = 1.172; ahead 74 of 256.</summary>
    public sealed class PegDrop : TableModule
    {
        public const int Rows = 8;
        public static readonly double[] Mult = { 20, 4, 1.6, 0.7, 0.4, 0.7, 1.6, 4, 20 };

        public override string Id => "pegdrop";
        public override string[] Cmds => new[] { "spin" };

        public override Odds Odds()
        {
            double rtp = 0, win = 0;
            for (int path = 0; path < 1 << Rows; path++)
            {
                int k = 0;
                for (int r = 0; r < Rows; r++) k += (path >> r) & 1;
                rtp += Mult[k] / 256;
                if (Mult[k] > 1) win += 1 / 256.0;
            }
            return new Odds { Rtp = rtp, WinP = win };
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T != "spin") return StartResult.Refuse("drop the ball");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            var path = new List<int>();
            for (int r = 0; r < Rows; r++) path.Add(g.Rng.Int(0, 1));
            int slot = path.Sum();
            double factor = Rules.PayoutFactor(g, p, Id);
            // Center Pin: the center returns the stake (and still marks you: DESIGN.md §15)
            bool pin = slot == 4 && p.Charms.Contains("centerpin");
            int ret = pin ? bet : Js.RoundInt(bet * Mult[slot] * factor);
            return StartResult.Of(new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = ret, Win = ret > bet, Factor = factor, Path = path, Slot = slot,
                Text = slot == 4 ? (pin ? $"THE CENTER PIN: ${ret} BACK, AND A MARK" : $"THE CENTER: ${ret} AND A MARK") : $"{Js.Num(Mult[slot])}x: ${ret}",
            });
        }

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (o.Slot == 4 && Rules.AddMark(g, "pegged", p.Id)) o.Mark = "pegged";
        }
    }

    // ------------------------------------------------------------------ Crash (high)

    /// <summary>The multiplier climbs from 1x (e^(0.28 s): 2x at 2.5 s, 10x at 8.2 s) and crashes at a point
    /// drawn so that P(it reaches x) = x^-0.8. Cash out (call "cash") before it crashes to win the stake times
    /// the multiplier; set an auto cash-out with the stake's call (a number). Cashing at 3x wins 41.5% for an
    /// RTP of 3^0.2 = 1.246, and the RTP rises the longer you ride; but a crash past 5x costs a life.</summary>
    public sealed class Crash : TableModule
    {
        public const double Shape = 0.8, Deadly = 5, Cap = 1000;

        public override string Id => "crash";
        public override string[] Cmds => new[] { "spin", "call" };

        /// <summary>The multiplier `ticks` after take-off, to the cent below.</summary>
        public static double MultAt(int ticks) => Math.Floor(Math.Exp(Feel.CrashRate * ticks / State.TicksPerS) * 100) / 100;

        /// <summary>P(the climb reaches x).</summary>
        public static double Reach(double x) => x <= 1 ? 1 : Math.Pow(x, -Shape);

        /// <summary>A multiplier as printed, the same on every machine.</summary>
        public static string X(double m) => m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public override Odds Odds() => new Odds { Rtp = 3 * Reach(3), WinP = Reach(3) };

        public override double? HoldS(Outcome o) => 0.5;

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            var t = g.State.Tables[Id];
            if (cmd.T == "call" && cmd.Call == "cash")
            {
                if (t.Crash == null) return StartResult.Refuse("nothing in the air");
                return StartResult.Of(CashAt(g, t, MultAt(g.Tick - t.Crash.Start)));
            }
            if (cmd.T != "spin") return StartResult.Refuse("launch, then cash out");
            if (t.Crash != null) return StartResult.Refuse("it's already climbing");
            var no = TakeStake(g, p, cmd, out int bet);
            if (no.HasValue) return no.Value;
            double u = 1 - g.Rng.Next();
            double at = Math.Floor(Math.Min(Cap, Math.Pow(u, -1 / Shape)) * 100) / 100;
            double target = 0;
            if (cmd.Call != null && double.TryParse(cmd.Call, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tg) && tg >= 1.01) target = Math.Floor(tg * 100) / 100;
            t.Crash = new CrashRound { Start = g.Tick, CrashAt = at, Target = target, Bet = bet, Player = p.Id };
            g.Emit(new SimEvent { T = "launch", Player = p.Id, Table = Id, Amount = bet });
            return StartResult.Pending;
        }

        /// <summary>Cash out at `mult` (refused, as a crash, if it already crashed there).</summary>
        static Outcome CashAt(Game g, TableState t, double mult)
        {
            var c = t.Crash;
            var p = g.State.Players[c.Player];
            if (mult >= c.CrashAt) return Crashed(g, t);
            double factor = Rules.PayoutFactor(g, p, "crash");
            int ret = Js.RoundInt(c.Bet * mult * factor);
            t.Crash = null;
            return new Outcome { Table = "crash", Player = p.Id, Bet = c.Bet, Ret = ret, Win = ret > c.Bet, Factor = factor, Mult = mult, Crashed = false, Text = $"CASHED AT {X(mult)}x: ${ret}" };
        }

        static Outcome Crashed(Game g, TableState t)
        {
            var c = t.Crash;
            var p = g.State.Players[c.Player];
            bool chute = p.Charms.Contains("chute");
            int ret = chute ? c.Bet / 2 : 0;
            t.Crash = null;
            return new Outcome
            {
                Table = "crash", Player = p.Id, Bet = c.Bet, Ret = ret, Win = false, Factor = 1, Mult = c.CrashAt, Crashed = true,
                Text = c.CrashAt >= Deadly && !chute ? $"CRASHED AT {X(c.CrashAt)}x: A LIFE" : chute ? $"CRASHED AT {X(c.CrashAt)}x: THE CHUTE SAVES ${ret}" : $"CRASHED AT {X(c.CrashAt)}x",
            };
        }

        /// <summary>The climb: an auto cash-out, or the crash (the tables system lands what this returns).</summary>
        public static Outcome Climb(Game g, TableState t)
        {
            var c = t.Crash;
            if (c == null) return null;
            double m = MultAt(g.Tick - c.Start);
            if (c.Target > 0 && m >= c.Target && c.Target < c.CrashAt) return CashAt(g, t, c.Target);
            if (m >= c.CrashAt) return Crashed(g, t);
            return null;
        }

        /// <summary>Walking away, or 0:00, cashes out where it is.</summary>
        public static Outcome Leave(Game g, TableState t) => t.Crash == null ? null : CashAt(g, t, MultAt(g.Tick - t.Crash.Start));

        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (o.Win)
            {
                int n = Economy.MarkersCrash.I * (p.Charms.Contains("magnet") ? 2 : 1);
                Rules.AddMarkers(g, n, "crash cash-out");
                o.Markers = n;
            }
            else if (o.Crashed == true && o.Mult >= Deadly && !p.Charms.Contains("chute"))
            {
                o.LifeLost = true;
                Rules.LoseLife(g, p, "crash");
            }
        }
    }
}
