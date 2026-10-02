using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the mod's AssetBundle from the command line:
///
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod BundleBuilder.All
///
/// Output lands in &lt;proj&gt;/BuiltBundles/pciassets and is copied next to the mod DLL.
/// </summary>
public static class BundleBuilder
{
    private const string BundleName = "pciassets";
    private const string PrefabDir = "Assets/Prefabs";

    public static void All()
    {
        CleanGenerated();
        ShapeKit.ResetNames();
        Directory.CreateDirectory(PrefabDir);
        MakeShuffleOrb();
        MakeLoadedDie();
        MakeBanana();
        MakeGroupA();
        SoundBuilder.BuildAll(BundleName);
        Build();
    }

    /// <summary>The thrown-projectile family: grenade, shard, sticky, icicle, pellet, boomerang.</summary>
    private static void MakeGroupA()
    {
        // From the spoon's side and a little in front, where the ring pin hangs.
        BuildOne(GroupAModels.Grenade(Paint),   "Grenade", new Vector3(1f, 0.40f, 0.55f));
        BuildOne(GroupAModels.Shard(Paint),     "Shard");
        BuildOne(GroupAModels.Sticky(Paint),    "Sticky");
        BuildOne(GroupAModels.Icicle(Paint),    "Icicle", new Vector3(1f, 0.22f, 0.30f));
        BuildOne(GroupAModels.Pellet(Paint),    "Pellet");
        BuildOne(GroupAModels.Boomerang(Paint), "Boomerang", new Vector3(0.18f, 1f, 0.30f), Vector3.forward);

        BuildOne(GroupAModels.SwapBag(Paint),   "SwapBag", new Vector3(0.25f, 0.20f, 1f));
        BuildOne(GroupAModels.Copier(Paint),    "Copier", new Vector3(-0.5f, 0.65f, 1f));   // front left: the tray and the copy on it
        BuildOne(GroupAModels.Junk(Paint),      "Junk", new Vector3(0.45f, 0.75f, 1f));
        BuildOne(GroupAModels.Tax(Paint),       "Tax", new Vector3(0.15f, 1f, -0.55f));   // read from -z, top of the page up

        BuildOne(GroupAModels.GlassCannon(Paint), "GlassCannon", new Vector3(1f, 0.30f, 0.40f));
        BuildOne(GroupAModels.Pinata(Paint),      "Pinata", new Vector3(0.45f, 0.22f, 1f));
        BuildOne(GroupAModels.Generosity(Paint),  "Generosity");

        BuildOne(GroupAModels.DeathWand(Paint), "DeathWand", new Vector3(0.15f, 0.10f, 1f));
        BuildOne(GroupAModels.Meteor(Paint),    "MeteorRock", new Vector3(1f, 0.35f, 0.10f));
        BuildOne(GroupAModels.Vacuum(Paint),    "Vacuum", new Vector3(0.45f, 0.5f, -1f));   // drum on the left, keys flying in on the right
        BuildOne(GroupAModels.Piggy(Paint),     "Piggy", new Vector3(0.55f, 0.30f, 1f));   // three-quarter, face towards the camera

        BuildOne(GroupAModels.Curse(Paint),      "Curse", new Vector3(0.20f, 0.15f, 1f));
        BuildOne(GroupAModels.Freeze(Paint),     "Freeze", new Vector3(0.28f, 0.40f, 1f));   // from the side: from above the crystals are a star
        BuildOne(GroupAModels.DoubleDice(Paint), "DoubleDice");
        BuildOne(GroupAModels.Grapple(Paint),    "Grapple", new Vector3(0.55f, 0.25f, 1f));
        BuildOne(GroupAModels.Boot(Paint),       "Boot", new Vector3(0.20f, 0.18f, 1f));
        BuildOne(GroupAModels.Ticket(Paint),     "Ticket");

        BuildOne(GroupAModels.Mine(Paint),     "Mine", new Vector3(0.40f, 0.70f, 0.60f));
        BuildOne(GroupAModels.Poison(Paint),   "Poison");
        BuildOne(GroupAModels.Signpost(Paint), "Signpost");

        BuildOne(GroupAModels.LifeMagnet(Paint), "LifeMagnet");
        BuildOne(GroupAModels.Ricochet(Paint),  "Ricochet", new Vector3(0.12f, 0.18f, -1f));   // lock side, muzzle to the right
    }

