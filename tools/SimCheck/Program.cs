// The sim's checks (CLAUDE.md, definition of done): the golden traces (the C# sim lands on the TS sim's
// state and events, tick by tick), replay determinism, and the data validation (derivations, RTPs,
// cross-references). Exit code 0 means every check passed.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Systems;
using Vig.Sim.Tables;

static class Program
{
    static int failures;
    static int passes;

    static void Check(bool ok, string what)
    {
        if (ok) passes++;
        else { failures++; Console.WriteLine("FAIL " + what); }
    }

    static int Main(string[] args)
    {
        string root = FindRoot();
        var only = args.FirstOrDefault();
        if (only == "regen") { Regen(Path.Combine(root, "tools", "golden", "traces")); return 0; }
        if (only == null || only == "golden") Golden(Path.Combine(root, "tools", "golden", "traces"));
        if (only == null || only == "fuzz") Fuzz();
        if (only == null || only == "replay") Replay();
        if (only == null || only == "data") DataChecks();
        if (only == null || only == "rules") RuleChecks();
        if (only == "pace") PaceReport(args.Length > 1 ? int.Parse(args[1]) : 100);
        if (only == null || only == "lessons") Lessons(100);
        Console.WriteLine($"{passes} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets", "Vig"))) d = d.Parent;
        return d?.FullName ?? throw new Exception("run inside the-vig-unity");
    }

    // ------------------------------------------------------------------ golden traces

    static Command CommandOf(JsonElement c)
    {
        var cmd = new Command { T = c.GetProperty("t").GetString() };
        foreach (var p in c.EnumerateObject())
        {
            switch (p.Name)
            {
                case "table": cmd.Table = p.Value.GetString(); break;
                case "amount": cmd.Amount = p.Value.GetDouble(); break;
                case "call": cmd.Call = p.Value.GetString(); break;
                case "item": cmd.Item = p.Value.GetString(); break;
                case "drink": cmd.Drink = p.Value.GetString(); break;
                case "circuit": cmd.Circuit = p.Value.GetInt32(); break;
                case "a": cmd.A = p.Value.GetInt32(); break;
                case "b": cmd.B = p.Value.GetInt32(); break;
                case "target": cmd.Target = p.Value.GetInt32(); break;
                case "mx": cmd.Mx = p.Value.GetDouble(); break;
                case "mz": cmd.Mz = p.Value.GetDouble(); break;
                case "yaw": cmd.Yaw = p.Value.GetDouble(); break;
                case "run": cmd.Run = p.Value.GetBoolean(); break;
            }
        }
        return cmd;
    }

    static void Golden(string dir)
    {
        var files = Directory.GetFiles(dir, "*.json.gz").OrderBy(f => f).ToList();
        Check(files.Count > 0, "golden: no traces in " + dir);
        foreach (var file in files)
        {
            JsonDocument doc;
            using (var gz = new GZipStream(File.OpenRead(file), CompressionMode.Decompress))
                doc = JsonDocument.Parse(gz);
            var r = doc.RootElement;
            var cfg = r.GetProperty("config");
            var opts = new GameOpts
            {
                Seed = cfg.GetProperty("seed").GetUInt32(),
                Players = cfg.GetProperty("players").GetInt32(),
                Home = cfg.GetProperty("home").GetBoolean(),
                Upgrades = cfg.GetProperty("upgrades").EnumerateArray().Select(x => x.GetString()).ToList(),
                Unlocked = cfg.GetProperty("unlocked").EnumerateArray().Select(x => x.GetString()).ToList(),
            };
            var actions = r.GetProperty("actions").EnumerateArray().ToList();
            var lines = r.GetProperty("lines").EnumerateArray().Select(x => x.GetString()).ToList();
            int ticks = r.GetProperty("ticks").GetInt32();

            var g = new Game(opts);
            int ai = 0, li = 0;
            string name = Path.GetFileName(file);
            bool ok = true;
            while (g.Tick < ticks && ok)
            {
                while (ai < actions.Count && actions[ai].GetProperty("tick").GetInt32() == g.Tick)
                {
                    var a = actions[ai++];
                    int pid = a.GetProperty("player").GetInt32();
                    if (a.TryGetProperty("tp", out var tp))
                    {
                        g.State.Players[pid].X = tp[0].GetDouble();
                        g.State.Players[pid].Z = tp[1].GetDouble();
                    }
                    if (a.TryGetProperty("cmd", out var c)) g.Submit(pid, CommandOf(c));
                }
                g.Step();
                var ev = g.DrainEvents();
                int tick = g.Tick - 1;
                if (li < lines.Count && int.Parse(lines[li].Substring(0, lines[li].IndexOf('|'))) == tick)
                {
                    var want = lines[li++].Split('|', 4);
                    string st = Canon.Of(g.State);
                    string got = $"{tick}|{g.Rng.S}|{Js.HashStr(st):x}|{Canon.Of(ev)}";
                    string wantS = string.Join("|", want);
                    if (got != wantS)
                    {
                        ok = false;
                        Console.WriteLine($"{name}: diverged at tick {tick}");
                        Console.WriteLine("  want " + Clip(wantS));
                        Console.WriteLine("  got  " + Clip(got));
                        Console.WriteLine("  state " + Clip(st, 1500));
                    }
                }
                else if (ev.Count > 0)
                {
                    ok = false;
                    Console.WriteLine($"{name}: tick {tick} emitted events the TS sim didn't: {Clip(Canon.Of(ev))}");
                }
            }
            if (ok && li != lines.Count) { ok = false; Console.WriteLine($"{name}: stopped with {lines.Count - li} trace lines unchecked"); }
            Check(ok, "golden " + name);
            if (ok) Console.WriteLine($"ok   golden {name}: {ticks} ticks, {lines.Count} checkpoints, ended {g.State.Phase} night {g.State.Night}");
        }
    }

    /// <summary>Rewrite each trace's expected lines from the C# sim, replaying its recorded commands (U11:
    /// after a deliberate rule change). The commands stay; the lines become the C# sim's.</summary>
    static void Regen(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*.json.gz").OrderBy(f => f))
        {
            JsonDocument doc;
            using (var gz = new GZipStream(File.OpenRead(file), CompressionMode.Decompress)) doc = JsonDocument.Parse(gz);
            var r = doc.RootElement;
            var cfg = r.GetProperty("config");
            var opts = new GameOpts
            {
                Seed = cfg.GetProperty("seed").GetUInt32(),
                Players = cfg.GetProperty("players").GetInt32(),
                Home = cfg.GetProperty("home").GetBoolean(),
                Upgrades = cfg.GetProperty("upgrades").EnumerateArray().Select(x => x.GetString()).ToList(),
                Unlocked = cfg.GetProperty("unlocked").EnumerateArray().Select(x => x.GetString()).ToList(),
            };
            var actions = r.GetProperty("actions").EnumerateArray().ToList();
            int ticks = r.GetProperty("ticks").GetInt32();
            var g = new Game(opts);
            var lines = new List<string>();
            int ai = 0;
            while (g.Tick < ticks)
            {
                bool acted = false;
                while (ai < actions.Count && actions[ai].GetProperty("tick").GetInt32() == g.Tick)
                {
                    var a = actions[ai++];
                    acted = true;
                    int pid = a.GetProperty("player").GetInt32();
                    if (a.TryGetProperty("tp", out var tp)) { g.State.Players[pid].X = tp[0].GetDouble(); g.State.Players[pid].Z = tp[1].GetDouble(); }
                    if (a.TryGetProperty("cmd", out var c)) g.Submit(pid, CommandOf(c));
                }
                g.Step();
                var ev = g.DrainEvents();
                if (ev.Count > 0 || acted || g.Tick % 600 == 0)
                    lines.Add($"{g.Tick - 1}|{g.Rng.S}|{Js.HashStr(Canon.Of(g.State)):x}|{Canon.Of(ev)}");
            }
            using (var fs = File.Create(file))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var w = new Utf8JsonWriter(gz))
            {
                w.WriteStartObject();
                w.WritePropertyName("config"); cfg.WriteTo(w);
                w.WriteString("source", "csharp");
                w.WriteNumber("ticks", ticks);
                w.WritePropertyName("actions"); r.GetProperty("actions").WriteTo(w);
                w.WriteStartArray("lines"); foreach (var l in lines) w.WriteStringValue(l); w.WriteEndArray();
                w.WriteEndObject();
            }
            Console.WriteLine($"regen {Path.GetFileName(file)}: {lines.Count} lines, ended {g.State.Phase} night {g.State.Night} floor {g.State.Floor}");
        }
    }

    // ------------------------------------------------------------------ fuzz: random play through every game

    /// <summary>Random commands at every table on both floors, through whole runs: no exception, the bank
    /// never below zero, and the command log replays to the same hash.</summary>
    static void Fuzz()
    {
        int rounds = 0, runs = 0;
        var seen = new HashSet<string>();
        for (uint seed = 1; seed <= 24; seed++)
        {
            var opts = new GameOpts { Seed = seed * 977, Players = seed % 4 == 0 ? 2 : 1, StartFloor = seed % 2 == 0 ? 2 : 1, Curve = seed % 3 == 0 ? "house" : "easy", Home = seed % 5 == 0,
                Unlocked = new[] { "insulated", "snake" } };
            var g = new Game(opts);
            var d = new Rng(seed);
            string fail = null;
            try
            {
                while (g.Tick < 160000 && g.State.Phase != Phase.Over)
                {
                    var s = g.State;
                    for (int pid = 0; pid < s.Players.Count; pid++)
                    {
                        if (d.Next() > 0.06) continue;
                        var p = s.Players[pid];
                        var floor = Layout.FloorOf(s.Floor);
                        switch (s.Phase)
                        {
                            case Phase.Home:
                            {
                                double r = d.Next();
                                if (r < 0.3) { p.X = Home.ComputerStand.X; p.Z = Home.ComputerStand.Z; g.Submit(pid, d.Next() < 0.2 ? Command.Reroll() : Command.Buy(s.Shop.Count > 0 ? d.Pick(s.Shop) : "x")); }
                                else if (r < 0.5) { p.X = Home.MailboxStand.X; p.Z = Home.MailboxStand.Z; g.Submit(pid, Command.Pickup()); }
                                else { p.X = Home.CarStand.X; p.Z = Home.CarStand.Z; g.Submit(pid, Command.Drive()); }
                                break;
                            }
                            case Phase.Floor:
                            {
                                if (p.Seat == null)
                                {
                                    if (d.Next() < 0.15) { p.X = floor.BarStand.X; p.Z = floor.BarStand.Z; g.Submit(pid, Command.Order(d.Pick(Drinks.All).Id)); break; }
                                    var t = d.Pick(floor.Tables);
                                    p.X = t.Stand.X; p.Z = t.Stand.Z;
                                    g.Submit(pid, Command.Sit(t.Table));
                                    break;
                                }
                                string id = p.Seat;
                                seen.Add(id);
                                double? amt = d.Next() < 0.5 ? (double?)null : d.Int(1, 30) * 50;
                                Command c;
                                switch (id)
                                {
                                    case "hilo": c = Command.CallIt(id, d.Pick(new[] { "hi", "lo" }), amt); break;
                                    case "redline": c = Command.CallIt(id, d.Pick(new[] { "red", "black" }), amt); break;
                                    case "roulette": c = Command.CallIt(id, d.Pick(new[] { "red", "black", "odd", "even", "low", "high", "d1", "d2", "d3", "n0", "n17", "n36", "bogus" }), amt); break;
                                    case "twentyone": c = s.Tables[id].Hand?.Stage == "play" ? Command.CallIt(id, d.Pick(new[] { "hit", "stand", "double", "split" })) : new Command { T = "bet", Table = id, Amount = amt, Call = d.Next() < 0.3 ? "pairs" : null }; break;
                                    case "crash": c = s.Tables[id].Crash != null ? Command.CallIt(id, "cash") : new Command { T = "spin", Table = id, Amount = amt, Call = d.Next() < 0.5 ? "3" : null }; break;
                                    case "craps": c = new Command { T = "spin", Table = id, Amount = amt, Call = d.Next() < 0.4 ? d.Pick(new[] { "field", "anycraps", "eleven", "bogus" }) : null }; break;
                                    default: c = Command.Spin(id, amt); break;
                                }
                                double r2 = d.Next();
                                if (r2 < 0.08) c = Command.Ride(id);
                                else if (r2 < 0.12) c = Command.Stand();
                                if (s.Bank < 400) s.Bank += 2000;
                                g.Submit(pid, c);
                                rounds++;
                                break;
                            }
                            case Phase.Choice: g.Submit(pid, d.Next() < 0.5 ? Command.Pay() : Command.Gamble()); break;
                            case Phase.Circuit:
                                if (s.Circuit.Turn == "team") { var up = CircuitPlay.Up(s.Circuit); if (up.Count > 0) g.Submit(pid, Command.Activate(d.Pick(up))); }
                                break;
                            case Phase.Wheel: g.Submit(pid, Command.Fortune()); break;
                            case Phase.Summary: g.Submit(pid, Command.Ready()); break;
                        }
                    }
                    g.Step();
                    g.DrainEvents();
                    if (g.State.Bank < 0) { fail = $"bank {g.State.Bank} at tick {g.Tick}"; break; }
                }
            }
            catch (Exception e) { fail = e.GetType().Name + ": " + e.Message + " " + (e.StackTrace ?? "").Split('\n').FirstOrDefault(); }
            Check(fail == null, $"fuzz seed {seed}: {fail}");
            runs++;
        }
        Check(seen.IsSupersetOf(new[] { "roulette", "craps", "pegdrop", "crash", "bandit", "twentyone", "hilo", "wheel", "redline", "deadman" }), "fuzz: didn't reach every table: " + string.Join(",", seen));
        Console.WriteLine($"ok   fuzz: {runs} runs, {rounds} commands at the tables, tables reached {seen.Count}");
    }

    static string Clip(string s, int n = 400) => s.Length <= n ? s : s.Substring(0, n) + "…";

    // ------------------------------------------------------------------ replay determinism

    static void Replay()
    {
        // a night of scripted play: the same command log from a fresh game reproduces the hash
        var opts = new GameOpts { Seed = 4242, Players = 2 };
        var g = new Game(opts);
        var rng = new Rng(7);
        string[] tables = State.TableIds;
        while (g.Tick < 30000 && g.State.Phase != Phase.Over)
        {
            if (g.Tick % 97 == 0)
            {
                int pid = rng.Int(0, 1);
                var t = rng.Pick(tables);
                var c = rng.Next() < 0.5 ? Command.Move(rng.Int(-1000, 1000), rng.Int(-1000, 1000), 0) :
                    rng.Next() < 0.5 ? Command.Sit(t) : rng.Next() < 0.5 ? Command.Spin(t) : Command.CallIt(t, rng.Pick(new[] { "hi", "lo", "red", "black", "hit", "stand" }));
                g.Submit(pid, c);
            }
            if (g.State.Phase == Phase.Choice) g.Submit(0, Command.Gamble());
            if (g.State.Phase == Phase.Circuit) g.Submit(0, Command.Activate(rng.Int(0, 9)));
            if (g.State.Phase == Phase.Wheel) g.Submit(0, Command.Fortune());
            if (g.State.Phase == Phase.Summary) g.Submit(0, Command.Ready());
            g.Step();
            g.DrainEvents();
        }
        var again = Game.Replay(opts, g.Log, g.Tick);
        Check(again.Hash() == g.Hash(), $"replay: hash {again.Hash()} != {g.Hash()}");
        Check(g.Log.Count > 100, "replay: the scripted night applied too few commands");
        Console.WriteLine($"ok   replay: {g.Log.Count} commands over {g.Tick} ticks reproduce hash {g.Hash()}");
    }

    // ------------------------------------------------------------------ rule checks

    /// <summary>The Boon wheel's charm slot gives `charm` count of each charm over many spins, from
    /// a player wearing two charms, with `unlocked` in the pool.</summary>
    static Dictionary<string, int> BoonCharms(string[] unlocked)
    {
        var got = new Dictionary<string, int>();
        for (uint seed = 1; seed <= 600; seed++)
        {
            var g = new Game(new GameOpts { Seed = seed, Unlocked = unlocked });
            var s = g.State;
            s.Players[0].Charms.AddRange(new[] { "luckyfuse", "overclock" });
            s.Phase = Phase.Wheel;
            s.Wheel = new WheelState { Kind = "boon" };
            s.Summary = new NightSummary { How = "kept" };
            g.Submit(0, Command.Fortune());
            for (int k = 0; k < 400 && !s.Wheel.Applied; k++) g.Step();
            if (s.Wheel.Slot != 3) continue;
            var c = s.Players[0].Charms.Last();
            got[c] = got.TryGetValue(c, out var n) ? n + 1 : 1;
        }
        return got;
    }

    static void RuleChecks()
    {
        RegularsCheck(100000);
        OneRollCheck(40000);
        CharmMath();
        // DECISIONS U5: a locked charm can't come off the Boon wheel; an unlocked one can
        var locked = BoonCharms(new string[0]);
        Check(locked.Count > 0, "rules: no spin landed on the Boon wheel's charm slot");
        Check(!locked.ContainsKey("insulated"), $"rules: the Boon wheel gave the locked Insulated Kit {(locked.TryGetValue("insulated", out var n) ? n : 0)} times");
        var open = BoonCharms(new[] { "insulated" });
        Check(open.ContainsKey("insulated"), "rules: the Boon wheel never gives the Insulated Kit once it's unlocked");
        Console.WriteLine($"ok   rules: Boon charm slot locked {string.Join(", ", locked.Select(kv => kv.Key + " " + kv.Value))}; unlocked {string.Join(", ", open.Select(kv => kv.Key + " " + kv.Value))}");
    }

    // ------------------------------------------------------------------ the pace report (DESIGN.md §8)

    static readonly uint[] PaceSeeds = Enumerable.Range(0, 400).Select(i => (uint)(1000 + i * 7919)).ToArray();

    static void PaceReport(int n)
    {
        var seeds = PaceSeeds.Take(n).ToArray();
        void Row(string name, Func<uint, Pace.Result> run)
        {
            var rs = seeds.Select(run).ToList();
            double n1 = rs.Count(r => r.Made.Count > 0 && r.Made[0]) / (double)rs.Count;
            double made = rs.Average(r => r.NightsMade);
            double walk = rs.Count(r => r.WalkedOut) / (double)rs.Count;
            double bank1 = rs.Where(r => r.BankAtMidnight.Count > 0).Average(r => r.BankAtMidnight[0]);
            double lives = rs.Average(r => r.TableLives);
            Console.WriteLine($"  {name,-34} first night made {n1,5:P0}   nights made {made,4:0.0}   walks out {walk,5:P0}   first-midnight bank ${bank1,6:0}   lives lost at tables {lives:0.00}");
        }
        foreach (var curve in new[] { "easy", "house" })
        {
            Console.WriteLine($"{curve} curve, a whole run from floor 1 ({n} seeds, always paying):");
            Row("safe-only", sd => Pace.Run(sd, Pace.SafeOnly, curve));
            Row("mid rotation", sd => Pace.Run(sd, Pace.MidRotation, curve));
            Row("mid + high bursts when behind", sd => Pace.Run(sd, Pace.HighWhenBehind(), curve));
        }
        Console.WriteLine($"floor 2 alone, nights 4-6, easy curve ({n} seeds):");
        Row("safe-only (roulette, craps)", sd => Pace.Run(sd, Pace.SafeOnly, "easy", 2, 3));
        Row("mid rotation (hi-lo, peg drop)", sd => Pace.Run(sd, Pace.MidRotation, "easy", 2, 3));
        Row("mid + crash bursts at 2x", sd => Pace.Run(sd, Pace.HighWhenBehind(), "easy", 2, 3));
        foreach (var x in new[] { 1.5, 2.0, 3.0, 5.0, 10.0 }) Row($"crash only, auto at {x}x", sd => Pace.Run(sd, Pace.CrashAt(x), "easy", 2, 3));
    }

    /// <summary>The lessons the numbers must keep teaching (DESIGN.md §8), on 100 seeds.</summary>
    static void Lessons(int n)
    {
        var seeds = PaceSeeds.Take(n).ToArray();
        double Rate(IEnumerable<Pace.Result> rs, Func<Pace.Result, bool> f) { var l = rs.ToList(); return l.Count(f) / (double)l.Count; }
        bool Night1(Pace.Result r) => r.Made.Count > 0 && r.Made[0];
        var safe1 = seeds.Select(sd => Pace.Run(sd, Pace.SafeOnly, "easy", 1, 1)).ToList();
        var mid1 = seeds.Select(sd => Pace.Run(sd, Pace.MidRotation, "easy")).ToList();
        var midHouse = seeds.Select(sd => Pace.Run(sd, Pace.MidRotation, "house")).ToList();
        var safe2 = seeds.Select(sd => Pace.Run(sd, Pace.SafeOnly, "easy", 2, 1)).ToList();
        var mid2 = seeds.Select(sd => Pace.Run(sd, Pace.MidRotation, "easy", 2, 3)).ToList();
        var crash2 = seeds.Select(sd => Pace.Run(sd, Pace.CrashAt(2), "easy", 2, 3)).ToList();
        var crash10 = seeds.Select(sd => Pace.Run(sd, Pace.CrashAt(10), "easy", 2, 3)).ToList();
        Check(Rate(safe1, Night1) <= 0.15, $"lesson 1: safe-only makes night 1 {Rate(safe1, Night1):P0} (at most 15%)");
        Check(Rate(safe2, Night1) <= 0.15, $"lesson 1b: safe-only makes night 4 on floor 2 {Rate(safe2, Night1):P0} (at most 15%)");
        Check(Rate(mid1, Night1) >= 0.5, $"lesson 2: the mid rotation makes night 1 {Rate(mid1, Night1):P0} (at least 50%)");
        Check(Rate(mid1, r => r.WalkedOut) >= 0.25, $"lesson 8 (U26): on the easy curve the mid rotation walks out of the run {Rate(mid1, r => r.WalkedOut):P0} (at least 25%)");
        Check(Rate(midHouse, r => r.WalkedOut) <= 0.1, $"lesson 9: on house rules the mid rotation alone walks out {Rate(midHouse, r => r.WalkedOut):P0} (at most 10%: the curve forces blood)");
        Check(Rate(mid2, Night1) >= 0.4, $"lesson 10: the floor-2 rotation makes night 4 {Rate(mid2, Night1):P0} (at least 40%)");
        Check(crash10.Average(r => r.TableLives) > crash2.Average(r => r.TableLives), "lesson 11: riding Crash to 10x costs more lives than cashing at 2x");
        Console.WriteLine($"ok   lessons: night 1 safe {Rate(safe1, Night1):P0} / mid {Rate(mid1, Night1):P0}; walks out easy {Rate(mid1, r => r.WalkedOut):P0} / house {Rate(midHouse, r => r.WalkedOut):P0}; floor 2 night 4 safe {Rate(safe2, Night1):P0} / mid {Rate(mid2, Night1):P0}; crash lives 2x {crash2.Average(r => r.TableLives):0.00} / 10x {crash10.Average(r => r.TableLives):0.00}");
    }

    /// <summary>U31: the third set's numbers, enumerated, match their derivations (DESIGN.md §15).</summary>
    static void CharmMath()
    {
        double pin = 0;
        for (int k = 0; k <= PegDrop.Rows; k++)
        {
            double ways = 1; for (int i = 0; i < k; i++) ways = ways * (PegDrop.Rows - i) / (i + 1);
            pin += ways / 256 * (k == 4 ? 1 : PegDrop.Mult[k]);
        }
        var craps = (Craps)TableModule.ById("craps");
        double shooter = craps.Odds().Rtp + 4 / 36.0;
        double vision = SideBets.ExactRtp(SideBets.PairsVision);
        Check(Math.Abs(pin - 342 / 256.0) < 1e-9, $"charm math: Center Pin's Peg Drop returns {pin:0.0000}, not 1.336");
        Check(Math.Abs(shooter - 1.097) < 0.0005, $"charm math: Hot Shooter's craps returns {shooter:0.0000}, not 1.097");
        Check(Math.Abs(vision - 359 / 311.0) < 1e-9, $"charm math: Double Vision's PAIRS returns {vision:0.0000}, not 1.154");
        Console.WriteLine($"ok   charm math: Center Pin {pin:0.000}, Hot Shooter {shooter:0.000}, Double Vision {vision:0.000}, Tailwind at 3x {3 * Crash.Reach(3) * 1.1:0.000}");
    }

    /// <summary>U34: craps' one-roll bets, thrown n times each beside the pass line on floor 2. Every bet's
    /// return is checked against the dice it was judged on, and measured against its printed 34/36.</summary>
    static void OneRollCheck(int n)
    {
        foreach (var d in SideBets.OneRoll)
        {
            double exact = SideBets.ExactRtp(d);
            Check(Derived(d.Derivation), $"data: the one-roll bet {d.Id} lacks a derivation");
            Check(exact < 1, $"data: {d.Name} returns {exact:0.000}: a side bet at a low table must be a slow loss");
            Check(Math.Abs(exact - 34 / 36.0) < 1e-9, $"data: {d.Name} returns {exact:0.0000}, not the 34/36 its derivation says");
            var g = new Game(new GameOpts { Seed = 3400u + (uint)Array.IndexOf(SideBets.OneRoll, d), Players = 1, StartFloor = 2 });
            var b = new Pace.Bot(g);
            b.Sit("craps");
            var t = g.State.Tables["craps"];
            int side = d.Mins * Vig.Sim.Data.Tables.LimitsOf(Vig.Sim.Data.Tables.ById("craps"), g.State.Floor).Min;
            double staked = 0, back = 0;
            int throws = 0, alone = 0, wrong = 0;
            while (throws < n)
            {
                g.State.Clock = State.NightTicks;
                g.State.Bank = 1000000;
                bool pointOn = t.Point != 0;
                int lineBet = t.PointBet;
                g.Submit(0, new Command { T = "spin", Table = "craps", Call = d.Id }); g.Step();
                var o = t.Pending;
                if (o == null) { Check(false, $"one-roll: a throw with {d.Name} riding landed nothing"); return; }
                TablesSystem.Land(g, "craps");
                g.DrainEvents();
                int sum = o.Dice[0] + o.Dice[1];
                int due = d.Pays[sum] > 0 ? Js.RoundInt(side * (d.Pays[sum] + 1)) : 0;
                if (o.Bet == side && t.Point != 0) { alone++; if (o.Ret != due) wrong++; }
                else
                {
                    // the line settled on this throw: what's left of the return after the side bet is the line's
                    int line = o.Ret - due, stake = o.Bet - side;
                    if (line != 0 && line != stake && line != 2 * stake) wrong++;
                    if (pointOn && stake != lineBet) wrong++;
                }
                staked += side; back += due; throws++;
            }
            // one throw's spread per $1, from the bet's own pays: four of them over n throws is the tolerance
            double sq = 0; for (int k = 2; k <= 12; k++) sq += (6 - Math.Abs(k - 7)) / 36.0 * (d.Pays[k] > 0 ? (d.Pays[k] + 1) * (d.Pays[k] + 1) : 0);
            double rtp = back / staked, tol = 4 * Math.Sqrt(sq - exact * exact) / Math.Sqrt(n);
            Check(wrong == 0, $"one-roll: {d.Name} paid the wrong amount on {wrong} of {n} throws");
            Check(alone > n / 3, $"one-roll: {d.Name} rode only {alone} throws while the point was on");
            Check(Math.Abs(rtp - exact) < tol, $"one-roll: {d.Name} returned {rtp:0.000} over {n} throws, not the printed {exact:0.000} (±{tol:0.000})");
            Console.WriteLine($"ok   one-roll: {d.Name} {rtp:0.000} over {n} throws (printed {exact:0.000} ±{tol:0.000}, wins {SideBets.WinP(d):P1}; {alone} on a point)");
        }
    }

    /// <summary>U29: the regulars share your shoe but not your odds. Play `n` hands of basic strategy with them
    /// at the table and measure your return against the exact one; report theirs (DESIGN.md §13).</summary>
    static void RegularsCheck(int n)
    {
        var g = new Game(new GameOpts { Seed = 2910, Players = 1 });
        var b = new Pace.Bot(g);
        b.Sit("twentyone");
        var t = g.State.Tables["twentyone"];
        double bet = 0, ret = 0, sideBet = 0, sideRet = 0;
        var rb = new Dictionary<string, double>(); var rr = new Dictionary<string, double>();
        int hands = 0, dealerDrewForThem = 0, tableWins = 0;
        while (hands < n)
        {
            g.State.Clock = State.NightTicks;
            g.State.Bank = 1000000;
            int before = t.Rounds;
            g.Submit(0, new Command { T = "bet", Table = "twentyone", Amount = b.Limit("twentyone", false), Call = "pairs" }); g.Step();
            sideBet += t.Hand.Side; sideRet += t.Hand.SideRet;
            for (int k = 0; k < 20 && t.Hand?.Stage == "play"; k++)
            {
                var h = t.Hand;
                var can = TwentyOne.Options(h);
                g.Submit(0, Command.CallIt("twentyone", TwentyOne.Advise(h.Hands[h.Active].Cards, TwentyOne.CardValue(h.Dealer[0]), can.dbl, can.split))); g.Step();
            }
            for (int k = 0; k < 2000 && t.Rounds == before; k++) g.Step();
            g.DrainEvents();
            var o = t.Last;
            if (o == null || t.Rounds == before) { Check(false, "regulars: a hand never settled"); return; }
            bet += o.Hands21.Sum(hr => hr.Bet); ret += o.Hands21.Sum(hr => hr.Ret); hands++;
            bool youOut = o.Hands21.All(hr => TwentyOne.HandValue(hr.Cards).total > 21) || (o.Hands21.Count == 1 && TwentyOne.IsNatural(o.Hands21[0].Cards));
            if (youOut && o.Dealer21.Count > 2) dealerDrewForThem++;
            if (TwentyOne.TableWins(o)) tableWins++;
            foreach (var r in o.Regulars21 ?? new List<RegularHand21>())
            {
                rb[r.Who] = (rb.TryGetValue(r.Who, out var x) ? x : 0) + r.Bet;
                rr[r.Who] = (rr.TryGetValue(r.Who, out var y) ? y : 0) + r.Ret;
            }
        }
        double rtp = ret / bet, exact = TwentyOne.ExactRtp();
        // one hand's return has a spread of about 1.15 per $1; four standard errors either side
        double tol = 4 * 1.15 / Math.Sqrt(n);
        Check(Math.Abs(rtp - exact) < tol, $"regulars: your return with them at the table {rtp:0.000} is not the printed {exact:0.000} (±{tol:0.000})");
        Check(rb.Count == Regulars.All.Length, "regulars: every regular played");
        foreach (var d in Regulars.All) Check(Derived(d.Derivation), $"data: regular {d.Id} lacks a derivation");
        // U30: PAIRS, measured on the same hands (one bet's spread is about 4 per $1)
        double side = sideRet / sideBet, sideExact = SideBets.ExactRtp(SideBets.Pairs), sideTol = 4 * 4.0 / Math.Sqrt(n);
        Check(Derived(SideBets.Pairs.Derivation), "data: the PAIRS side bet lacks a derivation");
        Check(sideExact < 1, $"data: PAIRS returns {sideExact:0.000}: a side bet at a low table must be a slow loss");
        Check(Math.Abs(side - sideExact) < sideTol, $"side bet: PAIRS returned {side:0.000} over {n} hands, not the printed {sideExact:0.000} (±{sideTol:0.000})");
        Console.WriteLine($"ok   side bet: PAIRS {side:0.000} over {n} hands (printed {sideExact:0.000} ±{sideTol:0.000}, wins {SideBets.WinP(SideBets.Pairs):P1})");
        Console.WriteLine($"ok   regulars: {n} hands, yours {rtp:0.000} (printed {exact:0.000} ±{tol:0.000}); " +
            string.Join(", ", Regulars.All.Select(d => $"{d.Name} ({d.Style}) {rr[d.Id] / rb[d.Id]:0.000}")) + $"; the dealer drew for them {dealerDrewForThem} times; the whole table won on a bust 1 hand in {n / (double)Math.Max(1, tableWins):0.0}");
    }

    // ------------------------------------------------------------------ data validation

    static bool Derived(string d) => !string.IsNullOrWhiteSpace(d) && d.Contains("DESIGN.md §");

    static void DataChecks()
    {
        foreach (var n in Economy.All) Check(Derived(n.Derivation), $"data: economy number {n.V} lacks a DESIGN.md derivation");
        foreach (var t in Vig.Sim.Data.Tables.All)
        {
            Check(Derived(t.Derivation), $"data: table {t.Id} lacks a derivation");
            var mod = TableModule.ById(t.Id);
            Check(mod != null, $"data: table {t.Id} has no module");
            if (mod == null) continue;
            var odds = mod.Odds();
            Check(Math.Abs(odds.Rtp - t.Rtp) / t.Rtp < 0.005, $"data: {t.Id} prints RTP {t.Rtp}, its outcomes enumerate to {odds.Rtp:F4}");
            if (t.Pays != null) Check(Math.Abs(t.Pays.Value * t.WinP - t.Rtp) / t.Rtp < 0.005, $"data: {t.Id} pays x p = {t.Pays * t.WinP} != RTP {t.Rtp}");
            Check(t.Bet.Std >= t.Bet.Min && t.Bet.Std <= t.Bet.Max && (t.Bet.Std - t.Bet.Min) % t.Bet.Step == 0, $"data: {t.Id} standard bet off its steps");
            Check(t.ResolveS < t.RoundS, $"data: {t.Id} resolves slower than a round");
            Check(new[] { 1, 2 }.Any(f => Vig.Sim.Data.Tables.OnFloor(f).Contains(t.Id)), $"data: table {t.Id} is on no floor");
            foreach (int f in new[] { 1, 2 }) if (Vig.Sim.Data.Tables.OnFloor(f).Contains(t.Id)) Check(Layout.FloorOf(f).TableOf(t.Id) != null, $"data: table {t.Id} isn't placed on floor {f}");
            Console.WriteLine($"ok   {t.Id,-10} tier {t.Tier,-4} RTP {t.Rtp} (enumerated {odds.Rtp:F4}), win {odds.WinP:F3}");
        }
        // the tier rule: RTP rises with the tier (PLAN pillar 1)
        double MaxRtp(string tier) => Vig.Sim.Data.Tables.All.Where(t => t.Tier == tier).Max(t => t.Rtp);
        double MinRtp(string tier) => Vig.Sim.Data.Tables.All.Where(t => t.Tier == tier).Min(t => t.Rtp);
        Check(MaxRtp(Tier.Low) < 1, "data: a low table pays above 1");
        Check(MaxRtp(Tier.Low) < MinRtp(Tier.Mid) && MaxRtp(Tier.Mid) < MinRtp(Tier.High), "data: RTP doesn't rise with the tier");
        foreach (var m in Marks.All) Check(Derived(m.Derivation), $"data: mark {m.Id} lacks a derivation");
        foreach (var c in Charms.All)
        {
            Check(Derived(c.Derivation), $"data: charm {c.Id} lacks a derivation");
            Check(c.Systems.Length >= 1, $"data: charm {c.Id} declares no systems (DECISIONS #85)");
            Check(c.Unlock == null || c.Unlock < Collectors.All.Length, $"data: charm {c.Id} unlocks from a Collector who doesn't exist");
        }
        foreach (var u in Charms.Upgrades) Check(Derived(u.Derivation), $"data: upgrade {u.Id} lacks a derivation");
        foreach (var s in Wheels.Boons.Concat(Wheels.Banes)) Check(Derived(s.Derivation), $"data: wheel slot {s.Id} lacks a derivation");
        Check(Wheels.Boons.Length == 6 && Wheels.Banes.Length == 6, "data: each wheel has six slots (fortune rolls 0..5)");
        foreach (var r in Circuit.Rules) Check(Derived(r.Derivation) && r.Live < r.Levers, $"data: circuit rules floor {r.Floor}");
        foreach (var d in Drinks.All) Check(Derived(d.Derivation), $"data: drink {d.Id} lacks a derivation");
        foreach (var c in Collectors.All) Check(Derived(c.Derivation) && (c.Unlocks == null || Charms.ById(c.Unlocks) != null), $"data: collector {c.Id}");
        foreach (var e in Collectors.Endings) Check(Derived(e.Derivation), $"data: ending {e.Id} lacks a derivation");
        Check(Derived(Home.PostS.Derivation), "data: the post lacks a derivation");
        // the layout: every stand point is clear of the colliders, or you couldn't reach it
        foreach (var f in new[] { Layout.Floor1, Layout.Floor2 })
        foreach (var t in f.Tables)
        {
            var (x, z) = Vig.Sim.Systems.MovementSystem.Collide(t.Stand.X, t.Stand.Z, Layout.PlayerR, f.Colliders);
            Check(Math.Abs(x - t.Stand.X) < 0.35 && Math.Abs(z - t.Stand.Z) < 0.35, $"layout: {t.Table}'s stand point is inside a collider");
        }
        Console.WriteLine($"ok   data: {Vig.Sim.Data.Tables.All.Length} tables, {Charms.All.Length} charms, {Layout.Floor1.Colliders.Length} + {Layout.Floor2.Colliders.Length} floor colliders");
    }
}
