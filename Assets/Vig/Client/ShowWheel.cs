// The Wheel of Marks: an upright big-six wheel on a brass stand at the table's end, 25 wedges in
// WheelOfMarks.Layout's order and colors, brass pegs between them, a flapper at the top that the pegs
// knock as they pass, a ring of lamps that chase while it turns and flash when it lands. It slows onto
// the slot the sim drew (Beats.WheelTurns whole turns first). Drawn for this game (TS DECISIONS #62).
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Client
{
    public sealed class WheelShow : TableShow
    {
        const float R = 0.3f, Face = -0.027f;
        readonly Transform wheel, rotor, flapper;
        readonly MeshRenderer[] lamps = new MeshRenderer[20];
        readonly TMPro.TextMeshPro hub;
        readonly Material lampOn = Look.Glow("wheelLampOn", new Color(1f, 0.9f, 0.7f), 7f), lampOff = Look.Glow("wheelLampOff", new Color(0.4f, 0.25f, 0.1f), 0.25f);
        readonly Material lampWin = Look.Glow("wheelLampWin", Look.Gold, 9f), lampMark = Look.Glow("wheelLampMark", new Color(1f, 0.55f, 0.1f), 7f);
        static float Theta => 360f / WheelOfMarks.Slots;

        /// <summary>the wheel's own colors (none of them a tier's)</summary>
        public static Material Wedge(string c)
        {
            switch (c)
            {
                case "red": return Look.Surface("wedgeRed", new Color(0.72f, 0.05f, 0.09f), 0.75f);
                case "black": return Look.Surface("wedgeBlack", new Color(0.025f, 0.022f, 0.028f), 0.85f);
                case "purple": return Look.Surface("wedgePurple", new Color(0.26f, 0.05f, 0.42f), 0.75f);
                case "gold": return Look.Surface("wedgeGold", Look.Gold, 0.8f, 1);
                default: return Look.Surface("wedgeMark", new Color(0.42f, 0.24f, 0.05f), 0.7f);
            }
        }

        static string Label(string c) => c == "mark" ? "MARK" : WheelOfMarks.Pays[c].ToString("0.#").TrimStart('0') + "x";
        static Color LabelColor(string c) => c == "gold" ? Look.Ink : c == "black" ? Look.Bone * 0.75f : c == "mark" ? Look.Bone : Color.white;

        /// <summary>A point on the face at angle `deg` clockwise from the top (as you face it), radius r.</summary>
        static Vector3 Dir(float deg, float r, float z = Face) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, Mathf.Cos(deg * Mathf.Deg2Rad) * r, z);

        public WheelShow(Transform root) : base("wheel", root)
        {
            // standing above the screen at the table's far edge, leaning a little to face the seat
            var at = new Vector3(0, 2.0f, (float)Pl.D / 2 - 0.02f);
            var eye = RoomBuilder.U(Pl.Eye) - root.position;
            Vector2 P(float r, float y) => new Vector2(r, y);
            // the stand: a brass column from the floor behind the table
            Props.Part("stand", MeshKit.Lathe(new[] { P(0.17f, 0), P(0.17f, 0.025f), P(0.06f, 0.06f), P(0.032f, 0.12f), P(0.032f, at.y), P(0, at.y) }, 28), Look.BrassMat, root, new Vector3(0, 0, at.z + 0.065f));
            wheel = Props.Group("wheel of marks", root, at, Quaternion.LookRotation(at - eye));
            // the body, the rim
            Props.Part("body", MeshKit.Lathe(new[] { P(R + 0.075f, -0.025f), P(R + 0.075f, 0.025f) }, 72), Look.DarkWood, wheel, Vector3.zero, Quaternion.Euler(90, 0, 0));
            var ring = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 72; i++) ring.Add(Dir(i * 5, R + 0.012f, 0));
            Props.Part("rim", MeshKit.Tube(ring, 0.01f, 8, true), Look.BrassMat, wheel, new Vector3(0, 0, Face));
            var outer = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 72; i++) outer.Add(Dir(i * 5, R + 0.075f, 0));
            Props.Part("outer rim", MeshKit.Tube(outer, 0.012f, 8, true), Look.BrassMat, wheel, Vector3.zero);
            // the rotor: wedges, their multiples, the pegs
            rotor = Props.Group("rotor", wheel, Vector3.zero);
            var peg = Props.Cached("wheelPeg", () => MeshKit.Lathe(new[] { P(0.0045f, 0), P(0.0045f, 0.018f), P(0.006f, 0.02f), P(0, 0.023f) }, 10));
            for (int i = 0; i < WheelOfMarks.Slots; i++)
            {
                string c = WheelOfMarks.Layout[i];
                Props.Part("wedge " + i, Sector(0.07f, R, i * Theta, (i + 1) * Theta), Wedge(c), rotor, new Vector3(0, 0, Face), null, false);
                float mid = (i + 0.5f) * Theta;
                // the multiple reads from the hub outward, along the wedge
                var t = Props.Print(rotor, Label(c), LabelColor(c), c == "mark" ? 0.019f : 0.027f, Dir(mid, R * 0.66f, Face - 0.0015f), Quaternion.Euler(0, 0, 90 - mid));
                t.characterSpacing = c == "mark" ? 2 : 0;
                Props.Part("peg", peg, Look.BrassMat, rotor, Dir(i * Theta, R - 0.01f, Face), Quaternion.Euler(-90, 0, 0), false);
            }
            Props.Part("hub", MeshKit.Lathe(new[] { P(0.07f, 0), P(0.068f, 0.008f), P(0.05f, 0.016f), P(0, 0.019f) }, 32), Look.BrassMat, wheel, new Vector3(0, 0, Face), Quaternion.Euler(-90, 0, 0));
            hub = Props.Print(wheel, "", Look.Ink, 0.026f, new Vector3(0, 0, Face - 0.0195f), Quaternion.identity);
            // the lamps round the body, outside the rim
            for (int k = 0; k < lamps.Length; k++)
                lamps[k] = Props.Part("lamp", Props.Bulb(0.011f), lampOff, wheel, Dir(k * 18 + 9, R + 0.045f, Face), null, false).GetComponent<MeshRenderer>();
            // the flapper at the top, pivoting on its pin; its tip rides in among the pegs
            flapper = Props.Group("flapper", wheel, new Vector3(0, R + 0.06f, Face - 0.03f));
            Props.Part("pin", MeshKit.Lathe(new[] { P(0.012f, -0.012f), P(0.012f, 0.03f), P(0, 0.03f) }, 12), Look.BrassMat, flapper, Vector3.zero, Quaternion.Euler(-90, 0, 0));
            var tip = Props.Part("tip", MeshKit.Lathe(new[] { P(0, -0.075f), P(0.02f, -0.01f), P(0.014f, 0.006f), P(0, 0.008f) }, 4), Look.Glow("flapper", new Color(1f, 0.96f, 0.9f), 1.4f), flapper, Vector3.zero, Quaternion.Euler(0, 45, 0));
            tip.transform.localScale = new Vector3(1, 1, 0.35f);
            // its own light: the pendant is over the felt
            var lg = new GameObject("wheel light");
            lg.transform.SetParent(wheel, false);
            lg.transform.localPosition = new Vector3(0, 0.3f, -0.55f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.7f);
            l.intensity = 1.2f;
            l.range = 1.3f;
            Party = new Celebration(wheel, new Vector3(0, 0, Face - 0.05f), -at.y, new Vector3(0, -R * 0.4f, Face - 0.09f), Quaternion.identity);
        }

        /// <summary>The seated eye looks level: the wheel above the screen and the screen both in frame.</summary>
        public override Vector3 LookShift => new Vector3(0, 0.36f, 0);

        /// <summary>A flat wedge from radius r0 to r1 between two angles, facing -Z.</summary>
        static Mesh Sector(float r0, float r1, float a0, float a1)
        {
            const int seg = 6;
            var v = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                v.Add(Dir(a, r0, 0));
                v.Add(Dir(a, r1, 0));
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 2;
                // clockwise as seen from -Z: inner, outer, next outer; inner, next outer, next inner
                tris.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
            }
            var m = new Mesh { name = "Sector" };
            m.SetVertices(v);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.back;
            m.normals = n;
            var uv = new Vector2[v.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x, v[i].y);
            m.uv = uv;
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            int land = o?.Slot ?? 0;
            float jitter = ((Round * 37) % 9 - 4) / 14f;
            float rest = -(land + 0.5f + jitter) * Theta;
            float a = live ? rest - (1 - Beats.Out3(p)) * (Beats.WheelTurns * 360f + 75f) : rest;
            rotor.localRotation = Quaternion.Euler(0, 0, -a);
            // what the pointer is on, on the hub
            float under = Mathf.Repeat(-a / Theta, WheelOfMarks.Slots);
            string now = WheelOfMarks.Layout[Mathf.FloorToInt(under) % WheelOfMarks.Slots];
            string label = Label(now);
            if (hub.text != label) hub.text = label;
            // the flapper: knocked aside by each peg as it passes, harder while the wheel is fast
            float fr = Mathf.Repeat(-a / Theta, 1);
            float speed = live ? 1 - Beats.Out3(p) * 0.85f : 0;
            flapper.localRotation = Quaternion.Euler(0, 0, live ? 22f * speed * Mathf.Pow(1 - fr, 2) : 0);
            // the lamps: chasing while it turns, all flashing gold on a win (amber on a mark), else every other
            bool party = !live && o != null && since >= 0 && since < Feel.CelebrateS;
            for (int k = 0; k < lamps.Length; k++)
            {
                Material m;
                if (party) m = Mathf.FloorToInt(since * 6) % 2 == 0 ? (o.Mark != null ? lampMark : o.Win ? lampWin : lampOn) : lampOff;
                else if (live) m = (k + Mathf.FloorToInt(nowS * 20)) % 4 == 0 ? lampOn : lampOff;
                else m = k % 2 == 0 ? lampOn : lampOff;
                if (lamps[k].sharedMaterial != m) lamps[k].sharedMaterial = m;
            }
        }
    }
}
