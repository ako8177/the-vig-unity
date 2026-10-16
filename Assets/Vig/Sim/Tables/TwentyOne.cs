// Twenty-One (low), dealt from a real shoe (DECISIONS #76): six decks shuffled by the sim's RNG,
// reshuffled when a quarter is left; the dealer hits soft 17 and peeks for a natural (a dealer natural
// ends the hand at once; a player natural pays 3:2); ties push. You may hit, stand, double on any first
// two cards (after a split too) and split a pair once (aces take one card each). The printed return is
// basic strategy's, by dynamic programming over an endless shoe (DESIGN.md §3). Two regulars share the
// shoe (U29, DESIGN.md §13): dealt round with you, first base playing before you and third base after;
// their money is their own, and the cards they take are as random as the ones you get.
using System;
using System.Collections.Generic;
using System.Linq;
using Vig.Sim.Data;

namespace Vig.Sim.Tables
{
    public sealed class TwentyOne : TableModule
    {
        public const int Decks = 6, ShoeSize = Decks * 52, ReshuffleAt = ShoeSize / 4;

        /// <summary>A card's value: ace 1, face cards 10.</summary>
        public static int CardValue(Card c) => Math.Min(10, c.R);

        /// <summary>A hand's best total and whether an ace is counting 11.</summary>
        public static (int total, bool soft) HandValue(List<Card> cards)
        {
            int t = 0; bool ace = false;
            foreach (var c in cards)
            {
                t += CardValue(c);
                if (c.R == 1) ace = true;
            }
            if (ace && t + 10 <= 21) return (t + 10, true);
            return (t, false);
        }

        public static bool IsNatural(List<Card> cards) => cards.Count == 2 && HandValue(cards).total == 21;

        /// <summary>Shuffle a fresh shoe (Fisher-Yates on the sim's RNG). Cards are codes 0..311.</summary>
        static List<int> Shuffle(Game g)
        {
            var shoe = Enumerable.Range(0, ShoeSize).ToList();
            for (int i = shoe.Count - 1; i > 0; i--)
            {
                int j = g.Rng.Int(0, i);
                int tmp = shoe[i]; shoe[i] = shoe[j]; shoe[j] = tmp;
            }
            return shoe;
        }

        /// <summary>The next card off the shoe (a fresh shoe if it ran dry mid-hand, which six decks never do).</summary>
        static Card Deal(Game g)
        {
            var t = g.State.Tables["twentyone"];
            if (t.Shoe == null || t.Shoe.Count == 0) t.Shoe = Shuffle(g);
            int c = t.Shoe[t.Shoe.Count - 1] % 52;
            t.Shoe.RemoveAt(t.Shoe.Count - 1);
            return new Card(c / 4 + 1, c % 4);
        }

        /// <summary>The dealer draws to 17 and hits soft 17.</summary>
        public static void DealerPlays(Game g, List<Card> dealer)
        {
            for (;;)
            {
                var v = HandValue(dealer);
                if (v.total > 17 || (v.total == 17 && !v.soft)) return;
                dealer.Add(Deal(g));
            }
        }

        // ---- the exact odds (endless shoe): dealer final-total distributions, then basic strategy's EV.
        // Distributions are insertion-ordered lists, as the TS sim's Maps are, so every sum adds in the
        // same order and a near-tie in Advise breaks the same way.

        static readonly int[] Vals = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10 };
        const double P = 1.0 / 13;

        sealed class Dist
        {
            public readonly List<int> Keys = new List<int>();
            public readonly List<double> Ps = new List<double>();
            public void Add(int k, double p)
            {
                int i = Keys.IndexOf(k);
                if (i < 0) { Keys.Add(k); Ps.Add(p); } else Ps[i] += p;
            }
            public void Set(int k, double p)
            {
                int i = Keys.IndexOf(k);
                if (i < 0) { Keys.Add(k); Ps.Add(p); } else Ps[i] = p;
            }
        }

