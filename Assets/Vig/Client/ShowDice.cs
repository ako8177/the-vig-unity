// Hi-Lo's die and Dead Man's two: rattled in the hand, thrown onto the baize, bouncing where the TS
// game put the thuds (Beats.DiceBounces), tumbling onto the face the sim drew. Dead Man's second die
// lands a beat after the first: the wait is the game. Snake eyes turn the pips blood red.
using System.Linq;
using UnityEngine;
using Vig.Sim;
using Vig.Sim.Data;

namespace Vig.Client
{
    public sealed class DiceShow : TableShow
    {
        readonly Props.Die[] dice;
        readonly Vector3[] from, rest;
        readonly bool two;

        public DiceShow(string id, Transform root) : base(id, root)
        {
            two = id == "deadman";
            float size = two ? 0.058f : 0.064f;
            dice = new Props.Die[two ? 2 : 1];
            for (int i = 0; i < dice.Length; i++) dice[i] = new Props.Die(root, size, "die " + (i + 1));
            float y = FeltY + size / 2;
            // thrown from the dealer's right, across the felt toward you, under the screen
            rest = two ? new[] { new Vector3(-0.07f, y, 0.07f), new Vector3(0.075f, y, 0.11f) } : new[] { new Vector3(0.02f, y, 0.08f) };
            from = two ? new[] { new Vector3(0.6f, y + 0.13f, 0.2f), new Vector3(0.66f, y + 0.14f, 0.27f) } : new[] { new Vector3(0.62f, y + 0.13f, 0.22f) };
            Party = new Celebration(root, new Vector3(0, FeltY + 0.03f, 0.09f), FeltY, new Vector3(0, FeltY + 0.05f, -0.07f), Quaternion.Euler(38, 0, 0));
        }

        DiceLab.Throw thr;
        int thrKey = int.MinValue;

        /// <summary>A real throw (DiceLab) from your side of the table, landing on the sim's faces.</summary>
        DiceLab.Throw Get(System.Collections.Generic.List<int> faces, int key)
        {
            if (thr != null && thrKey == key) return thr;
            float size = dice[0].Size, w = (float)Pl.W - 0.26f, d = (float)Pl.D - 0.26f;
            DiceLab.Launch(dice.Length, size, d, key, two ? 0.95f : 0.8f, out var from, out var vel, out var spin);
            thr = DiceLab.Simulate(size, w, d, 0.07f, from, vel, spin, faces.Take(dice.Length).ToArray());
            thrKey = key;
            Sfx.Dice(thr, Pl, two ? Feel.DeadmanS : Feel.DiceS);
            return thr;
        }

        void PoseThrow(DiceLab.Throw th, float s)
        {
            for (int k = 0; k < dice.Length; k++)
            {
                th.Pose(k, s, out var pos, out var rot);
                dice[k].T.localPosition = pos + new Vector3(0, FeltY + dice[k].Size / 2, 0);
                dice[k].T.localRotation = rot;
            }
        }

        protected override void Animate(Game g, TableState t, Outcome o, bool live, float p, float since, float nowS)
        {
            // real dice (U18): the throw is physics, the faces are the sim's
            if (o?.Dice != null && o.Dice.Count >= dice.Length)
            {
                int key = (live ? t.Rounds : t.Rounds - 1) * 97 + o.Dice[0] * 7 + (two ? o.Dice[1] : 0) + (two ? 5000 : 0);
                var th = Get(o.Dice, key);
                PoseThrow(th, live ? p * th.Seconds / 0.97f : th.Seconds);
                var pip0 = two && !live && o.LifeLost == true ? Props.PipBlood : Props.Pip;
                foreach (var d in dice) d.PipColor(pip0);
                return;
            }
            float s = (float)(two ? Feel.DeadmanS : Feel.DiceS);
            for (int k = 0; k < dice.Length; k++)
            {
                var d = dice[k];
                int face = o?.Dice != null && o.Dice.Count > k ? o.Dice[k] : 6;
                float yaw = (Salt * 23 + k * 41 + (Round * 13) % 17) % 50 - 25;
                float land = two ? (k == 0 ? Beats.DeadLandA : Beats.DeadLandB) : Beats.DiceLand;
                float shake = (two ? Beats.DeadShake : Beats.DiceShake) / s;
                if (two && k == 1) shake += (Beats.DeadLandB - Beats.DeadLandA) * 0.85f;
                if (live) Throw(d, p, from[k], rest[k], face, Salt + k * 2, land, shake, yaw);
                else
                {
                    d.T.localPosition = rest[k];
                    d.T.localRotation = Props.Die.FaceUp(face, yaw);
                }
            }
            // snake eyes on Dead Man's: the pips bleed once they're down
            var pip = two && !live && o?.LifeLost == true ? Props.PipBlood : Props.Pip;
            foreach (var d in dice) d.PipColor(pip);
        }

        /// <summary>A thrown die's flight, `u` 0..1 from release to rest: how far along, how high above the
        /// cloth, and which hop (the TS game's throwPath: the first fall, then hops each 0.42 as high).</summary>
        static (float along, float h) Path(float u, float height)
        {
            u = Mathf.Clamp01(u);
            var b = Beats.DiceBounces;
            float h = 0;
            if (u < b[0] / Beats.DiceLand) h = height * (1 - Mathf.Pow(u / (b[0] / Beats.DiceLand), 2));
            else
                for (int k = 1; k < b.Length; k++)
                {
                    float b0 = b[k - 1] / Beats.DiceLand, b1 = b[k] / Beats.DiceLand;
                    if (u < b1)
                    {
                        float s = (u - b0) / (b1 - b0);
                        h = 4 * height * Mathf.Pow(0.42f, k) * s * (1 - s);
                        break;
                    }
                }
            return (Beats.Out3(u), h);
        }

        static void Throw(Props.Die d, float t, Vector3 from, Vector3 to, int face, int salt, float land, float shakeEnd, float yaw)
        {
            if (t < shakeEnd)
            {
                // in the hand: a rattle above the throw point
                float j = Mathf.Sin(t * 90 + salt) * 0.008f;
                d.T.localPosition = from + new Vector3(j, Mathf.Cos(t * 70) * 0.006f, 0);
                d.T.localRotation = Quaternion.Euler(t * 1400 + salt * 40, t * 900, t * 600 + salt * 10);
                return;
            }
            var restRot = Props.Die.FaceUp(face, yaw);
            float u = (t - shakeEnd) / (land - shakeEnd);
            if (u >= 1)
            {
                d.T.localPosition = to;
                d.T.localRotation = restRot;
                return;
            }
            var (along, h) = Path(u, from.y - to.y);
            var flat = Vector3.Lerp(new Vector3(from.x, to.y, from.z), to, along);
            // it rolls forward along its flight, the spin running down to nothing as it settles on the face
            var dir = new Vector3(to.x - from.x, 0, to.z - from.z).normalized;
            var axis = Vector3.Cross(Vector3.up, dir);
            float spin = (1 - Beats.Out3(u)) * (2.5f + (salt % 3) * 0.25f) * 360f;
            var rot = Quaternion.AngleAxis(spin, axis) * Quaternion.AngleAxis(spin * 0.3f, Vector3.up) * restRot;
            // a die on a corner stands taller than one on a face: its lowest point touches the cloth
            float reach = (Mathf.Abs((rot * Vector3.right).y) + Mathf.Abs((rot * Vector3.up).y) + Mathf.Abs((rot * Vector3.forward).y)) * d.Size / 2;
            d.T.localPosition = flat + Vector3.up * (h + reach - d.Size / 2);
            d.T.localRotation = rot;
        }
    }
}
