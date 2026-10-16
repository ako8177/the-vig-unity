// Neon signs: letters in TextMeshPro with an HDR face the bloom catches, a glass tube round them in the
// same color, a black mounting board, and a real light so the sign lights what's near it. A sign's
// readable face points to -Z at yaw 0 (south in the sim: toward the entrance).
using TMPro;
using UnityEngine;

namespace Vig.Client
{
    public static class Neon
    {
        static TMP_FontAsset font;
        public static TMP_FontAsset Font
        {
            get
            {
                if (!font) font = TMP_Settings.defaultFontAsset;
                return font;
            }
        }

        /// <summary>Glowing letters, `height` m tall, centered on localPos, readable from -Z.</summary>
        public static TextMeshPro Text(Transform parent, string text, Color c, Vector3 localPos, float height, float yawDeg = 0, float intensity = 2.3f)
        {
            var go = new GameObject("neon " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yawDeg, 0);
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 8;
            // TMP's world-space size: a font size of 10 is about one unit from baseline to cap
            t.fontSize = height * 13f;
            t.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, text.Length * height * 1.1f), height * 1.6f);
            var m = t.fontMaterial;
            // a hot core a shade paler than the tube, the tube's color round it, a soft glow past that
            var hot = Color.Lerp(c, Color.white, 0.22f);
            m.SetColor("_FaceColor", new Color(hot.r * intensity, hot.g * intensity, hot.b * intensity, 1));
            m.SetColor("_OutlineColor", new Color(c.r * intensity * 0.8f, c.g * intensity * 0.8f, c.b * intensity * 0.8f, 1));
            m.SetFloat("_OutlineWidth", 0.18f);
            m.EnableKeyword("GLOW_ON");
            m.SetColor("_GlowColor", new Color(c.r * 1.3f, c.g * 1.3f, c.b * 1.3f, 0.5f));
            m.SetFloat("_GlowOuter", 0.55f);
            m.SetFloat("_GlowPower", 0.6f);
            t.UpdateMeshPadding();
            return t;
        }

        /// <summary>A sign: letters, a tube frame, a board behind, and a light in front.</summary>
        public static GameObject Sign(Transform parent, string text, Color c, Vector3 localPos, float yawDeg, float height, RoomBuilder b, bool frame = false)
        {
            var root = new GameObject("sign " + text);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0, yawDeg, 0);
            Text(root.transform, text, c, Vector3.zero, height);
            float w = text.Length * height * 0.78f + height * 0.9f, h = height * 1.75f;
            if (frame)
            {
                var tube = new GameObject("tube");
                tube.transform.SetParent(root.transform, false);
                tube.transform.localPosition = new Vector3(0, 0, 0.02f);
                tube.AddComponent<MeshFilter>().sharedMesh = MeshKit.Tube(MeshKit.RoundRect(w, h, h * 0.35f, 8), height * 0.035f, 6, true);
                tube.AddComponent<MeshRenderer>().sharedMaterial = Look.Neon(c, 5f);
            }
            var board = new GameObject("board");
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0, 0, 0.07f);
            board.AddComponent<MeshFilter>().sharedMesh = MeshKit.RoundedBox(w + height * 0.25f, h + height * 0.25f, 0.05f, 0.02f);
            board.AddComponent<MeshRenderer>().sharedMaterial = Look.BlackGloss;
            b?.SignLight(root.transform, c, new Vector3(0, -height * 0.4f, -0.45f), 1.6f + height * 2, 2.5f + height * 4);
            return root;
        }
    }
}