        static (int, bool) StepHand(int total, bool soft, int v)
        {
            int t = total + v; bool s = soft;
            if (v == 1 && t + 10 <= 21) { t += 10; s = true; }
            if (t > 21 && s) { t -= 10; s = false; }
            return (t, s);
        }

        static readonly Dictionary<(int, bool), Dist> dealerMemo = new Dictionary<(int, bool), Dist>();
        static Dist DealerFrom(int total, bool soft)
        {
            if (dealerMemo.TryGetValue((total, soft), out var hit)) return hit;
            var d = new Dist();
            if (total > 21) d.Set(22, 1);
            else if (total > 17 || (total == 17 && !soft)) d.Set(total, 1);
            else
                foreach (var v in Vals)
                {
                    var (t, s) = StepHand(total, soft, v);
                    var sub = DealerFrom(t, s);
                    for (int i = 0; i < sub.Keys.Count; i++) d.Add(sub.Keys[i], sub.Ps[i] * P);
                }
            dealerMemo[(total, soft)] = d;
            return d;
        }

        /// <summary>The dealer's final distribution from an upcard, given the hole card doesn't make a natural.</summary>
        static readonly Dictionary<int, (Dist dist, double noNat)> upMemo = new Dictionary<int, (Dist, double)>();
        static (Dist dist, double noNat) DealerFromUp(int up)
        {
            if (upMemo.TryGetValue(up, out var hit)) return hit;
            var dist = new Dist();
            double mass = 0;
            foreach (var v in Vals)
            {
                if ((up == 1 && v == 10) || (up == 10 && v == 1)) continue;
                var (t0, s0) = StepHand(up == 1 ? 11 : up, up == 1, v);
                var sub = DealerFrom(t0, s0);
                for (int i = 0; i < sub.Keys.Count; i++) dist.Add(sub.Keys[i], sub.Ps[i] * P);
                mass += P;
            }
            for (int i = 0; i < dist.Ps.Count; i++) dist.Ps[i] = dist.Ps[i] / mass;
            var r = (dist, mass);
            upMemo[up] = r;
            return r;
        }

        static double StandEV(int t, Dist d)
        {
            double ev = 0;
            for (int i = 0; i < d.Keys.Count; i++)
            {
                int k = d.Keys[i]; double p = d.Ps[i];
                ev += k == 22 || t > k ? p : t < k ? -p : 0;
            }
            return ev;
        }

        static readonly Dictionary<(int, bool, int), (double ev, bool hit)> playMemo = new Dictionary<(int, bool, int), (double, bool)>();
        /// <summary>The best play and its EV (per $1, profit) for a non-bust total against a dealer upcard.</summary>
        public static (double ev, bool hit) Best(int total, bool soft, int up)
        {
            if (playMemo.TryGetValue((total, soft, up), out var m)) return m;
            var d = DealerFromUp(up).dist;
            double st = StandEV(total, d);
            double h = 0;
            if (total < 21)
                foreach (var v in Vals)
                {
                    var (t, s) = StepHand(total, soft, v);
                    h += P * (t > 21 ? -1 : Best(t, s, up).ev);
                }
            else h = double.NegativeInfinity;
            var r = (Math.Max(st, h), h > st);
            playMemo[(total, soft, up)] = r;
            return r;
        }

        /// <summary>Doubling: one card, then stand, at twice the stake.</summary>
        static double DoubleEV(int total, bool soft, int up)
        {
            var d = DealerFromUp(up).dist;
            double ev = 0;
            foreach (var v in Vals)
            {
                var (t, _) = StepHand(total, soft, v);
                ev += P * (t > 21 ? -1 : StandEV(t, d));
            }
            return 2 * ev;
        }

        /// <summary>A two-card hand's best EV: hit/stand, or double.</summary>
        static double TwoCardEV(int total, bool soft, int up) => Math.Max(Best(total, soft, up).ev, DoubleEV(total, soft, up));

