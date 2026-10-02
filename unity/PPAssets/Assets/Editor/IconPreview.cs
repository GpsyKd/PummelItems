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
        Fill(sheet, Backdrop);

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

    // ------------------------------------------------------------------ the game's icon setup
    //
    // Replicates how the game itself renders an item icon, as measured in the game on
    // 2026-10-02 (AUDIT-RESULTS.md, section 6). A replica built this way matched the real
    // icons to within about 7/255 of mean brightness - close enough to judge a model by,
    // which the previous orthographic, extra-fill-light version was not: it flattered every
    // model, so the inventory looked worse than the preview.

    private const float GameFov = 60f;

    /// <summary>The background the icon tiles sit on in the README sheet.</summary>
    private static readonly Color Backdrop = new Color(0.204f, 0.216f, 0.247f, 1f);

    /// <summary>
    /// Which way the game looks at an icon: PreviewRendererCam puts its camera at
    /// center + normalize(offset) * distance and aims back, with offset defaulting to
    /// (-0.5, 0.6, 1). So the +Z face is the one that ends up on screen, seen from slightly
    /// above and to the left.
    /// </summary>
    private static readonly Vector3 GameCamOffset = new Vector3(-0.5f, 0.6f, 1f);

    private static Camera BuildCamera(GameObject rig)
    {
        GameObject go = new GameObject("Cam");
        go.transform.SetParent(rig.transform, false);

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = GameFov;
        cam.allowHDR = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Backdrop;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;

        // The PreviewRenderCamera prefab carries two directional lights of its own as children,
        // so they turn with the camera.
        AddLight(go.transform, "CamKey", Quaternion.Euler(45f, 45f, 0f), Color.white, 1f, local: true);
        AddLight(go.transform, "CamRim", Quaternion.Euler(315f, 225f, 0f), new Color(0.79f, 0.92f, 1f), 1f, local: true);
        return cam;
    }

    private static void BuildLights(GameObject rig)
    {
        // What ItemRegistry.RenderWithFill sets for the duration of each icon render: flat
        // ambient and no shadows. The menu scene's own ambient is near zero, which is what
        // made the sides of every model sink into black.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);
        QualitySettings.shadows = ShadowQuality.Disable;

        // PummelTargetRenderer's own TempDirectionalLight, in world space.
        AddLight(rig.transform, "TempDirectionalLight", Quaternion.Euler(45f, -45f, 6.748f),
                 new Color32(255, 244, 214, 255), 0.75f, local: false);
    }

    private static void AddLight(Transform parent, string name, Quaternion rotation, Color color,
                                 float intensity, bool local)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (local) go.transform.localRotation = rotation;
        else go.transform.rotation = rotation;

        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    /// <summary>
    /// Puts the camera where PreviewRendererCam.Focus puts it - the same direction, and the
    /// distance at which the bounds' sphere fits the field of view with the game's 5% margin.
    /// Also strips glow from the instance's materials (copies, never the assets): the game has
    /// no shader variant that draws emission, so showing it here would preview something no
    /// player will ever see.
    /// </summary>
    private static bool Frame(Camera cam, GameObject instance)
    {
        Renderer[] rs = instance.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        for (int i = 0; i < rs.Length; i++)
        {
            Material[] mats = rs[i].sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                Material copy = new Material(mats[m]);
                copy.DisableKeyword("_EMISSION");
                if (copy.HasProperty("_EmissionColor")) copy.SetColor("_EmissionColor", Color.black);
                mats[m] = copy;
            }
            rs[i].sharedMaterials = mats;
        }

        float distance = b.size.magnitude / 2f * 1.05f / Mathf.Sin(Mathf.Deg2Rad * GameFov / 2f);
        if (distance <= 0.0001f) distance = 1f;

        cam.transform.position = b.center + GameCamOffset.normalized * distance;
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
