// The air of the place: a dark ambient, a violet haze, and the post stack (bloom, ACES, grading,
// vignette, a little grain). The neon earns its bloom; everything else is lit by the room's lights.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vig.Client
{
    public static class Atmosphere
    {
        public static Volume Setup(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.07f, 0.13f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.05f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.025f, 0.035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.07f, 0.035f, 0.07f);
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1;

            var go = new GameObject("Post FX");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "Vig Post";
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(true);
            var tone = p.Add<Tonemapping>(true);
            // Neutral keeps the tier colors' hue where ACES bleaches bright neon white
            tone.mode.Override(TonemappingMode.Neutral);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(18f);
            ca.saturation.Override(14f);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);
            var chroma = p.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.06f);
            v.sharedProfile = p;
            return v;
        }

        /// <summary>The player's camera with post on.</summary>
        public static Camera MakeCamera(Transform parent)
        {
            var go = new GameObject("Eye");
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 80;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            return cam;
        }
    }
}