        /// <summary>Splitting a pair of value a: two hands, each its own first card plus one (no resplit).</summary>
        static double SplitEV(int a, int up)
        {
            double ev = 0;
            foreach (var v in Vals)
            {
                var (t1, s1) = StepHand(0, false, a);
                var (t, s) = StepHand(t1, s1, v);
                // split aces take one card and stand; any other split hand plays on (and may double)
                ev += P * (a == 1 ? StandEV(t, DealerFromUp(up).dist) : TwoCardEV(t, s, up));
            }
            return 2 * ev;
        }

        /// <summary>The best move for a hand against the dealer's upcard value (basic strategy, endless shoe).</summary>
        public static string Advise(List<Card> cards, int up, bool canDouble, bool canSplit)
        {
            var (total, soft) = HandValue(cards);
            var hs = Best(total, soft, up);
            string move = hs.hit ? "hit" : "stand";
            double ev = hs.ev;
            if (canDouble)
            {
                double d = DoubleEV(total, soft, up);
                if (d > ev) { ev = d; move = "double"; }
            }
            if (canSplit && SplitEV(CardValue(cards[0]), up) > ev) move = "split";
            return move;
        }

        /// <summary>The exact return per $1 with basic strategy over an endless shoe.</summary>
        public static double ExactRtp()
        {
            double total = 0;
            for (int up = 1; up <= 10; up++)
            {
                double pUp = up == 10 ? 4.0 / 13 : 1.0 / 13;
                double noNat = DealerFromUp(up).noNat;
                double pDealerNat = 1 - noNat;
                double ev = 0;
                foreach (var a in Vals)
                    foreach (var b in Vals)
                    {
                        double pp = P * P;
                        bool nat = (a == 1 && b == 10) || (a == 10 && b == 1);
                        if (nat) { ev += pp * noNat * 1.5; continue; }
                        var (t1, s1) = StepHand(0, false, a);
                        var (t, s) = StepHand(t1, s1, b);
                        double play = a == b ? Math.Max(TwoCardEV(t, s, up), SplitEV(a, up)) : TwoCardEV(t, s, up);
                        ev += pp * (pDealerNat * -1 + noNat * play);
                    }
                total += pUp * ev;
            }
            return 1 + total;
        }

        // ---- the round

        /// <summary>Can the active hand double (two cards, not split aces) or split (an unsplit pair)?</summary>
        public static (bool dbl, bool split) Options(Hand21 h)
        {
            var cur = h.Active < h.Hands.Count ? h.Hands[h.Active] : null;
            bool two = cur != null && cur.Cards.Count == 2 && !cur.Aces;
            return (two, two && h.Hands.Count == 1 && CardValue(cur.Cards[0]) == CardValue(cur.Cards[1]));
        }

        /// <summary>A regular plays their hand: the book (basic strategy, doubling) or a hunch (the dealer's
        /// own rule: hit to 17). Nobody plays into a natural.</summary>
        static void PlayRegular(Game g, Hand21 hand, RegularHand21 r)
        {
            if (r.Done) return;
            r.Done = true;
            if (IsNatural(r.Cards)) return;
            bool book = Regulars.ById(r.Who)?.Style == "book";
            int up = CardValue(hand.Dealer[0]);
            for (int k = 0; k < 12; k++)
            {
                var v = HandValue(r.Cards);
                if (v.total >= 21) return;
                string mv = book ? Advise(r.Cards, up, r.Cards.Count == 2, false) : v.total < 17 ? "hit" : "stand";
                if (mv == "stand") return;
                r.Cards.Add(Deal(g));
                if (mv == "double") { r.Bet *= 2; return; }
            }
        }

