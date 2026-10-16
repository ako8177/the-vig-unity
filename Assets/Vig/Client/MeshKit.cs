// Procedural meshes for the room: bevelled boxes, lathed solids (legs, pedestals, pillars, stools),
// tubes along a path (neon, rails, ropes). Everything in the room is built from the layout data by
// code, so the scene is reproducible and what you bump into is what you see.
using System.Collections.Generic;
using UnityEngine;

namespace Vig.Client
{
    public static class MeshKit
    {
        /// <summary>A box of size (sx, sy, sz) centered on the origin with its edges rounded by r.</summary>
        public static Mesh RoundedBox(float sx, float sy, float sz, float r, int seg = 4, float uvScale = 1)
        {
            float hx = sx / 2, hy = sy / 2, hz = sz / 2;
            r = Mathf.Min(r, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.999f);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float[] Samples(float h)
            {
                var s = new List<float> { -h };
                for (int i = 1; i <= seg; i++) s.Add(-h + r * (1 - Mathf.Cos(i / (float)seg * Mathf.PI / 2)));
                for (int i = seg - 1; i >= 0; i--) s.Add(h - r * (1 - Mathf.Cos(i / (float)seg * Mathf.PI / 2)));
                return s.ToArray();
            }
            var xs = Samples(hx); var ys = Samples(hy); var zs = Samples(hz);
            var inner = new Vector3(hx - r, hy - r, hz - r);
            // six faces: (normal axis, sign, u axis, v axis)
            void Face(int axis, float sign, int ua, int va)
            {
                float[] su = ua == 0 ? xs : ua == 1 ? ys : zs;
                float[] sv = va == 0 ? xs : va == 1 ? ys : zs;
                float h = axis == 0 ? hx : axis == 1 ? hy : hz;
                int start = verts.Count;
                for (int j = 0; j < sv.Length; j++)
                    for (int i = 0; i < su.Length; i++)
                    {
                        var p = Vector3.zero;
                        p[axis] = sign * h; p[ua] = su[i]; p[va] = sv[j];
                        var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - c;
                        Vector3 n = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero;
                        if (n == Vector3.zero) n[axis] = sign;
                        verts.Add(c + n * r);
                        norms.Add(n);
                        uvs.Add(new Vector2(p[ua], p[va]) * uvScale);
                    }
                int w = su.Length;
                for (int j = 0; j < sv.Length - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = start + j * w + i, b = a + 1, c2 = a + w, d2 = c2 + 1;
                        // wind so the face points along +sign on its axis (Unity: clockwise is front)
                        bool flip = (sign > 0) ^ (((ua + 1) % 3) == va);
                        if (flip) { tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(b); tris.Add(c2); tris.Add(d2); }
                        else { tris.Add(a); tris.Add(b); tris.Add(c2); tris.Add(b); tris.Add(d2); tris.Add(c2); }
                    }
            }
            Face(0, 1, 2, 1); Face(0, -1, 2, 1);
            Face(1, 1, 0, 2); Face(1, -1, 0, 2);
            Face(2, 1, 0, 1); Face(2, -1, 0, 1);
            return Build("RoundedBox", verts, norms, uvs, tris);
        }

