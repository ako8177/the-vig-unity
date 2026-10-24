// The seated UI (U19): when you sit, the cursor comes free and the table's controls come up across the
// bottom of the screen. The odds card on the left (tier, win chance, payout, what losing costs: always
// printed, CLAUDE.md), a chip rack and your bet in the middle, the table's own moves on the right, a
// "let it ride" offer after every win, Crash's live cash-out, roulette's betting layout. At the
// Collector's table the same bar carries pay / challenge, the levers, the wheel and the night's end.
// Every button sends a command; the bar reads the sim's state and never writes it.
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Tables;

namespace Vig.Client
{
    public sealed class TableUi
    {
        readonly Action<Command> send;
        readonly Canvas canvas;
        readonly RectTransform bar, actions, chipRow, ridePanel, layoutPanel, leverRow;
        readonly TextMeshProUGUI oddsText, betLabel, betText, rideText, collText, statusText;
        readonly Image rideTimer;
        readonly Button standBtn, rideBtn, takeBtn, clearBtn, maxBtn;
        readonly Dictionary<string, int> bets = new Dictionary<string, int>();
        readonly List<Button> live = new List<Button>();
        readonly Dictionary<string, Button> named = new Dictionary<string, Button>();
        readonly Dictionary<string, Image> layoutCells = new Dictionary<string, Image>();
        string mode;
        string rouletteCall = "red";
        int crashAuto;
        /// <summary>the PAIRS side bet rides with each deal (U30)</summary>
        bool sidePairs;
        /// <summary>craps' one-roll bet riding with each throw: 0 none, else 1 + its index in SideBets.OneRoll (U34)</summary>
        int crapsSide;
        OneRollDef CrapsOneRoll => crapsSide == 0 ? null : SideBets.OneRoll[crapsSide - 1];

        /// <summary>Turn a side bet on by its call (null: off), as its button would; the screenshot sweep uses it.</summary>
        public void PickSide(string table, string call)
        {
            if (table == "twentyone") sidePairs = call == SideBets.Pairs.Id;
            if (table == "craps") crapsSide = Array.FindIndex(SideBets.OneRoll, d => d.Id == call) + 1;
        }

        /// <summary>The side bet toggled on at this table, as the command's call (null: none); the keys use it too.</summary>
        public string SideCall(string table) => table == "twentyone" ? (sidePairs ? SideBets.Pairs.Id : null) : table == "craps" ? CrapsOneRoll?.Id : null;
        static readonly double[] AutoSteps = { 0, 1.5, 2, 3, 5, 10 };
        int dismissedRide = -1;
        public bool Visible { get; private set; }

        static readonly Color Panel = new Color(0.02f, 0.015f, 0.03f, 0.96f);
        static readonly Color Btn = new Color(0.16f, 0.12f, 0.2f, 1);
        static readonly int[] Denoms = { 25, 50, 100, 250, 500, 1000, 2500, 5000 };

