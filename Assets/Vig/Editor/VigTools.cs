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

        /// <summary>Draw every procedural texture (both floors, the house) and save it to Resources/VigTex, where a
        /// build loads it instead of drawing it at boot (U28). The editor always draws them fresh.</summary>
        [MenuItem("Vig/Bake Textures")]
        public static void BakeTextures()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Textures.ClearCache();
            Textures.Baking = true;
            var go = new GameObject("Vig (bake)");
            try
            {
                var play = go.AddComponent<Play>();
                play.Boot(false);
                play.NewRun(1, false, 2, "easy");
                play.NewRun(1, true, 1, "easy");
            }
            finally { Textures.Baking = false; }
            const string dir = "Assets/Vig/Resources/VigTex";
            Directory.CreateDirectory(dir);
            var saved = new List<(string key, bool linear)>();
            foreach (var kv in Textures.Drawn.ToList())
            {
                if (!kv.Value || !kv.Value.isReadable) continue;
                File.WriteAllBytes($"{dir}/{kv.Key}.png", kv.Value.EncodeToPNG());
                saved.Add((kv.Key, Textures.IsLinear(kv.Key)));
            }
            UnityEngine.Object.DestroyImmediate(go);
            Textures.ClearCache();
            AssetDatabase.Refresh();
            foreach (var (key, linear) in saved)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{key}.png");
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = !linear;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            Debug.Log($"Vig: baked {saved.Count} textures to {dir}");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Vig/Build Windows")]
        public static void BuildWindows()
        {
            Setup();
            BakeTextures();
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

        static List<Shot> Shots(Play play0)
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
                // the Collector in his chair, from beside his stool, before midnight (the people: U13)
                new Shot { Name = "28_collector_floor", Eye = U(f.Collector.Stand.X + 1.1, 1.6, f.Collector.Stand.Z + 0.4), At = U(f.Collector.X, 1.25, f.Collector.Z - 1.9) },
            };
            var hilo = f.TableOf("hilo");
            Shot Seat(string name, string table, Action<Play> before, bool hud = false)
            {
                var pl = f.TableOf(table);
                return new Shot { Name = name, Eye = RoomBuilder.U(pl.Eye), At = play0.SeatedLook(pl), Hud = hud, Before = before };
            }
            // Hi-Lo: the die in the air, at rest after a loss, and a win's celebration
            list.Add(Seat("16_hilo_throw", "hilo", play =>
            {
                SitAt(play, "hilo");
                Roll(play, "hilo", Command.CallIt("hilo", "hi"), 0.42f);
            }));
            list.Add(new Shot
            {
                Name = "08_hilo_seated", Eye = RoomBuilder.U(hilo.Eye), At = play0.SeatedLook(hilo), Hud = true,
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
            list.Add(Seat("17_hilo_win", "hilo", play => Until(play, "hilo", () => Command.CallIt("hilo", "hi"), o => o.Win, 0.3f)));
            // the Bandit: the first reel locked and two still rolling; a paying grid outlined
            list.Add(Seat("18_bandit_rolling", "bandit", play =>
            {
                SitAt(play, "bandit");
                Roll(play, "bandit", Command.Spin("bandit"), 0.56f);
            }));
            list.Add(Seat("09_bandit_seated", "bandit", play => Until(play, "bandit", () => Command.Spin("bandit"), o => o.Win, 0.6f)));
            var t21 = f.TableOf("twentyone");
            // Twenty-One: cards coming out of the shoe, the hand in play, and the hand settled
            list.Add(Seat("19_twentyone_deal", "twentyone", play =>
            {
                SitAt(play, "twentyone");
                Topup(play.G);
                play.G.Submit(0, Command.Bet("twentyone"));
                Run(play, (int)(Feel.DealGap * 60 * 1.75));
            }));
            list.Add(new Shot
            {
                Name = "10_twentyone_hand", Eye = RoomBuilder.U(t21.Eye), At = play0.SeatedLook(t21), Hud = true,
                Before = play =>
                {
                    Run(play, 120);
                    // deal until a hand is in play (a natural settles at once)
                    for (int i = 0; i < 10 && play.G.State.Tables["twentyone"].Hand?.Stage != "play"; i++)
                    {
                        play.G.Submit(0, Command.Bet("twentyone"));
                        Run(play, 240);
                    }
                },
            });
            list.Add(Seat("20_twentyone_settled", "twentyone", play =>
            {
                if (play.G.State.Tables["twentyone"].Hand?.Stage == "play") play.G.Submit(0, Command.CallIt("twentyone", "stand"));
                Run(play, 1);
                Settle(play, "twentyone", 0.4f);
            }, true));
            // the regulars (U29): over your shoulder, Dot at first base on your right, Ike at third on your left
            list.Add(new Shot { Name = "48_twentyone_regulars", Eye = U(6.5, 2.05, 7.25), At = U(6.5, 0.85, 4.95) });
            var red = f.TableOf("redline");
            // Red Line: the riffle, the flip, and the third loss in a row
            list.Add(Seat("21_redline_shuffle", "redline", play =>
            {
                SitAt(play, "redline");
                Roll(play, "redline", Command.CallIt("redline", "red"), 0.24f);
            }));
            list.Add(Seat("22_redline_flip", "redline", play =>
            {
                Settle(play, "redline", 0.2f);
                Roll(play, "redline", Command.CallIt("redline", "black"), 0.75f);
            }));
            list.Add(new Shot
            {
                Name = "11_redline_third_loss", Eye = RoomBuilder.U(red.Eye), At = play0.SeatedLook(red), Hud = true,
                Before = play =>
                {
                    var g = play.G;
                    Settle(play, "redline", 0);
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
            // the Wheel of Marks: turning, and landed with its lamps flashing
            list.Add(Seat("23_wheel_spin", "wheel", play =>
            {
                SitAt(play, "wheel");
                Roll(play, "wheel", Command.Spin("wheel"), 0.5f);
            }));
            list.Add(Seat("24_wheel_landed", "wheel", play => Settle(play, "wheel", 0.35f), true));
            // Dead Man's Hand: one die down and the second in the air; a big win; a jackpot
            list.Add(Seat("25_deadman_second", "deadman", play =>
            {
                SitAt(play, "deadman");
                Roll(play, "deadman", Command.Spin("deadman"), 0.76f);
            }));
            list.Add(Seat("26_deadman_bigwin", "deadman", play => Until(play, "deadman", () => Command.Spin("deadman", 500), o => o.Win, 0.35f), true));
            list.Add(Seat("27_deadman_jackpot", "deadman", play => Until(play, "deadman", () => Command.Spin("deadman", 2500), o => o.Win, 0.5f), true));
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
                Name = "13_collector_choice", Eye = Play.CollectorView().eye, At = Play.CollectorView().at, Hud = true,
                Before = play => Run(play, 200),
            });
            list.Add(new Shot
            {
                Name = "14_circuit_overload", Eye = Play.CollectorView().eye, At = Play.CollectorView().at, Hud = true,
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
                Name = "15_boon_wheel", Eye = Play.CollectorView().eye, At = Play.CollectorView().at, Hud = true,
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

            // ---- the house (U22)
            var hs = Home.Spawn;
            list.Add(new Shot
            {
                Name = "30_house_inside", Eye = U(hs.X - 0.8, 1.6, hs.Z + 0.4), At = U(Home.ComputerStand.X, 1.0, Home.ComputerStand.Z - 1.0), Hud = true,
                Before = play => { play.NewRun(4242, true, 1, "easy"); Run(play, 30); },
            });
            list.Add(new Shot
            {
                Name = "31_house_yard", Eye = U(Home.HomeX - 6, 2.2, 13.5), At = U(Home.HomeX + 1, 1.2, 1.5),
            });
            list.Add(new Shot
            {
                Name = "32_catalogue", Eye = U(Home.ComputerStand.X, 1.6, Home.ComputerStand.Z + 0.4), At = U(Home.ComputerStand.X, 1.0, Home.ComputerStand.Z - 1.2), Hud = true,
                Before = play =>
                {
                    var p = play.G.State.Players[0];
                    p.X = Home.ComputerStand.X; p.Z = Home.ComputerStand.Z;
                    play.Shop.Show(true);
                    Run(play, 2);
                },
            });

            // ---- floor 2 (DESIGN.md §11)
            var f2 = Layout.Floor2;
            list.Add(new Shot
            {
                Name = "33_floor2_door", Eye = U(0, 1.6, 16.6), At = U(0, 1.4, 6), Hud = true,
                Before = play => { play.Shop.Show(false); play.NewRun(5151, false, 2, "easy"); Run(play, 20); },
            });
            list.Add(new Shot { Name = "34_gold_room_wide", Eye = U(11.5, 3.2, 16.5), At = U(-4, 0.6, 4) });
            list.Add(new Shot { Name = "35_mirror_room", Eye = U(-8, 1.6, -2.6), At = U(-7, 1.2, -10) });
            list.Add(new Shot { Name = "36_furnace", Eye = U(8, 1.6, -2.6), At = U(6.5, 1.2, -10) });
            Shot Seated(string name, string table, int ticks, System.Action<Play> act)
            {
                var pl = f2.TableOf(table);
                var eye = RoomBuilder.U(pl.Eye);
                var shift = table == "pegdrop" ? new Vector3(0, 0.72f, 0) : table == "crash" ? new Vector3(0, 0.62f, 0.2f) : table == "craps" ? new Vector3(0, -0.15f, 0.35f) : Vector3.zero;
                var at = RoomBuilder.U(pl.Look) + shift + Vector3.down * 0.32f;
                return new Shot { Name = name, Eye = eye, At = at, Hud = true, Before = play => { SitAt(play, table); play.G.State.Bank = 6000; act(play); Run(play, ticks); } };
            }
            list.Add(Seated("37_roulette_spin", "roulette", 170, play => play.G.Submit(0, Command.CallIt("roulette", "red"))));
            list.Add(Seated("38_roulette_landed", "roulette", 200, play => { }));
            list.Add(Seated("39_craps_throw", "craps", 40, play => play.G.Submit(0, Command.Spin("craps"))));
            list.Add(Seated("40_craps_rest", "craps", 160, play => { }));
            list.Add(Seated("41_pegdrop_falling", "pegdrop", 90, play => play.G.Submit(0, Command.Spin("pegdrop"))));
            list.Add(Seated("42_pegdrop_landed", "pegdrop", 140, play => { }));
            list.Add(Seated("43_crash_climbing", "crash", 150, play => play.G.Submit(0, Command.Spin("crash"))));
            list.Add(Seated("44_crash_after", "crash", 900, play => { }));
            // your bet as chips on the felt (U27): the eye looks down at the near corner of the Hi-Lo table
            var chipShot = Seated("45_hilo_chips", "hilo", 150, play => play.G.Submit(0, Command.CallIt("hilo", "hi")));
            var sitHilo = chipShot.Before;
            chipShot.Before = play =>
            {
                sitHilo(play);
                var t = play.Room.Tables["hilo"];
                var pl = f2.TableOf("hilo");
                chipShot.At = t.TransformPoint(new Vector3(-0.2f, (float)pl.H, -(float)pl.D / 2 + 0.42f));
            };
            list.Add(chipShot);
            list.Add(new Shot { Name = "46_pause", Eye = RoomBuilder.U(f2.Spawn.X, 1.6, f2.Spawn.Z), At = RoomBuilder.U(f2.Spawn.X, 1.5, f2.Spawn.Z) + new Vector3(0, 0, 5), Hud = true, Before = play => play.Pause.Show(true) });
            list.Add(new Shot
            {
                Name = "47_results", Eye = RoomBuilder.U(f2.Spawn.X, 1.6, f2.Spawn.Z), At = RoomBuilder.U(f2.Spawn.X, 1.5, f2.Spawn.Z) + new Vector3(0, 0, 5), Hud = true,
                Before = play =>
                {
                    play.Pause.Show(false);
                    play.G.State.Result = new Done { Won = false, Why = "Night 5: $3,180 short of the Collector's $3,800, and no lives left to pay it with." };
                    play.Results.Show(play.G);
                }
            });
            return list;
        }

        /// <summary>Step the sim, the events going through the game's own handlers (lights, HUD), and the
        /// tables' shows posed every tick as the game would (Twenty-One counts its cards out).</summary>
        static void Run(Play play, int ticks)
        {
            for (int k = 0; k < ticks; k++) { play.G.Step(); play.Drain(); play.Present(); }
        }

        /// <summary>Keep the night going and the bank deep enough for the shots' bets (a debug step, like the
        /// TS skipTo), and a spare life, so a run of rolls can't end it.</summary>
        static void Topup(Game g)
        {
            g.State.Bank = Math.Max(g.State.Bank, 6000);
            g.State.Clock = Math.Max(g.State.Clock, State.TicksPerS * 60 * 3);
            var p = g.State.Players[0];
            p.Lives = Math.Max(p.Lives, 2);
        }

        /// <summary>Send a round and stop `frac` of the way through its hold (a mid-flight shot).</summary>
        static void Roll(Play play, string table, Command c, float frac)
        {
            var g = play.G;
            Settle(play, table, 0);
            Topup(g);
            g.Submit(0, c);
            Run(play, 1);
            var t = g.State.Tables[table];
            if (t.Pending == null) return;
            int hold = t.Until - (g.Tick - 1);
            Run(play, Mathf.RoundToInt(frac * hold));
        }

        /// <summary>Let the round in the air land, then `after` seconds more.</summary>
        static void Settle(Play play, string table, float after)
        {
            for (int i = 0; i < 1000 && play.G.State.Tables[table].Pending != null; i++) Run(play, 1);
            Run(play, Mathf.RoundToInt(after * State.TicksPerS));
        }

        /// <summary>Play rounds until one lands that `ok` likes, and stop `after` seconds past its landing.</summary>
        static void Until(Play play, string table, Func<Command> round, Func<Outcome, bool> ok, float after)
        {
            for (int i = 0; i < 40; i++)
            {
                Settle(play, table, 0);
                Topup(play.G);
                play.G.Submit(0, round());
                Run(play, 1);
                Settle(play, table, 0);
                if (play.G.State.Tables[table].Last is Outcome o && ok(o)) break;
            }
            Run(play, Mathf.RoundToInt(after * State.TicksPerS));
        }

        static void SitAt(Play play, string table)
        {
            var g = play.G;
            var p = g.State.Players[0];
            if (p.Seat != null) { g.Submit(0, Command.Stand()); Run(play, 1); }
            var pl = Layout.FloorOf(g.State.Floor).TableOf(table);
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
            foreach (var s in Shots(play))
            {
                s.Before?.Invoke(play);
                play.Hud.Banner("", Color.clear, 0);
                play.Refresh();
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.renderMode == RenderMode.WorldSpace || canvas.transform.parent != null) continue;
                    canvas.renderMode = s.Hud ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 0.2f - canvas.sortingOrder * 0.002f;
                    if (canvas == play.Hud.Canvas) canvas.enabled = s.Hud;
                }
                play.Hud.Refresh(play.G, 0);
                play.Hud.BarUp(play.TableBar.Visible);
                cam.transform.position = s.Eye;
                cam.transform.rotation = Quaternion.LookRotation(s.At - s.Eye);
                // the people, as far on as the sim (and glancing at this camera)
                play.People.CatchUp(play.G, s.Eye);
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
