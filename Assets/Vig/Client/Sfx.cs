// Every sound, synthesized at load (U21): no audio files. The tables' own noises (dice clicking on the felt
// where the physics says they hit, cards flicked off the shoe, chips, reels, the wheel's flapper, the
// roulette ball), the wins that grow with their size (a chime, a fanfare, a cascade of coins and the room
// cheering), the losses (a soft thud), the night's beats (the 0:00 bell, the shock, the rocket and its
// crash), and under it all the room: a murmur of voices and a slow lounge loop.
using System.Collections.Generic;
using UnityEngine;
using Vig.Sim.Data;

namespace Vig.Client
{
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static Transform root;
        static AudioSource[] pool;
        static int next;
        static AudioSource bed, music;
        public static float Volume = 0.8f, MusicVolume = 0.35f;
        static System.Random rnd = new System.Random(7);

        static bool Live => Application.isPlaying && root != null;

        public static void Init(Transform parent)
        {
            if (!Application.isPlaying) return;
            root = new GameObject("Sfx").transform;
            root.SetParent(parent, false);
            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = root.gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                pool[i] = s;
            }
            bed = Loop("murmur", Murmur(), 0.22f);
            music = Loop("lounge", Lounge(), MusicVolume);
        }

        static AudioSource Loop(string name, AudioClip c, float vol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = vol * Volume;
            s.spatialBlend = 0;
            s.Play();
            return s;
        }

        /// <summary>Play a clip at `at` (null: in your head), `delay` seconds from now.</summary>
        public static void Play(string name, Vector3? at = null, float vol = 1, float pitch = 1, double delay = 0)
        {
            if (!Live) return;
            var c = Clip(name);
            if (c == null) return;
            var s = pool[next++ % pool.Length];
            s.Stop();
            s.clip = c;
            s.volume = vol * Volume;
            s.pitch = pitch * (0.97f + (float)rnd.NextDouble() * 0.06f);
            if (at.HasValue)
            {
                s.transform.position = at.Value;
                s.spatialBlend = 0.75f;
                s.minDistance = 1.5f;
                s.maxDistance = 18f;
                s.rolloffMode = AudioRolloffMode.Linear;
            }
            else s.spatialBlend = 0;
            if (delay > 0) s.PlayScheduled(AudioSettings.dspTime + delay); else s.Play();
        }

        /// <summary>The dice's clicks where the physics had them hit, at the speed the show plays the throw.</summary>
        public static void Dice(DiceLab.Throw t, Placement pl, double holdS)
        {
            if (!Live || t == null) return;
            var at = RoomBuilder.U(pl.X, pl.H, pl.Z);
            float scale = (float)holdS * 0.97f / Mathf.Max(0.1f, t.Seconds);
            float last = -1;
            foreach (var h in t.Hits)
            {
                if (h - last < 0.04f) continue;
                last = h;
                Play("die", at, 0.7f, 1, h * scale);
            }
        }

        /// <summary>The music and the murmur duck at 0:00 and come back with the night.</summary>
        public static void Mood(bool midnight)
        {
            if (!Live) return;
            if (music) music.volume = (midnight ? 0.08f : MusicVolume) * Volume;
            if (bed) bed.volume = (midnight ? 0.05f : 0.22f) * Volume;
        }

        // ---------------------------------------------------------------- the clips

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            switch (name)
            {
                case "click": c = Make(name, 0.05f, (t, i) => Noise() * Env(t, 0.002f, 0.03f) * 0.5f); break;
                case "chip": c = Make(name, 0.12f, (t, i) => (Noise() * 0.5f + Tone(t, 3100) * 0.5f) * (Env(t, 0.001f, 0.025f) + Env(t - 0.055f, 0.001f, 0.02f) * 0.7f)); break;
                case "die": c = Make(name, 0.08f, (t, i) => (Noise() * 0.6f + Tone(t, 1900) * 0.4f) * Env(t, 0.0005f, 0.02f)); break;
                case "card": c = Make(name, 0.12f, (t, i) => Noise() * Env(t, 0.01f, 0.06f) * 0.45f * (1 + Mathf.Sin(t * 180))); break;
                case "reel": c = Make(name, 0.06f, (t, i) => (Tone(t, 900) * 0.5f + Noise() * 0.3f) * Env(t, 0.001f, 0.02f)); break;
                case "thunk": c = Make(name, 0.2f, (t, i) => (Tone(t, 110 - t * 200) + Noise() * 0.2f) * Env(t, 0.002f, 0.08f)); break;
                case "flap": c = Make(name, 0.05f, (t, i) => (Tone(t, 2400) * 0.4f + Noise() * 0.4f) * Env(t, 0.0005f, 0.012f)); break;
                case "ball": c = Make(name, 1.2f, (t, i) => Noise() * 0.18f * Mathf.Abs(Mathf.Sin(t * 70 * (1.4f - t))) * (1 - t / 1.2f)); break;
                case "lose": c = Make(name, 0.5f, (t, i) => (Tone(t, 70) * 0.8f + Tone(t, 105) * 0.3f) * Env(t, 0.01f, 0.25f)); break;
                case "win": c = Notes(name, new[] { 72, 76, 79 }, 0.09f, 0.35f, 0.5f); break;
                case "bigwin": c = Notes(name, new[] { 72, 76, 79, 84, 79, 84, 88 }, 0.1f, 0.6f, 0.55f, true); break;
                case "jackpot": c = Notes(name, new[] { 60, 64, 67, 72, 76, 79, 84, 88, 91, 96 }, 0.11f, 1.6f, 0.6f, true); break;
                case "coins": c = Make(name, 1.6f, (t, i) => { float s = 0; for (int k = 0; k < 6; k++) { float tk = Mathf.Repeat(t * (5 + k * 1.7f) + k * 0.13f, 1) / (5 + k * 1.7f); s += Tone(tk, 3000 + k * 410) * Env(tk, 0.0005f, 0.03f); } return s * 0.22f * (1 - t / 1.6f); }); break;
                case "cheer": c = Make(name, 2.2f, (t, i) => Noise() * 0.35f * Env(t, 0.25f, 1.1f) * (0.7f + 0.3f * Mathf.Sin(t * 13))); break;
                case "groan": c = Make(name, 1.4f, (t, i) => (Noise() * 0.15f + Tone(t, 160 - t * 40) * 0.12f) * Env(t, 0.15f, 0.6f)); break;
                case "coin": c = Make(name, 0.4f, (t, i) => (Tone(t, 1975) + Tone(t, 2637) * 0.6f) * Env(t, 0.001f, 0.15f) * 0.5f); break;
                case "mark": c = Make(name, 0.6f, (t, i) => (Tone(t, 311) + Tone(t, 330) + Tone(t, 466) * 0.6f) * Env(t, 0.005f, 0.3f) * 0.35f); break;
                case "life": c = Make(name, 1.0f, (t, i) => (Tone(t, 55) * (Env(t, 0.005f, 0.12f) + Env(t - 0.22f, 0.005f, 0.14f)) + Noise() * Env(t, 0.002f, 0.4f) * 0.25f) * 0.9f); break;
                case "bell": c = Make(name, 4.5f, (t, i) => (Tone(t, 220) + Tone(t, 440 * 1.19f) * 0.6f + Tone(t, 660 * 1.42f) * 0.4f + Tone(t, 1100 * 1.53f) * 0.25f) * Env(t, 0.003f, 1.6f) * 0.4f); break;
                case "shock": c = Make(name, 0.8f, (t, i) => (Saw(t, 120) * 0.5f + Noise() * 0.5f) * Env(t, 0.005f, 0.4f) * (0.6f + 0.4f * Mathf.Sin(t * 90))); break;
                case "lever": c = Make(name, 0.3f, (t, i) => (Tone(t, 180) * 0.4f + Noise() * 0.5f) * Env(t, 0.002f, 0.06f)); break;
                case "rocket": c = Make(name, 1.8f, (t, i) => Noise() * 0.3f * Env(t, 0.3f, 1.2f) * (0.5f + t)); break;
                case "boom": c = Make(name, 1.6f, (t, i) => (Noise() * 0.8f + Tone(t, 50) * 0.6f) * Env(t, 0.002f, 0.5f)); break;
                case "ching": c = Make(name, 0.9f, (t, i) => (Tone(t, 2093) + Tone(t, 3136) * 0.7f + Noise() * Env(t, 0.001f, 0.02f)) * Env(t, 0.001f, 0.35f) * 0.45f); break;
                case "whoosh": c = Make(name, 0.5f, (t, i) => Noise() * 0.3f * Mathf.Sin(t / 0.5f * Mathf.PI)); break;
                case "door": c = Make(name, 0.7f, (t, i) => (Tone(t, 90) + Noise() * 0.4f) * Env(t, 0.005f, 0.25f) * 0.6f); break;
            }
            if (c != null) clips[name] = c;
            return c;
        }

        static float Noise() => (float)rnd.NextDouble() * 2 - 1;
        static float Tone(float t, float hz) => t < 0 ? 0 : Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Saw(float t, float hz) => t < 0 ? 0 : 2 * (t * hz - Mathf.Floor(t * hz + 0.5f));
        static float Env(float t, float attack, float decay) => t < 0 ? 0 : t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        static float Midi(int n) => 440f * Mathf.Pow(2, (n - 69) / 12f);

        static AudioClip Make(string name, float secs, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(secs * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1, 1);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>A rising run of bell-ish notes (a win's chime), the last held; `sparkle` adds a shimmer.</summary>
        static AudioClip Notes(string name, int[] notes, float gap, float tail, float vol, bool sparkle = false)
        {
            float secs = notes.Length * gap + tail;
            return Make(name, secs, (t, i) =>
            {
                float s = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float tk = t - k * gap;
                    if (tk < 0) continue;
                    float hz = Midi(notes[k]);
                    float d = k == notes.Length - 1 ? tail * 0.6f : 0.18f;
                    s += (Tone(tk, hz) + Tone(tk, hz * 2) * 0.35f + Tone(tk, hz * 3.01f) * 0.12f) * Env(tk, 0.004f, d);
                }
                if (sparkle) s += Tone(t, 4186 + 300 * Mathf.Sin(t * 30)) * 0.08f * Env(t - notes.Length * gap * 0.5f, 0.01f, tail);
                return s * vol * 0.5f;
            });
        }

        /// <summary>The room: many voices far off (band-limited noise breathing), now and then a slot's jingle.</summary>
        static AudioClip Murmur()
        {
            const float secs = 12;
            float lp = 0, lp2 = 0;
            var jingles = new List<float>();
            for (int k = 0; k < 6; k++) jingles.Add((float)rnd.NextDouble() * (secs - 1));
            return Make("murmur", secs, (t, i) =>
            {
                float n = Noise();
                lp += (n - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.1f;
                float voices = (lp - lp2) * 3.5f * (0.75f + 0.25f * Mathf.Sin(t * 1.3f) * Mathf.Sin(t * 0.37f + 1));
                float j = 0;
                foreach (var at in jingles)
                {
                    float tj = t - at;
                    if (tj < 0 || tj > 0.8f) continue;
                    j += Tone(tj, Midi(84 + (int)(tj * 10) % 3 * 4)) * Env(tj, 0.002f, 0.3f) * 0.05f;
                }
                // loop seamlessly: fade the noise's ends into each other
                float edge = Mathf.Min(1, Mathf.Min(t, secs - t) / 0.5f);
                return (voices * 0.5f) * edge + j;
            });
        }

        /// <summary>A slow lounge loop: an electric piano on a ii-V-I-VI turnaround, a walking bass, brushes.</summary>
        static AudioClip Lounge()
        {
            const float bpm = 84, beat = 60 / bpm;
            int[][] chords = { new[] { 50, 53, 57, 60 }, new[] { 55, 59, 62, 65 }, new[] { 48, 52, 55, 59 }, new[] { 57, 61, 64, 67 } };
            int[][] bass = { new[] { 38, 41, 45, 47 }, new[] { 43, 47, 50, 49 }, new[] { 36, 40, 43, 44 }, new[] { 45, 44, 43, 42 } };
            float secs = chords.Length * 4 * beat * 2;
            return Make("lounge", secs, (t, i) =>
            {
                float bar = t / (4 * beat);
                int ci = (int)bar % chords.Length;
                float inBar = (bar - Mathf.Floor(bar)) * 4 * beat;
                float s = 0;
                // the piano: the chord on beat 1 and the and of 2
                foreach (float hit in new[] { 0f, 1.5f * beat })
                {
                    float tk = inBar - hit;
                    if (tk < 0) continue;
                    foreach (var n in chords[ci])
                    {
                        float hz = Midi(n + 12);
                        s += (Tone(tk, hz) + Tone(tk, hz * 2) * 0.2f) * Env(tk, 0.01f, 0.9f) * 0.05f;
                    }
                }
                // the bass walks a note a beat
                int bi = Mathf.Min(3, (int)(inBar / beat));
                float tb = inBar - bi * beat;
                s += (Tone(tb, Midi(bass[ci][bi])) + Tone(tb, Midi(bass[ci][bi]) * 2) * 0.25f) * Env(tb, 0.01f, 0.35f) * 0.16f;
                // brushes on 2 and 4
                float tbr = Mathf.Repeat(inBar - beat, 2 * beat);
                s += Noise() * Env(tbr, 0.02f, 0.12f) * 0.03f;
                return s;
            });
        }
    }
}