        /// <summary>A solid of revolution: profile points (radius, height) from bottom to top, lathed round Y.
        /// A point repeated makes a hard edge there.</summary>
        public static Mesh Lathe(IList<Vector2> profile, int sides = 32, bool capBottom = true, bool capTop = true)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int n = profile.Count;
            // a normal per profile point, from its neighbours (a repeated point starts a new smoothing group)
            var pn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = i > 0 && profile[i - 1] != profile[i] ? profile[i - 1] : profile[i];
                Vector2 next = i < n - 1 && profile[i + 1] != profile[i] ? profile[i + 1] : profile[i];
                if (i > 0 && profile[i - 1] == profile[i]) prev = profile[i];
                if (i < n - 1 && profile[i + 1] == profile[i]) next = profile[i];
                var t = next - prev;
                if (t.sqrMagnitude < 1e-12f) t = i < n - 1 ? profile[Mathf.Min(n - 1, i + 2)] - profile[i] : profile[i] - profile[Mathf.Max(0, i - 2)];
                pn[i] = new Vector2(t.y, -t.x).normalized;
            }
            float len = 0;
            var vs = new float[n];
            for (int i = 1; i < n; i++) { len += Vector2.Distance(profile[i - 1], profile[i]); vs[i] = len; }
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int i = 0; i < n; i++)
                {
                    var p = profile[i];
                    verts.Add(new Vector3(p.x * ca, p.y, p.x * sa));
                    norms.Add(new Vector3(pn[i].x * ca, pn[i].y, pn[i].x * sa));
                    uvs.Add(new Vector2(s / (float)sides * 2 * Mathf.PI * Mathf.Max(0.05f, profile[0].x), vs[i]));
                }
            }
            for (int s = 0; s < sides; s++)
                for (int i = 0; i < n - 1; i++)
                {
                    if (profile[i] == profile[i + 1]) continue;
                    int a = s * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
                }
            void Cap(int i, float ny)
            {
                if (profile[i].x < 1e-5f) return;
                int center = verts.Count;
                verts.Add(new Vector3(0, profile[i].y, 0)); norms.Add(new Vector3(0, ny, 0)); uvs.Add(Vector2.zero);
                int ring = verts.Count;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    verts.Add(new Vector3(profile[i].x * Mathf.Cos(a), profile[i].y, profile[i].x * Mathf.Sin(a)));
                    norms.Add(new Vector3(0, ny, 0));
                    uvs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * profile[i].x);
                }
                for (int s = 0; s < sides; s++)
                {
                    if (ny > 0) { tris.Add(center); tris.Add(ring + s + 1); tris.Add(ring + s); }
                    else { tris.Add(center); tris.Add(ring + s); tris.Add(ring + s + 1); }
                }
            }
            if (capBottom) Cap(0, -1);
            if (capTop) Cap(n - 1, 1);
            return Build("Lathe", verts, norms, uvs, tris);
        }

        /// <summary>A round tube of radius r along a path; closed joins the ends.</summary>
        public static Mesh Tube(IList<Vector3> path, float r, int sides = 10, bool closed = false, bool caps = true)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int n = path.Count;
            Vector3 up = Vector3.up;
            Vector3 prevSide = Vector3.zero;
            float dist = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 t;
                if (closed) t = (path[(i + 1) % n] - path[(i - 1 + n) % n]).normalized;
                else t = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                // a frame that turns smoothly along the path (parallel transport)
                Vector3 side = i == 0 ? Vector3.Cross(t, Mathf.Abs(Vector3.Dot(t, up)) > 0.9f ? Vector3.right : up).normalized
                    : Vector3.Cross(t, Vector3.Cross(prevSide, t)).normalized;
                if (side.sqrMagnitude < 1e-6f) side = prevSide;
                // keep the frame continuous
                if (i > 0 && Vector3.Dot(side, prevSide) < 0) side = -side;
                prevSide = side;
                Vector3 bin = Vector3.Cross(t, side).normalized;
                if (i > 0) dist += Vector3.Distance(path[i - 1], path[i]);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    var nn = side * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    verts.Add(path[i] + nn * r);
                    norms.Add(nn);
                    uvs.Add(new Vector2(s / (float)sides, dist));
                }
            }
            int rows = closed ? n : n - 1;
            int w = sides + 1;
            for (int i = 0; i < rows; i++)
            {
                int i0 = i * w, i1 = ((i + 1) % n) * w;
                for (int s = 0; s < sides; s++)
                {
                    int a = i0 + s, b = i0 + s + 1, c = i1 + s, d = i1 + s + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            if (!closed && caps)
            {
                for (int end = 0; end < 2; end++)
                {
                    int i = end == 0 ? 0 : n - 1;
                    Vector3 t = end == 0 ? (path[0] - path[1]).normalized : (path[n - 1] - path[n - 2]).normalized;
                    int center = verts.Count;
                    verts.Add(path[i]); norms.Add(t); uvs.Add(Vector2.zero);
                    int ring = verts.Count;
                    for (int s = 0; s <= sides; s++) { verts.Add(verts[i * w + s]); norms.Add(t); uvs.Add(Vector2.zero); }
                    for (int s = 0; s < sides; s++)
                    {
                        if (end == 0) { tris.Add(center); tris.Add(ring + s + 1); tris.Add(ring + s); }
                        else { tris.Add(center); tris.Add(ring + s); tris.Add(ring + s + 1); }
                    }
                }
            }
            return Build("Tube", verts, norms, uvs, tris);
        }

        /// <summary>A rounded rectangle's outline in the XY plane (for neon borders), centered on the origin.</summary>
        public static List<Vector3> RoundRect(float w, float h, float r, int seg = 6)
        {
            var pts = new List<Vector3>();
            r = Mathf.Min(r, Mathf.Min(w, h) / 2);
            Vector2[] centers = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90 + i * 90f / seg) * Mathf.Deg2Rad;
                    pts.Add(new Vector3(centers[c].x + Mathf.Cos(a) * r, centers[c].y + Mathf.Sin(a) * r, 0));
                }
            return pts;
        }

        /// <summary>A circle in the XZ plane.</summary>
        public static List<Vector3> Circle(float r, int seg = 48, float y = 0)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                pts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
            return pts;
        }

        /// <summary>A flat quad facing +Z (toward a viewer standing at -Z looks at its back: use for screens with a rotation).</summary>
        public static Mesh Quad(float w, float h)
        {
            var m = new Mesh { name = "Quad" };
            m.SetVertices(new List<Vector3> { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0) });
            m.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
