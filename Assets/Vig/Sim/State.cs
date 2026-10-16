// The sim's whole state: plain data. Field names map to the TS sim's keys (camelCase) for the golden
// trace (Canon.cs); a null field is the TS sim's undefined or null.
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim
{
    /// <summary>A timed debuff (DESIGN.md §3). Player -1 is a team mark.</summary>
    public sealed class Mark
    {
        public string Id;
        public int Player;
        /// <summary>ticks left (of floor time: marks don't tick at the Collector's table)</summary>
        public int Left;
        public int Stacks;
    }

    public sealed class Buff
    {
        public string Drink;
        public int Left;
    }

    public sealed class PlayerState
    {
        public int Id;
        /// <summary>meters on the floor plane</summary>
        public double X, Z;
        /// <summary>facing (milliradians), for other players' views only</summary>
        public double Yaw;
        /// <summary>walk intent (thousandths, world space)</summary>
        public double Mx, Mz;
        public string Seat;
        public int Lives;
        public bool Ghost;
        public List<string> Charms = new List<string>();
        /// <summary>Red Line losses in a row (survives standing up: DECISIONS #11)</summary>
        public int RedStreak;
        /// <summary>Overclock: ticks of x2 stakes left, counted only while the tables are open</summary>
        public int Overclock;
        public bool ApronUsed;
        /// <summary>ticks of buzz from the bar, counted down in floor time</summary>
        public int Drunk;
        public bool Run;
        public List<Buff> Buffs = new List<Buff>();
    }

    /// <summary>A card: rank 1 (ace) .. 13 (king), suit 0..3 (0, 1 red; 2, 3 black).</summary>
    public sealed class Card
    {
        public int R, S;
        public Card(int r, int s) { R = r; S = s; }
        public Card Clone() => new Card(R, S);
    }

    public sealed class HandResult
    {
        public List<Card> Cards;
        public int Bet, Ret;
    }

    /// <summary>What one round at a table came to: the panel draws it, the HUD toasts it.</summary>
    public sealed class Outcome
    {
        public string Table;
        public int Player;
        public int Bet;
        /// <summary>total returned to the bank (0 on a loss)</summary>
        public int Ret;
        public bool Win;
        public double Factor;
        public string Text;
        public bool? LifeLost;
        public string Mark;
        public int? Markers;
        /// <summary>the Bandit's grid, cell symbols row by row</summary>
        public List<int> Reels;
        public bool? Jackpot;
        public List<int> Dice;
        public Card Card;
        public string Call;
        public int? Slot;
        public List<HandResult> Hands21;
        public List<Card> Dealer21;
        public int? Streak;
    }

    public sealed class PlayerHand21
    {
        public List<Card> Cards;
        public int Bet;
        public bool Done;
        /// <summary>a split ace: one card and it stands</summary>
        public bool Aces;
    }

    public sealed class Hand21
    {
        public int Bet;
        public List<PlayerHand21> Hands;
        public int Active;
        public List<Card> Dealer;
        /// <summary>play: your move; done: waiting for the result tick</summary>
        public string Stage;
    }

    public sealed class TableState
    {
        /// <summary>the tick the pending result lands on (0: idle)</summary>
        public int Until;
        public Outcome Pending;
        public Outcome Last;
        public int Rounds;
        public Hand21 Hand;
        /// <summary>Twenty-One's shoe: the cards left, as codes</summary>
        public List<int> Shoe;
    }

    /// <summary>One lever under the Collector's table (DESIGN.md §6).</summary>
    public sealed class Lever
    {
        public bool Live, Pulled, Known;
        /// <summary>team | coll | null</summary>
        public string By;
    }

    public sealed class Done
    {
        public bool Won;
        public string Why;
    }

    public sealed class CircuitState
    {
        public List<Lever> Levers;
        public int Live;
        public List<int> Shocks;
        public int HisShocks;
        public List<int> Traces;
        /// <summary>team | coll</summary>
        public string Turn;
        public int Wait;
        public List<string> Log;
        public Done Done;
        public List<int> Survived;
        public int EndedAt;
    }

    public sealed class WheelState
    {
        /// <summary>boon | bane</summary>
        public string Kind;
        public int? Slot;
        public int Until;
        public bool Applied;
    }

    /// <summary>What a Boon or Bane does to the next night (DESIGN.md §7).</summary>
    public sealed class NightMods
    {
        public bool Colddeck, TwoLoss, LongMarks, DearSoul, Overloads3, Immune, Discount;
        public NightMods Clone() => (NightMods)MemberwiseClone();
    }

    public sealed class WheelResult
    {
        public string Kind, Id;
    }

    public sealed class NightSummary
    {
        public int Night, Quota, BankAtMidnight;
        /// <summary>paid | gambled | kept | short</summary>
        public string How;
        public int Short, LivesTaken, OvershootMarkers;
        public Done Circuit;
        public WheelResult Wheel;
        public string Ledger;
        public int NextQuota;
    }

    public sealed class RunStats
    {
        public int Rounds, Wins;
        [Canon.Name("won$")] public int WonDollars;
        [Canon.Name("lost$")] public int LostDollars;
        public int LivesLost, Marks, Biggest, MarkersEarned;
    }

    public sealed class PostItem
    {
        public string Item;
        public int Player;
    }

    public sealed class Post
    {
        public List<PostItem> Orders = new List<PostItem>();
        public int At;
        public List<PostItem> Box = new List<PostItem>();
    }

    public sealed class CollectorSeat
    {
        public int Idx;
        public bool Dead;
    }

    public static class Phase
    {
        public const string Home = "home", Floor = "floor", Midnight = "midnight", Choice = "choice", Circuit = "circuit",
            Wheel = "wheel", Summary = "summary", Over = "over";
    }

    public sealed class State
    {
        public bool Home;
        public Post Post = new Post();
        public int Floor, Night;
        public string Phase;
        public int PhaseAt;
        /// <summary>ticks left in the night</summary>
        public int Clock;
        public int Bank, Quota, Markers;
        public List<PlayerState> Players;
        public List<Mark> Marks = new List<Mark>();
        /// <summary>by id, iterated in TableIds order (the TS sim's object key order)</summary>
        public Dictionary<string, TableState> Tables;
        public List<string> Upgrades;
        public NightMods Mods = new NightMods();
        public NightMods Next = new NightMods();
        public CollectorSeat Collector = new CollectorSeat();
        public List<int> Killed = new List<int>();
        public List<string> Unlocked;
        public int NextDock;
        public CircuitState Circuit;
        public WheelState Wheel;
        public NightSummary Summary;
        public Done Result;
        public RunStats Stats = new RunStats();

        public const int TicksPerS = 60;
        public static readonly int NightTicks = Economy.NightS.I * TicksPerS;
        public static readonly string[] TableIds = { "bandit", "twentyone", "hilo", "wheel", "redline", "deadman" };

        public static Dictionary<string, TableState> NewTables() => TableIds.ToDictionary(id => id, id => new TableState());

        /// <summary>Where player id stands in the house (the start of a run, and after the drive home).</summary>
        public static void PlaceAtHome(PlayerState p, int id, int players)
        {
            p.X = Data.Home.Spawn.X;
            p.Z = Data.Home.Spawn.Z + (id - (players - 1) / 2.0) * 0.8;
            p.Yaw = Js.Round(Data.Home.SpawnYaw * 1000);
        }

        /// <summary>Where player id stands at the start of a night.</summary>
        public static void PlaceAtDoor(PlayerState p, int id, int players)
        {
            var f = Layout.Floor1;
            p.X = f.Spawn.X + (id - (players - 1) / 2.0) * 0.8;
            p.Z = f.Spawn.Z;
            p.Yaw = Js.Round(f.SpawnYaw * 1000);
        }

        public static State New(int players, IEnumerable<string> upgrades, bool home, IEnumerable<string> unlocked)
        {
            var ups = upgrades.ToList();
            var s = new State
            {
                Home = home, Floor = 1, Night = 1, Phase = home ? Sim.Phase.Home : Sim.Phase.Floor, PhaseAt = 0,
                Clock = NightTicks,
                Bank = Economy.StartBank.I + (ups.Contains("bank500") ? 500 : 0),
                Quota = Economy.Quota1.I,
                Markers = Economy.StartMarkers.I,
                Players = new List<PlayerState>(),
                Tables = NewTables(),
                Upgrades = ups,
                Unlocked = unlocked.ToList(),
            };
            for (int id = 0; id < players; id++)
            {
                var p = new PlayerState { Id = id, Lives = Economy.StartLives.I };
                if (home) PlaceAtHome(p, id, players); else PlaceAtDoor(p, id, players);
                s.Players.Add(p);
            }
            return s;
        }
    }
}
