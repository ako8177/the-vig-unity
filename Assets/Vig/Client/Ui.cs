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

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        public static string Col(string s, Color c) => $"<color={Hex(c)}>{s}</color>";
        public static string Money(int v) => (v < 0 ? "-$" : "$") + Mathf.Abs(v).ToString("N0");
    }
}