        /// <summary>What the dealer hands a regular back, by the table's rules (no marks, no charms: theirs).</summary>
        static void PayRegular(RegularHand21 r, List<Card> dealer, bool dn)
        {
            int pv = HandValue(r.Cards).total, dv = HandValue(dealer).total;
            bool rn = IsNatural(r.Cards);
            double x; string word;
            if (rn && dn) { x = 1; word = "PUSH"; }
            else if (rn) { x = 2.5; word = "NATURAL"; }
            else if (dn) { x = 0; word = "LOSES"; }
            else if (pv > 21) { x = 0; word = $"BUSTS {pv}"; }
            else if (dv > 21 || pv > dv) { x = 2; word = $"WINS {pv}"; }
            else if (pv == dv) { x = 1; word = $"PUSH {pv}"; }
            else { x = 0; word = $"LOSES {pv}"; }
            r.Ret = Js.RoundInt(r.Bet * x);
            r.Word = word;
        }

        Outcome Settle(Game g, PlayerState p)
        {
            var hand = g.State.Tables["twentyone"].Hand;
            double factor = Rules.PayoutFactor(g, p, Id);
            bool one = hand.Hands.Count == 1;
            bool pn = one && IsNatural(hand.Hands[0].Cards), dn = IsNatural(hand.Dealer);
            var words = new List<string>();
            var res = new List<HandResult>();
            var regs = hand.Regulars ?? new List<RegularHand21>();
            // third base plays (and first base, if your natural came before its turn); a dealer natural ends it
            if (!dn) foreach (var r in regs) PlayRegular(g, hand, r);
            // the dealer plays while any hand at the table still stands (not all bust, not all naturals)
            bool youStand = !pn && hand.Hands.Any(h => HandValue(h.Cards).total <= 21);
            bool theyStand = regs.Any(r => HandValue(r.Cards).total <= 21 && !IsNatural(r.Cards));
            if (!dn && (youStand || theyStand)) DealerPlays(g, hand.Dealer);
            foreach (var r in regs) PayRegular(r, hand.Dealer, dn);
            int dv = HandValue(hand.Dealer).total;
            foreach (var h in hand.Hands)
            {
                int pv = HandValue(h.Cards).total;
                double x = 0; string word;
                if (pn && dn) { x = 1; word = "BOTH NATURAL: PUSH"; }
                else if (pn) { x = p.Charms.Contains("counter") ? 3 : 2.5; word = "NATURAL"; }
                else if (dn) word = "DEALER NATURAL";
                else if (pv > 21) word = $"BUST {pv}";
                else if (dv > 21) { x = 2; word = one ? $"DEALER BUSTS {dv}" : $"WIN {pv}"; }
                else if (pv > dv) { x = 2; word = one ? $"{pv} BEATS {dv}" : $"WIN {pv}"; }
                else if (pv == dv) { x = 1; word = $"PUSH {pv}"; }
                else word = one ? $"{dv} BEATS {pv}" : $"LOSE {pv}";
                // a push hands the stake back untouched; a win is a payout (marks and Cold Deck apply)
                int ret = x == 1 ? h.Bet : Js.RoundInt(h.Bet * x * factor);
                words.Add(word);
                res.Add(new HandResult { Cards = h.Cards.Select(c => c.Clone()).ToList(), Bet = h.Bet, Ret = ret });
            }
            // the side bet was judged on the deal; it lands with the hand
            int bet = res.Sum(r => r.Bet) + hand.Side, retT = res.Sum(r => r.Ret) + hand.SideRet;
            string text = string.Join(" / ", words);
            if (hand.Side > 0) text += hand.SideRet > 0 ? $" + {hand.SideWord}" : " (NO PAIR)";
            if (retT > bet) text += $": ${retT}";
            hand.Stage = "done";
            return new Outcome
            {
                Table = Id, Player = p.Id, Bet = bet, Ret = retT, Win = retT > bet, Factor = factor, Text = text,
                Hands21 = res, Dealer21 = hand.Dealer.Select(c => c.Clone()).ToList(),
                Regulars21 = regs.Count > 0 ? regs.Select(r => r.Clone()).ToList() : null,
            };
        }

