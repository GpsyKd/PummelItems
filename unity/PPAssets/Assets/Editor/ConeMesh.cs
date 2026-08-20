using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Truncated cones, which Unity has no primitive for. A taper is the one shape that reads as
/// "opening" or "funnel" at icon size - a cylinder of any proportion just reads as a can.
/// Built along +Z so it lines up with the models that point that way.
/// </summary>
public static class ConeMesh
{
    private const string MeshDir = "Assets/Meshes";

    // Same rule as the materials: an asset created twice at one path destroys the first copy
    // and leaves anything referencing it pointing at nothing, which draws as bright magenta.
    private static readonly Dictionary<string, Mesh> s_cache = new Dictionary<string, Mesh>();

    /// <summary>
    /// A part with a frustum mesh: <paramref name="frontRadius"/> at the far end (+Z),
    /// <paramref name="backRadius"/> at the near end, open ends capped.
    /// </summary>
    public static GameObject Object(string name, float backRadius, float frontRadius, float length)
    {
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = Get(name, backRadius, frontRadius, length);
        go.AddComponent<MeshRenderer>();
        return go;
    }

    private static Mesh Get(string name, float backRadius, float frontRadius, float length)
    {
        string path = MeshDir + "/Cone_" + name + ".asset";

        Mesh cached;
        if (s_cache.TryGetValue(path, out cached) && cached != null) return cached;

        Mesh mesh = Build(backRadius, frontRadius, length);
        mesh.name = "Cone_" + name;

        Directory.CreateDirectory(MeshDir);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);

        cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        s_cache[path] = cached;
        return cached;
    }

    private static Mesh Build(float backRadius, float frontRadius, float length)
    {
        const int sides = 24;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        // Side wall. The normal leans along the taper, otherwise the shading gives away that
        // this is a cylinder pretending to be a cone.
        float slope = (backRadius - frontRadius) / Mathf.Max(0.0001f, length);

        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), cy = Mathf.Sin(a);

            verts.Add(new Vector3(cx * backRadius, cy * backRadius, 0f));
            verts.Add(new Vector3(cx * frontRadius, cy * frontRadius, length));

            Vector3 n = new Vector3(cx, cy, slope).normalized;
            norms.Add(n);
            norms.Add(n);
        }

        for (int i = 0; i < sides; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        AddCap(verts, norms, tris, backRadius, 0f, Vector3.back, sides);
        AddCap(verts, norms, tris, frontRadius, length, Vector3.forward, sides);

        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddCap(List<Vector3> verts, List<Vector3> norms, List<int> tris,
                               float radius, float z, Vector3 normal, int sides)
    {
        int centre = verts.Count;
        verts.Add(new Vector3(0f, 0f, z));
        norms.Add(normal);

        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z));
            norms.Add(normal);
        }

        bool forward = normal.z > 0f;
        for (int i = 0; i < sides; i++)
        {
            int a = centre + 1 + i, b = centre + 2 + i;
            if (forward) { tris.Add(centre); tris.Add(a); tris.Add(b); }
            else { tris.Add(centre); tris.Add(b); tris.Add(a); }
        }
    }
}
