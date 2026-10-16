// The house (DESIGN.md §5; TS DECISIONS #79; U22): where a run starts and where you come back after every
// Collector, built from the same data the sim collides against (Vig.Sim.Data.Home): the living room with
// the couch and the television, the kitchen corner, the desk with the computer you order charms on, the
// front door; outside, the yard and its path, the hedges, the mailbox by the curb (its flag goes up when
// the post comes), your car in the driveway, the street under a lamp. Night, always.
using System.Collections.Generic;
using UnityEngine;
using Vig.Sim.Data;
using Collider = Vig.Sim.Data.Collider;

namespace Vig.Client
{
    public sealed class HouseBuilder
    {
        public readonly Transform Root;
        public Transform Flag, ComputerScreen, TvScreen;
        public readonly List<Light> Lights = new List<Light>();
        static Vector3 U(double x, double y, double z) => RoomBuilder.U(x, y, z);
        static Vector2 P(float r, float y) => new Vector2(r, y);

        HouseBuilder(Transform parent)
        {
            Root = new GameObject("The House").transform;
            Root.SetParent(parent, false);
        }

        GameObject Part(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Transform parent = null, bool shadows = true) =>
            Props.Part(name, mesh, mat, parent ? parent : Root, pos, rot, shadows);

        static Mesh Box(float x, float y, float z, float r = 0.02f, float uv = 1) => MeshKit.RoundedBox(x, y, z, r, 3, uv);

        Light Lamp(string name, LightType type, Color c, float intensity, float range, Vector3 pos, bool shadows = false, float angle = 90, Vector3? dir = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            if (dir.HasValue) go.transform.rotation = Quaternion.LookRotation(dir.Value);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.5f; }
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            Lights.Add(l);
            return l;
        }

        /// <summary>A box collider of the sim's, drawn: the furniture is exactly where you bump into it.</summary>
        GameObject Solid(Collider c, Material m, float r = 0.03f, float h = -1)
        {
            float w = (float)(c.X1 - c.X0), d = (float)(c.Z1 - c.Z0), hh = h > 0 ? h : (float)c.H;
            return Part(c.What, Box(w, hh, d, r), m, U((c.X0 + c.X1) / 2, hh / 2, (c.Z0 + c.Z1) / 2));
        }

        static Material M(string key, Color c, float smooth, float metal = 0, Texture2D tex = null) => Look.Surface(key, c, smooth, metal, tex);

        public static HouseBuilder Build(Transform parent)
        {
            var b = new HouseBuilder(parent);
            b.Interior();
            b.Outside();
            return b;
        }