        /// <summary>The active hand is finished: move to the next (it gets its second card), or settle.</summary>
        Outcome Next(Game g, PlayerState p)
        {
            var hand = g.State.Tables["twentyone"].Hand;
            hand.Hands[hand.Active].Done = true;
            while (hand.Active < hand.Hands.Count - 1)
            {
                hand.Active++;
                var h = hand.Hands[hand.Active];
                if (h.Cards.Count < 2) h.Cards.Add(Deal(g));
                if (h.Aces || HandValue(h.Cards).total >= 21) h.Done = true;
                if (!h.Done) return null;
            }
            return Settle(g, p);
        }

        /// <summary>Did the whole table win on a dealer bust (you, every hand of yours, and every regular)?</summary>
        public static bool TableWins(Outcome o) =>
            o.Dealer21 != null && HandValue(o.Dealer21).total > 21 && o.Hands21 != null && o.Hands21.All(h => h.Ret > h.Bet)
            && o.Regulars21 != null && o.Regulars21.Count > 0 && o.Regulars21.All(r => r.Ret > r.Bet);

        /// <summary>Good Company (U31): a marker when the whole table wins on a dealer bust, 3 a night.</summary>
        public override void Resolve(Game g, PlayerState p, Outcome o)
        {
            if (p.Charms.Contains("company") && p.Company < 3 && TableWins(o))
            {
                p.Company++;
                Rules.AddMarkers(g, 1, "good company");
            }
        }

        public override string Id => "twentyone";
        public override string[] Cmds => new[] { "bet", "call" };

        // the chance of coming out ahead is measured (tests); the RTP is exact for basic strategy
        public override Odds Odds() => new Odds { Rtp = ExactRtp(), WinP = Def.WinP };

        /// <summary>The result waits for the cards still to come down at Feel.DealGap each: a whole deal (a
        /// natural), or the last hit, the hole card and the dealer's draws (presentation pacing).</summary>
        public override double? HoldS(Outcome o)
        {
            int dealer = o.Dealer21?.Count ?? 2;
            var hands = o.Hands21 ?? new List<HandResult>();
            var regs = o.Regulars21 ?? new List<RegularHand21>();
            int mine = hands.Sum(h => h.Cards.Count);
            // settled on the deal (a natural either side): every card of it; else your last card, third base's
            // draws, the hole card and the dealer's draws
            bool natural = mine == 2 && hands.Count == 1 && (IsNatural(hands[0].Cards) || (o.Dealer21 != null && IsNatural(o.Dealer21)));
            int cards = natural ? 2 * (2 + regs.Count) + regs.Sum(r => r.Cards.Count - 2) + Math.Max(0, dealer - 2) + 1
                : 1 + regs.Where(r => Regulars.ById(r.Who)?.Seat == 1).Sum(r => r.Cards.Count - 2) + 1 + Math.Max(0, dealer - 2);
            return cards * Feel.DealGap + 0.5;
        }

