using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a banana as a tube swept along a curved centreline, tapering to points at both
/// ends. No modelling package involved - the shape is described by two curves:
/// where the centre goes, and how thick it is along the way.
/// </summary>
public static class BananaMesh
{
    public static Mesh Build(int segments = 24, int sides = 10,
                             float length = 1.0f, float bend = 55f, float thickness = 0.13f)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        // Centreline: an arc, so the fruit curves the way a banana does.
        Vector3[] spine = new Vector3[segments + 1];
        Vector3[] tangents = new Vector3[segments + 1];
        float arc = bend * Mathf.Deg2Rad;
        float radius = length / arc;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = (t - 0.5f) * arc;
            spine[i] = new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius - radius, 0f);
            tangents[i] = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f).normalized;
        }

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            // Fat in the middle, pinched at both tips - the exponent keeps the belly
            // full instead of making it a lens shape.
            float r = thickness * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.45f);
            r = Mathf.Max(r, 0.004f);

            Vector3 fwd = tangents[i];
            Vector3 side = Vector3.Cross(fwd, Vector3.forward).normalized;
            Vector3 up = Vector3.Cross(side, fwd).normalized;

            for (int j = 0; j < sides; j++)
            {
                float ang = 2f * Mathf.PI * j / sides;
                // Slightly flattened cross-section reads better than a plain tube.
                Vector3 n = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang) * 0.82f).normalized;
                verts.Add(spine[i] + n * r);
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

        Mesh mesh = new Mesh();
        mesh.name = "BananaMesh";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Banana body plus the little brown stem, saved as a mesh asset.</summary>
    public static GameObject BuildPrefabRoot(string assetPath)
    {
        Mesh mesh = Build();
        AssetDatabase.CreateAsset(mesh, assetPath);

        GameObject root = new GameObject("Banana");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        body.AddComponent<MeshRenderer>();

        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(root.transform, false);
        stem.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
        stem.transform.localPosition = new Vector3(0.47f, 0.10f, 0f);
        stem.transform.localRotation = Quaternion.Euler(0f, 0f, -62f);

        return root;
    }
}
