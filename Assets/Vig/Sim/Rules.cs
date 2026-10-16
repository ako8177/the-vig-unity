// Rules shared by the systems: payout factors, lives, marks, markers. Pure functions of the Game.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim
{
    public static class Rules
    {
        public static int Ticks(double s) => Js.RoundInt(s * State.TicksPerS);

        public static Mark MarkOf(Game g, string id, int player) => g.State.Marks.FirstOrDefault(m => m.Id == id && m.Player == player);

        public static bool HasTeamMark(Game g, string id) => g.State.Marks.Any(m => m.Id == id && m.Player == -1);

        /// <summary>Hi-Lo mark stacks on a player.</summary>
        public static int Stacks(Game g, int player) => MarkOf(g, "hilo", player)?.Stacks ?? 0;

        /// <summary>What a payout is multiplied by for this player at this table: Hi-Lo marks (-5% a
        /// stack), Cold Deck (-10%), a drink's edge. DESIGN.md §3, §10; DECISIONS #39.</summary>
        public static double PayoutFactor(Game g, PlayerState p, string table) => FactorOf(g.State, p, table);

        /// <summary>PayoutFactor over a bare State (panels and the HUD print it).</summary>
        public static double FactorOf(State s, PlayerState p, string table)
        {
            double f = 1;
            if (Marks.HiloTables.Contains(table))
                f *= 1 - Marks.HiloPerStack * (s.Marks.FirstOrDefault(m => m.Id == "hilo" && m.Player == p.Id)?.Stacks ?? 0);
            if (s.Marks.Any(m => m.Id == "colddeck" && m.Player == -1)) f *= Marks.ColdDeck;
            var tier = Data.Tables.ById(table)?.Tier;
            if (tier != null)
                foreach (var b in p.Buffs)
                {
                    var d = Drinks.ById(b.Drink);
                    if (d != null && d.Tiers.Contains(tier)) f *= 1 + d.Bonus;
                }
            return f;
        }

        /// <summary>A table's stake for this player: doubled under Overclock, so the win doubles and the odds don't (DECISIONS #54).</summary>
        public static int StakeFor(PlayerState p, int bet) => (int)(bet * (p.Overclock > 0 ? Charms.OverclockFactor : 1));

        /// <summary>Hi-Lo's mark length this night: 30 s, 20 s with the upgrade, 60 s under the Bane.</summary>
        public static double HiloMarkS(Game g)
        {
            if (g.State.Mods.LongMarks) return Wheels.LongMarksS;
            if (g.State.Upgrades.Contains("hilo20")) return 20;
            return Marks.ById("hilo").DurS;
        }

        /// <summary>Put a mark on a player (or the team: player -1). A Boon's clean slate blocks it.</summary>
        public static bool AddMark(Game g, string id, int player)
        {
            var s = g.State;
            if (s.Mods.Immune) return false;
            var def = Marks.ById(id);
            s.Stats.Marks++;
            if (id == "tax")
            {
                int amount = (int)Math.Floor(s.Bank * Marks.Tax);
                s.Bank -= amount;
                g.Emit(new SimEvent { T = "tax", Amount = amount });
                g.Emit(new SimEvent { T = "mark", Player = player, Id = id, Stacks = 1 });
                return true;
            }
            int left = Ticks(id == "hilo" ? HiloMarkS(g) : def.DurS);
            var m = MarkOf(g, id, player);
            if (m != null)
            {
                m.Left = Math.Max(m.Left, left);
                if (id == "hilo") m.Stacks = Math.Min(Marks.HiloMaxStacks, m.Stacks + 1);
            }
            else
            {
                m = new Mark { Id = id, Player = player, Left = left, Stacks = 1 };
                s.Marks.Add(m);
            }
            g.Emit(new SimEvent { T = "mark", Player = player, Id = id, Stacks = m.Stacks });
            return true;
        }

        public static void AddMarkers(Game g, int n, string why)
        {
            if (n <= 0) return;
            g.State.Markers += n;
            g.State.Stats.MarkersEarned += n;
            g.Emit(new SimEvent { T = "markers", Delta = n, Why = why });
        }

        public static void GainLife(Game g, PlayerState p, string why)
        {
            if (p.Ghost) return;
            int before = p.Lives;
            p.Lives = Math.Min(Economy.MaxLives.I, p.Lives + 1);
            if (p.Lives != before) g.Emit(new SimEvent { T = "life", Player = p.Id, Delta = 1, Lives = p.Lives, Why = why });
        }

        /// <summary>Take a life. Lead Apron takes the first one each night instead (DECISIONS #53). At 0
        /// the player is a ghost; when no one living is left, the run is over. Returns lost | apron | none.</summary>
        public static string LoseLife(Game g, PlayerState p, string why)
        {
            if (p.Ghost || p.Lives <= 0) return "none";
            if (p.Charms.Contains("leadapron") && !p.ApronUsed)
            {
                p.ApronUsed = true;
                g.Emit(new SimEvent { T = "life", Player = p.Id, Delta = 0, Lives = p.Lives, Why = why + ": the lead apron took it" });
                return "apron";
            }
            p.Lives--;
            g.State.Stats.LivesLost++;
            g.Emit(new SimEvent { T = "life", Player = p.Id, Delta = -1, Lives = p.Lives, Why = why });
            if (p.Lives == 0)
            {
                p.Ghost = true;
                if (p.Seat != null)
                {
                    var table = p.Seat;
                    p.Seat = null;
                    g.Emit(new SimEvent { T = "stood", Player = p.Id, Table = table });
                }
                g.Emit(new SimEvent { T = "ghost", Player = p.Id });
                if (g.State.Players.All(o => o.Ghost))
                    EndRun(g, false, g.State.Players.Count == 1 ? "OUT OF LIVES. A GHOST ALONE CANNOT BE BOUGHT BACK." : "EVERY SOUL BELONGS TO THE HOUSE.");
            }
            return "lost";
        }

        public static void EndRun(Game g, bool won, string why)
        {
            var s = g.State;
            if (s.Phase == Phase.Over) return;
            s.Phase = Phase.Over;
            s.PhaseAt = g.Tick;
            s.Result = new Done { Won = won, Why = why };
            g.Emit(new SimEvent { T = "over", Won = won, Why = why });
        }

        public static List<PlayerState> Living(Game g) => g.State.Players.Where(p => !p.Ghost).ToList();

        public static double Dist2(double x, double z, V2 at) => (x - at.X) * (x - at.X) + (z - at.Z) * (z - at.Z);
    }
}
