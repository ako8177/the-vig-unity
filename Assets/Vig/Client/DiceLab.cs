// Real dice (U20): every throw is simulated in PhysX in a lab far below the room (a felt and four walls the
// size of the table, the dice as rigid bodies), recorded frame by frame, then each die's mesh is turned by a
// die symmetry so the face the sim drew is the one that ends up on top. What you see is a real roll; what
// it lands on is still the sim's. The room itself has no physics, so the lab is the only thing simulated.
using System.Collections.Generic;
using UnityEngine;

namespace Vig.Client
{
    public static class DiceLab
    {
        /// <summary>A recorded throw: per frame (1/60 s), each die's position and turn in the table's frame;
        /// the times the dice hit something hard (for the sound); how long until they settle.</summary>
        public sealed class Throw
        {
            public Vector3[][] Pos;
            public Quaternion[][] Rot;
            public readonly List<float> Hits = new List<float>();
            public int Frames => Pos.Length;
            public float Seconds => Frames / 60f;

            /// <summary>Die k at time `s` (clamped to the throw), interpolated between frames.</summary>
            public void Pose(int k, float s, out Vector3 pos, out Quaternion rot)
            {
                float f = Mathf.Clamp(s * 60f, 0, Frames - 1);
                int a = Mathf.FloorToInt(f), b = Mathf.Min(Frames - 1, a + 1);
                float t = f - a;
                pos = Vector3.Lerp(Pos[a][k], Pos[b][k], t);
                rot = Quaternion.Slerp(Rot[a][k], Rot[b][k], t);
            }
        }

        static readonly Vector3 LabAt = new Vector3(0, -600, 0);
        static PhysicsMaterial felt, bone;

        /// <summary>Throw `faces.Length` dice of `size` onto a felt `w` x `d` (walls `wallH` high) from `from`
        /// with `vel` and `spin` (table frame, felt top at y = 0); land them on `faces`.</summary>
        public static Throw Simulate(float size, float w, float d, float wallH, Vector3[] from, Vector3[] vel, Vector3[] spin, int[] faces)
        {
            Physics.simulationMode = SimulationMode.Script;
            felt = felt ? felt : new PhysicsMaterial("felt") { bounciness = 0.25f, dynamicFriction = 0.45f, staticFriction = 0.6f, bounceCombine = PhysicsMaterialCombine.Average };
            bone = bone ? bone : new PhysicsMaterial("bone") { bounciness = 0.45f, dynamicFriction = 0.3f, staticFriction = 0.4f, bounceCombine = PhysicsMaterialCombine.Maximum };
            var lab = new GameObject("dice lab").transform;
            lab.position = LabAt;
            void Wall(Vector3 c, Vector3 s, PhysicsMaterial m)
            {
                var go = new GameObject("wall");
                go.transform.SetParent(lab, false);
                go.transform.localPosition = c;
                var bc = go.AddComponent<BoxCollider>();
                bc.size = s;
                bc.sharedMaterial = m;
            }
            Wall(new Vector3(0, -0.1f, 0), new Vector3(w + 1, 0.2f, d + 1), felt);
            Wall(new Vector3(0, wallH / 2, d / 2 + 0.05f), new Vector3(w + 0.2f, wallH, 0.1f), bone);
            Wall(new Vector3(0, wallH / 2, -d / 2 - 0.05f), new Vector3(w + 0.2f, wallH, 0.1f), bone);
            Wall(new Vector3(w / 2 + 0.05f, wallH / 2, 0), new Vector3(0.1f, wallH, d + 0.2f), bone);
            Wall(new Vector3(-w / 2 - 0.05f, wallH / 2, 0), new Vector3(0.1f, wallH, d + 0.2f), bone);
            int n = faces.Length;
            var bodies = new Rigidbody[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("die");
                go.transform.SetParent(lab, false);
                go.transform.localPosition = from[i];
                go.transform.localRotation = Quaternion.Euler(i * 37 + from[i].x * 900, i * 71 + from[i].z * 700, i * 13);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one * size;
                bc.sharedMaterial = bone;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.016f;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.35f;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.maxAngularVelocity = 60;
                rb.linearVelocity = vel[i];
                rb.angularVelocity = spin[i];
                bodies[i] = rb;
            }
            var pos = new List<Vector3[]>();
            var rot = new List<Quaternion[]>();
            var t = new Throw();
            var lastV = new Vector3[n];
            int still = 0;
            for (int f = 0; f < 60 * 6; f++)
            {
                var ps = new Vector3[n];
                var rs = new Quaternion[n];
                bool quiet = true;
                for (int i = 0; i < n; i++)
                {
                    ps[i] = bodies[i].transform.localPosition;
                    rs[i] = bodies[i].transform.localRotation;
                    var v = bodies[i].linearVelocity;
                    // a hard stop or bounce is a click on the felt or the wall
                    if (f > 0 && (v - lastV[i]).magnitude > 0.45f) t.Hits.Add(f / 60f);
                    lastV[i] = v;
                    if (v.magnitude > 0.02f || bodies[i].angularVelocity.magnitude > 0.2f) quiet = false;
                }
                pos.Add(ps);
                rot.Add(rs);
                still = quiet ? still + 1 : 0;
                if (still > 12 && f > 30) break;
                Physics.Simulate(1 / 60f);
            }
            // turn each die so the sim's face is the one on top at rest
            var last = rot[rot.Count - 1];
            var remap = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                int up = 1;
                float best = -2;
                for (int fc = 1; fc <= 6; fc++)
                {
                    float dot = Vector3.Dot(last[i] * Props.FaceN[fc], Vector3.up);
                    if (dot > best) { best = dot; up = fc; }
                }
                remap[i] = Symmetry(Props.FaceN[Mathf.Clamp(faces[i], 1, 6)], Props.FaceN[up]);
            }
            for (int f = 0; f < rot.Count; f++)
                for (int i = 0; i < n; i++) rot[f][i] = rot[f][i] * remap[i];
            t.Pos = pos.ToArray();
            t.Rot = rot.ToArray();
            if (Application.isPlaying) Object.Destroy(lab.gameObject); else Object.DestroyImmediate(lab.gameObject);
            return t;
        }

        /// <summary>A turn of the cube onto itself taking face normal `from` to `to` (a quarter or half turn
        /// about a body axis), so the pips stay where a die's pips are.</summary>
        static Quaternion Symmetry(Vector3 from, Vector3 to)
        {
            if (from == to) return Quaternion.identity;
            if (from == -to)
            {
                var axis = Mathf.Abs(from.x) < 0.5f ? Vector3.right : Vector3.forward;
                return Quaternion.AngleAxis(180, axis);
            }
            return Quaternion.AngleAxis(90, Vector3.Cross(from, to).normalized);
        }

        /// <summary>A throw's starting push from the seat's side toward the far wall, varied by `salt`.</summary>
        public static void Launch(int n, float size, float d, int salt, float power, out Vector3[] from, out Vector3[] vel, out Vector3[] spin)
        {
            var r = new System.Random(salt * 7919 + 17);
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            from = new Vector3[n];
            vel = new Vector3[n];
            spin = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                from[i] = new Vector3(F(-0.12f, 0.12f) + i * size * 1.6f - (n - 1) * size * 0.8f, F(0.14f, 0.2f), -d / 2 + 0.12f + i * 0.02f);
                vel[i] = new Vector3(F(-0.35f, 0.35f), F(0.4f, 0.9f), F(1.6f, 2.3f) * power);
                spin[i] = new Vector3(F(-25, 25), F(-25, 25), F(-25, 25));
            }
        }
    }
}
