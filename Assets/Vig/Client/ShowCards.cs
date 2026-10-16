// The card tables. Red Line: the deck riffled, one card dealt face down and turned over. Twenty-One: a
// card out of the shoe every Feel.DealGap (you, the dealer, you, the dealer face down; a hit slides in;
// a split parts the pair), and at the stand the hole card turns before the dealer draws. The cards are
// the sim's: the hand in play, or the outcome in the air or landed.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public sealed class RedLineShow : TableShow
    {
        readonly Transform deck, halfA, halfB;
        readonly Props.CardProp flick, card;
        readonly Transform chipsRed, chipsBlack;
        readonly Vector3 deckAt, spot;
        const float HalfH = 0.01f;

        public RedLineShow(Transform root) : base("redline", root)
        {
            deckAt = new Vector3(-0.3f, FeltY, 0.2f);
            spot = new Vector3(0.06f, FeltY + Props.CardT / 2, 0.09f);
            deck = Props.Group("deck", root, deckAt);
            halfA = Half("half a");
            halfB = Half("half b");
            flick = new Props.CardProp(deck, "riffled card");
            card = new Props.CardProp(root, "dealt card");
            // where the call goes down: RED and BLACK printed on the felt, the chips on the one you called
            var flat = Quaternion.Euler(90, 0, 0);
            Props.Print(root, "RED", new Color(0.85f, 0.12f, 0.18f), 0.026f, new Vector3(-0.13f, FeltY + 0.0005f, -0.08f), flat);
            Props.Print(root, "BLACK", new Color(0.04f, 0.035f, 0.04f), 0.026f, new Vector3(0.15f, FeltY + 0.0005f, -0.08f), flat);
            chipsRed = Props.Chips(root, "chips red", new Vector3(-0.13f, FeltY, -0.035f), 4);
            chipsBlack = Props.Chips(root, "chips black", new Vector3(0.15f, FeltY, -0.035f), 4);
            Party = new Celebration(root, spot + Vector3.up * 0.02f, FeltY, new Vector3(0, FeltY + 0.05f, -0.07f), Quaternion.Euler(38, 0, 0));
        }

        Transform Half(string name)
        {
            var g = Props.Group(name, deck, Vector3.zero);
            Props.Part("stock", Props.Cached("deckHalf", () => MeshKit.RoundedBox(Props.CardW, HalfH, Props.CardL, 0.001f, 2)), Props.CardStock, g, new Vector3(0, HalfH / 2, 0));
            Props.Part("back", Props.Cached("cardBackQuad", () => MeshKit.Quad(Props.CardW - 0.004f, Props.CardL - 0.004f)), Props.CardBack, g, new Vector3(0, HalfH + 0.0004f, 0), Quaternion.Euler(90, 0, 0), false);
            return g;
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            // the deck: squared, or riffling while it shuffles (the halves part, bend, fall together)
            bool shuffling = live && p >= Beats.ShuffleA && p < Beats.ShuffleB;
            if (shuffling)
            {
                float q = (p - Beats.ShuffleA) / (Beats.ShuffleB - Beats.ShuffleA);
                float gap = Mathf.Sin(q * Mathf.PI) * 0.055f;
                halfA.localPosition = new Vector3(-gap, 0, 0);
                halfB.localPosition = new Vector3(gap, 0, 0);
                halfA.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(q * Mathf.PI) * 14);
                halfB.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(q * Mathf.PI) * 14);
                // a card from each side in turn, falling into the middle
                int n = Mathf.FloorToInt(q * 18);
                float f = q * 18 - n;
                float side = n % 2 == 0 ? -1 : 1;
                flick.Visible(true);
                flick.T.localPosition = new Vector3(side * gap * (1 - f), HalfH * 2 * (1 - f) + 0.004f, 0);
                flick.T.localRotation = Quaternion.Euler(0, 0, 180 + side * 30 * (1 - f));
            }
            else
            {
                halfA.localPosition = Vector3.zero;
                halfB.localPosition = new Vector3(0, HalfH, 0);
                halfA.localRotation = halfB.localRotation = Quaternion.identity;
                flick.Visible(false);
            }
            // the card: dealt off the deck face down, then turned over; it stays face up until the next deal
            if (live)
            {
                if (p < Beats.DealA) card.Visible(false);
                else
                {
                    card.Visible(true);
                    if (o?.Card != null) card.Show(o.Card);
                    var top = deckAt + Vector3.up * (HalfH * 2 + Props.CardT / 2);
                    if (p < Beats.FlipA)
                    {
                        float k = Beats.Out3((p - Beats.DealA) / (Beats.DealB - Beats.DealA));
                        card.T.localPosition = Vector3.Lerp(top, spot, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.02f;
                        card.T.localRotation = Quaternion.Euler(0, (1 - k) * 25, 180);
                    }
                    else
                    {
                        // turned over along its long side, lifted clear of the felt as it stands on edge
                        float u = Mathf.Clamp01((p - Beats.FlipA) / (Beats.FlipB - Beats.FlipA));
                        card.T.localPosition = spot + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.045f;
                        card.T.localRotation = Quaternion.Euler(0, 0, 180 * (1 - u));
                    }
                }
            }
            else if (o?.Card != null)
            {
                card.Visible(true);
                card.Show(o.Card);
                card.T.localPosition = spot;
                card.T.localRotation = Quaternion.identity;
            }
            else card.Visible(false);
            string call = o?.Call;
            chipsRed.gameObject.SetActive(call == "red");
            chipsBlack.gameObject.SetActive(call == "black");
        }
    }

    public sealed class TwentyOneShow : TableShow
    {
        const int Max = 10;
        readonly Props.CardProp[] dealerCards = new Props.CardProp[Max];
        readonly Props.CardProp[][] handCards = { new Props.CardProp[Max], new Props.CardProp[Max] };
        readonly Transform[][] stacks = { new Transform[2], new Transform[2] };
        readonly Vector3 mouth;

        /// <summary>cards on the cloth so far, per hand and the dealer's: the rest are still in the shoe,
        /// coming out one every Feel.DealGap (the screen prints only these)</summary>
        public readonly List<int> SeenH = new List<int>();
        public int SeenD;
        /// <summary>the dealer's hole card has turned over</summary>
        public bool HoleUp;
        float lastAt = -1e9f;
        bool lastWasFlip, live, started;
        /// <summary>the card that came out last: its row (-1 the dealer's, else the hand) and place</summary>
        int lastRow = -2, lastIdx = -1;

        public TwentyOneShow(Transform root) : base("twentyone", root)
        {
            // the shoe on the dealer's left (your right), its mouth toward the felt
            var shoe = Props.Group("shoe", root, new Vector3(0.8f, FeltY, 0.29f), Quaternion.Euler(0, -18, 0));
            Props.Part("body", MeshKit.RoundedBox(0.12f, 0.08f, 0.2f, 0.012f, 3), Look.BlackGloss, shoe, new Vector3(0, 0.04f, 0));
            Props.Part("lip", MeshKit.RoundedBox(0.1f, 0.012f, 0.04f, 0.004f, 2), Look.BrassMat, shoe, new Vector3(0, 0.03f, -0.105f));
            var next = new Props.CardProp(shoe, "next card");
            next.T.localPosition = new Vector3(0, 0.05f, -0.03f);
            next.T.localRotation = Quaternion.Euler(-20, 0, 180);
            mouth = root.InverseTransformPoint(shoe.TransformPoint(new Vector3(0, 0.035f, -0.12f)));
            for (int i = 0; i < Max; i++)
            {
                dealerCards[i] = new Props.CardProp(root, "dealer card " + (i + 1));
                for (int h = 0; h < 2; h++) handCards[h][i] = new Props.CardProp(root, $"hand {h + 1} card {i + 1}");
            }
            for (int h = 0; h < 2; h++)
            {
                stacks[h][0] = Props.Chips(root, "stake", Vector3.zero, 4);
                stacks[h][1] = Props.Chips(root, "double", Vector3.zero, 4);
            }
            Party = new Celebration(root, new Vector3(0, FeltY + 0.03f, 0.12f), FeltY, new Vector3(0, FeltY + 0.05f, -0.1f), Quaternion.Euler(38, 0, 0));
        }

        public override void Reset()
        {
            base.Reset();
            started = false;
            live = false;
        }

        sealed class Target
        {
            public List<List<Card>> Hands;
            public List<Card> Dealer;
            public bool Hole;
            public int Active;
            public List<int> Bets;
        }

        /// <summary>The cards heading for the cloth: the round in play (hole down), or the outcome.</summary>
        static Target TargetOf(TableState t, Outcome o)
        {
            var hand = t.Hand;
            if (hand != null && hand.Stage == "play")
                return new Target { Hands = hand.Hands.Select(h => h.Cards).ToList(), Dealer = hand.Dealer, Hole = true, Active = hand.Active, Bets = hand.Hands.Select(h => h.Bet).ToList() };
            if (o?.Hands21 != null)
                return new Target { Hands = o.Hands21.Select(h => h.Cards).ToList(), Dealer = o.Dealer21, Hole = false, Active = -1, Bets = o.Hands21.Select(h => h.Bet).ToList() };
            return null;
        }

        int Seen(int i) => i < SeenH.Count ? SeenH[i] : 0;

        /// <summary>How many of a hand's cards (or the dealer's) are down on the cloth yet.</summary>
        public int Shown(int hand, int all) => started ? Mathf.Min(all, Seen(hand)) : all;
        public int ShownDealer(int all) => started ? Mathf.Min(all, SeenD) : all;

        /// <summary>Still bringing the cloth up to the round.</summary>
        public bool Dealing(TableState t)
        {
            var tg = TargetOf(t, t.Pending ?? t.Last);
            return tg != null && (tg.Hands.Select((h, i) => Seen(i) < h.Count).Any(x => x) || SeenD < tg.Dealer.Count || !tg.Hole && !HoleUp);
        }

        /// <summary>One card at a time, the TS game's order: the deal goes you, dealer, you, dealer (face
        /// down); a hit or a double slides in; a split parts the pair; at the stand the hole card turns
        /// over, then the dealer draws.</summary>
        void Step(TableState t, Target tg, float now)
        {
            bool isLive = t.Pending != null || t.Hand?.Stage == "play";
            float gap = (float)Feel.DealGap;
            if (!started || tg == null)
            {
                // opening on a round already dealt: show it as it is
                started = true;
                SeenH.Clear();
                if (tg != null) SeenH.AddRange(tg.Hands.Select(h => h.Count));
                SeenD = tg?.Dealer.Count ?? 0;
                HoleUp = tg != null && !tg.Hole;
            }
            else if (isLive && !live)
            {
                // a new round: the cloth is cleared
                SeenH.Clear();
                SeenH.Add(0);
                SeenD = 0;
                HoleUp = false;
                lastAt = now - gap * 0.6f;
            }
            live = isLive;
            if (tg == null) return;
            if (SeenH.Count < tg.Hands.Count)
            {
                // the split: one card stays, the other starts the second hand
                int first = Mathf.Min(Seen(0), 1);
                SeenH.Clear();
                for (int i = 0; i < tg.Hands.Count; i++) SeenH.Add(i == 0 ? first : 1);
            }
            if (now - lastAt < gap) return;
            int h0 = Seen(0);
            bool opening = tg.Hands.Count == 1 && (h0 < 2 || SeenD < 2);
            int more = -1;
            for (int i = 0; i < tg.Hands.Count; i++) if (Seen(i) < tg.Hands[i].Count) { more = i; break; }
            if (opening && h0 <= SeenD && h0 < tg.Hands[0].Count) { SeenH[0] = h0 + 1; lastRow = 0; lastIdx = h0; }
            else if (opening && SeenD < tg.Dealer.Count) { lastRow = -1; lastIdx = SeenD; SeenD++; }
            else if (more >= 0) { lastRow = more; lastIdx = Seen(more); SeenH[more] = lastIdx + 1; }
            else if (!tg.Hole && !HoleUp && SeenD >= 2)
            {
                HoleUp = true;
                lastAt = now;
                lastWasFlip = true;
                return;
            }
            else if (SeenD < tg.Dealer.Count) { lastRow = -1; lastIdx = SeenD; SeenD++; }
            else return;
            lastAt = now;
            lastWasFlip = false;
        }

        Vector3 Slot(bool dealer, int hand, int i, int hands)
        {
            float y = FeltY + Props.CardT / 2 + i * 0.0005f;
            if (dealer) return new Vector3(-0.25f + i * 0.105f, y, 0.235f);
            if (hands < 2) return new Vector3(-0.15f + i * 0.105f, y, 0.025f);
            return new Vector3((hand == 0 ? -0.5f : 0.06f) + i * 0.08f, y, 0.025f);
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool isLive, float p, float since, float nowS)
        {
            var tg = TargetOf(t, o);
            Step(t, tg, nowS);
            float gap = (float)Feel.DealGap;
            float ago = nowS - lastAt;
            int hands = tg?.Hands.Count ?? 0;
            for (int i = 0; i < Max; i++)
            {
                Pose(dealerCards[i], tg?.Dealer, i, SeenD, true, 0, hands, ago, gap);
                for (int h = 0; h < 2; h++) Pose(handCards[h][i], h < hands ? tg.Hands[h] : null, i, Seen(h), false, h, hands, ago, gap);
            }
            // the stakes in front of each hand, a second stack when it doubled
            int baseBet = t.Hand?.Bet ?? (tg != null && tg.Bets.Count > 0 ? tg.Bets.Min() : 0);
            for (int h = 0; h < 2; h++)
            {
                bool on = tg != null && h < hands && Seen(h) > 0;
                var at = Slot(false, h, 0, hands);
                stacks[h][0].gameObject.SetActive(on);
                stacks[h][1].gameObject.SetActive(on && tg.Bets[h] > baseBet);
                stacks[h][0].localPosition = new Vector3(at.x + 0.05f, FeltY, -0.1f);
                stacks[h][1].localPosition = new Vector3(at.x + 0.098f, FeltY, -0.1f);
            }
        }

        void Pose(Props.CardProp c, List<Card> cards, int i, int shown, bool dealer, int hand, int hands, float ago, float gap)
        {
            if (cards == null || i >= shown || i >= cards.Count)
            {
                c.Visible(false);
                return;
            }
            c.Visible(true);
            c.Show(cards[i]);
            var at = Slot(dealer, hand, i, hands);
            bool hole = dealer && i == 1 && !HoleUp;
            bool newest = (dealer ? lastRow == -1 : lastRow == hand) && i == lastIdx && ago < gap && !lastWasFlip;
            if (dealer && i == 1 && HoleUp && lastWasFlip && ago < gap && shown == 2)
            {
                // the hole card turns over along its long side
                float k = Mathf.Clamp01(ago / (gap * 0.8f));
                c.T.localPosition = at + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.045f;
                c.T.localRotation = Quaternion.Euler(0, 0, 180 * (1 - k));
                return;
            }
            if (newest)
            {
                // out of the shoe face down, across the felt, turning face up as it lands (the hole card stays down)
                float k = Beats.Out3(ago / (gap * 0.7f));
                c.T.localPosition = Vector3.Lerp(mouth, at, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.03f;
                float roll = hole || k < 0.8f ? 180 : 180 * (1 - (k - 0.8f) / 0.2f);
                c.T.localRotation = Quaternion.Euler(0, (1 - k) * -30, roll);
                return;
            }
            c.T.localPosition = at;
            c.T.localRotation = Quaternion.Euler(0, 0, hole ? 180 : 0);
        }
    }
}
