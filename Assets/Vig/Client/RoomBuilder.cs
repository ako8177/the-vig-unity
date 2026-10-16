// Builds floor 1 from the layout data (Vig.Sim.Data.Layout.Floor1): the three rooms, their walls,
// wainscot and doorways, every table, the bar, the cage, the pillars, the decorations and the lights.
// Sim coordinates are right-handed with -Z north (the TS game's three.js frame); Unity is left-handed,
// so a sim point (x, y, z) sits at Unity (x, y, -z) and the room is not mirrored (U.P).
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Vig.Sim.Data;
using Collider = Vig.Sim.Data.Collider;

namespace Vig.Client
{
    public sealed class RoomBuilder
    {
        /// <summary>A sim point in Unity space.</summary>
        public static Vector3 U(double x, double y, double z) => new Vector3((float)x, (float)y, (float)-z);
        public static Vector3 U(V3 p) => U(p.X, p.Y, p.Z);

        public readonly Transform Root;
        /// <summary>each table's screen (the panel goes here), facing the seat</summary>
        public readonly Dictionary<string, Transform> Screens = new Dictionary<string, Transform>();
        public Transform CollectorScreen;
        /// <summary>each game table's group (its frame: felt at Placement.H, the seat toward -Z)</summary>
        public readonly Dictionary<string, Transform> Tables = new Dictionary<string, Transform>();
        public readonly List<Light> RoomLights = new List<Light>();
        int shadowed;

        /// <summary>which floor this is (1: the slice's; 2: the Gold Room, the Mirror Room, the Furnace)</summary>
        public readonly int FloorId;
        bool Two => FloorId >= 2;

        RoomBuilder(Transform parent, int floor)
        {
            FloorId = floor;
            Root = new GameObject("Floor " + floor).transform;
            Root.SetParent(parent, false);
        }

        public static RoomBuilder Build(Transform parent = null, int floor = 1)
        {
            var b = new RoomBuilder(parent, floor);
            var f = Layout.FloorOf(floor);
            b.Rooms(f);
            foreach (var t in f.Tables) b.GameTable(t);
            b.CollectorTable(f.Collector);
            b.Bar(f);
            b.Cage(f);
            foreach (var p in f.Pillars) b.Pillar(p);
            foreach (var p in f.Props) b.Prop(p);
            b.Entrance(f);
            b.Probes(f);
            return b;
        }

        // ---------------------------------------------------------------- helpers

        GameObject Part(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Transform parent = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : Root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        Transform Group(string name, Vector3 pos, float yawDeg = 0, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent ? parent : Root, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yawDeg, 0);
            return t;
        }