        public override StartResult Start(Game g, PlayerState p, Command cmd)
        {
            var t = g.State.Tables["twentyone"];
            if (cmd.T == "bet")
            {
                if (t.Hand != null && t.Hand.Stage == "play") return StartResult.Refuse("finish the hand");
                if (!BetOf(g, Def, cmd.Amount, out int b, out string why)) return StartResult.Refuse(why);
                int amount = Rules.StakeFor(p, b);
                // PAIRS rides beside the hand at the table minimum (U30)
                var pairs = SideBets.PairsFor(p);
                int side = cmd.Call == pairs.Id ? pairs.Mins * Data.Tables.LimitsOf(Def, g.State.Floor).Min : 0;
                if (side > 0 && g.State.Bank < amount + side) return StartResult.Refuse("the bank can't cover the side bet too");
                var no = Stake(g, amount);
                if (no != null) return StartResult.Refuse(no);
                if (side > 0) Stake(g, side);
                if (t.Shoe == null || t.Shoe.Count < ReshuffleAt)
                {
                    t.Shoe = Shuffle(g);
                    g.Emit(new SimEvent { T = "shuffle", Table = "twentyone" });
                }
                // round the table twice: first base, you, third base, the dealer (the second face down)
                int min = Data.Tables.LimitsOf(Def, g.State.Floor).Min;
                var regs = Regulars.All.OrderBy(d => d.Seat).Select(d => new RegularHand21 { Who = d.Id, Cards = new List<Card>(), Bet = d.Mins * min }).ToList();
                var mine = new List<Card>();
                var dealer = new List<Card>();
                for (int k = 0; k < 2; k++)
                {
                    foreach (var r in regs) if (Regulars.ById(r.Who).Seat == 0) r.Cards.Add(Deal(g));
                    mine.Add(Deal(g));
                    foreach (var r in regs) if (Regulars.ById(r.Who).Seat == 1) r.Cards.Add(Deal(g));
                    dealer.Add(Deal(g));
                }
                t.Hand = new Hand21
                {
                    Bet = amount, Active = 0, Stage = "play",
                    Hands = new List<PlayerHand21> { new PlayerHand21 { Cards = mine, Bet = amount } },
                    Dealer = dealer,
                    Regulars = regs,
                };
                if (side > 0)
                {
                    var (word, pays) = SideBets.Judge(pairs, mine[0], mine[1]);
                    t.Hand.Side = side;
                    t.Hand.SideWord = word;
                    t.Hand.SideRet = pays > 0 ? Js.RoundInt(side * (pays + 1) * Rules.PayoutFactor(g, p, Id)) : 0;
                }
                if (IsNatural(dealer)) return StartResult.Of(Settle(g, p));
                // first base plays before you
                foreach (var r in regs) if (Regulars.ById(r.Who).Seat == 0) PlayRegular(g, t.Hand, r);
                if (IsNatural(mine)) return StartResult.Of(Settle(g, p));
                return StartResult.Pending;
            }
            if (cmd.T == "call")
            {
                var hand = t.Hand;
                if (hand == null || hand.Stage != "play") return StartResult.Refuse("place a bet first");
                var h = hand.Hands[hand.Active];
                var can = Options(hand);
                switch (cmd.Call)
                {
                    case "hit":
                        h.Cards.Add(Deal(g));
                        return HandValue(h.Cards).total >= 21 ? StartResult.Of(Next(g, p)) : StartResult.Pending;
                    case "stand":
                        return StartResult.Of(Next(g, p));
                    case "double":
                    {
                        if (!can.dbl) return StartResult.Refuse("double on your first two cards");
                        var why = Stake(g, h.Bet);
                        if (why != null) return StartResult.Refuse(why);
                        h.Bet *= 2;
                        h.Cards.Add(Deal(g));
                        return StartResult.Of(Next(g, p));
                    }
                    case "split":
                    {
                        if (!can.split) return StartResult.Refuse("split a pair, once");
                        var why = Stake(g, h.Bet);
                        if (why != null) return StartResult.Refuse(why);
                        bool aces = h.Cards[0].R == 1;
                        var last = h.Cards[h.Cards.Count - 1];
                        h.Cards.RemoveAt(h.Cards.Count - 1);
                        var second = new PlayerHand21 { Cards = new List<Card> { last }, Bet = h.Bet, Done = false, Aces = aces };
                        h.Aces = aces;
                        h.Cards.Add(Deal(g));
                        hand.Hands.Add(second);
                        // split aces take one card each and stand
                        if (aces || HandValue(h.Cards).total >= 21) return StartResult.Of(Next(g, p));
                        return StartResult.Pending;
                    }
                    case "leave":
                        // walking away (or 0:00) stands every hand still in play
                        while (hand.Stage == "play")
                        {
                            var o = Next(g, p);
                            if (o != null) return StartResult.Of(o);
                        }
                        return StartResult.Pending;
                }
                return StartResult.Refuse("hit, stand, double or split");
            }
            return StartResult.Refuse("bet, then play the hand");
        }
    }
}
