// A small kit for the HUD and the table screens: canvases, panels, text (TextMeshPro), built in code.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vig.Client
{
    public static class Ui
    {
        public static Canvas ScreenCanvas(string name, int order = 0)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        /// <summary>A world-space canvas `px` wide covering a screen `w` x `h` m, on `parent` facing -Z.</summary>
        public static Canvas WorldCanvas(string name, Transform parent, float w, float h, float px = 640)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0, -0.004f);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            float py = px * h / w;
            rt.sizeDelta = new Vector2(px, py);
            go.transform.localScale = Vector3.one * (w / px);
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color c, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color c, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, bool bold = false)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offMin, offMax);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Neon.Font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        // ---------------------------------------------------------------- sprites and buttons (drawn by code)

        static Sprite round, ring, chip;

        /// <summary>A rounded rectangle, 9-sliced: buttons and panels.</summary>
        public static Sprite Round => round ? round : (round = Rounded(64, 18, false));
        /// <summary>The same with only its outline: selection rings.</summary>
        public static Sprite Ring => ring ? ring : (ring = Rounded(64, 18, true));

        static Sprite Rounded(int size, int r, bool outline)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (outline) a *= Mathf.Clamp01(d - (r - 4) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        /// <summary>A casino chip from above: a disc, a striped edge, an inner ring (white, tinted by the Image).</summary>
        public static Sprite Chip
        {
            get
            {
                if (chip) return chip;
                const int s = 128;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f);
                        float a = Mathf.Clamp01((1 - d) * s / 2f);
                        float ang = Mathf.Atan2(dy, dx);
                        float v = 1;
                        if (d > 0.78f && Mathf.Repeat(ang * 8 / Mathf.PI, 2) < 1) v = 1.7f;
                        if (d > 0.74f && d < 0.78f) v = 0.55f;
                        if (d > 0.5f && d < 0.54f) v = 1.6f;
                        if (d < 0.5f) v = 0.85f;
                        byte c = (byte)Mathf.Clamp(v * 150, 0, 255);
                        px[y * s + x] = new Color32(c, c, c, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply(true);
                return chip = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>A button: a rounded panel, a label, a click.</summary>
        public static Button Button(string name, Transform parent, string label, Color c, System.Action onClick, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, float size = 26)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.color = c;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            colors.colorMultiplier = 1.5f;
            b.colors = colors;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("click", null, 0.6f); onClick(); });
            var t = Text("label", rt, label, size, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2), true);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 11;
            t.fontSizeMax = size;
            return b;
        }

        /// <summary>Destroy now in the editor (the screenshot tool), at the frame's end while playing.</summary>
        public static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static void Label(Button b, string text) => b.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        public static string Col(string s, Color c) => $"<color={Hex(c)}>{s}</color>";
        public static string Money(int v) => (v < 0 ? "-$" : "$") + Mathf.Abs(v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
