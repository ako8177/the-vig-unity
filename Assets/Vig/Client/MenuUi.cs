// The title screen (the run's options: the quota curve, the floor to start on, whether to start at the
// house) and the computer's catalogue at the house (tonight's charms, the reroll). Both are buttons that
// send commands or start a run; neither touches the sim's state.
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vig.Sim;
using Vig.Sim.Data;
using Vig.Sim.Systems;

namespace Vig.Client
{
    /// <summary>The run's options, kept between sessions.</summary>
    public static class Options
    {
        public static string Curve { get => PlayerPrefs.GetString("vig.curve", "easy"); set => PlayerPrefs.SetString("vig.curve", value); }
        public static int StartFloor { get => PlayerPrefs.GetInt("vig.floor", 1); set => PlayerPrefs.SetInt("vig.floor", value); }
        public static bool StartHome { get => PlayerPrefs.GetInt("vig.home", 1) == 1; set => PlayerPrefs.SetInt("vig.home", value ? 1 : 0); }
        /// <summary>charms unlocked by killing Collectors, for every run after (TS DECISIONS #81)</summary>
        public static string[] Unlocked
        {
            get => PlayerPrefs.GetString("vig.unlocked", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            set => PlayerPrefs.SetString("vig.unlocked", string.Join(",", value));
        }
    }

    public sealed class TitleUi
    {
        readonly Canvas canvas;
        readonly Button curveBtn, floorBtn, homeBtn;
        public bool Open => canvas.enabled;

