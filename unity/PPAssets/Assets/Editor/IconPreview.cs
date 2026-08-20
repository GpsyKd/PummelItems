using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders every icon prefab into one contact sheet so the whole set can be judged side by
/// side, without launching the game and waiting for it to cache them.
///
/// This is a review aid, not the real thing: the game frames icons with its own renderer, so
/// treat the sheet as a check on silhouette and colour rather than on exact framing. What it
/// catches - shapes that collapse into a blob at small size, parts that read as one mass,
/// models pointed straight at the camera - is exactly what goes wrong with these.
/// </summary>
public static class IconPreview
{
    private const int Cell = 170;
    private const int Cols = 6;
    private const int Pad = 2;

    /// <summary>
    /// Same sheet, but only the icons named in zoom.txt and four times the size. Small
    /// details - a pin, an eye socket, pips - cannot be judged in a 170-pixel cell, and
    /// deciding they are fine there is how they end up invisible in the game.
    /// </summary>
    public static void RenderZoom()
    {
        string list = Path.Combine(Directory.GetCurrentDirectory(), "zoom.txt");
        if (!File.Exists(list)) { Debug.Log("[PCI] zoom.txt not found"); return; }

        HashSet<string> wanted = new HashSet<string>();
        foreach (string line in File.ReadAllLines(list))
        {
            string t = line.Trim();
            if (t.Length > 0) wanted.Add(t);
        }

        Render(wanted, 380, 3, "IconZoom.png");
    }

    public static void RenderSheet()
    {
        Render(null, Cell, Cols, "IconSheet.png");
    }

    private static void Render(HashSet<string> only, int cell, int cols, string fileName)
    {
        List<string> all = IconPrefabPaths();
        List<string> paths = new List<string>();

        for (int i = 0; i < all.Count; i++)
        {
            if (only == null || only.Contains(Path.GetFileNameWithoutExtension(all[i])))
                paths.Add(all[i]);
        }
        if (paths.Count == 0) { Debug.Log("[PCI] no icon prefabs matched"); return; }

        RenderGrid(paths, cell, cols, fileName);
    }

    private static void RenderGrid(List<string> paths, int Cell, int Cols, string fileName)
    {
        int rows = Mathf.CeilToInt(paths.Count / (float)Cols);
        Texture2D sheet = new Texture2D(Cols * Cell, rows * Cell, TextureFormat.RGBA32, false);
        Fill(sheet, new Color(0.62f, 0.64f, 0.66f, 1f));

        GameObject rig = new GameObject("PCI_PreviewRig");
        Camera cam = BuildCamera(rig);
        BuildLights(rig);

        RenderTexture rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        Texture2D cell = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);

        for (int i = 0; i < paths.Count; i++)
        {
            string name = Path.GetFileNameWithoutExtension(paths[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null) continue;

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                if (!Frame(cam, instance)) { Debug.Log("[PCI] icon has no renderers: " + name); continue; }

                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                cell.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                cell.Apply();
                RenderTexture.active = prev;

                int col = i % Cols;
                int row = i / Cols;
                // Sheet origin is bottom-left; laying rows out top-down keeps the printed
                // order and the picture in agreement.
                Blit(sheet, cell, col * Cell, (rows - 1 - row) * Cell);

                Debug.Log(string.Format("[PCI] sheet r{0}c{1} = {2}", row + 1, col + 1, name));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(rig);

        string outPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        Debug.Log("[PCI] icon sheet written: " + outPath + "  (" + paths.Count + " icons, " +
                  Cols + " per row)");
    }

    private static List<string> IconPrefabPaths()
    {
        List<string> paths = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (p.EndsWith("Icon.prefab")) paths.Add(p);
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static Camera BuildCamera(GameObject rig)
    {
        GameObject go = new GameObject("Cam");
        go.transform.SetParent(rig.transform, false);

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.64f, 0.66f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;
        return cam;
    }

    /// <summary>
    /// Which way the game looks at an icon: PreviewRendererCam puts its camera at
    /// center + normalize(offset) * distance and aims back, with offset defaulting to
    /// (-0.5, 0.6, 1). So the +Z face is the one that ends up on screen, seen from slightly
    /// above and to the left - and a preview lit and aimed any other way is reviewing a side
    /// of the model the player never sees.
    /// </summary>
    private static readonly Vector3 GameCamOffset = new Vector3(-0.5f, 0.6f, 1f);

    private static void BuildLights(GameObject rig)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.46f);

        // The angle and tint the game uses for its own icon renders.
        GameObject key = new GameObject("Key");
        key.transform.SetParent(rig.transform, false);
        key.transform.rotation = Quaternion.Euler(45f, -45f, 6.748f);
        Light kl = key.AddComponent<Light>();
        kl.type = LightType.Directional;
        kl.intensity = 0.95f;
        kl.color = new Color32(255, 244, 214, 255);
        kl.shadows = LightShadows.None;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(rig.transform, false);
        fill.transform.rotation = Quaternion.Euler(-10f, 150f, 0f);
        Light fl = fill.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.intensity = 0.35f;
        fl.shadows = LightShadows.None;
    }

    /// <summary>Puts the camera where the game puts it, and sizes it to fit.</summary>
    private static bool Frame(Camera cam, GameObject instance)
    {
        Renderer[] rs = instance.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        // Bounds are measured along the world axes; the view is at an angle to all three, so
        // the enclosing sphere is what has to fit rather than any one extent.
        float radius = b.extents.magnitude;
        if (radius <= 0.0001f) radius = 0.5f;

        cam.orthographicSize = radius * 1.05f;
        cam.transform.position = b.center + GameCamOffset.normalized * (radius + 20f);
        cam.transform.rotation = Quaternion.LookRotation((b.center - cam.transform.position).normalized);
        return true;
    }

    private static void Fill(Texture2D tex, Color c)
    {
        Color[] px = new Color[tex.width * tex.height];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
    }

    private static void Blit(Texture2D dst, Texture2D src, int x, int y)
    {
        int w = src.width - Pad * 2;
        int h = src.height - Pad * 2;
        dst.SetPixels(x + Pad, y + Pad, w, h, src.GetPixels(Pad, Pad, w, h));
        dst.Apply();
    }
}
