// The sim: pure, deterministic, command-driven. It never sees input, rendering, audio, wall-clock time
// or the network. Commands are queued with the tick they apply on; each step applies that tick's
// commands in (player, arrival) order, then runs the systems in their fixed order, and leaves the
// events for presentation to drain.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Systems;

namespace Vig.Sim
{
    public interface ISystem
    {
        string Id { get; }
        /// <summary>the commands this system owns</summary>
        string[] Handles { get; }
        void Command(Game g, PlayerState p, Command cmd);
        void Tick(Game g);
    }

    public sealed class GameOpts
    {
        public uint Seed;
        public int Players = 1;
        /// <summary>permanent upgrades the run starts with (meta; DESIGN.md §5)</summary>
        public IEnumerable<string> Upgrades = new string[0];
        /// <summary>start at the house and go home between nights (the game); off for the pace bots</summary>
        public bool Home;
        /// <summary>charms unlocked by killing Collectors in earlier runs (meta; DECISIONS #81)</summary>
        public IEnumerable<string> Unlocked = new string[0];
    }

    public sealed class Game
    {
        /// <summary>The fixed system order: clock → movement → tables → marks → lives and souls → cage → home → collector → economy → stats.</summary>
        public static readonly ISystem[] Systems =
        {
            new ClockSystem(), new MovementSystem(), new TablesSystem(), new MarksSystem(), new LivesSystem(),
            new CageSystem(), new HomeSystem(), new CollectorSystem(), new EconomySystem(), new StatsSystem(),
        };

        public readonly uint Seed;
        public int Tick;
        /// <summary>every random number in the sim comes from here</summary>
        public readonly Rng Rng;
        public State State;
        /// <summary>counts emitted events: presentation redraws a panel when it moves (not part of the hash)</summary>
        public int Rev;
        /// <summary>the applied command log, in the order applied (replaying it reproduces the night)</summary>
        public readonly List<Stamped> Log = new List<Stamped>();
        List<Stamped> queue = new List<Stamped>();
        List<SimEvent> events = new List<SimEvent>();
        static readonly Dictionary<string, ISystem> owner = BuildOwners();

        static Dictionary<string, ISystem> BuildOwners()
        {
            var o = new Dictionary<string, ISystem>();
            foreach (var s in Systems)
                foreach (var t in s.Handles)
                {
                    if (o.ContainsKey(t)) throw new InvalidOperationException($"command {t} owned by both {o[t].Id} and {s.Id}");
                    o[t] = s;
                }
            return o;
        }

        public Game(GameOpts opts)
        {
            Seed = opts.Seed;
            Rng = new Rng(Seed);
            State = State.New(opts.Players, opts.Upgrades, opts.Home, opts.Unlocked);
        }

        /// <summary>Queue a command for the tick it names (a command for a past tick applies on the next one).</summary>
        public void Submit(int player, Command cmd, int? tick = null)
        {
            queue.Add(new Stamped { Tick = Math.Max(tick ?? Tick, Tick), Player = player, Cmd = cmd });
        }

        /// <summary>Advance one tick.</summary>
        public void Step()
        {
            var now = queue.Where(c => c.Tick == Tick).ToList();
            if (now.Count > 0)
            {
                queue = queue.Where(c => c.Tick != Tick).ToList();
                // stable: the same player keeps arrival order
                now = now.OrderBy(c => c.Player).ToList();
                foreach (var c in now)
                {
                    var p = c.Player >= 0 && c.Player < State.Players.Count ? State.Players[c.Player] : null;
                    owner.TryGetValue(c.Cmd.T ?? "", out var sys);
                    Log.Add(c);
                    if (p != null && sys != null) sys.Command(this, p, c.Cmd);
                }
            }
            foreach (var s in Systems) s.Tick(this);
            Tick++;
        }

        public void Emit(SimEvent e)
        {
            events.Add(e);
            Rev++;
        }

        /// <summary>Hand the events since the last drain to presentation.</summary>
        public List<SimEvent> DrainEvents()
        {
            var e = events;
            events = new List<SimEvent>();
            return e;
        }

        /// <summary>A hash of everything that matters: two runs agree on this or they diverged.</summary>
        public string Hash()
        {
            string s = $"{Tick}|{Rng.S}|{Canon.Of(State)}";
            var rev = s.ToCharArray();
            Array.Reverse(rev);
            return Js.HashStr(s).ToString("x8") + Js.HashStr(new string(rev)).ToString("x8");
        }

        /// <summary>Replay a command log from a fresh game with the same seed, to `ticks`.</summary>
        public static Game Replay(GameOpts opts, IEnumerable<Stamped> log, int ticks)
        {
            var g = new Game(opts);
            foreach (var c in log) g.Submit(c.Player, c.Cmd, c.Tick);
            while (g.Tick < ticks) g.Step();
            return g;
        }
    }
}
