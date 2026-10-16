// Floor 2's tables in 3D (DESIGN.md §11): the roulette wheel turning one way and the ball the other until
// it drops into the sim's pocket; craps dice thrown by you, really rolling (DiceLab), against the far wall;
// the peg board's ball bouncing down the sim's path into its slot; Crash's monitor drawing the climb.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Client
{
    // ------------------------------------------------------------------ Roulette

    public sealed class RouletteShow : TableShow
    {
        const float BowlR = 0.42f, TrackR = 0.36f, PocketR = 0.25f;
        readonly Transform rotor, ball;
        readonly Vector3 at;
        static float Theta => 360f / Roulette.Wheel.Length;

        public override Vector3 LookShift => new Vector3(-0.5f, -0.1f, 0.15f);

        public RouletteShow(Transform root) : base("roulette", root)
        {
            at = new Vector3(-(float)Pl.W / 2 + 0.55f, FeltY, 0.05f);
            Vector2 P(float r, float y) => new Vector2(r, y);
            Props.Part("bowl", MeshKit.Lathe(new[] { P(BowlR + 0.04f, 0), P(BowlR + 0.04f, 0.09f), P(BowlR, 0.1f), P(TrackR + 0.02f, 0.07f), P(PocketR + 0.04f, 0.035f), P(PocketR, 0.03f) }, 64), Look.DarkWood, root, at);
            rotor = Props.Group("rotor", root, at + Vector3.up * 0.03f);
            for (int i = 0; i < Roulette.Wheel.Length; i++)
            {
                int n = Roulette.Wheel[i];
                var mat = n == 0 ? Look.Surface("rPocketG", new Color(0.02f, 0.4f, 0.15f), 0.8f) : Roulette.IsRed(n) ? Look.Surface("rPocketR", new Color(0.65f, 0.04f, 0.07f), 0.8f) : Look.Surface("rPocketB", new Color(0.03f, 0.03f, 0.035f), 0.85f);
                Props.Part("pocket " + n, Sector(0.12f, PocketR, i * Theta, (i + 1) * Theta), mat, rotor, Vector3.zero, null, false);
                float mid = (i + 0.5f) * Theta * Mathf.Deg2Rad;
                Props.Print(rotor, n.ToString(), Color.white, 0.022f, new Vector3(Mathf.Sin(mid) * PocketR * 0.82f, 0.002f, Mathf.Cos(mid) * PocketR * 0.82f), Quaternion.Euler(90, (i + 0.5f) * Theta, 0));
            }
            Props.Part("cone", MeshKit.Lathe(new[] { P(0.12f, 0), P(0.06f, 0.03f), P(0.025f, 0.05f), P(0.025f, 0.09f), P(0.045f, 0.1f), P(0, 0.11f) }, 32), Look.BrassMat, rotor, Vector3.zero);
            ball = Props.Part("ball", Props.Bulb(0.011f), Look.Surface("ivory", new Color(0.95f, 0.94f, 0.9f), 0.9f), root, at).transform;
            Party = new Celebration(root, new Vector3(0.2f, FeltY + 0.03f, 0), FeltY, new Vector3(0.2f, FeltY + 0.05f, -0.2f), Quaternion.Euler(38, 0, 0));
            // the layout printed on the felt, for show (bets go through the screen's layout)
            for (int col = 0; col < 12; col++)
                for (int row = 0; row < 3; row++)
                {
                    int n = col * 3 + 3 - row;
                    Props.Print(root, n.ToString(), Roulette.IsRed(n) ? new Color(1f, 0.3f, 0.3f) : Look.Bone, 0.04f,
                        new Vector3(-0.15f + col * 0.11f, FeltY + 0.002f, 0.18f - row * 0.12f), Quaternion.Euler(90, 0, 0));
                }
        }

        static Mesh Sector(float r0, float r1, float a0, float a1)
        {
            var v = new List<Vector3>(); var tris = new List<int>();
            int seg = 4;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                v.Add(new Vector3(Mathf.Sin(a) * r0, 0, Mathf.Cos(a) * r0));
                v.Add(new Vector3(Mathf.Sin(a) * r1, 0, Mathf.Cos(a) * r1));
            }
            for (int i = 0; i < seg; i++) { int b = i * 2; tris.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 }); }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            int pocket = o?.Pocket ?? 0;
            int idx = System.Array.IndexOf(Roulette.Wheel, pocket);
            float pocketDeg = (idx + 0.5f) * Theta;
            float rotorDeg, ballDeg, r, y;
            if (live)
            {
                float e = Beats.Out3(p);
                rotorDeg = Salt * 50 + 720 * e;
                // the ball runs against the rotor, loses speed, falls off the track and rattles into its pocket
                float drop = Mathf.InverseLerp(0.62f, 0.86f, p);
                ballDeg = rotorDeg + pocketDeg + (1 - e) * -1440 - (1 - drop) * 30;
                r = Mathf.Lerp(TrackR, PocketR * 0.86f, drop * drop);
                y = Mathf.Lerp(0.085f, 0.04f, drop) + Mathf.Abs(Mathf.Sin(drop * Mathf.PI * 4)) * 0.012f * (1 - drop);
            }
            else
            {
                rotorDeg = Salt * 50 + 720 + since * 18;
                ballDeg = rotorDeg + pocketDeg;
                r = PocketR * 0.86f;
                y = 0.04f;
            }
            rotor.localRotation = Quaternion.Euler(0, rotorDeg, 0);
            float br = ballDeg * Mathf.Deg2Rad;
            ball.localPosition = at + new Vector3(Mathf.Sin(br) * r, y, Mathf.Cos(br) * r);
        }
    }

    // ------------------------------------------------------------------ Craps

    /// <summary>Two dice thrown from your end of the table against the far wall, really rolling (DiceLab).
    /// Every roll shows: the come-out, each roll for the point (the sim's 'roll' events), the deciding one.</summary>
    public sealed class CrapsShow : TableShow
    {
        readonly Props.Die[] dice = new Props.Die[2];
        readonly TextMeshPro puck;
        DiceLab.Throw thr;
        int thrKey = -1;
        List<int> rollDice;
        int rollTick = -1000, rollHold;
        const float Size = 0.05f;

        public CrapsShow(Transform root) : base("craps", root)
        {
            for (int i = 0; i < 2; i++) dice[i] = new Props.Die(root, Size, "die " + (i + 1));
            puck = Props.Print(root, "OFF", Look.Ink, 0.05f, new Vector3(0.5f, FeltY + 0.03f, 0.3f), Quaternion.Euler(90, 0, 0));
            Props.Part("puck", Props.Cached("puck", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.06f, 0), new Vector2(0.06f, 0.025f), new Vector2(0, 0.025f) }, 32)), Look.Surface("puckWhite", new Color(0.9f, 0.9f, 0.88f), 0.6f), root, new Vector3(0.5f, FeltY, 0.3f));
            Party = new Celebration(root, new Vector3(0, FeltY + 0.03f, 0.1f), FeltY, new Vector3(0, FeltY + 0.05f, -0.15f), Quaternion.Euler(38, 0, 0));
        }

        /// <summary>A roll that didn't decide the bet (the sim's 'roll' event).</summary>
        public void OnRoll(List<int> d, int tick, int hold)
        {
            rollDice = d;
            rollTick = tick;
            rollHold = Mathf.Max(1, hold);
            latestRoll = true;
        }

        public override Vector3 LookShift => new Vector3(0, -0.15f, 0.35f);

        void Pose(DiceLab.Throw th, float s)
        {
            for (int k = 0; k < 2; k++)
            {
                th.Pose(k, s, out var pos, out var rot);
                dice[k].T.localPosition = pos + new Vector3(0, FeltY + Size / 2, 0);
                dice[k].T.localRotation = rot;
            }
        }

        DiceLab.Throw Get(List<int> faces, int key)
        {
            if (thr != null && thrKey == key) return thr;
            DiceLab.Launch(2, Size, (float)Pl.D - 0.3f, key, 1.1f, out var from, out var vel, out var spin);
            thr = DiceLab.Simulate(Size, (float)Pl.W - 0.3f, (float)Pl.D - 0.3f, 0.16f, from, vel, spin, faces.Take(2).ToArray());
            thrKey = key;
            Sfx.Dice(thr, Pl, Feel.CrapsS);
            return thr;
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            puck.text = t.Point == 0 ? "OFF" : t.Point.ToString();
            float nowT = nowS * State.TicksPerS;
            if (live && o?.Dice != null)
            {
                latestRoll = false;
                Pose(Get(o.Dice, t.Rounds * 101 + o.Dice[0] * 7 + o.Dice[1]), p * thr.Seconds / 0.97f);
            }
            else if (latestRoll && rollDice != null)
                Pose(Get(rollDice, rollTick * 13 + rollDice[0] * 7 + rollDice[1]), (nowT - rollTick) / rollHold * thr.Seconds / 0.97f);
            else if (o?.Dice != null)
                Pose(Get(o.Dice, (t.Rounds - 1) * 101 + o.Dice[0] * 7 + o.Dice[1]), 99);
            else
                for (int k = 0; k < 2; k++)
                {
                    dice[k].T.localPosition = new Vector3(0.35f + k * 0.07f, FeltY + Size / 2, -0.25f);
                    dice[k].T.localRotation = Props.Die.FaceUp(k == 0 ? 6 : 1, k * 20);
                }
        }

        bool latestRoll;
    }

    // ------------------------------------------------------------------ Peg Drop

    /// <summary>The board above the cabinet's screen: 8 rows of pegs, 9 slots with their multiples, a ball that
    /// bounces left or right at each row as the sim's path says and lands in the sim's slot.</summary>
    public sealed class PegShow : TableShow
    {
        readonly Transform ball;
        readonly MeshRenderer[] slotLamps = new MeshRenderer[9];
        readonly Vector3 top;
        const float Gap = 0.105f, RowH = 0.12f;

        public PegShow(Transform root) : base("pegdrop", root)
        {
            float bd = Mathf.Min(0.8f, (float)Pl.D);
            top = new Vector3(0, 2.98f, -bd / 2 + 0.0f);
            var peg = Props.Bulb(0.011f);
            for (int r = 0; r < PegDrop.Rows; r++)
                for (int k = 0; k <= r + 1; k++)
                    Props.Part("peg", peg, Look.Chrome, root, top + new Vector3((k - (r + 1) / 2f) * Gap, -(r + 1) * RowH, 0), null, false);
            for (int s = 0; s < 9; s++)
            {
                var pos = top + new Vector3((s - 4) * Gap, -(PegDrop.Rows + 1) * RowH - 0.05f, -0.002f);
                Color c = PegDrop.Mult[s] >= 4 ? Look.Gold : PegDrop.Mult[s] > 1 ? Look.LowHi : s == 4 ? Look.Blood : Look.Smoke;
                Props.Print(root, Js.Num(PegDrop.Mult[s]) + "x", c, 0.032f, pos, Quaternion.identity);
                slotLamps[s] = Props.Part("slot lamp", Props.Bulb(0.014f), Look.Glow("pegOff", new Color(0.3f, 0.2f, 0.4f), 0.4f), root, pos + new Vector3(0, -0.045f, 0), null, false).GetComponent<MeshRenderer>();
            }
            ball = Props.Part("ball", Props.Bulb(0.022f), Look.Glow("pegBall", new Color(1f, 0.85f, 0.5f), 4f), root, top + new Vector3(0, 0.05f, -0.01f), null, false).transform;
            Party = new Celebration(root, top + new Vector3(0, -1.1f, -0.05f), 1.0f, top + new Vector3(0, -0.4f, -0.1f), Quaternion.identity);
        }

        public override Vector3 LookShift => new Vector3(0, 0.72f, 0);

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            var path = o?.Path;
            int slot = o?.Slot ?? 4;
            Vector3 pos;
            if (live && path != null)
            {
                // nine legs: a drop to the first row, then a hop per row, then into the slot
                float f = p * (PegDrop.Rows + 1);
                int leg = Mathf.Min(PegDrop.Rows, Mathf.FloorToInt(f));
                float u = f - leg;
                float x0 = Xat(path, leg), x1 = Xat(path, Mathf.Min(PegDrop.Rows, leg + 1));
                float y0 = -leg * RowH, y1 = -(leg + 1) * RowH;
                pos = top + new Vector3(Mathf.Lerp(x0, x1, u), Mathf.Lerp(y0, y1, u * u) + Mathf.Sin(u * Mathf.PI) * 0.03f, -0.012f);
            }
            else pos = top + new Vector3((slot - 4) * Gap, -(PegDrop.Rows + 1) * RowH, -0.012f);
            ball.localPosition = pos;
            for (int s = 0; s < 9; s++)
                slotLamps[s].sharedMaterial = !live && o != null && s == slot && since < 6 && Mathf.Repeat(since * 4, 1) < 0.6f ? Look.Glow("pegOn", PegDrop.Mult[s] > 1 ? Look.Gold : Look.Blood, 6f) : Look.Glow("pegOff", new Color(0.3f, 0.2f, 0.4f), 0.4f);
        }

        /// <summary>The ball's x after `rows` bounces of the path.</summary>
        static float Xat(List<int> path, int rows)
        {
            int rights = 0;
            for (int i = 0; i < rows && i < path.Count; i++) rights += path[i];
            return (rights - rows / 2f) * Gap;
        }
    }

    // ------------------------------------------------------------------ Crash

    /// <summary>The monitor behind the table: the climb drawn as a curve, the multiplier big in the middle,
    /// gold as it rises and red past the deadly 5x; it freezes green when you cash, red when it crashes.</summary>
    public sealed class CrashShow : TableShow
    {
        readonly TextMeshPro mult, note;
        readonly LineRenderer line;
        readonly Transform screen;
        const float W = 1.5f, H = 0.85f;

        public CrashShow(Transform root) : base("crash", root)
        {
            float h = (float)Pl.H, d = (float)Pl.D;
            screen = Props.Group("crash screen", root, new Vector3(0, h + 1.1f, d / 2 + 0.005f));
            Props.Part("glass", MeshKit.Quad(W + 0.1f, H + 0.1f), Look.Screen("crashGlass", new Color(0.02f, 0.01f, 0.01f)), screen, new Vector3(0, 0, -0.002f), null, false);
            mult = Props.Print(screen, "1.00x", Color.white, 0.2f, new Vector3(0, 0.05f, -0.01f), Quaternion.identity);
            mult.rectTransform.sizeDelta = new Vector2(W, 0.4f);
            note = Props.Print(screen, "", Look.Smoke, 0.06f, new Vector3(0, -0.3f, -0.01f), Quaternion.identity);
            note.rectTransform.sizeDelta = new Vector2(W, 0.15f);
            var lg = new GameObject("curve");
            lg.transform.SetParent(screen, false);
            lg.transform.localPosition = new Vector3(0, 0, -0.006f);
            line = lg.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = 0.012f;
            line.sharedMaterial = Look.Neon(Look.High, 3f);
            line.positionCount = 0;
            Party = new Celebration(root, new Vector3(0, h + 0.03f, 0.1f), h, new Vector3(0, h + 0.05f, -0.15f), Quaternion.Euler(38, 0, 0));
        }

        public override Vector3 LookShift => new Vector3(0, 0.62f, 0.2f);

        void Curve(double upTo, double scaleMax)
        {
            int n = 40;
            line.positionCount = n;
            double secs = System.Math.Log(System.Math.Max(1.0001, upTo)) / Feel.CrashRate;
            double ymax = System.Math.Max(2, scaleMax);
            for (int i = 0; i < n; i++)
            {
                double s = secs * i / (n - 1);
                double m = System.Math.Exp(Feel.CrashRate * s);
                float x = -W / 2 + 0.05f + (float)(i / (double)(n - 1)) * (W - 0.1f) * Mathf.Clamp01((float)(secs / 6));
                float y = -H / 2 + 0.05f + (float)((m - 1) / (ymax - 1)) * (H - 0.1f);
                line.SetPosition(i, new Vector3(x, Mathf.Min(y, H / 2 - 0.02f), 0));
            }
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            if (t.Crash != null)
            {
                double m = Crash.MultAt(Mathf.FloorToInt(nowS * State.TicksPerS) - t.Crash.Start);
                mult.text = Crash.X(m) + "x";
                mult.color = m >= Crash.Deadly ? new Color(1f, 0.2f, 0.2f) : m >= 2 ? Look.Gold : Color.white;
                note.text = m >= Crash.Deadly ? "PAST 5x: A CRASH COSTS A LIFE" : t.Crash.Target > 0 ? $"AUTO CASH-OUT AT {Crash.X(t.Crash.Target)}x" : "CASH OUT BEFORE IT CRASHES";
                Curve(m, m * 1.2);
                line.sharedMaterial = Look.Neon(m >= Crash.Deadly ? Look.Blood : Look.Gold, 3f);
            }
            else if (o?.Mult != null)
            {
                bool crashed = o.Crashed == true;
                mult.text = Crash.X(o.Mult.Value) + "x";
                mult.color = crashed ? new Color(1f, 0.15f, 0.2f) : Look.LowHi;
                note.text = crashed ? "CRASHED" : $"CASHED OUT: {Ui.Money(o.Ret)}";
                Curve(o.Mult.Value, o.Mult.Value * 1.2);
                line.sharedMaterial = Look.Neon(crashed ? Look.Blood : Look.Low, 3f);
            }
            else
            {
                mult.text = "1.00x";
                mult.color = Color.white;
                note.text = "LAUNCH TO START THE CLIMB";
                line.positionCount = 0;
            }
        }
    }
}