        public TableUi(Action<Command> send)
        {
            this.send = send;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            canvas = Ui.ScreenCanvas("Table UI", 20);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var t = canvas.transform;

            // the bar across the bottom
            bar = Ui.Panel("bar", t, Panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 252)).rectTransform;
            ((Image)bar.GetComponent<Image>()).raycastTarget = true;
            Ui.Panel("bar rule", bar, new Color(1, 1, 1, 0.08f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), Vector2.zero);
            oddsText = Ui.Text("odds", bar, "", 21, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(36, 18), new Vector2(520, -18));
            betLabel = Ui.Text("bet label", bar, "YOUR BET", 18, new Color(0.7f, 0.65f, 0.75f), TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-400, -50), new Vector2(0, -16), true);
            betText = Ui.Text("bet", bar, "", 58, Look.Brass, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-400, -118), new Vector2(-40, -42), true);
            clearBtn = Ui.Button("clear", bar, "CLEAR", Btn, () => SetBet(Min()), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-30, -98), new Vector2(80, -52), 18);
            maxBtn = Ui.Button("max", bar, "MAX", Btn, () => SetBet(Max()), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(90, -98), new Vector2(180, -52), 18);
            chipRow = Ui.Rect("chips", bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-400, 20), new Vector2(240, 120));
            actions = Ui.Rect("actions", bar, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-760, 20), new Vector2(-36, -64));
            standBtn = Ui.Button("stand", bar, "STAND UP  (X)", new Color(0.25f, 0.06f, 0.08f, 1), () => send(Command.Stand()), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -54), new Vector2(-36, -12), 18);
            statusText = Ui.Text("status", bar, "", 20, new Color(0.8f, 0.75f, 0.85f), TextAlignmentOptions.TopLeft, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-760, -54), new Vector2(-280, -14), true);
            collText = Ui.Text("collector", bar, "", 26, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(36, 18), new Vector2(-20, -18));

            // let it ride: double or nothing on the win that just landed (U18)
            ridePanel = Ui.Panel("ride", t, new Color(0.12f, 0.08f, 0.01f, 1f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-420, 560), new Vector2(420, 696)).rectTransform;
            ridePanel.GetComponent<Image>().sprite = Ui.Round;
            ridePanel.GetComponent<Image>().type = Image.Type.Sliced;
            rideText = Ui.Text("ride text", ridePanel, "", 24, Look.Brass, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(24, 50), new Vector2(-300, -14), true);
            rideBtn = Ui.Button("ride it", ridePanel, "LET IT RIDE", new Color(0.55f, 0.36f, 0.02f, 1), () => send(Command.Ride(Seat())), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-280, 52), new Vector2(-24, -16), 24);
            takeBtn = Ui.Button("take it", ridePanel, "TAKE IT", Btn, () => dismissedRide = rideKey, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-280, 12), new Vector2(-24, 46), 18);
            rideTimer = Ui.Panel("ride timer", ridePanel, Look.Brass, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 18), new Vector2(500, 26));

            // roulette's layout: 0, the 36 numbers in three rows of twelve, the outside bets
            layoutPanel = Ui.Panel("layout", t, new Color(0.0f, 0.12f, 0.06f, 0.93f), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-1010, 266), new Vector2(-30, 512)).rectTransform;
            layoutPanel.GetComponent<Image>().raycastTarget = true;
            BuildLayout();
            leverRow = Ui.Rect("levers", bar, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 20), new Vector2(-36, -64));
            SetVisible(false);
        }

        int rideKey;
        string seat;
        string Seat() => seat;

        // ---------------------------------------------------------------- bets

        BetLimits Lim(string table) => Vig.Sim.Data.Tables.LimitsOf(Vig.Sim.Data.Tables.ById(table), floor);
        int floor = 1;
        int Min() => Lim(seat).Min;
        int Max() => Lim(seat).Max;

        public int Bet(string table)
        {
            var l = Lim(table);
            if (!bets.TryGetValue(table, out int b)) b = l.Std;
            return Mathf.Clamp(b, l.Min, l.Max);
        }

        void SetBet(int v)
        {
            if (seat == null) return;
            var l = Lim(seat);
            bets[seat] = Mathf.Clamp(v / l.Step * l.Step, l.Min, l.Max);
        }

        /// <summary>The keyboard's [ ]: a step down or up.</summary>
        public void Nudge(string table, int dir)
        {
            seat = table;
            SetBet(Bet(table) + dir * Lim(table).Step);
        }

        // ---------------------------------------------------------------- building per table

        void SetVisible(bool v)
        {
            Visible = v;
            canvas.enabled = v;
        }

        void Clear()
        {
            foreach (var b in live) if (b) Ui.Kill(b.gameObject);
            live.Clear();
            named.Clear();
            for (int i = chipRow.childCount - 1; i >= 0; i--) Ui.Kill(chipRow.GetChild(i).gameObject);
            for (int i = leverRow.childCount - 1; i >= 0; i--) Ui.Kill(leverRow.GetChild(i).gameObject);
        }

        Button Action(string key, string label, Color c, Action click, int slot, int slots, float size = 28)
        {
            float w = 1f / slots;
            var b = Ui.Button(key, actions, label, c, click, new Vector2(slot * w, 0), new Vector2((slot + 1) * w, 1), new Vector2(6, 0), new Vector2(-6, 0), size);
            live.Add(b);
            named[key] = b;
            return b;
        }

        /// <summary>a side bet's button while it rides: a deep gold that keeps white text readable</summary>
        static readonly Color SideOn = new Color(0.36f, 0.26f, 0.04f);

        static Color Tint(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);

        void BuildTable(string table, Game g)
        {
            Clear();
            var def = Vig.Sim.Data.Tables.ById(table);
            var tc = Look.TierColor(def.Tier);
            // chips that fit this table's step and limit
            var l = Lim(table);
            var chips = Denoms.Where(d => d >= l.Step && d <= l.Max && d % l.Step == 0).Take(6).ToArray();
            Color[] chipColors = { new Color(0.9f, 0.88f, 0.8f), new Color(0.75f, 0.12f, 0.15f), new Color(0.15f, 0.3f, 0.75f), new Color(0.1f, 0.5f, 0.2f), new Color(0.12f, 0.12f, 0.14f), new Color(0.55f, 0.2f, 0.65f), new Color(0.85f, 0.6f, 0.1f), new Color(0.9f, 0.4f, 0.6f) };
            for (int i = 0; i < chips.Length; i++)
            {
                int v = chips[i];
                var rt = Ui.Rect("chip " + v, chipRow, new Vector2(0, 0), new Vector2(0, 1), new Vector2(i * 104, 0), new Vector2(i * 104 + 96, 0));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Ui.Chip;
                img.preserveAspect = true;
                img.color = chipColors[Array.IndexOf(Denoms, v) % chipColors.Length];
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => { SetBet(Bet(seat) + v); Sfx.Play("chip", null, 0.8f); });
                Ui.Text("v", rt, v >= 1000 ? (v / 1000) + "K" : v.ToString(), 26, v == 25 || v == 2500 ? Color.black : Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                live.Add(b);
            }
            switch (table)
            {
                case "hilo":
                    Action("hi", "HI  4-6", Tint(tc, 0.35f), () => send(Command.CallIt(table, "hi", Bet(table))), 0, 2, 34);
                    Action("lo", "LO  1-3", Tint(tc, 0.35f), () => send(Command.CallIt(table, "lo", Bet(table))), 1, 2, 34);
                    break;
                case "redline":
                    Action("red", "RED", new Color(0.55f, 0.04f, 0.08f), () => send(Command.CallIt(table, "red", Bet(table))), 0, 2, 36);
                    Action("black", "BLACK", new Color(0.08f, 0.08f, 0.1f), () => send(Command.CallIt(table, "black", Bet(table))), 1, 2, 36);
                    break;
                case "twentyone":
                    Action("deal", "DEAL", Tint(tc, 0.35f), () => send(new Command { T = "bet", Table = table, Amount = Bet(table), Call = SideCall(table) }), 0, 4, 32);
                    Action("pairs", "", Btn, () => sidePairs = !sidePairs, 1, 4, 20);
                    Action("hit", "HIT", Btn, () => send(Command.CallIt(table, "hit")), 0, 4);
                    Action("stand", "STAND", Btn, () => send(Command.CallIt(table, "stand")), 1, 4);
                    Action("double", "DOUBLE", Btn, () => send(Command.CallIt(table, "double")), 2, 4);
                    Action("split", "SPLIT", Btn, () => send(Command.CallIt(table, "split")), 3, 4);
                    break;
                case "bandit": Action("go", "PULL THE LEVER", Tint(tc, 0.35f), () => send(Command.Spin(table, Bet(table))), 0, 1, 36); break;
                case "wheel": Action("go", "SPIN THE WHEEL", Tint(tc, 0.35f), () => send(Command.Spin(table, Bet(table))), 0, 1, 36); break;
                case "deadman": Action("go", "THROW THE DICE", Tint(tc, 0.4f), () => send(Command.Spin(table, Bet(table))), 0, 1, 36); break;
                case "pegdrop": Action("go", "DROP THE BALL", Tint(tc, 0.35f), () => send(Command.Spin(table, Bet(table))), 0, 1, 36); break;
                case "craps":
                    Action("go", "THROW THE DICE", Tint(tc, 0.35f), () => send(new Command { T = "spin", Table = table, Amount = Bet(table), Call = SideCall(table) }), 0, 2, 32);
                    Action("side", "", Btn, () => crapsSide = (crapsSide + 1) % (SideBets.OneRoll.Length + 1), 1, 2, 20);
                    break;
                case "roulette": Action("go", "SPIN", Tint(tc, 0.35f), () => send(Command.CallIt(table, rouletteCall, Bet(table))), 0, 1, 36); break;
                case "crash":
                    Action("go", "LAUNCH", Tint(tc, 0.4f), () => send(new Command { T = "spin", Table = table, Amount = Bet(table), Call = crashAuto > 0 ? AutoSteps[crashAuto].ToString(System.Globalization.CultureInfo.InvariantCulture) : null }), 0, 3, 34);
                    Action("auto", "AUTO: OFF", Btn, () => crashAuto = (crashAuto + 1) % AutoSteps.Length, 1, 3, 22);
                    Action("cash", "CASH OUT", new Color(0.1f, 0.45f, 0.15f), () => send(Command.CallIt(table, "cash")), 2, 3, 30);
                    break;
            }
        }

        void BuildCollector(Game g)
        {
            Clear();
            var s = g.State;
            switch (s.Phase)
            {
                case Phase.Choice:
                    Action("pay", $"PAY {Ui.Money(s.Quota)}", new Color(0.4f, 0.3f, 0.05f), () => send(Command.Pay()), 0, 2, 28);
                    Action("gamble", "CHALLENGE HIM", new Color(0.35f, 0.08f, 0.45f), () => send(Command.Gamble()), 1, 2, 28);
                    break;
                case Phase.Circuit:
                {
                    int n = s.Circuit?.Levers.Count ?? 10;
                    for (int i = 0; i < n; i++)
                    {
                        int k = i;
                        float w = 1f / n;
                        var b = Ui.Button("lever " + i, leverRow, (i + 1).ToString(), Btn, () => send(traceMode ? Command.Inspect(k) : Command.Activate(k)),
                            new Vector2(k * w, 0.35f), new Vector2((k + 1) * w, 1), new Vector2(3, 0), new Vector2(-3, 0), 26);
                        live.Add(b);
                        named["lever" + i] = b;
                    }
                    var tr = Ui.Button("trace", leverRow, "TRACE A WIRE: OFF", Btn, () => traceMode = !traceMode, new Vector2(0, 0), new Vector2(0.5f, 0.3f), new Vector2(3, 0), new Vector2(-3, 0), 18);
                    live.Add(tr);
                    named["trace"] = tr;
                    break;
                }
                case Phase.Wheel:
                    Action("spin", "SPIN THE WHEEL", new Color(0.35f, 0.08f, 0.45f), () => send(Command.Fortune()), 0, 1, 32);
                    break;
                case Phase.Summary:
                    Action("ready", s.Night < Economy.Nights.I ? (State.FloorOfNight(s.Night + 1) != s.Floor ? "TAKE THE STAIRS DOWN" : "THE NEXT NIGHT") : "WALK OUT", new Color(0.4f, 0.3f, 0.05f), () => send(Command.Ready()), 0, 1, 30);
                    break;
                case Phase.Over:
                    Action("new", "A NEW RUN", new Color(0.4f, 0.3f, 0.05f), () => NewRun?.Invoke(), 0, 1, 30);
                    break;
            }
        }

        bool traceMode;
        /// <summary>The run is over and the player wants another (Play starts it).</summary>
        public Action NewRun;

        void BuildLayout()
        {
            var t = layoutPanel;
            Ui.Text("hint", t, "PLACE YOUR BET ON THE LAYOUT, THEN SPIN", 18, new Color(0.8f, 0.9f, 0.8f), TextAlignmentOptions.Top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -32), new Vector2(0, -6), true);
            void Cell(string call, string label, Color c, float x0, float y0, float x1, float y1)
            {
                var b = Ui.Button("cell " + call, t, label, c, () => rouletteCall = call, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x0, y0), new Vector2(x1, y1), 20);
                layoutCells[call] = (Image)b.targetGraphic;
            }
            const float cw = 70, ch = 50, ox = 84, oy = 78;
            Cell("n0", "0", new Color(0.05f, 0.45f, 0.2f), 20, oy, ox - 6, oy + ch * 3 - 6);
            for (int col = 0; col < 12; col++)
                for (int row = 0; row < 3; row++)
                {
                    int n = col * 3 + (3 - row);
                    Cell("n" + n, n.ToString(), Roulette.IsRed(n) ? new Color(0.6f, 0.05f, 0.08f) : new Color(0.06f, 0.06f, 0.07f), ox + col * cw, oy + row * ch, ox + col * cw + cw - 6, oy + row * ch + ch - 6);
                }
            string[] calls = { "d1", "d2", "d3", "low", "even", "red", "black", "odd", "high" };
            string[] labels = { "1ST 12", "2ND 12", "3RD 12", "1-18", "EVEN", "RED", "BLACK", "ODD", "19-36" };
            float w = (cw * 12) / calls.Length;
            for (int i = 0; i < calls.Length; i++)
            {
                Color c = calls[i] == "red" ? new Color(0.6f, 0.05f, 0.08f) : calls[i] == "black" ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.04f, 0.3f, 0.14f);
                Cell(calls[i], labels[i], c, ox + i * w, 20, ox + i * w + w - 6, 20 + ch - 6);
            }
        }

        // ---------------------------------------------------------------- every frame

        public void Refresh(Game g, PlayerState me)
        {
            var s = g.State;
            floor = s.Floor;
            string want = s.Phase == Phase.Floor && me.Seat != null ? "table:" + me.Seat
                : s.Phase == Phase.Choice || s.Phase == Phase.Circuit || s.Phase == Phase.Wheel || s.Phase == Phase.Summary || s.Phase == Phase.Over ? "coll:" + s.Phase + (s.Circuit?.Levers.Count ?? 0)
                : null;
            if (want == null) { if (Visible) SetVisible(false); mode = null; return; }
            if (!Visible) SetVisible(true);
            if (want != mode)
            {
                mode = want;
                seat = me.Seat;
                traceMode = false;
                if (want.StartsWith("table:")) BuildTable(me.Seat, g); else BuildCollector(g);
            }
            bool atTable = want.StartsWith("table:");
            foreach (var go in new Component[] { betLabel, betText, clearBtn, maxBtn, chipRow, standBtn, oddsText }) go.gameObject.SetActive(atTable);
            collText.gameObject.SetActive(!atTable);
            layoutPanel.gameObject.SetActive(atTable && seat == "roulette");
            if (atTable) RefreshTable(g, me); else RefreshCollector(g, me);
        }

        void RefreshTable(Game g, PlayerState me)
        {
            var s = g.State;
            var t = s.Tables[seat];
            var def = Vig.Sim.Data.Tables.ById(seat);
            var tc = Look.TierColor(def.Tier);
            oddsText.text = OddsCard(g, me, seat);
            int stake = Rules.StakeFor(me, Bet(seat));
            betText.text = Ui.Money(stake) + (me.Overclock > 0 ? Ui.Col("  x2", Look.Soul) : "");
            bool busy = t.Pending != null;
            bool canBet = s.Bank >= stake;
            string status = busy ? "THE ROUND IS IN THE AIR..." : canBet ? "" : Ui.Col("THE BANK CAN'T COVER THIS BET", Look.Blood);
            foreach (var b in live) if (b && b.transform.parent == chipRow) b.interactable = !busy;
            switch (seat)
            {
                case "twentyone":
                {
                    bool play = t.Hand != null && t.Hand.Stage == "play";
                    (bool dbl, bool split) o = play ? TwentyOne.Options(t.Hand) : (false, false);
                    named["deal"].gameObject.SetActive(!play);
                    named["deal"].interactable = !busy && canBet;
                    // the side bet: on or off, its price and pays printed on the button
                    var sbd = SideBets.PairsFor(me);
                    int sideAmt = sbd.Mins * Vig.Sim.Data.Tables.LimitsOf(Vig.Sim.Data.Tables.ById(seat), s.Floor).Min;
                    named["pairs"].gameObject.SetActive(!play);
                    named["pairs"].interactable = !busy;
                    named["pairs"].image.color = sidePairs ? SideOn : Btn;
                    Ui.Label(named["pairs"], (sidePairs ? "PAIRS: ON  +" : "PAIRS: OFF  ") + Ui.Money(sideAmt) + $"\n<size=15>{sbd.Mixed}:1 · {sbd.Colored}:1 · {sbd.Suited}:1</size>");
                    foreach (var k in new[] { "hit", "stand", "double", "split" }) named[k].gameObject.SetActive(play);
                    named["double"].interactable = o.dbl && s.Bank >= t.Hand?.Hands[t.Hand.Active].Bet;
                    named["split"].interactable = o.split && s.Bank >= t.Hand?.Hands[t.Hand.Active].Bet;
                    if (play) status = "YOUR MOVE";
                    break;
                }
                case "crash":
                {
                    bool up = t.Crash != null;
                    named["go"].interactable = !up && !busy && canBet;
                    named["auto"].interactable = !up;
                    Ui.Label(named["auto"], crashAuto == 0 ? "AUTO: OFF" : $"AUTO AT {AutoSteps[crashAuto]}x");
                    named["cash"].interactable = up;
                    double m = up ? Crash.MultAt(g.Tick - t.Crash.Start) : 1;
                    Ui.Label(named["cash"], up ? $"CASH OUT  {Crash.X(m)}x\n{Ui.Money((int)(t.Crash.Bet * m))}" : "CASH OUT");
                    status = up ? (m >= Crash.Deadly ? Ui.Col($"PAST {Crash.Deadly}x: A CRASH NOW COSTS A LIFE", Look.Blood) : "IT'S CLIMBING...") : status;
                    break;
                }
                case "craps":
                {
                    // the one-roll bet: which, its price and pays on the button, its odds on the card
                    var one = CrapsOneRoll;
                    int sideAmt = (one?.Mins ?? 1) * Vig.Sim.Data.Tables.LimitsOf(def, s.Floor).Min;
                    bool sideOk = one == null || s.Bank >= sideAmt + (t.Point == 0 ? stake : 0);
                    Ui.Label(named["go"], t.Point == 0 ? "THROW: COME OUT" : $"THROW FOR THE {t.Point}");
                    named["go"].interactable = !busy && (t.Point != 0 || canBet) && sideOk;
                    named["side"].interactable = !busy;
                    named["side"].image.color = one != null ? SideOn : Btn;
                    Ui.Label(named["side"], one == null ? "ONE ROLL: OFF\n<size=15>FIELD · ANY CRAPS · ELEVEN</size>" : $"ONE ROLL: {one.Name}  +{Ui.Money(sideAmt)}\n<size=15>{one.PaysText}</size>");
                    if (one != null && !Rules.HasTeamMark(g, "blackout"))
                        oddsText.text += $"\n{Ui.Col("ONE ROLL", Look.Gold)} {one.Name}  {Ui.Col("WIN", Look.Smoke)} {Ui.Col($"{SideBets.WinP(one) * 100:0}%", tc)}  {Ui.Col("PAYS", Look.Smoke)} {one.PaysShort}  {Ui.Col("RETURNS", Look.Smoke)} ${SideBets.ExactRtp(one):0.00}";
                    if (t.Point != 0) status = $"THE POINT IS {t.Point}: HIT IT AGAIN BEFORE A 7. YOUR {Ui.Money(t.PointBet)} RIDES.";
                    if (!sideOk && !busy) status = Ui.Col("THE BANK CAN'T COVER THE ONE-ROLL BET TOO", Look.Blood);
                    break;
                }
                case "roulette":
                    foreach (var kv in layoutCells)
                    {
                        bool sel = kv.Key == rouletteCall;
                        kv.Value.transform.localScale = Vector3.one * (sel ? 1.08f : 1f);
                        kv.Value.GetComponentInChildren<TextMeshProUGUI>().color = sel ? Look.Gold : Color.white;
                    }
                    Ui.Label(named["go"], $"SPIN: {Roulette.CallName(rouletteCall)}");
                    named["go"].interactable = !busy && canBet;
                    break;
                default:
                    foreach (var k in new[] { "go", "hi", "lo", "red", "black" }) if (named.TryGetValue(k, out var b)) b.interactable = !busy && canBet;
                    break;
            }
            statusText.text = status;
            // let it ride
            var r = t.Ride;
            rideKey = r == null ? -1 : r.Until * 31 + r.Count;
            bool offer = r != null && r.Player == me.Id && g.Tick <= r.Until && rideKey != dismissedRide;
            ridePanel.gameObject.SetActive(offer);
            if (offer)
            {
                rideText.text = $"LET IT RIDE?  {Ui.Money(r.Amount)}  →  {Ui.Col(Ui.Money(r.Amount * 2), Look.LowHi)}  OR  {Ui.Col("$0", Look.Blood)}\n" +
                    Ui.Col($"DOUBLE OR NOTHING ON YOUR WINNINGS. 50%. {(r.Count > 0 ? $"RIDE {r.Count + 1} OF {Economy.RideMax.I}." : "")}", new Color(0.85f, 0.75f, 0.55f));
                rideBtn.interactable = s.Bank >= r.Amount;
                float left = Mathf.Clamp01((r.Until - g.Tick) / (float)Rules.Ticks(Economy.RideS.V));
                rideTimer.rectTransform.offsetMax = new Vector2(24 + 476 * left, 26);
            }
        }

        void RefreshCollector(Game g, PlayerState me)
        {
            var s = g.State;
            var r = Circuit.For(s.Floor);
            switch (s.Phase)
            {
                case Phase.Choice:
                {
                    int live = s.Mods.Overloads3 ? r.Live + 1 : r.Live;
                    double win = s.Mods.Overloads3 ? r.OddsBane : r.OddsWin;
                    collText.text = $"YOU OWE {Ui.Col(Ui.Money(s.Quota), Look.Brass)}. THE BANK HOLDS {Ui.Money(s.Bank)}.\n" +
                        $"{Ui.Col("CHALLENGE", Look.Coll)}: {r.Levers} LEVERS, {live} LIVE. YOU WIN {Mathf.RoundToInt((float)win * 100)}%.\n" +
                        Ui.Col("WIN: HE DIES, YOU KEEP THE QUOTA, SPIN THE BOON. LOSE: A LIFE, THE QUOTA, THE BANE.", new Color(0.75f, 0.7f, 0.8f));
                    break;
                }
                case Phase.Circuit:
                {
                    var c = s.Circuit;
                    collText.text = $"YOUR SHOCKS {Ui.Col(c.Shocks[me.Id] + " / " + r.PlayerShocks, Look.Blood)}    HIS {Ui.Col(c.HisShocks + " / " + r.CollShocks, Look.Coll)}\n" +
                        (c.Done != null ? c.Done.Why : c.Turn == "team" ? (traceMode ? "PICK A WIRE TO TRACE." : "YOUR PULL. PICK A LEVER.") : "HIS PULL...") + "\n" + Ui.Col(c.Log.FirstOrDefault() ?? "", new Color(0.75f, 0.7f, 0.8f));
                    bool mine = c.Done == null && c.Turn == "team";
                    for (int i = 0; i < c.Levers.Count; i++)
                    {
                        if (!named.TryGetValue("lever" + i, out var b)) continue;
                        var lv = c.Levers[i];
                        b.interactable = mine && !lv.Pulled && !(traceMode && lv.Known);
                        ((Image)b.targetGraphic).color = lv.Pulled ? (lv.Live ? new Color(0.5f, 0.03f, 0.1f) : new Color(0.08f, 0.08f, 0.08f)) : lv.Known ? (lv.Live ? new Color(0.7f, 0.1f, 0.15f) : new Color(0.1f, 0.35f, 0.15f)) : Btn;
                        Ui.Label(b, (i + 1) + (lv.Pulled ? (lv.Live ? "\n!" : "\n-") : lv.Known ? (lv.Live ? "\nLIVE" : "\nDEAD") : ""));
                    }
                    if (named.TryGetValue("trace", out var tr))
                    {
                        int traces = c.Traces[me.Id];
                        tr.gameObject.SetActive(traces > 0);
                        Ui.Label(tr, traceMode ? $"TRACING ({traces} LEFT): PICK A LEVER" : $"TRACE A WIRE ({traces} LEFT)");
                    }
                    break;
                }
                case Phase.Wheel:
                    collText.text = (s.Wheel.Kind == "boon" ? Ui.Col("HE IS DEAD. THE BOON WHEEL.", Look.Soul) : Ui.Col("THE BANE WHEEL. IT LASTS TOMORROW.", Look.Blood)) + "\n" +
                        string.Join("  ·  ", (s.Wheel.Kind == "boon" ? Wheels.Boons : Wheels.Banes).Select(x => x.Label));
                    if (named.TryGetValue("spin", out var sp)) sp.interactable = s.Wheel.Slot == null;
                    break;
                case Phase.Summary:
                {
                    var m = s.Summary;
                    collText.text = $"NIGHT {m.Night}: {m.How.ToUpperInvariant()}" + (m.Wheel != null ? $"  ·  {m.Wheel.Kind.ToUpperInvariant()}: {m.Wheel.Id.ToUpperInvariant()}" : "") + "\n" +
                        (m.Night < Economy.Nights.I ? $"TOMORROW'S QUOTA {Ui.Col(Ui.Money(m.NextQuota), Look.Brass)}" + (State.FloorOfNight(m.Night + 1) != s.Floor ? Ui.Col("  ·  THE STAIRS GO DOWN TO FLOOR " + State.FloorOfNight(m.Night + 1), Look.Gold) : "") : "THE LAST NIGHT IS DONE.");
                    break;
                }
                case Phase.Over:
                    collText.text = s.Result != null ? Ui.Col(s.Result.Won ? "YOU WALK OUT." : "THE HOUSE KEEPS YOU.", s.Result.Won ? Look.Soul : Look.Blood) + "\n" + s.Result.Why : "";
                    break;
            }
        }

        /// <summary>The odds card: always printed (CLAUDE.md), the Blackout mark the one thing that hides it.</summary>
        public static string OddsCard(Game g, PlayerState me, string table)
        {
            var def = Vig.Sim.Data.Tables.ById(table);
            var tc = Look.TierColor(def.Tier);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=30><b>{def.Name.ToUpperInvariant()}</b></size>   {Ui.Col("<b>" + def.Tier.ToUpperInvariant() + "</b>", tc)}\n");
            if (Rules.HasTeamMark(g, "blackout")) sb.Append(Ui.Col("BLACKOUT: THE ODDS ARE HIDDEN\n", Look.Blood));
            else
            {
                var o = TableModule.ById(table).Odds();
                string win = table == "crash" ? $"{Mathf.RoundToInt((float)o.WinP * 100)}% AT 3x" : $"{(o.WinP * 100):0}%";
                sb.Append($"{Ui.Col("WIN", Look.Smoke)} {Ui.Col(win, tc)}   {Ui.Col("PAYS", Look.Smoke)} {def.PaysText}\n");
                sb.Append($"{Ui.Col("RETURNS", Look.Smoke)} ${o.Rtp:0.00} PER $1{(o.Rtp < 1 ? Ui.Col("  (A SLOW LOSS)", Look.Smoke) : "")}\n");
            }
            sb.Append($"{Ui.Col("LOSING COSTS", Look.Smoke)} {Ui.Col(def.LossText, def.Tier == "high" ? Look.High : def.Tier == "mid" ? Look.MidHi : Look.Bone)}\n");
            double f = Rules.FactorOf(g.State, me, table);
            var extra = new List<string>();
            if (Math.Abs(f - 1) > 1e-9) extra.Add(Ui.Col($"PAYOUTS x{f:0.00}", f < 1 ? Look.Blood : Look.LowHi));
            if (table == "redline" && me.RedStreak > 0) extra.Add(Ui.Col($"STREAK {me.RedStreak}/{RedLine.StreakLimit(g)}", Look.High));
            if (me.HotTable == table && me.HotRun > 0 && me.Charms.Contains("hothand")) extra.Add(Ui.Col($"HOT x{me.HotRun}", Look.Gold));
            if (table == "bandit") extra.Add(Ui.Col($"PROGRESSIVE {Ui.Money(g.State.Pot)}", Look.Gold));
            if (extra.Count > 0) sb.Append(string.Join("   ", extra));
            return sb.ToString();
        }
    }
}
