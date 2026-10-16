// Your bet as real chips on the felt (IDEAS.md 16): a stack in casino colors built from your bet while you
// sit, staked when the round goes up; when it lands a win's payout stack drops in beside it, a loss is swept
// away toward the dealer. Presentation of the sim's state only.
using System.Collections.Generic;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public sealed class BetChips
    {
        static readonly int[] Denoms = { 5000, 1000, 500, 100, 50, 25 };
        static readonly Color[] Colors = { new Color(0.9f, 0.5f, 0.1f), new Color(0.45f, 0.15f, 0.6f), new Color(0.15f, 0.15f, 0.17f), new Color(0.12f, 0.25f, 0.65f), new Color(0.75f, 0.1f, 0.12f), new Color(0.1f, 0.5f, 0.2f) };
        static Mesh chip;
        readonly string id;
        readonly Transform root, stake, pay;
        readonly Vector3 stakeAt, payAt;
        int shownStake = -1, shownPay = -1;
        int landedRound = -1;
        float landedAt;

        public BetChips(string id, Transform table, Placement pl)
        {
            this.id = id;
            root = table;
            float felt = (float)pl.H + 0.012f;
            stakeAt = new Vector3(-0.42f, felt, -(float)pl.D / 2 + 0.42f);
            payAt = stakeAt + new Vector3(0.09f, 0, 0);
            stake = Props.Group("bet chips", root, stakeAt);
            pay = Props.Group("payout chips", root, payAt);
            chip = chip ? chip : MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.0195f, 0), new Vector2(0.02f, 0.001f), new Vector2(0.02f, 0.0034f), new Vector2(0.0195f, 0.0044f), new Vector2(0, 0.0044f) }, 24, true, false);
        }

        static void Build(Transform t, int amount)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Ui.Kill(t.GetChild(i).gameObject);
            int n = 0;
            for (int d = 0; d < Denoms.Length && n < 24; d++)
                while (amount >= Denoms[d] && n < 24)
                {
                    amount -= Denoms[d];
                    var go = Props.Part("chip", chip, Look.Surface("betChip" + d, Colors[d], 0.65f), t, new Vector3(((n * 37) % 5 - 2) * 0.0005f, n * 0.0045f, ((n * 13) % 5 - 2) * 0.0005f), Quaternion.Euler(0, n * 47, 0));
                    n++;
                }
        }

        /// <summary>Pose the stacks: `bet` is what the bar has built (shown while you sit with nothing staked).</summary>
        public void Update(Game g, PlayerState me, int bet, float nowS)
        {
            var t = g.State.Tables[id];
            bool seated = me.Seat == id;
            int stakeAmt = 0, payAmt = 0;
            var o = t.Last;
            if (t.Pending != null) stakeAmt = t.Pending.Bet;
            else if (id == "craps" && t.Point != 0) stakeAmt = t.PointBet;
            else if (id == "crash" && t.Crash != null) stakeAmt = t.Crash.Bet;
            else if (o != null && t.Rounds != landedRound)
            {
                landedRound = t.Rounds;
                landedAt = nowS;
            }
            float since = nowS - landedAt;
            bool showingResult = t.Pending == null && o != null && since < 2.6f && t.Rounds == landedRound && (id != "craps" || t.Point == 0) && (id != "crash" || t.Crash == null);
            if (showingResult)
            {
                stakeAmt = o.Bet;
                payAmt = Mathf.Max(0, o.Ret - o.Bet);
            }
            else if (stakeAmt == 0 && seated) stakeAmt = Rules.StakeFor(me, bet);
            if (stakeAmt != shownStake) { Build(stake, stakeAmt); shownStake = stakeAmt; }
            if (payAmt != shownPay) { Build(pay, payAmt); shownPay = payAmt; }
            // a loss is raked toward the dealer; a win's payout drops onto the felt
            bool lost = showingResult && o.Ret < o.Bet;
            float rake = lost ? Mathf.SmoothStep(0, 1, (since - 0.5f) / 0.6f) : 0;
            stake.localPosition = stakeAt + new Vector3(0, 0, rake * 0.5f);
            stake.gameObject.SetActive(stakeAmt > 0 && rake < 0.99f);
            float drop = showingResult && payAmt > 0 ? Mathf.Clamp01(since / 0.35f) : 1;
            pay.localPosition = payAt + new Vector3(0, (1 - drop * drop) * 0.15f, 0);
            pay.gameObject.SetActive(payAmt > 0);
        }
    }
}
