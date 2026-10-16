// The pace bots (DESIGN.md §8), ported from the TS game's tests/bots.ts and grown to the run of two
// floors (U15): they play the real sim through commands; the one shortcut is walking (a bot steps to a
// table's stand point and pays the walk in clock time at a sprint, 5.1 m/s). Each strategy plays whole
// runs on many seeds; the numbers go in DESIGN.md §8 and the lessons are checked.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Systems;
using Vig.Sim.Tables;

static class Pace
{
    public sealed class Bot
    {
        public readonly Game G;
        public int TeamMarks, LivesLostTables;
        public Bot(Game g) { G = g; }
        public PlayerState P => G.State.Players[0];
        public bool OnFloor => G.State.Phase == Phase.Floor;
        public State S => G.State;

        public void Step(int n = 1)
        {
            for (int i = 0; i < n && OnFloor; i++)
            {
                G.Step();
                foreach (var e in G.DrainEvents())
                {
                    if (e.T == "mark" && e.Player == -1) TeamMarks++;
                    if (e.T == "life" && e.Delta < 0 && e.Why != "shortfall" && e.Why != "circuit") LivesLostTables++;
                }
            }
        }

        public void Wait(double s) => Step((int)Math.Round(s * State.TicksPerS));

        public int Limit(string t, bool std = true) { var l = Tables.LimitsOf(Tables.ById(t), S.Floor); return std ? l.Std : l.Min; }

        public void Sit(string table)
        {
            if (P.Seat == table) return;
            if (P.Seat != null) { G.Submit(0, Command.Stand()); Step(); }
            var pl = Layout.FloorOf(S.Floor).TableOf(table);
            double d = Math.Sqrt(Rules.Dist2(P.X, P.Z, pl.Stand));
            Wait(d / (MovementSystem.WalkSpeed * MovementSystem.Sprint));
            if (!OnFloor) return;
            P.X = pl.Stand.X; P.Z = pl.Stand.Z;
            G.Submit(0, Command.Sit(table));
            Step();
        }

        public bool CanBet(string t, int amount) => S.Bank >= amount;

        /// <summary>One round at the table you sit at, at that table's pace (DESIGN.md §3 "Round").</summary>
        public void Round(string table, string call = null, int? amount = null)
        {
            if (!OnFloor || P.Ghost) return;
            var def = Tables.ById(table);
            int t0 = G.Tick;
            double? a = amount;
            switch (table)
            {
                case "twentyone":
                    G.Submit(0, Command.Bet(table, a)); Step();
                    for (int k = 0; k < 20 && S.Tables["twentyone"].Hand?.Stage == "play"; k++)
                    {
                        var h = S.Tables["twentyone"].Hand;
                        var can = TwentyOne.Options(h);
                        string mv = TwentyOne.Advise(h.Hands[h.Active].Cards, TwentyOne.CardValue(h.Dealer[0]), can.dbl, can.split);
                        if ((mv == "double" || mv == "split") && S.Bank < h.Hands[h.Active].Bet) mv = TwentyOne.Advise(h.Hands[h.Active].Cards, TwentyOne.CardValue(h.Dealer[0]), false, false);
                        G.Submit(0, Command.CallIt(table, mv)); Step();
                    }
                    break;
                case "craps":
                    // a pass-line bet rolled to its end
                    G.Submit(0, Command.Spin(table, a)); Step();
                    for (int k = 0; k < 60 && S.Tables["craps"].Point != 0 && OnFloor; k++) { Wait(1.6); G.Submit(0, Command.Spin(table)); Step(); }
                    break;
                case "crash":
                    G.Submit(0, new Command { T = "spin", Table = table, Amount = a, Call = call });
                    Step();
                    for (int k = 0; k < 60 * 30 && S.Tables["crash"].Crash != null && OnFloor; k++) Step();
                    break;
                default:
                    G.Submit(0, call != null ? Command.CallIt(table, call, a) : Command.Spin(table, a)); Step();
                    break;
            }
            int left = (int)Math.Round(def.RoundS * State.TicksPerS) - (G.Tick - t0);
            Step(Math.Max(1, left));
        }

        public bool Marked(string id) => S.Marks.Any(m => m.Id == id && m.Player == 0);
        public int Stacks(string id) => S.Marks.FirstOrDefault(m => m.Id == id && m.Player == 0)?.Stacks ?? 0;
        public bool Made(int cushion = 300) => S.Bank >= S.Quota + cushion;

        public void Idle()
        {
            if (P.Seat != null) { G.Submit(0, Command.Stand()); Step(); }
            while (OnFloor) Step(600);
        }
    }

    // ---------------------------------------------------------------- strategies (per floor)

    public static void SafeOnly(Bot b)
    {
        if (b.S.Floor >= 2)
        {
            if (!b.CanBet("roulette", b.Limit("roulette", false))) { b.Idle(); return; }
            b.Sit("roulette");
            for (int i = 0; i < 6; i++) b.Round("roulette", "red", b.Limit("roulette", false));
            b.Sit("craps");
            for (int i = 0; i < 3; i++) b.Round("craps", null, b.Limit("craps", false));
            return;
        }
        if (!b.CanBet("bandit", b.Limit("bandit", false))) { b.Idle(); return; }
        b.Sit("bandit");
        for (int i = 0; i < 6; i++) b.Round("bandit", null, b.Limit("bandit", false));
        b.Sit("twentyone");
        for (int i = 0; i < 3; i++) b.Round("twentyone", null, b.Limit("twentyone", false));
    }

