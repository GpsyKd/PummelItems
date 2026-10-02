using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural shapes for the models.
///
/// Unity's primitives are a cube with razor edges, a ball, a can and a pill, and the whole set
/// used to be built from those alone - which is what made it read as programmer art no matter
/// how good the proportions were. This kit covers what the primitives cannot: soft edges that
/// catch a highlight, shapes turned on a lathe, tubes swept along a curve, tori, rocks,
/// crystals and blobs.
///
/// Conventions: Unity's left-handed space, where a triangle (a, b, c) faces the side that
/// Cross(b - a, c - a) points to. Every generator below is wound so its faces point out.
///
/// Every mesh is saved as its own asset with a unique name. CreateAsset on a taken path
/// destroys whatever lives there (the trap behind the old magenta materials), so a name used
/// twice is reported as an error instead of quietly replacing the first mesh.
/// </summary>
public static class ShapeKit
{
    private const string MeshDir = "Assets/Meshes";
    private static readonly HashSet<string> s_paths = new HashSet<string>();

    public static void ResetNames()
    {
        s_paths.Clear();
    }

    public static Mesh Save(Mesh mesh, string name)
    {
        string path = MeshDir + "/Kit_" + name + ".asset";
        if (!s_paths.Add(path)) Debug.LogError("[PCI] mesh name used twice: " + name);

        mesh.name = "Kit_" + name;
        mesh.RecalculateBounds();
        Directory.CreateDirectory(MeshDir);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    /// <summary>A child object carrying one of these meshes.</summary>
    public static GameObject Part(GameObject parent, string name, Mesh mesh, Vector3 position, Quaternion rotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        return go;
    }

    private static Mesh Build(List<Vector3> v, List<Vector3> n, List<int> t)
    {
        Mesh m = new Mesh();
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        if (n != null) m.SetNormals(n);
        m.SetTriangles(t, 0);
        if (n == null) m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ rounded box

    /// <summary>
    /// A box of the given full size with every edge and corner rounded by <paramref name="radius"/>.
    /// Faces stay flat; only the band within <paramref name="radius"/> of an edge curves.
    /// </summary>
    public static Mesh RoundedBox(Vector3 size, float radius, int seg = 3)
    {
        Vector3 h = size * 0.5f;
        float r = Mathf.Clamp(radius, 0.0005f, 0.49f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        Vector3 inner = new Vector3(h.x - r, h.y - r, h.z - r);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        // (normal, u, v) with Cross(u, v) == normal, so (v00, v10, v01) faces outward.
        Vector3[,] faces =
        {
            { Vector3.right,   Vector3.up,      Vector3.forward },
            { Vector3.left,    Vector3.forward, Vector3.up },
            { Vector3.up,      Vector3.forward, Vector3.right },
            { Vector3.down,    Vector3.right,   Vector3.forward },
            { Vector3.forward, Vector3.right,   Vector3.up },
            { Vector3.back,    Vector3.up,      Vector3.right },
        };

        for (int f = 0; f < 6; f++)
        {
            Vector3 nrm = faces[f, 0], u = faces[f, 1], w = faces[f, 2];
            float hu = Vector3.Scale(Abs(u), h).magnitude;
            float hw = Vector3.Scale(Abs(w), h).magnitude;
            float hn = Vector3.Scale(Abs(nrm), h).magnitude;
            float[] cu = BevelCoords(hu, r, seg);
            float[] cw = BevelCoords(hw, r, seg);
            int start = verts.Count;

            for (int j = 0; j < cw.Length; j++)
            {
                for (int i = 0; i < cu.Length; i++)
                {
                    Vector3 p = nrm * hn + u * cu[i] + w * cw[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                            Mathf.Clamp(p.y, -inner.y, inner.y),
                                            Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - c;
                    Vector3 dn = d.sqrMagnitude > 1e-12f ? d.normalized : nrm;
                    verts.Add(c + dn * r);
                    norms.Add(dn);
                }
            }

            int nu = cu.Length;
            for (int j = 0; j < cw.Length - 1; j++)
            {
                for (int i = 0; i < nu - 1; i++)
                {
                    int v00 = start + j * nu + i, v10 = v00 + 1, v01 = v00 + nu, v11 = v01 + 1;
                    tris.Add(v00); tris.Add(v10); tris.Add(v01);
                    tris.Add(v10); tris.Add(v11); tris.Add(v01);
                }
            }
        }
        return Build(verts, norms, tris);
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static float[] BevelCoords(float half, float r, int seg)
    {
        float[] c = new float[(seg + 1) * 2];
        for (int i = 0; i <= seg; i++)
        {
            float k = (float)i / seg;
            c[i] = -half + r * k;
            c[seg + 1 + i] = half - r + r * k;
        }
        return c;
    }

    // ------------------------------------------------------------------------ lathe

    /// <summary>
    /// Surface of revolution around Y. <paramref name="profile"/> runs from bottom to top as
    /// (radius, height); <paramref name="normals"/> are the profile's own 2D normals (outward,
    /// up). sx/sz stretch the circle into an ellipse.
    /// </summary>
    public static Mesh Lathe(Vector2[] profile, Vector2[] normals, int sides, float sx = 1f, float sz = 1f)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int k = 0; k < profile.Length; k++)
        {
            for (int j = 0; j < sides; j++)
            {
                float a = 2f * Mathf.PI * j / sides;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts.Add(new Vector3(profile[k].x * ca * sx, profile[k].y, profile[k].x * sa * sz));
                Vector2 n = normals[k];
                norms.Add(new Vector3(n.x * ca / sx, n.y, n.x * sa / sz).normalized);
            }
        }

        for (int k = 0; k < profile.Length - 1; k++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = k * sides + j, b = (k + 1) * sides + j;
                int c = k * sides + (j + 1) % sides, d = (k + 1) * sides + (j + 1) % sides;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }
        return Build(verts, norms, tris);
    }

    /// <summary>Lathe with normals worked out from the profile itself (for smooth outlines).</summary>
    public static Mesh Lathe(Vector2[] profile, int sides, float sx = 1f, float sz = 1f)
    {
        Vector2[] n = new Vector2[profile.Length];
        for (int k = 0; k < profile.Length; k++)
        {
            Vector2 prev = profile[Mathf.Max(0, k - 1)], next = profile[Mathf.Min(profile.Length - 1, k + 1)];
            Vector2 t = (next - prev).normalized;
            n[k] = new Vector2(t.y, -t.x);   // tangent turned clockwise: outward for an upward profile
            if (profile[k].x < 1e-5f) n[k] = new Vector2(0f, k == 0 ? -1f : 1f);
        }
        return Lathe(profile, n, sides, sx, sz);
    }

    /// <summary>
    /// A cylinder along Y with its rim rounded by <paramref name="bevel"/>: rx/rz are the radii,
    /// <paramref name="height"/> the full height. Same footprint as a scaled Unity cylinder.
    /// </summary>
    public static Mesh BevelCylinder(float rx, float rz, float height, float bevel, int sides = 28)
    {
        float r = Mathf.Max(rx, rz), hh = height * 0.5f;
        float b = Mathf.Clamp(bevel, 0.0002f, 0.49f * Mathf.Min(r, height));
        const int arc = 4;

        List<Vector2> p = new List<Vector2>();
        List<Vector2> n = new List<Vector2>();
        p.Add(new Vector2(0f, -hh)); n.Add(Vector2.down);
        for (int i = 0; i <= arc; i++)
        {
            float a = Mathf.Lerp(-90f, 0f, (float)i / arc) * Mathf.Deg2Rad;
            p.Add(new Vector2(r - b + b * Mathf.Cos(a), -hh + b + b * Mathf.Sin(a)));
            n.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        for (int i = 0; i <= arc; i++)
        {
            float a = Mathf.Lerp(0f, 90f, (float)i / arc) * Mathf.Deg2Rad;
            p.Add(new Vector2(r - b + b * Mathf.Cos(a), hh - b + b * Mathf.Sin(a)));
            n.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        p.Add(new Vector2(0f, hh)); n.Add(Vector2.up);

        return Lathe(p.ToArray(), n.ToArray(), sides, rx / r, rz / r);
    }

    // ------------------------------------------------------------------------ sweep

    /// <summary>
    /// A tube along <paramref name="path"/> (dense points; more points, smoother bend). The
    /// section is an ellipse: <paramref name="wx"/> scales it across the path in the plane of
    /// <paramref name="upHint"/>'s normal, <paramref name="wy"/> along <paramref name="upHint"/>.
    /// A radius of 0 at an end closes it into a point.
    /// </summary>
    public static Mesh Sweep(IList<Vector3> path, Func<float, float> radius, Vector3 upHint,
                             float wx = 1f, float wy = 1f, int sides = 16)
    {
        int count = path.Count;
        float[] len = new float[count];
        for (int i = 1; i < count; i++) len[i] = len[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        float total = Mathf.Max(1e-6f, len[count - 1]);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i < count; i++)
        {
            Vector3 T = (path[Mathf.Min(count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            Vector3 side = Vector3.Cross(T, upHint);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(T, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, T).normalized;
            float r = radius(len[i] / total);

            for (int j = 0; j < sides; j++)
            {
                float a = 2f * Mathf.PI * j / sides;
                verts.Add(path[i] + side * (Mathf.Cos(a) * r * wx) + up * (Mathf.Sin(a) * r * wy));
            }
        }

        for (int i = 0; i < count - 1; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j, b = i * sides + (j + 1) % sides;
                int c = (i + 1) * sides + j, d = (i + 1) * sides + (j + 1) % sides;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        return Build(verts, null, tris);
    }

    /// <summary>Points along a quadratic Bezier, for sweeps.</summary>
    public static List<Vector3> Bezier(Vector3 a, Vector3 control, Vector3 b, int steps)
    {
        List<Vector3> pts = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps, u = 1f - t;
            pts.Add(u * u * a + 2f * u * t * control + t * t * b);
        }
        return pts;
    }

    /// <summary>Points through the given ones, Catmull-Rom smoothed, for sweeps.</summary>
    public static List<Vector3> Smooth(IList<Vector3> pts, int perSegment)
    {
        List<Vector3> o = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            for (int s = 0; s < perSegment; s++)
            {
                float t = (float)s / perSegment, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        o.Add(pts[pts.Count - 1]);
        return o;
    }

    /// <summary>Radius profile that rounds both ends of a sweep shut.</summary>
    public static float Capped(float t, float end = 0.05f)
    {
        return Mathf.Sqrt(Mathf.Clamp01(t / end)) * Mathf.Sqrt(Mathf.Clamp01((1f - t) / end));
    }

    // ------------------------------------------------------------------------ torus

    /// <summary>A ring lying in the XZ plane, around Y.</summary>
    public static Mesh Torus(float major, float minor, int majorSegs = 72, int minorSegs = 16)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i < majorSegs; i++)
        {
            float t = 2f * Mathf.PI * i / majorSegs;
            Vector3 c = new Vector3(Mathf.Cos(t) * major, 0f, Mathf.Sin(t) * major);
            for (int j = 0; j < minorSegs; j++)
            {
                float p = 2f * Mathf.PI * j / minorSegs;
                Vector3 n = new Vector3(Mathf.Cos(p) * Mathf.Cos(t), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Sin(t));
                verts.Add(c + n * minor);
                norms.Add(n);
            }
        }

        for (int i = 0; i < majorSegs; i++)
        {
            for (int j = 0; j < minorSegs; j++)
            {
                int a = i * minorSegs + j, b = ((i + 1) % majorSegs) * minorSegs + j;
                int c = i * minorSegs + (j + 1) % minorSegs, d = ((i + 1) % majorSegs) * minorSegs + (j + 1) % minorSegs;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        return Build(verts, norms, tris);
    }

    // ------------------------------------------------------------- spheres and rocks

    private static void IcoSphere(int subdivisions, List<Vector3> verts, List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v0 =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] f0 =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        foreach (Vector3 v in v0) verts.Add(v.normalized);
        tris.AddRange(f0);

        for (int s = 0; s < subdivisions; s++)
        {
            Dictionary<long, int> mid = new Dictionary<long, int>();
            List<int> next = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b, verts, mid), bc = Mid(b, c, verts, mid), ca = Mid(c, a, verts, mid);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris.Clear();
            tris.AddRange(next);
        }

        // Make every face point outward, whatever the source list's winding was.
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
            {
                int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp;
            }
        }
    }

    private static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        int idx;
        if (cache.TryGetValue(key, out idx)) return idx;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        idx = verts.Count - 1;
        cache[key] = idx;
        return idx;
    }

    /// <summary>Smooth, fixed-seed noise on the unit sphere, about -1..1.</summary>
    private static Func<Vector3, float> Noise(int seed, int octaves, float frequency)
    {
        System.Random rnd = new System.Random(seed);
        Vector3[] dirs = new Vector3[octaves * 3];
        float[] phase = new float[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            dirs[i] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
            phase[i] = (float)rnd.NextDouble() * 6.283f;
        }
        return p =>
        {
            float sum = 0f, amp = 1f, freq = frequency, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int i = o * 3 + k;
                    sum += amp * Mathf.Sin(Vector3.Dot(p, dirs[i]) * freq + phase[i]);
                    norm += amp;
                }
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return sum / norm * 1.6f;
        };
    }

    /// <summary>
    /// A lumpy rock with flat facets: an icosphere pushed in and out by noise, then given one
    /// normal per face so every facet catches the light on its own.
    /// </summary>
    public static Mesh Rock(float radius, int subdivisions, float roughness, int seed, Vector3 squash)
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        IcoSphere(subdivisions, v, t);
        Func<Vector3, float> noise = Noise(seed, 3, 2.3f);
        for (int i = 0; i < v.Count; i++)
            v[i] = Vector3.Scale(v[i] * (radius * (1f + roughness * noise(v[i]))), squash);

        List<Vector3> fv = new List<Vector3>();
        List<Vector3> fn = new List<Vector3>();
        List<int> ft = new List<int>();
        for (int i = 0; i < t.Count; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int s = fv.Count;
            fv.Add(a); fv.Add(b); fv.Add(c);
            fn.Add(n); fn.Add(n); fn.Add(n);
            ft.Add(s); ft.Add(s + 1); ft.Add(s + 2);
        }
        return Build(fv, fn, ft);
    }

    /// <summary>A soft lumpy blob: the same noisy icosphere, smooth-shaded, bottom flattened.</summary>
    public static Mesh Blob(float radius, float lumpiness, int seed, float squashY, float floorY)
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        IcoSphere(4, v, t);
        Func<Vector3, float> noise = Noise(seed, 2, 2.0f);
        for (int i = 0; i < v.Count; i++)
        {
            Vector3 p = v[i] * (radius * (1f + lumpiness * noise(v[i])));
            p.y *= squashY;
            if (p.y < floorY) p.y = floorY + (p.y - floorY) * 0.25f;   // sags onto whatever it hit
            v[i] = p;
        }
        return Build(v, null, t);
    }

    // ------------------------------------------------------------------------ frill

    /// <summary>
    /// A band of cut paper fringe around Y, the way a pinata is covered: it starts at radius
    /// <paramref name="rTop"/> at y = 0 and flares to <paramref name="rHem"/> at y = -height,
    /// where the hem is cut into <paramref name="teeth"/> points <paramref name="toothDepth"/>
    /// long. <paramref name="curve"/> shapes the flare: above 1 it stays narrow and kicks out
    /// at the hem, below 1 it swells at once - a dome, for covering the end of something.
    /// Both faces are built, so the inside of the flare never shows as a hole.
    /// </summary>
    public static Mesh Frill(float rTop, float rHem, float height, int teeth, float toothDepth, float curve = 1.5f)
    {
        int cols = teeth * 2;   // a point and a notch per tooth
        const int rows = 4;
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();

        for (int side = 0; side < 2; side++)
        {
            int start = v.Count;
            float inset = side == 0 ? 0f : -0.0015f;
            for (int k = 0; k < rows; k++)
            {
                float f = (float)k / (rows - 1);
                for (int j = 0; j < cols; j++)
                {
                    float a = 2f * Mathf.PI * j / cols;
                    bool hem = k == rows - 1, point = hem && j % 2 == 0;
                    float r = Mathf.Lerp(rTop, rHem, Mathf.Pow(f, curve)) + (point ? toothDepth * 0.2f : 0f) + inset;
                    float y = -height * f - (point ? toothDepth : 0f);
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
            for (int k = 0; k < rows - 1; k++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int a = start + k * cols + j, c = start + k * cols + (j + 1) % cols;
                    int b = a + cols, d = c + cols;
                    if (side == 0) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                    else           { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
                }
            }
        }
        return Build(v, null, t);
    }

    // ---------------------------------------------------------------------- extrude

    /// <summary>
    /// A flat plank cut to <paramref name="outline"/> (a convex outline in the XY plane), with
    /// <paramref name="depth"/> of thickness along Z. Flat-shaded, like sawn wood.
    /// </summary>
    public static Mesh Extrude(Vector2[] outline, float depth)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<int> t = new List<int>();
        float hz = depth * 0.5f;

        Vector2 c2 = Vector2.zero;
        foreach (Vector2 p in outline) c2 += p;
        c2 /= outline.Length;
        Vector3 centre = new Vector3(c2.x, c2.y, 0f);

        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 p0 = outline[i], p1 = outline[(i + 1) % outline.Length];
            Vector3 f0 = new Vector3(p0.x, p0.y, hz), f1 = new Vector3(p1.x, p1.y, hz);
            Vector3 b0 = new Vector3(p0.x, p0.y, -hz), b1 = new Vector3(p1.x, p1.y, -hz);

            Face(v, n, t, centre, f0, f1, b1);
            Face(v, n, t, centre, f0, b1, b0);
            Face(v, n, t, centre, new Vector3(c2.x, c2.y, hz), f0, f1);
            Face(v, n, t, centre, new Vector3(c2.x, c2.y, -hz), b1, b0);
        }
        return Build(v, n, t);
    }

    // ---------------------------------------------------------------------- crystal

    /// <summary>
    /// A faceted crystal standing on the origin along +Y: a prism of <paramref name="sides"/>
    /// faces, <paramref name="length"/> tall, ending in a point <paramref name="tip"/> long.
    /// </summary>
    public static Mesh Crystal(float radius, float length, float tip, int sides = 6)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<int> t = new List<int>();
        Vector3 centre = new Vector3(0f, (length + tip) * 0.4f, 0f);
        Vector3 apex = new Vector3(0f, length + tip, 0f);

        for (int i = 0; i < sides; i++)
        {
            float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            // the base is a touch narrower than the shoulder: crystals grow out, not straight up
            Vector3 s0 = b0 * 1.08f + Vector3.up * length, s1 = b1 * 1.08f + Vector3.up * length;

            Face(v, n, t, centre, b0, b1, s1);
            Face(v, n, t, centre, b0, s1, s0);
            Face(v, n, t, centre, s0, s1, apex);
            Face(v, n, t, centre, Vector3.zero, b1, b0);
        }
        return Build(v, n, t);
    }

    private static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 centre, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 nn = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(nn, (a + b + c) / 3f - centre) < 0f) { Vector3 tmp = b; b = c; c = tmp; nn = -nn; }
        nn.Normalize();
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(nn); n.Add(nn); n.Add(nn);
        t.Add(s); t.Add(s + 1); t.Add(s + 2);
    }
}
