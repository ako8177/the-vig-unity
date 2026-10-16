// The systems, in their fixed order (Game.Systems): clock → movement → tables → marks → lives and souls
// → cage → home → collector → economy → stats. Each owns its commands; ticks run in that order.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Sim.Systems
{
    public abstract class SystemBase : ISystem
    {
        public abstract string Id { get; }
        public virtual string[] Handles => new string[0];
        public virtual void Command(Game g, PlayerState p, Command cmd) { }
        public virtual void Tick(Game g) { }
        protected static void Refuse(Game g, PlayerState p, string cmd, string why) =>
            g.Emit(new SimEvent { T = "refused", Player = p.Id, Cmd = cmd, Why = why });
    }

    // ------------------------------------------------------------------ the clock (DESIGN.md §1)

    /// <summary>The night clock: 6:00 down to 0:00. At 0:00 every round on the felt lands, everyone is pulled
    /// from their seat to the Collector's table and the lights drop; the collector system takes it from there.</summary>
    public sealed class ClockSystem : SystemBase
    {
        /// <summary>How long the 0:00 beat lasts before the Collector speaks (s).</summary>
        public const double MidnightS = 3;

        public override string Id => "clock";

        public static void Midnight(Game g)
        {
            var s = g.State;
            // an unfinished Twenty-One round stands every hand where it is
            var hand = s.Tables["twentyone"].Hand;
            if (hand != null && hand.Stage == "play")
            {
                var owner = s.Players.FirstOrDefault(p => p.Seat == "twentyone") ?? s.Players[0];
                var r = TableModule.ById("twentyone").Start(g, owner, Vig.Sim.Command.CallIt("twentyone", "leave"));
                if (r.Outcome != null) s.Tables["twentyone"].Pending = r.Outcome;
            }
            // a craps bet riding on a point comes back; a Crash in the air cashes out where it is
            var craps = s.Tables["craps"];
            if (craps.Point != 0)
            {
                s.Bank += craps.PointBet;
                craps.Point = 0;
                craps.PointBet = 0;
            }
            var crash = s.Tables["crash"];
            if (crash.Crash != null && crash.Pending == null) crash.Pending = Crash.Leave(g, crash);
            foreach (var id in State.TableIds) TablesSystem.Land(g, id);
            foreach (var id in State.TableIds) s.Tables[id].Ride = null;
            foreach (var p in s.Players)
            {
                TablesSystem.StandUp(g, p);
                p.Mx = p.Mz = 0;
            }
            s.Clock = 0;
            s.Phase = Phase.Midnight;
            s.PhaseAt = g.Tick;
            g.Emit(new SimEvent { T = "midnight" });
        }

        public override void Tick(Game g)
        {
            var s = g.State;
            if (s.Phase != Phase.Floor) return;
            if (s.Clock > 0) s.Clock--;
            if (s.Clock == 0) Midnight(g);
        }
    }

    // ------------------------------------------------------------------ movement

    /// <summary>Walking the floor at 3 m/s with the player's capsule (a circle on the floor plane) pushed out
    /// of the room's colliders. No trigonometry: the client sends a world-space direction.</summary>
    public sealed class MovementSystem : SystemBase
    {
        /// <summary>Walking speed, m/s. Sticky Floor (DESIGN.md §3) scales it by 0.7.</summary>
        public const double WalkSpeed = 3;
        /// <summary>Holding sprint (DESIGN.md §1): 5.1 m/s.</summary>
        public const double Sprint = 1.7;
        const double StepM = WalkSpeed / State.TicksPerS;

        static double ClampInt(double v, double a, double b) => Math.Max(a, Math.Min(b, Js.Round(double.IsNaN(v) || double.IsInfinity(v) ? 0 : v)));

        /// <summary>Push a circle (x, z, r) out of every collider, two passes so a corner between two boxes settles.</summary>
        public static (double x, double z) Collide(double x, double z, double r, IReadOnlyList<Collider> cols)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var c in cols)
                {
                    if (c.IsBox)
                    {
                        double cx = Math.Max(c.X0, Math.Min(x, c.X1));
                        double cz = Math.Max(c.Z0, Math.Min(z, c.Z1));
                        double dx = x - cx, dz = z - cz;
                        double d2 = dx * dx + dz * dz;
                        if (d2 >= r * r) continue;
                        if (d2 > 1e-12)
                        {
                            double d = Math.Sqrt(d2);
                            x = cx + (dx / d) * r;
                            z = cz + (dz / d) * r;
                        }
                        else
                        {
                            // the center is inside the box: leave by the shortest way out
                            double o0 = x - c.X0, o1 = c.X1 - x, o2 = z - c.Z0, o3 = c.Z1 - z;
                            double m = Math.Min(Math.Min(o0, o1), Math.Min(o2, o3));
                            if (m == o0) x = c.X0 - r;
                            else if (m == o1) x = c.X1 + r;
                            else if (m == o2) z = c.Z0 - r;
                            else z = c.Z1 + r;
                        }
                    }
                    else
                    {
                        double dx = x - c.X, dz = z - c.Z;
                        double rr = r + c.R;
                        double d2 = dx * dx + dz * dz;
                        if (d2 >= rr * rr) continue;
                        double d = Math.Sqrt(d2);
                        if (d > 1e-9)
                        {
                            x = c.X + (dx / d) * rr;
                            z = c.Z + (dz / d) * rr;
                        }
                        else z = c.Z + rr;
                    }
                }
            }
            return (x, z);
        }

        public override string Id => "movement";
        public override string[] Handles => new[] { "move" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            p.Mx = ClampInt(cmd.Mx, -1000, 1000);
            p.Mz = ClampInt(cmd.Mz, -1000, 1000);
            p.Yaw = ClampInt(cmd.Yaw, -7000, 7000);
            p.Run = cmd.Run;
        }

        public override void Tick(Game g)
        {
            // you walk the floor while the night runs, and the house between nights; at 0:00 you're held
            // at the Collector's table
            var ph = g.State.Phase;
            if (ph != Phase.Floor && ph != Phase.Home) return;
            var cols = ph == Phase.Home ? Home.Colliders : Layout.FloorOf(g.State.Floor).Colliders;
            bool sticky = Rules.HasTeamMark(g, "sticky");
            foreach (var p in g.State.Players)
            {
                // the Insulated Kit's rubber soles don't stick (DECISIONS #81)
                double step = StepM * (sticky && !p.Charms.Contains("insulated") ? Marks.Sticky : 1);
                if (p.Seat != null || (p.Mx == 0 && p.Mz == 0)) continue;
                double k = p.Run ? Sprint : 1;
                double vx = p.Mx / 1000, vz = p.Mz / 1000;
                double len = Math.Sqrt(vx * vx + vz * vz);
                if (len > 1) { vx /= len; vz /= len; }
                var to = Collide(p.X + vx * step * k, p.Z + vz * step * k, Layout.PlayerR, cols);
                p.X = to.x;
                p.Z = to.z;
            }
        }
    }

    // ------------------------------------------------------------------ tables

    /// <summary>Sitting and standing, and each table's module behind bet / call / spin. A round's stake leaves
    /// the bank at the call; its result is held back for the table's ResolveS and then lands.</summary>
    public sealed class TablesSystem : SystemBase
    {
        /// <summary>Is this table on the floor the run is on?</summary>
        public static bool Open(Game g, string table) => table != null && Vig.Sim.Data.Tables.OnFloor(g.State.Floor).Contains(table);

        /// <summary>Land a table's held-back result: pay it out and charge its price.</summary>
        public static void Land(Game g, string id)
        {
            var s = g.State;
            var t = s.Tables[id];
            var o = t.Pending;
            if (o == null) return;
            t.Pending = null;
            t.Until = 0;
            var p = s.Players[o.Player];
            s.Bank += o.Ret;
            TableModule.ById(id).Resolve(g, p, o);
            t.Last = o;
            t.Rounds++;
            s.Stats.Rounds++;
            // Hot Hand counts wins in a row at one table (DESIGN.md §12)
            if (o.Win) { if (p.HotTable == id) p.HotRun++; else { p.HotTable = id; p.HotRun = 1; } }
            else if (o.Ret < o.Bet) { p.HotTable = id; p.HotRun = 0; }
            // a win's profit is offered to let ride, double or nothing (U18)
            t.Ride = o.Win && o.Ret > o.Bet ? new Ride { Player = o.Player, Amount = o.Ret - o.Bet, Until = g.Tick + Rules.Ticks(Economy.RideS.V), Count = 0 } : null;
            if (o.Win)
            {
                s.Stats.Wins++;
                s.Stats.WonDollars += o.Ret - o.Bet;
                s.Stats.Biggest = Math.Max(s.Stats.Biggest, o.Ret - o.Bet);
            }
            else if (o.Ret < o.Bet) s.Stats.LostDollars += o.Bet - o.Ret;
            g.Emit(new SimEvent { T = "round", Player = o.Player, Outcome = o });
        }

        static int HoldTicks(TableModule mod, Outcome o) => Rules.Ticks(mod.HoldS(o) ?? mod.Def.ResolveS);

        /// <summary>Stand a player up (no event if they weren't seated).</summary>
        public static void StandUp(Game g, PlayerState p)
        {
            if (p.Seat == null) return;
            var table = p.Seat;
            // walking away from a Twenty-One hand stands on it
            var tc = g.State.Tables["crash"];
            if (table == "crash" && tc.Crash != null && tc.Pending == null)
            {
                var o = Crash.Leave(g, tc);
                if (o != null) { tc.Pending = o; tc.Until = g.Tick; }
            }
            var t21 = g.State.Tables["twentyone"];
            if (table == "twentyone" && t21.Hand?.Stage == "play" && t21.Pending == null)
            {
                var mod = TableModule.ById("twentyone");
                var r = mod.Start(g, p, Vig.Sim.Command.CallIt(table, "leave"));
                if (r.Outcome != null)
                {
                    t21.Pending = r.Outcome;
                    t21.Until = g.Tick + HoldTicks(mod, r.Outcome);
                }
            }
            p.Seat = null;
            g.Emit(new SimEvent { T = "stood", Player = p.Id, Table = table });
        }

        public override string Id => "tables";
        public override string[] Handles => new[] { "sit", "stand", "bet", "call", "spin", "ride" };

        /// <summary>Let it ride: the last win's profit, double or nothing, at even odds (U18).</summary>
        static void LetItRide(Game g, PlayerState p, string table)
        {
            var s = g.State;
            if (s.Phase != Phase.Floor) { Refuse(g, p, "ride", "the tables are closed"); return; }
            if (p.Seat != table || !s.Tables.TryGetValue(table, out var t)) { Refuse(g, p, "ride", "sit down first"); return; }
            var r = t.Ride;
            if (r == null || r.Player != p.Id || g.Tick > r.Until) { Refuse(g, p, "ride", "no win to ride"); return; }
            if (s.Bank < r.Amount) { Refuse(g, p, "ride", "the bank is short"); return; }
            bool won = g.Rng.Int(0, 1) == 1;
            if (won)
            {
                s.Bank += r.Amount;
                s.Stats.WonDollars += r.Amount;
                r.Amount *= 2;
                r.Count++;
                r.Until = g.Tick + Rules.Ticks(Economy.RideS.V);
                g.Emit(new SimEvent { T = "ride", Player = p.Id, Table = table, Won = true, Amount = r.Amount, Count = r.Count });
                if (r.Count >= Economy.RideMax.I) t.Ride = null;
            }
            else
            {
                s.Bank -= r.Amount;
                s.Stats.LostDollars += r.Amount;
                g.Emit(new SimEvent { T = "ride", Player = p.Id, Table = table, Won = false, Amount = r.Amount, Count = r.Count });
                t.Ride = null;
            }
        }

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            var s = g.State;
            if (cmd.T == "ride") { LetItRide(g, p, cmd.Table); return; }
            if (cmd.T == "sit")
            {
                var pl = Layout.FloorOf(s.Floor).TableOf(cmd.Table);
                if (pl == null || !Open(g, cmd.Table)) { Refuse(g, p, cmd.T, "no such table"); return; }
                if (s.Phase != Phase.Floor) { Refuse(g, p, cmd.T, "the tables are closed"); return; }
                if (p.Seat != null) { Refuse(g, p, cmd.T, "already seated"); return; }
                if (p.Ghost) { Refuse(g, p, cmd.T, "ghosts cannot bet"); return; }
                double dx = p.X - pl.Stand.X, dz = p.Z - pl.Stand.Z;
                if (dx * dx + dz * dz > Layout.UseRadius * Layout.UseRadius) { Refuse(g, p, cmd.T, "too far"); return; }
                if (s.Players.Any(o => o.Seat == cmd.Table)) { Refuse(g, p, cmd.T, "seat taken"); return; }
                p.Seat = cmd.Table;
                p.Mx = p.Mz = 0;
                p.X = pl.Stand.X;
                p.Z = pl.Stand.Z;
                g.Emit(new SimEvent { T = "sat", Player = p.Id, Table = cmd.Table });
                return;
            }
            if (cmd.T == "stand") { StandUp(g, p); return; }

            // bet / call / spin: at the table you're sitting at, while the night runs
            var id = cmd.Table;
            var mod = TableModule.ById(id);
            if (mod == null || !mod.Cmds.Contains(cmd.T)) { Refuse(g, p, cmd.T, "not at this table"); return; }
            if (s.Phase != Phase.Floor) { Refuse(g, p, cmd.T, "the tables are closed"); return; }
            if (p.Seat != id) { Refuse(g, p, cmd.T, "sit down first"); return; }
            var t = s.Tables[id];
            if (t.Pending != null) { Refuse(g, p, cmd.T, "wait for the result"); return; }
            var r = mod.Start(g, p, cmd);
            if (r.Refusal != null) { Refuse(g, p, cmd.T, r.Refusal); return; }
            t.Ride = null;
            if (r.Outcome != null)
            {
                int n = HoldTicks(mod, r.Outcome);
                t.Pending = r.Outcome;
                t.Until = g.Tick + n;
                g.Emit(new SimEvent { T = "play", Player = p.Id, Table = id, Ticks = n });
            }
        }

        public override void Tick(Game g)
        {
            var s = g.State;
            if (s.Phase != Phase.Floor) return;
            // Crash climbs: an auto cash-out or the crash lands now
            var tc = s.Tables["crash"];
            if (tc.Crash != null && tc.Pending == null)
            {
                var o = Crash.Climb(g, tc);
                if (o != null) { tc.Pending = o; tc.Until = g.Tick; }
            }
            foreach (var id in State.TableIds)
            {
                var t = s.Tables[id];
                if (t.Pending != null && g.Tick >= t.Until) Land(g, id);
                if (t.Ride != null && g.Tick > t.Ride.Until) t.Ride = null;
            }
        }
    }

    // ------------------------------------------------------------------ marks and timed statuses

    /// <summary>Marks, Overclock windows and a buzz count down in table time only. At the Collector's table
    /// the clock has stopped, and so do they.</summary>
    public sealed class MarksSystem : SystemBase
    {
        public override string Id => "marks";

        public override void Tick(Game g)
        {
            var s = g.State;
            if (s.Phase != Phase.Floor) return;
            foreach (var m in s.Marks) m.Left--;
            if (s.Marks.Any(m => m.Left <= 0)) s.Marks = s.Marks.Where(m => m.Left > 0).ToList();
            foreach (var p in s.Players)
            {
                if (p.Overclock > 0) p.Overclock--;
                // a buzz wears off in floor time too (DESIGN.md §10)
                if (p.Drunk > 0) p.Drunk--;
                if (p.Buffs.Count > 0)
                {
                    foreach (var b in p.Buffs) b.Left--;
                    p.Buffs = p.Buffs.Where(b => b.Left > 0).ToList();
                }
            }
        }
    }

    // ------------------------------------------------------------------ lives and souls (DESIGN.md §4)

    /// <summary>A living player at the cage buys a ghost's soul back from the bank for 50% of the current
    /// quota (75% under the Bane). The soul comes back with one life (DECISIONS #34).</summary>
    public sealed class LivesSystem : SystemBase
    {
        public static int BuybackPrice(Game g) => Js.RoundInt(g.State.Quota * (g.State.Mods.DearSoul ? Wheels.DearSoul : Economy.BuybackShare.V));

        public override string Id => "lives";
        public override string[] Handles => new[] { "buyback" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            var s = g.State;
            var ghost = cmd.Target >= 0 && cmd.Target < s.Players.Count ? s.Players[cmd.Target] : null;
            if (s.Phase != Phase.Floor) { Refuse(g, p, "buyback", "the cage is shut"); return; }
            if (ghost == null || !ghost.Ghost) { Refuse(g, p, "buyback", "no soul to buy"); return; }
            if (p.Ghost) { Refuse(g, p, "buyback", "a ghost cannot buy"); return; }
            if (!CageSystem.AtCage(p)) { Refuse(g, p, "buyback", "walk to the cage"); return; }
            int price = BuybackPrice(g);
            if (s.Bank < price) { Refuse(g, p, "buyback", "the bank is short"); return; }
            s.Bank -= price;
            ghost.Ghost = false;
            ghost.Lives = 1;
            g.Emit(new SimEvent { T = "life", Player = ghost.Id, Delta = 1, Lives = 1, Why = "bought back" });
        }
    }

    // ------------------------------------------------------------------ the cage and the bar (DESIGN.md §5, §10)

    /// <summary>Markers buy charms (worn by the buyer, one a slot). The bar is the floor's other counter: cash
    /// buys a drink, and the drink a buzz.</summary>
    public sealed class CageSystem : SystemBase
    {
        /// <summary>A player's charm slots (3, 4 with the upgrade).</summary>
        public static int SlotsOf(Game g) => Economy.CharmSlots.I + (g.State.Upgrades.Contains("slot4") ? 1 : 0);

        public static bool AtCage(PlayerState p) => Rules.Dist2(p.X, p.Z, Layout.Floor1.CageStand) <= Layout.CageRadius * Layout.CageRadius;
        public static bool AtBar(PlayerState p) => Rules.Dist2(p.X, p.Z, Layout.Floor1.BarStand) <= Layout.BarRadius * Layout.BarRadius;

        /// <summary>What a drink costs this player (the Comp Card: nothing).</summary>
        public static int PriceOf(PlayerState p, DrinkDef d) => p.Charms.Contains("comp") ? 0 : d.Price;

        /// <summary>Why a player can't order this drink now, or null if they can.</summary>
        public static string CantOrder(Game g, PlayerState p, string drink)
        {
            var d = Drinks.ById(drink);
            if (d == null) return "no such drink";
            if (g.State.Phase != Phase.Floor) return "the bar is shut";
            if (p.Ghost) return "the bartender can't see you";
            if (!AtBar(p)) return "walk to the bar";
            if (g.State.Bank < PriceOf(p, d)) return "the bank is short";
            return null;
        }

        /// <summary>Why a player can't buy this now, or null if they can.</summary>
        public static string CantBuy(Game g, PlayerState p, string item)
        {
            var s = g.State;
            // with the house, charms come from the computer at home, by post (DECISIONS #79)
            if (s.Home)
            {
                if (s.Phase != Phase.Home) return "order it from the computer at home";
                if (!HomeSystem.AtComputer(p)) return "walk to the computer";
            }
            else
            {
                if (s.Phase != Phase.Floor) return "the cage is shut";
                if (!AtCage(p)) return "walk to the cage";
            }
            if (p.Ghost) return "ghosts cannot buy";
            var charm = Charms.ById(item);
            var up = Charms.UpgradeById(item);
            if (charm != null)
            {
                if (charm.Unlock != null && !s.Unlocked.Contains(charm.Id)) return "locked: kill a Collector at his game";
                if (s.Home && !s.Shop.Contains(charm.Id)) return "not in tonight's catalogue";
                var coming = HomeSystem.PendingCharms(g, p.Id);
                if (p.Charms.Contains(charm.Id)) return "already worn";
                if (coming.Contains(charm.Id)) return "already in the post";
                if (p.Charms.Count + coming.Count >= SlotsOf(g)) return "no free slot";
                if (s.Markers < charm.Price) return "not enough markers";
                return null;
            }
            // markers buy charms only now: lasting upgrades will come from the skill tree (DECISIONS #84)
            if (up != null) return s.Upgrades.Contains(up.Id) ? "already owned" : "markers buy charms only";
            return "no such item";
        }

        public override string Id => "cage";
        public override string[] Handles => new[] { "buy", "order" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            if (cmd.T == "order")
            {
                var why0 = CantOrder(g, p, cmd.Drink);
                if (why0 != null) { Refuse(g, p, "order", why0); return; }
                var d = Drinks.ById(cmd.Drink);
                g.State.Bank -= PriceOf(p, d);
                p.Drunk = Math.Min(Rules.Ticks(Drinks.BuzzMaxS), p.Drunk + Rules.Ticks(d.BuzzS));
                // its edge runs for its own buzz (twice that with the Comp Card); the same drink again starts it over
                int edge = Rules.Ticks(d.BuzzS * (p.Charms.Contains("comp") ? 2 : 1));
                var b = p.Buffs.FirstOrDefault(x => x.Drink == d.Id);
                if (b != null) b.Left = Math.Max(b.Left, edge);
                else p.Buffs.Add(new Buff { Drink = d.Id, Left = edge });
                g.Emit(new SimEvent { T = "drank", Player = p.Id, Drink = d.Id });
                return;
            }
            var why = CantBuy(g, p, cmd.Item);
            if (why != null) { Refuse(g, p, "buy", why); return; }
            var s = g.State;
            var charm = Charms.ById(cmd.Item);
            if (s.Home)
            {
                // paid now, carried in from the mailbox later
                int price = charm != null ? charm.Price : Charms.UpgradeById(cmd.Item).Price;
                s.Markers -= price;
                HomeSystem.Post(g, p, cmd.Item);
                g.Emit(new SimEvent { T = "ordered", Player = p.Id, Item = cmd.Item, Kind = charm != null ? "charm" : "upgrade" });
                return;
            }
            if (charm != null)
            {
                s.Markers -= charm.Price;
                p.Charms.Add(charm.Id);
                g.Emit(new SimEvent { T = "bought", Player = p.Id, Item = charm.Id, Kind = "charm" });
                return;
            }
            var up = Charms.UpgradeById(cmd.Item);
            s.Markers -= up.Price;
            s.Upgrades.Add(up.Id);
            g.Emit(new SimEvent { T = "bought", Player = p.Id, Item = up.Id, Kind = "upgrade" });
        }
    }

    // ------------------------------------------------------------------ the house (DESIGN.md §5; DECISIONS #79)

    /// <summary>Between nights, and before the first, the run is at home. The computer takes orders (the cage
    /// checks and charges them); the post brings them to the mailbox PostS later; pickup at the mailbox
    /// carries them in; drive at the car starts the night at the casino's door.</summary>
    public sealed class HomeSystem : SystemBase
    {
        static bool Near(PlayerState p, V2 at) => Rules.Dist2(p.X, p.Z, at) <= Home.UseR * Home.UseR;
        public static bool AtComputer(PlayerState p) => Near(p, Home.ComputerStand);
        public static bool AtMailbox(PlayerState p) => Near(p, Home.MailboxStand);
        public static bool AtCar(PlayerState p) => Near(p, Home.CarStand);

        /// <summary>Post an order: it reaches the mailbox PostS after the latest order.</summary>
        public static void Post(Game g, PlayerState p, string item)
        {
            var s = g.State;
            s.Post.Orders.Add(new PostItem { Item = item, Player = p.Id });
            s.Post.At = g.Tick + Rules.Ticks(Home.PostS.V);
        }

        /// <summary>What a player has coming: charms in the post or the mailbox (they hold a slot already).</summary>
        public static List<string> PendingCharms(Game g, int player)
        {
            var s = g.State;
            return s.Post.Orders.Concat(s.Post.Box).Where(o => o.Player == player && Charms.ById(o.Item) != null).Select(o => o.Item).ToList();
        }

        public static List<string> PendingItems(Game g) => g.State.Post.Orders.Concat(g.State.Post.Box).Select(o => o.Item).ToList();

        /// <summary>Tonight's catalogue: ShopSize charms from the pool (U17).</summary>
        public static void DrawShop(Game g)
        {
            var s = g.State;
            var pool = Charms.All.Where(c => c.Unlock == null || s.Unlocked.Contains(c.Id)).Select(c => c.Id).ToList();
            g.Rng.Shuffle(pool);
            s.Shop = pool.Take(Economy.ShopSize.I).ToList();
        }

        public override string Id => "home";
        public override string[] Handles => new[] { "pickup", "drive", "reroll" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            var s = g.State;
            if (s.Phase != Phase.Home) { Refuse(g, p, cmd.T, "you're not home"); return; }
            if (cmd.T == "reroll")
            {
                if (!AtComputer(p)) { Refuse(g, p, cmd.T, "walk to the computer"); return; }
                if (s.Markers < Economy.RerollCost.I) { Refuse(g, p, cmd.T, "not enough markers"); return; }
                s.Markers -= Economy.RerollCost.I;
                DrawShop(g);
                g.Emit(new SimEvent { T = "shop", Player = p.Id, Items = s.Shop.ToList() });
                return;
            }
            if (cmd.T == "pickup")
            {
                if (!AtMailbox(p)) { Refuse(g, p, cmd.T, "walk to the mailbox"); return; }
                if (s.Post.Box.Count == 0) { Refuse(g, p, cmd.T, s.Post.Orders.Count > 0 ? "the post is on its way" : "the mailbox is empty"); return; }
                var items = new List<string>();
                foreach (var o in s.Post.Box)
                {
                    var buyer = o.Player >= 0 && o.Player < s.Players.Count ? s.Players[o.Player] : p;
                    if (Charms.ById(o.Item) != null)
                    {
                        if (!buyer.Charms.Contains(o.Item)) buyer.Charms.Add(o.Item);
                    }
                    else if (!s.Upgrades.Contains(o.Item)) s.Upgrades.Add(o.Item);
                    items.Add(o.Item);
                }
                s.Post.Box = new List<PostItem>();
                g.Emit(new SimEvent { T = "collected", Player = p.Id, Items = items });
                return;
            }
            if (cmd.T == "drive")
            {
                if (!AtCar(p)) { Refuse(g, p, cmd.T, "walk to the car"); return; }
                // the car takes everyone: the night starts at the casino's door
                for (int i = 0; i < s.Players.Count; i++)
                {
                    var q = s.Players[i];
                    State.PlaceAtDoor(q, i, s.Players.Count);
                    q.Mx = q.Mz = 0;
                    q.Seat = null;
                }
                s.Phase = Phase.Floor;
                s.PhaseAt = g.Tick;
                g.Emit(new SimEvent { T = "drive", Night = s.Night });
            }
        }

        public override void Tick(Game g)
        {
            var s = g.State;
            if (s.Phase != Phase.Home || s.Post.Orders.Count == 0 || g.Tick < s.Post.At) return;
            s.Post.Box.AddRange(s.Post.Orders);
            g.Emit(new SimEvent { T = "post", Count = s.Post.Orders.Count });
            s.Post.Orders = new List<PostItem>();
        }
    }

    // ------------------------------------------------------------------ the Collector (DESIGN.md §2, §6, §7)

    /// <summary>At 0:00, short of quota he takes a life per $2,000 short and the bank; at or over it, the team
    /// pays, or challenges him at the Circuit: win and he dies, you keep the quota and spin the Boon; lose
    /// and it's a life, the quota and the Bane.</summary>
    public sealed class CollectorSystem : SystemBase
    {
        /// <summary>How close to the Collector's table you must stand to call the night (m).</summary>
        public const double CallRadius = 1.6;
        /// <summary>The beat between the Circuit ending and the wheel coming up (s): a loss, or his slow death
        /// and the pit boss dragging him out (presentation pacing in the sim).</summary>
        public const double AfterCircuitS = 7, AfterDeathS = 23.5;

        static void Shortfall(Game g)
        {
            var s = g.State;
            int shortBy = s.Quota - s.Bank;
            int lives = (int)Math.Ceiling(shortBy / (double)Economy.ShortfallPerLife.I);
            int bank = s.Bank;
            s.Bank = 0;
            g.Emit(new SimEvent { T = "shortfall", Short = shortBy, Lives = lives });
            int taken = 0;
            for (int i = 0; i < lives; i++)
            {
                var alive = Rules.Living(g);
                if (alive.Count == 0) break;
                // from the player with the most lives (ties: the lowest id)
                var p = alive.Aggregate((a, b) => b.Lives > a.Lives ? b : a);
                if (Rules.LoseLife(g, p, "shortfall") == "lost") taken++;
            }
            EconomySystem.Summarize(g, new NightSummary { BankAtMidnight = bank, How = "short", Short = shortBy, LivesTaken = taken });
        }

        static PlayerState CharmFor(Game g)
        {
            int slots = Economy.CharmSlots.I + (g.State.Upgrades.Contains("slot4") ? 1 : 0);
            return Rules.Living(g).FirstOrDefault(p => p.Charms.Count < slots && p.Charms.Count < Charms.All.Length);
        }

        static (string id, string text) ApplyWheel(Game g, string kind, int slot)
        {
            var s = g.State;
            if (kind == "boon")
            {
                var b = Wheels.Boons[slot];
                switch (b.Id)
                {
                    case "life": foreach (var p in Rules.Living(g)) Rules.GainLife(g, p, "boon"); break;
                    case "cash": s.Bank += Wheels.Cash; break;
                    case "markers": Rules.AddMarkers(g, Wheels.Markers, "boon"); break;
                    case "charm":
                    {
                        var p = CharmFor(g);
                        // only from the pool: a locked charm can't come off the wheel either (DECISIONS U5:
                        // the TS sim let the Insulated Kit, unlock index 0, through)
                        var free = p != null ? Charms.All.Where(c => !p.Charms.Contains(c.Id) && (c.Unlock == null || s.Unlocked.Contains(c.Id))).ToList() : new List<CharmDef>();
                        if (p != null && free.Count > 0)
                        {
                            var c = g.Rng.Pick(free);
                            p.Charms.Add(c.Id);
                            g.Emit(new SimEvent { T = "bought", Player = p.Id, Item = c.Id, Kind = "charm" });
                            return (b.Id, $"{c.Name.ToUpperInvariant()} IS YOURS");
                        }
                        Rules.AddMarkers(g, Wheels.Markers, "boon");
                        return (b.Id, "NO FREE SLOT: +3 MARKERS");
                    }
                    case "discount": s.Next.Discount = true; break;
                    case "immune": s.Marks = new List<Mark>(); s.Next.Immune = true; break;
                }
                return (b.Id, b.Text);
            }
            var bane = Wheels.Banes[slot];
            switch (bane.Id)
            {
                case "colddeck": s.Next.Colddeck = true; break;
                case "bleed": { var alive = Rules.Living(g); if (alive.Count > 0) Rules.LoseLife(g, g.Rng.Pick(alive), "bane"); break; }
                case "twoloss": s.Next.TwoLoss = true; break;
                case "longmarks": s.Next.LongMarks = true; break;
                case "dearsoul":
                    if (s.Players.Count == 1)
                    {
                        s.NextDock = Wheels.DearSoulsSoloDock;
                        return (bane.Id, Wheels.DearSoulsSoloText);
                    }
                    s.Next.DearSoul = true;
                    break;
                case "overloads": s.Next.Overloads3 = true; break;
            }
            return (bane.Id, bane.Text);
        }

        public override string Id => "collector";
        public override string[] Handles => new[] { "pay", "gamble", "inspect", "reroute", "activate", "fortune", "callit" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            var s = g.State;
            if (cmd.T == "callit")
            {
                // a broke team need not wait out the clock
                if (s.Phase != Phase.Floor) { Refuse(g, p, cmd.T, "not now"); return; }
                if (p.Ghost) { Refuse(g, p, cmd.T, "a ghost has no say"); return; }
                if (s.Players.Count > 1) { Refuse(g, p, cmd.T, "the whole team must agree (co-op: Milestone 3)"); return; }
                var c = Layout.Floor1.Collector.Stand;
                if (Rules.Dist2(p.X, p.Z, c) > CallRadius * CallRadius) { Refuse(g, p, cmd.T, "walk to the Collector's table"); return; }
                ClockSystem.Midnight(g);
                return;
            }
            if (cmd.T == "pay" || cmd.T == "gamble")
            {
                if (s.Phase != Phase.Choice) { Refuse(g, p, cmd.T, "not now"); return; }
                if (p.Ghost) { Refuse(g, p, cmd.T, "a ghost has no say"); return; }
                int bank = s.Bank;
                if (cmd.T == "pay")
                {
                    int over = EconomySystem.PayQuota(g);
                    EconomySystem.Summarize(g, new NightSummary { BankAtMidnight = bank, How = "paid", OvershootMarkers = over });
                    return;
                }
                // the challenge: nothing is paid yet. Win and you keep the quota; lose and it's his (DECISIONS #80)
                s.Circuit = CircuitPlay.New(g);
                s.Summary = new NightSummary { Night = s.Night, Quota = s.Quota, NextQuota = 0, BankAtMidnight = bank, How = "gambled" };
                s.Phase = Phase.Circuit;
                s.PhaseAt = g.Tick;
                return;
            }
            if (cmd.T == "inspect" || cmd.T == "reroute" || cmd.T == "activate")
            {
                if (s.Phase != Phase.Circuit) { Refuse(g, p, cmd.T, "no circuit"); return; }
                var why = cmd.T == "reroute" ? CircuitPlay.TeamAct(g, p, "reroute", cmd.A) : CircuitPlay.TeamAct(g, p, cmd.T, cmd.Circuit);
                if (why != null) Refuse(g, p, cmd.T, why);
                return;
            }
            if (cmd.T == "fortune")
            {
                var w = s.Wheel;
                if (s.Phase != Phase.Wheel || w == null || w.Slot != null) { Refuse(g, p, cmd.T, "not now"); return; }
                if (p.Ghost) { Refuse(g, p, cmd.T, "a ghost has no say"); return; }
                w.Slot = g.Rng.Int(0, 5);
                w.Until = g.Tick + Rules.Ticks(Wheels.SpinS);
                g.Emit(new SimEvent { T = "spinning", Kind = w.Kind, Slot = w.Slot, Ticks = Rules.Ticks(Wheels.SpinS) });
            }
        }

        public override void Tick(Game g)
        {
            var s = g.State;
            if (s.Phase == Phase.Midnight && g.Tick - s.PhaseAt >= Rules.Ticks(ClockSystem.MidnightS))
            {
                if (s.Bank >= s.Quota)
                {
                    s.Phase = Phase.Choice;
                    s.PhaseAt = g.Tick;
                    g.Emit(new SimEvent { T = "choice" });
                }
                else Shortfall(g);
            }
            else if (s.Phase == Phase.Circuit && s.Circuit != null)
            {
                var c = s.Circuit;
                if (c.Done == null && c.Turn == "coll" && g.Tick >= c.Wait) CircuitPlay.CollectorAct(g);
                if (c.Done != null && s.Phase == Phase.Circuit && g.Tick - c.EndedAt >= Rules.Ticks(c.Done.Won ? AfterDeathS : AfterCircuitS))
                {
                    s.Summary.Circuit = c.Done;
                    // won: he's dead, the quota stays in the bank (its overshoot still pays markers), and the
                    // first kill of each Collector puts his charm in the pool; lost: the quota is paid now
                    if (c.Done.Won)
                    {
                        s.Summary.OvershootMarkers = EconomySystem.Overshoot(g);
                        s.Summary.How = "kept";
                        s.Collector.Dead = true;
                        s.Killed.Add(s.Collector.Idx);
                        g.Emit(new SimEvent { T = "killed", Idx = s.Collector.Idx });
                        var charm = Collectors.Of(s.Collector.Idx).Unlocks;
                        if (charm != null && !s.Unlocked.Contains(charm))
                        {
                            s.Unlocked.Add(charm);
                            g.Emit(new SimEvent { T = "unlocked", Charm = charm });
                        }
                    }
                    else s.Summary.OvershootMarkers = EconomySystem.PayQuota(g);
                    // the last night: no tomorrow for a Boon or a Bane to touch. A win takes one permanent
                    // upgrade from the house's ledger; a loss costs only what the Circuit cost (DECISIONS #50)
                    if (s.Night >= Economy.Nights.I)
                    {
                        var part = s.Summary;
                        string ledger = null;
                        if (c.Done.Won)
                        {
                            var free = Charms.Upgrades.Where(u => !s.Upgrades.Contains(u.Id)).ToList();
                            if (free.Count > 0)
                            {
                                var u = g.Rng.Pick(free);
                                s.Upgrades.Add(u.Id);
                                ledger = u.Id;
                                g.Emit(new SimEvent { T = "bought", Player = 0, Item = u.Id, Kind = "upgrade" });
                            }
                        }
                        EconomySystem.Summarize(g, new NightSummary
                        {
                            BankAtMidnight = part.BankAtMidnight, How = part.How, OvershootMarkers = part.OvershootMarkers,
                            Circuit = c.Done, Ledger = ledger,
                        });
                        return;
                    }
                    s.Wheel = new WheelState { Kind = c.Done.Won ? "boon" : "bane", Slot = null, Until = 0, Applied = false };
                    s.Phase = Phase.Wheel;
                    s.PhaseAt = g.Tick;
                }
            }
            else if (s.Phase == Phase.Wheel && s.Wheel != null && s.Wheel.Slot != null && !s.Wheel.Applied && g.Tick >= s.Wheel.Until)
            {
                var w = s.Wheel;
                w.Applied = true;
                var r = ApplyWheel(g, w.Kind, w.Slot.Value);
                g.Emit(new SimEvent { T = "landed", Kind = w.Kind, Id = r.id, Text = r.text });
                var part = s.Summary;
                EconomySystem.Summarize(g, new NightSummary
                {
                    BankAtMidnight = part.BankAtMidnight, How = part.How, OvershootMarkers = part.OvershootMarkers,
                    Circuit = part.Circuit, Wheel = new WheelResult { Kind = w.Kind, Id = r.id },
                });
            }
        }
    }

    // ------------------------------------------------------------------ the economy between nights (DESIGN.md §2)

    /// <summary>Paying the quota, overshoot markers, the next quota, the bank floor, and starting the next night.</summary>
    public sealed class EconomySystem : SystemBase
    {
        /// <summary>Pay the quota out of the bank; overshoot pays a marker per whole $500 over. Returns the markers.</summary>
        public static int PayQuota(Game g)
        {
            var s = g.State;
            int over = (int)Math.Floor((s.Bank - s.Quota) / (double)Economy.OvershootPerMarker.I);
            s.Bank -= s.Quota;
            Rules.AddMarkers(g, over, "overshoot");
            g.Emit(new SimEvent { T = "paid", Quota = s.Quota, OvershootMarkers = over });
            return over;
        }

        /// <summary>The overshoot markers alone, the quota kept (a won challenge: DECISIONS #80).</summary>
        public static int Overshoot(Game g)
        {
            var s = g.State;
            int over = Math.Max(0, (int)Math.Floor((s.Bank - s.Quota) / (double)Economy.OvershootPerMarker.I));
            Rules.AddMarkers(g, over, "overshoot");
            return over;
        }

        /// <summary>Next night's quota: x1.5, a Boon's -20%; to the nearest $10.</summary>
        public static int NextQuota(Game g)
        {
            var s = g.State;
            double q = (s.Curve == "house" ? s.Quota * Economy.QuotaGrowth.V : s.Quota + Economy.QuotaStep.V) * (s.Next.Discount ? 1 - Wheels.Discount : 1);
            return Js.RoundInt(q / 10) * 10;
        }

        /// <summary>Close the night: the summary card waits for ready. `part` brings everything but the night,
        /// the quota and the next quota.</summary>
        public static void Summarize(Game g, NightSummary part)
        {
            var s = g.State;
            part.Night = s.Night;
            part.Quota = s.Quota;
            part.NextQuota = NextQuota(g);
            s.Summary = part;
            if (s.Phase == Phase.Over) return;
            s.Phase = Phase.Summary;
            s.PhaseAt = g.Tick;
            g.Emit(new SimEvent { T = "summary", Summary = s.Summary });
        }

        public static void StartNight(Game g)
        {
            var s = g.State;
            var sum = s.Summary;
            s.Night++;
            s.Quota = sum.NextQuota;
            s.Bank = Math.Max(s.Bank, s.Curve == "house" ? Economy.BankFloor.I : Economy.EasyBankFloor.I) + (s.Upgrades.Contains("bank500") ? 500 : 0) - s.NextDock;
            s.NextDock = 0;
            // Compound Interest, once for the team (DESIGN.md §12)
            if (s.Players.Any(p => !p.Ghost && p.Charms.Contains("interest")))
            {
                int interest = Math.Min(600, Js.RoundInt(s.Bank * 0.1));
                s.Bank += interest;
                g.Emit(new SimEvent { T = "interest", Amount = interest });
            }
            // three nights a floor, then the stairs down: a fresh progressive on the new floor
            int floor = State.FloorOfNight(s.Night);
            if (floor != s.Floor)
            {
                s.Floor = floor;
                s.Pot = Economy.PotSeed.I;
                g.Emit(new SimEvent { T = "floor", Floor = floor });
            }
            s.Clock = State.NightTicks;
            s.Mods = s.Next.Clone();
            s.Next = new NightMods();
            // a Collector who died at the table is replaced by the next one (DECISIONS #81)
            if (s.Collector.Dead) s.Collector = new CollectorSeat { Idx = s.Collector.Idx + 1, Dead = false };
            s.Marks = s.Mods.Colddeck ? new List<Mark> { new Mark { Id = "colddeck", Player = -1, Left = State.NightTicks, Stacks = 1 } } : new List<Mark>();
            s.Tables = State.NewTables();
            s.Circuit = null;
            s.Wheel = null;
            s.Summary = null;
            for (int i = 0; i < s.Players.Count; i++)
            {
                var p = s.Players[i];
                // with the house, the night begins at home: the car takes you to the casino (DECISIONS #79)
                if (s.Home) State.PlaceAtHome(p, i, s.Players.Count); else State.PlaceAtDoor(p, i, s.Players.Count);
                p.Mx = p.Mz = 0;
                p.Seat = null;
                p.ApronUsed = false;
                p.FootUsed = false;
                p.RedStreak = 0;
                p.HotTable = null;
                p.HotRun = 0;
            }
            if (s.Home) HomeSystem.DrawShop(g);
            s.Phase = s.Home ? Phase.Home : Phase.Floor;
            s.PhaseAt = g.Tick;
            g.Emit(new SimEvent { T = "night", Night = s.Night, Quota = s.Quota, Bank = s.Bank });
        }

        public override string Id => "economy";
        public override string[] Handles => new[] { "ready" };

        public override void Command(Game g, PlayerState p, Command cmd)
        {
            var s = g.State;
            if (s.Phase != Phase.Summary) return;
            if (s.Night >= Economy.Nights.I)
            {
                // the ending: by how many Collectors died at your table (DECISIONS #81)
                var e = Collectors.EndingFor(s.Killed.Count);
                Rules.EndRun(g, true, $"{e.Title}. {e.Text}");
                g.Emit(new SimEvent { T = "ending", Id = e.Id });
            }
            else StartNight(g);
        }
    }

    /// <summary>Stats for the results screen and the pace bots (DESIGN.md §8). The counters live in
    /// State.Stats and are kept where things happen; this system keeps the fixed order's slot.</summary>
    public sealed class StatsSystem : SystemBase
    {
        public override string Id => "stats";
    }
}
