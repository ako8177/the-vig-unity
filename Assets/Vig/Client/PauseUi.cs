// The pause menu (Esc: the night stops), its settings (mouse sensitivity, volume, music: kept between
// sessions), and the results screen at the end of a run (how far you got, what it cost, your best).
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public static class Settings
    {
        public static float Sensitivity { get => PlayerPrefs.GetFloat("vig.sens", 1f); set => PlayerPrefs.SetFloat("vig.sens", value); }
        public static float Master { get => PlayerPrefs.GetFloat("vig.vol", 0.8f); set { PlayerPrefs.SetFloat("vig.vol", value); Sfx.Volume = value; Sfx.Mood(false); } }
        public static float Music { get => PlayerPrefs.GetFloat("vig.music", 0.35f); set { PlayerPrefs.SetFloat("vig.music", value); Sfx.MusicVolume = value; Sfx.Mood(false); } }
        public static int BestNight { get => PlayerPrefs.GetInt("vig.best", 0); set => PlayerPrefs.SetInt("vig.best", value); }

        public static void Apply()
        {
            Sfx.Volume = Master;
            Sfx.MusicVolume = Music;
        }
    }

    public sealed class PauseUi
    {
        readonly Canvas canvas;
        readonly Button sens, vol, mus;
        public bool Open => canvas.enabled;

        public PauseUi(Action resume, Action toTitle)
        {
            canvas = Ui.ScreenCanvas("Pause", 35);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var t = canvas.transform;
            Ui.Panel("shade", t, new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).raycastTarget = true;
            Ui.Text("title", t, "PAUSED", 96, Look.PinkHi, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-600, -280), new Vector2(600, -120), true);
            Vector2 a = new Vector2(0.5f, 1);
            float y = -320;
            Ui.Button("resume", t, "RESUME  (ESC)", new Color(0.4f, 0.1f, 0.3f), () => resume(), a, a, new Vector2(-340, y - 80), new Vector2(340, y), 30);
            sens = Ui.Button("sens", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Settings.Sensitivity = Next(Settings.Sensitivity, new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f }); Refresh(); }, a, a, new Vector2(-340, y - 180), new Vector2(340, y - 110), 24);
            vol = Ui.Button("vol", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Settings.Master = Next(Settings.Master, new[] { 0f, 0.25f, 0.5f, 0.8f, 1f }); Refresh(); }, a, a, new Vector2(-340, y - 270), new Vector2(340, y - 200), 24);
            mus = Ui.Button("mus", t, "", new Color(0.16f, 0.12f, 0.2f), () => { Settings.Music = Next(Settings.Music, new[] { 0f, 0.15f, 0.35f, 0.6f }); Refresh(); }, a, a, new Vector2(-340, y - 360), new Vector2(340, y - 290), 24);
            Ui.Button("title", t, "QUIT TO THE TITLE", new Color(0.25f, 0.06f, 0.08f), () => toTitle(), a, a, new Vector2(-340, y - 470), new Vector2(340, y - 400), 24);
            Show(false);
            Refresh();
        }

        static float Next(float v, float[] steps)
        {
            for (int i = 0; i < steps.Length; i++) if (steps[i] > v + 0.001f) return steps[i];
            return steps[0];
        }

        void Refresh()
        {
            Ui.Label(sens, $"MOUSE SENSITIVITY: {Settings.Sensitivity:0.##}x");
            Ui.Label(vol, $"VOLUME: {Mathf.RoundToInt(Settings.Master * 100)}%");
            Ui.Label(mus, $"MUSIC: {Mathf.RoundToInt(Settings.Music / 0.35f * 100)}%");
        }

        public void Show(bool on) => canvas.enabled = on;
    }

    /// <summary>The end of a run: the ending, the night you reached, what the run cost and won, your best.</summary>
    public sealed class ResultsUi
    {
        readonly Canvas canvas;
        readonly TextMeshProUGUI head, body;
        public bool Open => canvas.enabled;

        public ResultsUi(Action again, Action toTitle)
        {
            canvas = Ui.ScreenCanvas("Results", 38);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var t = canvas.transform;
            Ui.Panel("shade", t, new Color(0.02f, 0.01f, 0.03f, 0.93f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).raycastTarget = true;
            head = Ui.Text("head", t, "", 84, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-900, -300), new Vector2(900, -90), true);
            body = Ui.Text("body", t, "", 30, new Color(0.88f, 0.85f, 0.92f), TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-700, -800), new Vector2(700, -310));
            body.lineSpacing = 22;
            Vector2 b = new Vector2(0.5f, 0);
            Ui.Button("again", t, "ANOTHER RUN", new Color(0.5f, 0.1f, 0.32f), () => again(), b, b, new Vector2(-420, 90), new Vector2(-20, 180), 32);
            Ui.Button("title", t, "THE TITLE", new Color(0.16f, 0.12f, 0.2f), () => toTitle(), b, b, new Vector2(20, 90), new Vector2(420, 180), 28);
            Show(false);
        }

        public void Show(Game g)
        {
            var s = g.State;
            var st = s.Stats;
            bool won = s.Result?.Won == true;
            bool best = s.Night > Settings.BestNight;
            if (best) Settings.BestNight = s.Night;
            head.text = Ui.Col(won ? "YOU WALK OUT" : "THE HOUSE KEEPS YOU", won ? Look.Soul : Look.Blood);
            double winRate = st.Rounds > 0 ? st.Wins / (double)st.Rounds : 0;
            body.text = $"{s.Result?.Why}\n\n" +
                $"NIGHT {s.Night} OF {Economy.Nights.I}  ·  FLOOR {s.Floor}" + (best ? Ui.Col("  ·  YOUR BEST", Look.Gold) : $"  ·  BEST: NIGHT {Settings.BestNight}") + "\n\n" +
                $"ROUNDS PLAYED {st.Rounds}   ·   WON {st.Wins} ({Mathf.RoundToInt((float)winRate * 100)}%)\n" +
                $"{Ui.Col("WON " + Ui.Money(st.WonDollars), Look.LowHi)}   ·   {Ui.Col("LOST " + Ui.Money(st.LostDollars), Look.Blood)}   ·   BIGGEST WIN {Ui.Money(st.Biggest)}\n" +
                $"LIVES LOST {st.LivesLost}   ·   MARKS TAKEN {st.Marks}   ·   MARKERS EARNED {st.MarkersEarned}\n" +
                $"COLLECTORS KILLED {s.Killed.Count}";
            canvas.enabled = true;
        }

        public void Show(bool on) => canvas.enabled = on;
    }
}