    /// <summary>
    /// Where the game's icon camera ends up relative to whatever it is pointed at.
    /// PreviewRendererCam places itself at center + normalize(offset) * distance and looks
    /// back, with offset = (-0.5, 0.6, 1). Everything about icon framing follows from this
    /// one vector, and picking bake angles without it is guesswork - which is exactly how the
    /// first set ended up showing the back of half the models.
    /// </summary>
    private static readonly Vector3 GameCamDir = new Vector3(-0.5f, 0.6f, 1f).normalized;

    /// <param name="faceDir">
    /// The direction, in the model's own space, that the camera should look from - so
    /// (0, 0, 1) means "show me its front", (1, 0, 0) means "show me its right side". The
    /// bake rotation needed to put that side in front of the game's camera is worked out
    /// from here rather than dialled in by hand.
    /// </param>
    private static void BuildOne(GameObject root, string name, Vector3 faceDir)
    {
        BuildOne(root, name, faceDir, Vector3.up);
    }

    /// <param name="upHint">
    /// Which way up the model should read on screen. Only matters for something lying flat:
    /// looking almost straight down at it leaves world-up with no useful projection, and the
    /// picture ends up rolled to some arbitrary angle - which is how the ticket and the tax
    /// page came out as diamonds.
    /// </param>
    private static void BuildOne(GameObject root, string name, Vector3 faceDir, Vector3 upHint)
    {
        Soften(root, name);
        StripColliders(root);
        SaveIconVariant(root, name, BakeRotation(faceDir, upHint));
        SavePrefab(root, name);
    }

    /// <summary>Same, but with the viewing side worked out from the model's proportions.</summary>
    private static void BuildOne(GameObject root, string name)
    {
        Soften(root, name);
        Vector3 up;
        Vector3 dir = AutoFaceDir(root, out up);

        StripColliders(root);
        SaveIconVariant(root, name, BakeRotation(dir, up));
        SavePrefab(root, name);
    }

