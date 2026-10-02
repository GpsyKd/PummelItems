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

            // Fat in the middle, pinched at the flower end - the exponent keeps the belly
            // full instead of making it a lens shape. The stalk end does not come to a point:
            // a banana narrows into its stalk and stops square, which is half of what makes
            // it read as a banana rather than a boat.
            float r = thickness * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.45f);
            if (t > 0.5f) r = Mathf.Max(r, thickness * 0.30f * Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.45f));
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

        // Close the square stalk end with a slightly domed cap.
        {
            int ring = segments * sides;
            Vector3 fwd = tangents[segments];
            float rEnd = (verts[ring] - spine[segments]).magnitude;
            int centre = verts.Count;
            verts.Add(spine[segments] + fwd * rEnd * 0.25f);
            norms.Add(fwd);
            for (int j = 0; j < sides; j++)
            {
                int a = ring + j, b = ring + (j + 1) % sides;
                tris.Add(centre); tris.Add(b); tris.Add(a);
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
        const float length = 1.0f, bend = 55f;   // the stem and the tip below use the same arc

        // Five sides: a banana is ridged, not round, and with smooth normals a pentagon reads
        // as a soft ridged fruit where ten sides read as a sausage.
        Mesh mesh = Build(segments: 32, sides: 5, length: length, bend: bend);
        AssetDatabase.CreateAsset(mesh, assetPath);

        GameObject root = new GameObject("Banana");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        body.AddComponent<MeshRenderer>();

        // The stem sits on the end of the centreline and carries on along it. It used to be
        // placed by hand at (0.47, 0.10) pointing up-right, 6 cm above the tip it belongs to
        // and angled the other way; deriving it from the same arc as the body cannot drift.
        float arc = bend * Mathf.Deg2Rad;
        float radius = length / arc;
        float a = 0.5f * arc;
        Vector3 tip = new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius - radius, 0f);
        Vector3 along = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);

        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(root.transform, false);
        stem.transform.localScale = new Vector3(0.045f, 0.05f, 0.045f);
        stem.transform.localPosition = tip + along * 0.035f;
        stem.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along);

        // The dark flower tip at the other end.
        Vector3 flower = new Vector3(-Mathf.Sin(a) * radius, Mathf.Cos(a) * radius - radius, 0f);
        Vector3 back = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);   // tangent at the flower end, into the fruit
        GameObject end = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        end.name = "Tip";
        end.transform.SetParent(root.transform, false);
        end.transform.localScale = new Vector3(0.028f, 0.028f, 0.028f);
        end.transform.localPosition = flower + back * 0.006f;

        return root;
    }
}