    /// <summary>Floor 1: Hi-Lo bursts (out at 3 stacks), the Wheel while marked. Floor 2: Hi-Lo bursts, Peg
    /// Drop while marked (and Hi-Lo while Pegged).</summary>
    public static void MidRotation(Bot b)
    {
        if (b.Made()) { b.Idle(); return; }
        string other = b.S.Floor >= 2 ? "pegdrop" : "wheel";
        int hb = b.Limit("hilo"), ob = b.Limit(other);
        if (!b.Marked("hilo") && b.CanBet("hilo", hb))
        {
            b.Sit("hilo");
            for (int i = 0; i < 5 && b.Stacks("hilo") < 3 && b.CanBet("hilo", hb) && !b.Made(); i++) b.Round("hilo", i % 2 == 1 ? "hi" : "lo");
        }
        if (b.CanBet(other, ob))
        {
            b.Sit(other);
            while (b.OnFloor && b.Marked("hilo") && b.Stacks("pegged") < 3 && b.CanBet(other, ob) && !b.Made()) b.Round(other);
        }
        else if (b.Marked("hilo")) b.Wait(2);
    }

    /// <summary>Mid rotation, plus up to two six-round high-table bursts when behind (past half the night,
    /// short, with a cushion): Red Line on floor 1, Crash at an auto 2x on floor 2.</summary>
    public static Action<Bot> HighWhenBehind()
    {
        int bursts = 0, night = 0;
        return b =>
        {
            if (b.S.Night != night) { night = b.S.Night; bursts = 0; }
            if (b.Made()) { b.Idle(); return; }
            var s = b.S;
            bool behind = s.Clock < State.NightTicks / 2 && s.Bank < s.Quota;
            string high = s.Floor >= 2 ? "crash" : "redline";
            int hb = b.Limit(high);
            if (bursts < 2 && behind && s.Bank >= hb * 4)
            {
                bursts++;
                b.Sit(high);
                for (int i = 0; i < 6 && b.CanBet(high, hb) && !b.P.Ghost && !b.Made(); i++)
                    b.Round(high, high == "crash" ? "2" : (i % 2 == 1 ? "red" : "black"));
            }
            MidRotation(b);
        };
    }

    /// <summary>Floor 2 only: Crash at a fixed auto cash-out, the standard stake.</summary>
    public static Action<Bot> CrashAt(double x) => b =>
    {
        if (b.S.Floor < 2 || b.Made()) { b.Idle(); return; }
        int hb = b.Limit("crash");
        if (!b.CanBet("crash", hb)) { b.Idle(); return; }
        b.Sit("crash");
        b.Round("crash", x.ToString(System.Globalization.CultureInfo.InvariantCulture));
    };

    // ---------------------------------------------------------------- a run

    public sealed class Result
    {
        public int NightsMade;      // paid nights (made the quota at 0:00)
        public bool WalkedOut;      // survived all six
        public int LastNight;
        public int Lives;
        public List<bool> Made = new List<bool>();
        public List<int> BankAtMidnight = new List<int>();
        public int TeamMarks, TableLives;
    }

    /// <summary>One run from `startFloor` for `nights` nights, the strategy on every floor night, always paying.
    /// It walks out if it reaches the last night's summary alive.</summary>
    public static Result Run(uint seed, Action<Bot> strategy, string curve = "easy", int startFloor = 1, int nights = 6)
    {
        var g = new Game(new GameOpts { Seed = seed, Players = 1, Curve = curve, StartFloor = startFloor });
        var b = new Bot(g);
        var r = new Result();
        int first = g.State.Night, last = Math.Min(Economy.Nights.I, first + nights - 1), recorded = 0;
        while (g.State.Phase != Phase.Over)
        {
            var s = g.State;
            if (s.Phase == Phase.Floor)
            {
                int t = g.Tick;
                strategy(b);
                if (g.Tick == t) b.Step(60);
                continue;
            }
            if (s.Phase == Phase.Midnight && recorded != s.Night)
            {
                recorded = s.Night;
                r.BankAtMidnight.Add(s.Bank);
                r.Made.Add(s.Bank >= s.Quota);
                if (s.Bank >= s.Quota) r.NightsMade++;
            }
            if (s.Phase == Phase.Choice) g.Submit(0, Command.Pay());
            if (s.Phase == Phase.Summary)
            {
                if (s.Night >= last) { r.WalkedOut = !s.Players[0].Ghost; break; }
                g.Submit(0, Command.Ready());
            }
            g.Step();
            g.DrainEvents();
        }
        r.LastNight = g.State.Night;
        r.Lives = g.State.Players[0].Lives;
        r.TeamMarks = b.TeamMarks;
        r.TableLives = b.LivesLostTables;
        return r;
    }
}
