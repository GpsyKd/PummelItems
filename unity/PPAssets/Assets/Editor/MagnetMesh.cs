using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Horseshoe magnet built as a tube swept along an arc - the same technique as the banana,
/// but with a constant thickness and a wider sweep so the ends come back down and leave a
/// gap. Built from scratch rather than borrowed: nothing in the mod reuses the game's art.
/// </summary>
public static class MagnetMesh
{
    /// <summary>
    /// The arc, opening downward. <paramref name="sweep"/> is the total angle covered, so
    /// anything under 360 leaves the gap between the poles.
    /// </summary>
    public static Mesh Build(int segments = 40, int sides = 12,
                             float radius = 0.34f, float thickness = 0.095f, float sweep = 210f)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        float half = sweep * 0.5f * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(-half, half, t);

            // Centre of this ring, and the direction the tube is heading.
            Vector3 centre = new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, 0f);
            Vector3 fwd = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f).normalized;

            Vector3 side = Vector3.Cross(fwd, Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(side, fwd).normalized;

            for (int j = 0; j < sides; j++)
            {
                float ang = 2f * Mathf.PI * j / sides;
                Vector3 n = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)).normalized;
                verts.Add(centre + n * thickness);
                norms.Add(n);
            }
        }

        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j;
                int b = i * sides + (j + 1) % sides;
                int c = (i + 1) * sides + j;
                int d = (i + 1) * sides + (j + 1) % sides;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        // Caps, so the open ends are not see-through.
        AddCap(verts, norms, tris, 0, sides, true);
        AddCap(verts, norms, tris, segments * sides, sides, false);

        Mesh mesh = new Mesh();
        mesh.name = "MagnetMesh";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddCap(List<Vector3> verts, List<Vector3> norms, List<int> tris,
                               int ringStart, int sides, bool flip)
    {
        Vector3 centre = Vector3.zero;
        for (int j = 0; j < sides; j++) centre += verts[ringStart + j];
        centre /= sides;

        Vector3 normal = Vector3.Cross(verts[ringStart + 1] - verts[ringStart],
                                       verts[ringStart + 2] - verts[ringStart]).normalized;
        if (flip) normal = -normal;

        int centreIndex = verts.Count;
        verts.Add(centre);
        norms.Add(normal);

        for (int j = 0; j < sides; j++)
        {
            int a = ringStart + j;
            int b = ringStart + (j + 1) % sides;
            if (flip) { tris.Add(centreIndex); tris.Add(a); tris.Add(b); }
            else      { tris.Add(centreIndex); tris.Add(b); tris.Add(a); }
        }
    }

    /// <summary>Position and direction of one pole end, for placing the tips.</summary>
    public static void PoleEnd(bool left, out Vector3 position, out Quaternion rotation,
                               float radius = 0.34f, float sweep = 210f)
    {
        float a = (left ? -1f : 1f) * sweep * 0.5f * Mathf.Deg2Rad;
        position = new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, 0f);

        Vector3 fwd = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f).normalized;
        if (left) fwd = -fwd;

        // Cylinders point along their local Y, so line that up with the tube direction.
        rotation = Quaternion.FromToRotation(Vector3.up, fwd);
    }

    public static GameObject BuildPrefabRoot(string meshAssetPath)
    {
        Mesh mesh = Build();
        AssetDatabase.CreateAsset(mesh, meshAssetPath);

        GameObject root = new GameObject("LifeMagnet");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
        body.AddComponent<MeshRenderer>();

        return root;
    }
}
