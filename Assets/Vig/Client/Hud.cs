// The in-play HUD: the clock, the bank against the quota, markers, lives, marks; the prompt for what's
// in reach; toasts for what just happened; banners for the night's beats.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public sealed class Hud
    {
        readonly TextMeshProUGUI clock, money, status, prompt, toasts, banner, help;
        readonly List<(string text, float until)> lines = new List<(string, float)>();
        float bannerUntil;
        public readonly Canvas Canvas;

        public Hud()
        {
            Canvas = Ui.ScreenCanvas("HUD", 10);
            var c = Canvas.transform;
            var tl = new Vector2(0, 1);
            Ui.Panel("top shade", c, new Color(0, 0, 0, 0.35f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), Vector2.zero);
            clock = Ui.Text("clock", c, "", 52, Color.white, TextAlignmentOptions.TopLeft, tl, tl, new Vector2(36, -88), new Vector2(260, -14), true);
            money = Ui.Text("money", c, "", 30, Look.Brass, TextAlignmentOptions.TopLeft, tl, tl, new Vector2(250, -80), new Vector2(1100, -20), true);
            status = Ui.Text("status", c, "", 26, Color.white, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-900, -84), new Vector2(-36, -20), true);
            prompt = Ui.Text("prompt", c, "", 30, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-700, 150), new Vector2(700, 230), true);
            toasts = Ui.Text("toasts", c, "", 26, Color.white, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 36), new Vector2(1100, 420));
            banner = Ui.Text("banner", c, "", 72, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-900, 120), new Vector2(900, 300), true);
            help = Ui.Text("help", c, "WASD WALK   SHIFT RUN   E USE   MOUSE LOOK   ESC CURSOR", 18, new Color(1, 1, 1, 0.4f), TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-1000, 24), new Vector2(-36, 60));
            Ui.Text("dot", c, "·", 40, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20, -20), new Vector2(20, 20));
        }

        public void Toast(string text, float secs = 4f) => lines.Add((text, Time.time + secs));

        public void Banner(string text, Color c, float secs = 2.5f)
        {
            banner.text = Ui.Col(text, c);
            bannerUntil = Time.time + secs;
        }

        public void Prompt(string text) => prompt.text = text ?? "";

        public void Refresh(Game g, int me)
        {
            var s = g.State;
            var p = s.Players[me];
            int secs = s.Clock / State.TicksPerS;
            clock.text = s.Phase == Phase.Floor ? $"{secs / 60}:{secs % 60:00}" : s.Phase == Phase.Midnight || s.Phase == Phase.Choice || s.Phase == Phase.Circuit || s.Phase == Phase.Wheel ? Ui.Col("0:00", Look.Coll) : "";
            Color bc = s.Bank >= s.Quota ? Look.LowHi : Look.Brass;
            money.text = $"{Ui.Col(Ui.Money(s.Bank), bc)} {Ui.Col("/ " + Ui.Money(s.Quota), Look.Smoke)}    {Ui.Col("NIGHT " + s.Night, Look.Bone)}";
            string hearts = string.Concat(Enumerable.Range(0, Mathf.Max(p.Lives, 0)).Select(_ => "♥"));
            var marks = s.Marks.Where(m => m.Player == -1 || m.Player == me).Select(m => Ui.Col(Marks.ById(m.Id).Name.ToUpperInvariant() + (m.Stacks > 1 ? " x" + m.Stacks : "") + $" {Mathf.CeilToInt(m.Left / 60f)}s", Look.Blood));
            status.text = $"{string.Join("  ", marks)}    {Ui.Col(s.Markers + " MARKERS", Look.Marker)}    {(p.Ghost ? Ui.Col("GHOST", Look.Soul) : Ui.Col(hearts, Look.Blood))}";
            float now = Time.time;
            lines.RemoveAll(l => l.until < now);
            while (lines.Count > 6) lines.RemoveAt(0);
            toasts.text = string.Join("\n", lines.Select(l => l.text));
            if (now > bannerUntil) banner.text = "";
            help.enabled = s.Phase == Phase.Floor && p.Seat == null;
        }
    }
}
