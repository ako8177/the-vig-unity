// Every texture in the room, drawn by code at load: carpets, damask, planks, wood grain, felt, marble,
// and normal maps from height. Tileable (every pattern wraps on its own period); cached.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vig.Client
{
    public static class Textures
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        // ---------------------------------------------------------------- noise (tileable value noise)

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        /// <summary>Value noise that tiles every `period` cells.</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = Smooth(x - xi), yf = Smooth(y - yi);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period;
            int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
        }

        /// <summary>Fractal noise over a [0,1) tile: base frequency `f` cells across.</summary>
        static float Fbm(float u, float v, int f, int seed, int oct = 4)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                sum += Noise(u * f, v * f, f, seed + i * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        static Texture2D Make(string key, int size, Func<float, float, Color> px, bool linear = false)
        {
            if (cache.TryGetValue(key, out var t) && t) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear) { name = key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var c = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    c[y * size + x] = px((x + 0.5f) / size, (y + 0.5f) / size);
            t.SetPixels32(c);
            t.Apply(true, false);
            cache[key] = t;
            return t;
        }

        /// <summary>A normal map from a tileable height function.</summary>
        static Texture2D NormalFrom(string key, int size, Func<float, float, float> h, float strength)
        {
            if (cache.TryGetValue(key, out var t) && t) return t;
            var hs = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    hs[y * size + x] = h((x + 0.5f) / size, (y + 0.5f) / size);
            return Make(key, size, (u, v) =>
            {
                int x = Mathf.Clamp((int)(u * size), 0, size - 1), y = Mathf.Clamp((int)(v * size), 0, size - 1);
                float l = hs[y * size + (x - 1 + size) % size], r = hs[y * size + (x + 1) % size];
                float d = hs[((y - 1 + size) % size) * size + x], up = hs[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - up) * strength, 1).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }, true);
        }

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        static float Band(float x, float c, float w) => Mathf.Clamp01(1 - Mathf.Abs(x - c) / w);
        static float Frac(float x) => x - Mathf.Floor(x);

        // ---------------------------------------------------------------- carpets

        /// <summary>THE FLOOR: an indigo carpet of gold medallions on a pink-diamond lattice (STYLE.md, DECISIONS #65).</summary>
        public static Texture2D CarpetMedallion() => Make("carpetMedallion", 1024, (u, v) =>
        {
            float n = Fbm(u, v, 64, 3, 3);
            var c = Mix(Look.Indigo, Look.IndigoHi, 0.3f + n * 0.6f) * 1.15f;
            // diamond lattice
            float du = Frac(u * 2) - 0.5f, dv = Frac(v * 2) - 0.5f;
            float diamond = Mathf.Abs(du) + Mathf.Abs(dv);
            c = Mix(c, Look.Pink * 0.4f, Band(diamond, 0.5f, 0.009f));
            // medallions at the lattice nodes
            float cu = Frac(u * 2 + 0.5f) - 0.5f, cv = Frac(v * 2 + 0.5f) - 0.5f;
            float r = Mathf.Sqrt(cu * cu + cv * cv);
            float ang = Mathf.Atan2(cv, cu);
            float petal = 0.17f + 0.035f * Mathf.Cos(ang * 8);
            c = Mix(c, Look.Gold * 0.8f, Band(r, petal, 0.012f));
            c = Mix(c, Look.Gold * 0.7f, Band(r, 0.08f, 0.01f));
            c = Mix(c, Look.Teal * 0.8f, Mathf.Clamp01((0.06f - r) / 0.01f));
            c = Mix(c, Look.Pink * 0.7f, Band(r, 0.125f, 0.008f) * (0.5f + 0.5f * Mathf.Cos(ang * 16)));
            // the pile: fine speckle
            c *= 0.85f + 0.3f * Hash((int)(u * 1024), (int)(v * 1024), 9);
            c.a = 1;
            return c;
        });

        /// <summary>THE LOUNGE: a violet carpet with a small gold lattice.</summary>
        public static Texture2D CarpetLounge() => Make("carpetLounge", 512, (u, v) =>
        {
            float n = Fbm(u, v, 32, 5, 3);
            var c = Mix(Look.Carpet * 0.75f, Look.Carpet * 1.1f, n);
            float gu = Frac(u * 8) - 0.5f, gv = Frac(v * 8) - 0.5f;
            float d = Mathf.Abs(gu) + Mathf.Abs(gv);
            c = Mix(c, Look.Gold * 0.55f, Band(d, 0.32f, 0.03f));
            c = Mix(c, Look.PinkHi * 0.45f, Mathf.Clamp01((0.06f - Mathf.Sqrt(gu * gu + gv * gv)) / 0.02f));
            c *= 0.85f + 0.3f * Hash((int)(u * 512), (int)(v * 512), 11);
            c.a = 1;
            return c;
        });

        public static Texture2D FiberNormal() => NormalFrom("fiberN", 512, (u, v) => Hash((int)(u * 512), (int)(v * 512), 21) * 0.6f + Fbm(u, v, 64, 22, 2) * 0.4f, 1.6f);

        // ---------------------------------------------------------------- floors and walls

        const int PlankRows = 8;
        static float PlankEdge(float u, float v, out int row, out float along)
        {
            row = Mathf.FloorToInt(v * PlankRows);
            float off = Hash(row, 0, 41) * 0.7f;
            along = Frac(u * 2 + off);
            float inRow = Frac(v * PlankRows);
            float edge = Mathf.Min(inRow, 1 - inRow) * 6;
            float end = Mathf.Min(along, 1 - along) * 24;
            return Mathf.Clamp01(Mathf.Min(edge, end));
        }

        /// <summary>THE BACK ROOM: worn dark planks.</summary>
        public static Texture2D Planks() => Make("planks", 1024, (u, v) =>
        {
            float e = PlankEdge(u, v, out int row, out float along);
            float tone = 0.6f + 0.5f * Hash(row, Mathf.FloorToInt(u * 2 + Hash(row, 0, 41) * 0.7f), 43);
            float grain = Fbm(u * 0.25f + row * 0.13f, v, 64, 44, 3);
            float stripes = 0.5f + 0.5f * Mathf.Sin((v * PlankRows * 6 + grain * 8) * Mathf.PI);
            var c = Mix(Look.WoodLo * 0.6f, Look.Wood * 0.9f, grain * 0.7f + stripes * 0.2f) * tone;
            c *= Mathf.Lerp(0.25f, 1, e);
            c.a = 1;
            return c;
        });

        public static Texture2D PlankNormal() => NormalFrom("planksN", 512, (u, v) => PlankEdge(u, v, out _, out _) * 0.8f + Fbm(u, v, 64, 45, 2) * 0.2f, 3f);

        static float DamaskMotif(float u, float v)
        {
            // an ogee: two offset sine curves make the lattice, a flower sits in each cell
            float x = Frac(u * 2) * 2 - 1, y = Frac(v * 1.5f) * 2 - 1;
            float og = Mathf.Abs(Mathf.Abs(x) - 0.55f - 0.35f * Mathf.Cos(y * Mathf.PI));
            float lattice = Mathf.Clamp01(1 - og / 0.07f);
            float r = Mathf.Sqrt(x * x * 1.4f + y * y);
            float ang = Mathf.Atan2(y, x);
            float flower = Mathf.Clamp01(1 - Mathf.Abs(r - (0.32f + 0.1f * Mathf.Cos(ang * 6))) / 0.05f);
            float heart = Mathf.Clamp01((0.12f - r) / 0.03f);
            return Mathf.Max(lattice, Mathf.Max(flower, heart));
        }

        /// <summary>THE FLOOR: cream damask.</summary>
        public static Texture2D Damask() => Make("damask", 512, (u, v) =>
        {
            float m = DamaskMotif(u, v);
            float n = Fbm(u, v, 16, 51, 3);
            var c = Mix(Look.Cream * 0.82f, Look.Cream * 0.62f, n * 0.4f);
            c = Mix(c, new Color(0.62f, 0.36f, 0.24f), m * 0.75f);
            c.a = 1;
            return c;
        });

        public static Texture2D DamaskNormal() => NormalFrom("damaskN", 512, (u, v) => DamaskMotif(u, v) * 0.5f + Hash((int)(u * 512), (int)(v * 512), 52) * 0.15f, 2f);

        /// <summary>THE LOUNGE: deep violet flock.</summary>
        public static Texture2D Flock() => Make("flock", 512, (u, v) =>
        {
            float m = DamaskMotif(u * 1.0f + 0.25f, v);
            float n = Fbm(u, v, 16, 53, 3);
            var c = Mix(new Color(0.16f, 0.05f, 0.2f), new Color(0.22f, 0.07f, 0.27f), n);
            c = Mix(c, new Color(0.4f, 0.12f, 0.36f), m * 0.6f);
            c.a = 1;
            return c;
        });

        static float PlasterH(float u, float v) => Fbm(u, v, 8, 61, 5);

        /// <summary>THE BACK ROOM: stained plaster.</summary>
        public static Texture2D Plaster() => Make("plaster", 512, (u, v) =>
        {
            float h = PlasterH(u, v);
            float stain = Fbm(u, v * 0.5f, 4, 62, 4);
            var c = Mix(new Color(0.22f, 0.17f, 0.15f), new Color(0.36f, 0.3f, 0.26f), h);
            c *= Mathf.Lerp(0.55f, 1, Mathf.SmoothStep(0.3f, 0.7f, stain));
            c.a = 1;
            return c;
        });

        public static Texture2D PlasterNormal() => NormalFrom("plasterN", 512, PlasterH, 4f);

        /// <summary>THE FLOOR's ceiling: dark coffers with gilt edges.</summary>
        public static Texture2D Coffers() => Make("coffers", 512, (u, v) =>
        {
            float x = Frac(u * 2), y = Frac(v * 2);
            float edge = Mathf.Min(Mathf.Min(x, 1 - x), Mathf.Min(y, 1 - y));
            var c = new Color(0.16f, 0.08f, 0.1f);
            c = Mix(c, Look.Gold * 0.6f, Band(edge, 0.06f, 0.012f));
            c = Mix(c, new Color(0.08f, 0.04f, 0.06f), Mathf.Clamp01((edge - 0.08f) * 4));
            c.a = 1;
            return c;
        });

        // ---------------------------------------------------------------- wood, felt, leather, marble

        /// <summary>Orange lacquered wood: the rails, the wainscot, the cabinets.</summary>
        public static Texture2D WoodGrain() => Make("woodGrain", 512, (u, v) =>
        {
            float g = Fbm(u, v * 0.15f, 16, 71, 4);
            float rings = 0.5f + 0.5f * Mathf.Sin((v * 40 + g * 10) * Mathf.PI);
            var c = Mix(Look.WoodLo, Look.WoodHi * 0.85f, 0.35f + rings * 0.3f + g * 0.3f);
            c.a = 1;
            return c;
        });

        public static Texture2D FeltNoise() => Make("feltNoise", 256, (u, v) =>
        {
            float n = 0.85f + 0.3f * Fbm(u, v, 32, 81, 3) + 0.1f * Hash((int)(u * 256), (int)(v * 256), 82);
            return new Color(n, n, n, 1);
        });

        public static Texture2D FeltNormal() => NormalFrom("feltN", 256, (u, v) => Hash((int)(u * 256), (int)(v * 256), 83), 0.8f);

        public static Texture2D LeatherNormal() => NormalFrom("leatherN", 256, (u, v) =>
        {
            // pebbled: cells of noise
            float n = Noise(u * 48, v * 48, 48, 91);
            return Mathf.SmoothStep(0.3f, 0.7f, n);
        }, 1.5f);

        /// <summary>Cream marble with grey veins: the main floor's pillars.</summary>
        public static Texture2D MarbleLight() => Make("marbleLight", 512, (u, v) =>
        {
            float w = Fbm(u, v, 4, 111, 5);
            float vein = Mathf.Abs(Mathf.Sin((u * 2 + v * 3 + w * 5) * Mathf.PI));
            var c = Mix(new Color(0.78f, 0.72f, 0.64f), new Color(0.9f, 0.86f, 0.8f), w);
            c = Mix(c, new Color(0.45f, 0.4f, 0.38f), Mathf.Clamp01((0.05f - vein) / 0.05f) * 0.8f);
            c.a = 1;
            return c;
        });

        /// <summary>THE GOLD ROOM (floor 2): a black carpet of gold art-deco fans.</summary>
        public static Texture2D DecoCarpet() => Make("decoCarpet", 1024, (u, v) =>
        {
            float n = Fbm(u, v, 64, 121, 3);
            var c = Mix(new Color(0.03f, 0.025f, 0.03f), new Color(0.07f, 0.05f, 0.05f), n);
            // fans: concentric arcs in a staggered grid
            float gu = Frac(u * 4 + (Mathf.Floor(v * 4) % 2) * 0.5f), gv = Frac(v * 4);
            float r = Mathf.Sqrt((gu - 0.5f) * (gu - 0.5f) + gv * gv);
            float arcs = Mathf.Abs(Frac(r * 6) - 0.5f);
            if (r < 0.95f) c = Mix(c, Look.Gold * 0.55f, Mathf.Clamp01((0.06f - arcs) / 0.03f) * (1 - r));
            float ray = Mathf.Abs(Frac(Mathf.Atan2(gv, gu - 0.5f) / Mathf.PI * 9) - 0.5f);
            if (r < 0.9f && r > 0.2f) c = Mix(c, Look.Gold * 0.4f, Mathf.Clamp01((0.05f - ray) / 0.03f) * 0.6f);
            c = Mix(c, new Color(0.0f, 0.25f, 0.18f), Band(r, 0.12f, 0.02f) * 0.8f);
            c *= 0.85f + 0.3f * Hash((int)(u * 1024), (int)(v * 1024), 122);
            c.a = 1;
            return c;
        });

        /// <summary>THE GOLD ROOM: black lacquer panels with gold deco lines.</summary>
        public static Texture2D DecoWall() => Make("decoWall", 512, (u, v) =>
        {
            float x = Frac(u * 2), y = Frac(v * 1.2f);
            var c = new Color(0.03f, 0.03f, 0.035f);
            float frame = Mathf.Min(Mathf.Min(x, 1 - x), Mathf.Min(y, 1 - y));
            c = Mix(c, Look.Gold * 0.7f, Band(frame, 0.05f, 0.008f));
            // a stepped chevron in each panel
            float ch = Mathf.Abs(x - 0.5f) + (1 - y) * 0.6f;
            c = Mix(c, Look.Gold * 0.6f, Band(Frac(ch * 4), 0.5f, 0.03f) * (frame > 0.07f ? 1 : 0) * 0.7f);
            c.a = 1;
            return c;
        });

        /// <summary>THE FURNACE: soot-black brick, the mortar glowing faintly.</summary>
        static float BrickH(float u, float v)
        {
            float row = Mathf.Floor(v * 16);
            float bx = Frac(u * 8 + (row % 2) * 0.5f), by = Frac(v * 16);
            float edge = Mathf.Min(Mathf.Min(bx, 1 - bx) * 4, Mathf.Min(by, 1 - by) * 2);
            return Mathf.Clamp01(edge * 3) * (0.8f + 0.2f * Fbm(u, v, 32, 131, 2));
        }

        public static Texture2D Brick() => Make("brick", 1024, (u, v) =>
        {
            float h = BrickH(u, v);
            float row = Mathf.Floor(v * 16), col = Mathf.Floor(u * 8 + (row % 2) * 0.5f);
            float tone = 0.6f + 0.6f * Hash((int)col, (int)row, 133);
            var c = new Color(0.16f, 0.07f, 0.05f) * tone * (0.7f + 0.3f * Fbm(u, v, 16, 134, 3));
            c = Mix(new Color(0.35f, 0.12f, 0.04f), c, Mathf.Clamp01(h * 1.5f));
            c.a = 1;
            return c;
        });

        public static Texture2D BrickNormal() => NormalFrom("brickN", 512, BrickH, 4f);

        /// <summary>Black marble with gold veins: the bar top.</summary>
        public static Texture2D Marble() => Make("marble", 512, (u, v) =>
        {
            float w = Fbm(u, v, 4, 101, 5);
            float vein = Mathf.Abs(Mathf.Sin((u * 3 + v * 2 + w * 4) * Mathf.PI));
            var c = Mix(new Color(0.02f, 0.02f, 0.025f), new Color(0.07f, 0.06f, 0.07f), w);
            c = Mix(c, Look.Gold * 0.7f, Mathf.Clamp01((0.04f - vein) / 0.04f));
            c.a = 1;
            return c;
        });
    }
}
