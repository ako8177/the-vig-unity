// The tables' moving pieces, built by code like the rest of the room (U8): dice with sunk pips, playing
// cards (card stock, a printed back, the rank and suit in TextMeshPro), chip stacks. The shows
// (Shows.cs) move them; nothing here knows a rule.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Vig.Sim;
using Vig.Sim.Tables;

namespace Vig.Client
{
    public static class Props
    {
        static Vector2 P(float r, float y) => new Vector2(r, y);
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        public static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!meshes.TryGetValue(key, out var m) || !m) meshes[key] = m = make();
            return m;
        }

        public static GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Quaternion? rot = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static Transform Group(string name, Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot ?? Quaternion.identity;
            return t;
        }

        /// <summary>Flat text lying in its transform's XY plane, readable from -Z (rotate it onto a surface).</summary>
        public static TextMeshPro Print(Transform parent, string text, Color c, float height, Vector3 pos, Quaternion rot, bool bold = true)
        {
            var go = new GameObject("print " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Neon.Font;
            t.text = text;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.fontSize = height * 13f;
            t.rectTransform.sizeDelta = new Vector2(height * 6, height * 2);
            return t;
        }

        // ---------------------------------------------------------------- materials

        public static Material DieBody => Look.Surface("dieBody", new Color(0.93f, 0.9f, 0.82f), 0.82f);
        public static Material Pip => Look.Surface("pip", new Color(0.03f, 0.025f, 0.03f), 0.15f);
        public static Material PipBlood => Look.Glow("pipBlood", new Color(0.9f, 0.05f, 0.3f), 3f);
        public static Material CardStock => Look.Surface("cardStock", new Color(0.95f, 0.93f, 0.88f), 0.45f);
        public static Material CardBack => Look.Surface("cardBack", Color.white, 0.5f, 0, CardBackTex());
        public static Material ChipCream => Look.Surface("chipCream", new Color(0.88f, 0.84f, 0.74f), 0.6f);
        public static Material ChipRed => Look.Surface("chipRed", new Color(0.42f, 0.03f, 0.08f), 0.6f);

        static Texture2D cardBack;
        /// <summary>The back of every card: a crimson field, a fine diamond lattice, a cream inner frame.</summary>
        static Texture2D CardBackTex()
        {
            if (cardBack) return cardBack;
            const int w = 128, h = 180;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "cardBack", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[w * h];
            Color field = new Color(0.45f, 0.03f, 0.08f), line = new Color(0.72f, 0.18f, 0.2f), cream = new Color(0.95f, 0.92f, 0.85f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = field;
                    float u = x + 0.5f, v = y + 0.5f;
                    // the lattice: two families of diagonals
                    if (Mathf.Repeat(u + v, 12) < 1.4f || Mathf.Repeat(u - v, 12) < 1.4f) c = line;
                    // the frame: 6 px in, 2 px wide
                    bool inFrame = (x >= 6 && x < 8 || x >= w - 8 && x < w - 6) && y >= 6 && y < h - 6 || (y >= 6 && y < 8 || y >= h - 8 && y < h - 6) && x >= 6 && x < w - 6;
                    if (x < 3 || y < 3 || x >= w - 3 || y >= h - 3) c = cream;
                    else if (inFrame) c = cream;
                    px[y * w + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return cardBack = t;
        }

        // ---------------------------------------------------------------- dice

        /// <summary>A die's face normals: 1 and 6, 2 and 5, 3 and 4 opposite (they sum to 7).</summary>
        public static readonly Vector3[] FaceN = { Vector3.zero, Vector3.up, Vector3.right, Vector3.forward, Vector3.back, Vector3.left, Vector3.down };
        static readonly int[][] PipsOf =
        {
            new int[0], new[] { 1, 1 }, new[] { 0, 0, 2, 2 }, new[] { 0, 0, 1, 1, 2, 2 }, new[] { 0, 0, 2, 0, 0, 2, 2, 2 },
            new[] { 0, 0, 2, 0, 1, 1, 0, 2, 2, 2 }, new[] { 0, 0, 2, 0, 0, 1, 2, 1, 0, 2, 2, 2 },
        };

        public sealed class Die
        {
            public readonly Transform T;
            readonly List<MeshRenderer> pips = new List<MeshRenderer>();
            public readonly float Size;

            public Die(Transform parent, float size, string name = "die")
            {
                Size = size;
                T = Group(name, parent, Vector3.zero);
                Part("body", Cached("dieBody" + size, () => MeshKit.RoundedBox(size, size, size, size * 0.16f, 4)), DieBody, T, Vector3.zero);
                float r = size * 0.085f, k = size * 0.25f;
                var pip = Cached("pip" + size, () => MeshKit.Lathe(new[] { P(r, -0.0012f), P(r, 0.0003f), P(r * 0.6f, 0.0006f), P(0, 0.0006f) }, 14, false, false));
                for (int f = 1; f <= 6; f++)
                {
                    var n = FaceN[f];
                    var rot = Quaternion.FromToRotation(Vector3.up, n);
                    var u = rot * Vector3.right;
                    var v = rot * Vector3.forward;
                    var pp = PipsOf[f];
                    for (int i = 0; i < pp.Length; i += 2)
                    {
                        var at = n * (size / 2) + u * (pp[i] - 1) * k + v * (pp[i + 1] - 1) * k;
                        var go = Part("pip", pip, Pip, T, at, rot, false);
                        pips.Add(go.GetComponent<MeshRenderer>());
                    }
                }
            }

            public void PipColor(Material m)
            {
                foreach (var p in pips) if (p.sharedMaterial != m) p.sharedMaterial = m;
            }

            /// <summary>The die's turn with `face` up, then turned `yawDeg` about the vertical.</summary>
            public static Quaternion FaceUp(int face, float yawDeg) =>
                Quaternion.Euler(0, yawDeg, 0) * Quaternion.FromToRotation(FaceN[Mathf.Clamp(face, 1, 6)], Vector3.up);
        }

        // ---------------------------------------------------------------- cards

        public const float CardW = 0.095f, CardL = 0.133f, CardT = 0.0016f;

        /// <summary>A playing card lying flat (its face up at identity, its top toward +Z): card stock with the
        /// rank and suit printed in two corners and a big suit in the middle, the printed back underneath.</summary>
        public sealed class CardProp
        {
            public readonly Transform T;
            readonly TextMeshPro corner, corner2, middle;
            Card shown;

            public CardProp(Transform parent, string name = "card")
            {
                T = Group(name, parent, Vector3.zero);
                Part("stock", Cached("cardStock", () => MeshKit.RoundedBox(CardW, CardT, CardL, 0.0007f, 2)), CardStock, T, Vector3.zero);
                Part("back", Cached("cardBackQuad", () => MeshKit.Quad(CardW - 0.004f, CardL - 0.004f)), CardBack, T, new Vector3(0, -CardT / 2 - 0.0004f, 0), Quaternion.Euler(-90, 0, 0), false);
                var up = Quaternion.Euler(90, 0, 0);
                float y = CardT / 2 + 0.0005f;
                corner = Print(T, "", Color.black, 0.019f, new Vector3(-CardW / 2 + 0.015f, y, CardL / 2 - 0.026f), up);
                corner2 = Print(T, "", Color.black, 0.019f, new Vector3(CardW / 2 - 0.015f, y, -CardL / 2 + 0.026f), Quaternion.Euler(90, 180, 0));
                middle = Print(T, "", Color.black, 0.05f, new Vector3(0, y, 0), up);
                foreach (var t in new[] { corner, corner2, middle }) t.lineSpacing = -30;
            }

            static string[] suits;
            public static string Suit(int s)
            {
                suits = suits ?? new[] { TableScreen.Glyph('♥', "H"), TableScreen.Glyph('♦', "D"), TableScreen.Glyph('♣', "C"), TableScreen.Glyph('♠', "S") };
                return suits[s];
            }

            public void Show(Card c)
            {
                if (c == null || shown != null && shown.R == c.R && shown.S == c.S) return;
                shown = c;
                var col = c.S < 2 ? new Color(0.78f, 0.04f, 0.12f) : new Color(0.06f, 0.05f, 0.07f);
                string rank = TableModule.Rank[c.R];
                corner.text = corner2.text = rank + "\n" + Suit(c.S);
                middle.text = c.R > 10 ? rank : Suit(c.S);
                corner.color = corner2.color = middle.color = col;
            }

            public void Visible(bool on) { if (T.gameObject.activeSelf != on) T.gameObject.SetActive(on); }
        }

        /// <summary>A stack of n chips standing at the origin.</summary>
        public static Transform Chips(Transform parent, string name, Vector3 pos, int n)
        {
            var g = Group(name, parent, pos);
            var chip = Cached("chip", () => MeshKit.Lathe(new[] { P(0.019f, 0), P(0.02f, 0.001f), P(0.02f, 0.0032f), P(0.019f, 0.0042f), P(0, 0.0042f) }, 24, true, false));
            for (int i = 0; i < n; i++)
                Part("chip", chip, i % 2 == 0 ? ChipRed : ChipCream, g, new Vector3(((i * 37) % 5 - 2) * 0.0006f, i * 0.0043f, ((i * 13) % 5 - 2) * 0.0006f), Quaternion.Euler(0, i * 47, 0));
            return g;
        }

        /// <summary>A small bulb (for lamp rings).</summary>
        public static Mesh Bulb(float r) => Cached("propBulb" + r, () => MeshKit.Lathe(new[] { P(0, -r), P(r * 0.7f, -r * 0.7f), P(r, 0), P(r * 0.7f, r * 0.7f), P(0, r) }, 10));
    }
}
