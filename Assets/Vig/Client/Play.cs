// The game in the scene: builds the room, runs the sim at a fixed 60 Hz, turns the keyboard and mouse
// into commands (never into state), and turns the sim's state and events into the camera, the screens,
// the HUD and the lights. Same rule as the TS game: presentation reads, commands write.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Systems;

namespace Vig.Client
{
    public sealed class Play : MonoBehaviour
    {
        public Game G;
        const int Me = 0;
        Camera cam;
        RoomBuilder room;
        Hud hud;
        readonly Dictionary<string, TableScreen> screens = new Dictionary<string, TableScreen>();
        CollectorScreen collScreen;
        readonly Dictionary<string, int> bets = new Dictionary<string, int>();
        readonly List<(Light l, float i)> lights = new List<(Light, float)>();
        double acc;
        float yaw, pitch;
        Vector3 prevSim, curSim;
        Vector3 camPos;
        Quaternion camRot;
        int lastRev = -1;

        /// <summary>Build the room and start a run (also called by the screenshot tool, in edit mode).</summary>
        public void Boot(bool live)
        {
            Atmosphere.Setup(transform);
            room = RoomBuilder.Build(transform);
            foreach (var l in room.RoomLights) lights.Add((l, l.intensity));
            cam = Atmosphere.MakeCamera(transform);
            foreach (var kv in room.Screens)
            {
                var pl = Layout.Floor1.TableOf(kv.Key);
                screens[kv.Key] = new TableScreen(kv.Key, kv.Value, (float)pl.Screen.W, (float)pl.Screen.H);
                bets[kv.Key] = Vig.Sim.Data.Tables.ById(kv.Key).Bet.Std;
            }
            var cs = Layout.Floor1.Collector.Screen;
            collScreen = new CollectorScreen(room.CollectorScreen, (float)cs.W, (float)cs.H);
            hud = new Hud();
            NewRun((uint)System.DateTime.Now.Ticks);
            if (live)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void NewRun(uint seed)
        {
            G = new Game(new GameOpts { Seed = seed == 0 ? 1u : seed, Players = 1 });
            var p = G.State.Players[Me];
            prevSim = curSim = new Vector3((float)p.X, 0, (float)p.Z);
            yaw = 0;
            pitch = 0;
            SetDim(false);
            camPos = Eye(curSim);
            camRot = Quaternion.Euler(pitch, yaw, 0);
            hud?.Banner("NIGHT 1", Color.white);
            RefreshAll(true);
        }

        string selfShot;
        int frames;

        void Start()
        {
            // a built player's self-test: `TheVig.exe -vigshot out.png` renders, saves a frame and quits
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-vigshot");
            if (i >= 0 && i + 1 < args.Length) selfShot = args[i + 1];
            if (G == null) Boot(selfShot == null);
        }

        void LateUpdate()
        {
            if (selfShot == null) return;
            frames++;
            if (frames == 90) ScreenCapture.CaptureScreenshot(selfShot);
            if (frames == 100) Application.Quit();
        }

        static Vector3 Eye(Vector3 sim) => RoomBuilder.U(sim.x, Layout.EyeH, sim.z);

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            var s = G.State;
            var p = s.Players[Me];
            // look
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * 0.08f;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80, 80);
            }
            Commands(kb, s, p);
            // the sim, at 60 Hz
            acc += Time.deltaTime;
            int steps = 0;
            while (acc >= 1.0 / State.TicksPerS && steps < 8)
            {
                acc -= 1.0 / State.TicksPerS;
                prevSim = curSim;
                G.Step();
                curSim = new Vector3((float)G.State.Players[Me].X, 0, (float)G.State.Players[Me].Z);
                foreach (var e in G.DrainEvents()) OnEvent(e);
                steps++;
            }
            if (steps == 8) acc = 0;
            CameraFollow();
            RefreshAll(false);
        }

        // ---------------------------------------------------------------- input → commands

        void Send(Command c) => G.Submit(Me, c);

        void Commands(Keyboard kb, State s, PlayerState p)
        {
            string prompt = null;
            if (s.Phase == Phase.Floor && !p.Ghost)
            {
                if (p.Seat == null)
                {
                    Walk(kb, p);
                    var near = Layout.Floor1.Tables.Where(t => Rules.Dist2(p.X, p.Z, t.Stand) <= Layout.UseRadius * Layout.UseRadius)
                        .OrderBy(t => Rules.Dist2(p.X, p.Z, t.Stand)).FirstOrDefault();
                    if (near != null)
                    {
                        var def = Vig.Sim.Data.Tables.ById(near.Table);
                        prompt = $"E  SIT AT {Ui.Col(def.Name.ToUpperInvariant(), Look.TierColor(def.Tier))}";
                        if (kb.eKey.wasPressedThisFrame) { Send(Command.Move(0, 0, 0)); Send(Command.Sit(near.Table)); }
                    }
                    else if (Rules.Dist2(p.X, p.Z, Layout.Floor1.Collector.Stand) <= CollectorSystem.CallRadius * CollectorSystem.CallRadius)
                    {
                        prompt = $"E  {Ui.Col("CALL THE NIGHT", Look.Coll)}  (THE CLOCK GOES TO 0:00)";
                        if (kb.eKey.wasPressedThisFrame) Send(Command.Callit());
                    }
                    else if (CageSystem.AtBar(p))
                    {
                        prompt = string.Join("    ", Drinks.All.Select((d, i) => $"{i + 1}  {d.Name.ToUpperInvariant()} {Ui.Money(d.Price)} {Ui.Col(d.EdgeText, Look.Smoke)}"));
                        for (int i = 0; i < Drinks.All.Length; i++)
                            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Send(Command.Order(Drinks.All[i].Id));
                    }
                    else if (CageSystem.AtCage(p))
                        prompt = Ui.Col("THE CAGE: CHARMS COME BY POST FROM THE HOUSE (NOT BUILT IN UNITY YET)", Look.Smoke);
                }
                else Seated(kb, p.Seat);
            }
            else if (s.Phase == Phase.Choice)
            {
                if (kb.pKey.wasPressedThisFrame) Send(Command.Pay());
                if (kb.gKey.wasPressedThisFrame) Send(Command.Gamble());
            }
            else if (s.Phase == Phase.Circuit && s.Circuit != null && s.Circuit.Done == null && s.Circuit.Turn == "team")
            {
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                for (int i = 0; i < 10; i++)
                {
                    var key = i == 9 ? Key.Digit0 : (Key)((int)Key.Digit1 + i);
                    if (kb[key].wasPressedThisFrame) Send(shift ? Command.Inspect(i) : Command.Activate(i));
                }
            }
            else if (s.Phase == Phase.Wheel && kb.spaceKey.wasPressedThisFrame) Send(Command.Fortune());
            else if (s.Phase == Phase.Summary && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Send(Command.Ready());
            else if (s.Phase == Phase.Over && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) NewRun((uint)System.DateTime.Now.Ticks);
            hud.Prompt(prompt);
        }

        void Walk(Keyboard kb, PlayerState p)
        {
            float f = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
            float r = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
            float a = yaw * Mathf.Deg2Rad;
            // Unity direction on the floor, then into the sim's frame (z flips)
            var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * f + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * r;
            if (dir.sqrMagnitude > 1) dir.Normalize();
            double mx = Js.Round(dir.x * 1000), mz = Js.Round(-dir.y * 1000);
            bool run = kb.leftShiftKey.isPressed;
            // the sim holds the intent: send only a change (a repeat before it lands is harmless)
            if (mx != p.Mx || mz != p.Mz || run != p.Run)
            {
                float wrapped = Mathf.Repeat(yaw + 180, 360) - 180;
                Send(Command.Move(mx, mz, Js.Round(-wrapped * Mathf.Deg2Rad * 1000), run));
            }
        }

        void Seated(Keyboard kb, string table)
        {
            var def = Vig.Sim.Data.Tables.ById(table);
            var lim = Vig.Sim.Data.Tables.LimitsOf(def, G.State.Floor);
            int b = bets[table];
            if (kb.leftBracketKey.wasPressedThisFrame) bets[table] = Mathf.Max(lim.Min, b - lim.Step);
            if (kb.rightBracketKey.wasPressedThisFrame) bets[table] = Mathf.Min(lim.Max, b + lim.Step);
            double amount = bets[table];
            switch (table)
            {
                case "hilo":
                    if (kb.hKey.wasPressedThisFrame) Send(Command.CallIt(table, "hi", amount));
                    if (kb.lKey.wasPressedThisFrame) Send(Command.CallIt(table, "lo", amount));
                    break;
                case "redline":
                    if (kb.rKey.wasPressedThisFrame) Send(Command.CallIt(table, "red", amount));
                    if (kb.bKey.wasPressedThisFrame) Send(Command.CallIt(table, "black", amount));
                    break;
                case "twentyone":
                    if (kb.spaceKey.wasPressedThisFrame) Send(Command.Bet(table, amount));
                    if (kb.hKey.wasPressedThisFrame) Send(Command.CallIt(table, "hit"));
                    if (kb.sKey.wasPressedThisFrame) Send(Command.CallIt(table, "stand"));
                    if (kb.dKey.wasPressedThisFrame) Send(Command.CallIt(table, "double"));
                    if (kb.pKey.wasPressedThisFrame) Send(Command.CallIt(table, "split"));
                    break;
                default:
                    if (kb.spaceKey.wasPressedThisFrame) Send(Command.Spin(table, amount));
                    break;
            }
            if (kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Send(Command.Stand());
        }

        // ---------------------------------------------------------------- events → presentation

        void OnEvent(SimEvent e)
        {
            switch (e.T)
            {
                case "round":
                {
                    var o = e.Outcome;
                    var def = Vig.Sim.Data.Tables.ById(o.Table);
                    int net = o.Ret - o.Bet;
                    string amt = net > 0 ? Ui.Col("+" + Ui.Money(net), Look.LowHi) : net < 0 ? Ui.Col(Ui.Money(net), Look.Blood) : Ui.Col("$0", Look.Smoke);
                    hud.Toast($"{Ui.Col(def.Sign, Look.TierColor(def.Tier))}  {o.Text}  {amt}");
                    if (net >= Feel.Jackpot) hud.Banner("JACKPOT", Look.Gold);
                    else if (net >= Feel.BigWin) hud.Banner("BIG WIN", Look.LowHi, 1.6f);
                    break;
                }
                case "mark": hud.Toast(Ui.Col($"MARKED: {Marks.ById(e.Id).Name.ToUpperInvariant()} — {Marks.ById(e.Id).Text}", Look.Blood)); break;
                case "tax": hud.Toast(Ui.Col($"TAX: {Ui.Money(e.Amount ?? 0)} FROM THE BANK", Look.Blood)); break;
                case "life":
                    if (e.Delta < 0) { hud.Banner("A LIFE", Look.Blood, 1.8f); hud.Toast(Ui.Col($"-1 LIFE ({e.Why})", Look.Blood)); }
                    else if (e.Delta > 0) hud.Toast(Ui.Col($"+1 LIFE ({e.Why})", Look.Soul));
                    else hud.Toast(Ui.Col(e.Why.ToUpperInvariant(), Look.Soul));
                    break;
                case "sat":
                {
                    // face the table: standing up again leaves you looking at it
                    var pl = Layout.Floor1.TableOf(e.Table);
                    var look = RoomBuilder.U(pl.Look) - RoomBuilder.U(pl.Eye);
                    yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                    pitch = 0;
                    break;
                }
                case "warning": hud.Toast(Ui.Col("RED LINE: ONE MORE LOSS COSTS A LIFE", Look.High)); break;
                case "markers": hud.Toast(Ui.Col($"+{e.Delta} MARKER{(e.Delta == 1 ? "" : "S")} ({e.Why})", Look.Marker)); break;
                case "refused": hud.Toast(Ui.Col(e.Why.ToUpperInvariant(), Look.Smoke), 2.5f); break;
                case "drank": hud.Toast(Ui.Col(Drinks.ById(e.Drink).Name.ToUpperInvariant() + ": " + Drinks.ById(e.Drink).EdgeText, Look.PinkHi)); break;
                case "midnight":
                    SetDim(true);
                    hud.Banner("0:00", Look.Coll, 3);
                    break;
                case "shortfall": hud.Banner($"{Ui.Money(e.Short ?? 0)} SHORT", Look.Blood, 3); break;
                case "paid": hud.Toast(Ui.Col($"PAID {Ui.Money(e.Quota ?? 0)}", Look.Brass)); break;
                case "circuit": hud.Toast(e.Who == "coll" ? Ui.Col(e.Text, Look.CollHi) : Ui.Col(e.Text, e.Over == true ? Look.Blood : Look.Bone)); break;
                case "circuitEnd": hud.Banner(e.Won == true ? "HE IS DEAD" : "THE QUOTA IS HIS", e.Won == true ? Look.Soul : Look.Blood, 3); break;
                case "landed": hud.Banner(e.Text, e.Kind == "boon" ? Look.Soul : Look.Blood, 3); break;
                case "night":
                    SetDim(false);
                    var p = G.State.Players[Me];
                    prevSim = curSim = new Vector3((float)p.X, 0, (float)p.Z);
                    yaw = 0; pitch = 0;
                    hud.Banner($"NIGHT {e.Night}", Color.white);
                    break;
                case "over": hud.Banner(e.Won == true ? "YOU WALK OUT" : "GAME OVER", e.Won == true ? Look.Soul : Look.Blood, 5); break;
            }
        }

        /// <summary>At 0:00 the lights drop everywhere but the Collector's table.</summary>
        void SetDim(bool on)
        {
            foreach (var (l, i) in lights)
            {
                if (!l) continue;
                bool his = l.transform.IsChildOf(room.Root.Find("The Collector"));
                l.intensity = on && !his ? i * 0.12f : i;
            }
        }

        // ---------------------------------------------------------------- camera and screens

        void CameraFollow()
        {
            var s = G.State;
            var p = s.Players[Me];
            Vector3 targetPos;
            Quaternion targetRot;
            float alpha = (float)(acc * State.TicksPerS);
            if (s.Phase == Phase.Floor && p.Seat != null)
            {
                var pl = Layout.Floor1.TableOf(p.Seat);
                targetPos = RoomBuilder.U(pl.Eye);
                var look = RoomBuilder.U(pl.Look) - targetPos;
                targetRot = Quaternion.LookRotation(look);
            }
            else if (s.Phase != Phase.Floor && s.Phase != Phase.Home)
            {
                var c = Layout.Floor1.Collector;
                targetPos = RoomBuilder.U(c.Eye);
                targetRot = Quaternion.LookRotation(RoomBuilder.U(c.Look) - targetPos);
            }
            else
            {
                targetPos = Eye(Vector3.Lerp(prevSim, curSim, alpha));
                targetRot = Quaternion.Euler(pitch, yaw, 0);
                // standing: the camera is the body, no easing
                camPos = targetPos;
                camRot = targetRot;
            }
            float k = 1 - Mathf.Exp(-Time.deltaTime * 7);
            camPos = Vector3.Lerp(camPos, targetPos, k);
            camRot = Quaternion.Slerp(camRot, targetRot, k);
            cam.transform.SetPositionAndRotation(camPos, camRot);
        }

        void RefreshAll(bool force)
        {
            if (G == null || hud == null) return;
            var me = G.State.Players[Me];
            if (force || G.Rev != lastRev || me.Seat != null)
            {
                foreach (var kv in screens) kv.Value.Refresh(G, me, bets[kv.Key]);
                collScreen.Refresh(G);
                lastRev = G.Rev;
            }
            hud.Refresh(G, Me);
        }

        /// <summary>For the screenshot tool: stand at a point looking somewhere, with no input.</summary>
        public void Pose(Vector3 unityPos, Vector3 lookAt)
        {
            cam.transform.position = unityPos;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - unityPos);
        }

        public Camera Cam => cam;
        public Hud Hud => hud;
        public RoomBuilder Room => room;
        public void Refresh() => RefreshAll(true);
        /// <summary>Hand the sim's pending events to the presentation (the screenshot tool steps the sim itself).</summary>
        public void Drain() { foreach (var e in G.DrainEvents()) OnEvent(e); }
    }
}