        public TitleUi(Action play)
        {
            canvas = Ui.ScreenCanvas("Title", 40);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var t = canvas.transform;
            Ui.Panel("shade", t, new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).raycastTarget = true;
            var title = Ui.Text("title", t, "THE VIG", 190, Look.PinkHi * 1.2f, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-800, -330), new Vector2(800, -90), true);
            title.characterSpacing = 18;
            Ui.Text("sub", t, "THE HOUSE WANTS MORE EVERY NIGHT. GAMBLE A SHARED BANK UP TO ITS QUOTA, THEN PAY THE COLLECTOR, OR PLAY HIM FOR IT.", 26, new Color(0.85f, 0.8f, 0.9f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-760, -420), new Vector2(760, -330));
            float y = -470;
            curveBtn = Ui.Button("curve", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Options.Curve = Options.Curve == "easy" ? "house" : "easy"; Refresh(); }, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, y - 70), new Vector2(380, y), 26);
            floorBtn = Ui.Button("floor", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Options.StartFloor = Options.StartFloor == 1 ? 2 : 1; Refresh(); }, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, y - 160), new Vector2(380, y - 90), 26);
            homeBtn = Ui.Button("home", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Options.StartHome = !Options.StartHome; Refresh(); }, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, y - 250), new Vector2(380, y - 180), 26);
            Ui.Button("play", t, "PLAY", new Color(0.55f, 0.1f, 0.35f), () => play(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, y - 380), new Vector2(380, y - 280), 46);
            Ui.Button("quit", t, "QUIT", new Color(0.12f, 0.1f, 0.12f), Application.Quit, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-120, y - 460), new Vector2(120, y - 400), 22);
            Ui.Text("how", t, "WASD WALK · SHIFT RUN · MOUSE LOOK · E USE · CLICK THE TABLE'S BUTTONS TO PLAY · ESC FREES THE MOUSE\n" +
                "LOW TABLES BLEED YOU SLOWLY. MID TABLES PAY BETTER AND MARK YOU. HIGH TABLES PAY BEST AND CAN KILL YOU. EVERY TABLE PRINTS ITS ODDS.",
                18, new Color(0.65f, 0.6f, 0.7f), TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 30), new Vector2(-60, 110));
            Refresh();
        }

        void Refresh()
        {
            Ui.Label(curveBtn, Options.Curve == "easy" ? "QUOTAS: EASY  (+$200 A NIGHT)" : "QUOTAS: HOUSE RULES  (x1.5 A NIGHT)");
            Ui.Label(floorBtn, Options.StartFloor == 1 ? "START ON FLOOR 1" : "START ON FLOOR 2  (NIGHT 4: ROULETTE, CRAPS, PEG DROP, CRASH)");
            Ui.Label(homeBtn, Options.StartHome ? "START AT THE HOUSE: ON" : "START AT THE HOUSE: OFF  (STRAIGHT TO THE CASINO)");
        }

        public void Show(bool on) => canvas.enabled = on;
    }

    /// <summary>The computer at the house: tonight's catalogue (DESIGN.md §5, U17).</summary>
    public sealed class ShopUi
    {
        readonly Canvas canvas;
        readonly Action<Command> send;
        readonly TextMeshProUGUI head, foot;
        readonly RectTransform cards;
        readonly Button reroll;
        string built;
        public bool Open => canvas.enabled;

        public ShopUi(Action<Command> send)
        {
            this.send = send;
            canvas = Ui.ScreenCanvas("Catalogue", 30);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var t = canvas.transform;
            Ui.Panel("shade", t, new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).raycastTarget = true;
            var panel = Ui.Panel("panel", t, new Color(0.01f, 0.06f, 0.03f, 0.97f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-860, -400), new Vector2(860, 400)).rectTransform;
            panel.GetComponent<Image>().sprite = Ui.Round;
            panel.GetComponent<Image>().type = Image.Type.Sliced;
            head = Ui.Text("head", panel, "", 34, new Color(0.5f, 1f, 0.65f), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -90), new Vector2(-40, -24), true);
            cards = Ui.Rect("cards", panel, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 170), new Vector2(-40, -110));
            foot = Ui.Text("foot", panel, "", 22, new Color(0.7f, 0.85f, 0.75f), TextAlignmentOptions.TopLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 80), new Vector2(-560, 160));
            reroll = Ui.Button("reroll", panel, "NEW CATALOGUE  (1 MARKER)", new Color(0.08f, 0.25f, 0.14f), () => send(Command.Reroll()), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-520, 96), new Vector2(-40, 156), 22);
            Ui.Button("close", panel, "CLOSE  (ESC)", new Color(0.15f, 0.12f, 0.12f), () => Show(false), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-520, 28), new Vector2(-40, 84), 22);
            Show(false);
        }

        public void Show(bool on)
        {
            canvas.enabled = on;
            built = null;
        }

        public void Refresh(Game g, PlayerState me)
        {
            if (!Open) return;
            var s = g.State;
            head.text = $"THE CATALOGUE  ·  TONIGHT'S CHARMS  ·  {Ui.Col(s.Markers + " MARKERS", Look.Marker)}  ·  {CageSystem.SlotsOf(g) - me.Charms.Count - HomeSystem.PendingCharms(g, me.Id).Count} FREE SLOTS";
            string key = string.Join(",", s.Shop) + "|" + s.Markers + "|" + me.Charms.Count + "|" + s.Post.Orders.Count + s.Post.Box.Count;
            if (key != built)
            {
                built = key;
                for (int k = cards.childCount - 1; k >= 0; k--) Ui.Kill(cards.GetChild(k).gameObject);
                for (int i = 0; i < s.Shop.Count; i++)
                {
                    var def = Charms.ById(s.Shop[i]);
                    float w = 1f / Math.Max(1, s.Shop.Count);
                    var card = Ui.Panel("card " + def.Id, cards, new Color(0.03f, 0.12f, 0.07f, 1), new Vector2(i * w, 0), new Vector2((i + 1) * w, 1), new Vector2(10, 0), new Vector2(-10, 0)).rectTransform;
                    card.GetComponent<Image>().sprite = Ui.Round;
                    card.GetComponent<Image>().type = Image.Type.Sliced;
                    Ui.Text("name", card, def.Name.ToUpperInvariant(), 30, Color.white, TextAlignmentOptions.Top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -70), new Vector2(-16, -18), true);
                    Ui.Text("price", card, $"{def.Price} MARKERS", 22, Look.Marker, TextAlignmentOptions.Top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -104), new Vector2(-16, -72), true);
                    Ui.Text("text", card, string.Join(" ", def.Text), 22, new Color(0.85f, 0.95f, 0.88f), TextAlignmentOptions.Top, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 90), new Vector2(-18, -120));
                    // its picture (U33), drawn pixel by pixel in pixel-art/charm-icons, shown crisp at 3x
                    var icon = Resources.Load<Texture2D>("VigIcons/" + def.Id);
                    if (icon)
                    {
                        var go = new GameObject("icon", typeof(RectTransform), typeof(RawImage));
                        var rt = (RectTransform)go.transform;
                        rt.SetParent(card, false);
                        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                        rt.offsetMin = new Vector2(-48, 206);
                        rt.offsetMax = new Vector2(48, 302);
                        var ri = go.GetComponent<RawImage>();
                        ri.texture = icon;
                        ri.raycastTarget = false;
                    }
                    // what it changes, in numbers (U31): the card's middle was empty
                    if (def.Change != null)
                    {
                        Ui.Panel("change rule", card, new Color(1, 1, 1, 0.08f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 196), new Vector2(-24, 198));
                        Ui.Text("change", card, def.Change, 21, Look.Gold, TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 110), new Vector2(-18, 190), true);
                    }
                    Ui.Text("systems", card, "TOUCHES: " + string.Join(", ", def.Systems).ToUpperInvariant(), 15, new Color(0.5f, 0.7f, 0.55f), TextAlignmentOptions.Bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 76), new Vector2(-16, 100));
                    string why = CageSystem.CantBuy(g, me, def.Id);
                    var id = def.Id;
                    var b = Ui.Button("buy", card, why == null ? "BUY" : why.ToUpperInvariant(), why == null ? new Color(0.1f, 0.45f, 0.2f) : new Color(0.12f, 0.12f, 0.12f), () => send(Command.Buy(id)), new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 16), new Vector2(-16, 68), why == null ? 26 : 16);
                    b.interactable = why == null;
                }
            }
            var coming = s.Post.Orders.Concat(s.Post.Box).Where(o => o.Player == me.Id).Select(o => Charms.ById(o.Item)?.Name ?? o.Item).ToList();
            foot.text = $"WEARING: {(me.Charms.Count == 0 ? "NOTHING" : string.Join(", ", me.Charms.Select(c => Charms.ById(c).Name.ToUpperInvariant())))}\n" +
                $"IN THE POST: {(coming.Count == 0 ? "NOTHING" : string.Join(", ", coming).ToUpperInvariant())}" + (s.Post.Box.Count > 0 ? Ui.Col("  ·  IT'S IN THE MAILBOX", Look.Gold) : s.Post.Orders.Count > 0 ? "  ·  ON ITS WAY" : "");
            reroll.interactable = s.Markers >= Economy.RerollCost.I;
        }
    }
}
