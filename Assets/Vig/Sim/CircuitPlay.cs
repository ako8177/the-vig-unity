// The Circuit (DESIGN.md §6; DECISIONS #80): the owner's lever duel. Ten levers under the Collector's
// table, some wired live to the battery. You and he take turns pulling one, you first. A dead lever pays
// the team; a live one shocks whoever pulled it. Your second shock ends it: a life, and the quota is
// paid. His second shock kills him: you keep the quota. He picks at random; so do you, unless the
// Insulated Kit lets you trace a wire. Pure functions of the Game.
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim
{
    public static class CircuitPlay
    {
        public static CircuitState New(Game g)
        {
            var r = Circuit.For(g.State.Floor);
            // the Bane: a fourth live lever (DESIGN.md §7)
            int live = g.State.Mods.Overloads3 ? Wheels.BigOverloads : r.Live;
            var states = Enumerable.Range(0, r.Levers).Select(i => i < live).ToList();
            g.Rng.Shuffle(states);
            return new CircuitState
            {
                Levers = states.Select(l => new Lever { Live = l }).ToList(),
                Live = live,
                Shocks = g.State.Players.Select(_ => 0).ToList(),
                HisShocks = 0,
                Traces = g.State.Players.Select(p => p.Charms.Contains("insulated") ? r.KitTraces : 0).ToList(),
                Turn = "team",
                Wait = 0,
                Survived = new List<int>(),
                Log = new List<string> { "YOUR PULL." },
                Done = null,
                EndedAt = 0,
            };
        }

        static void Say(CircuitState c, string line)
        {
            c.Log.Insert(0, line);
            if (c.Log.Count > 4) c.Log.RemoveRange(4, c.Log.Count - 4);
        }

        /// <summary>End it: the team won (he's dead) or lost. Lucky Fuse pays a survivor of a shock on a win.</summary>
        static void Finish(Game g, CircuitState c, bool won, string why)
        {
            if (won)
                foreach (var id in c.Survived)
                {
                    var p = g.State.Players[id];
                    if (p.Charms.Contains("luckyfuse") && !p.Ghost) Rules.GainLife(g, p, "lucky fuse");
                }
            c.Done = new Done { Won = won, Why = why };
            c.EndedAt = g.Tick;
            g.Emit(new SimEvent { T = "circuitEnd", Won = won, Why = why });
        }

        /// <summary>The levers still up, with their indices.</summary>
        public static List<int> Up(CircuitState c) => Enumerable.Range(0, c.Levers.Count).Where(i => !c.Levers[i].Pulled).ToList();

        /// <summary>A team action: activate pulls a lever (uses the turn), inspect traces one wire with the
        /// Insulated Kit (free). Returns a refusal reason, or null when it was taken.</summary>
        public static string TeamAct(Game g, PlayerState p, string act, int a)
        {
            var c = g.State.Circuit;
            if (c == null || c.Done != null) return "no circuit";
            if (act == "reroute") return "the wires are too tangled to move";
            if (c.Turn != "team") return "it's his pull";
            if (a < 0 || a >= c.Levers.Count) return "no such lever";
            if (p.Ghost) return "a ghost cannot pull";
            var r = Circuit.For(g.State.Floor);
            var lv = c.Levers[a];
            if (lv.Pulled) return "already pulled";
            if (act == "inspect")
            {
                if (c.Traces[p.Id] <= 0) return "no traces left (the Insulated Kit gives two)";
                if (lv.Known) return "already traced";
                c.Traces[p.Id]--;
                lv.Known = true;
                string tt = $"YOU TRACE #{a + 1}: {(lv.Live ? "LIVE" : "DEAD")}.";
                Say(c, tt);
                g.Emit(new SimEvent { T = "circuit", Who = "team", Action = "inspect", Slot = a, Over = lv.Live, Text = tt });
                return null;
            }
            // a lever you traced live is one you'd only pull with nothing else left
            if (lv.Known && lv.Live && Up(c).Any(i => !(c.Levers[i].Known && c.Levers[i].Live))) return "you traced it live: pull another";
            lv.Pulled = true;
            lv.Known = true;
            lv.By = "team";
            if (lv.Live)
            {
                c.Shocks[p.Id]++;
                int n = c.Shocks[p.Id];
                if (n >= r.PlayerShocks)
                {
                    var lost = Rules.LoseLife(g, p, "circuit");
                    string t2 = $"#{a + 1} IS LIVE. YOUR SECOND SHOCK{(lost == "apron" ? ": THE LEAD APRON SAVES THE LIFE" : "")}.";
                    Say(c, t2);
                    g.Emit(new SimEvent { T = "circuit", Who = "team", Action = "activate", Slot = a, Over = true, Text = t2 });
                    Finish(g, c, false, lost == "apron" ? "TWO SHOCKS. THE APRON KEPT YOUR LIFE; THE QUOTA IS HIS." : "TWO SHOCKS. A LIFE, AND THE QUOTA IS HIS.");
                    return null;
                }
                string text = $"#{a + 1} IS LIVE. A SHOCK: {n} OF {r.PlayerShocks}.";
                // surviving a shock is what the fuse charms are for
                if (!c.Survived.Contains(p.Id)) c.Survived.Add(p.Id);
                if (p.Charms.Contains("overclock"))
                {
                    p.Overclock = Rules.Ticks(Charms.OverclockS);
                    g.Emit(new SimEvent { T = "overclock", Player = p.Id });
                    text += " OVERCLOCKED.";
                }
                Say(c, text);
                g.Emit(new SimEvent { T = "circuit", Who = "team", Action = "activate", Slot = a, Over = true, Text = text });
            }
            else
            {
                g.State.Bank += r.SafeBank;
                int markers = r.SafeMarkers * (p.Charms.Contains("magnet") ? 2 : 1);
                Rules.AddMarkers(g, markers, "dead lever");
                string text = $"#{a + 1} IS DEAD: +${r.SafeBank}, +{markers} MARKER{(markers == 1 ? "" : "S")}.";
                Say(c, text);
                g.Emit(new SimEvent { T = "circuit", Who = "team", Action = "activate", Slot = a, Over = false, Text = text });
            }
            if (Rules.Living(g).Count == 0) { Finish(g, c, false, "NO ONE IS LEFT STANDING."); return null; }
            c.Turn = "coll";
            c.Wait = g.Tick + Rules.Ticks(lv.Live ? r.AfterShockS : r.CollectorTurnS);
            return null;
        }

        /// <summary>The lever he takes: any one still up, at random (the owner's rule: he doesn't count up).</summary>
        public static int CollectorPick(Game g, CircuitState c)
        {
            var open = Up(c);
            return open.Count > 0 ? g.Rng.Pick(open) : -1;
        }

        /// <summary>The Collector's pull.</summary>
        public static void CollectorAct(Game g)
        {
            var c = g.State.Circuit;
            if (c == null || c.Done != null || c.Turn != "coll") return;
            var r = Circuit.For(g.State.Floor);
            int i = CollectorPick(g, c);
            if (i < 0) { Finish(g, c, false, "NO LEVER LEFT. TIES GO TO THE HOUSE."); return; }
            var lv = c.Levers[i];
            lv.Pulled = true;
            lv.Known = true;
            lv.By = "coll";
            string text;
            if (lv.Live)
            {
                c.HisShocks++;
                int limit = r.CollShocks;
                text = $"HE PULLS #{i + 1}: LIVE. {(c.HisShocks >= limit ? "HIS LAST SHOCK." : $"A SHOCK: {c.HisShocks} OF {limit}.")}";
            }
            else text = $"HE PULLS #{i + 1}: DEAD.";
            Say(c, text);
            g.Emit(new SimEvent { T = "circuit", Who = "coll", Action = "activate", Slot = i, Over = lv.Live, Text = text });
            if (c.HisShocks >= r.CollShocks) { Finish(g, c, true, "TWO SHOCKS. THE COLLECTOR IS DEAD: KEEP THE QUOTA."); return; }
            if (Up(c).Count == 0) { Finish(g, c, false, "NO LEVER LEFT. TIES GO TO THE HOUSE."); return; }
            c.Turn = "team";
        }
    }
}
