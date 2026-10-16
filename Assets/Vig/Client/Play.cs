// The game in the scene: runs the sim at a fixed 60 Hz, turns the keyboard, the mouse and the buttons into
// commands (never into state), and turns the sim's state and events into the worlds (the house, a casino
// floor), the camera, the screens, the shows, the people, the sound, the HUD and the menus. Same rule as
// the TS game: presentation reads, commands write.
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
        Hud hud;
        TableUi ui;
        TitleUi title;
        ShopUi shop;
        PauseUi pause;
        ResultsUi results;
        // the casino floor that's built (rebuilt on the stairs down), and the house
        Transform casinoRoot;
        int builtFloor;
        RoomBuilder room;
        People people;
        readonly Dictionary<string, TableScreen> screens = new Dictionary<string, TableScreen>();
        CollectorScreen collScreen;
        Dictionary<string, TableShow> shows = new Dictionary<string, TableShow>();
        readonly Dictionary<string, BetChips> chips = new Dictionary<string, BetChips>();
        readonly List<(Light l, float i)> lights = new List<(Light, float)>();
        HouseBuilder house;
        People family;
        string world;
        double acc;
        float yaw, pitch;
        Vector3 prevSim, curSim;
        Vector3 camPos;
        Quaternion camRot;
        int lastRev = -1;
        bool paused;
        /// <summary>wins in a row, any table (the streak banners)</summary>
        int streak;

        /// <summary>Build everything and start a run (also called by the screenshot tool, in edit mode).</summary>
        public void Boot(bool live)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Atmosphere.Setup(transform);
            cam = Atmosphere.MakeCamera(transform);
            Sfx.Init(transform);
            casinoRoot = new GameObject("Casino").transform;
            casinoRoot.SetParent(transform, false);
            BuildCasino(1);
            house = HouseBuilder.Build(transform);
            family = People.BuildFamily(house.Root);
            Debug.Log($"Vig: the worlds built in {clock.ElapsedMilliseconds} ms");
            hud = new Hud();
            ui = new TableUi(Send) { NewRun = () => { title.Show(true); paused = true; } };
            shop = new ShopUi(Send);
            title = new TitleUi(StartChosen);
            Settings.Apply();
            pause = new PauseUi(() => pause.Show(false), ShowTitle);
            results = new ResultsUi(() => { results.Show(false); StartChosen(); }, ShowTitle);
            title.Show(live);
            paused = live;
            NewRun((uint)System.DateTime.Now.Ticks);
            SetWorld(true);
            Debug.Log($"Vig: booted in {clock.ElapsedMilliseconds} ms");
        }

        /// <summary>PLAY on the title screen: a run on the chosen options.</summary>
        void StartChosen()
        {
            NewRun((uint)System.DateTime.Now.Ticks, Options.StartHome, Options.StartFloor, Options.Curve);
            title.Show(false);
            paused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Sfx.Play("door");
        }

        /// <summary>Back to the title: the room turns behind it.</summary>
        void ShowTitle()
        {
            pause.Show(false);
            results.Show(false);
            shop.Show(false);
            title.Show(true);
            paused = true;
        }

        public void NewRun(uint seed) => NewRun(seed, false, 1, "easy");

        public void NewRun(uint seed, bool home, int startFloor, string curve)
        {
            G = new Game(new GameOpts { Seed = seed == 0 ? 1u : seed, Players = 1, Home = home, StartFloor = startFloor, Curve = curve, Unlocked = Options.Unlocked });
            var p = G.State.Players[Me];
            prevSim = curSim = new Vector3((float)p.X, 0, (float)p.Z);
            yaw = home ? -90 : 0;
            pitch = 0;
            SetWorld(true);
            SetDim(false);
            camPos = Eye(curSim);
            camRot = Quaternion.Euler(pitch, yaw, 0);
            hud?.Clear();
            hud?.Banner(home ? "HOME" : $"NIGHT {G.State.Night}", Color.white);
            foreach (var sh in shows.Values) sh.Reset();
            RefreshAll(true);
        }

        // ---------------------------------------------------------------- the worlds

        void BuildCasino(int floor)
        {
            if (room != null) Kill(room.Root.gameObject);
            if (people != null) Kill(people.Root.gameObject);
            screens.Clear();
            lights.Clear();
            builtFloor = floor;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            room = RoomBuilder.Build(casinoRoot, floor);
            people = People.Build(casinoRoot, floor);
            foreach (var kv in room.Screens)
            {
                var pl = Layout.FloorOf(floor).TableOf(kv.Key);
                screens[kv.Key] = new TableScreen(kv.Key, kv.Value, (float)pl.Screen.W, (float)pl.Screen.H);
            }
            shows = TableShow.Build(room);
            chips.Clear();
            foreach (var id in new[] { "hilo", "wheel", "deadman", "roulette", "craps", "crash" })
                if (room.Tables.TryGetValue(id, out var tt)) chips[id] = new BetChips(id, tt, Layout.FloorOf(floor).TableOf(id));
            if (screens.TryGetValue("twentyone", out var t21) && shows.TryGetValue("twentyone", out var s21)) t21.Deal = (TwentyOneShow)s21;
            foreach (var l in room.Root.GetComponentsInChildren<Light>(true))
                if (l.name != "flash") lights.Add((l, l.intensity));
            var cs = Layout.FloorOf(floor).Collector.Screen;
            collScreen = new CollectorScreen(room.CollectorScreen, (float)cs.W, (float)cs.H);
            lastRev = -1;
            Debug.Log($"Vig: floor {floor} built in {clock.ElapsedMilliseconds} ms");
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        /// <summary>The house while the run is at home, the run's floor otherwise.</summary>
        void SetWorld(bool force = false)
        {
            if (G == null || house == null) return;
            var s = G.State;
            string want = s.Phase == Phase.Home ? "home" : "floor" + s.Floor;
            if (want == world && !force) return;
            world = want;
            bool home = s.Phase == Phase.Home;
            if (!home && builtFloor != s.Floor) BuildCasino(s.Floor);
            casinoRoot.gameObject.SetActive(!home);
            house.Root.gameObject.SetActive(home);
            RenderSettings.fogColor = home ? new Color(0.02f, 0.03f, 0.06f) : s.Floor >= 2 ? new Color(0.07f, 0.04f, 0.02f) : new Color(0.07f, 0.035f, 0.07f);
            RenderSettings.fogDensity = home ? 0.02f : 0.028f;
            RenderSettings.ambientSkyColor = home ? new Color(0.16f, 0.16f, 0.22f) : new Color(0.11f, 0.07f, 0.13f);
            RenderSettings.ambientEquatorColor = home ? new Color(0.12f, 0.1f, 0.1f) : new Color(0.08f, 0.05f, 0.07f);
            if (!home && shop != null) shop.Show(false);
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
            // the self-test can start where it should look: `-vigstart home` or `-vigstart floor2`
            int j = System.Array.IndexOf(args, "-vigstart");
            if (selfShot != null && j >= 0 && j + 1 < args.Length)
                NewRun(77, args[j + 1] == "home", args[j + 1] == "floor2" ? 2 : 1, "easy");
        }

        void LateUpdate()
        {
            if (selfShot == null) return;
            frames++;
            if (frames == 90) ScreenCapture.CaptureScreenshot(selfShot);
            if (frames == 100) Application.Quit();
        }

        static Vector3 Eye(Vector3 sim) => RoomBuilder.U(sim.x, Layout.EyeH, sim.z);

        bool MenuOpen => (title != null && title.Open) || (shop != null && shop.Open) || (pause != null && pause.Open) || (results != null && results.Open);

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || G == null) return;
            var s = G.State;
            var p = s.Players[Me];
            ui.Refresh(G, p);
            shop.Refresh(G, p);
            bool free = MenuOpen || ui.Visible;
            hud.BarUp(ui.Visible);
            if (kb.escapeKey.wasPressedThisFrame)
            {
                // Esc: close the catalogue, or pause (and unpause)
                if (shop.Open) shop.Show(false);
                else if (!title.Open && !results.Open) pause.Show(!pause.Open);
            }
            if (free) { if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (pause.Open) { hud.Prompt(null); return; }
            if (paused)
            {
                // the title: the room turns slowly behind it
                float a = Time.time * 0.05f;
                var eye = RoomBuilder.U(Mathf.Sin(a) * 6, 2.4, 9 + Mathf.Cos(a) * 5);
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(RoomBuilder.U(0, 1.2, 5) - eye));
                if (world != "home") people.Tick(G, Time.deltaTime, eye);
                RefreshAll(false);
                Animate(0);
                return;
            }
            // look
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked && !free)
            {
                var d = mouse.delta.ReadValue() * 0.08f * Settings.Sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80, 80);
            }
            if (!MenuOpen) Commands(kb, s, p);
            else hud.Prompt(null);
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
            SetWorld();
            CameraFollow();
            if (world == "home") { family.Tick(G, Time.deltaTime, cam.transform.position); HomeView(); }
            else people.Tick(G, Time.deltaTime, cam.transform.position);
            RefreshAll(false);
            Animate((float)(acc * State.TicksPerS));
        }

        /// <summary>Pose every table's round for this moment (`alpha`: the fraction of the next tick).</summary>
        void Animate(float alpha)
        {
            if (G == null || world == "home") return;
            foreach (var sh in shows.Values) sh.Update(G, G.Tick + alpha);
            var me = G.State.Players[Me];
            foreach (var kv in chips) kv.Value.Update(G, me, ui != null ? ui.Bet(kv.Key) : 0, (G.Tick + alpha) / State.TicksPerS);
        }

        void HomeView()
        {
            var s = G.State;
            if (house.Flag) house.Flag.localRotation = Quaternion.Euler(s.Post.Box.Count > 0 ? -90 : 0, 0, 0);
            if (house.TvScreen) house.TvScreen.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_EmissionColor", new Color(0.25f, 0.45f, 0.9f) * (1.1f + 0.4f * Mathf.PerlinNoise(Time.time * 3, 0)));
        }

        /// <summary>Where the seated eye looks at a table: its screen, or beside it where the show is.</summary>
        public Vector3 SeatedLook(Placement pl) => RoomBuilder.U(pl.Look) + (shows.TryGetValue(pl.Table, out var sh) ? sh.LookShift : Vector3.zero);

        // ---------------------------------------------------------------- input → commands

        void Send(Command c) => G.Submit(Me, c);

        Floor F => Layout.FloorOf(G.State.Floor);

        void Commands(Keyboard kb, State s, PlayerState p)
        {
            string prompt = null;
            bool e = kb.eKey.wasPressedThisFrame;
            if (s.Phase == Phase.Home)
            {
                Walk(kb, p);
                if (HomeSystem.AtComputer(p))
                {
                    prompt = $"E  {Ui.Col("THE COMPUTER", new Color(0.5f, 1f, 0.6f))}: TONIGHT'S CHARMS ({s.Markers} MARKERS)";
                    if (e) { shop.Show(true); Send(Command.Move(0, 0, 0)); }
                }
                else if (HomeSystem.AtMailbox(p))
                {
                    prompt = s.Post.Box.Count > 0 ? $"E  PICK UP THE POST ({s.Post.Box.Count})" : Ui.Col(s.Post.Orders.Count > 0 ? "THE POST IS ON ITS WAY" : "THE MAILBOX IS EMPTY", Look.Smoke);
                    if (e && s.Post.Box.Count > 0) Send(Command.Pickup());
                }
                else if (HomeSystem.AtCar(p))
                {
                    prompt = $"E  {Ui.Col("DRIVE TO THE VIG", Look.PinkHi)}  (NIGHT {s.Night}, FLOOR {s.Floor}, QUOTA {Ui.Money(s.Quota)})" + (s.Post.Box.Count > 0 ? Ui.Col("   YOUR POST IS STILL IN THE MAILBOX", Look.Gold) : "");
                    if (e) Send(Command.Drive());
                }
            }
            else if (s.Phase == Phase.Floor && !p.Ghost)
            {
                if (p.Seat == null)
                {
                    Walk(kb, p);
                    var near = F.Tables.Where(t => Rules.Dist2(p.X, p.Z, t.Stand) <= Layout.UseRadius * Layout.UseRadius)
                        .OrderBy(t => Rules.Dist2(p.X, p.Z, t.Stand)).FirstOrDefault();
                    if (near != null)
                    {
                        var def = Vig.Sim.Data.Tables.ById(near.Table);
                        prompt = $"E  SIT AT {Ui.Col(def.Name.ToUpperInvariant(), Look.TierColor(def.Tier))}   {Ui.Col(def.LossText, Look.Smoke)}";
                        if (e) { Send(Command.Move(0, 0, 0)); Send(Command.Sit(near.Table)); }
                    }
                    else if (Rules.Dist2(p.X, p.Z, F.Collector.Stand) <= CollectorSystem.CallRadius * CollectorSystem.CallRadius)
                    {
                        prompt = $"E  {Ui.Col("CALL THE NIGHT", Look.Coll)}  (THE CLOCK GOES TO 0:00)";
                        if (e) Send(Command.Callit());
                    }
                    else if (CageSystem.AtBar(p))
                    {
                        prompt = string.Join("    ", Drinks.All.Select((d, i) => $"{i + 1}  {d.Name.ToUpperInvariant()} {Ui.Money(CageSystem.PriceOf(p, d))} {Ui.Col(d.EdgeText, Look.Smoke)}"));
                        for (int i = 0; i < Drinks.All.Length; i++)
                            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Send(Command.Order(Drinks.All[i].Id));
                    }
                    else if (CageSystem.AtCage(p))
                        prompt = Ui.Col("THE CAGE IS CLOSED: CHARMS COME BY POST, FROM THE COMPUTER AT HOME", Look.Smoke);
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
                for (int i = 0; i < 10 && i < s.Circuit.Levers.Count; i++)
                {
                    var key = i == 9 ? Key.Digit0 : (Key)((int)Key.Digit1 + i);
                    if (kb[key].wasPressedThisFrame) Send(shift ? Command.Inspect(i) : Command.Activate(i));
                }
            }
            else if (s.Phase == Phase.Wheel && kb.spaceKey.wasPressedThisFrame) Send(Command.Fortune());
            else if (s.Phase == Phase.Summary && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Send(Command.Ready());
            else if (s.Phase == Phase.Over && !results.Open) { hud.Clear(); results.Show(G); }
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

        /// <summary>The keyboard at a table, beside the buttons: [ ] the bet, the table's keys, X to stand.</summary>
        void Seated(Keyboard kb, string table)
        {
            if (kb.leftBracketKey.wasPressedThisFrame) ui.Nudge(table, -1);
            if (kb.rightBracketKey.wasPressedThisFrame) ui.Nudge(table, 1);
            double amount = ui.Bet(table);
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
                case "crash":
                    if (kb.spaceKey.wasPressedThisFrame) Send(G.State.Tables["crash"].Crash != null ? Command.CallIt(table, "cash") : Command.Spin(table, amount));
                    break;
                case "roulette":
                    break;
                default:
                    if (kb.spaceKey.wasPressedThisFrame) Send(Command.Spin(table, amount));
                    break;
            }
            if (kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Send(Command.Stand());
        }

        // ---------------------------------------------------------------- events → presentation

        Vector3? TableAt(string table)
        {
            var pl = table == null ? null : F.TableOf(table);
            return pl == null ? (Vector3?)null : RoomBuilder.U(pl.X, pl.H + 0.2, pl.Z);
        }

        void OnEvent(SimEvent e)
        {
            if (world != "home") people?.OnEvent(e, G);
            switch (e.T)
            {
                case "play":
                {
                    if (e.Table != null && shows.TryGetValue(e.Table, out var played)) played.OnPlay(e.Ticks ?? 0);
                    var at = TableAt(e.Table);
                    float hold = (e.Ticks ?? 60) / 60f;
                    switch (e.Table)
                    {
                        case "hilo": case "deadman": for (int k = 0; k < 5; k++) Sfx.Play("die", at, 0.6f, 1, hold * (0.3 + k * 0.13)); break;
                        case "redline": case "twentyone": Sfx.Play("card", at); Sfx.Play("card", at, 0.8f, 1.1f, hold * 0.7); break;
                        case "bandit": for (int k = 0; k < 12; k++) Sfx.Play("reel", at, 0.45f, 1, k * 0.11); for (int k = 0; k < 3; k++) Sfx.Play("thunk", at, 0.7f, 1, hold * (0.45 + k * 0.2)); break;
                        case "wheel": for (int k = 0; k < 26; k++) Sfx.Play("flap", at, 0.5f, 1, hold * Mathf.Pow(k / 26f, 0.5f)); break;
                        case "roulette": Sfx.Play("ball", at, 0.7f, 1, hold * 0.55); Sfx.Play("whoosh", at, 0.4f); break;
                        case "pegdrop": for (int k = 0; k < 9; k++) Sfx.Play("flap", at, 0.55f, 0.8f + k * 0.05f, hold * k / 9.0); break;
                    }
                    break;
                }
                case "roll":
                    if (e.Table != null && shows.TryGetValue(e.Table, out var rolled) && rolled is CrapsShow cs) cs.OnRoll(e.Dice, G.Tick, e.Ticks ?? 60);
                    hud.Toast($"{Ui.Col("CRAPS", Look.Low)}  {e.Text}");
                    break;
                case "launch": Sfx.Play("rocket", TableAt(e.Table), 0.7f); break;
                case "round":
                {
                    var o = e.Outcome;
                    if (shows.TryGetValue(o.Table, out var landed)) landed.OnLanded(o, G.Tick);
                    var def = Vig.Sim.Data.Tables.ById(o.Table);
                    int net = o.Ret - o.Bet;
                    var at = TableAt(o.Table);
                    string amt = net > 0 ? Ui.Col("+" + Ui.Money(net), Look.LowHi) : net < 0 ? Ui.Col(Ui.Money(net), Look.Blood) : Ui.Col("$0", Look.Smoke);
                    hud.Toast($"{Ui.Col(def.Sign, Look.TierColor(def.Tier))}  {o.Text}  {amt}");
                    if (o.Pot != null) { hud.Banner($"THE PROGRESSIVE: {Ui.Money(o.Pot.Value)}", Look.Gold, 4); Sfx.Play("jackpot"); Sfx.Play("coins"); Sfx.Play("cheer", at); }
                    else if (net >= Feel.Jackpot) { hud.Banner("JACKPOT", Look.Gold); Sfx.Play("jackpot"); Sfx.Play("coins"); Sfx.Play("cheer", at); }
                    else if (net >= Feel.BigWin) { hud.Banner("BIG WIN", Look.LowHi, 1.6f); Sfx.Play("bigwin"); Sfx.Play("coins", null, 0.7f); Sfx.Play("cheer", at, 0.6f); }
                    else if (net > 0) Sfx.Play(o.Table == "crash" ? "ching" : "win", null, 0.8f);
                    else if (o.Crashed == true) Sfx.Play("boom", at);
                    else if (net < 0) Sfx.Play("lose", at, 0.7f);
                    if (net <= -Feel.BigWin) Sfx.Play("groan", at, 0.6f);
                    // a run of wins gets the room's attention
                    streak = net > 0 ? streak + 1 : net < 0 ? 0 : streak;
                    if (net > 0 && streak >= 3 && net < Feel.BigWin) hud.Banner(streak >= 6 ? $"ON FIRE: {streak} IN A ROW" : $"ON A ROLL: {streak} IN A ROW", Look.Gold, 1.4f);
                    // the Bandit's near miss, only when it really was one: two sevens on a line and the third not
                    if (o.Table == "bandit" && o.Reels != null && !o.Win && NearMiss(o.Reels)) { hud.Toast(Ui.Col("SO CLOSE: TWO SEVENS ON A LINE", Look.Gold)); Sfx.Play("groan", at, 0.35f, 1.3f); }
                    break;
                }
                case "ride":
                    if (e.Won == true) { hud.Banner($"DOUBLED: {Ui.Money(e.Amount ?? 0)}", Look.LowHi, 1.6f); Sfx.Play("ching"); Sfx.Play("win"); }
                    else { hud.Banner("GONE", Look.Blood, 1.4f); Sfx.Play("lose"); Sfx.Play("groan", null, 0.5f); }
                    break;
                case "mark": hud.Toast(Ui.Col($"MARKED: {Marks.ById(e.Id).Name.ToUpperInvariant()} — {Marks.ById(e.Id).Text}", Look.Blood)); Sfx.Play("mark", null, 0.7f); break;
                case "tax": hud.Toast(Ui.Col($"TAX: {Ui.Money(e.Amount ?? 0)} FROM THE BANK", Look.Blood)); break;
                case "life":
                    if (e.Delta < 0) { hud.Banner("A LIFE", Look.Blood, 1.8f); hud.Toast(Ui.Col($"-1 LIFE ({e.Why})", Look.Blood)); Sfx.Play("life"); }
                    else if (e.Delta > 0) { hud.Toast(Ui.Col($"+1 LIFE ({e.Why})", Look.Soul)); Sfx.Play("coin"); }
                    else hud.Toast(Ui.Col(e.Why.ToUpperInvariant(), Look.Soul));
                    break;
                case "sat":
                {
                    // face the table: standing up again leaves you looking at it
                    var pl = F.TableOf(e.Table);
                    var look = SeatedLook(pl) - RoomBuilder.U(pl.Eye);
                    yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                    pitch = 0;
                    Sfx.Play("chip", TableAt(e.Table), 0.5f);
                    break;
                }
                case "warning": hud.Toast(Ui.Col("RED LINE: ONE MORE LOSS COSTS A LIFE", Look.High)); hud.Banner("ONE MORE LOSS: A LIFE", Look.High, 1.6f); break;
                case "markers": hud.Toast(Ui.Col($"+{e.Delta} MARKER{(e.Delta == 1 ? "" : "S")} ({e.Why})", Look.Marker)); Sfx.Play("coin", null, 0.6f); break;
                case "refused": hud.Toast(Ui.Col(e.Why.ToUpperInvariant(), Look.Smoke), 2.5f); Sfx.Play("click", null, 0.5f, 0.6f); break;
                case "drank": hud.Toast(Ui.Col(Drinks.ById(e.Drink).Name.ToUpperInvariant() + ": " + Drinks.ById(e.Drink).EdgeText, Look.PinkHi)); Sfx.Play("ching", null, 0.4f, 1.4f); break;
                case "shuffle": for (int k = 0; k < 4; k++) Sfx.Play("card", TableAt(e.Table), 0.6f, 1.2f, k * 0.08); break;
                case "interest": hud.Toast(Ui.Col($"COMPOUND INTEREST: +{Ui.Money(e.Amount ?? 0)}", Look.Gold)); Sfx.Play("ching"); break;
                case "floor": hud.Banner($"FLOOR {e.Floor}", Look.Gold, 4); break;
                case "midnight":
                    SetDim(true);
                    hud.Banner("0:00", Look.Coll, 3);
                    Sfx.Play("bell");
                    Sfx.Mood(true);
                    break;
                case "shortfall": hud.Banner($"{Ui.Money(e.Short ?? 0)} SHORT", Look.Blood, 3); break;
                case "paid": hud.Toast(Ui.Col($"PAID {Ui.Money(e.Quota ?? 0)}", Look.Brass)); Sfx.Play("chip"); break;
                case "circuit":
                    hud.Toast(e.Who == "coll" ? Ui.Col(e.Text, Look.CollHi) : Ui.Col(e.Text, e.Over == true ? Look.Blood : Look.Bone));
                    if (e.Action == "activate") { Sfx.Play("lever"); if (e.Over == true) Sfx.Play("shock", null, 0.9f, 1, 0.12); }
                    break;
                case "circuitEnd": hud.Banner(e.Won == true ? "HE IS DEAD" : "THE QUOTA IS HIS", e.Won == true ? Look.Soul : Look.Blood, 3); Sfx.Play(e.Won == true ? "cheer" : "groan"); break;
                case "landed": hud.Banner(e.Text, e.Kind == "boon" ? Look.Soul : Look.Blood, 3); Sfx.Play(e.Kind == "boon" ? "bigwin" : "mark"); break;
                case "unlocked":
                    Options.Unlocked = Options.Unlocked.Union(new[] { e.Charm }).ToArray();
                    hud.Toast(Ui.Col($"UNLOCKED FOR EVERY RUN: {Charms.ById(e.Charm)?.Name.ToUpperInvariant()}", Look.Gold), 6);
                    break;
                case "ordered": hud.Toast(Ui.Col($"ORDERED: {Charms.ById(e.Item)?.Name.ToUpperInvariant() ?? e.Item}. IT COMES BY POST.", new Color(0.5f, 1f, 0.6f))); Sfx.Play("click"); break;
                case "shop": Sfx.Play("card", null, 0.6f); break;
                case "post": hud.Toast(Ui.Col("THE POST HAS COME. IT'S IN THE MAILBOX.", Look.Gold)); Sfx.Play("coin"); break;
                case "collected": hud.Toast(Ui.Col("CARRIED IN: " + string.Join(", ", e.Items.Select(i => Charms.ById(i)?.Name.ToUpperInvariant() ?? i)), Look.Gold)); Sfx.Play("chip"); break;
                case "drive": hud.Banner($"NIGHT {e.Night}", Color.white); Sfx.Play("door"); break;
                case "night":
                {
                    SetDim(false);
                    Sfx.Mood(false);
                    var p = G.State.Players[Me];
                    prevSim = curSim = new Vector3((float)p.X, 0, (float)p.Z);
                    yaw = G.State.Home ? -90 : 0; pitch = 0;
                    hud.Banner(G.State.Home ? "HOME" : $"NIGHT {e.Night}", Color.white);
                    break;
                }
                case "over": hud.Banner(e.Won == true ? "YOU WALK OUT" : "GAME OVER", e.Won == true ? Look.Soul : Look.Blood, 5); break;
            }
        }

        static bool NearMiss(List<int> g)
        {
            foreach (var line in Vig.Sim.Tables.Bandit.Lines)
            {
                int sevens = line.Count(i => g[i] == Vig.Sim.Tables.Bandit.Seven);
                if (sevens == 2) return true;
            }
            return false;
        }

        /// <summary>At 0:00 the lights drop everywhere but the Collector's table.</summary>
        void SetDim(bool on)
        {
            if (room == null) return;
            var his = room.Root.Find("The Collector");
            foreach (var (l, i) in lights)
            {
                if (!l) continue;
                l.intensity = on && !(his && l.transform.IsChildOf(his)) ? i * 0.12f : i;
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
                var pl = F.TableOf(p.Seat);
                targetPos = RoomBuilder.U(pl.Eye);
                // the bar has the controls: look down past the screen to the felt, where the game is
                targetRot = Quaternion.LookRotation(SeatedLook(pl) + Vector3.down * 0.32f - targetPos);
            }
            else if (s.Phase != Phase.Floor && s.Phase != Phase.Home)
            {
                var (eye, at) = CollectorView(s.Floor);
                targetPos = eye;
                targetRot = Quaternion.LookRotation(at - eye);
            }
            else
            {
                targetPos = Eye(Vector3.Lerp(prevSim, curSim, alpha));
                targetRot = Quaternion.Euler(pitch, yaw, 0);
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
            if (world != "home" && (force || G.Rev != lastRev || me.Seat != null))
            {
                foreach (var kv in screens) kv.Value.Refresh(G, me, ui != null ? ui.Bet(kv.Key) : Vig.Sim.Data.Tables.ById(kv.Key).Bet.Std);
                collScreen.Refresh(G);
                lastRev = G.Rev;
            }
            hud.Refresh(G, Me);
            if (room != null) foreach (var t in room.PotTexts) if (t) t.text = Ui.Money(G.State.Pot);
        }

        /// <summary>
        /// Where you look at the Collector's table: from the sim's seated eye, not down at the screen as the
        /// sim's look point has it, but up across the screen to him (DECISIONS U14).
        /// </summary>
        public static (Vector3 eye, Vector3 at) CollectorView(int floor = 1)
        {
            var c = Layout.FloorOf(floor).Collector;
            var toHim = RoomBuilder.U(c.X, 0, c.Z) - RoomBuilder.U(c.Stand.X, 0, c.Stand.Z);
            toHim.y = 0;
            return (RoomBuilder.U(c.Eye), RoomBuilder.U(c.Look) + Vector3.up * 0.22f + toHim.normalized * 0.4f);
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
        public People People => people;
        public TableUi TableBar => ui;
        public ShopUi Shop => shop;
        public PauseUi Pause => pause;
        public ResultsUi Results => results;
        public HouseBuilder House => house;
        public void Refresh()
        {
            SetWorld();
            RefreshAll(true);
            if (ui != null) ui.Refresh(G, G.State.Players[Me]);
            if (shop != null) shop.Refresh(G, G.State.Players[Me]);
            Animate(0);
        }
        /// <summary>For the screenshot tool, every tick it steps: the shows keep count of what they've dealt.</summary>
        public void Present() => Animate(0);
        /// <summary>Hand the sim's pending events to the presentation (the screenshot tool steps the sim itself).</summary>
        public void Drain() { foreach (var e in G.DrainEvents()) OnEvent(e); SetWorld(); }
    }
}
