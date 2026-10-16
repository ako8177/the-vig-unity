// The people of floor 1 (DECISIONS U13): the Collector in his chair across his table, and the crowd
// from Layout.Floor1.Npcs (the players at the slots and tables, the dealers, the bartender, the drinkers,
// smokers and railbirds, the booths, the pianist, the walkers). Presentation only, never in the sim: the
// sim's state and events say what the Collector does; the crowd moves on its own clock.
//
// Every person is rigid Blender-built parts (art/people/build_*.py → Art/People/Resources/People/*.fbx),
// each with its origin at its joint, hung on a small tree of joints and posed by code each frame, as the
// TS game's crowd was (../the-vig/src/render3d/npcs.ts: its joints, measures and poses carry over). A
// part's meshes come split by role (`head`, `head__dark`); each role gets a material here.
//
// Angles from the TS poses: a TS figure faces -Z in a right-handed frame, a Unity one faces +Z in a
// left-handed one, so a joint's X and Y turns flip sign and its Z turn doesn't (a positive shoulder
// still swings the arm forward: Euler(-sh, 0, 0) here). Sim (x, y, z) is Unity (x, y, -z) (U6).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public sealed class People
    {
        public readonly Transform Root;
        readonly Him him;
        readonly List<Person> crowd = new List<Person>();
        /// <summary>the people's own clock (s): the shot tool poses them in edit mode, where Time stands still</summary>
        float now;
        int lastShoe = -1, lastTick = -1;
        Game lastGame;

        static Dictionary<string, Mesh> kit;
        static readonly string[] Roles = { "base", "shade", "dark", "accent", "skin", "metal", "ember", "eye", "pale" };

        static Dictionary<string, Mesh> Kit()
        {
            if (kit != null) return kit;
            kit = new Dictionary<string, Mesh>();
            foreach (var path in new[] { "People/Collector", "People/Crowd" })
                foreach (var m in Resources.LoadAll<Mesh>(path)) kit[m.name] = m;
            if (!kit.ContainsKey("coll_torso") || !kit.ContainsKey("torso_coat"))
                Debug.LogError("Vig: the people's meshes are missing (Art/People/Resources/People/*.fbx): " + string.Join(", ", kit.Keys));
            return kit;
        }

        People(Transform parent)
        {
            Root = new GameObject("People").transform;
            Root.SetParent(parent, false);
            Kit();
            him = new Him(this, Layout.Floor1.Collector);
            for (int i = 0; i < Layout.Floor1.Npcs.Length; i++) crowd.Add(new Person(this, Layout.Floor1.Npcs[i], i));
        }

        public static People Build(Transform parent) => new People(parent);

        // ---------------------------------------------------------------- parts and materials

        internal static Transform Joint(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>Hang part `part` (all its role meshes) on a joint; `mat` colors each role.</summary>
        internal static void Dress(Transform joint, string part, System.Func<string, Material> mat, bool shadows = true)
        {
            foreach (var role in Roles)
            {
                string n = role == "base" ? part : part + "__" + role;
                if (!kit.TryGetValue(n, out var mesh)) continue;
                var go = new GameObject(n);
                go.transform.SetParent(joint, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat(role);
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        internal static Material Cloth(Color c, float smooth = 0.28f) => Look.Surface("ppl" + ColorUtility.ToHtmlStringRGB(c) + smooth, c, smooth);
        internal static readonly Material Dark = Look.Surface("pplDark", new Color(0.035f, 0.03f, 0.035f), 0.45f);

        // the crowd's colors: dressed for a night out, bright enough to read against dark cabinets, a few
        // dark suits; never a tier's hue (STYLE.md: the tiers own their colors)
        static readonly Color[] Skin = { Hex("f1c6a5"), Hex("e0a982"), Hex("c68863"), Hex("9a6243"), Hex("6b4029"), Hex("4a2c1d") };
        static readonly Color[] HairCol = { Hex("1f1d1e"), Hex("3b2416"), Hex("1f1d1e"), Hex("6b4a2b"), Hex("c8a060"), Hex("b8b8b0"), Hex("7a2a12") };
        static readonly Color[] Coat = { Look.Pink, Look.Teal, Look.IndigoHi, Hex("5555e6"), Hex("9c3b10"), Look.Cream, Look.PinkHi, Hex("8a1d4a"), Look.Night, Hex("1a6d7a"), Look.Smoke, Look.Bone, Look.Indigo, Hex("2a2a40") };
        static readonly Color[] Pants = { Look.Ink, Look.Night, Look.Smoke, Look.WoodLo, Look.Indigo, Hex("8c9687") };
        static readonly Color[] Shoes = { Look.Ink, Look.Ink, Look.WoodLo, Look.Night };
        static readonly Color[] Hats = { Look.Ink, Look.Night, Look.WoodLo, Look.Smoke, Look.Indigo };
        static readonly Color[] Ties = { Look.Blood, Look.Brass, Look.IndigoHi, Look.Teal, Look.Ink, Look.PinkHi };
        static readonly string[] HairCoat = { "hair_short", "hair_short", "hair_quiff", "hat", "hair_long" };
        static readonly string[] HairDress = { "hair_long", "hair_long", "hair_bun", "hair_short" };

        /// <summary>A stable pseudo-random number in [0, 1) for person i, draw k (the TS crowd's hash).</summary>
        static float Rand(int i, int k)
        {
            uint h = (uint)(i * 374761393 + k * 668265263) * 1274126177u;
            return (h % 10000) / 10000f;
        }

        static T Pick<T>(T[] a, int i, int k) => a[Mathf.Min(a.Length - 1, (int)(Rand(i, k) * a.Length))];

        // ---------------------------------------------------------------- the frame

        /// <summary>Pose everyone, `dt` seconds on. `you` is the camera (people near you glance at you).</summary>
        public void Tick(Game g, float dt, Vector3 you)
        {
            now += dt;
            lastTick = g.Tick;
            lastGame = g;
            var s = g.State;
            var me = s.Players[0];
            // the Twenty-One dealer deals when a card leaves the shoe
            int shoe = s.Tables.TryGetValue("twentyone", out var t21) && t21.Shoe != null ? t21.Shoe.Count : -1;
            if (lastShoe >= 0 && shoe >= 0 && shoe < lastShoe) Deal("twentyone");
            lastShoe = shoe;
            string seat = s.Phase == Phase.Floor ? me.Seat : null;
            Vector3? seatAt = null;
            if (seat != null)
            {
                var pl = Layout.Floor1.TableOf(seat);
                if (pl != null) seatAt = RoomBuilder.U(pl.Stand.X, 0, pl.Stand.Z);
            }
            foreach (var p in crowd) p.Pose(dt, now, seat, seatAt, you);
            him.Pose(g, dt, now);
        }

        /// <summary>
        /// For the shot tool, which steps the sim by itself: pose everyone as far on as the sim has gone
        /// since they were last posed (a cheer from three shots ago is over; a death has finished slumping).
        /// </summary>
        public void CatchUp(Game g, Vector3 you) =>
            Tick(g, Mathf.Max(0, g.Tick - (g == lastGame ? lastTick : 0)) / (float)State.TicksPerS, you);

        readonly Dictionary<string, float> dealt = new Dictionary<string, float>();
        void Deal(string table) => dealt[table] = now;
        internal float DealtAt(string table) => table != null && dealt.TryGetValue(table, out var t) ? t : -99;

        public void OnEvent(SimEvent e, Game g)
        {
            switch (e.T)
            {
                case "round":
                {
                    var o = e.Outcome;
                    if (o == null) break;
                    int net = o.Ret - o.Bet;
                    var pl = Layout.Floor1.TableOf(o.Table);
                    var at = pl != null ? RoomBuilder.U(pl.X, 0, pl.Z) : Vector3.zero;
                    React(o.Table, net > 0, net >= Feel.BigWin || net <= -Feel.BigWin, at);
                    break;
                }
                default:
                    him.OnEvent(e, g);
                    break;
            }
        }

        /// <summary>A result at `table`: its railbirds cheer a win (a big one brings the room in), groan at a big loss.</summary>
        void React(string table, bool win, bool big, Vector3 at)
        {
            foreach (var p in crowd)
            {
                bool mine = p.Spot.Table == table;
                float near = Vector3.Distance(p.Pos, at);
                if (win && (mine || (big && near < 9 && p.Spot.Role != "walker"))) { p.Cheer = big ? 2.2f : 1.3f; p.Big = big; }
                else if (!win && mine && big) p.Groan = 1.4f;
            }
        }

        /// <summary>The shortest signed turn from a to b (degrees).</summary>
        internal static float Turn(float a, float b) => Mathf.DeltaAngle(a, b);

        // ================================================================ the crowd

        sealed class Person
        {
            public readonly NpcSpot Spot;
            readonly People ppl;
            readonly int idx;
            readonly Transform root, torso, head, shL, shR, elL, elR, hipL, hipR, knL, knR;
            readonly Transform glass, cig;
            readonly float phase, speed;
            /// <summary>Unity position on the floor and facing (degrees; 0 looks +Z)</summary>
            public Vector3 Pos;
            float yaw;
            int leg = 1, dir = 1;
            public float Cheer, Groan;
            public bool Big;
            readonly Vector3[] path;

            /// <summary>the figure's measures (m): about 1.72 m standing (npcs.ts)</summary>
            const float Thigh = 0.44f, Shin = 0.4f, StandHip = 0.9f, Upper = 0.3f, Fore = 0.27f, ShX = 0.24f, ShY = 0.53f;

            static float SeatTop(string role) => role switch
            {
                "slots" => 0.69f, "drinker" => 0.725f, "booth" => 0.455f, "pianist" => 0.54f, _ => 0.55f,
            };

            public Person(People ppl, NpcSpot spot, int i)
            {
                Spot = spot;
                this.ppl = ppl;
                idx = i;
                phase = Rand(i, 1) * 10;
                speed = 0.9f + Rand(i, 2) * 0.5f;
                Pos = RoomBuilder.U(spot.X, 0, spot.Z);
                yaw = -(float)spot.Yaw * Mathf.Rad2Deg;
                path = spot.Path?.Select(p => RoomBuilder.U(p.X, 0, p.Z)).ToArray();

                string role = spot.Role;
                bool staff = role == "dealer" || role == "bartender";
                bool dress = !staff && Rand(i, 9) < 0.42f;
                var skin = Pick(Skin, i, 3);
                var hairs = dress ? HairDress : staff ? HairCoat.Take(3).ToArray() : HairCoat;
                string hairPart = Pick(hairs, i, 10);
                var hair = hairPart == "hat" ? Pick(Hats, i, 4) : Pick(HairCol, i, 4);
                var coat = staff ? Look.Ink : Pick(Coat, i, 5);
                var sleeve = staff ? Look.Bone : coat;
                var pants = staff ? Look.Ink : Pick(Pants, i, 6);
                var shoe = Pick(Shoes, i, 7);
                bool skirt = dress && Rand(i, 11) < 0.6f;
                var shirt = dress ? skin : Rand(i, 12) < 0.7f ? Look.Bone : Look.Cream;
                var tie = Pick(Ties, i, 13);

                System.Func<string, Material> Mat(Color c, float sm = 0.28f, Color? accent = null) => r => r switch
                {
                    "shade" => Cloth(c * 0.55f, sm),
                    "dark" => Dark,
                    "accent" => Cloth(accent ?? tie, 0.45f),
                    "skin" => Cloth(skin, 0.42f),
                    "metal" => Look.BrassMat,
                    "ember" => Look.Glow("ember", new Color(1f, 0.42f, 0.12f), 5f),
                    _ => Cloth(c, sm),
                };

                root = Joint(role + " " + i, ppl.Root, Pos);
                root.localRotation = Quaternion.Euler(0, yaw, 0);
                torso = Joint("torso", root, new Vector3(0, StandHip, 0));
                Dress(torso, skirt ? "skirt" : "pelvis", Mat(skirt ? coat : pants));
                Dress(torso, dress ? "torso_dress" : "torso_coat", Mat(coat));
                Dress(torso, dress ? "neckline" : staff ? "shirt_bow" : "shirt_tie", Mat(shirt, 0.35f));
                head = Joint("head", torso, new Vector3(0, ShY + 0.05f, 0));
                Dress(head, "head", Mat(skin, 0.42f));
                Dress(head, hairPart, Mat(hair, hairPart == "hat" ? 0.3f : 0.38f, Look.Ink));
                shL = Joint("shoulder L", torso, new Vector3(-ShX, ShY, 0));
                shR = Joint("shoulder R", torso, new Vector3(ShX, ShY, 0));
                Dress(shL, "arm_upper", Mat(sleeve));
                Dress(shR, "arm_upper", Mat(sleeve));
                elL = Joint("elbow L", shL, new Vector3(0, -Upper, 0));
                elR = Joint("elbow R", shR, new Vector3(0, -Upper, 0));
                Dress(elL, "arm_fore", Mat(sleeve));
                Dress(elR, "arm_fore", Mat(sleeve));
                Dress(Joint("wrist L", elL, new Vector3(0, -Fore, 0)), "hand", Mat(skin, 0.42f));
                Dress(Joint("wrist R", elR, new Vector3(0, -Fore, 0)), "hand", Mat(skin, 0.42f));
                hipL = Joint("hip L", root, new Vector3(-0.09f, StandHip, 0));
                hipR = Joint("hip R", root, new Vector3(0.09f, StandHip, 0));
                Dress(hipL, "thigh", Mat(pants));
                Dress(hipR, "thigh", Mat(pants));
                knL = Joint("knee L", hipL, new Vector3(0, -Thigh, 0));
                knR = Joint("knee R", hipR, new Vector3(0, -Thigh, 0));
                Dress(knL, "shin", Mat(pants));
                Dress(knR, "shin", Mat(pants));
                Dress(Joint("ankle L", knL, new Vector3(0, -Shin, 0)), "shoe", Mat(shoe, 0.7f));
                Dress(Joint("ankle R", knR, new Vector3(0, -Shin, 0)), "shoe", Mat(shoe, 0.7f));

                // what's under them: the room has bar stools and booth benches; the rest bring their own
                if (spot.Seated)
                {
                    if (role == "table") Dress(Joint("chair", root, new Vector3(0, 0, 0.04f)), "chair", r => r == "metal" ? Look.BrassMat : Look.DarkWood);
                    else if (role == "slots") Dress(Joint("stool", root, Vector3.zero), "stool", r => r == "metal" ? Look.BrassMat : Look.Velvet);
                    else if (role == "pianist") Dress(Joint("bench", root, Vector3.zero), "bench", _ => Look.BlackGloss);
                }
                // what's in the right hand
                if (role == "drinker" || role == "booth" || role == "bartender")
                {
                    glass = Joint("glass", elR, new Vector3(0, -Fore - 0.06f, 0.01f));
                    Dress(glass, "glass", r => r == "accent" ? Look.Surface("drink", new Color(0.55f, 0.22f, 0.04f), 0.9f) : Look.Surface("tumbler", new Color(0.55f, 0.6f, 0.65f), 0.95f), false);
                }
                if (role == "smoker")
                {
                    cig = Joint("cigarette", elR, new Vector3(0, -Fore - 0.07f, 0.03f));
                    Dress(cig, "cig", r => r == "ember" ? Look.Glow("ember", new Color(1f, 0.42f, 0.12f), 5f) : Look.Surface("paper", new Color(0.9f, 0.88f, 0.84f), 0.3f), false);
                }
            }

            static float Pulse(float tt, float period, float len)
            {
                float k = ((tt % period) + period) % period;
                return k < len ? Mathf.Sin(k / len * Mathf.PI) : 0;
            }

            public void Pose(float dt, float t, string seat, Vector3? seatAt, Vector3 you)
            {
                string role = Spot.Role;
                float tt = t * speed + phase;
                float facing = yaw;
                bool walk = false;
                // walkers and the bartender move along their path, out and back
                if ((role == "walker" || role == "bartender") && path != null && path.Length > 1)
                {
                    var to = path[leg];
                    var d = to - Pos;
                    d.y = 0;
                    float sp = role == "walker" ? 1.1f : 0.5f;
                    if (d.magnitude < 0.05f)
                    {
                        if (leg + dir < 0 || leg + dir >= path.Length) dir = -dir;
                        leg += dir;
                    }
                    else
                    {
                        // the bartender pauses to polish a glass now and then, facing the back bar
                        bool pause = role == "bartender" && Mathf.Sin(tt * 0.4f) > 0.3f;
                        if (!pause)
                        {
                            Pos += d * Mathf.Min(1, sp * dt / d.magnitude);
                            walk = true;
                        }
                        facing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        if (pause) facing = -(float)Spot.Yaw * Mathf.Rad2Deg;
                    }
                    yaw = facing;
                }
                // the railbirds by a table turn to watch you play it, then turn back
                if (role == "watcher")
                {
                    float want = -(float)Spot.Yaw * Mathf.Rad2Deg;
                    float rate = 1.5f;
                    if (seat != null && seat == Spot.Table && seatAt.HasValue)
                    {
                        var d = seatAt.Value - Pos;
                        want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        rate = 3;
                    }
                    yaw += Turn(yaw, want) * Mathf.Min(1, dt * rate);
                    facing = yaw;
                }
                // anyone close by glances at you
                float headYaw = 0;
                var toYou = you - Pos;
                toYou.y = 0;
                if (toYou.magnitude < 3.5f && role != "walker")
                    headYaw = Mathf.Clamp(Turn(facing, Mathf.Atan2(toYou.x, toYou.z) * Mathf.Rad2Deg), -50, 50);
                Cheer = Mathf.Max(0, Cheer - dt);
                Groan = Mathf.Max(0, Groan - dt);

                // ---- the pose (radians, as npcs.ts: a positive shoulder or hip swings the limb forward,
                // a positive elbow bends the forearm forward, a positive knee folds the shin back; `ab`
                // lifts an arm out to the side, negative tucks it across)
                bool seated = Spot.Seated;
                float hip = seated ? SeatTop(role) + 0.07f : StandHip;
                float shLa = 0.05f, shRa = 0.05f, elLa = 0.15f, elRa = 0.15f, abL = 0.06f, abR = 0.06f, lean = 0, hop = 0;
                float thL = seated ? Mathf.PI / 2 : 0, thR = thL, kL = thL, kR = thL;
                bool holding = role == "drinker" || role == "booth" || role == "smoker";
                switch (role)
                {
                    case "slots":
                    {
                        // a hand on the buttons, the other on the handle: up, pull down, let go
                        float pl = Pulse(tt, 3.2f, 0.8f);
                        shLa = 0.75f; elLa = 0.7f;
                        shRa = 1.25f - 0.8f * pl; elRa = 0.3f + 0.3f * pl; abR = 0.25f;
                        lean = 0.1f;
                        break;
                    }
                    case "table":
                    {
                        // leaning in, forearms on the rail; the right pushes chips out now and then
                        float push = Pulse(tt, 4.5f, 0.7f);
                        shLa = 0.75f; elLa = 0.85f;
                        shRa = 0.75f + 0.35f * push; elRa = 0.85f - 0.25f * push;
                        abL = abR = 0.12f;
                        lean = seated ? 0.32f : 0.16f;
                        break;
                    }
                    case "dealer":
                    {
                        // the Twenty-One dealer deals when a card comes out; the others on their own beat
                        float since = t - ppl.DealtAt(Spot.Table);
                        float deal = Spot.Table != null ? (since < 0.38f ? Mathf.Sin(since / 0.38f * Mathf.PI) : 0) : Pulse(tt, 1.6f, 0.6f);
                        shLa = 0.45f; elLa = 0.75f;
                        shRa = 0.45f + 0.4f * deal; elRa = 0.75f - 0.3f * deal; abR = 0.08f + 0.3f * deal;
                        lean = 0.12f;
                        break;
                    }
                    case "bartender":
                        if (!walk)
                        {
                            // polishing a glass at the chest
                            shLa = shRa = 0.4f; elLa = 1.25f; elRa = 1.3f + 0.25f * Mathf.Sin(tt * 6); abL = abR = -0.12f;
                            holding = true;
                        }
                        break;
                    case "drinker":
                    case "booth":
                    {
                        float sip = Pulse(tt, 6, 1.6f);
                        shRa = 0.3f + 0.5f * sip; elRa = 0.95f + 1.4f * sip;
                        if (seated) { shLa = 0.4f; elLa = 1.15f; }
                        break;
                    }
                    case "smoker":
                    {
                        // one arm across the body, the cigarette hand up to the mouth for a drag
                        float drag = Pulse(tt, 5, 1.4f);
                        shRa = 0.25f + 0.45f * drag; elRa = 0.65f + 1.9f * drag;
                        shLa = 0.25f; elLa = 1.45f; abL = -0.25f;
                        break;
                    }
                    case "pianist":
                        shLa = shRa = 0.5f;
                        elLa = 0.9f + 0.12f * Mathf.Sin(tt * 9);
                        elRa = 0.9f + 0.12f * Mathf.Sin(tt * 9 + 1.7f);
                        lean = 0.1f;
                        break;
                    case "watcher":
                        // half of them fold their arms, the rest let them hang
                        // (folded at the chest: npcs.ts's 0.3 + 1.65 raised the forearms to the face at full resolution)
                        if (Rand(idx, 8) < 0.5f) { shLa = shRa = 0.12f; elLa = elRa = 1.5f; abL = abR = -0.5f; }
                        else shLa = 0.06f + 0.03f * Mathf.Sin(tt);
                        lean = seat != null && seat == Spot.Table ? 0.12f : 0;
                        break;
                }
                if (walk)
                {
                    // a stride: the legs swing, the back knee folds, the arms swing against the legs
                    float st = tt * (role == "walker" ? 7 : 6), amp = role == "walker" ? 0.45f : 0.35f, sw = Mathf.Sin(st);
                    thL = sw * amp; thR = -sw * amp;
                    kL = Mathf.Max(0, -Mathf.Cos(st)) * 0.6f; kR = Mathf.Max(0, Mathf.Cos(st)) * 0.6f;
                    shLa = -sw * amp * 0.8f; shRa = sw * amp * 0.8f; elLa = elRa = 0.25f;
                }
                // breathing, so nobody is a statue
                float breathe = Mathf.Sin(tt * 1.7f) * 0.012f;
                if (Cheer > 0)
                {
                    // arms up and a hop (a big win: higher, longer)
                    float k = Mathf.Abs(Mathf.Sin(Cheer * 9));
                    shLa = shRa = 2.9f; elLa = elRa = 0.2f; abL = abR = 0.35f;
                    hop = seated ? 0 : (Big ? 0.14f : 0.07f) * k;
                }
                else if (Groan > 0)
                {
                    // hands to the head
                    shLa = shRa = 2.7f; elLa = elRa = 2.0f; abL = abR = 0.5f; lean = 0.25f;
                }

                const float D = Mathf.Rad2Deg;
                var rot = Quaternion.Euler(0, facing, 0);
                var at = Pos;
                // a slot player's spot is at the machine: the stool is a hand's width behind it. A seated
                // card player's spot is the table's edge: the chair goes back so the knees clear it
                if (role == "slots") at += rot * new Vector3(0, 0, -0.12f);
                if (role == "table" && seated) at += rot * new Vector3(0, 0, -0.42f);
                root.localPosition = at + Vector3.up * hop;
                root.localRotation = rot;
                torso.localPosition = new Vector3(0, hip, 0);
                torso.localRotation = Quaternion.Euler((lean + breathe) * D, 0, 0);
                // the head stays upright as the body leans, and turns toward you when you're close
                head.localRotation = Quaternion.Euler(-lean * 0.7f * D, headYaw, 0);
                shL.localRotation = Quaternion.Euler(-shLa * D, 0, -abL * D);
                shR.localRotation = Quaternion.Euler(-shRa * D, 0, abR * D);
                elL.localRotation = Quaternion.Euler(-elLa * D, 0, 0);
                elR.localRotation = Quaternion.Euler(-elRa * D, 0, 0);
                hipL.localPosition = new Vector3(-0.09f, hip, 0);
                hipR.localPosition = new Vector3(0.09f, hip, 0);
                hipL.localRotation = Quaternion.Euler(-thL * D, 0, 0);
                hipR.localRotation = Quaternion.Euler(-thR * D, 0, 0);
                knL.localRotation = Quaternion.Euler(kL * D, 0, 0);
                knR.localRotation = Quaternion.Euler(kR * D, 0, 0);
                // a glass stays upright in the hand; a cheer puts it down (out of sight)
                if (glass)
                {
                    glass.gameObject.SetActive(holding && Cheer <= 0 && Groan <= 0);
                    glass.rotation = rot;
                }
                if (cig) cig.gameObject.SetActive(Cheer <= 0 && Groan <= 0);
            }
        }

        // ================================================================ the Collector

        /// <summary>
        /// The Collector (DECISIONS #81, U13): seated in his high-backed chair across his round table,
        /// facing you, eyes burning his tier's violet (the next nights' Collectors are the same figure
        /// with other eyes: Collectors.Of(idx).Eyes). He breathes and looks about; his hands rest on the
        /// felt; at the Circuit he nods as he speaks, leans in and reaches to pull, convulses at a live
        /// lever, and at his last shock slumps over the table with his eyes going out.
        /// </summary>
        sealed class Him
        {
            readonly Transform root, hips, torso, head, shL, shR, elL, elR, wrL, wrR;
            readonly Material eyeMat;
            readonly Vector3 restL, restR, center;
            const float HipY = 0.68f, Upper = 0.46f, Fore = 0.42f;
            float reachT, shockT, talkT, dead;
            int reachSlot = -1;
            int idx = -1;
            Color eyes;

            public Him(People ppl, CollectorTable c)
            {
                var ch = c.Chair;
                // his back against the chair's (its cushion's front is 0.09 m in front of the chair's center)
                var chairC = RoomBuilder.U((ch.X0 + ch.X1) / 2, 0, (ch.Z0 + ch.Z1) / 2);
                center = RoomBuilder.U(c.X, c.H, c.Z);
                // he faces the table: from his chair toward its center
                var face = (center - chairC);
                face.y = 0;
                face.Normalize();
                root = Joint("The Collector (him)", ppl.Root, chairC - face * 0.1f);
                root.localRotation = Quaternion.LookRotation(face);
                var side = Vector3.Cross(Vector3.up, face);
                // resting: his wrists on the felt a hand's length in from his edge of the table
                var edge = center - face * ((float)c.R - 0.15f);
                restL = edge - side * 0.3f + Vector3.up * 0.03f;
                restR = edge + side * 0.3f + Vector3.up * 0.03f;

                var coat = Look.Surface("collCoat", new Color(0.045f, 0.042f, 0.055f), 0.35f);
                var skull = Look.Surface("collSkull", new Color(0.03f, 0.026f, 0.034f), 0.55f);
                var hat = Look.Surface("collHat", new Color(0.022f, 0.02f, 0.026f), 0.3f);
                var accent = Look.Surface("collAccent", Look.CollLo * 0.55f, 0.6f);
                var pale = Look.Surface("collPale", new Color(0.62f, 0.6f, 0.58f), 0.45f);
                eyeMat = new Material(Look.Neon(Look.Coll, 6f)) { name = "collEyes" };
                System.Func<string, Material> M(Material bas) => r => r switch
                {
                    "dark" => Dark, "accent" => accent, "pale" => pale, "eye" => eyeMat, _ => bas,
                };

                hips = Joint("hips", root, new Vector3(0, HipY, 0));
                Dress(hips, "coll_pelvis", M(coat));
                torso = Joint("torso", hips, Vector3.zero);
                Dress(torso, "coll_torso", M(coat));
                head = Joint("head", torso, new Vector3(0, 0.8f, 0));
                Dress(head, "coll_head", M(skull));
                Dress(head, "coll_hat", M(hat));
                foreach (var e in head.GetComponentsInChildren<MeshRenderer>())
                    if (e.name.EndsWith("__eye")) e.shadowCastingMode = ShadowCastingMode.Off;
                shL = Joint("shoulder L", torso, new Vector3(-0.36f, 0.64f, 0));
                shR = Joint("shoulder R", torso, new Vector3(0.36f, 0.64f, 0));
                Dress(shL, "coll_upper", M(coat));
                Dress(shR, "coll_upper", M(coat));
                elL = Joint("elbow L", shL, new Vector3(0, -Upper, 0));
                elR = Joint("elbow R", shR, new Vector3(0, -Upper, 0));
                Dress(elL, "coll_fore", M(coat));
                Dress(elR, "coll_fore", M(coat));
                wrL = Joint("wrist L", elL, new Vector3(0, -Fore, 0));
                wrR = Joint("wrist R", elR, new Vector3(0, -Fore, 0));
                Dress(wrL, "coll_hand", M(pale));
                Dress(wrR, "coll_hand", M(pale));
                // seated: the thighs run forward, a little down; the shins drop to the floor
                foreach (var sx in new[] { -0.11f, 0.11f })
                {
                    var hp = Joint("hip", root, new Vector3(sx, HipY, 0));
                    hp.localRotation = Quaternion.Euler(-78, 0, 0);
                    Dress(hp, "coll_thigh", M(coat));
                    var kn = Joint("knee", hp, new Vector3(0, -0.5f, 0));
                    kn.localRotation = Quaternion.Euler(78, 0, 0);
                    Dress(kn, "coll_shin", M(coat));
                    Dress(Joint("ankle", kn, new Vector3(0, -0.5f, 0)), "coll_shoe", M(coat));
                }
            }

            static Color EyeColor(string eyes) => eyes == "high" ? Look.High : eyes == "soul" ? Look.Soul : eyes == "mid" ? Look.Mid : eyes == "low" ? Look.Low : Look.Coll;

            public void OnEvent(SimEvent e, Game g)
            {
                switch (e.T)
                {
                    case "midnight": talkT = 1.6f; break;
                    case "circuit":
                        if (e.Who == "coll")
                        {
                            reachT = 0.9f;
                            reachSlot = e.Slot ?? 0;
                            talkT = 1.2f;
                            if (e.Over == true) shockT = 1.3f;
                        }
                        else if (e.Over == true) talkT = 1.4f; // your shock: he has something to say about it
                        break;
                    case "circuitEnd":
                        if (e.Won == true) dead = 0.0001f;
                        else talkT = 2f;
                        break;
                    case "night": dead = 0; shockT = 0; reachT = 0; break;
                }
            }

            public void Pose(Game g, float dt, float t)
            {
                var s = g.State;
                if (idx != s.Collector.Idx)
                {
                    idx = s.Collector.Idx;
                    eyes = EyeColor(Collectors.Of(idx).Eyes);
                    dead = 0;
                }
                // a death the presentation didn't see happen (the shot tool) is already over
                bool killed = s.Collector.Dead || (s.Circuit?.Done?.Won == true && s.Phase != Phase.Floor);
                if (killed && dead <= 0) dead = 1;
                if (!killed && dead > 0 && s.Phase == Phase.Floor) dead = 0;

                float breathe = Mathf.Sin(t * 1.3f) * 0.012f;
                hips.localPosition = new Vector3(0, HipY + breathe * 0.5f, 0);
                float lean = 0.15f, headX = -0.05f - Mathf.Sin(t * 0.5f) * 0.03f, headY = Mathf.Sin(t * 0.37f) * 0.08f;
                if (talkT > 0 && dead <= 0)
                {
                    talkT = Mathf.Max(0, talkT - dt);
                    headX -= Mathf.Abs(Mathf.Sin(t * 9)) * 0.07f;
                }
                Vector3 hl = restL, hr = restR;
                if (reachT > 0)
                {
                    reachT = Mathf.Max(0, reachT - dt);
                    float u = Mathf.Sin((1 - reachT / 0.9f) * Mathf.PI);
                    lean = 0.15f + 0.4f * u;
                    // toward the lever he pulls: a row across the middle of the felt
                    var side = root.right;
                    var lever = center + side * ((reachSlot - 4.5f) * 0.09f) + root.forward * -0.35f + Vector3.up * 0.05f;
                    hr = Vector3.Lerp(restR, lever, u);
                }
                float eyeK = 1;
                if (dead > 0 && shockT <= 0)
                {
                    // slumped over the table, eyes going out
                    dead = Mathf.Min(1, dead + dt / 2.5f);
                    lean = 0.15f + 0.75f * dead;
                    headX = 0.5f * dead;
                    hl = restL + (root.forward * 0.25f - root.right * 0.1f) * dead;
                    hr = restR + (root.forward * 0.3f + root.right * 0.15f) * dead;
                    eyeK = 1 - dead;
                }
                const float D = Mathf.Rad2Deg;
                torso.localRotation = Quaternion.Euler(lean * D, 0, 0);
                head.localRotation = Quaternion.Euler(headX * D, headY * D, 0);
                root.localPosition = new Vector3(root.localPosition.x, 0, root.localPosition.z);
                Arm(shL, elL, wrL, hl, -1);
                Arm(shR, elR, wrR, hr, 1);
                var eyeC = eyes * eyeK;
                if (shockT > 0)
                {
                    // the current through him: everything jerks, the eyes flare and blink
                    shockT = Mathf.Max(0, shockT - dt);
                    float J() => (Random.value - 0.5f) * 0.5f;
                    torso.localRotation *= Quaternion.Euler(J() * 0.4f * D, 0, J() * 0.3f * D);
                    head.localRotation *= Quaternion.Euler(J() * D, 0, J() * D);
                    shL.localRotation *= Quaternion.Euler(J() * 60, J() * 30, 0);
                    shR.localRotation *= Quaternion.Euler(J() * 60, J() * 30, 0);
                    eyeC = Random.value < 0.4f ? Color.black : Random.value < 0.5f ? Color.white : Look.Soul;
                }
                float k = 6f * 0.4f;
                eyeMat.SetColor("_BaseColor", new Color(eyeC.r * k, eyeC.g * k, eyeC.b * k, 1));
            }

            /// <summary>
            /// A two-bone arm: the wrist to `target` if it can reach, the elbow bent out and down; then the
            /// hand laid flat on the felt, fingers toward the table's center.
            /// </summary>
            void Arm(Transform sh, Transform el, Transform wr, Vector3 target, float side)
            {
                var s = sh.position;
                var d = target - s;
                float len = Mathf.Clamp(d.magnitude, 0.1f, Upper + Fore - 0.002f);
                var dir = d.normalized;
                float cosA = (Upper * Upper + len * len - Fore * Fore) / (2 * Upper * len);
                float a = Mathf.Acos(Mathf.Clamp(cosA, -1, 1));
                // the elbow goes out to his side and down
                var pole = root.right * side * 0.8f + Vector3.down * 0.6f + root.forward * -0.1f;
                var u = Vector3.ProjectOnPlane(pole, dir).normalized;
                var elbow = s + dir * (Upper * Mathf.Cos(a)) + u * (Upper * Mathf.Sin(a));
                var wrist = s + dir * len;
                // (the arm's forward is sideways: it never runs along the arm, which reaches forward)
                sh.rotation = Quaternion.LookRotation(root.right * side, s - elbow);
                el.rotation = Quaternion.LookRotation(root.right * side, elbow - wrist);
                // the hand: fingers (its -y) toward the table's center, thumb (its +z) inward, palm down
                wr.rotation = Quaternion.LookRotation(-root.right * side, -root.forward);
            }
        }
    }
}
