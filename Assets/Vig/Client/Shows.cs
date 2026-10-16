// The tables' rounds in 3D (DECISIONS U11), as the TS game's panels animate them (src/panels/games.ts,
// fx.ts): the die thrown onto Hi-Lo's baize, Dead Man's two dice, Red Line's shuffle, deal and flip,
// Twenty-One dealt card by card, the Wheel of Marks turning onto its slot, the Bandit's reels stopping
// in turn, and a celebration that grows with the win. Every face comes from the sim's Outcome (the round
// in the air is the table's Pending; the last that landed is its Last); every frame is a function of
// the sim's tick, so presentation never decides a result and a screenshot can stop one mid-flight.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Client
{
    /// <summary>Where each round's beats fall, as fractions of the result's hold: the TS game's FEEL
    /// (src/data/feel.ts). The totals are the sim's (Feel, TableDef.ResolveS, TwentyOne.HoldS); these are
    /// presentation pacing, not design numbers.</summary>
    public static class Beats
    {
        /// <summary>Hi-Lo's die: shaken, thrown, five bounces, settles</summary>
        public const float DiceShake = 0.16f, DiceLand = 0.9f;
        public static readonly float[] DiceBounces = { 0.3f, 0.5f, 0.65f, 0.76f, 0.84f };
        /// <summary>Dead Man's two dice: the second lands a beat after the first</summary>
        public const float DeadShake = 0.14f, DeadLandA = 0.62f, DeadLandB = 0.9f;
        /// <summary>Red Line: a riffle shuffle, the deal, the flip</summary>
        public const float ShuffleA = 0.02f, ShuffleB = 0.42f, DealA = 0.47f, DealB = 0.62f, FlipA = 0.7f, FlipB = 0.8f;
        /// <summary>the Wheel of Marks: whole turns before it slows onto the slot</summary>
        public const float WheelTurns = 3;
        /// <summary>the Bandit: each reel stops in turn</summary>
        public static readonly float[] BanditStops = { 0.45f, 0.66f, 0.86f };
        /// <summary>a won round's word stays up this long (s); the rest of the party runs Feel.CelebrateS</summary>
        public const float WordS = 1.5f;

        public static float Out3(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }
    }

    public abstract class TableShow
    {
        public readonly string Id;
        protected readonly Transform Root;
        protected readonly Placement Pl;
        /// <summary>the felt's top in the table's frame (+Z away from the seat, the seat at -Z)</summary>
        protected readonly float FeltY;
        protected Celebration Party;
        /// <summary>the round on show: its number (the same in the air and after it lands), and a salt from
        /// it so no two throws look the same</summary>
        protected int Round, Salt;
        Outcome rolling;
        int hold, nextHold;
        int landTick = -100000;

        protected TableShow(string id, Transform root)
        {
            Id = id;
            Root = root;
            Pl = Layout.Floor1.TableOf(id);
            FeltY = (float)Pl.H + 0.01f;
        }

        public static Dictionary<string, TableShow> Build(RoomBuilder room)
        {
            var d = new Dictionary<string, TableShow>();
            foreach (var kv in room.Tables)
            {
                TableShow s;
                switch (kv.Key)
                {
                    case "hilo": case "deadman": s = new DiceShow(kv.Key, kv.Value); break;
                    case "redline": s = new RedLineShow(kv.Value); break;
                    case "twentyone": s = new TwentyOneShow(kv.Value); break;
                    case "wheel": s = new WheelShow(kv.Value); break;
                    case "bandit": s = new BanditShow(kv.Value); break;
                    default: continue;
                }
                d[kv.Key] = s;
            }
            return d;
        }

        /// <summary>Where the seated eye looks, if not at the screen's center (Unity space): the table's
        /// show beside or above the screen in frame with it.</summary>
        public virtual Vector3 LookShift => Vector3.zero;

        /// <summary>The 'play' event: how many ticks the sim holds this result.</summary>
        public void OnPlay(int ticks) => nextHold = ticks;

        /// <summary>The 'round' event: the result landed on this tick.</summary>
        public void OnLanded(Outcome o, int tick)
        {
            landTick = tick;
            Party?.Begin(o);
        }

        public virtual void Reset()
        {
            rolling = null;
            landTick = -100000;
            Party?.Begin(null);
        }

        /// <summary>Pose everything for this moment: `nowTicks` is the sim's tick count plus the fraction of
        /// the next (the render's interpolation).</summary>
        public void Update(Game g, float nowTicks)
        {
            var t = g.State.Tables[Id];
            if (t.Pending != null && t.Pending != rolling)
            {
                rolling = t.Pending;
                var mod = TableModule.ById(Id);
                hold = nextHold > 0 ? nextHold : Rules.Ticks(mod.HoldS(rolling) ?? mod.Def.ResolveS);
                nextHold = 0;
            }
            if (t.Pending == null) rolling = null;
            Round = Mathf.Max(0, rolling != null ? t.Rounds : t.Rounds - 1);
            Salt = (Round * 7) % 6;
            // the play event comes from the step that ends on tick Until - hold + 1; the result lands on Until
            float p = rolling != null ? Mathf.Clamp01((nowTicks - (t.Until - hold + 1)) / hold) : 0;
            float since = (nowTicks - landTick) / State.TicksPerS;
            Animate(g, t, rolling ?? t.Last, rolling != null, p, since, nowTicks / State.TicksPerS);
            Party?.Update(since);
        }

        /// <param name="o">the round in the air, or the last that landed (null: none yet)</param>
        /// <param name="live">o is still in the air</param>
        /// <param name="p">0..1 through the hold (0 when idle)</param>
        /// <param name="since">seconds since the last result landed</param>
        /// <param name="nowS">the sim's time (s)</param>
        protected abstract void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS);
    }

    // ------------------------------------------------------------------ the celebration

    /// <summary>A landed win: sparks burst from the play, a gold light flares, the word and the amount
    /// float up; bigger for a big win (Feel.BigWin), coins and a strobe for a jackpot (Feel.Jackpot or the
    /// Bandit's grid). A life lost flares blood red. Posed from the seconds since the landing.</summary>
    public sealed class Celebration
    {
        readonly Transform space;
        readonly Vector3 origin, textPos;
        readonly float floorY;
        readonly Transform[] sparks = new Transform[48];
        readonly Transform[] coins = new Transform[28];
        readonly Light flash;
        readonly TextMeshPro word;
        readonly Transform wordT;
        readonly Quaternion textRot;
        int kind;

        const int None = 0, Win = 1, Big = 2, Jackpot = 3, Life = -1;

        /// <param name="space">an upright frame (gravity is its -Y)</param>
        public Celebration(Transform space, Vector3 origin, float floorY, Vector3 textPos, Quaternion textRot)
        {
            this.space = space;
            this.origin = origin;
            this.floorY = floorY;
            this.textPos = textPos;
            this.textRot = textRot;
            var g = Props.Group("celebration", space, Vector3.zero);
            Material[] mats =
            {
                Look.Glow("sparkWhite", new Color(1f, 0.95f, 0.85f), 9f), Look.Glow("sparkGold", Look.Gold, 8f), Look.Glow("sparkBrass", new Color(1f, 0.7f, 0.3f), 7f),
            };
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = Props.Part("spark", Props.Bulb(0.0055f), mats[i % 3], g, origin, null, false).transform;
                sparks[i].gameObject.SetActive(false);
            }
            var coin = Props.Cached("coin", () => MeshKit.Lathe(new[] { new Vector2(0.016f, -0.0015f), new Vector2(0.017f, 0), new Vector2(0.016f, 0.0015f) }, 18));
            for (int i = 0; i < coins.Length; i++)
            {
                coins[i] = Props.Part("coin", coin, Look.BrassMat, g, origin, null, false).transform;
                coins[i].gameObject.SetActive(false);
            }
            var lg = new GameObject("flash");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = origin + Vector3.up * 0.3f;
            flash = lg.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 2.2f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;
            word = Neon.Text(g, "", Look.Gold, textPos, 0.05f, 0, 1.5f);
            wordT = word.transform;
            wordT.localRotation = textRot;
            word.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f);
            word.characterSpacing = 4;
            word.gameObject.SetActive(false);
        }

        public void Begin(Outcome o)
        {
            kind = None;
            if (o == null) return;
            int net = o.Ret - o.Bet;
            if (o.LifeLost == true) kind = Life;
            else if (net > 0) kind = net >= Feel.Jackpot || o.Jackpot == true ? Jackpot : net >= Feel.BigWin ? Big : Win;
            string amount = "+" + Ui.Money(net);
            word.text = kind == Life ? Ui.Col("-1 LIFE", Look.Blood * 1.8f) : kind == Jackpot ? "JACKPOT\n" + amount : kind == Big ? "BIG WIN\n" + amount : "WIN  " + amount;
            word.lineSpacing = -20;
        }

        static float Hash(int i, int k) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.547f, 1f);

        public void Update(float t)
        {
            bool on = kind != None && t >= 0 && t < Feel.CelebrateS;
            // sparks: out from the play and falling, each its own way, a few at a time
            int n = !on || kind == Life ? 0 : kind == Win ? 14 : kind == Big ? 30 : sparks.Length;
            float speed = kind == Win ? 0.9f : kind == Big ? 1.25f : 1.6f;
            for (int i = 0; i < sparks.Length; i++)
            {
                var s = sparks[i];
                float tau = t - (i % 5) * 0.07f - (i / 16) * 0.35f;
                const float life = 1.2f;
                bool show = i < n && tau > 0 && tau < life;
                if (s.gameObject.activeSelf != show) s.gameObject.SetActive(show);
                if (!show) continue;
                float ang = i * 2.399f + (i % 3) * 0.4f;
                float h = 0.35f + Hash(i, 1) * 0.5f, up = 0.6f + Hash(i, 2) * 0.7f;
                var v = new Vector3(Mathf.Cos(ang) * h, up, Mathf.Sin(ang) * h) * speed * (0.7f + Hash(i, 3) * 0.5f);
                var pos = origin + v * tau + 0.5f * new Vector3(0, -3.2f, 0) * tau * tau;
                pos.y = Mathf.Max(pos.y, floorY + 0.004f);
                s.localPosition = pos;
                s.localScale = Vector3.one * (tau < life * 0.55f ? 1 : 1 - (tau - life * 0.55f) / (life * 0.45f));
            }
            // a jackpot showers coins that land and lie
            int nc = on && kind == Jackpot ? coins.Length : 0;
            for (int i = 0; i < coins.Length; i++)
            {
                var c = coins[i];
                float tau = t - (i % 7) * 0.05f;
                bool show = i < nc && tau > 0;
                if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
                if (!show) continue;
                float ang = i * 2.1f + Hash(i, 4);
                var v = new Vector3(Mathf.Cos(ang) * (0.15f + Hash(i, 5) * 0.35f), 0.8f + Hash(i, 6) * 0.5f, Mathf.Sin(ang) * (0.1f + Hash(i, 7) * 0.25f));
                var pos = origin + v * tau + 0.5f * new Vector3(0, -6f, 0) * tau * tau;
                bool down = pos.y <= floorY + 0.0016f;
                pos.y = Mathf.Max(pos.y, floorY + 0.0016f);
                c.localPosition = pos;
                c.localRotation = down ? Quaternion.Euler(0, i * 40, 0) : Quaternion.Euler(tau * (500 + i * 23), tau * 300, i * 17);
            }
            // the light: a gold flare (a strobe on a jackpot), or blood red for a life
            flash.enabled = on;
            if (on)
            {
                float peak = kind == Life ? 7f : kind == Win ? 1.2f : kind == Big ? 2.6f : 5f;
                float blink = kind == Jackpot ? (Mathf.FloorToInt(t * 10) % 2 == 0 ? 1 : 0.35f) : kind == Life && t < 0.9f ? (Mathf.FloorToInt(t * 8) % 2 == 0 ? 1 : 0.3f) : 1;
                flash.color = kind == Life ? Look.Blood : Look.Gold;
                flash.intensity = peak * Mathf.Exp(-t * (kind == Life ? 1.4f : 1.6f)) * blink;
            }
            // the word: pops in, drifts up, gone after Beats.WordS
            bool w = on && kind != None && t < Beats.WordS;
            if (word.gameObject.activeSelf != w) word.gameObject.SetActive(w);
            if (w)
            {
                float size = kind == Win || kind == Life ? 0.045f : kind == Big ? 0.055f : 0.062f;
                word.fontSize = size * 13f * (t < 0.15f ? 1.3f : 1f);
                wordT.localPosition = textPos + textRot * Vector3.up * (0.02f * t);
            }
        }
    }
}
