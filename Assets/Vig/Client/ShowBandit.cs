// The Bandit: three backlit drums in a window above the cabinet's screen, three rows showing. Pull the
// lever and each reel runs past, slows and locks, left to right (Beats.BanditStops); each column's run of
// symbols ends on the three the sim drew, so what you watch is what lands. Once the grid is down the
// patterns that paid are outlined (Bandit.GridPays: the sim's own pay check).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Client
{
    public sealed class BanditShow : TableShow
    {
        /// <summary>the reel window in the playable cabinet (RoomBuilder cuts the body round it)</summary>
        public const float WinY = 1.95f, WinH = 0.34f, WinW = 0.84f;
        const float DrumR = 0.19f, DrumW = 0.235f, Pitch = 30f, Lean = -15f;
        /// <summary>how many cells a reel rolls past before it stops (the TS game's REEL_RUN)</summary>
        const int Run = 24;
        static readonly int[] Idle = { Bandit.Seven, Bandit.Star, Bandit.Bar, Bandit.Star, Bandit.Seven, Bandit.Bar, Bandit.Bar, Bandit.Bar, Bandit.Seven };

        readonly Transform[] drums = new Transform[Bandit.Cols];
        readonly TextMeshPro[][] cells = new TextMeshPro[Bandit.Cols][];
        readonly Transform[] frames = new Transform[Bandit.Cells];
        readonly MeshRenderer[] frameR = new MeshRenderer[Bandit.Cells];
        readonly Transform lever;
        readonly Material hitBig = Look.Neon(Look.Gold, 6f), hitSmall = Look.Neon(new Color(1f, 0.95f, 0.85f), 4f);
        readonly List<Bandit.Hit> hits = new List<Bandit.Hit>();
        Outcome hitsOf;

        public BanditShow(Transform root) : base("bandit", root)
        {
            float bd = Mathf.Min(0.85f, (float)Pl.D);
            float z = -bd / 2 + DrumR + 0.025f;
            Vector2 P(float r, float y) => new Vector2(r, y);
            var drum = Props.Cached("drum", () => MeshKit.Lathe(new[] { P(DrumR - 0.01f, -DrumW / 2), P(DrumR, -DrumW / 2 + 0.006f), P(DrumR, DrumW / 2 - 0.006f), P(DrumR - 0.01f, DrumW / 2) }, 40));
            var drumMat = Look.Surface("reelDrum", new Color(0.92f, 0.88f, 0.8f), 0.4f, 0, emission: new Color(0.42f, 0.38f, 0.3f));
            var frame = Props.Cached("cellFrame", () => MeshKit.Tube(MeshKit.RoundRect(DrumW - 0.03f, 0.088f, 0.012f), 0.0028f, 5, true));
            for (int c = 0; c < Bandit.Cols; c++)
            {
                var at = new Vector3((c - 1) * (DrumW + 0.035f), WinY + 0.045f, z);
                drums[c] = Props.Group("reel " + (c + 1), root, at);
                Props.Part("drum", drum, drumMat, drums[c], Vector3.zero, Quaternion.Euler(0, 0, 90), false);
                cells[c] = new TextMeshPro[6];
                for (int k = 0; k < cells[c].Length; k++) cells[c][k] = Props.Print(drums[c], "", Color.black, 0.06f, Vector3.zero, Quaternion.identity);
                for (int r = 0; r < Bandit.Rows; r++)
                {
                    int i = r * Bandit.Cols + c;
                    float a = (1 - r) * Pitch + Lean;
                    var go = Props.Part("paid " + i, frame, hitSmall, drums[c], OnDrum(a, 0.006f), Quaternion.Euler(a, 0, 0), false);
                    frames[i] = go.transform;
                    frameR[i] = go.GetComponent<MeshRenderer>();
                    go.SetActive(false);
                }
            }
            // the lever on the cabinet's side: RoomBuilder made it, the show swings it from its foot
            var stick = root.Find("lever");
            var knob = root.Find("knob");
            if (stick && knob)
            {
                lever = Props.Group("lever pivot", root, stick.localPosition);
                stick.SetParent(lever, true);
                knob.SetParent(lever, true);
            }
            // a lamp over the reels, inside the window
            var lg = new GameObject("reel light");
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(0, WinY + WinH / 2 - 0.03f, -bd / 2 + 0.03f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.65f);
            l.intensity = 0.5f;
            l.range = 0.5f;
            Party = new Celebration(root, new Vector3(0, WinY, -bd / 2 - 0.06f), 0, new Vector3(0, 0.9f, -bd / 2 - 0.3f), Quaternion.Euler(25, 0, 0));
        }

        /// <summary>The seated eye lifts a little: the reels are above the screen.</summary>
        public override Vector3 LookShift => new Vector3(0, 0.14f, 0);

        /// <summary>A point on a drum's face at `deg` above the front (0 is the middle row), `lift` off it.</summary>
        static Vector3 OnDrum(float deg, float lift) => new Vector3(0, Mathf.Sin(deg * Mathf.Deg2Rad), -Mathf.Cos(deg * Mathf.Deg2Rad)) * (DrumR + lift);

        static void Symbol(TextMeshPro t, int s)
        {
            string text; Color c; float h;
            switch (s)
            {
                case Bandit.Seven: text = "7"; c = new Color(0.78f, 0.04f, 0.14f); h = 0.075f; break;
                case Bandit.Star: text = TableScreen.Glyph('★', "*"); c = new Color(0.12f, 0.22f, 0.7f); h = text == "*" ? 0.11f : 0.075f; break;
                case Bandit.Bar: text = "BAR"; c = new Color(0.08f, 0.07f, 0.08f); h = 0.052f; break;
                default: text = "-"; c = new Color(0.55f, 0.5f, 0.45f); h = 0.04f; break;
            }
            if (t.text != text) t.text = text;
            t.color = c;
            t.fontSize = h * 13f;
        }

        /// <summary>A column's reel for this round: its three results first (top to bottom), then a run of
        /// the strip above them, the same every frame of the round (the TS game's reel()).</summary>
        static int SeqAt(int c, IReadOnlyList<int> grid, int round, int k)
        {
            if (k < Bandit.Rows) return grid[k * Bandit.Cols + c];
            uint x = unchecked((uint)(round * 7919 + c * 104729));
            int s = Bandit.Seven;
            for (int i = Bandit.Rows; i <= k && i < Bandit.Rows + Run + 2; i++)
            {
                x = unchecked(x * 1103515245u + 12345u);
                s = Bandit.Strip[(x >> 16) % (uint)Bandit.Strip.Length];
            }
            return s;
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            var grid = o?.Reels != null && o.Reels.Count == Bandit.Cells ? (IReadOnlyList<int>)o.Reels : Idle;
            float roundS = (float)Feel.BanditS;
            for (int c = 0; c < Bandit.Cols; c++)
            {
                float stopAt = Beats.BanditStops[c];
                bool done = !live || p >= stopAt;
                // s: how many cells of the run are still above the window; 0 is the result in place
                float s = done ? 0 : Run * Mathf.Pow(1 - p / stopAt, 3);
                float stopped = live ? (p - stopAt) * roundS : 1;
                float bob = done && stopped < 0.12f ? Mathf.Sin(stopped / 0.12f * Mathf.PI) * -4f : 0;
                int k0 = Mathf.CeilToInt(s - 1.6f);
                var pool = cells[c];
                for (int j = 0; j < pool.Length; j++)
                {
                    int k = k0 + j;
                    float row = k - s;
                    bool show = k >= 0 && row < 3.6f;
                    if (pool[j].gameObject.activeSelf != show) pool[j].gameObject.SetActive(show);
                    if (!show) continue;
                    float a = (1 - row) * Pitch + Lean + bob;
                    pool[j].transform.localPosition = OnDrum(a, 0.0015f);
                    pool[j].transform.localRotation = Quaternion.Euler(a, 0, 0);
                    Symbol(pool[j], SeqAt(c, grid, Round, k));
                }
            }
            // the lever: pulled down and let go as the reels start
            if (lever)
            {
                float el = live ? p * roundS : 9;
                float k = Mathf.Clamp01(el / 0.45f);
                lever.localRotation = Quaternion.Euler(-Mathf.Sin(k * Mathf.PI) * 42f, 0, 0);
            }
            // the patterns that paid, outlined once the grid is down (blinking through the celebration)
            if (o != hitsOf)
            {
                hitsOf = o;
                hits.Clear();
                if (o?.Reels != null) Bandit.GridPays(o.Reels, hits);
            }
            bool blink = Mathf.FloorToInt(since * 4) % 2 == 0 || since > Feel.CelebrateS;
            for (int i = 0; i < Bandit.Cells; i++)
            {
                double best = 0;
                foreach (var h in hits) if (System.Array.IndexOf(h.Cells, i) >= 0) best = System.Math.Max(best, h.X);
                bool on = !live && blink && best > 0;
                if (frames[i].gameObject.activeSelf != on) frames[i].gameObject.SetActive(on);
                if (on) frameR[i].sharedMaterial = best >= Bandit.PayLineStar ? hitBig : hitSmall;
            }
        }
    }
}