    /// <summary>
    /// Picks a viewing side from the shape itself: look down the model's thinnest axis, since
    /// that is the direction with the most to see, then tilt off it so the result is a
    /// three-quarter view rather than a flat elevation drawing.
    ///
    /// Good enough for compact and flat objects. Anything long and thin needs telling - the
    /// thinnest axis of a funnel is across it, and a funnel viewed end-on is a circle.
    /// </summary>
    private static Vector3 AutoFaceDir(GameObject root, out Vector3 upHint)
    {
        upHint = Vector3.up;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) return new Vector3(0.3f, 0.35f, 1f);

        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
        Bounds b = new Bounds();
        bool first = true;

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null) continue;

            Matrix4x4 m = toRoot * filters[i].transform.localToWorldMatrix;
            Bounds mb = filters[i].sharedMesh.bounds;
            Vector3 c = m.MultiplyPoint3x4(mb.center);
            Vector3 e = mb.extents;

            // Extents under an arbitrary rotation: sum the absolute axis contributions.
            Vector3 ext = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);

            Bounds part = new Bounds(c, ext * 2f);
            if (first) { b = part; first = false; } else b.Encapsulate(part);
        }

        Vector3 x = b.extents;
        int thin = (x.x <= x.y && x.x <= x.z) ? 0 : ((x.y <= x.z) ? 1 : 2);

        Vector3 dir = Vector3.zero;
        dir[thin] = 1f;
        for (int i = 0; i < 3; i++)
        {
            if (i == thin) continue;
            dir[i] += (i == 1) ? 0.36f : 0.28f;   // a touch more lift than sideways turn
        }

        // Standing objects keep world up. Something lying flat has no meaningful world up in
        // view, so the shorter of its two in-plane axes goes up and the longer lies across -
        // which is the way a card or a page wants to be read.
        if (thin == 1)
            upHint = (x.x <= x.z) ? Vector3.right : Vector3.forward;

        return dir;
    }

    private static Quaternion BakeRotation(Vector3 faceDir)
    {
        return BakeRotation(faceDir, Vector3.up);
    }

    private static Quaternion BakeRotation(Vector3 faceDir, Vector3 upHint)
    {
        faceDir = faceDir.normalized;
        upHint = upHint.normalized;

        // LookRotation needs an up vector that is not parallel to what it is looking along.
        if (Mathf.Abs(Vector3.Dot(faceDir, upHint)) > 0.9f)
            upHint = (Mathf.Abs(faceDir.z) < 0.9f) ? Vector3.forward : Vector3.right;

        // Turn the chosen side to face the camera, and keep the model the right way up.
        return Quaternion.LookRotation(GameCamDir, Vector3.up) *
               Quaternion.Inverse(Quaternion.LookRotation(faceDir, upHint));
    }

    [MenuItem("PCI/Rebuild Everything")]
    public static void RebuildFromMenu()
    {
        All();
    }

    // --------------------------------------------------------------------- Banana

    private static void MakeBanana()
    {
        Directory.CreateDirectory("Assets/Meshes");
        GameObject root = BananaMesh.BuildPrefabRoot("Assets/Meshes/Banana.asset");

        Paint(root.transform.Find("Body").gameObject,
              new Color(0.97f, 0.85f, 0.20f), emissive: false, matName: "Banana_Body");
        Paint(root.transform.Find("Stem").gameObject,
              new Color(0.35f, 0.25f, 0.10f), emissive: false, matName: "Banana_Stem");
        Paint(root.transform.Find("Tip").gameObject,
              new Color(0.24f, 0.17f, 0.08f), emissive: false, matName: "Banana_Tip");

        Soften(root, "Banana");
        StripColliders(root);
        SaveIconVariant(root, "Banana", BakeRotation(new Vector3(0.20f, 0.28f, 1f)));
        SavePrefab(root, "Banana");
    }

    // ------------------------------------------------------------------ Loaded Die

    /// <summary>
    /// A real die: rounded edges and corners, flat pips set into the faces, opposite faces
    /// adding up to seven. It used to be a razor-edged cube studded with 21 red half-balls -
    /// 16 thousand triangles that still read as rivets. "Loaded" means the next roll is the
    /// maximum, so the six is on top and its pips glow gold.
    /// </summary>
    private static void MakeLoadedDie()
    {
        GameObject root = new GameObject("LoadedDie");

        Mesh body = ShapeKit.Save(ShapeKit.RoundedBox(Vector3.one * 0.5f, 0.07f, 4), "LoadedDie_Body");
        Paint(ShapeKit.Part(root, "Body", body, Vector3.zero, Quaternion.identity),
              new Color(0.95f, 0.93f, 0.87f), emissive: false, matName: "Die_Body");

        Mesh pip = ShapeKit.Save(ShapeKit.BevelCylinder(0.042f, 0.042f, 0.012f, 0.004f, 24), "LoadedDie_Pip");
        Mesh one = ShapeKit.Save(ShapeKit.BevelCylinder(0.06f, 0.06f, 0.012f, 0.004f, 28), "LoadedDie_PipOne");

        AddPips(root, 6, Vector3.up,      Vector3.right,   Vector3.forward, pip, new Color(0.98f, 0.76f, 0.26f), true,  "Die_PipGold");
        AddPips(root, 1, Vector3.down,    Vector3.right,   Vector3.forward, one, new Color(0.78f, 0.10f, 0.10f), false, "Die_PipRed");
        AddPips(root, 2, Vector3.forward, Vector3.right,   Vector3.up,      pip, new Color(0.12f, 0.12f, 0.14f), false, "Die_PipDark");
        AddPips(root, 5, Vector3.back,    Vector3.right,   Vector3.up,      pip, new Color(0.12f, 0.12f, 0.14f), false, "Die_PipDark");
        AddPips(root, 3, Vector3.right,   Vector3.forward, Vector3.up,      pip, new Color(0.12f, 0.12f, 0.14f), false, "Die_PipDark");
        AddPips(root, 4, Vector3.left,    Vector3.forward, Vector3.up,      pip, new Color(0.12f, 0.12f, 0.14f), false, "Die_PipDark");

        StripColliders(root);
        SaveIconVariant(root, "LoadedDie", BakeRotation(new Vector3(0.50f, 0.40f, 1f)));
        SavePrefab(root, "LoadedDie");
    }

    /// <summary>Flat pips on one face, given its normal and two in-face axes.</summary>
    private static void AddPips(GameObject root, int count, Vector3 normal, Vector3 u, Vector3 v,
                                Mesh pipMesh, Color colour, bool glow, string matName)
    {
        const float half = 0.25f;   // body is 0.5 across, so the face sits at 0.25
        const float o = 0.125f;     // pip offset from centre, inside the flat part of the face
        Vector2[] spots = PipLayout(count, o);
        Quaternion facing = Quaternion.FromToRotation(Vector3.up, normal);

        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 at = normal * (half - 0.002f) + u * spots[i].x + v * spots[i].y;
            Paint(ShapeKit.Part(root, "Pip" + count + "_" + i, pipMesh, at, facing), colour, glow, matName);
        }
    }

    private static Vector2[] PipLayout(int count, float o)
    {
        switch (count)
        {
            case 1: return new[] { Vector2.zero };
            case 2: return new[] { new Vector2(-o, -o), new Vector2(o, o) };
            case 3: return new[] { new Vector2(-o, -o), Vector2.zero, new Vector2(o, o) };
            case 4: return new[] { new Vector2(-o, -o), new Vector2(-o, o), new Vector2(o, -o), new Vector2(o, o) };
            case 5: return new[] { new Vector2(-o, -o), new Vector2(-o, o), Vector2.zero, new Vector2(o, -o), new Vector2(o, o) };
            default: return new[] { new Vector2(-o, -o), new Vector2(-o, 0f), new Vector2(-o, o),
                                    new Vector2(o, -o),  new Vector2(o, 0f),  new Vector2(o, o) };
        }
    }

    // ---------------------------------------------------------------- Shuffle Orb

    private static void MakeShuffleOrb()
    {
        Directory.CreateDirectory(PrefabDir);

        // A deliberately non-default shape, so "the bundle loaded" is unmistakable
        // in game: a violet core with two crossed rings.
        GameObject root = new GameObject("ShuffleOrb");

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.55f;
        Paint(core, new Color(0.45f, 0.20f, 0.90f), emissive: true, matName: "Orb_Core");

        // Real rings. They used to be flattened cylinders - solid discs - so from above the
        // orb read as a plate with a ball in it.
        Mesh ring = ShapeKit.Save(ShapeKit.Torus(0.43f, 0.022f, 96, 14), "ShuffleOrb_Ring");
        Paint(ShapeKit.Part(root, "RingA", ring, Vector3.zero, Quaternion.identity),
              new Color(0.95f, 0.80f, 0.15f), emissive: false, matName: "Orb_Ring");
        Paint(ShapeKit.Part(root, "RingB", ring, Vector3.zero, Quaternion.Euler(0f, 0f, 90f)),
              new Color(0.95f, 0.80f, 0.15f), emissive: false, matName: "Orb_Ring");

        // Four beads in player colours riding the rings: the item swaps everybody's places.
        float d = 0.43f * Mathf.Sqrt(0.5f);
        Vector3[] at = { new Vector3(d, 0f, d), new Vector3(-d, 0f, -d), new Vector3(0f, d, -d), new Vector3(0f, -d, d) };
        Color[] colours = { new Color(0.90f, 0.28f, 0.26f), new Color(0.28f, 0.56f, 0.95f),
                            new Color(0.32f, 0.78f, 0.38f), new Color(0.97f, 0.80f, 0.24f) };
        for (int i = 0; i < at.Length; i++)
        {
            GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bead.name = "Bead" + i;
            bead.transform.SetParent(root.transform, false);
            bead.transform.localScale = Vector3.one * 0.09f;
            bead.transform.localPosition = at[i];
            Paint(bead, colours[i], emissive: false, matName: "Orb_Bead" + i);
        }

        StripColliders(root);
        SaveIconVariant(root, "ShuffleOrb", BakeRotation(new Vector3(0.35f, 0.30f, 1f)));
        SavePrefab(root, "ShuffleOrb");
    }

    /// <summary>
    /// Builds the icon stand-in for a model: every child mesh baked into ONE mesh, with a
    /// chosen viewing angle frozen into the vertices.
    ///
    /// PummelTargetRenderer draws meshes at Matrix4x4.TRS(pos, identity, one) - transforms
    /// are ignored - and re-aims the camera once per MeshFilter while rendering only once.
    /// A multi-part model therefore ends up framed on whichever part happens to be last,
    /// which is how a banana became a yellow barrel. One mesh, one framing, no surprises.
    /// </summary>
    private static void SaveIconVariant(GameObject root, string name, Quaternion viewRotation)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) return;

        Matrix4x4 view = Matrix4x4.TRS(Vector3.zero, viewRotation, Vector3.one);
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

        CombineInstance[] parts = new CombineInstance[filters.Length];
        Material[] mats = new Material[filters.Length];

        for (int i = 0; i < filters.Length; i++)
        {
            parts[i].mesh = filters[i].sharedMesh;
            parts[i].transform = view * toRoot * filters[i].transform.localToWorldMatrix;

            MeshRenderer mr = filters[i].GetComponent<MeshRenderer>();
            mats[i] = (mr != null) ? mr.sharedMaterial : null;
        }

        Mesh combined = new Mesh();
        combined.name = name + "IconMesh";
        // mergeSubMeshes:false keeps one submesh per part, so each keeps its own material.
        combined.CombineMeshes(parts, false, true);
        combined.RecalculateBounds();

        Directory.CreateDirectory("Assets/Meshes");
        string meshPath = "Assets/Meshes/" + name + "Icon.asset";
        AssetDatabase.CreateAsset(combined, meshPath);

        GameObject iconRoot = new GameObject(name + "Icon");
        iconRoot.AddComponent<MeshFilter>().sharedMesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        iconRoot.AddComponent<MeshRenderer>().sharedMaterials = mats;

        string path = PrefabDir + "/" + name + "Icon.prefab";
        PrefabUtility.SaveAsPrefabAsset(iconRoot, path);
        Object.DestroyImmediate(iconRoot);

        AssetImporter imp = AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            imp.assetBundleName = BundleName;
            imp.SaveAndReimport();
        }
        Debug.Log("[PCI] icon model built: " + path + " (" + filters.Length + " parts merged)");
    }

    private static void SavePrefab(GameObject root, string name)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.assetBundleName = BundleName;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PCI] model built: " + path);
    }

    /// <summary>
    /// Swaps every plain Unity cube and cylinder in a model for the same shape at the same
    /// size with its edges rounded. Razor-sharp primitive edges are the biggest single tell of
    /// programmer art; a rounded edge catches a highlight and the part reads as a made thing.
    ///
    /// The part's scale is baked into its new mesh - a bevel on a unit cube would be squashed
    /// along with it on a thin card - and handed down to its children so nothing moves. That
    /// hand-down is exact for children that are not rotated or whose parent scales evenly,
    /// which is every case in this set.
    /// </summary>
    private static void Soften(GameObject root, string model)
    {
        int index = 0;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh src = mf.sharedMesh;
            if (src == null) continue;
            bool cube = src.name == "Cube", cylinder = src.name == "Cylinder";
            if (!cube && !cylinder) continue;

            Transform t = mf.transform;
            Vector3 s = t.localScale;
            Mesh soft;
            if (cube)
            {
                float thinnest = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                soft = ShapeKit.RoundedBox(s, Mathf.Clamp(thinnest * 0.2f, 0.0015f, 0.03f), 3);
            }
            else
            {
                float rx = 0.5f * s.x, rz = 0.5f * s.z, height = 2f * s.y;
                float bevel = Mathf.Clamp(0.25f * Mathf.Min(Mathf.Min(rx, rz), height * 0.5f), 0.001f, 0.02f);
                soft = ShapeKit.BevelCylinder(rx, rz, height, bevel, 28);
            }

            mf.sharedMesh = ShapeKit.Save(soft, "Soft_" + model + "_" + (index++) + "_" + t.name);
            t.localScale = Vector3.one;
            foreach (Transform child in t)
            {
                child.localPosition = Vector3.Scale(s, child.localPosition);
                child.localScale = Vector3.Scale(s, child.localScale);
            }
        }
    }

    // One material asset per name, created once and handed out afterwards.
    //
    // CreateAsset on a path that is already taken DESTROYS the object living there and puts
    // a new one in its place. Any renderer already pointing at the old one is left holding a
    // dead reference, which serialises into the bundle as null - and a null material is what
    // Unity draws in bright magenta. Parts built in a loop (mine prongs, dice pips, grenade
    // bands) all share one name, so every copy but the last used to come out magenta.
    private static readonly Dictionary<string, Material> s_materials = new Dictionary<string, Material>();

    // What each name was first asked for. The cache above hands out one material per name,
    // so a second request with a different colour used to get the first one without a word -
    // the boot's darker foot, the skull's jaw, the grenade's lever and two more were lost that
    // way. A clash is now an error in the build log instead of a silent substitution.
    private static readonly Dictionary<string, KeyValuePair<Color, bool>> s_requests =
        new Dictionary<string, KeyValuePair<Color, bool>>();

    private static void Paint(GameObject go, Color c, bool emissive, string matName)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = MaterialNamed(matName, c, emissive);
    }

    private static Material MaterialNamed(string matName, Color c, bool emissive)
    {
        Material shared;
        if (s_materials.TryGetValue(matName, out shared) && shared != null)
        {
            KeyValuePair<Color, bool> first;
            if (s_requests.TryGetValue(matName, out first) && (first.Key != c || first.Value != emissive))
            {
                Debug.LogError("[PCI] material name clash: '" + matName + "' was made as " + first.Key +
                               (first.Value ? " (glowing)" : "") + " and is now asked for as " + c +
                               (emissive ? " (glowing)" : "") + " - give the second one its own name");
            }
            return shared;
        }

        // Built-in render pipeline - the game ships no URP/HDRP assemblies.
        MaterialStyle style = MaterialStyle.For(matName);
        if (!MaterialStyle.IsKnown(matName))
            Debug.LogWarning("[PCI] material '" + matName + "' has no entry in MaterialStyles - default plastic");

        Material m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.SetFloat("_Glossiness", style.Smoothness);
        m.SetFloat("_Metallic", style.Metallic);
        if (emissive && style.Emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * (0.6f * style.Emission));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        s_requests[matName] = new KeyValuePair<Color, bool>(c, emissive);

        Directory.CreateDirectory("Assets/Materials");
        string matPath = "Assets/Materials/" + matName + ".mat";
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(m, matPath);

        shared = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        s_materials[matName] = shared;
        return shared;
    }

    /// <summary>
    /// Starts every build from empty generated folders. Assets left over from models that
    /// were renamed or removed used to stay behind - a stale prefab still carries the bundle
    /// name and would ship - and nothing showed which files a build actually made.
    /// </summary>
    private static void CleanGenerated()
    {
        string[] dirs = { "Assets/Materials", "Assets/Meshes", PrefabDir };
        int removed = 0;

        foreach (string dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p)) continue;
                if (AssetDatabase.DeleteAsset(p)) removed++;
            }
        }

        s_materials.Clear();
        s_requests.Clear();
        AssetDatabase.Refresh();
        Debug.Log("[PCI] removed " + removed + " generated asset(s) from the previous build");
    }

    private static void StripColliders(GameObject root)
    {
        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) Object.DestroyImmediate(cols[i]);
    }

    public static void Build()
    {
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "BuiltBundles");
        Directory.CreateDirectory(outDir);

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            outDir,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        if (manifest == null)
        {
            Debug.LogError("[PCI] BuildAssetBundles returned null - build FAILED");
            EditorApplication.Exit(1);
            return;
        }

        foreach (string b in manifest.GetAllAssetBundles())
            Debug.Log("[PCI] built bundle: " + b);

        Debug.Log("[PCI] bundles written to " + outDir);
    }
}
