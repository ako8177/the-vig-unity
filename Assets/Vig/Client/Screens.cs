// The table screens: one world-space panel per table, on the dealer's display (or the Bandit's glass),
// and the Collector's on his felt. Odds are always printed (CLAUDE.md): tier, win chance, payout and
// what losing costs, before a bet can be placed; only the Blackout team mark hides them, and it says so.
// Panels read the sim's state; they never write it (Play sends the commands).
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Systems;
using Vig.Sim.Tables;

namespace Vig.Client
{
    public sealed class TableScreen
    {
        public readonly string Id;
        readonly TableDef def;
        readonly TextMeshProUGUI title, tier, odds, how, bet, result, hint;
        readonly Image glow;
        /// <summary>Twenty-One's cards on the felt: the screen prints only what's been dealt so far</summary>
        public TwentyOneShow Deal;

        public TableScreen(string id, Transform screen, float w, float h)
        {
            Id = id;
            def = Vig.Sim.Data.Tables.ById(id);
            var tc = Look.TierColor(def.Tier);
            var c = Ui.WorldCanvas("panel " + id, screen, w, h);
            var t = c.transform;
            Ui.Panel("bg", t, new Color(0.012f, 0.01f, 0.018f, 1), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            glow = Ui.Panel("tier bar", t, tc * 1.4f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), Vector2.zero);
            Ui.Panel("rule", t, new Color(1, 1, 1, 0.08f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -104), new Vector2(-20, -102));
            title = Ui.Text("title", t, def.Name.ToUpperInvariant(), 34, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -60), new Vector2(-150, -14), true);
            tier = Ui.Text("tier", t, def.Tier.ToUpperInvariant(), 22, tc, TextAlignmentOptions.TopRight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-170, -52), new Vector2(-22, -18), true);
            how = Ui.Text("how", t, def.HowText, 16, new Color(0.75f, 0.72f, 0.8f), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -98), new Vector2(-22, -62));
            odds = Ui.Text("odds", t, "", 20, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -178), new Vector2(-22, -112));
            bet = Ui.Text("bet", t, "", 22, Look.Brass, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -214), new Vector2(-22, -182), true);
            result = Ui.Text("result", t, "", 30, Color.white, TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 50), new Vector2(-22, 146), true);
            hint = Ui.Text("hint", t, "", 15, new Color(0.6f, 0.58f, 0.66f), TextAlignmentOptions.Bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 44));
        }

        static string Pct(double p) => (p * 100).ToString(p < 0.1 ? "0.0" : "0") + "%";

        /// <summary>A symbol if the font can draw it, else a fallback.</summary>
        public static string Glyph(char c, string fallback)
        {
            var f = Neon.Font;
            return f != null && f.HasCharacter(c, true, true) ? c.ToString() : fallback;
        }

        static string[] suits;
        public static string Card(Card c)
        {
            suits = suits ?? new[] { Glyph('♥', "h"), Glyph('♦', "d"), Glyph('♣', "c"), Glyph('♠', "s") };
            string s = TableModule.Rank[c.R] + suits[c.S];
            return c.S < 2 ? Ui.Col(s, new Color(1f, 0.25f, 0.35f)) : s;
        }

        /// <summary>A die's face: its number in a lit box.</summary>
        public static string Die(int d) => $"<mark=#ffffff22 padding=8,8,2,2><b> {d} </b></mark>";

        public void Refresh(Game g, PlayerState me, int chosenBet)
        {
            var s = g.State;
            var t = s.Tables[Id];
            var mod = TableModule.ById(Id);
            bool blackout = Rules.HasTeamMark(g, "blackout");
            double factor = Rules.FactorOf(s, me, Id);
            var tc = Look.TierColor(def.Tier);
            if (blackout)
                odds.text = Ui.Col("BLACKOUT: THE ODDS ARE HIDDEN ON EVERY TABLE", Look.Blood) + "\n" + Ui.Col("LOSING COSTS  ", Look.Smoke) + def.LossText;
            else
            {
                var o = mod.Odds();
                odds.text = $"{Ui.Col("WIN", Look.Smoke)} {Ui.Col(Pct(o.WinP), tc)}    {Ui.Col("PAYS", Look.Smoke)} {def.PaysText}    {Ui.Col("RTP", Look.Smoke)} {o.Rtp:0.###}\n" +
                    $"{Ui.Col("LOSING COSTS", Look.Smoke)}  {Ui.Col(def.LossText, def.Tier == "high" ? Look.High : def.Tier == "mid" ? Look.MidHi : Look.Bone)}";
                if (Id == SideBets.Pairs.Table)
                {
                    var sb = SideBets.PairsFor(me);
                    odds.text += $"\n<size=15>{Ui.Col("SIDE: PAIRS", Look.Gold)} {Ui.Money(sb.Mins * Vig.Sim.Data.Tables.LimitsOf(def, s.Floor).Min)}  {Ui.Col("WIN", Look.Smoke)} {Pct(SideBets.WinP(sb))}  {Ui.Col("PAYS", Look.Smoke)} PAIR {sb.Mixed}:1, COLOR {sb.Colored}:1, SUITED {sb.Suited}:1  {Ui.Col("RTP", Look.Smoke)} {SideBets.ExactRtp(sb):0.###}</size>";
                }
            }
            string f = System.Math.Abs(factor - 1) > 1e-9 ? Ui.Col($"   PAYOUTS x{factor:0.##}", factor < 1 ? Look.Blood : Look.Low) : "";
            string oc = me.Overclock > 0 ? Ui.Col("   OVERCLOCK x2", Look.Soul) : "";
            bet.text = $"BET {Ui.Money(Rules.StakeFor(me, chosenBet))}{f}{oc}";
            if (Id == "redline" && me.RedStreak > 0)
                bet.text += Ui.Col($"   STREAK {me.RedStreak}/{RedLine.StreakLimit(g)}", Look.High);
            if (Id == "hilo" && Rules.Stacks(g, me.Id) > 0) bet.text += Ui.Col($"   MARKED x{Rules.Stacks(g, me.Id)}", Look.Blood);
            result.text = ResultText(g, t);
            bool seated = me.Seat == Id;
            hint.text = seated ? Controls() : "";
            glow.color = tc * (seated ? 2.2f : 1.2f);
        }

        string Controls()
        {
            switch (Id)
            {
                case "hilo": return "H  HI     L  LO     [ ]  BET     X  STAND";
                case "redline": return "R  RED     B  BLACK     [ ]  BET     X  STAND";
                case "twentyone": return "SPACE  DEAL     H  HIT     S  STAND     D  DOUBLE     P  SPLIT     [ ]  BET     X  UP";
                case "bandit": return "SPACE  PULL     [ ]  BET     X  STAND";
                case "wheel": return "SPACE  SPIN     [ ]  BET     X  STAND";
                default: return "SPACE  ROLL     [ ]  BET     X  STAND";
            }
        }

        string ResultText(Game g, TableState t)
        {
            if (Id == "twentyone" && t.Hand != null && t.Hand.Stage == "play")
            {
                var h = t.Hand;
                var sb = new StringBuilder();
                // only what's been dealt onto the felt so far (TwentyOneShow brings it out a card at a time)
                int dn = Deal?.ShownDealer(h.Dealer.Count) ?? h.Dealer.Count;
                sb.Append(Ui.Col("DEALER  ", Look.Smoke)).Append(dn > 0 ? Card(h.Dealer[0]) : "").Append("  ").Append(dn > 1 ? Ui.Col("[?]", Look.Smoke) : "").Append('\n');
                for (int i = 0; i < h.Hands.Count; i++)
                {
                    var hh = h.Hands[i];
                    var cards = hh.Cards.Take(Deal?.Shown(i, hh.Cards.Count) ?? hh.Cards.Count).ToList();
                    var v = TwentyOne.HandValue(cards);
                    sb.Append(i == h.Active ? "> " : "  ").Append(string.Join(" ", cards.Select(Card))).Append(cards.Count > 0 ? Ui.Col($"   {(v.soft ? "SOFT " : "")}{v.total}", Look.Brass) : "").Append('\n');
                }
                string side = "";
                if (h.Side > 0 && (Deal?.Shown(0, 2) ?? 2) >= 2)
                    side = "\n<size=18>" + (h.SideRet > 0 ? Ui.Col($"PAIRS: {h.SideWord}! {Ui.Money(h.SideRet)} COMING", Look.Gold) : Ui.Col("PAIRS: NO PAIR", Look.Smoke)) + "</size>";
                return $"<size=24>{sb}</size>" + RegularsLine(h.Regulars, false) + side;
            }
            if (t.Pending != null)
                return Ui.Col(Id == "bandit" ? "THE REELS SPIN..." : Id == "wheel" ? "THE WHEEL TURNS..." : Id == "redline" || Id == "twentyone" ? "THE CARD TURNS..."
                    : Id == "roulette" ? "THE BALL RUNS..." : Id == "pegdrop" ? "THE BALL DROPS..." : Id == "crash" ? "..." : "THE DICE ROLL...", Look.Smoke);
            // craps between throws: the point is on and the pass-line bet rides
            if (Id == "craps" && t.Point != 0)
                return Ui.Col($"THE POINT IS {t.Point}", Look.Gold) + "\n" + Ui.Col($"<size=24>{Ui.Money(t.PointBet)} RIDES: HIT IT AGAIN BEFORE A 7</size>", Look.Smoke);
            var o = t.Last;
            if (o == null) return Ui.Col("PLACE YOUR BET", Look.Smoke);
            Color c = o.Win ? Look.LowHi : o.LifeLost == true ? Look.Blood : Look.Bone;
            string face = "";
            // the Bandit's grid is on its reels (BanditShow); the others print their face beside the verdict
            if (o.Reels != null) face = "";
            else if (o.Dice != null) face = string.Join(" ", o.Dice.Select(Die)) + "   ";
            else if (o.Card != null) face = Card(o.Card) + "  ";
            else if (o.Slot != null) face = Ui.Col(WheelOfMarks.Layout[o.Slot.Value].ToUpperInvariant(), WheelColor(WheelOfMarks.Layout[o.Slot.Value])) + "  ";
            else if (o.Dealer21 != null) face = $"<size=20>{Ui.Col("DEALER", Look.Smoke)} {string.Join(" ", o.Dealer21.Select(Card))}   {Ui.Col("YOU", Look.Smoke)} {string.Join(" / ", o.Hands21.Select(h => string.Join(" ", h.Cards.Select(Card))))}</size>\n";
            return face + Ui.Col(o.Text, c) + (o.Regulars21 != null ? "\n" + RegularsLine(o.Regulars21, true) : "");
        }

        /// <summary>The regulars (U29) on one small line: their cards as far as they're dealt, or how they did.</summary>
        string RegularsLine(List<RegularHand21> regs, bool done)
        {
            if (regs == null || regs.Count == 0) return "";
            var parts = new List<string>();
            foreach (var r in regs)
            {
                var def = Regulars.ById(r.Who);
                if (def == null) continue;
                var cards = done ? r.Cards : r.Cards.Take(Deal?.ShownRegular(def.Seat, r.Cards.Count) ?? r.Cards.Count).ToList();
                if (cards.Count == 0) continue;
                string tail = done ? Ui.Col(r.Word, r.Ret > r.Bet ? Look.LowHi : r.Ret == r.Bet ? Look.Bone : Look.Smoke)
                    : Ui.Col(TwentyOne.HandValue(cards).total.ToString(), Look.Brass);
                parts.Add($"{Ui.Col(def.Name.ToUpperInvariant(), Look.Gold)} {string.Join(" ", cards.Select(Card))} {tail}");
            }
            return parts.Count == 0 ? "" : $"<size=17>{string.Join("    ", parts)}</size>";
        }

        public static Color WheelColor(string c) =>
            c == "red" ? new Color(1f, 0.25f, 0.3f) : c == "black" ? Look.Smoke : c == "purple" ? Look.Coll : c == "gold" ? Look.Gold : Look.Blood;
    }

    /// <summary>The Collector's screen on his felt: the choice, the levers, the wheel, the night's end.</summary>
    public sealed class CollectorScreen
    {
        readonly TextMeshProUGUI head, body, hint;

        public CollectorScreen(Transform screen, float w, float h)
        {
            var c = Ui.WorldCanvas("panel collector", screen, w, h);
            var t = c.transform;
            Ui.Panel("bg", t, new Color(0.02f, 0.008f, 0.03f, 1), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Ui.Panel("tier bar", t, Look.Coll * 1.6f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), Vector2.zero);
            head = Ui.Text("head", t, "THE COLLECTOR", 32, Look.CollHi, TextAlignmentOptions.Top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -60), new Vector2(-20, -14), true);
            body = Ui.Text("body", t, "", 20, Color.white, TextAlignmentOptions.Top, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 48), new Vector2(-24, -66));
            hint = Ui.Text("hint", t, "", 15, new Color(0.65f, 0.6f, 0.72f), TextAlignmentOptions.Bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 44));
        }

        public void Refresh(Game g)
        {
            var s = g.State;
            var who = Collectors.Of(s.Collector.Idx).Name;
            head.text = who;
            var r = Circuit.For(s.Floor);
            switch (s.Phase)
            {
                case Phase.Floor:
                    body.text = $"QUOTA {Ui.Money(s.Quota)}\n{Ui.Col("AT 0:00 YOU PAY IT, OR PLAY ME FOR IT.", Look.Smoke)}";
                    hint.text = "";
                    break;
                case Phase.Midnight:
                    body.text = Ui.Col("MIDNIGHT. SIT DOWN.", Look.CollHi);
                    hint.text = "";
                    break;
                case Phase.Choice:
                    body.text = $"YOU OWE {Ui.Money(s.Quota)}.  THE BANK HOLDS {Ui.Money(s.Bank)}.\n\n" +
                        $"{Ui.Col("PAY", Look.Brass)}: THE QUOTA LEAVES THE BANK.\n" +
                        $"{Ui.Col("CHALLENGE", Look.Coll)}: {r.Levers} LEVERS, {(s.Mods.Overloads3 ? r.Live + 1 : r.Live)} LIVE. TWO SHOCKS END YOU (A LIFE, THE QUOTA IS HIS);\n" +
                        $"TWO SHOCKS KILL HIM (KEEP THE QUOTA). YOU WIN {Pct(s.Mods.Overloads3 ? r.OddsBane : r.OddsWin)} ({Pct(r.OddsWithKit)} WITH THE INSULATED KIT).";
                    hint.text = "P  PAY        G  CHALLENGE";
                    break;
                case Phase.Circuit:
                {
                    string Glyph(char ch, string fb) => TableScreen.Glyph(ch, fb);
                    var c = s.Circuit;
                    var sb = new StringBuilder("<mspace=0.7em>");
                    for (int i = 0; i < c.Levers.Count; i++)
                    {
                        var lv = c.Levers[i];
                        string mark = !lv.Known ? "?" : lv.Live ? Glyph('⚡', "!") : "-";
                        Color col = !lv.Known ? Look.Bone : lv.Live ? Look.Blood : Look.Smoke;
                        sb.Append(Ui.Col(((i + 1) % 10).ToString() + (lv.Pulled ? "↓" : "↑") + mark, lv.Pulled ? col * 0.7f : col)).Append(' ');
                    }
                    sb.Append("</mspace>\n\n");
                    sb.Append($"YOUR SHOCKS {Ui.Col(string.Join(" ", c.Shocks), Look.Blood)} / {r.PlayerShocks}    HIS {Ui.Col(c.HisShocks.ToString(), Look.Coll)} / {r.CollShocks}\n\n");
                    sb.Append(string.Join("\n", c.Log.Take(3).Select((l, i) => i == 0 ? l : Ui.Col(l, Look.Smoke))));
                    body.text = sb.ToString();
                    hint.text = c.Done != null ? "" : c.Turn == "team" ? "1-0  PULL A LEVER" + (c.Traces.Any(x => x > 0) ? "      SHIFT + 1-0  TRACE A WIRE" : "") : "HIS PULL...";
                    break;
                }
                case Phase.Wheel:
                {
                    var w = s.Wheel;
                    var slots = w.Kind == "boon" ? Wheels.Boons : Wheels.Banes;
                    body.text = Ui.Col(w.Kind == "boon" ? "THE BOON WHEEL" : "THE BANE WHEEL", w.Kind == "boon" ? Look.Soul : Look.Blood) + "\n\n" +
                        string.Join("\n", slots.Select((sl, i) => w.Slot == i && w.Applied ? Ui.Col("> " + sl.Label + " <", Look.Gold) : sl.Label));
                    hint.text = w.Slot == null ? "SPACE  SPIN" : w.Applied ? "" : "...";
                    break;
                }
                case Phase.Summary:
                {
                    var m = s.Summary;
                    body.text = $"NIGHT {m.Night}: {m.How.ToUpperInvariant()}\n\n" +
                        (m.How == "short" ? $"SHORT {Ui.Money(m.Short)}. LIVES TAKEN: {m.LivesTaken}\n" : "") +
                        (m.OvershootMarkers > 0 ? $"OVERSHOOT: +{m.OvershootMarkers} MARKERS\n" : "") +
                        (m.Circuit != null ? m.Circuit.Why + "\n" : "") +
                        (m.Wheel != null ? $"{m.Wheel.Kind.ToUpperInvariant()}: {m.Wheel.Id.ToUpperInvariant()}\n" : "") +
                        (m.Night < Economy.Nights.I ? $"\nTOMORROW'S QUOTA {Ui.Money(m.NextQuota)}" : "\nTHE LAST NIGHT IS DONE.");
                    hint.text = "ENTER  " + (m.Night < Economy.Nights.I ? "NEXT NIGHT" : "WALK OUT");
                    break;
                }
                case Phase.Over:
                    body.text = s.Result != null ? Ui.Col(s.Result.Won ? "YOU WALK OUT." : "THE HOUSE KEEPS YOU.", s.Result.Won ? Look.Soul : Look.Blood) + "\n\n" + s.Result.Why : "";
                    hint.text = "ENTER  A NEW RUN";
                    break;
                default:
                    body.text = "";
                    hint.text = "";
                    break;
            }
        }

        static string Pct(double p) => Mathf.RoundToInt((float)p * 100) + "%";
    }
}
