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
        if (only == null || only == "golden") Golden(Path.Combine(root, "tools", "golden", "traces"));
        if (only == null || only == "replay") Replay();
        if (only == null || only == "data") DataChecks();
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
            Check(Layout.Floor1.TableOf(t.Id) != null, $"data: table {t.Id} isn't placed on floor 1");
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
        var f = Layout.Floor1;
        foreach (var t in f.Tables)
        {
            var (x, z) = Vig.Sim.Systems.MovementSystem.Collide(t.Stand.X, t.Stand.Z, Layout.PlayerR, f.Colliders);
            Check(Math.Abs(x - t.Stand.X) < 0.35 && Math.Abs(z - t.Stand.Z) < 0.35, $"layout: {t.Table}'s stand point is inside a collider");
        }
        Console.WriteLine($"ok   data: {Vig.Sim.Data.Tables.All.Length} tables, {Charms.All.Length} charms, {f.Colliders.Length} floor colliders");
    }
}
