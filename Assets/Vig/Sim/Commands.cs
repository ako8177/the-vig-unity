// Commands: the only way anything changes the sim. Panels, the first-person controller, bots and
// (later) remote peers all produce these; the sim applies them in tick order. Same seed + same command
// log = same night. Events: what the sim tells presentation happened; presentation never writes back.
// Both are flat (one class, the fields a kind uses) so they serialize and send as they are.
using System.Collections.Generic;

namespace Vig.Sim
{
    public sealed class Command
    {
        /// <summary>sit | stand | bet | call | spin | buy | pickup | drive | order | inspect | reroute | activate |
        /// pay | gamble | buyback | fortune | callit | ready | move</summary>
        public string T;
        public string Table;
        /// <summary>the stake (bet, call, spin); null: the table's standard</summary>
        public double? Amount;
        /// <summary>Hi-Lo hi | lo; Red Line red | black; Twenty-One hit | stand | double | split</summary>
        public string Call;
        public string Item;
        public string Drink;
        public int Circuit;
        public int A, B;
        public int Target;
        /// <summary>walk intent in world space, in thousandths, and the facing in milliradians</summary>
        public double Mx, Mz, Yaw;
        public bool Run;

        public static Command Sit(string table) => new Command { T = "sit", Table = table };
        public static Command Stand() => new Command { T = "stand" };
        public static Command Bet(string table, double? amount = null) => new Command { T = "bet", Table = table, Amount = amount };
        public static Command CallIt(string table, string call, double? amount = null) => new Command { T = "call", Table = table, Call = call, Amount = amount };
        public static Command Spin(string table, double? amount = null) => new Command { T = "spin", Table = table, Amount = amount };
        public static Command Buy(string item) => new Command { T = "buy", Item = item };
        public static Command Pickup() => new Command { T = "pickup" };
        public static Command Drive() => new Command { T = "drive" };
        public static Command Order(string drink) => new Command { T = "order", Drink = drink };
        public static Command Inspect(int circuit) => new Command { T = "inspect", Circuit = circuit };
        public static Command Reroute(int a, int b) => new Command { T = "reroute", A = a, B = b };
        public static Command Activate(int circuit) => new Command { T = "activate", Circuit = circuit };
        public static Command Pay() => new Command { T = "pay" };
        public static Command Gamble() => new Command { T = "gamble" };
        public static Command Buyback(int target) => new Command { T = "buyback", Target = target };
        public static Command Fortune() => new Command { T = "fortune" };
        public static Command Callit() => new Command { T = "callit" };
        public static Command Ready() => new Command { T = "ready" };
        public static Command Move(double mx, double mz, double yaw, bool run = false) => new Command { T = "move", Mx = mx, Mz = mz, Yaw = yaw, Run = run };
    }

    /// <summary>A command as it goes into the log: who sent it, and the tick it applies on.</summary>
    public sealed class Stamped
    {
        public int Tick;
        public int Player;
        public Command Cmd;
    }

    public sealed class SimEvent
    {
        public string T;
        public int? Player;
        public string Table;
        public int? Ticks;
        public Outcome Outcome;
        /// <summary>a mark's id, a landed wheel slot's id, an ending's id</summary>
        public string Id;
        public int? Stacks;
        public int? Amount;
        public int? Delta;
        public int? Lives;
        public string Why;
        public int? Short;
        public int? Quota;
        public int? OvershootMarkers;
        public string Who;
        public string Action;
        public int? Slot;
        public bool? Over;
        public string Text;
        public bool? Won;
        public int? Idx;
        public string Charm;
        public string Kind;
        public NightSummary Summary;
        public int? Night;
        public int? Bank;
        public string Item;
        public int? Count;
        public List<string> Items;
        public string Drink;
        public string Cmd;
    }
}
