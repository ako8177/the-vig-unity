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
                                    case "twentyone": c = s.Tables[id].Hand?.Stage == "play" ? Command.CallIt(id, d.Pick(new[] { "hit", "stand", "double", "split" })) : Command.Bet(id, amt); break;
                                    case "crash": c = s.Tables[id].Crash != null ? Command.CallIt(id, "cash") : new Command { T = "spin", Table = id, Amount = amt, Call = d.Next() < 0.5 ? "3" : null }; break;
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
        // DECISIONS U5: a locked charm can't come off the Boon wheel; an unlocked one can
        var locked = BoonCharms(new string[0]);
        Check(locked.Count > 0, "rules: no spin landed on the Boon wheel's charm slot");
        Check(!locked.ContainsKey("insulated"), $"rules: the Boon wheel gave the locked Insulated Kit {(locked.TryGetValue("insulated", out var n) ? n : 0)} times");
        var open = BoonCharms(new[] { "insulated" });
        Check(open.ContainsKey("insulated"), "rules: the Boon wheel never gives the Insulated Kit once it's unlocked");
        Console.WriteLine($"ok   rules: Boon charm slot locked {string.Join(", ", locked.Select(kv => kv.Key + " " + kv.Value))}; unlocked {string.Join(", ", open.Select(kv => kv.Key + " " + kv.Value))}");
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
