// The look (docs/STYLE.md, re-read for a lit, full-resolution game: DECISIONS.md U2): a velvet trap under
// hot neon. The palette roles and the tier colors carry over from the TS game; what changed is that
// colors are no longer snapped to 48 or dithered, surfaces are lit physically (URP Lit), and the neon
// is real light. Every material and texture is made here, by code.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vig.Client
{
    public static class Look
    {
        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        // the tiers own their colors (pillar 5): base, hot core, low shade
        public static readonly Color Low = Hex("00e60f"), LowHi = Hex("c4ff91");
        public static readonly Color Mid = Hex("f0ff00"), MidHi = Hex("ffff8e");
        public static readonly Color High = Hex("f5320a"), HighHi = Hex("f5916e");
        public static readonly Color Coll = Hex("be5aff"), CollHi = Hex("e1b6ff"), CollLo = Hex("801cc0");
        public static readonly Color Soul = Hex("32fad2"), Marker = Hex("6e8ef0"), Blood = Hex("ac0a48");
        public static readonly Color Brass = Hex("f8d280"), Gold = Hex("f8d280"), Cream = Hex("f7c3a0");
        public static readonly Color Indigo = Hex("1a0070"), IndigoHi = Hex("3c1e8e"), Pink = Hex("d7198c"), PinkHi = Hex("fa5adc"), Teal = Hex("05826c");
        public static readonly Color Ink = Hex("1f1d1e"), Night = Hex("303232"), Smoke = Hex("645c5a"), Bone = Hex("c8c8b5");
        public static readonly Color Wood = Hex("642808"), WoodHi = Hex("c86900"), WoodLo = Hex("511806");
        public static readonly Color Carpet = Hex("410966");
        public static readonly Color Baize = Hex("005a20");

        public static Color TierColor(string tier) => tier == "low" ? Low : tier == "mid" ? Mid : tier == "high" ? High : Coll;
        public static Color TierHot(string tier) => tier == "low" ? LowHi : tier == "mid" ? MidHi : tier == "high" ? HighHi : CollHi;

        // the shaders come from material assets in Resources/VigShaders, which carry every keyword
        // variant the code turns on (normal map, emission): a player build strips what no asset uses
        static Shader lit, unlit;
        static Shader Lit => lit ? lit : (lit = ShaderOf("Lit", "Universal Render Pipeline/Lit"));
        static Shader Unlit => unlit ? unlit : (unlit = ShaderOf("Unlit", "Universal Render Pipeline/Unlit"));
        static Shader ShaderOf(string asset, string name)
        {
            var m = Resources.Load<Material>("VigShaders/" + asset);
            return m ? m.shader : Shader.Find(name);
        }
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        /// <summary>A lit surface: base color (and map), smoothness, metallic, an optional normal map and emission.</summary>
        public static Material Surface(string key, Color color, float smooth, float metal = 0, Texture2D map = null, Texture2D normal = null,
            float normalScale = 1, Vector2? tiling = null, Color? emission = null)
        {
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Lit) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (map)
            {
                m.SetTexture("_BaseMap", map);
                if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            }
            if (normal)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", normalScale);
                if (tiling.HasValue) m.SetTextureScale("_BumpMap", tiling.Value);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Neon: an unlit HDR color, bright enough that the bloom catches it.</summary>
        public static Material Neon(Color c, float intensity = 6f)
        {
            string key = "neon" + ColorUtility.ToHtmlStringRGB(c) + intensity;
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Unlit) { name = key };
            // tubes keep their hue: bright enough to bloom, not so bright the tonemapper bleaches them white
            float k = intensity * 0.4f;
            var hdr = new Color(c.r * k, c.g * k, c.b * k, 1);
            m.SetColor("_BaseColor", hdr);
            cache[key] = m;
            return m;
        }

        /// <summary>A light-emitting surface that is still lit (lamp shades, bulbs, screens' glow).</summary>
        public static Material Glow(string key, Color c, float intensity) =>
            Surface(key, c * 0.4f, 0.6f, 0, emission: new Color(c.r * intensity, c.g * intensity, c.b * intensity));

        // ---------------------------------------------------------------- the materials the room uses

        public static Material CarpetFloor => Surface("carpetFloor", Color.white, 0.12f, 0, Textures.CarpetMedallion(), Textures.FiberNormal(), 0.5f, new Vector2(1, 1));
        public static Material CarpetLounge => Surface("carpetLounge", Color.white, 0.1f, 0, Textures.CarpetLounge(), Textures.FiberNormal(), 0.5f);
        public static Material Planks => Surface("planks", Color.white, 0.55f, 0, Textures.Planks(), Textures.PlankNormal(), 0.6f);
        public static Material WallFloor => Surface("wallFloor", Color.white, 0.25f, 0, Textures.Damask(), Textures.DamaskNormal(), 0.35f);
        public static Material WallLounge => Surface("wallLounge", Color.white, 0.2f, 0, Textures.Flock(), Textures.DamaskNormal(), 0.3f);
        public static Material WallBack => Surface("wallBack", new Color(0.55f, 0.5f, 0.48f), 0.15f, 0, Textures.Plaster(), Textures.PlasterNormal(), 0.6f);
        public static Material Ceiling => Surface("ceiling", new Color(0.09f, 0.07f, 0.1f), 0.2f);
        public static Material CeilingFloor => Surface("ceilingFloor", new Color(0.2f, 0.12f, 0.1f), 0.35f, 0, Textures.Coffers());
        public static Material Wainscot => Surface("wainscot", Color.white, 0.82f, 0, Textures.WoodGrain(), null, 1, new Vector2(1, 1));
        public static Material Lacquer => Surface("lacquer", Color.white, 0.88f, 0, Textures.WoodGrain());
        public static Material DarkWood => Surface("darkWood", new Color(0.35f, 0.2f, 0.14f), 0.7f, 0, Textures.WoodGrain());
        public static Material Felt => Surface("felt", new Color(0.19f, 0.2f, 0.2f), 0.05f, 0, Textures.FeltNoise(), Textures.FeltNormal(), 0.4f);
        public static Material BaizeMat => Surface("baize", Look.Baize, 0.05f, 0, Textures.FeltNoise(), Textures.FeltNormal(), 0.4f);
        public static Material Leather => Surface("leather", new Color(0.1f, 0.05f, 0.06f), 0.55f, 0, null, Textures.LeatherNormal(), 0.5f);
        public static Material BrassMat => Surface("brass", Brass, 0.78f, 1);
        public static Material Chrome => Surface("chrome", new Color(0.8f, 0.8f, 0.82f), 0.92f, 1);
        public static Material BlackGloss => Surface("blackGloss", new Color(0.02f, 0.02f, 0.025f), 0.9f, 0);
        public static Material Marble => Surface("marble", Color.white, 0.85f, 0, Textures.Marble());
        public static Material MarbleLight => Surface("marbleLight", Color.white, 0.8f, 0, Textures.MarbleLight());
        public static Material Velvet => Surface("velvet", new Color(0.32f, 0.02f, 0.12f), 0.25f, 0, null, Textures.FiberNormal(), 0.4f);
        public static Material VelvetPurple => Surface("velvetPurple", new Color(0.18f, 0.03f, 0.28f), 0.25f, 0, null, Textures.FiberNormal(), 0.4f);
        public static Material Glass => Surface("glass", new Color(0.05f, 0.05f, 0.06f), 0.95f, 0);
        public static Material Plant => Surface("plant", new Color(0.04f, 0.16f, 0.07f), 0.35f);
        public static Material Pot => Surface("pot", new Color(0.12f, 0.1f, 0.1f), 0.7f);
        public static Material Shade => Glow("shade", new Color(1f, 0.72f, 0.4f), 2.2f);
        public static Material Bulb => Glow("bulb", new Color(1f, 0.8f, 0.55f), 9f);
        public static Material Crystal => Glow("crystal", new Color(1f, 0.92f, 0.8f), 3.5f);
        public static Material Screen(string key, Color c) => Surface(key, Color.black, 0.9f, 0, emission: c);

        // floor 2 (DESIGN.md §11): the Gold Room, the Mirror Room, the Furnace
        public static Material CarpetDeco => Surface("carpetDeco", Color.white, 0.18f, 0, Textures.DecoCarpet(), Textures.FiberNormal(), 0.4f);
        public static Material WallDeco => Surface("wallDeco", Color.white, 0.85f, 0, Textures.DecoWall());
        public static Material WallMirror => Surface("wallMirror", new Color(0.32f, 0.36f, 0.4f), 0.96f, 1);
        public static Material FloorMirror => Surface("floorMirror", new Color(0.05f, 0.05f, 0.07f), 0.9f, 0.3f, Textures.Marble());
        public static Material WallBrick => Surface("wallBrick", Color.white, 0.2f, 0, Textures.Brick(), Textures.BrickNormal(), 0.8f);
        public static Material FloorIron => Surface("floorIron", new Color(0.18f, 0.16f, 0.15f), 0.55f, 0.8f, Textures.Plaster(), Textures.PlasterNormal(), 0.8f);
        public static Material Emerald => Surface("emerald", new Color(0.0f, 0.22f, 0.15f), 0.08f, 0, Textures.FeltNoise(), Textures.FeltNormal(), 0.4f);
        public static Material Ember => Glow("ember", new Color(1f, 0.35f, 0.08f), 3f);
    }
}
