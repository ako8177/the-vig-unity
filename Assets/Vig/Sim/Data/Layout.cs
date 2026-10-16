// Floor and house layouts as data: the rooms, walls and doorways, where each table stands, where you
// sit, the bar, the cage, the decorations and where the crowd stands. The sim's movement system
// collides against Colliders; the Unity scene builder builds the rooms from the same numbers, so what
// you bump into is what you see. Meters, Y up, the entrance on the south wall (+Z) looking north (-Z).
//
// Floor 1 is three rooms (DECISIONS #65): THE FLOOR, the big bright main room by the entrance with the
// low tables, the bar and the cage; THE LOUNGE through the north-west arch, violet and soft, with the
// mid tables; THE BACK ROOM through the north-east arch, dark and smoky, with the high tables and the
// Collector. The deeper you walk, the worse it costs.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vig.Sim.Data
{
    public struct V2 { public double X, Z; public V2(double x, double z) { X = x; Z = z; } }
    public struct V3 { public double X, Y, Z; public V3(double x, double y, double z) { X = x; Y = y; Z = z; } }

    /// <summary>A collider on the floor plane: a box (X0..X1, Z0..Z1) or a circle (X, Z, R), H tall.</summary>
    public sealed class Collider
    {
        public bool IsBox;
        public double X0, Z0, X1, Z1;
        public double X, Z, R;
        public double H;
        public string What;
        public static Collider Box(double x0, double z0, double x1, double z1, double h, string what) =>
            new Collider { IsBox = true, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, H = h, What = what };
        public static Collider Circle(double x, double z, double r, double h, string what) =>
            new Collider { IsBox = false, X = x, Z = z, R = r, H = h, What = what };
    }

    public sealed class Screen { public double X, Y, Z, W, H, Tilt; }

    public sealed class Placement
    {
        public string Table;
        /// <summary>table center, footprint (width along X, depth along Z), felt height</summary>
        public double X, Z, W, D, H;
        /// <summary>where you stand to sit down (a circle of UseRadius around it)</summary>
        public V2 Stand;
        /// <summary>the seated camera: eye and look target</summary>
        public V3 Eye, Look;
        /// <summary>the panel's screen; null for none</summary>
        public Screen Screen;
        public double SignY;
        /// <summary>a slot cabinet (its screen is the glass on its front)</summary>
        public bool Cabinet;
    }

    public sealed class Room
    {
        public string Id, Name, Tier;
        public double X0, Z0, X1, Z1, Ceiling;
    }

    /// <summary>A gap in a wall: centered on (X, Z), W wide, in a wall running along Along ("x" or "z").</summary>
    public sealed class Doorway { public double X, Z, W, H; public string Along, To, Sign; }

    /// <summary>Decorations: carousel | slot | roulette | cardtable | craps | poker | booth | piano | chandelier | bulb | rope | plant | stool.</summary>
    public sealed class Prop { public string Kind; public double X, Z; public double? W, D, Rot; public string Room; }

    /// <summary>Where the crowd is, facing Yaw (radians: 0 looks north, -Z).</summary>
    public sealed class NpcSpot
    {
        public string Role; public double X, Z, Yaw; public bool Seated; public string Room; public string Table;
        /// <summary>a Twenty-One regular's id (U29): they play the hands the sim deals them</summary>
        public string Who;
        public V2[] Path;
    }

    public sealed class CollectorTable
    {
        public double X, Z, R, H, SignY;
        public V2 Stand;
        public V3 Eye, Look;
        public Screen Screen;
        public Collider Chair;
    }

    public sealed class Floor
    {
        public int Id;
        public Room[] Rooms;
        public Doorway[] Doorways;
        public double Ceiling;
        public V2 Spawn; public double SpawnYaw;
        public double DoorX, DoorW, DoorH;
        public Collider[] Colliders;
        public Placement[] Tables;
        public CollectorTable Collector;
        public Collider Bar; public V2 BarStand;
        public Collider Cage; public V2 CageStand;
        public Collider[] Pillars;
        public Prop[] Props;
        public NpcSpot[] Npcs;
        public Walls WallBoxes;

        public Placement TableOf(string id) => Tables.FirstOrDefault(t => t.Table == id);
        public Room RoomAt(double x, double z) => Rooms.FirstOrDefault(r => x >= r.X0 && x <= r.X1 && z >= r.Z0 && z <= r.Z1);
    }

    public sealed class Walls { public Collider[] Boxes; }

    public static class Layout
    {
        /// <summary>You can sit at a table from this far from its stand point (m).</summary>
        public const double UseRadius = 1.3;
        public const double CageRadius = 1.8;
        /// <summary>The bar is a long counter.</summary>
        public const double BarRadius = 2.4;
        /// <summary>Eye height standing (m).</summary>
        public const double EyeH = 1.6;
        /// <summary>The player's capsule radius (m).</summary>
        public const double PlayerR = 0.3;
        /// <summary>A person in the crowd, for collision (m).</summary>
        public const double NpcR = 0.28;

        const double Wall = 0.4;
        /// <summary>the seated eye's distance from the dealer's screen (m)</summary>
        const double SeatD = 1.25;

        /// <summary>A table's seat and screen from its footprint: the seat faces -Z, the screen stands on the far edge.</summary>
        static Placement Place(string table, double x, double z, double w, double d, double signY = 2.95, bool screen = true)
        {
            const double h = 0.9;
            // the dealer's screen: 1.28 x 0.72 m, leaning back 18 deg, 1.25 m from the seated eye
            Screen sc = screen ? new Screen { X = x, Y = 1.25, Z = z - d / 2 + 0.12, W = 1.28, H = 0.72, Tilt = 0.31 } : null;
            V3 eye = sc != null ? new V3(x, sc.Y + Math.Sin(sc.Tilt) * SeatD, sc.Z + Math.Cos(sc.Tilt) * SeatD) : new V3(x, 1.25, z + d / 2 + 0.45);
            return new Placement
            {
                Table = table, X = x, Z = z, W = w, D = d, H = h,
                Stand = new V2(x, z + d / 2 + 0.75),
                Eye = eye,
                Look = sc != null ? new V3(x, sc.Y, sc.Z) : new V3(x, h, z),
                Screen = sc,
                SignY = signY,
            };
        }

        /// <summary>A slot cabinet: its screen is the glass on its front, upright, and you sit facing it.</summary>
        static Placement Cabinet(Placement p)
        {
            var sc = new Screen { X = p.X, Y = 1.3, Z = p.Z + p.D / 2 + 0.02, W = 1.28, H = 0.72, Tilt = 0 };
            p.Screen = sc;
            p.Eye = new V3(p.X, sc.Y, sc.Z + SeatD);
            p.Look = new V3(p.X, sc.Y, sc.Z);
            p.Cabinet = true;
            return p;
        }

        static IEnumerable<Collider> WallX(double z, double x0, double x1, Doorway[] gaps, double h, string what = "wall")
        {
            var cuts = gaps.Where(d => d.Along == "x" && Math.Abs(d.Z - z) < 0.01).Select(d => new[] { d.X - d.W / 2, d.X + d.W / 2 }).OrderBy(c => c[0]).ToList();
            double at = x0;
            foreach (var c in cuts)
            {
                if (c[0] > at) yield return Collider.Box(at, z - Wall / 2, c[0], z + Wall / 2, h, what);
                at = c[1];
            }
            if (x1 > at) yield return Collider.Box(at, z - Wall / 2, x1, z + Wall / 2, h, what);
        }

        static IEnumerable<Collider> PropColliders(Prop p)
        {
            if (p.Kind == "chandelier" || p.Kind == "bulb" || p.Kind == "stool") yield break;
            if (p.Kind == "plant") { yield return Collider.Circle(p.X, p.Z, 0.35, 1.2, "plant"); yield break; }
            if (p.Kind == "carousel") { yield return Collider.Circle(p.X, p.Z, (p.W ?? 1.5) + 0.9, 2, "carousel"); yield break; }
            if (p.Kind == "rope") { yield return Collider.Box(p.X - 0.06, p.Z - (p.D ?? 4) / 2, p.X + 0.06, p.Z + (p.D ?? 4) / 2, 1, "rope"); yield break; }
            double w = p.W ?? 1, d = p.D ?? 1;
            yield return Collider.Box(p.X - w / 2, p.Z - d / 2, p.X + w / 2, p.Z + d / 2, 1, p.Kind);
        }

        public static readonly Floor Floor1 = BuildFloor(1);
        /// <summary>Floor 2 (DESIGN.md §11): floor 1's shell (rooms, bar, cage, his table) with its own games,
        /// each where the floor-1 game it replaces stood, and its own names and look.</summary>
        public static readonly Floor Floor2 = BuildFloor(2);

        public static Floor FloorOf(int floor) => floor >= 2 ? Floor2 : Floor1;

        static Floor BuildFloor(int id)
        {
            bool two = id >= 2;
            var rooms = new[]
            {
                new Room { Id = "floor", Name = two ? "THE GOLD ROOM" : "THE FLOOR", Tier = Data.Tier.Low, X0 = -14, Z0 = -2, X1 = 14, Z1 = 18, Ceiling = 5 },
                new Room { Id = "lounge", Name = two ? "THE MIRROR ROOM" : "THE LOUNGE", Tier = Data.Tier.Mid, X0 = -14, Z0 = -16, X1 = -0.2, Z1 = -2, Ceiling = 3.6 },
                new Room { Id = "back", Name = two ? "THE FURNACE" : "THE BACK ROOM", Tier = Data.Tier.High, X0 = 0.2, Z0 = -16, X1 = 14, Z1 = -2, Ceiling = 3.6 },
            };
            var doorways = new[]
            {
                new Doorway { X = -8, Z = -2, W = 2.6, H = 2.8, Along = "x", To = "lounge", Sign = rooms[1].Name },
                new Doorway { X = 8, Z = -2, W = 2.6, H = 2.8, Along = "x", To = "back", Sign = rooms[2].Name },
            };
            var tables = two ? new[]
            {
                // THE GOLD ROOM: a real roulette wheel where floor 1's was for show, craps where Twenty-One stood
                Place("roulette", -6.5, 4.5, 2.8, 1.5, 3.6),
                Place("craps", 6.5, 5, 3.2, 1.6, 3.6),
                // THE MIRROR ROOM
                Cabinet(Place("pegdrop", -10.5, -8, 1.6, 0.9)),
                Place("hilo", -4, -8, 1.8, 1.1),
                // THE FURNACE
                Place("crash", 3.6, -7, 2.2, 1.2),
                Place("redline", 11, -7, 2.2, 1.2),
            } : new[]
            {
                // THE FLOOR: the Bandit sits in a bank of slots by the entrance, Twenty-One beside the bar's light
                Cabinet(Place("bandit", -6.9, 11, 1.4, 0.9, 3.6)),
                Place("twentyone", 6.5, 5, 2.2, 1.2, 3.6),
                // THE LOUNGE
                Place("hilo", -10.5, -8, 1.8, 1.1),
                Place("wheel", -4, -8, 2.0, 1.2),
                // THE BACK ROOM
                Place("redline", 3.6, -7, 2.2, 1.2),
                Place("deadman", 11, -7, 2.0, 1.2),
            };
            var bar = Collider.Box(-13.8, 1, -12.2, 9.5, 1.1, "bar");
            var cage = Collider.Box(12.2, 12.5, 13.8, 16.5, 3.0, "cage");
            var pillars = new[]
            {
                Collider.Circle(-3.5, 8, 0.42, 5, "pillar"), Collider.Circle(3.5, 8, 0.42, 5, "pillar"),
                Collider.Circle(-3.5, 1.5, 0.42, 5, "pillar"), Collider.Circle(3.5, 1.5, 0.42, 5, "pillar"),
            };

            // the Collector's round table, at the back of the back room; his screen lies back on the felt
            double cx = 7.4, cz = -11.8, cr = 1.3;
            var cs = new Screen { X = cx, Y = 1.18, Z = cz + 0.2, W = 1.28, H = 0.72, Tilt = 0.6 };
            var collector = new CollectorTable
            {
                X = cx, Z = cz, R = cr, H = 0.85, Stand = new V2(cx, cz + cr + 0.75), SignY = 3.0, Screen = cs,
                Eye = new V3(cx, cs.Y + Math.Sin(cs.Tilt) * SeatD, cs.Z + Math.Cos(cs.Tilt) * SeatD),
                Look = new V3(cx, cs.Y, cs.Z),
                Chair = Collider.Box(cx - 0.55, cz - cr - 0.55 - 0.375, cx + 0.55, cz - cr - 0.55 - 0.025, 2.55, "collectorchair"),
            };

            const double slotZ = 11;
            Func<double[], IEnumerable<Prop>> slots = xs => xs.Select(x => new Prop { Kind = "slot", X = x, Z = slotZ, W = 1.4, D = 0.9, Room = "floor" });
            var props = new List<Prop>();
            // the banks of slots either side of the entrance aisle (the Bandit is one of the west bank)
            props.AddRange(slots(two ? new[] { -11.4, -9.9, -8.4, -6.9, -5.4, -3.9 } : new[] { -11.4, -9.9, -8.4, -5.4, -3.9 }));
            props.AddRange(slots(new[] { 3.9, 5.4, 6.9, 8.4, 9.9 }));
            props.AddRange(new[]
            {
                // the centerpiece: a ring of slots round a progressive jackpot tower
                new Prop { Kind = "carousel", X = 0, Z = 5.6, W = 1.5, Room = "floor" },

                new Prop { Kind = "cardtable", X = 10.4, Z = 4.5, W = 2.2, D = 1.2, Room = "floor" },
                new Prop { Kind = "cardtable", X = -4.6, Z = 0.3, W = 2.2, D = 1.2, Room = "floor" },
                new Prop { Kind = "chandelier", X = 0, Z = 9.5, Room = "floor" },
                new Prop { Kind = "chandelier", X = 0, Z = 3.5, Room = "floor" },
                new Prop { Kind = "chandelier", X = -8, Z = 7, Room = "floor" },
                new Prop { Kind = "chandelier", X = 8, Z = 7, Room = "floor" },
                // velvet ropes along the entrance aisle
                new Prop { Kind = "rope", X = -1.7, Z = 15, D = 4, Room = "floor" },
                new Prop { Kind = "rope", X = 1.7, Z = 15, D = 4, Room = "floor" },
                new Prop { Kind = "plant", X = -13, Z = 16.8, Room = "floor" },
                new Prop { Kind = "plant", X = 11.2, Z = 16.8, Room = "floor" },
                new Prop { Kind = "plant", X = -13, Z = -0.8, Room = "floor" },
                new Prop { Kind = "plant", X = 13, Z = -0.8, Room = "floor" },
            });
            props.AddRange(new[] { 2.5, 4, 5.5, 7, 8.5 }.Select(z => new Prop { Kind = "stool", X = -11.7, Z = z, Room = "floor" }));
            props.AddRange(new[]
            {
                // THE LOUNGE: a craps rail, booths, a piano
                new Prop { Kind = "craps", X = -7.2, Z = -13, W = 3.2, D = 1.6, Room = "lounge" },
                new Prop { Kind = "booth", X = -13.2, Z = -5, W = 1.2, D = 2.4, Room = "lounge" },
                new Prop { Kind = "booth", X = -13.2, Z = -11, W = 1.2, D = 2.4, Room = "lounge" },
                new Prop { Kind = "piano", X = -2.2, Z = -14, W = 1.6, D = 1.2, Room = "lounge" },
                new Prop { Kind = "chandelier", X = -7.2, Z = -8.5, Room = "lounge" },
                new Prop { Kind = "plant", X = -1.1, Z = -3, Room = "lounge" },
                // THE BACK ROOM: a poker table under a bare bulb, bulbs over the blood tables
                new Prop { Kind = "poker", X = 3.4, Z = -13.6, W = 2.4, D = 1.4, Room = "back" },
                new Prop { Kind = "bulb", X = 3.4, Z = -13.6, Room = "back" },
                new Prop { Kind = "bulb", X = 3.6, Z = -7, Room = "back" },
                new Prop { Kind = "bulb", X = 11, Z = -7, Room = "back" },
            });

            const double N = 0, S = Math.PI, E = -Math.PI / 2, Wd = Math.PI / 2;
            Func<string, double, double, double, bool, string, string, NpcSpot> npc = (role, x, z, yaw, seated, room, table) =>
                new NpcSpot { Role = role, X = x, Z = z, Yaw = yaw, Seated = seated, Room = room, Table = table };
            var npcs = new List<NpcSpot>();
            // at the slots (west bank, east bank): seated, facing their machines
            npcs.AddRange(new[] { -11.4, -8.4, -5.4 }.Select(x => npc("slots", x, slotZ + 1.05, N, true, "floor", null)));
            npcs.AddRange(new[] { 3.9, 6.9, 8.4, 9.9 }.Select(x => npc("slots", x, slotZ + 1.05, N, true, "floor", null)));
            npcs.AddRange(new[]
            {
                // round the roulette wheel and the card tables, and their dealers
                npc("table", -7.6, 5.75, N, true, "floor", null), npc("table", -5.4, 5.75, N, true, "floor", null),
                npc("table", -8.4, 4.5, E, true, "floor", null), npc("dealer", -6.5, 3.35, S, false, "floor", null),
                npc("table", 9.6, 5.6, N, true, "floor", null), npc("table", 11.2, 5.6, N, true, "floor", null),
                npc("dealer", 10.4, 3.5, S, false, "floor", null),
                npc("table", -5.4, 1.4, N, true, "floor", null), npc("dealer", -4.6, -0.7, S, false, "floor", null),
                npc("dealer", 6.5, 3.95, S, false, "floor", "twentyone"),
            });
            // Twenty-One's regulars (U29): first base on your right, third base on your left, at the table's edge
            if (!two)
            {
                var dot = npc("table", 7.32, 5.62, N - 0.3, true, "floor", "twentyone"); dot.Who = "dot";
                var ike = npc("table", 5.68, 5.62, N + 0.3, true, "floor", "twentyone"); ike.Who = "ike";
                npcs.Add(dot); npcs.Add(ike);
            }
            // the bar: the bartender behind it, drinkers at it
            var bartender = npc("bartender", -13.3, 5, Wd, false, "floor", null);
            bartender.Path = new[] { new V2(-13.3, 2), new V2(-13.3, 8.6) };
            npcs.Add(bartender);
            npcs.AddRange(new[]
            {
                npc("drinker", -11.7, 2.5, E, true, "floor", null), npc("drinker", -11.7, 8.5, E, true, "floor", null),
                npc("drinker", -10.8, 7.2, E + 0.4, false, "floor", null), npc("smoker", 12.8, 0.6, Wd + 0.3, false, "floor", null),
                // railbirds: they watch whoever plays the table beside them
                npc("watcher", 4.6, 7, N + 0.5, false, "floor", "twentyone"), npc("watcher", 8.7, 7.1, N - 0.5, false, "floor", "twentyone"),
                npc("watcher", -8.6, 13.2, N + 0.7, false, "floor", "bandit"),
            });
            // walkers: they pace out and back along clear lines across the floor
            var w1 = npc("walker", -1, 12.6, N, false, "floor", null);
            w1.Path = new[] { new V2(-1, 12.6), new V2(-1, 9.7), new V2(-9.6, 9.7), new V2(-9.6, 7.6) };
            var w2 = npc("walker", 2.5, 2.4, N, false, "floor", null);
            w2.Path = new[] { new V2(2.5, 2.4), new V2(2.5, 9.8), new V2(11, 9.8) };
            npcs.Add(w1); npcs.Add(w2);
            // at the carousel: three players on its outward-facing machines
            foreach (var k in new[] { 0, 2, 5 })
            {
                double a = (k / 8.0) * Math.PI * 2, r = 1.5 + 0.45 + 0.75;
                npcs.Add(npc("slots", Math.Sin(a) * r, 5.6 + Math.Cos(a) * r, a, true, "floor", null));
            }
            npcs.AddRange(new[]
            {
                // THE LOUNGE
                npc("table", -8.4, -11.75, N, false, "lounge", null), npc("table", -6, -11.75, N, false, "lounge", null),
                npc("dealer", -7.2, -14.2, S, false, "lounge", null),
                npc("booth", -13.45, -4.4, E, true, "lounge", null), npc("booth", -13.45, -5.8, E, true, "lounge", null),
                npc("booth", -13.45, -11, E, true, "lounge", null), npc("pianist", -2.2, -13, N, true, "lounge", null),
                npc("watcher", -12, -6.4, N + 0.4, false, "lounge", "hilo"), npc("watcher", -9, -6.1, N - 0.5, false, "lounge", "hilo"),
                npc("watcher", -2.4, -6.1, N - 0.5, false, "lounge", "wheel"), npc("watcher", -5.6, -6, N + 0.5, false, "lounge", "wheel"),
                npc("drinker", -1.4, -9.5, Wd, false, "lounge", null),
                // THE BACK ROOM
                npc("table", 2.6, -12.6, N, true, "back", null), npc("table", 4.2, -12.6, N, true, "back", null),
                npc("table", 4.9, -13.6, Wd, true, "back", null),
                npc("smoker", 1, -9.4, E, false, "back", null), npc("smoker", 13.3, -3.2, Wd + 0.6, false, "back", null),
                npc("smoker", 13.2, -14.6, Wd - 0.5, false, "back", null),
                npc("watcher", 1.9, -5.2, N + 0.6, false, "back", "redline"), npc("watcher", 5.4, -5.1, N - 0.6, false, "back", "redline"),
                npc("watcher", 9.3, -5.2, N + 0.6, false, "back", "deadman"), npc("watcher", 12.6, -5.3, N - 0.5, false, "back", "deadman"),
            });

            // floor 1's decorative roulette wheel (floor 2 plays a real one on that spot)
            if (!two) props.Add(new Prop { Kind = "roulette", X = -6.5, Z = 4.5, W = 2.8, D = 1.5, Room = "floor" });
            // the crowd watches the game that stands where theirs did
            if (two)
            {
                var swap = new Dictionary<string, string> { { "twentyone", "craps" }, { "bandit", "roulette" }, { "hilo", "pegdrop" }, { "wheel", "hilo" }, { "redline", "crash" }, { "deadman", "redline" } };
                foreach (var n in npcs) if (n.Table != null && swap.TryGetValue(n.Table, out var to)) n.Table = to;
                // the croupier at the real wheel works it: she sends the ball round when a spin goes up
                foreach (var n in npcs) if (n.Role == "dealer" && n.X == -6.5 && n.Z == 3.35) n.Table = "roulette";
                // a crowd for Crash (U32): three more railbirds at the Furnace's monitor, clear of the walk in
                npcs.Add(npc("watcher", 1.45, -6.3, E + 0.3, false, "back", "crash"));
                npcs.Add(npc("watcher", 5.8, -6.2, Wd - 0.3, false, "back", "crash"));
                npcs.Add(npc("watcher", 6.3, -4.8, Wd - 0.7, false, "back", "crash"));
            }

            var walls = new List<Collider>();
            walls.AddRange(WallX(18, -14.2, 14.2, new Doorway[0], 5));
            walls.AddRange(WallX(-16, -14.2, 14.2, new Doorway[0], 3.6));
            walls.AddRange(WallX(-2, -14.2, 14.2, doorways, 5));
            walls.Add(Collider.Box(-14.2, -16, -13.8, 18, 5, "wall"));
            walls.Add(Collider.Box(13.8, -16, 14.2, 18, 5, "wall"));
            walls.Add(Collider.Box(-0.2, -16, 0.2, -2, 3.6, "wall"));

            var cols = new List<Collider>();
            cols.AddRange(walls);
            cols.Add(bar);
            cols.Add(cage);
            cols.AddRange(pillars);
            cols.AddRange(tables.Select(t => Collider.Box(t.X - t.W / 2, t.Z - t.D / 2, t.X + t.W / 2, t.Z + t.D / 2, t.H, t.Table)));
            cols.Add(Collider.Circle(collector.X, collector.Z, collector.R, collector.H, "collector"));
            cols.Add(collector.Chair);
            cols.AddRange(props.SelectMany(PropColliders));
            // the crowd stands where it stands (walkers move, and you can brush past them)
            cols.AddRange(npcs.Where(n => n.Role != "walker" && n.Role != "bartender").Select(n => Collider.Circle(n.X, n.Z, NpcR, 1.7, "npc")));

            return new Floor
            {
                Id = id, Rooms = rooms, Doorways = doorways, Ceiling = 5,
                Spawn = new V2(0, 16.6), SpawnYaw = 0,
                DoorX = 0, DoorW = 2.4, DoorH = 3,
                Colliders = cols.ToArray(), Tables = tables, Collector = collector,
                Bar = bar, BarStand = new V2(bar.X1 + 1, (bar.Z0 + bar.Z1) / 2),
                Cage = cage, CageStand = new V2(cage.X0 - 0.9, (cage.Z0 + cage.Z1) / 2),
                Pillars = pillars, Props = props.ToArray(), Npcs = npcs.ToArray(),
                WallBoxes = new Walls { Boxes = walls.ToArray() },
            };
        }
    }

    /// <summary>
    /// The house (DECISIONS #79): where a run starts and where you come back after every Collector.
    /// Far east of the casino (HomeX), so the two are never drawn together.
    /// </summary>
    public static class Home
    {
        public const double HomeX = 200;
        static double Ox(double x) => HomeX + x;
        static Collider B(double x0, double z0, double x1, double z1, double h, string what) => Collider.Box(Ox(x0), z0, Ox(x1), z1, h, what);
        const double W = 0.2;

        /// <summary>The house's interior: x -6..6, z -5..1, a 2.8 m ceiling; the front door a gap at x -0.6..0.6.</summary>
        public static readonly double HouseX0 = Ox(-6), HouseX1 = Ox(6), HouseZ0 = -5, HouseZ1 = 1, HouseCeiling = 2.8, DoorX = Ox(0), DoorW = 1.2, DoorH = 2.1;
        /// <summary>The yard and the street: out to the hedges and the far curb.</summary>
        public static readonly double YardX0 = Ox(-9.5), YardX1 = Ox(9.5), YardZ0 = 1, YardZ1 = 16, Street = 12.5;

        public static readonly Collider Couch = B(-3.6, -3, -2.4, -1, 0.8, "couch");
        public static readonly Collider Coffee = B(-4.7, -2.5, -4.1, -1.5, 0.45, "coffee table");
        public static readonly Collider Tv = B(-6, -2.7, -5.6, -1.3, 1.1, "tv");
        public static readonly Collider Desk = B(3.6, -5, 5.6, -4.3, 0.76, "desk");
        public static readonly Collider Counter = B(0.8, -5, 2.8, -4.4, 0.92, "counter");
        public static readonly Collider Fridge = B(-0.2, -5, 0.6, -4.3, 1.8, "fridge");
        public static readonly Collider Shelf = B(5.6, -3, 6, -0.5, 1.9, "shelf");
        public static readonly Collider Car = B(4.1, 3.9, 5.9, 8.1, 1.4, "car");
        public static readonly Collider Mailbox = Collider.Circle(Ox(-3), 11.6, 0.18, 1.2, "mailbox");
        /// <summary>the rug (not a collider): center and size</summary>
        public static readonly double RugX = Ox(-4), RugZ = -2, RugW = 3, RugD = 2.6;

        public static readonly V2 ComputerStand = new V2(Ox(4.6), -3.5);
        public static readonly V2 MailboxStand = new V2(Ox(-3), 10.8);
        public static readonly V2 CarStand = new V2(Ox(3.5), 5.6);
        public const double UseR = 1.2;

        /// <summary>Where a player stands at the start of a run and after the drive home.</summary>
        public static readonly V2 Spawn = new V2(Ox(-0.8), -1.6);
        public const double SpawnYaw = Math.PI / 2;

        /// <summary>The family: presentation only, like the crowd (DECISIONS #66).</summary>
        public static readonly NpcSpot[] Family =
        {
            new NpcSpot { Role = "booth", X = Ox(-2.9), Z = -2.3, Yaw = Math.PI / 2, Seated = true, Room = "home" },
            new NpcSpot { Role = "walker", X = Ox(1), Z = -3.7, Yaw = 0, Seated = false, Room = "home", Path = new[] { new V2(Ox(1), -3.7), new V2(Ox(2.6), -3.7), new V2(Ox(2.6), -2.6) } },
            new NpcSpot { Role = "watcher", X = Ox(-1.2), Z = 0.2, Yaw = Math.PI, Seated = false, Room = "home" },
        };

        public static readonly Collider[] Colliders = BuildColliders();

        static Collider[] BuildColliders()
        {
            var c = new List<Collider>
            {
                // the house's walls, the front one with the door in it
                B(-6 - W, -5 - W, 6 + W, -5, 3, "wall"),
                B(-6 - W, -5, -6, 1 + W, 3, "wall"),
                B(6, -5, 6 + W, 1 + W, 3, "wall"),
                B(-6 - W, 1, -0.6, 1 + W, 3, "wall"),
                B(0.6, 1, 6 + W, 1 + W, 3, "wall"),
                // the yard's hedges and the far side of the street
                B(-9.5 - W, 1, -9.5, 16, 1.2, "hedge"),
                B(9.5, 1, 9.5 + W, 16, 1.2, "hedge"),
                B(-9.5, 16, 9.5, 16 + W, 1.2, "curb"),
                // the house's outside walls block the yard behind it
                B(-9.5, -5.2, -6.2, 1.2, 3, "house"),
                B(6.2, -5.2, 9.5, 1.2, 3, "house"),
                Couch, Coffee, Tv, Desk, Counter, Fridge, Shelf, Car, Mailbox,
            };
            c.AddRange(Family.Where(n => n.Role != "walker").Select(n => Collider.Circle(n.X, n.Z, Layout.NpcR, 1.7, "npc")));
            return c.ToArray();
        }

        /// <summary>The post: how long an order takes to reach the mailbox (s of time at home).</summary>
        public static readonly Num PostS = new Num(6, "DESIGN.md §5: long enough to walk from the desk to the curb, short enough not to wait on");
    }
}