        void Interior()
        {
            float x0 = (float)Home.HouseX0, x1 = (float)Home.HouseX1, z0 = (float)Home.HouseZ0, z1 = (float)Home.HouseZ1, h = (float)Home.HouseCeiling;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            var wood = M("homeFloor", new Color(0.55f, 0.36f, 0.22f), 0.45f, 0, Textures.Planks());
            var wall = M("homeWall", new Color(0.95f, 0.86f, 0.72f), 0.25f, 0, Textures.Damask());
            Part("floor", MeshKit.RoundedBox(x1 - x0, 0.1f, z1 - z0, 0.001f, 1, 0.4f), wood, U(cx, -0.05, cz), null, null, false);
            Part("ceiling", MeshKit.RoundedBox(x1 - x0, 0.1f, z1 - z0, 0.001f), M("homeCeiling", new Color(0.8f, 0.78f, 0.72f), 0.2f), U(cx, h + 0.05, cz), null, null, false);
            // the walls (the sim's boxes), with a gap for the door in the front one
            // the walls (the sim's boxes) in siding, with wallpaper skins on their inner faces
            var siding = M("siding", new Color(0.55f, 0.58f, 0.62f), 0.3f, 0, Textures.Plaster());
            foreach (var c in Home.Colliders)
            {
                if (!c.IsBox || c.What != "wall") continue;
                Solid(c, siding, 0.005f);
                float w = (float)(c.X1 - c.X0), d = (float)(c.Z1 - c.Z0);
                // which face looks into the house: toward its middle
                var mid = new Vector2((float)((c.X0 + c.X1) / 2), (float)((c.Z0 + c.Z1) / 2));
                bool alongX = w > d;
                float inward = alongX ? Mathf.Sign(cz - mid.y) : Mathf.Sign(cx - mid.x);
                var skinPos = alongX ? U(mid.x, h / 2, alongX ? (inward > 0 ? c.Z1 + 0.006 : c.Z0 - 0.006) : mid.y)
                    : U(inward > 0 ? c.X1 + 0.006 : c.X0 - 0.006, h / 2, mid.y);
                var size = alongX ? new Vector3(w, h, 0.01f) : new Vector3(0.01f, h, d);
                Part("wallpaper", MeshKit.RoundedBox(size.x, size.y, size.z, 0.002f, 1, 1 / 1.6f), wall, skinPos, null, null, false);
            }
            foreach (var c in Home.Colliders)
                if (c.IsBox && c.What == "house") Solid(c, M("siding", new Color(0.35f, 0.38f, 0.42f), 0.3f, 0, Textures.Plaster()), 0.01f);
            // the skirting, a window either side of the door looking out on the yard
            foreach (var wx in new[] { -3.5, 3.0 })
            {
                Part("window", Box(1.6f, 1.1f, 0.06f, 0.01f), M("night glass", new Color(0.02f, 0.03f, 0.08f), 0.95f), U(Home.HomeX + wx, 1.5, z1), null, null, false);
                Part("window frame", MeshKit.Tube(MeshKit.RoundRect(1.6f, 1.1f, 0.02f), 0.02f, 6, true), M("frameWhite", new Color(0.85f, 0.85f, 0.8f), 0.5f), U(Home.HomeX + wx, 1.5, z1 - 0.05), null, null, false);
            }
            // the front door, ajar
            Part("door", Box(1.0f, 2.05f, 0.05f, 0.01f), M("doorWood", new Color(0.3f, 0.12f, 0.06f), 0.6f), U(Home.HomeX - 0.55, 1.03, z1 - 0.45), Quaternion.Euler(0, 70, 0), null);
            // the living room: the rug, the couch, the coffee table, the television
            Part("rug", MeshKit.RoundedBox((float)Home.RugW, 0.012f, (float)Home.RugD, 0.004f), Look.Velvet, U(Home.RugX, 0.006, Home.RugZ), null, null, false);
            var couch = Home.Couch;
            float ccx = (float)(couch.X0 + couch.X1) / 2, ccz = (float)(couch.Z0 + couch.Z1) / 2;
            var fabric = M("couchFabric", new Color(0.18f, 0.22f, 0.32f), 0.25f, 0, null);
            Part("couch seat", Box(1.1f, 0.42f, 2.0f, 0.08f), fabric, U(ccx, 0.21, ccz));
            Part("couch back", Box(0.25f, 0.85f, 2.0f, 0.1f), fabric, U(couch.X1 - 0.12, 0.42, ccz));
            foreach (var az in new[] { couch.Z0 + 0.1, couch.Z1 - 0.1 }) Part("couch arm", Box(1.1f, 0.62f, 0.2f, 0.08f), fabric, U(ccx, 0.31, az));
            Solid(Home.Coffee, M("coffeeWood", new Color(0.3f, 0.18f, 0.1f), 0.7f), 0.02f);
            var tv = Home.Tv;
            Solid(tv, M("tvStand", new Color(0.12f, 0.1f, 0.09f), 0.6f), 0.02f, 0.5f);
            TvScreen = Part("tv screen", Box(0.06f, 0.62f, 1.1f, 0.01f), Look.Screen("tvGlow", new Color(0.25f, 0.45f, 0.9f) * 1.4f), U((tv.X0 + tv.X1) / 2, 0.95, (tv.Z0 + tv.Z1) / 2), null, null, false).transform;
            Lamp("tv light", LightType.Point, new Color(0.45f, 0.6f, 1f), 1.4f, 3.2f, U(tv.X1 + 0.4, 1.0, (tv.Z0 + tv.Z1) / 2));
            // the kitchen corner: the counter, the fridge; the shelf of books
            Solid(Home.Counter, M("counterTop", new Color(0.75f, 0.73f, 0.7f), 0.7f), 0.02f);
            Solid(Home.Fridge, M("fridge", new Color(0.85f, 0.86f, 0.86f), 0.8f, 0.2f), 0.04f);
            Solid(Home.Shelf, M("shelfWood", new Color(0.35f, 0.22f, 0.12f), 0.5f), 0.02f);
            var bookCols = new[] { new Color(0.5f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.45f), new Color(0.2f, 0.4f, 0.2f), new Color(0.6f, 0.5f, 0.3f) };
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 9; k++)
                    Part("book", Box(0.22f, 0.24f + (k % 3) * 0.03f, 0.06f, 0.005f), M("book" + (k + row) % 4, bookCols[(k + row) % 4], 0.4f), U(Home.Shelf.X0 + 0.15, 0.55 + row * 0.5, Home.Shelf.Z0 + 0.2 + k * 0.25), null, null, false);
            // the desk and the computer: its screen is the shop (the light you'd walk to)
            var desk = Home.Desk;
            Solid(desk, M("deskWood", new Color(0.4f, 0.25f, 0.13f), 0.55f), 0.02f);
            float dx = (float)(desk.X0 + desk.X1) / 2, dz = (float)(desk.Z0 + desk.Z1) / 2;
            Part("monitor", Box(0.62f, 0.4f, 0.04f, 0.01f), Look.BlackGloss, U(dx, 1.05, dz), null, null);
            ComputerScreen = Part("monitor screen", MeshKit.Quad(0.56f, 0.34f), Look.Screen("pcGlow", new Color(0.3f, 0.95f, 0.55f) * 1.3f), U(dx, 1.05, dz + 0.025), Quaternion.Euler(0, 180, 0), null, false).transform;
            Part("monitor foot", MeshKit.Lathe(new[] { P(0.12f, 0), P(0.12f, 0.01f), P(0.02f, 0.02f), P(0.02f, 0.25f) }, 16), Look.BlackGloss, U(dx, 0.76, dz - 0.05), null, null);
            Part("keyboard", Box(0.45f, 0.02f, 0.15f, 0.005f), Look.BlackGloss, U(dx, 0.78, dz + 0.22), null, null, false);
            Lamp("screen glow", LightType.Point, new Color(0.4f, 1f, 0.6f), 0.9f, 2.4f, U(dx, 1.1, dz + 0.5));
            // the room's light: a ceiling lamp, a floor lamp by the couch
            Part("ceiling lamp", MeshKit.Lathe(new[] { P(0.0f, -0.12f), P(0.25f, -0.1f), P(0.3f, 0), P(0, 0.02f) }, 24), Look.Shade, U(cx, h - 0.05, cz), null, null, false);
            Lamp("ceiling lamp", LightType.Point, new Color(1f, 0.82f, 0.6f), 14f, 10f, U(cx, h - 0.35, cz), true);
            Lamp("kitchen light", LightType.Point, new Color(1f, 0.9f, 0.75f), 8f, 6f, U(Home.HomeX + 1.8, h - 0.3, -4.0));
            Lamp("living light", LightType.Point, new Color(1f, 0.75f, 0.5f), 7f, 6f, U(Home.HomeX - 4, h - 0.3, -2.0));
            Part("floor lamp", MeshKit.Lathe(new[] { P(0.15f, 0), P(0.15f, 0.02f), P(0.015f, 0.04f), P(0.015f, 1.5f), P(0.18f, 1.52f), P(0.12f, 1.75f), P(0, 1.76f) }, 18), Look.Shade, U(couch.X0 - 0.2, 0, couch.Z0 - 0.3), null, null, false);
            Lamp("floor lamp", LightType.Point, new Color(1f, 0.7f, 0.45f), 2.5f, 4.5f, U(couch.X0 - 0.2, 1.6, couch.Z0 - 0.3));
        }

        void Outside()
        {
            float yx0 = (float)Home.YardX0, yx1 = (float)Home.YardX1, yz0 = (float)Home.YardZ0, yz1 = (float)Home.YardZ1, street = (float)Home.Street;
            float cx = (yx0 + yx1) / 2;
            var grass = M("grass", new Color(0.14f, 0.3f, 0.12f), 0.1f, 0, Textures.FeltNoise());
            Part("lawn", MeshKit.RoundedBox(yx1 - yx0, 0.1f, street - 1.2f - yz0, 0.001f, 1, 1), grass, U(cx, -0.05, (yz0 + street - 1.2f) / 2), null, null, false);
            Part("sidewalk", MeshKit.RoundedBox(yx1 - yx0 + 6, 0.12f, 1.2f, 0.01f, 1, 1), M("concrete", new Color(0.4f, 0.4f, 0.38f), 0.2f, 0, Textures.Plaster()), U(cx, -0.04, street - 0.6f), null, null, false);
            Part("street", MeshKit.RoundedBox(yx1 - yx0 + 30, 0.1f, yz1 - street + 2, 0.001f, 1, 1), M("asphalt", new Color(0.06f, 0.06f, 0.07f), 0.35f, 0, Textures.Plaster()), U(cx, -0.07, street + (yz1 - street) / 2 + 0.5), null, null, false);
            for (float x = yx0 - 12; x < yx1 + 12; x += 3)
                Part("lane line", Box(1.4f, 0.01f, 0.12f, 0.003f), M("paint", new Color(0.8f, 0.7f, 0.2f), 0.4f), U(x, -0.015, street + 2.3), null, null, false);
            Part("path", MeshKit.RoundedBox(1.1f, 0.06f, street - 1.2f - yz0, 0.01f), M("pavers", new Color(0.35f, 0.32f, 0.3f), 0.25f, 0, Textures.Plaster()), U(Home.DoorX, -0.01, (yz0 + street - 1.2f) / 2), null, null, false);
            var car = Home.Car;
            Part("driveway", MeshKit.RoundedBox(3f, 0.06f, street - 1.2f - yz0, 0.01f), M("concreteDrive", new Color(0.3f, 0.3f, 0.29f), 0.25f, 0, Textures.Plaster()), U((car.X0 + car.X1) / 2, -0.012, (yz0 + street - 1.2f) / 2), null, null, false);
            // the hedges, the roof over the house
            foreach (var c in Home.Colliders)
                if (c.IsBox && c.What == "hedge") Solid(c, M("hedge", new Color(0.04f, 0.12f, 0.05f), 0.15f, 0, Textures.FeltNoise()), 0.25f);
            float hx0 = (float)Home.HouseX0 - 3.7f, hx1 = (float)Home.HouseX1 + 3.7f;
            Part("roof", Box(hx1 - hx0 + 0.6f, 0.25f, 7.2f, 0.05f), M("roofTiles", new Color(0.12f, 0.1f, 0.1f), 0.3f, 0, Textures.Planks()), U((hx0 + hx1) / 2, 3.12, -2), null, null);
            Part("porch light", Props.Bulb(0.06f), Look.Bulb, U(Home.DoorX + 0.9, 2.3, Home.HouseZ1 + 0.25), null, null, false);
            Lamp("porch", LightType.Point, new Color(1f, 0.78f, 0.5f), 9f, 9f, U(Home.DoorX + 0.9, 2.2, Home.HouseZ1 + 0.6));
            Lamp("yard flood", LightType.Spot, new Color(0.8f, 0.85f, 1f), 10f, 16f, U(Home.HomeX, 3.0, Home.HouseZ1 + 0.3), true, 110, new Vector3(0, -0.7f, 1).normalized);
            // the mailbox: a post, the box, its flag (up when there's post: HomeView raises it)
            var mb = Home.Mailbox;
            Part("mailbox post", Box(0.09f, 1.0f, 0.09f, 0.01f), M("postWood", new Color(0.3f, 0.2f, 0.12f), 0.4f), U(mb.X, 0.5, mb.Z), null, null);
            Part("mailbox", Box(0.24f, 0.24f, 0.46f, 0.1f), M("mailboxMetal", new Color(0.12f, 0.15f, 0.2f), 0.6f, 0.6f), U(mb.X, 1.12, mb.Z), null, null);
            Flag = Props.Group("flag", Root, U(mb.X + 0.13, 1.05, mb.Z + 0.1));
            Part("flag", Box(0.02f, 0.18f, 0.08f, 0.005f), Look.Surface("flagRed", new Color(0.8f, 0.05f, 0.05f), 0.5f), new Vector3(0, 0.09f, 0.04f), null, Flag);
            // the car: a long low sedan in the driveway, nose to the street
            float carX = (float)(car.X0 + car.X1) / 2, carZ = (float)(car.Z0 + car.Z1) / 2;
            var paint = M("carPaint", new Color(0.35f, 0.02f, 0.06f), 0.88f, 0.5f);
            var carT = Props.Group("car", Root, U(carX, 0, carZ));
            Part("body", Box(1.8f, 0.55f, 4.3f, 0.2f), paint, new Vector3(0, 0.55f, 0), null, carT);
            Part("cabin", Box(1.6f, 0.5f, 2.2f, 0.2f), M("carGlass", new Color(0.02f, 0.03f, 0.05f), 0.95f), new Vector3(0, 1.05f, 0.2f), null, carT);
            foreach (var wx in new[] { -0.85f, 0.85f })
                foreach (var wz in new[] { -1.35f, 1.35f })
                    Part("wheel", MeshKit.Lathe(new[] { P(0, -0.11f), P(0.34f, -0.11f), P(0.36f, 0), P(0.34f, 0.11f), P(0, 0.11f) }, 24), M("tire", new Color(0.03f, 0.03f, 0.03f), 0.3f), new Vector3(wx, 0.36f, wz), Quaternion.Euler(0, 0, 90), carT);
            foreach (var hx in new[] { -0.6f, 0.6f })
            {
                Part("headlight", Props.Bulb(0.07f), Look.Bulb, new Vector3(hx, 0.6f, -2.15f), null, carT, false);
                Part("taillight", Props.Bulb(0.06f), Look.Glow("tail", new Color(1f, 0.05f, 0.05f), 3f), new Vector3(hx, 0.65f, 2.15f), null, carT, false);
            }
            Lamp("headlights", LightType.Spot, new Color(1f, 0.92f, 0.8f), 8f, 14f, U(carX, 0.65, car.Z1 + 0.3), false, 50, new Vector3(0, -0.15f, -1));
            // the street lamp and the moon
            Part("lamp post", MeshKit.Lathe(new[] { P(0.12f, 0), P(0.1f, 0.3f), P(0.05f, 0.4f), P(0.05f, 4.4f), P(0, 4.45f) }, 16), M("lampPost", new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.6f), U(Home.DoorX - 5, 0, street - 0.4), null, null);
            Part("lamp head", Props.Bulb(0.16f), Look.Bulb, U(Home.DoorX - 5, 4.4, street - 0.4), null, null, false);
            Lamp("street lamp", LightType.Spot, new Color(1f, 0.75f, 0.45f), 45f, 14f, U(Home.DoorX - 5, 4.3, street - 0.4), true, 120, Vector3.down);
            Lamp("street lamp 2", LightType.Spot, new Color(1f, 0.75f, 0.45f), 45f, 14f, U(Home.DoorX + 7, 4.3, street - 0.4), false, 120, Vector3.down);
            Part("lamp post 2", MeshKit.Lathe(new[] { P(0.12f, 0), P(0.1f, 0.3f), P(0.05f, 0.4f), P(0.05f, 4.4f), P(0, 4.45f) }, 16), M("lampPost", new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.6f), U(Home.DoorX + 7, 0, street - 0.4), null, null);
            Part("lamp head 2", Props.Bulb(0.16f), Look.Bulb, U(Home.DoorX + 7, 4.4, street - 0.4), null, null, false);
            var moon = Lamp("moon", LightType.Directional, new Color(0.45f, 0.55f, 0.85f), 1.1f, 0, U(Home.HomeX, 20, 0), true);
            moon.transform.rotation = Quaternion.Euler(40, 160, 0);
        }
    }
}
