// Editor tools: set the project up (TextMeshPro's resources, the scene, the build list) and take the
// review screenshots (CLAUDE.md, definition of done: a screenshot taken and looked at). Both run from
// the Vig menu or in batch mode:
//   Unity.exe -batchmode -projectPath . -executeMethod Vig.EditorTools.VigTools.BatchSetup -quit
//   Unity.exe -batchmode -projectPath . -executeMethod Vig.EditorTools.VigTools.BatchShots -quit
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vig.Client;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.EditorTools
{
    public static class VigTools
    {
        public const string ScenePath = "Assets/Scenes/Floor1.unity";

        [MenuItem("Vig/Set Up Project")]
        public static void Setup()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
                var pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                AssetDatabase.ImportPackage(pkg, false);
                AssetDatabase.Refresh();
                Debug.Log("Vig: imported TMP Essential Resources");
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Vig").AddComponent<Play>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Vig: created " + ScenePath);
            }
            ShaderAssets();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "The Vig";
            PlayerSettings.companyName = "ako8177";
            AssetDatabase.SaveAssets();
        }

        /// <summary>Material assets that keep the shader variants the runtime materials use in a build.</summary>
        static void ShaderAssets()
        {
            const string dir = "Assets/Vig/Resources/VigShaders";
            Directory.CreateDirectory(dir);
            void Mat(string name, string shader, params string[] keywords)
            {
                string path = $"{dir}/{name}.mat";
                if (File.Exists(path)) return;
                var m = new Material(Shader.Find(shader));
                foreach (var k in keywords) m.EnableKeyword(k);
                if (keywords.Contains("_EMISSION")) m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                AssetDatabase.CreateAsset(m, path);
            }
            Mat("Lit", "Universal Render Pipeline/Lit");
            Mat("Lit_Normal", "Universal Render Pipeline/Lit", "_NORMALMAP");
            Mat("Lit_Emission", "Universal Render Pipeline/Lit", "_EMISSION");
            Mat("Lit_Normal_Emission", "Universal Render Pipeline/Lit", "_NORMALMAP", "_EMISSION");
            Mat("Unlit", "Universal Render Pipeline/Unlit");
            // the neon letters' glow
            string tmp = $"{dir}/TMP_Glow.mat";
            if (!File.Exists(tmp))
            {
                var m = new Material(Shader.Find("TextMeshPro/Distance Field"));
                m.EnableKeyword("GLOW_ON");
                AssetDatabase.CreateAsset(m, tmp);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Vig/Build Windows")]
        public static void BuildWindows()
        {
            Setup();
            var r = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/TheVig.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log($"Vig: build {r.summary.result}, {r.summary.totalSize / (1024 * 1024)} MB, {r.summary.totalErrors} errors");
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        public static void BatchSetup()
        {
            Setup();
            Debug.Log("Vig: setup done");
        }

        // ---------------------------------------------------------------- screenshots

        /// <summary>A named view: where the eye is and what it looks at (Unity space), and what to do first.</summary>
        sealed class Shot
        {
            public string Name;
            public Vector3 Eye, At;
            public Action<Play> Before;
            public bool Hud;
        }

        static Vector3 U(double x, double y, double z) => RoomBuilder.U(x, y, z);

        static List<Shot> Shots()
        {
            var f = Layout.Floor1;
            var list = new List<Shot>
            {
                new Shot { Name = "01_door", Eye = U(0, 1.6, 16.6), At = U(0, 1.4, 6), Hud = true },
                new Shot { Name = "02_floor_wide", Eye = U(11.5, 3.2, 16.5), At = U(-4, 0.6, 4) },
                new Shot { Name = "03_bar", Eye = U(-7.5, 1.65, 9.5), At = U(-13, 1.4, 4.5) },
                new Shot { Name = "04_slots", Eye = U(-3.2, 1.6, 15.2), At = U(-8, 1.3, 11) },
                new Shot { Name = "05_lounge", Eye = U(-8, 1.6, -2.6), At = U(-7, 1.0, -10) },
                new Shot { Name = "06_back_room", Eye = U(8, 1.6, -2.6), At = U(6.5, 1.0, -10) },
                new Shot { Name = "07_twentyone", Eye = U(4.6, 1.6, 8.2), At = U(6.5, 1.0, 5) },
            };
            var hilo = f.TableOf("hilo");
            list.Add(new Shot
            {
                Name = "08_hilo_seated", Eye = RoomBuilder.U(hilo.Eye), At = RoomBuilder.U(hilo.Look), Hud = true,
                Before = play =>
                {
                    var g = play.G;
                    var p = g.State.Players[0];
                    p.X = hilo.Stand.X; p.Z = hilo.Stand.Z;
                    g.Submit(0, Command.Sit("hilo"));
                    g.Step();
                    // play until a loss hands out a mark
                    for (int i = 0; i < 12; i++)
                    {
                        g.Submit(0, Command.CallIt("hilo", i % 2 == 0 ? "hi" : "lo"));
                        Run(play, 120);
                        if (g.State.Tables["hilo"].Last != null && !g.State.Tables["hilo"].Last.Win) break;
                    }
                },
            });
            var bandit = f.TableOf("bandit");
            list.Add(new Shot
            {
                Name = "09_bandit_seated", Eye = RoomBuilder.U(bandit.Eye), At = RoomBuilder.U(bandit.Look),
                Before = play =>
                {
                    SitAt(play, "bandit");
                    play.G.Submit(0, Command.Spin("bandit"));
                    Run(play, 200);
                },
            });
            var t21 = f.TableOf("twentyone");
            list.Add(new Shot
            {
                Name = "10_twentyone_hand", Eye = RoomBuilder.U(t21.Eye), At = RoomBuilder.U(t21.Look), Hud = true,
                Before = play =>
                {
                    SitAt(play, "twentyone");
                    // deal until a hand is in play (a natural settles at once)
                    for (int i = 0; i < 10 && play.G.State.Tables["twentyone"].Hand?.Stage != "play"; i++)
                    {
                        play.G.Submit(0, Command.Bet("twentyone"));
                        Run(play, 240);
                    }
                },
            });
            var red = f.TableOf("redline");
            list.Add(new Shot
            {
                Name = "11_redline_third_loss", Eye = RoomBuilder.U(red.Eye), At = RoomBuilder.U(red.Look), Hud = true,
                Before = play =>
                {
                    var g = play.G;
                    if (g.State.Tables["twentyone"].Hand?.Stage == "play") { g.Submit(0, Command.CallIt("twentyone", "stand")); Run(play, 300); }
                    SitAt(play, "redline");
                    // play red until three in a row lose and the life goes (the bank is topped up: a debug step, like the TS skipTo)
                    for (int i = 0; i < 80; i++)
                    {
                        g.State.Bank = System.Math.Max(g.State.Bank, 3000);
                        g.Submit(0, Command.CallIt("redline", "red"));
                        Run(play, 160);
                        if (g.State.Tables["redline"].Last?.LifeLost == true) break;
                    }
                },
            });
            var co = f.Collector;
            list.Add(new Shot
            {
                Name = "12_midnight_lights", Eye = U(8, 1.7, -3.0), At = U(co.X, 1.2, co.Z),
                Before = play =>
                {
                    var g = play.G;
                    if (g.State.Players[0].Seat != null) { g.Submit(0, Command.Stand()); g.Step(); }
                    g.State.Bank = g.State.Quota + 900;
                    g.State.Clock = 2;
                    Run(play, 40);
                },
            });
            list.Add(new Shot
            {
                Name = "13_collector_choice", Eye = RoomBuilder.U(co.Eye), At = RoomBuilder.U(co.Look), Hud = true,
                Before = play => Run(play, 200),
            });
            list.Add(new Shot
            {
                Name = "14_circuit_overload", Eye = RoomBuilder.U(co.Eye), At = RoomBuilder.U(co.Look), Hud = true,
                Before = play =>
                {
                    // challenge him, and pull until someone takes a shock
                    var g = play.G;
                    g.Submit(0, Command.Gamble());
                    Run(play, 2);
                    var rng = new Rng(9);
                    for (int i = 0; i < 40 && g.State.Circuit?.Done == null; i++)
                    {
                        var c = g.State.Circuit;
                        if (c.Turn == "team")
                        {
                            var up = Vig.Sim.CircuitPlay.Up(c);
                            g.Submit(0, Command.Activate(up[rng.Int(0, up.Count - 1)]));
                        }
                        Run(play, 30);
                        if (c.Levers.Any(l => l.Pulled && l.Live)) break;
                    }
                },
            });
            list.Add(new Shot
            {
                Name = "15_boon_wheel", Eye = RoomBuilder.U(co.Eye), At = RoomBuilder.U(co.Look), Hud = true,
                Before = play =>
                {
                    // fresh runs until one wins the Circuit, then spin the Boon
                    for (uint seed = 1; seed < 200; seed++)
                    {
                        play.NewRun(seed);
                        var g = play.G;
                        g.State.Bank = g.State.Quota + 900;
                        g.State.Clock = 1;
                        Run(play, 200);
                        g.Submit(0, Command.Gamble());
                        Run(play, 2);
                        var rng = new Rng(seed);
                        while (g.State.Phase == Phase.Circuit && g.State.Circuit.Done == null)
                        {
                            if (g.State.Circuit.Turn == "team")
                            {
                                var up = Vig.Sim.CircuitPlay.Up(g.State.Circuit);
                                g.Submit(0, Command.Activate(up[rng.Int(0, up.Count - 1)]));
                            }
                            Run(play, 30);
                        }
                        if (g.State.Circuit.Done.Won) break;
                    }
                    Run(play, 60 * 24);
                    play.G.Submit(0, Command.Fortune());
                    Run(play, 60 * 2);
                },
            });
            return list;
        }

        /// <summary>Step the sim, the events going through the game's own handlers (lights, HUD).</summary>
        static void Run(Play play, int ticks)
        {
            for (int k = 0; k < ticks; k++) { play.G.Step(); play.Drain(); }
        }

        static void SitAt(Play play, string table)
        {
            var g = play.G;
            var p = g.State.Players[0];
            if (p.Seat != null) { g.Submit(0, Command.Stand()); Run(play, 1); }
            var pl = Layout.Floor1.TableOf(table);
            p.X = pl.Stand.X; p.Z = pl.Stand.Z;
            g.Submit(0, Command.Sit(table));
            Run(play, 1);
        }

        [MenuItem("Vig/Take Screenshots")]
        public static void TakeScreenshots()
        {
            Setup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var go = new GameObject("Vig (shots)");
            var play = go.AddComponent<Play>();
            play.Boot(false);
            play.NewRun(20261015);
            Directory.CreateDirectory("shots");
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var cam = play.Cam;
            foreach (var r in play.Room.Root.GetComponentsInChildren<ReflectionProbe>()) r.RenderProbe();
            foreach (var s in Shots())
            {
                s.Before?.Invoke(play);
                play.Hud.Banner("", Color.clear, 0);
                play.Refresh();
                var canvas = play.Hud.Canvas;
                canvas.renderMode = s.Hud ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.2f;
                canvas.enabled = s.Hud;
                play.Hud.Refresh(play.G, 0);
                cam.transform.position = s.Eye;
                cam.transform.rotation = Quaternion.LookRotation(s.At - s.Eye);
                Canvas.ForceUpdateCanvases();
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes($"shots/{s.Name}.png", tex.EncodeToPNG());
                Debug.Log($"Vig: shots/{s.Name}.png");
            }
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(rt);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BatchShots()
        {
            try { TakeScreenshots(); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