        Light AddLight(string name, LightType type, Color color, float intensity, float range, Vector3 pos, Transform parent = null, bool shadows = false, float spotAngle = 70, Vector3? lookDir = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : Root, false);
            go.transform.localPosition = pos;
            if (lookDir.HasValue) go.transform.rotation = Quaternion.LookRotation(lookDir.Value);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = spotAngle; l.innerSpotAngle = spotAngle * 0.55f; }
            // shadows on a budget: the table pendants and the back room's bulbs
            if (shadows && shadowed < 14)
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.85f;
                l.shadowBias = 0.02f;
                l.shadowNormalBias = 0.3f;
                shadowed++;
            }
            else l.shadows = LightShadows.None;
            RoomLights.Add(l);
            return l;
        }

        static Mesh Box(float x, float y, float z, float r = 0.02f, float uv = 1) => MeshKit.RoundedBox(x, y, z, r, 3, uv);

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!meshes.TryGetValue(key, out var m) || !m) meshes[key] = m = make();
            return m;
        }

        static Vector2 P(float r, float y) => new Vector2(r, y);

        /// <summary>Small bulbs every `spacing` m along a closed path, as one mesh.</summary>
        static Mesh Bulbs(List<Vector3> path, float spacing, float r)
        {
            var bulb = Cached("bulbDot" + r, () => MeshKit.Lathe(new[] { P(0, -r), P(r * 0.7f, -r * 0.7f), P(r, 0), P(r * 0.7f, r * 0.7f), P(0, r) }, 8));
            var parts = new List<CombineInstance>();
            float carry = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var a = path[i]; var b = path[(i + 1) % path.Count];
                float len = Vector3.Distance(a, b);
                if (len < 1e-5f) continue;
                float t = carry;
                for (; t < len; t += spacing)
                    parts.Add(new CombineInstance { mesh = bulb, transform = Matrix4x4.Translate(Vector3.Lerp(a, b, t / len)) });
                carry = t - len;
            }
            var m = new Mesh { name = "Bulbs" };
            m.CombineMeshes(parts.ToArray(), true, true);
            return m;
        }

        // ---------------------------------------------------------------- rooms

        void Rooms(Floor f)
        {
            foreach (var r in f.Rooms)
            {
                var g = Group(r.Name, Vector3.zero);
                // the inner faces: the room's rect, less half a wall where a wall stands on its edge
                float x0 = (float)(r.X0 <= -13.9 ? -13.8 : r.X0), x1 = (float)(r.X1 >= 13.9 ? 13.8 : r.X1);
                float z0 = (float)(r.Z0 <= -15.9 ? -15.8 : r.Z0 == -2 ? -1.8 : r.Z0), z1 = (float)(r.Z1 >= 17.9 ? 17.8 : r.Z1 == -2 ? -2.2 : r.Z1);
                float w = x1 - x0, d = z1 - z0, h = (float)r.Ceiling;
                var center = U((x0 + x1) / 2, 0, (z0 + z1) / 2);
                Material floorMat = Two ? (r.Id == "floor" ? Look.CarpetDeco : r.Id == "lounge" ? Look.FloorMirror : Look.FloorIron)
                    : r.Id == "floor" ? Look.CarpetFloor : r.Id == "lounge" ? Look.CarpetLounge : Look.Planks;
                Material wallMat = Two ? (r.Id == "floor" ? Look.WallDeco : r.Id == "lounge" ? Look.WallMirror : Look.WallBrick)
                    : r.Id == "floor" ? Look.WallFloor : r.Id == "lounge" ? Look.WallLounge : Look.WallBack;
                float tile = r.Id == "floor" ? 1 / 3.2f : r.Id == "lounge" ? 1 / 2.4f : 1 / 3.6f;
                Part("floor", MeshKit.RoundedBox(w, 0.1f, d, 0.001f, 1, tile), floorMat, center + Vector3.down * 0.05f, null, g, false);
                Part("ceiling", MeshKit.RoundedBox(w, 0.1f, d, 0.001f, 1, r.Id == "floor" ? 1 / 2.5f : 1), r.Id == "floor" ? Look.CeilingFloor : Look.Ceiling, center + Vector3.up * (h + 0.05f), null, g, false);
                // walls: each side as runs between the doorways that cut it
                WallRuns(g, x0, x1, z0, true, h, wallMat, r, f.Doorways, -1);
                WallRuns(g, x0, x1, z1, true, h, wallMat, r, f.Doorways, 1);
                WallRuns(g, z0, z1, x0, false, h, wallMat, r, f.Doorways, -1);
                WallRuns(g, z0, z1, x1, false, h, wallMat, r, f.Doorways, 1);
                RoomLighting(g, r, x0, x1, z0, z1, h);
            }
            // the walls' thickness, and the lintels over the arches
            foreach (var wb in f.WallBoxes.Boxes)
            {
                float bw = (float)(wb.X1 - wb.X0), bd = (float)(wb.Z1 - wb.Z0);
                Part("wall", MeshKit.RoundedBox(bw, (float)wb.H, bd, 0.001f, 1), Look.Ceiling, U((wb.X0 + wb.X1) / 2, wb.H / 2, (wb.Z0 + wb.Z1) / 2), null, null, true);
            }
            foreach (var dw in f.Doorways) Arch(dw);
        }

        /// <summary>One side of a room: wall skin, wainscot, chair rail, crown, in runs between doorways.</summary>
        void WallRuns(Transform g, float a0, float a1, float at, bool alongX, float h, Material wallMat, Room r, Doorway[] doors, int side)
        {
            var cuts = new List<(float a, float b, float h)>();
            foreach (var dw in doors)
                if (alongX && dw.Along == "x" && Mathf.Abs((float)dw.Z - at) < 0.5f)
                    cuts.Add(((float)(dw.X - dw.W / 2), (float)(dw.X + dw.W / 2), (float)dw.H));
            cuts = cuts.Where(c => c.b > a0 && c.a < a1).OrderBy(c => c.a).ToList();
            var runs = new List<(float a, float b, float y0)>();
            float pos = a0;
            foreach (var c in cuts)
            {
                if (c.a > pos) runs.Add((pos, c.a, 0));
                runs.Add((c.a, c.b, c.h));
                pos = c.b;
            }
            if (a1 > pos) runs.Add((pos, a1, 0));
            // the inside of the wall faces into the room
            float inward = alongX ? (side > 0 ? -1 : 1) : (side > 0 ? -1 : 1);
            foreach (var run in runs)
            {
                float len = run.b - run.a, mid = (run.a + run.b) / 2, y0 = run.y0, hh = h - y0;
                if (len < 0.01f || hh < 0.01f) continue;
                Vector3 c = alongX ? U(mid, 0, at) : U(at, 0, mid);
                // in Unity, sim +z is -z: the inward offset flips for walls along X
                Vector3 inv = alongX ? new Vector3(0, 0, -inward) : new Vector3(inward, 0, 0);
                Vector3 size(float t, float hgt) => alongX ? new Vector3(len, hgt, t) : new Vector3(t, hgt, len);
                Mesh skin(float t, float hgt, float uv) { var s = size(t, hgt); return MeshKit.RoundedBox(s.x, s.y, s.z, 0.004f, 1, uv); }
                Part("wall skin", skin(0.02f, hh, 1 / 1.6f), wallMat, c + inv * 0.01f + Vector3.up * (y0 + hh / 2), null, g, false);
                if (y0 > 0) continue;
                // wainscot to 1.05 m, a chair rail, a crown under the ceiling
                Part("wainscot", skin(0.06f, 1.05f, 1 / 1.2f), r.Id == "back" ? Look.DarkWood : Look.Wainscot, c + inv * 0.03f + Vector3.up * 0.525f, null, g, false);
                Part("chair rail", skin(0.09f, 0.07f, 1), r.Id == "back" ? Look.DarkWood : Look.BrassMat, c + inv * 0.045f + Vector3.up * 1.07f, null, g, false);
                Part("skirting", skin(0.09f, 0.14f, 1), Look.BlackGloss, c + inv * 0.045f + Vector3.up * 0.07f, null, g, false);
                Part("crown", skin(0.14f, 0.16f, 1), r.Id == "floor" ? Look.BrassMat : Look.DarkWood, c + inv * 0.07f + Vector3.up * (h - 0.08f), null, g, false);
            }
        }

        void RoomLighting(Transform g, Room r, float x0, float x1, float z0, float z1, float h)
        {
            // a cove of warm light round the ceiling's edge: the room's ambient, made by light, not a fill
            Color cove = Two ? (r.Id == "floor" ? new Color(1f, 0.75f, 0.35f) : r.Id == "lounge" ? new Color(0.3f, 0.85f, 0.9f) : new Color(1f, 0.3f, 0.08f))
                : r.Id == "floor" ? new Color(1f, 0.62f, 0.38f) : r.Id == "lounge" ? new Color(0.78f, 0.32f, 1f) : new Color(1f, 0.35f, 0.18f);
            float inten = r.Id == "floor" ? 7f : r.Id == "lounge" ? 6f : 3f;
            float step = r.Id == "floor" ? 7f : 6.5f;
            for (float x = x0 + step / 2; x < x1; x += step)
            {
                AddLight("cove", LightType.Point, cove, inten, 7.5f, U(x, h - 0.4f, z0 + 0.6f), g);
                AddLight("cove", LightType.Point, cove, inten, 7.5f, U(x, h - 0.4f, z1 - 0.6f), g);
            }
            // recessed downlights: warm pools on the carpet between the tables (dimmer the deeper you go)
            float dl = r.Id == "floor" ? 5f : 4.6f;
            float dlI = r.Id == "floor" ? 26f : r.Id == "lounge" ? 15f : 7f;
            Color dlC = r.Id == "back" ? new Color(1f, 0.6f, 0.38f) : new Color(1f, 0.8f, 0.6f);
            for (float x = x0 + dl * 0.75f; x < x1 - dl * 0.4f; x += dl)
                for (float z = z0 + dl * 0.75f; z < z1 - dl * 0.4f; z += dl)
                {
                    AddLight("downlight", LightType.Spot, dlC, dlI, h + 1.5f, U(x, h - 0.05f, z), g, false, 75, Vector3.down);
                    Part("downlight can", Cached("can", () => MeshKit.Lathe(new[] { P(0.1f, -0.02f), P(0.1f, 0.0f), P(0.07f, 0.0f), P(0.07f, -0.01f) }, 18, false, false)), Look.BrassMat, U(x, h, z), null, g, false);
                    Part("downlight lens", Cached("lens", () => MeshKit.Lathe(new[] { P(0, -0.012f), P(0.07f, -0.012f) }, 18, false, false)), Look.Bulb, U(x, h, z), null, g, false);
                }
            // the neon strip that makes the cove: a tube round the ceiling
            var ring = new List<Vector3> { U(x0 + 0.15f, h - 0.22f, z0 + 0.15f), U(x1 - 0.15f, h - 0.22f, z0 + 0.15f), U(x1 - 0.15f, h - 0.22f, z1 - 0.15f), U(x0 + 0.15f, h - 0.22f, z1 - 0.15f) };
            var strip = new List<Vector3>();
            for (int i = 0; i < 4; i++) { var a = ring[i]; var b = ring[(i + 1) % 4]; for (int k = 0; k < 8; k++) strip.Add(Vector3.Lerp(a, b, k / 8f)); }
            Color tube = Two ? (r.Id == "floor" ? Look.Gold : r.Id == "lounge" ? Look.Soul : new Color(1f, 0.45f, 0.1f))
                : r.Id == "floor" ? Look.PinkHi : r.Id == "lounge" ? Look.Coll : Look.High;
            Part("cove neon", MeshKit.Tube(strip, 0.018f, 6, true), Look.Neon(tube, r.Id == "back" ? 2.5f : 3.5f), Vector3.zero, null, g, false);
        }

        /// <summary>An arch between rooms: a lacquered frame, brass, and the room's name in its tier's neon.</summary>
        void Arch(Doorway dw)
        {
            var g = Group("arch " + dw.To, U(dw.X, 0, dw.Z));
            float w = (float)dw.W, h = (float)dw.H;
            Part("jamb", Box(0.18f, h, 0.56f, 0.03f), Look.Lacquer, new Vector3(-w / 2 - 0.09f, h / 2, 0), null, g);
            Part("jamb", Box(0.18f, h, 0.56f, 0.03f), Look.Lacquer, new Vector3(w / 2 + 0.09f, h / 2, 0), null, g);
            Part("head", Box(w + 0.36f, 0.22f, 0.56f, 0.03f), Look.Lacquer, new Vector3(0, h + 0.11f, 0), null, g);
            // the wall over the arch, up to the main floor's ceiling
            float top = (float)Layout.FloorOf(FloorId).Ceiling;
            Part("lintel", MeshKit.RoundedBox(w, top - h, 0.4f, 0.001f), Look.Ceiling, new Vector3(0, h + (top - h) / 2, 0), null, g);
            string tier = dw.To == "lounge" ? Vig.Sim.Data.Tier.Mid : Vig.Sim.Data.Tier.High;
            // the sign faces the main floor (south in the sim: -Z in Unity)
            Neon.Sign(g, dw.To == "lounge" ? "THE LOUNGE" : "THE BACK ROOM", Look.TierColor(tier), new Vector3(0, h + 0.62f, -0.32f), 0, 0.32f, this);
        }

        // ---------------------------------------------------------------- the game tables

        void GameTable(Placement t)
        {
            var def = Vig.Sim.Data.Tables.ById(t.Table);
            var tierColor = Look.TierColor(def.Tier);
            var g = Group(def.Name, U(t.X, 0, t.Z));
            Tables[t.Table] = g;
            float w = (float)t.W, d = (float)t.D, h = (float)t.H;
            if (t.Table == "pegdrop") PegCabinet(g, w, d, tierColor, def.Sign);
            else if (t.Cabinet) SlotCabinet(g, w, d, tierColor, true, def.Sign);
            else
            {
                var felt = t.Table == "hilo" || t.Table == "deadman" ? Look.BaizeMat : t.Table == "roulette" || t.Table == "craps" ? Look.Emerald : Look.Felt;
                FeltTable(g, w, d, h, tierColor, felt);
                // craps: the high walls the dice bounce off
                if (t.Table == "craps")
                {
                    var wall = MeshKit.RoundRect(w - 0.18f, d - 0.18f, 0.18f, 8).Select(p => new Vector3(p.x, 0, p.y)).ToList();
                    Part("craps wall", MeshKit.Tube(wall, 0.035f, 8, true), Look.Lacquer, new Vector3(0, h + 0.12f, 0), null, g);
                    Part("craps wall inner", MeshKit.Tube(wall, 0.03f, 8, true), Look.Lacquer, new Vector3(0, h + 0.06f, 0), null, g);
                }
                // Crash: a tall monitor behind the dealer's side shows the climb
                if (t.Table == "crash")
                {
                    Part("crash monitor", Box(1.7f, 1.0f, 0.08f, 0.03f), Look.BlackGloss, new Vector3(0, h + 1.1f, d / 2 + 0.05f), null, g);
                    Part("crash post", MeshKit.Lathe(new[] { P(0.06f, 0), P(0.06f, h + 0.62f) }, 16, false, true), Look.Chrome, new Vector3(0, 0, d / 2 + 0.12f), null, g);
                }
                // the stool where you sit, at the stand point
                Stool(Group("stool", U(t.Stand.X, 0, t.Stand.Z) - g.position, 0, g));
                // the sign hangs behind the dealer's side, facing the seat
                Neon.Sign(g, def.Sign, tierColor, new Vector3(0, (float)t.SignY + (def.Tier == "low" ? 0 : 0.12f), d / 2 + 0.35f), 0, 0.42f, this, frame: true);
                if (t.Table == "crash") AddLight("crash wash", LightType.Spot, new Color(1f, 0.6f, 0.4f), 14f, 6f, new Vector3(0, 3.4f, -0.6f), g, true, 70, Vector3.down);
                else Pendant(g, new Vector3(0, def.Tier == "high" ? 2.05f : def.Tier == "mid" ? 2.1f : 2.7f, 0), def.Tier);
            }
            if (t.Screen != null)
            {
                var sc = t.Screen;
                var st = new GameObject("screen " + t.Table).transform;
                st.SetParent(Root, false);
                st.position = U(sc.X, sc.Y, sc.Z);
                st.rotation = Quaternion.Euler(Mathf.Rad2Deg * (float)sc.Tilt, 0, 0);
                Screens[t.Table] = st;
                if (!t.Cabinet)
                {
                    // the dealer's display: a brass-framed slab on a post
                    Part("display frame", Box((float)sc.W + 0.08f, (float)sc.H + 0.08f, 0.05f, 0.02f), Look.BlackGloss, new Vector3(0, 0, 0.03f), null, st);
                    Part("display bezel", MeshKit.Tube(MeshKit.RoundRect((float)sc.W + 0.06f, (float)sc.H + 0.06f, 0.03f), 0.008f, 6, true), Look.BrassMat, new Vector3(0, 0, 0.0f), null, st);
                }
            }
        }

        /// <summary>A felt table: pedestals, a lacquered apron with the tier's neon under the rail, felt, a padded leather rail.</summary>
        void FeltTable(Transform g, float w, float d, float h, Color tier, Material felt)
        {
            var ped = Cached("pedestal", () => MeshKit.Lathe(new[] { P(0.34f, 0), P(0.34f, 0.04f), P(0.3f, 0.07f), P(0.1f, 0.12f), P(0.075f, 0.2f), P(0.075f, 0.62f), P(0.11f, 0.68f), P(0.22f, 0.72f), P(0.22f, 0.76f) }, 28));
            if (w > 1.9f)
            {
                Part("pedestal", ped, Look.BrassMat, new Vector3(-w * 0.26f, 0, 0), null, g);
                Part("pedestal", ped, Look.BrassMat, new Vector3(w * 0.26f, 0, 0), null, g);
            }
            else Part("pedestal", ped, Look.BrassMat, Vector3.zero, null, g);
            Part("apron", Box(w - 0.06f, 0.1f, d - 0.06f, 0.04f), Look.Lacquer, new Vector3(0, h - 0.1f, 0), null, g);
            Part("top", Box(w, 0.05f, d, 0.02f), Look.Lacquer, new Vector3(0, h - 0.03f, 0), null, g);
            Part("felt", MeshKit.RoundedBox(w - 0.2f, 0.02f, d - 0.2f, 0.008f, 2, 2), felt, new Vector3(0, h - 0.0f, 0), null, g, false);
            var rail = MeshKit.RoundRect(w - 0.1f, d - 0.1f, 0.22f, 8).Select(p => new Vector3(p.x, 0, p.y)).ToList();
            Part("rail", MeshKit.Tube(rail, 0.05f, 12, true), Look.Leather, new Vector3(0, h + 0.03f, 0), null, g);
            var trim = MeshKit.RoundRect(w - 0.02f, d - 0.02f, 0.24f, 8).Select(p => new Vector3(p.x, 0, p.y)).ToList();
            Part("tier neon", MeshKit.Tube(trim, 0.009f, 6, true), Look.Neon(tier, 5f), new Vector3(0, h - 0.13f, 0), null, g, false);
            // the neon lights the carpet under the table
            AddLight("tier glow", LightType.Point, tier, 0.6f, 1.6f, new Vector3(0, 0.35f, 0), g);
        }

        /// <summary>A lamp hung over a table: a brass shade, a warm spot down onto the felt (with shadows).</summary>
        void Pendant(Transform g, Vector3 at, string tier)
        {
            Part("cord", MeshKit.Tube(new[] { at, at + Vector3.up * 2.2f }, 0.006f, 5), Look.BlackGloss, Vector3.zero, null, g, false);
            var shade = Cached("shade", () => MeshKit.Lathe(new[] { P(0.03f, 0.24f), P(0.06f, 0.22f), P(0.2f, 0.06f), P(0.34f, 0), P(0.335f, -0.01f), P(0.19f, 0.05f), P(0.055f, 0.2f) }, 32, false, false));
            Part("shade", shade, Look.BrassMat, at, null, g);
            Part("bulb", Cached("bulbMesh", () => MeshKit.Lathe(new[] { P(0, -0.07f), P(0.04f, -0.05f), P(0.05f, 0), P(0.02f, 0.05f), P(0.02f, 0.08f) }, 16)), Look.Bulb, at + Vector3.up * 0.02f, null, g, false);
            Color warm = tier == "high" ? new Color(1f, 0.62f, 0.38f) : new Color(1f, 0.82f, 0.62f);
            AddLight("pendant", LightType.Spot, warm, tier == "high" ? 16f : 20f, 6f, at - Vector3.up * 0.05f, g, true, 92, Vector3.down);
        }

        void Stool(Transform g)
        {
            var m = Cached("stool", () => MeshKit.Lathe(new[] { P(0.22f, 0), P(0.22f, 0.02f), P(0.04f, 0.05f), P(0.035f, 0.6f), P(0.08f, 0.62f) }, 24));
            Part("stool base", m, Look.BrassMat, Vector3.zero, null, g);
            var seat = Cached("stoolSeat", () => MeshKit.Lathe(new[] { P(0, 0.62f), P(0.2f, 0.62f), P(0.21f, 0.66f), P(0.2f, 0.7f), P(0.12f, 0.72f), P(0, 0.725f) }, 28, false, false));
            Part("stool seat", seat, Look.Velvet, Vector3.zero, null, g);
            Part("foot ring", MeshKit.Tube(MeshKit.Circle(0.17f, 24, 0.28f), 0.012f, 6, true), Look.BrassMat, Vector3.zero, null, g);
        }

        /// <summary>Peg Drop's cabinet: the screen low, the board above it (the show hangs the pegs and the ball on it).</summary>
        void PegCabinet(Transform g, float w, float d, Color tier, string sign)
        {
            float bw = Mathf.Min(1.5f, w), bd = Mathf.Min(0.8f, d);
            Part("plinth", Box(bw + 0.04f, 0.32f, bd + 0.04f, 0.03f), Look.BlackGloss, new Vector3(0, 0.16f, 0), null, g);
            Part("body", Box(bw, 1.5f, bd, 0.08f), Look.Surface("pegBody", new Color(0.1f, 0.02f, 0.14f), 0.85f), new Vector3(0, 1.05f, 0), null, g);
            Part("board", Box(bw + 0.06f, 1.35f, 0.12f, 0.06f), Look.BlackGloss, new Vector3(0, 2.45f, -bd / 2 + 0.1f), null, g);
            Part("board face", MeshKit.Quad(bw - 0.1f, 1.22f), Look.Screen("pegFace", new Color(0.02f, 0.01f, 0.05f)), new Vector3(0, 2.45f, -bd / 2 + 0.035f), null, g, false);
            Part("board neon", MeshKit.Tube(MeshKit.RoundRect(bw, 1.3f, 0.12f), 0.011f, 6, true), Look.Neon(tier, 4f), new Vector3(0, 2.45f, -bd / 2 + 0.03f), null, g, false);
            Part("chaser bulbs", Bulbs(MeshKit.RoundRect(bw - 0.1f, 0.86f, 0.08f), 0.075f, 0.011f), Look.Glow("chaser", new Color(1f, 0.78f, 0.45f), 2.6f), new Vector3(0, 1.3f, -bd / 2 - 0.006f), null, g, false);
            if (sign != null) Neon.Sign(g, sign, tier, new Vector3(0, 3.33f, -bd / 2 + 0.05f), 0, 0.22f, this, frame: true);
        }

        /// <summary>A slot cabinet facing south (toward -Z in Unity): plinth, gloss body, the screen, a topper in neon, a lever.</summary>
        void SlotCabinet(Transform g, float w, float d, Color tier, bool playable, string sign)
        {
            float bw = Mathf.Min(1.4f, w), bd = Mathf.Min(0.85f, d);
            Part("plinth", Box(bw + 0.04f, 0.32f, bd + 0.04f, 0.03f), Look.BlackGloss, new Vector3(0, 0.16f, 0), null, g);
            // the playable machine stands taller: its reels turn in a window above the screen (BanditShow)
            float top = playable ? 2.3f : 1.9f, topper = top + 0.25f;
            if (playable)
            {
                var red = Look.Surface("cabRed", new Color(0.35f, 0.02f, 0.05f), 0.85f);
                float wy0 = BanditShow.WinY - BanditShow.WinH / 2, wy1 = BanditShow.WinY + BanditShow.WinH / 2, side = (bw - BanditShow.WinW) / 2;
                Part("body", Box(bw, wy0 - 0.3f, bd, 0.03f), red, new Vector3(0, (0.3f + wy0) / 2, 0), null, g);
                Part("body top", Box(bw, top - wy1, bd, 0.03f), red, new Vector3(0, (wy1 + top) / 2, 0), null, g);
                Part("body side", Box(side, BanditShow.WinH + 0.02f, bd, 0.01f), red, new Vector3(-(BanditShow.WinW + side) / 2, BanditShow.WinY, 0), null, g);
                Part("body side", Box(side, BanditShow.WinH + 0.02f, bd, 0.01f), red, new Vector3((BanditShow.WinW + side) / 2, BanditShow.WinY, 0), null, g);
                Part("reel box", Box(BanditShow.WinW, BanditShow.WinH, 0.04f, 0.005f), Look.BlackGloss, new Vector3(0, BanditShow.WinY, -bd / 2 + 0.47f), null, g);
                Part("reel trim", MeshKit.Tube(MeshKit.RoundRect(BanditShow.WinW + 0.02f, BanditShow.WinH + 0.02f, 0.03f), 0.012f, 6, true), Look.Chrome, new Vector3(0, BanditShow.WinY, -bd / 2 - 0.006f), null, g);
                Part("reel bulbs", Bulbs(MeshKit.RoundRect(BanditShow.WinW + 0.09f, BanditShow.WinH + 0.08f, 0.05f), 0.075f, 0.011f), Look.Glow("chaser", new Color(1f, 0.78f, 0.45f), 2.6f), new Vector3(0, BanditShow.WinY, -bd / 2 - 0.006f), null, g, false);
            }
            else Part("body", Box(bw, 1.6f, bd, 0.08f), Look.Surface("cabDark", new Color(0.08f, 0.05f, 0.1f), 0.85f), new Vector3(0, 1.1f, 0), null, g);
            // the button deck, sloping toward the player
            Part("deck", Box(bw - 0.1f, 0.06f, 0.32f, 0.02f), Look.Chrome, new Vector3(0, 0.88f, -bd / 2 - 0.08f), Quaternion.Euler(-12, 0, 0), g);
            for (int i = -2; i <= 2; i++)
                Part("button", Cached("btn", () => MeshKit.Lathe(new[] { P(0.035f, 0), P(0.035f, 0.02f), P(0.028f, 0.03f), P(0, 0.032f) }, 14)),
                    i == 0 ? Look.Neon(tier, 2f) : Look.Glow("btnGlow", new Color(1f, 0.75f, 0.4f), 1.2f),
                    new Vector3(i * 0.18f, 0.915f, -bd / 2 - 0.1f), Quaternion.Euler(-12, 0, 0), g, false);
            // the topper: an arched crown with the sign
            Part("topper", Box(bw + 0.02f, 0.5f, bd * 0.7f, 0.12f), Look.BlackGloss, new Vector3(0, topper, 0.05f), null, g);
            Part("topper neon", MeshKit.Tube(MeshKit.RoundRect(bw - 0.06f, 0.42f, 0.14f), 0.01f, 6, true), Look.Neon(tier, 4f), new Vector3(0, topper, 0.05f - bd * 0.35f - 0.01f), null, g, false);
            // chaser bulbs round the glass, a chrome trim round the face
            Part("chaser bulbs", Bulbs(MeshKit.RoundRect(bw - 0.1f, 0.86f, 0.08f), 0.075f, 0.011f), Look.Glow("chaser", new Color(1f, 0.78f, 0.45f), 2.6f), new Vector3(0, 1.3f, -bd / 2 - 0.006f), null, g, false);
            Part("trim", MeshKit.Tube(MeshKit.RoundRect(bw - 0.02f, top - 0.32f, 0.08f), 0.012f, 6, true), Look.Chrome, new Vector3(0, (0.3f + top) / 2, -bd / 2 - 0.004f), null, g);
            if (sign != null) Neon.Text(g, sign, tier, new Vector3(0, topper, 0.05f - bd * 0.35f - 0.03f), 0.2f);
            // the lever on the right as you face it (west in Unity: the player looks +Z, their right is +X)
            Part("lever", MeshKit.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0.02f, 0.45f, 0) }, 0.015f, 8), Look.Chrome, new Vector3(bw / 2 + 0.05f, 1.15f, -0.1f), null, g);
            Part("knob", Cached("knob", () => MeshKit.Lathe(new[] { P(0, -0.05f), P(0.04f, -0.035f), P(0.05f, 0), P(0.04f, 0.035f), P(0, 0.05f) }, 16)), Look.Neon(Look.Blood, 1.2f), new Vector3(bw / 2 + 0.07f, 1.62f, -0.1f), null, g);
            if (!playable)
            {
                // a decorative machine: dark glass and three reels of symbols lit from behind
                Part("glass", MeshKit.Quad(bw - 0.2f, 0.72f), Look.Screen("slotGlass", new Color(0.05f, 0.03f, 0.07f)), new Vector3(0, 1.3f, -bd / 2 - 0.012f), null, g, false);
                int seed = Mathf.Abs(g.position.GetHashCode());
                string[] syms = { "7", "BAR", TableScreen.Glyph('\u2605', "*"), "7" };
                for (int k = -1; k <= 1; k++)
                {
                    Part("reel", MeshKit.Quad((bw - 0.3f) / 3.2f, 0.58f), Look.Surface("reelFace", new Color(0.9f, 0.86f, 0.78f), 0.4f, 0, emission: new Color(0.55f, 0.5f, 0.42f)),
                        new Vector3(k * (bw - 0.3f) / 3f, 1.3f, -bd / 2 - 0.014f), null, g, false);
                    string sym = syms[(seed >> ((k + 1) * 3)) % syms.Length];
                    var tx = Neon.Text(g, sym, sym == "7" ? Look.Blood : sym == "BAR" ? Look.Ink : Look.Marker, new Vector3(k * (bw - 0.3f) / 3f, 1.3f, -bd / 2 - 0.018f), sym == "BAR" ? 0.09f : 0.16f, 0, 0.9f);
                    tx.fontMaterial.DisableKeyword("GLOW_ON");
                }
            }
        }

        // ---------------------------------------------------------------- the Collector's table

        void CollectorTable(CollectorTable c)
        {
            var g = Group("The Collector", U(c.X, 0, c.Z));
            float r = (float)c.R, h = (float)c.H;
            Part("pedestal", MeshKit.Lathe(new[] { P(0.6f, 0), P(0.6f, 0.05f), P(0.5f, 0.1f), P(0.16f, 0.2f), P(0.13f, 0.6f), P(0.3f, 0.72f), P(0.3f, h - 0.08f) }, 40), Look.BlackGloss, Vector3.zero, null, g);
            Part("top", MeshKit.Lathe(new[] { P(0, h - 0.08f), P(r - 0.04f, h - 0.08f), P(r, h - 0.05f), P(r, h - 0.01f), P(r - 0.04f, h), P(0, h) }, 64), Look.DarkWood, Vector3.zero, null, g);
            Part("felt", MeshKit.Lathe(new[] { P(0, h + 0.005f), P(r - 0.16f, h + 0.005f) }, 64, false, true), Look.Surface("collFelt", new Color(0.09f, 0.04f, 0.12f), 0.05f, 0, Textures.FeltNoise(), Textures.FeltNormal(), 0.4f), Vector3.zero, null, g, false);
            Part("rail", MeshKit.Tube(MeshKit.Circle(r - 0.08f, 64, 0), 0.055f, 12, true), Look.Leather, new Vector3(0, h + 0.03f, 0), null, g);
            Part("violet neon", MeshKit.Tube(MeshKit.Circle(r + 0.005f, 72, 0), 0.01f, 6, true), Look.Neon(Look.Coll, 6f), new Vector3(0, h - 0.06f, 0), null, g, false);
            AddLight("collector glow", LightType.Point, Look.Coll, 1.2f, 2.5f, new Vector3(0, 0.3f, 0), g);
            // his chair: a high back of purple velvet in a black frame, across the table (north of it)
            var ch = Layout.FloorOf(FloorId).Collector.Chair;
            var chair = Group("his chair", U((ch.X0 + ch.X1) / 2, 0, (ch.Z0 + ch.Z1) / 2) - g.position, 180, g);
            Part("seat", Box(0.9f, 0.14f, 0.7f, 0.05f), Look.VelvetPurple, new Vector3(0, 0.5f, 0.1f), null, chair);
            Part("back", Box(1.0f, 2.0f, 0.18f, 0.07f), Look.VelvetPurple, new Vector3(0, 1.55f, -0.18f), null, chair);
            Part("frame", MeshKit.Tube(MeshKit.RoundRect(1.06f, 2.06f, 0.4f, 10), 0.035f, 8, true), Look.BlackGloss, new Vector3(0, 1.55f, -0.1f), Quaternion.Euler(0, 180, 0), chair);
            Part("crest", MeshKit.Tube(MeshKit.RoundRect(0.22f, 0.22f, 0.11f, 10), 0.008f, 6, true), Look.Neon(Look.Coll, 2.5f), new Vector3(0, 2.35f, -0.08f), Quaternion.Euler(0, 180, 0), chair, false);
            foreach (var sx in new[] { -0.42f, 0.42f })
            {
                Part("arm", Box(0.12f, 0.1f, 0.7f, 0.04f), Look.BlackGloss, new Vector3(sx, 0.8f, 0.08f), null, chair);
                Part("leg", Box(0.08f, 0.5f, 0.08f, 0.02f), Look.BlackGloss, new Vector3(sx, 0.25f, 0.35f), null, chair);
            }
            Neon.Sign(g, Vig.Sim.Data.Tables.CollectorSign, Look.Coll, new Vector3(0, (float)c.SignY, 1.4f), 0, 0.42f, this, frame: true);
            Pendant(g, new Vector3(0, 2.2f, 0), "coll");
            var sc = c.Screen;
            var st = new GameObject("screen collector").transform;
            st.SetParent(Root, false);
            st.position = U(sc.X, sc.Y, sc.Z);
            st.rotation = Quaternion.Euler(Mathf.Rad2Deg * (float)sc.Tilt, 0, 0);
            CollectorScreen = st;
            Part("display frame", Box((float)sc.W + 0.08f, (float)sc.H + 0.08f, 0.05f, 0.02f), Look.BlackGloss, new Vector3(0, 0, 0.03f), null, st);
            Stool(Group("stool", U(c.Stand.X, 0, c.Stand.Z) - g.position, 0, g));
        }

        // ---------------------------------------------------------------- the bar and the cage

        void Bar(Floor f)
        {
            var b = f.Bar;
            var g = Group("The Bar", Vector3.zero);
            float z0 = (float)b.Z0, z1 = (float)b.Z1, len = z1 - z0;
            float front = -12.45f;
            // the counter: a lacquered front, a marble top, a brass foot rail
            Part("counter", Box(0.5f, 1.02f, len, 0.04f), Look.Lacquer, U(front, 0.51, (z0 + z1) / 2), null, g);
            Part("counter top", Box(0.7f, 0.06f, len + 0.1f, 0.025f), Look.Marble, U(front + 0.05f, 1.07, (z0 + z1) / 2), null, g);
            Part("foot rail", MeshKit.Tube(new[] { U(front + 0.36f, 0.18, z0 + 0.1), U(front + 0.36f, 0.18, z1 - 0.1) }, 0.025f, 10), Look.BrassMat, Vector3.zero, null, g);
            Part("under glow", MeshKit.Tube(new[] { U(front + 0.27f, 0.98, z0 + 0.05), U(front + 0.27f, 0.98, z1 - 0.05) }, 0.008f, 6), Look.Neon(Look.PinkHi, 4f), Vector3.zero, null, g, false);
            // the back bar against the west wall: shelves of bottles in front of a smoked mirror
            Part("back counter", Box(0.45f, 0.95f, len, 0.03f), Look.DarkWood, U(-13.55, 0.475, (z0 + z1) / 2), null, g);
            Part("mirror", Box(0.03f, 1.5f, len - 0.4f, 0.01f), Look.Surface("mirror", new Color(0.25f, 0.2f, 0.22f), 0.97f, 1), U(-13.75, 1.95, (z0 + z1) / 2), null, g, false);
            var bottle = Cached("bottle", () => MeshKit.Lathe(new[] { P(0, 0), P(0.035f, 0), P(0.038f, 0.02f), P(0.038f, 0.18f), P(0.03f, 0.22f), P(0.012f, 0.26f), P(0.012f, 0.32f), P(0, 0.325f) }, 14));
            Color[] glass = { new Color(0.55f, 0.25f, 0.05f), new Color(0.1f, 0.35f, 0.12f), new Color(0.6f, 0.55f, 0.3f), new Color(0.35f, 0.05f, 0.12f), new Color(0.15f, 0.2f, 0.45f) };
            int k = 0;
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 1.25f + shelf * 0.45f;
                Part("shelf", Box(0.26f, 0.03f, len - 0.4f, 0.01f), Look.Glass, U(-13.62, y, (z0 + z1) / 2), null, g, false);
                Part("shelf light", MeshKit.Tube(new[] { U(-13.52, y - 0.025, z0 + 0.3), U(-13.52, y - 0.025, z1 - 0.3) }, 0.006f, 5), Look.Neon(new Color(1f, 0.7f, 0.4f), 3f), Vector3.zero, null, g, false);
                for (float z = z0 + 0.4f; z < z1 - 0.35f; z += 0.16f + 0.05f * ((k * 7) % 3))
                {
                    var c = glass[(k * 3 + shelf) % glass.Length];
                    Part("bottle", bottle, Look.Surface("bottle" + ((k * 3 + shelf) % glass.Length), c, 0.95f, 0, emission: c * 0.25f), U(-13.62, y + 0.015, z), null, g, false);
                    k++;
                }
            }
            AddLight("back bar", LightType.Point, new Color(1f, 0.66f, 0.4f), 3.5f, 4.5f, U(-13.2, 2.2, (z0 + z1) / 2), g);
            AddLight("bar", LightType.Point, new Color(1f, 0.55f, 0.6f), 3f, 5f, U(-12.0, 2.6, (z0 + z1) / 2 - 2.5), g);
            AddLight("bar", LightType.Point, new Color(1f, 0.55f, 0.6f), 3f, 5f, U(-12.0, 2.6, (z0 + z1) / 2 + 2.5), g);
            Neon.Sign(g, "BAR", Look.PinkHi, U(-13.7, 3.55, (z0 + z1) / 2), -90, 0.6f, this, frame: true);
        }

        void Cage(Floor f)
        {
            var c = f.Cage;
            var g = Group("The Cage", U((c.X0 + c.X1) / 2, 0, (c.Z0 + c.Z1) / 2));
            float w = (float)(c.X1 - c.X0), d = (float)(c.Z1 - c.Z0), h = (float)c.H;
            Part("lower", Box(w, 1.1f, d, 0.04f), Look.Lacquer, new Vector3(0, 0.55f, 0), null, g);
            Part("ledge", Box(w + 0.16f, 0.06f, d + 0.1f, 0.02f), Look.Marble, new Vector3(-0.05f, 1.13f, 0), null, g);
            Part("roof", Box(w + 0.1f, 0.25f, d + 0.1f, 0.04f), Look.Lacquer, new Vector3(0, h - 0.12f, 0), null, g);
            // brass bars up the front (facing west, toward the floor) and the sides
            for (float z = -d / 2 + 0.1f; z <= d / 2 - 0.09f; z += 0.12f)
                Part("bar", MeshKit.Tube(new[] { new Vector3(-w / 2, 1.16f, z), new Vector3(-w / 2, h - 0.24f, z) }, 0.012f, 6), Look.BrassMat, Vector3.zero, null, g);
            Part("window", Box(0.06f, 0.42f, 0.8f, 0.03f), Look.Surface("cageWindow", Color.black, 0.9f, 0, emission: new Color(0.9f, 0.7f, 0.4f) * 0.6f), new Vector3(-w / 2 + 0.03f, 1.4f, 0), null, g, false);
            AddLight("cage", LightType.Point, new Color(1f, 0.78f, 0.5f), 3f, 4f, new Vector3(0, 2.2f, 0), g);
            Neon.Sign(g, "CAGE", Look.Marker, new Vector3(-w / 2 - 0.05f, h + 0.45f, 0), 90, 0.5f, this, frame: true);
        }

        void Pillar(Collider p)
        {
            var g = Group("pillar", U(p.X, 0, p.Z));
            float r = (float)p.R, h = (float)p.H;
            Part("column", MeshKit.Lathe(new[] { P(r + 0.12f, 0), P(r + 0.12f, 0.12f), P(r + 0.06f, 0.18f), P(r, 0.24f), P(r, h - 0.4f), P(r + 0.05f, h - 0.32f), P(r + 0.14f, h - 0.18f), P(r + 0.16f, h) }, 40), Look.MarbleLight, Vector3.zero, null, g);
            Part("band", MeshKit.Tube(MeshKit.Circle(r + 0.012f, 40, 0), 0.02f, 8, true), Look.BrassMat, new Vector3(0, 1.07f, 0), null, g);
            Part("band", MeshKit.Tube(MeshKit.Circle(r + 0.012f, 40, 0), 0.015f, 8, true), Look.BrassMat, new Vector3(0, h - 0.42f, 0), null, g);
            // uplights at the foot, washing the column
            AddLight("uplight", LightType.Spot, new Color(1f, 0.7f, 0.45f), 4f, 5f, new Vector3(0, 0.25f, -r - 0.25f), g, false, 40, (Vector3.up * 3 + Vector3.forward * 0.35f).normalized);
        }

        // ---------------------------------------------------------------- the decorations

        void Prop(Prop p)
        {
            var at = U(p.X, 0, p.Z);
            switch (p.Kind)
            {
                case "slot":
                {
                    // the Bandit's own spot in the west bank is the playable cabinet
                    if (Mathf.Abs((float)p.X + 6.9f) < 0.01f && Mathf.Abs((float)p.Z - 11) < 0.01f) return;
                    var g = Group("slot", at);
                    var hue = new[] { Look.PinkHi, Look.Marker, Look.Soul, Look.Gold, Look.Coll }[Mathf.Abs((int)(p.X * 7)) % 5];
                    SlotCabinet(g, (float)(p.W ?? 1.4), (float)(p.D ?? 0.9), hue, false, null);
                    return;
                }
                case "carousel":
                {
                    var g = Group("carousel", at);
                    float r = (float)(p.W ?? 1.5);
                    Part("plinth", MeshKit.Lathe(new[] { P(r + 0.75f, 0), P(r + 0.75f, 0.12f), P(r + 0.6f, 0.16f), P(0, 0.16f) }, 64), Look.BlackGloss, Vector3.zero, null, g);
                    Part("tower", MeshKit.Lathe(new[] { P(0.55f, 0), P(0.55f, 2.4f), P(0.7f, 2.5f), P(0.7f, 3.3f), P(0.4f, 3.5f), P(0, 3.55f) }, 40), Look.Lacquer, Vector3.zero, null, g);
                    Part("jackpot", MeshKit.Lathe(new[] { P(0.72f, 2.6f), P(0.72f, 3.2f) }, 48, false, false), Look.Screen("jackpot", new Color(1f, 0.6f, 0.15f) * 2.2f), Vector3.zero, null, g, false);
                    Part("jackpot ring", MeshKit.Tube(MeshKit.Circle(0.74f, 48, 0), 0.015f, 6, true), Look.Neon(Look.Gold, 6f), new Vector3(0, 2.58f, 0), null, g, false);
                    Part("jackpot ring", MeshKit.Tube(MeshKit.Circle(0.74f, 48, 0), 0.015f, 6, true), Look.Neon(Look.Gold, 6f), new Vector3(0, 3.22f, 0), null, g, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * 360f;
                        var m = Group("machine", Quaternion.Euler(0, a, 0) * new Vector3(0, 0, r + 0.2f), a + 180, g);
                        SlotCabinet(m, 0.95f, 0.7f, new[] { Look.PinkHi, Look.Marker, Look.Soul, Look.Gold }[i % 4], false, null);
                    }
                    AddLight("jackpot", LightType.Point, new Color(1f, 0.65f, 0.25f), 6f, 6f, new Vector3(0, 3.0f, 0), g);
                    return;
                }
                case "roulette":
                {
                    var g = Group("roulette", at);
                    float w = (float)(p.W ?? 2.8), d = (float)(p.D ?? 1.5);
                    FeltTable(g, w, d, 0.9f, Look.Gold * 0.8f, Look.BaizeMat);
                    var wheel = Group("wheel", new Vector3(-w / 2 + 0.55f, 0.9f, 0), 0, g);
                    Part("bowl", MeshKit.Lathe(new[] { P(0.42f, 0), P(0.42f, 0.08f), P(0.36f, 0.1f), P(0.3f, 0.06f), P(0.12f, 0.03f), P(0.05f, 0.06f), P(0, 0.06f) }, 48), Look.DarkWood, Vector3.zero, null, wheel);
                    Part("rotor", MeshKit.Lathe(new[] { P(0.3f, 0.055f), P(0.12f, 0.035f) }, 38, false, false), Look.Surface("rotor", new Color(0.4f, 0.02f, 0.04f), 0.8f), Vector3.zero, null, wheel);
                    Part("turret", MeshKit.Lathe(new[] { P(0.03f, 0.03f), P(0.03f, 0.12f), P(0.05f, 0.14f), P(0, 0.17f) }, 16), Look.BrassMat, Vector3.zero, null, wheel);
                    Pendant(g, new Vector3(0, 3.1f, 0), "low");
                    return;
                }
                case "cardtable":
                case "poker":
                case "craps":
                {
                    var g = Group(p.Kind, at);
                    float w = (float)(p.W ?? 2.2), d = (float)(p.D ?? 1.2);
                    var trim = p.Kind == "craps" ? Look.Mid : p.Kind == "poker" ? Look.High : Look.Low;
                    FeltTable(g, w, d, 0.9f, trim * 0.6f, p.Kind == "craps" ? Look.BaizeMat : Look.Felt);
                    // chip stacks
                    var chip = Cached("chip", () => MeshKit.Lathe(new[] { P(0, 0), P(0.02f, 0), P(0.02f, 0.035f), P(0, 0.035f) }, 18));
                    Color[] cc = { Look.Blood, Look.Marker, Look.Bone, Look.Low * 0.6f };
                    for (int s = 0; s < 5; s++)
                        Part("chips", chip, Look.Surface("chip" + s % 4, cc[s % 4], 0.6f), new Vector3(-w / 2 + 0.35f + s * 0.07f, 0.91f, d / 2 - 0.3f), null, g, false);
                    if (p.Kind != "poker") Pendant(g, new Vector3(0, p.Room == "floor" ? 3.1f : 2.8f, 0), p.Room == "back" ? "high" : "low");
                    return;
                }
                case "booth":
                {
                    var g = Group("booth", at);
                    float w = (float)(p.W ?? 1.2), d = (float)(p.D ?? 2.4);
                    Part("bench", Box(w * 0.5f, 0.45f, d, 0.06f), Look.VelvetPurple, new Vector3(-w * 0.2f, 0.23f, 0), null, g);
                    Part("back", Box(0.2f, 1.2f, d, 0.08f), Look.VelvetPurple, new Vector3(-w / 2 + 0.1f, 0.85f, 0), null, g);
                    Part("cap", Box(0.26f, 0.06f, d + 0.04f, 0.02f), Look.BrassMat, new Vector3(-w / 2 + 0.1f, 1.46f, 0), null, g);
                    Part("table", Box(0.6f, 0.04f, d * 0.6f, 0.02f), Look.Marble, new Vector3(w * 0.35f, 0.72f, 0), null, g);
                    Part("table leg", MeshKit.Lathe(new[] { P(0.2f, 0), P(0.2f, 0.02f), P(0.04f, 0.05f), P(0.04f, 0.7f) }, 20), Look.BrassMat, new Vector3(w * 0.35f, 0, 0), null, g);
                    Part("lamp", Cached("tableLamp", () => MeshKit.Lathe(new[] { P(0.06f, 0), P(0.06f, 0.02f), P(0.012f, 0.03f), P(0.012f, 0.28f), P(0.1f, 0.3f), P(0.06f, 0.42f), P(0.0f, 0.43f) }, 18)), Look.Shade, new Vector3(w * 0.35f, 0.74f, 0), null, g, false);
                    AddLight("booth lamp", LightType.Point, new Color(1f, 0.6f, 0.4f), 1.4f, 2.2f, new Vector3(w * 0.35f, 1.1f, 0), g);
                    return;
                }
                case "piano":
                {
                    // its keys (local -z) face south, to the pianist's bench (Layout's pianist sits south of it, facing north)
                    var g = Group("piano", at, 20);
                    Part("body", Box(1.5f, 0.32f, 1.1f, 0.12f), Look.BlackGloss, new Vector3(0, 0.82f, 0), null, g);
                    Part("lid", Box(1.45f, 0.02f, 1.05f, 0.1f), Look.BlackGloss, new Vector3(0.05f, 1.25f, 0.18f), Quaternion.Euler(0, 0, 0) * Quaternion.Euler(-28, 0, 0), g);
                    Part("keys", Box(1.2f, 0.04f, 0.18f, 0.005f), Look.Surface("keys", new Color(0.92f, 0.9f, 0.85f), 0.7f), new Vector3(0, 0.86f, -0.62f), null, g);
                    foreach (var lx in new[] { -0.6f, 0.6f, 0f })
                        Part("leg", MeshKit.Lathe(new[] { P(0.05f, 0), P(0.06f, 0.06f), P(0.045f, 0.4f), P(0.065f, 0.66f) }, 16), Look.BlackGloss, new Vector3(lx, 0, lx == 0 ? 0.42f : -0.35f), null, g);
                    return;
                }
                case "chandelier":
                {
                    float y = p.Room == "floor" ? 3.95f : 2.85f;
                    var g = Group("chandelier", at + Vector3.up * y);
                    Part("chain", MeshKit.Tube(new[] { Vector3.zero, Vector3.up * (p.Room == "floor" ? 1.05f : 0.75f) }, 0.012f, 6), Look.BrassMat, Vector3.zero, null, g, false);
                    Part("ring", MeshKit.Tube(MeshKit.Circle(0.55f, 40, 0), 0.025f, 8, true), Look.BrassMat, Vector3.zero, null, g, false);
                    Part("ring", MeshKit.Tube(MeshKit.Circle(0.32f, 32, 0.22f), 0.02f, 8, true), Look.BrassMat, Vector3.zero, null, g, false);
                    var drop = Cached("drop", () => MeshKit.Lathe(new[] { P(0, -0.09f), P(0.03f, -0.03f), P(0.022f, 0.02f), P(0, 0.04f) }, 8));
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2;
                        Part("crystal", drop, Look.Crystal, new Vector3(Mathf.Cos(a) * 0.55f, -0.09f, Mathf.Sin(a) * 0.55f), null, g, false);
                        if (i % 2 == 0) Part("candle", Cached("candle", () => MeshKit.Lathe(new[] { P(0.018f, 0), P(0.018f, 0.1f), P(0.025f, 0.12f), P(0.0f, 0.18f) }, 10)), Look.Bulb, new Vector3(Mathf.Cos(a) * 0.32f, 0.22f, Mathf.Sin(a) * 0.32f), null, g, false);
                    }
                    AddLight("chandelier", LightType.Point, new Color(1f, 0.78f, 0.55f), p.Room == "floor" ? 22f : 12f, p.Room == "floor" ? 10f : 7f, Vector3.zero, g, false);
                    return;
                }
                case "bulb":
                {
                    var g = Group("bare bulb", at);
                    Part("cord", MeshKit.Tube(new[] { new Vector3(0, 2.25f, 0), new Vector3(0, 3.6f, 0) }, 0.006f, 5), Look.BlackGloss, Vector3.zero, null, g, false);
                    Part("cone", MeshKit.Lathe(new[] { P(0.02f, 2.42f), P(0.05f, 2.4f), P(0.2f, 2.26f), P(0.2f, 2.25f), P(0.05f, 2.39f) }, 20, false, false), Look.Surface("tin", new Color(0.25f, 0.27f, 0.22f), 0.5f, 0.6f), Vector3.zero, null, g);
                    Part("bulb", Cached("bulbMesh", () => MeshKit.Lathe(new[] { P(0, -0.07f), P(0.04f, -0.05f), P(0.05f, 0), P(0.02f, 0.05f), P(0.02f, 0.08f) }, 16)), Look.Bulb, new Vector3(0, 2.3f, 0), null, g, false);
                    AddLight("bulb", LightType.Spot, new Color(1f, 0.7f, 0.42f), 18f, 5.5f, new Vector3(0, 2.26f, 0), g, true, 120, Vector3.down);
                    return;
                }
                case "rope":
                {
                    var g = Group("rope", at);
                    float d = (float)(p.D ?? 4);
                    var post = Cached("stanchion", () => MeshKit.Lathe(new[] { P(0.16f, 0), P(0.16f, 0.03f), P(0.03f, 0.06f), P(0.025f, 0.95f), P(0.05f, 0.98f), P(0.045f, 1.03f), P(0, 1.06f) }, 20));
                    int posts = 3;
                    for (int i = 0; i < posts; i++) Part("stanchion", post, Look.BrassMat, new Vector3(0, 0, -d / 2 + d * i / (posts - 1)), null, g);
                    for (int i = 0; i < posts - 1; i++)
                    {
                        var pts = new List<Vector3>();
                        float a = -d / 2 + d * i / (posts - 1), b = -d / 2 + d * (i + 1) / (posts - 1);
                        for (int k = 0; k <= 16; k++) { float t = k / 16f; pts.Add(new Vector3(0, 0.95f - Mathf.Sin(t * Mathf.PI) * 0.18f, Mathf.Lerp(a, b, t))); }
                        Part("velvet rope", MeshKit.Tube(pts, 0.022f, 8), Look.Velvet, Vector3.zero, null, g);
                    }
                    return;
                }
                case "plant":
                {
                    var g = Group("plant", at);
                    Part("pot", MeshKit.Lathe(new[] { P(0.18f, 0), P(0.26f, 0.5f), P(0.28f, 0.52f), P(0.28f, 0.56f), P(0.24f, 0.56f), P(0.24f, 0.5f) }, 28), Look.Pot, Vector3.zero, null, g);
                    var leaf = Cached("leaf", () => MeshKit.Lathe(new[] { P(0, 0), P(0.05f, 0.12f), P(0.06f, 0.35f), P(0.03f, 0.55f), P(0, 0.62f) }, 6));
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 40, tilt = 25 + (i % 3) * 12;
                        Part("leaf", leaf, Look.Plant, new Vector3(0, 0.52f, 0), Quaternion.Euler(0, a, 0) * Quaternion.Euler(tilt, 0, 0) * Quaternion.Euler(0, 0, 0), g);
                    }
                    return;
                }
                case "stool":
                    Stool(Group("bar stool", at));
                    return;
            }
        }

        void Entrance(Floor f)
        {
            // the entrance doors on the south wall, closed behind you; a red carpet runner up the aisle
            var g = Group("entrance", U(f.DoorX, 0, 17.8));
            float w = (float)f.DoorW, h = (float)f.DoorH;
            foreach (var sx in new[] { -1, 1 })
            {
                Part("door", Box(w / 2 - 0.03f, h, 0.08f, 0.02f), Look.Lacquer, new Vector3(sx * w / 4, h / 2, 0.02f), null, g);
                Part("panel", MeshKit.Tube(MeshKit.RoundRect(w / 2 - 0.3f, h - 0.6f, 0.08f), 0.012f, 6, true), Look.BrassMat, new Vector3(sx * w / 4, h / 2, -0.03f), null, g);
                Part("handle", MeshKit.Tube(new[] { new Vector3(sx * 0.1f, 0.9f, -0.07f), new Vector3(sx * 0.1f, 1.5f, -0.07f) }, 0.015f, 8), Look.BrassMat, Vector3.zero, null, g);
            }
            Part("frame", Box(w + 0.3f, 0.2f, 0.2f, 0.03f), Look.BrassMat, new Vector3(0, h + 0.1f, 0), null, g);
            Part("runner", MeshKit.RoundedBox(2.2f, 0.012f, 16f, 0.004f, 1, 1), Look.Velvet, U(0, 0.006, 9.8), null, null, false);
            Neon.Sign(g, Two ? "FLOOR 2" : "THE VIG", Two ? Look.Gold : Look.PinkHi, new Vector3(0, h + 0.75f, -0.1f), 180, 0.7f, this, frame: true);
        }

        void Probes(Floor f)
        {
            foreach (var r in f.Rooms)
            {
                var go = new GameObject("reflections " + r.Name);
                go.transform.SetParent(Root, false);
                go.transform.position = U((r.X0 + r.X1) / 2, r.Ceiling / 2, (r.Z0 + r.Z1) / 2);
                var p = go.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Realtime;
                p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                p.size = new Vector3((float)(r.X1 - r.X0), (float)r.Ceiling, (float)(r.Z1 - r.Z0));
                p.boxProjection = true;
                p.resolution = 256;
                p.hdr = true;
                p.RenderProbe();
            }
        }

        /// <summary>Register a light made elsewhere (the signs) so the room's light list stays complete.</summary>
        public Light SignLight(Transform parent, Color c, Vector3 localPos, float intensity, float range) =>
            AddLight("sign light", LightType.Point, c, intensity, range, localPos, parent);
    }
}
