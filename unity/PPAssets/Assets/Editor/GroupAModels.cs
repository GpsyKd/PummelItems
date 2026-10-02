using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Models for the item set. Simple, strongly coloured shapes that read at board-camera
/// distance; the soft and curved parts come from ShapeKit, and every plain cube or cylinder
/// left here gets its edges rounded by BundleBuilder.Soften on the way into the bundle.
/// </summary>
public static class GroupAModels
{
    public delegate void PaintFn(GameObject go, Color c, bool emissive, string matName);

    private static GameObject Solid(GameObject root, string name, Mesh mesh, Vector3 position, Quaternion rotation,
                                    PaintFn paint, Color colour, bool glow, string matName)
    {
        GameObject go = ShapeKit.Part(root, name, mesh, position, rotation);
        paint(go, colour, glow, matName);
        return go;
    }

    /// <summary>
    /// Pineapple grenade: a turned body cut into segments by a grid of grooves, the fuse, the
    /// spoon running down its side and the ring pin. It used to be an ellipsoid with three
    /// discs sticking out of it - a stack of tyres - and had no ring, the one detail everybody
    /// draws a grenade with.
    /// </summary>
    public static GameObject Grenade(PaintFn paint)
    {
        GameObject root = new GameObject("Grenade");

        Vector2[] profile =
        {
            new Vector2(0f, -0.215f), new Vector2(0.05f, -0.212f), new Vector2(0.10f, -0.195f), new Vector2(0.14f, -0.16f),
            new Vector2(0.165f, -0.10f), new Vector2(0.176f, -0.03f), new Vector2(0.174f, 0.04f), new Vector2(0.160f, 0.10f),
            new Vector2(0.132f, 0.152f), new Vector2(0.092f, 0.187f), new Vector2(0.055f, 0.202f), new Vector2(0f, 0.205f),
        };
        Solid(root, "Body", ShapeKit.Save(ShapeKit.Lathe(profile, 40), "Grenade_Body"), Vector3.zero, Quaternion.identity,
              paint, new Color(0.30f, 0.38f, 0.21f), false, "Gren_Body");

        // The segments: rings round it and ribs down it, both a shade darker.
        Color groove = new Color(0.14f, 0.18f, 0.11f);
        float[] heights = { -0.13f, -0.06f, 0.01f, 0.08f, 0.14f };
        for (int i = 0; i < heights.Length; i++)
        {
            Mesh ring = ShapeKit.Save(ShapeKit.Torus(RadiusAt(profile, heights[i]) * 1.004f, 0.0055f, 64, 8), "Grenade_Ring" + i);
            Solid(root, "Groove" + i, ring, new Vector3(0f, heights[i], 0f), Quaternion.identity, paint, groove, false, "Gren_Band");
        }
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            List<Vector3> pts = new List<Vector3>();
            for (int p = 1; p < profile.Length - 1; p++) pts.Add(dir * (profile[p].x * 1.006f) + Vector3.up * profile[p].y);
            Mesh rib = ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Smooth(pts, 4), t => 0.005f * ShapeKit.Capped(t, 0.02f),
                                                    Vector3.Cross(dir, Vector3.up), 1f, 1f, 6), "Grenade_Rib" + k);
            Solid(root, "Rib" + k, rib, Vector3.zero, Quaternion.identity, paint, groove, false, "Gren_Band");
        }

        Color steel = new Color(0.55f, 0.50f, 0.42f);
        Solid(root, "Fuse", ShapeKit.Save(ShapeKit.BevelCylinder(0.05f, 0.05f, 0.05f, 0.008f), "Grenade_Fuse"),
              new Vector3(0f, 0.225f, 0f), Quaternion.identity, paint, steel, false, "Gren_Cap");
        Solid(root, "Cap", ShapeKit.Save(ShapeKit.BevelCylinder(0.038f, 0.038f, 0.03f, 0.006f), "Grenade_Cap"),
              new Vector3(0f, 0.262f, 0f), Quaternion.identity, paint, steel, false, "Gren_Cap");

        // The spoon: a strip from the top of the fuse down the side, hugging the body.
        Vector3[] spoon =
        {
            new Vector3(0.03f, 0.272f, 0f), new Vector3(0.08f, 0.262f, 0f), new Vector3(0.13f, 0.222f, 0f),
            new Vector3(0.166f, 0.162f, 0f), new Vector3(0.188f, 0.082f, 0f), new Vector3(0.193f, 0f, 0f),
            new Vector3(0.184f, -0.06f, 0f),
        };
        Solid(root, "Lever", ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Smooth(spoon, 6), t => 0.03f * ShapeKit.Capped(t, 0.03f),
                                                          Vector3.forward, 0.28f, 1f, 12), "Grenade_Spoon"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.62f, 0.58f, 0.48f), false, "Gren_Lever");

        // The ring pin, hanging off the front of the fuse.
        Solid(root, "Pin", ShapeKit.Save(ShapeKit.Torus(0.034f, 0.0055f, 40, 8), "Grenade_Pin"),
              new Vector3(0f, 0.215f, 0.085f), Quaternion.Euler(0f, 0f, 90f), paint, new Color(0.80f, 0.78f, 0.72f), false, "Gren_Pin");

        return root;
    }

    private static float RadiusAt(Vector2[] profile, float y)
    {
        for (int i = 0; i < profile.Length - 1; i++)
        {
            if (y >= profile[i].y && y <= profile[i + 1].y)
                return Mathf.Lerp(profile[i].x, profile[i + 1].x, (y - profile[i].y) / (profile[i + 1].y - profile[i].y));
        }
        return 0f;
    }

    /// <summary>A jagged splinter thrown out by the grenade.</summary>
    public static GameObject Shard(PaintFn paint)
    {
        GameObject root = new GameObject("Shard");

        GameObject a = GameObject.CreatePrimitive(PrimitiveType.Cube);
        a.name = "Core";
        a.transform.SetParent(root.transform, false);
        a.transform.localScale = new Vector3(0.13f, 0.05f, 0.09f);
        paint(a, new Color(0.45f, 0.47f, 0.50f), false, "Shard_Metal");

        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "Spur";
        b.transform.SetParent(root.transform, false);
        b.transform.localScale = new Vector3(0.09f, 0.04f, 0.06f);
        b.transform.localPosition = new Vector3(0.05f, 0.02f, 0.02f);
        b.transform.localRotation = Quaternion.Euler(20f, 35f, 15f);
        paint(b, new Color(0.45f, 0.47f, 0.50f), false, "Shard_Metal");

        return root;
    }

    /// <summary>
    /// Sticky bomb: one glossy lump of goo sagging onto whatever it hit, with a detonator
    /// stuck in the top - a little box, a red light and an aerial. Six overlapping balls and a
    /// red cube read as broccoli with a brick on it.
    /// </summary>
    public static GameObject Sticky(PaintFn paint)
    {
        GameObject root = new GameObject("Sticky");

        Solid(root, "Blob", ShapeKit.Save(ShapeKit.Blob(0.20f, 0.09f, 11, 0.80f, -0.10f), "Sticky_Blob"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.48f, 0.80f, 0.22f), false, "Sticky_Goo");

        Color metal = new Color(0.24f, 0.25f, 0.28f);
        Solid(root, "Device", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.14f, 0.05f, 0.10f), 0.015f), "Sticky_Device"),
              new Vector3(0f, 0.155f, 0f), Quaternion.Euler(0f, 18f, 0f), paint, metal, false, "Sticky_Device");

        GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        led.name = "Charge";
        led.transform.SetParent(root.transform, false);
        led.transform.localScale = Vector3.one * 0.04f;
        led.transform.localPosition = new Vector3(0.03f, 0.185f, -0.01f);
        paint(led, new Color(0.85f, 0.15f, 0.12f), true, "Sticky_Charge");

        Solid(root, "Aerial", ShapeKit.Save(ShapeKit.BevelCylinder(0.004f, 0.004f, 0.09f, 0.001f, 10), "Sticky_Aerial"),
              new Vector3(-0.04f, 0.22f, 0.02f), Quaternion.identity, paint, metal, false, "Sticky_Device");
        GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "AerialTip";
        knob.transform.SetParent(root.transform, false);
        knob.transform.localScale = Vector3.one * 0.018f;
        knob.transform.localPosition = new Vector3(-0.04f, 0.265f, 0.02f);
        paint(knob, metal, false, "Sticky_Device");

        return root;
    }

    /// <summary>Icicle: a tapered spike, point forward along +Z.</summary>
    /// <summary>
    /// A spike, not a rod. The old one was a capsule with a ball on each end, which reads as
    /// a pill; what makes ice look like ice is the taper to an actual point.
    /// </summary>
    public static GameObject Icicle(PaintFn paint)
    {
        GameObject root = new GameObject("Icicle");

        GameObject spike = ConeMesh.Object("IceSpike", 0.135f, 0.006f, 0.62f);
        spike.transform.SetParent(root.transform, false);
        spike.transform.localPosition = new Vector3(0f, 0f, -0.26f);
        paint(spike, new Color(0.62f, 0.86f, 0.96f), true, "Ice_Body");

        // Two smaller spurs off the shaft: a single clean cone looks machined rather than frozen.
        for (int i = 0; i < 2; i++)
        {
            GameObject spur = ConeMesh.Object("IceSpur" + i, 0.055f, 0.004f, 0.20f);
            spur.transform.SetParent(root.transform, false);
            spur.transform.localPosition = new Vector3((i == 0) ? 0.05f : -0.045f, (i == 0) ? 0.04f : -0.05f, -0.14f);
            spur.transform.localRotation = Quaternion.Euler((i == 0) ? -22f : 18f, (i == 0) ? 16f : -20f, 0f);
            paint(spur, new Color(0.75f, 0.92f, 1f), true, "Ice_Tip");
        }

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localScale = new Vector3(0.15f, 0.15f, 0.10f);
        cap.transform.localPosition = new Vector3(0f, 0f, -0.27f);
        paint(cap, new Color(0.55f, 0.80f, 0.94f), false, "Ice_Cap");

        return root;
    }

    /// <summary>A single ricochet pellet.</summary>
    public static GameObject Pellet(PaintFn paint)
    {
        GameObject root = new GameObject("Pellet");

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.SetParent(root.transform, false);
        ball.transform.localScale = Vector3.one * 0.16f;
        paint(ball, new Color(0.92f, 0.72f, 0.25f), true, "Pellet_Brass");

        return root;
    }

    /// <summary>
    /// Two cloth sacks and a pair of curved arrows trading between them.
    ///
    /// The sacks used to be perfect spheres with a funnel on top, which read as Christmas
    /// baubles or perfume bottles. A sack is a soft body gathered at the neck and tied off,
    /// with the cloth flaring out above the tie - and a patch says "cloth" at a glance. The
    /// straight arrows are now one arc each way, the usual sign for an exchange.
    /// </summary>
    public static GameObject SwapBag(PaintFn paint)
    {
        GameObject root = new GameObject("SwapBag");

        // A sack, bottom to top: flat-ish base, round belly, gathered neck, flared top.
        Vector2[] sack =
        {
            new Vector2(0f, 0f), new Vector2(0.075f, 0.004f), new Vector2(0.115f, 0.030f), new Vector2(0.132f, 0.080f),
            new Vector2(0.128f, 0.140f), new Vector2(0.105f, 0.190f), new Vector2(0.060f, 0.225f), new Vector2(0.032f, 0.245f),
            new Vector2(0.034f, 0.262f), new Vector2(0.060f, 0.285f), new Vector2(0.072f, 0.300f), new Vector2(0.058f, 0.306f),
            new Vector2(0f, 0.303f),
        };
        Mesh body = ShapeKit.Save(ShapeKit.Lathe(sack, 36, 1.06f, 0.92f), "Swap_Sack");
        Mesh tie = ShapeKit.Save(ShapeKit.Torus(0.038f, 0.011f, 40, 10), "Swap_Tie");
        Mesh patch = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.075f, 0.065f, 0.012f), 0.004f), "Swap_Patch");
        Mesh tail = ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Bezier(new Vector3(0.03f, 0.25f, 0.025f), new Vector3(0.06f, 0.24f, 0.05f),
                                                                 new Vector3(0.05f, 0.18f, 0.065f), 12),
                                                 t => 0.0065f * ShapeKit.Capped(t, 0.15f), Vector3.up, 1f, 1f, 8), "Swap_Tail");

        Color[] cloth = { new Color(0.78f, 0.36f, 0.24f), new Color(0.26f, 0.46f, 0.76f) };
        Color[] patchCol = { new Color(0.58f, 0.25f, 0.17f), new Color(0.17f, 0.33f, 0.57f) };
        Color rope = new Color(0.82f, 0.70f, 0.44f);

        for (int i = 0; i < 2; i++)
        {
            GameObject bag = new GameObject("Sack" + i);
            bag.transform.SetParent(root.transform, false);
            bag.transform.localPosition = new Vector3((i == 0) ? -0.17f : 0.17f, -0.20f, 0f);
            // Turned a little and leaning in towards each other, as two filled sacks slump.
            bag.transform.localRotation = Quaternion.Euler(0f, (i == 0) ? 20f : -25f, (i == 0) ? -7f : 8f);

            Solid(bag, "Body", body, Vector3.zero, Quaternion.identity, paint, cloth[i], false, "Swap_Bag" + i);
            Solid(bag, "Tie", tie, new Vector3(0f, 0.252f, 0f), Quaternion.identity, paint, rope, false, "Swap_Neck");
            Solid(bag, "Tail", tail, Vector3.zero, Quaternion.identity, paint, rope, false, "Swap_Neck");
            Solid(bag, "Patch", patch, new Vector3(0.03f, 0.10f, 0.12f), Quaternion.Euler(-8f, 12f, 10f),
                  paint, patchCol[i], false, "Swap_Patch" + i);
        }

        // One arc each way above the sacks: the outer one runs left to right, the inner one back.
        Vector3 centre = new Vector3(0f, 0.10f, 0.03f);
        float[][] arcs = { new[] { 0.24f, 160f, 22f, 0.017f }, new[] { 0.145f, 25f, 155f, 0.015f } };
        Color[] arrowCol = { new Color(0.96f, 0.82f, 0.25f), new Color(0.55f, 0.76f, 0.96f) };
        for (int i = 0; i < 2; i++)
        {
            float r = arcs[i][0], from = arcs[i][1] * Mathf.Deg2Rad, to = arcs[i][2] * Mathf.Deg2Rad, thick = arcs[i][3];

            List<Vector3> path = new List<Vector3>();
            for (int k = 0; k <= 24; k++)
            {
                float a = Mathf.Lerp(from, to, k / 24f);
                path.Add(centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
            }
            Solid(root, "Arc" + i, ShapeKit.Save(ShapeKit.Sweep(path, t => thick, Vector3.forward, 1f, 1f, 12), "Swap_Arc" + i),
                  Vector3.zero, Quaternion.identity, paint, arrowCol[i], false, "Swap_Arrow" + i);

            // The head points along the arc where it ends.
            float sign = (to < from) ? -1f : 1f;
            Vector3 tangent = new Vector3(-Mathf.Sin(to), Mathf.Cos(to), 0f) * sign;
            GameObject head = ConeMesh.Object("SwapHead" + i, thick * 2.7f, 0.002f, thick * 4.4f);
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = path[path.Count - 1];
            head.transform.localRotation = Quaternion.LookRotation(tangent);
            paint(head, arrowCol[i], false, "Swap_Arrow" + i);
        }

        return root;
    }

    /// <summary>Copier: a sheet with a duplicate peeling off it.</summary>
    /// <summary>
    /// The item is called a photocopier, so it is one: a grey office machine with its lid up,
    /// the scanning light glowing green on the glass, a gold token lying on it and the copy
    /// of that token coming out on a sheet at the side.
    ///
    /// The first version put a green cross on a white sheet, which every player reads as a
    /// medical kit; the second was two flat cards and a gold brick, which at icon size read as
    /// nothing in particular.
    /// </summary>
    public static GameObject Copier(PaintFn paint)
    {
        GameObject root = new GameObject("Copier");

        Color grey = new Color(0.84f, 0.85f, 0.83f), trim = new Color(0.72f, 0.73f, 0.72f), dark = new Color(0.30f, 0.31f, 0.34f);
        Color gold = new Color(0.98f, 0.74f, 0.18f);

        Solid(root, "Body", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.42f, 0.22f, 0.32f), 0.025f), "Copier_Body"),
              new Vector3(0f, 0.11f, 0f), Quaternion.identity, paint, grey, false, "Copy_Body");

        // Paper drawers across the front, each with a handle.
        Mesh drawer = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.36f, 0.072f, 0.012f), 0.004f), "Copier_Drawer");
        Mesh handle = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.10f, 0.012f, 0.010f), 0.004f), "Copier_Handle");
        for (int i = 0; i < 2; i++)
        {
            float y = 0.05f + i * 0.085f;
            Solid(root, "Drawer" + i, drawer, new Vector3(0f, y, 0.164f), Quaternion.identity, paint, trim, false, "Copy_Drawer");
            Solid(root, "Handle" + i, handle, new Vector3(0f, y + 0.012f, 0.172f), Quaternion.identity, paint, dark, false, "Copy_Panel");
        }

        // The glass, lit by the scanning lamp, and the token being copied lying on it.
        Solid(root, "Glass", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.28f, 0.006f, 0.22f), 0.002f), "Copier_Glass"),
              new Vector3(-0.04f, 0.221f, 0.005f), Quaternion.identity, paint, new Color(0.45f, 1f, 0.62f), true, "Copy_Glass");
        Solid(root, "Token", ShapeKit.Save(ShapeKit.BevelCylinder(0.05f, 0.05f, 0.016f, 0.005f), "Copier_Token"),
              new Vector3(-0.05f, 0.232f, 0.02f), Quaternion.identity, paint, gold, true, "Copy_Token");

        // Lid hinged along the back edge and propped open.
        GameObject hinge = new GameObject("Hinge");
        hinge.transform.SetParent(root.transform, false);
        hinge.transform.localPosition = new Vector3(-0.04f, 0.214f, -0.135f);
        hinge.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        Solid(hinge, "Lid", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.31f, 0.026f, 0.26f), 0.01f), "Copier_Lid"),
              new Vector3(0f, 0.013f, 0.13f), Quaternion.identity, paint, new Color(0.36f, 0.38f, 0.42f), false, "Copy_Lid");

        // Control panel at the front right, tipped toward whoever is using it.
        GameObject panel = Solid(root, "Panel", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.085f, 0.03f, 0.075f), 0.008f), "Copier_Panel"),
                                 new Vector3(0.155f, 0.232f, 0.095f), Quaternion.Euler(14f, 0f, 0f), paint, dark, false, "Copy_Panel");
        Solid(panel, "Button", ShapeKit.Save(ShapeKit.BevelCylinder(0.013f, 0.013f, 0.012f, 0.004f, 20), "Copier_Button"),
              new Vector3(0.018f, 0.017f, 0.012f), Quaternion.identity, paint, new Color(0.25f, 0.85f, 0.35f), true, "Copy_Button");
        Solid(panel, "Screen", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.036f, 0.004f, 0.026f), 0.0015f), "Copier_Screen"),
              new Vector3(-0.016f, 0.0155f, -0.008f), Quaternion.identity, paint, new Color(0.55f, 0.80f, 0.95f), true, "Copy_Screen");

        // Output tray on the left with the copy sliding out of the slot onto it.
        GameObject tray = Solid(root, "Tray", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.11f, 0.008f, 0.22f), 0.003f), "Copier_Tray"),
                                new Vector3(-0.255f, 0.125f, 0f), Quaternion.Euler(0f, 0f, -12f), paint, trim, false, "Copy_Drawer");
        GameObject sheet = Solid(tray, "Paper", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.13f, 0.004f, 0.19f), 0.0015f), "Copier_Paper"),
                                 new Vector3(0.02f, 0.006f, 0f), Quaternion.identity, paint, new Color(0.97f, 0.97f, 0.95f), false, "Copy_Paper");
        Solid(sheet, "Print", ShapeKit.Save(ShapeKit.BevelCylinder(0.036f, 0.036f, 0.002f, 0.0008f, 24), "Copier_Print"),
              new Vector3(-0.02f, 0.0025f, 0f), Quaternion.identity, paint, new Color(0.86f, 0.64f, 0.16f), false, "Copy_Print");

        return root;
    }

    /// <summary>
    /// An open cardboard box of odds and ends, with a price tag on a string.
    ///
    /// It was a crate holding a sphere, a cube and a cylinder - three primitives, which say
    /// "geometry" rather than "junk". Junk is recognisable things that do not belong together:
    /// a tin can, a bedspring, a cog, a bottle. The open flaps and the tag say "box" and "shop"
    /// before any of the contents register.
    /// </summary>
    public static GameObject Junk(PaintFn paint)
    {
        GameObject root = new GameObject("Junk");

        Color card = new Color(0.68f, 0.51f, 0.32f), flapCol = new Color(0.60f, 0.44f, 0.27f);
        Color brass = new Color(0.86f, 0.66f, 0.30f), steel = new Color(0.72f, 0.74f, 0.78f);
        Vector3 size = new Vector3(0.34f, 0.22f, 0.28f);
        float top = size.y * 0.5f;

        Solid(root, "Box", ShapeKit.Save(ShapeKit.RoundedBox(size, 0.012f), "Junk_Box"), Vector3.zero, Quaternion.identity,
              paint, card, false, "Junk_Crate");
        // The dark inside, seen through the open top.
        Solid(root, "Inside", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(size.x - 0.03f, 0.01f, size.z - 0.03f), 0.004f), "Junk_Inside"),
              new Vector3(0f, top - 0.002f, 0f), Quaternion.identity, paint, new Color(0.17f, 0.12f, 0.08f), false, "Junk_Inside");

        // Four flaps standing open, tipped outwards from the rim.
        Mesh flapX = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(size.x, 0.008f, 0.11f), 0.003f), "Junk_FlapX");
        Mesh flapZ = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(size.z, 0.008f, 0.10f), 0.003f), "Junk_FlapZ");
        float lift = 35f * Mathf.Deg2Rad;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 dz = new Vector3(0f, Mathf.Sin(lift), Mathf.Cos(lift) * s);
            Solid(root, s > 0 ? "FlapFront" : "FlapBack", flapX, new Vector3(0f, top, size.z * 0.5f * s) + dz * 0.055f,
                  Quaternion.LookRotation(dz, Vector3.up), paint, flapCol, false, "Junk_Flap");

            Vector3 dx = new Vector3(Mathf.Cos(lift) * s, Mathf.Sin(lift), 0f);
            Solid(root, s > 0 ? "FlapRight" : "FlapLeft", flapZ, new Vector3(size.x * 0.5f * s, top, 0f) + dx * 0.05f,
                  Quaternion.LookRotation(dx, Vector3.up), paint, flapCol, false, "Junk_Flap");
        }

        // A tin can lying half out of the box.
        GameObject can = new GameObject("Can");
        can.transform.SetParent(root.transform, false);
        can.transform.localPosition = new Vector3(-0.08f, top + 0.035f, -0.02f);
        can.transform.localRotation = Quaternion.Euler(0f, 30f, 62f);
        Solid(can, "Tin", ShapeKit.Save(ShapeKit.BevelCylinder(0.042f, 0.042f, 0.12f, 0.006f), "Junk_Tin"), Vector3.zero, Quaternion.identity,
              paint, steel, false, "Junk_Tin");
        Solid(can, "Label", ShapeKit.Save(ShapeKit.BevelCylinder(0.0436f, 0.0436f, 0.06f, 0.002f), "Junk_Label"), Vector3.zero, Quaternion.identity,
              paint, new Color(0.82f, 0.22f, 0.20f), false, "Junk_Label");

        // A bedspring standing up at the back right.
        List<Vector3> coil = new List<Vector3>();
        const int turns = 5, per = 24;
        for (int k = 0; k <= turns * per; k++)
        {
            float t = (float)k / (turns * per);
            float a = t * turns * Mathf.PI * 2f;
            coil.Add(new Vector3(Mathf.Cos(a) * 0.034f, t * 0.15f, Mathf.Sin(a) * 0.034f));
        }
        Solid(root, "Spring", ShapeKit.Save(ShapeKit.Sweep(coil, t => 0.0065f, Vector3.up, 1f, 1f, 8), "Junk_Spring"),
              new Vector3(0.10f, top - 0.03f, -0.05f), Quaternion.Euler(0f, 0f, -14f), paint, steel, false, "Junk_Spring");

        // A cog standing on its edge, facing out of the box.
        GameObject cog = new GameObject("Cog");
        cog.transform.SetParent(root.transform, false);
        cog.transform.localPosition = new Vector3(0f, top + 0.06f, 0.07f);
        cog.transform.localRotation = Quaternion.Euler(-25f, 15f, 20f);
        Quaternion face = Quaternion.Euler(90f, 0f, 0f);   // a Y-axis disc turned to face +Z
        Solid(cog, "Disc", ShapeKit.Save(ShapeKit.BevelCylinder(0.05f, 0.05f, 0.02f, 0.004f), "Junk_CogDisc"), Vector3.zero, face,
              paint, brass, false, "Junk_Cog");
        Mesh tooth = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.024f, 0.02f, 0.02f), 0.003f), "Junk_CogTooth");
        for (int k = 0; k < 10; k++)
        {
            float a = k * 36f * Mathf.Deg2Rad;
            Solid(cog, "Tooth" + k, tooth, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.058f, Quaternion.Euler(0f, 0f, k * 36f),
                  paint, brass, false, "Junk_Cog");
        }
        Solid(cog, "Hole", ShapeKit.Save(ShapeKit.BevelCylinder(0.016f, 0.016f, 0.024f, 0.002f), "Junk_CogHole"), Vector3.zero, face,
              paint, new Color(0.20f, 0.16f, 0.10f), false, "Junk_Hole");

        // A bottle leaning against the back.
        Vector2[] bottle =
        {
            new Vector2(0f, 0f), new Vector2(0.030f, 0f), new Vector2(0.034f, 0.008f), new Vector2(0.034f, 0.085f),
            new Vector2(0.028f, 0.105f), new Vector2(0.013f, 0.125f), new Vector2(0.011f, 0.150f), new Vector2(0.014f, 0.155f),
            new Vector2(0.014f, 0.166f), new Vector2(0f, 0.166f),
        };
        Solid(root, "Bottle", ShapeKit.Save(ShapeKit.Lathe(bottle, 24), "Junk_Bottle"), new Vector3(-0.03f, top - 0.05f, -0.07f),
              Quaternion.Euler(-18f, 0f, 22f), paint, new Color(0.30f, 0.62f, 0.40f), false, "Junk_Bottle");

        // The price tag, hanging off the front on a string.
        Vector2[] tagOutline =
        {
            new Vector2(-0.035f, -0.022f), new Vector2(0.025f, -0.022f), new Vector2(0.045f, 0f),
            new Vector2(0.025f, 0.022f), new Vector2(-0.035f, 0.022f),
        };
        Vector3 tagAt = new Vector3(0.13f, -0.02f, size.z * 0.5f + 0.02f);
        Quaternion tagTurn = Quaternion.Euler(0f, -10f, -60f);
        Solid(root, "Tag", ShapeKit.Save(ShapeKit.Extrude(tagOutline, 0.006f), "Junk_Tag"), tagAt, tagTurn,
              paint, new Color(0.96f, 0.94f, 0.86f), false, "Junk_Tag");
        Vector3 knot = tagAt + tagTurn * new Vector3(-0.03f, 0f, 0f);
        Vector3 corner = new Vector3(size.x * 0.5f - 0.02f, top, size.z * 0.5f - 0.005f);
        Solid(root, "String", ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Bezier(corner, new Vector3(corner.x + 0.01f, (corner.y + knot.y) * 0.5f, corner.z + 0.035f), knot, 12),
                                                           t => 0.0028f, Vector3.up, 1f, 1f, 6), "Junk_String"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.55f, 0.45f, 0.30f), false, "Junk_String");

        return root;
    }

    /// <summary>Tax: a stamped document with a coin on it.</summary>
    /// <summary>
    /// Tax: a demand on the table - a heading and lines of small print, a red percent sign
    /// stamped on it - and the money it is after stacked on its corner. A blank sheet with a
    /// coin on its edge and a red dot said "money"; the percent is what reads as tax.
    /// The page is laid out to be read from -z, the side the icon looks from, top at +z.
    /// </summary>
    public static GameObject Tax(PaintFn paint)
    {
        GameObject root = new GameObject("Tax");

        Solid(root, "Paper", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.30f, 0.008f, 0.38f), 0.003f), "Tax_Paper"),
              new Vector3(0f, 0.004f, 0f), Quaternion.identity, paint, new Color(0.95f, 0.93f, 0.86f), false, "Tax_Paper");

        // z, length, width: the heading, then the text, flush left
        float[,] lines = { { 0.14f, 0.16f, 0.022f }, { 0.09f, 0.22f, 0.012f }, { 0.055f, 0.19f, 0.012f }, { 0.02f, 0.22f, 0.012f }, { -0.015f, 0.13f, 0.012f } };
        for (int i = 0; i < lines.GetLength(0); i++)
        {
            float len = lines[i, 1];
            Solid(root, "Line" + i, ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(len, 0.003f, lines[i, 2]), 0.0012f), "Tax_Line" + i),
                  new Vector3(-0.11f + len * 0.5f, 0.0085f, lines[i, 0]), Quaternion.identity,
                  paint, new Color(0.34f, 0.35f, 0.40f), false, "Tax_Ink");
        }

        // The stamp: a ring with a percent sign in it, slapped across the end of the text.
        Color red = new Color(0.82f, 0.18f, 0.16f);
        Vector3 stamp = new Vector3(-0.02f, 0.0085f, -0.085f);
        Solid(root, "StampRing", ShapeKit.Save(ShapeKit.Torus(0.072f, 0.0045f, 64, 8), "Tax_StampRing"),
              stamp, Quaternion.identity, paint, red, false, "Tax_Stamp");
        Mesh dot = ShapeKit.Save(ShapeKit.Torus(0.016f, 0.0055f, 32, 8), "Tax_PercentDot");
        Solid(root, "PercentDot0", dot, stamp + new Vector3(-0.026f, 0f, 0.03f), Quaternion.identity, paint, red, false, "Tax_Stamp");
        Solid(root, "PercentDot1", dot, stamp + new Vector3(0.026f, 0f, -0.03f), Quaternion.identity, paint, red, false, "Tax_Stamp");
        Solid(root, "PercentBar", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.012f, 0.003f, 0.115f), 0.0012f), "Tax_PercentBar"),
              stamp, Quaternion.Euler(0f, 35f, 0f), paint, red, false, "Tax_Stamp");

        // What is being collected, stacked a little untidily on the bottom right corner.
        Mesh coin = ShapeKit.Save(ShapeKit.BevelCylinder(0.055f, 0.055f, 0.016f, 0.004f), "Tax_Coin");
        Vector2[] jitter = { new Vector2(0f, 0f), new Vector2(0.006f, -0.004f), new Vector2(-0.004f, 0.005f) };
        for (int i = 0; i < jitter.Length; i++)
            Solid(root, "Coin" + i, coin, new Vector3(0.118f + jitter[i].x, 0.016f + i * 0.016f, -0.148f + jitter[i].y), Quaternion.identity,
                  paint, new Color(0.95f, 0.78f, 0.22f), true, "Tax_Coin");

        return root;
    }

    /// <summary>Glass cannons: a fragile glass sphere already cracking apart.</summary>
    /// <summary>
    /// A cannon made of glass. The old one was a sphere with the cracks modelled inside it -
    /// invisible, since the sphere is opaque - so it came out as a plain blue ball. Both
    /// halves of the name have to be on the outside: a barrel for the cannon, chips breaking
    /// off it for the glass.
    /// </summary>
    /// <summary>
    /// A glass cannon on a field carriage.
    ///
    /// The barrel used to be a cone widening towards the muzzle - the shape of a megaphone, and
    /// that is how it read. A cannon is the other way round: heavy at the breech, tapering to
    /// the muzzle, with raised reinforcing rings and a swell at the mouth. That outline is what
    /// says "cannon" before the wheels do.
    ///
    /// Glass is told with what the game can actually draw. Its shaders have neither
    /// transparency nor glow for these materials, so it is a pale cyan with cartoon highlight
    /// streaks, a crack running down the barrel and splinters coming off the muzzle.
    /// </summary>
    public static GameObject GlassCannon(PaintFn paint)
    {
        GameObject root = new GameObject("GlassCannon");

        Color glass = new Color(0.70f, 0.90f, 0.98f), shine = new Color(0.97f, 0.99f, 1f);
        Color wood = new Color(0.47f, 0.32f, 0.19f), iron = new Color(0.27f, 0.28f, 0.31f);

        // The barrel, turned around Y from the breech (0) to the muzzle (0.56), then laid along +Z.
        Vector2[] barrel =
        {
            new Vector2(0f, 0f), new Vector2(0.118f, 0f), new Vector2(0.134f, 0.014f), new Vector2(0.134f, 0.048f),
            new Vector2(0.122f, 0.060f), new Vector2(0.118f, 0.118f), new Vector2(0.128f, 0.126f), new Vector2(0.128f, 0.150f),
            new Vector2(0.114f, 0.160f), new Vector2(0.098f, 0.375f), new Vector2(0.107f, 0.386f), new Vector2(0.107f, 0.406f),
            new Vector2(0.093f, 0.416f), new Vector2(0.088f, 0.495f), new Vector2(0.104f, 0.518f), new Vector2(0.112f, 0.540f),
            new Vector2(0.108f, 0.560f), new Vector2(0f, 0.560f),
        };
        const float axisY = 0.07f, breechZ = -0.25f, length = 0.56f;
        Quaternion lay = Quaternion.Euler(90f, 0f, 0f);   // the lathe's +Y becomes +Z: the muzzle end

        Solid(root, "Barrel", ShapeKit.Save(ShapeKit.Lathe(barrel, 40), "GlassCannon_Barrel"),
              new Vector3(0f, axisY, breechZ), lay, paint, glass, false, "Glass_Core");

        // The cascabel: a ball on a short neck behind the breech.
        Vector2[] knob =
        {
            new Vector2(0f, 0f), new Vector2(0.026f, 0.004f), new Vector2(0.040f, 0.022f), new Vector2(0.036f, 0.040f),
            new Vector2(0.020f, 0.052f), new Vector2(0.020f, 0.070f), new Vector2(0f, 0.070f),
        };
        Solid(root, "Cascabel", ShapeKit.Save(ShapeKit.Lathe(knob, 24), "GlassCannon_Cascabel"),
              new Vector3(0f, axisY, breechZ - 0.066f), lay, paint, new Color(0.55f, 0.80f, 0.92f), false, "Glass_Breech");

        // The bore: a dark disc just proud of the muzzle face, so the end reads as a hole.
        Solid(root, "Bore", ShapeKit.Save(ShapeKit.BevelCylinder(0.068f, 0.068f, 0.006f, 0.002f), "GlassCannon_Bore"),
              new Vector3(0f, axisY, breechZ + length + 0.0015f), lay, paint, new Color(0.05f, 0.08f, 0.11f), false, "Glass_Bore");

        // Carriage: two cheeks either side of the barrel, an axle, and a trail down to the ground.
        Mesh cheek = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.035f, 0.15f, 0.32f), 0.01f), "GlassCannon_Cheek");
        for (int s = -1; s <= 1; s += 2)
            Solid(root, s < 0 ? "CheekL" : "CheekR", cheek, new Vector3(0.13f * s, -0.02f, -0.06f), Quaternion.identity,
                  paint, wood, false, "Glass_Carriage");

        Solid(root, "Trail", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.09f, 0.06f, 0.36f), 0.012f), "GlassCannon_Trail"),
              new Vector3(0f, -0.17f, -0.30f), Quaternion.Euler(-20f, 0f, 0f), paint, wood, false, "Glass_Carriage");

        Quaternion acrossX = Quaternion.Euler(0f, 0f, 90f);   // a Y-axis cylinder turned to run along X
        Solid(root, "Axle", ShapeKit.Save(ShapeKit.BevelCylinder(0.022f, 0.022f, 0.44f, 0.004f), "GlassCannon_Axle"),
              new Vector3(0f, -0.10f, -0.05f), acrossX, paint, iron, false, "Glass_Iron");

        // Spoked wheels, outside the barrel's widest point so the profile shows them whole.
        Mesh rim = ShapeKit.Save(ShapeKit.Torus(0.135f, 0.020f, 56, 10), "GlassCannon_Rim");
        Mesh hub = ShapeKit.Save(ShapeKit.BevelCylinder(0.035f, 0.035f, 0.05f, 0.01f), "GlassCannon_Hub");
        Mesh spoke = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.016f, 0.125f, 0.016f), 0.004f), "GlassCannon_Spoke");
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject wheel = new GameObject(s < 0 ? "WheelL" : "WheelR");
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = new Vector3(0.21f * s, -0.10f, -0.05f);

            Solid(wheel, "Rim", rim, Vector3.zero, acrossX, paint, iron, false, "Glass_Iron");
            Solid(wheel, "Hub", hub, Vector3.zero, acrossX, paint, iron, false, "Glass_Iron");
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                Solid(wheel, "Spoke" + k, spoke, dir * 0.07f, Quaternion.FromToRotation(Vector3.up, dir),
                      paint, wood, false, "Glass_Wheel");
            }
        }

        // Highlight streaks and a crack, laid on the surface where the icon camera sees it
        // (from +X, a little above). theta is measured round the barrel from +X towards +Y.
        System.Func<float, float, float, Vector3> onBarrel = (h, thetaDeg, lift) =>
        {
            float r = LatheRadius(barrel, h) * lift;
            float t = thetaDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(t) * r, axisY + Mathf.Sin(t) * r, breechZ + h);
        };

        float[][] streaks = { new[] { 0.17f, 0.36f, 48f }, new[] { 0.42f, 0.49f, 60f }, new[] { 0.07f, 0.11f, 56f } };
        for (int k = 0; k < streaks.Length; k++)
        {
            List<Vector3> line = new List<Vector3>();
            for (int j = 0; j <= 12; j++)
                line.Add(onBarrel(Mathf.Lerp(streaks[k][0], streaks[k][1], j / 12f), streaks[k][2], 1.035f));
            Solid(root, "Shine" + k, ShapeKit.Save(ShapeKit.Sweep(line, t => 0.0095f * ShapeKit.Capped(t, 0.25f),
                                                                  Vector3.up, 1f, 1f, 8), "GlassCannon_Shine" + k),
                  Vector3.zero, Quaternion.identity, paint, shine, false, "Glass_Shine");
        }

        float[][] crack =
        {
            new[] { 0.165f, 18f }, new[] { 0.190f, 27f }, new[] { 0.215f, 19f }, new[] { 0.245f, 30f },
            new[] { 0.270f, 22f }, new[] { 0.300f, 31f }, new[] { 0.325f, 24f },
        };
        List<Vector3> fissure = new List<Vector3>();
        for (int k = 0; k < crack.Length; k++) fissure.Add(onBarrel(crack[k][0], crack[k][1], 1.02f));
        Solid(root, "Crack", ShapeKit.Save(ShapeKit.Sweep(fissure, t => 0.0045f * ShapeKit.Capped(t, 0.1f), Vector3.up, 1f, 1f, 6),
                                           "GlassCannon_Crack"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.13f, 0.30f, 0.42f), false, "Glass_Crack");

        // Splinters flying off the muzzle.
        Mesh splinter = ShapeKit.Save(ShapeKit.Crystal(0.016f, 0.035f, 0.024f, 5), "GlassCannon_Splinter");
        Vector3[] chips = { new Vector3(0.07f, axisY + 0.10f, 0.34f), new Vector3(0.11f, axisY - 0.02f, 0.36f), new Vector3(0.02f, axisY + 0.14f, 0.37f) };
        Vector3[] spin = { new Vector3(30f, 20f, -50f), new Vector3(-40f, 60f, 70f), new Vector3(80f, -20f, 15f) };
        for (int k = 0; k < chips.Length; k++)
            Solid(root, "Splinter" + k, splinter, chips[k], Quaternion.Euler(spin[k]), paint, glass, false, "Glass_Chip");

        return root;
    }

    /// <summary>Radius of a lathe profile at height <paramref name="h"/>, by straight interpolation.</summary>
    private static float LatheRadius(Vector2[] profile, float h)
    {
        for (int i = 1; i < profile.Length; i++)
        {
            if (profile[i].y < h || profile[i].y == profile[i - 1].y) continue;
            float k = Mathf.InverseLerp(profile[i - 1].y, profile[i].y, h);
            return Mathf.Lerp(profile[i - 1].x, profile[i].x, k);
        }
        return profile[profile.Length - 1].x;
    }

    /// <summary>
    /// Donkey pinata, built the way the real ones are: a barrel of a body, a neck, a boxy head
    /// with a pale muzzle and googly eyes, tall ears, four thick legs, all of it covered in
    /// rows of cut paper fringe in loud colours, and a tail of paper streamers.
    ///
    /// The old one was a ball wrapped in three hoops on four thin blue sticks, which stopped
    /// short of the body. The legs here are as thick as a real pinata's and run up into the
    /// body, and the fringe on them hides the joint.
    /// </summary>
    public static GameObject Pinata(PaintFn paint)
    {
        GameObject root = new GameObject("Pinata");

        Color[] paper =
        {
            new Color(0.96f, 0.36f, 0.55f), new Color(0.98f, 0.58f, 0.18f), new Color(0.98f, 0.85f, 0.22f),
            new Color(0.35f, 0.80f, 0.42f), new Color(0.30f, 0.62f, 0.95f), new Color(0.66f, 0.42f, 0.92f),
        };
        Color shell = new Color(0.97f, 0.52f, 0.66f), dark = new Color(0.08f, 0.07f, 0.09f);
        Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);

        // Body: a barrel along x, nose end at +x, centred where the old body was; the
        // feet stand at y = -0.25.
        const float R = 0.115f, ground = -0.25f;
        Vector2[] barrel =
        {
            new Vector2(0f, -0.175f), new Vector2(0.085f, -0.175f), new Vector2(0.108f, -0.165f), new Vector2(R, -0.14f),
            new Vector2(R, 0.13f), new Vector2(0.108f, 0.155f), new Vector2(0.085f, 0.165f), new Vector2(0f, 0.165f),
        };
        Solid(root, "Body", ShapeKit.Save(ShapeKit.Lathe(barrel, 32), "Pinata_Body"), new Vector3(-0.02f, 0f, 0f), alongX,
              paint, shell, false, "Pin_Body");

        // Six rows of fringe, front to back, each hem lapping over the top of the next.
        Mesh bodyFringe = ShapeKit.Save(ShapeKit.Frill(R - 0.004f, R + 0.013f, 0.066f, 28, 0.013f), "Pinata_FringeBody");
        for (int i = 0; i < 6; i++)
            Solid(root, "Fringe" + i, bodyFringe, new Vector3(0.125f - i * 0.055f, 0f, 0f), alongX, paint, paper[i], false, "Pin_Paper" + i);
        // and a domed rosette over the flat front of the barrel, which showed as a pink plate
        Solid(root, "Chest", ShapeKit.Save(ShapeKit.Frill(0f, R + 0.010f, 0.065f, 28, 0.013f, 0.5f), "Pinata_FringeChest"),
              new Vector3(0.185f, 0f, 0f), alongX, paint, paper[5], false, "Pin_Paper5");

        // Legs: thick posts running up into the body, two rows of fringe each, dark hooves.
        Mesh legCore = ShapeKit.Save(ShapeKit.BevelCylinder(0.040f, 0.040f, 0.22f, 0.01f), "Pinata_Leg");
        Mesh legFringe = ShapeKit.Save(ShapeKit.Frill(0.038f, 0.050f, 0.055f, 12, 0.011f), "Pinata_FringeLeg");
        Mesh hoof = ShapeKit.Save(ShapeKit.BevelCylinder(0.044f, 0.044f, 0.03f, 0.008f), "Pinata_Hoof");
        for (int i = 0; i < 4; i++)
        {
            Vector3 foot = new Vector3((i < 2) ? 0.085f : -0.115f, ground, (i % 2 == 0) ? 0.062f : -0.062f);
            Solid(root, "Leg" + i, legCore, foot + new Vector3(0f, 0.11f, 0f), Quaternion.identity, paint, shell, false, "Pin_Body");
            Solid(root, "LegFringeA" + i, legFringe, foot + new Vector3(0f, 0.165f, 0f), Quaternion.identity, paint, paper[2], false, "Pin_Paper2");
            Solid(root, "LegFringeB" + i, legFringe, foot + new Vector3(0f, 0.115f, 0f), Quaternion.identity, paint, paper[4], false, "Pin_Paper4");
            Solid(root, "Hoof" + i, hoof, foot + new Vector3(0f, 0.015f, 0f), Quaternion.identity, paint, new Color(0.22f, 0.16f, 0.20f), false, "Pin_Hoof");
        }

        // Neck up and forward out of the front of the body, fringed like the rest.
        Vector3 neckBase = new Vector3(0.105f, 0.045f, 0f), neckTop = new Vector3(0.175f, 0.20f, 0f);
        Vector3 neckDir = (neckTop - neckBase).normalized;
        float neckLen = (neckTop - neckBase).magnitude;
        Quaternion neckRot = Quaternion.FromToRotation(Vector3.up, neckDir);
        Solid(root, "Neck", ShapeKit.Save(ShapeKit.BevelCylinder(0.052f, 0.050f, neckLen + 0.04f, 0.012f), "Pinata_Neck"),
              (neckBase + neckTop) * 0.5f, neckRot, paint, shell, false, "Pin_Body");
        Mesh neckFringe = ShapeKit.Save(ShapeKit.Frill(0.050f, 0.064f, 0.06f, 14, 0.012f), "Pinata_FringeNeck");
        Solid(root, "NeckFringe0", neckFringe, neckBase + neckDir * (neckLen - 0.005f), neckRot, paint, paper[3], false, "Pin_Paper3");
        Solid(root, "NeckFringe1", neckFringe, neckBase + neckDir * (neckLen - 0.06f), neckRot, paint, paper[1], false, "Pin_Paper1");

        // Head: a rounded box tipped nose-down, a pale muzzle with nostrils, googly eyes.
        GameObject head = Solid(root, "Head", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.17f, 0.105f, 0.10f), 0.032f), "Pinata_Head"),
                                new Vector3(0.235f, 0.215f, 0f), Quaternion.Euler(0f, 0f, -14f), paint, shell, false, "Pin_Body");
        Solid(head, "Muzzle", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.075f, 0.09f, 0.104f), 0.03f), "Pinata_Muzzle"),
              new Vector3(0.07f, -0.008f, 0f), Quaternion.identity, paint, new Color(0.98f, 0.90f, 0.80f), false, "Pin_Muzzle");
        for (int i = 0; i < 2; i++)
        {
            float side = (i == 0) ? 1f : -1f;

            GameObject nostril = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nostril.name = "Nostril" + i;
            nostril.transform.SetParent(head.transform, false);
            nostril.transform.localPosition = new Vector3(0.107f, -0.002f, 0.022f * side);
            nostril.transform.localScale = new Vector3(0.010f, 0.016f, 0.012f);
            paint(nostril, dark, false, "Pin_Eye");

            GameObject white = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            white.name = "EyeWhite" + i;
            white.transform.SetParent(head.transform, false);
            white.transform.localPosition = new Vector3(0.018f, 0.022f, 0.050f * side);
            white.transform.localScale = new Vector3(0.036f, 0.036f, 0.016f);
            paint(white, Color.white, false, "Pin_EyeWhite");

            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil" + i;
            pupil.transform.SetParent(head.transform, false);
            pupil.transform.localPosition = new Vector3(0.024f, 0.017f, 0.0575f * side);
            pupil.transform.localScale = new Vector3(0.017f, 0.017f, 0.008f);
            paint(pupil, dark, false, "Pin_Eye");
        }

        // Ears: tall flattened cones, leaning back and out, open side forward.
        Vector2[] earProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.024f, 0.004f), new Vector2(0.027f, 0.030f), new Vector2(0.023f, 0.075f),
            new Vector2(0.013f, 0.110f), new Vector2(0.004f, 0.125f), new Vector2(0f, 0.128f),
        };
        Mesh ear = ShapeKit.Save(ShapeKit.Lathe(earProfile, 20, 1f, 0.42f), "Pinata_Ear");
        for (int i = 0; i < 2; i++)
        {
            float side = (i == 0) ? 1f : -1f;
            GameObject e = Solid(head, "Ear" + i, ear, new Vector3(-0.035f, 0.045f, 0.028f * side),
                                 Quaternion.LookRotation(new Vector3(1f, 0.15f, 0.6f * side), new Vector3(-0.20f, 1f, 0.30f * side)),
                                 paint, paper[2], false, "Pin_Paper2");
            GameObject inner = Solid(e, "Inner", ear, new Vector3(0f, 0.02f, 0.008f), Quaternion.identity, paint, paper[0], false, "Pin_Paper0");
            inner.transform.localScale = Vector3.one * 0.7f;
        }

        // Tail: a bunch of paper streamers out of the rump.
        for (int i = 0; i < 4; i++)
        {
            float dz = -0.024f + i * 0.016f;
            List<Vector3> path = ShapeKit.Smooth(new[] { new Vector3(-0.17f, 0.04f, dz * 0.5f), new Vector3(-0.235f, 0.02f, dz),
                                                         new Vector3(-0.27f, -0.06f, dz * 1.4f), new Vector3(-0.265f, -0.15f, dz * 1.8f) }, 8);
            int colour = (i * 2 + 1) % paper.Length;
            Solid(root, "Streamer" + i, ShapeKit.Save(ShapeKit.Sweep(path, t => 0.013f * ShapeKit.Capped(t, 0.04f), Vector3.forward, 1f, 0.22f, 10),
                                                       "Pinata_Streamer" + i),
                  Vector3.zero, Quaternion.identity, paint, paper[colour], false, "Pin_Paper" + colour);
        }

        return root;
    }

    /// <summary>Generosity: an open box overflowing with gifts.</summary>
    public static GameObject Generosity(PaintFn paint)
    {
        GameObject root = new GameObject("Generosity");

        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Box";
        box.transform.SetParent(root.transform, false);
        box.transform.localScale = new Vector3(0.36f, 0.20f, 0.32f);
        box.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        paint(box, new Color(0.85f, 0.75f, 0.30f), false, "Gen_Box");

        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(root.transform, false);
        lid.transform.localScale = new Vector3(0.40f, 0.04f, 0.36f);
        lid.transform.localPosition = new Vector3(-0.18f, 0.14f, 0f);
        lid.transform.localRotation = Quaternion.Euler(0f, 0f, 55f);
        paint(lid, new Color(0.70f, 0.60f, 0.22f), false, "Gen_Lid");

        Color[] gifts = {
            new Color(0.90f, 0.30f, 0.30f),
            new Color(0.35f, 0.80f, 0.45f),
            new Color(0.40f, 0.60f, 0.95f),
        };
        for (int i = 0; i < 3; i++)
        {
            GameObject gift = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gift.name = "Gift" + i;
            gift.transform.SetParent(root.transform, false);
            gift.transform.localScale = Vector3.one * 0.12f;
            gift.transform.localPosition = new Vector3(-0.08f + i * 0.09f, 0.10f + (i % 2) * 0.07f, 0.02f);
            gift.transform.localRotation = Quaternion.Euler(15f * i, 25f * i, 10f * i);
            paint(gift, gifts[i], true, "Gen_Gift" + i);

            // A ribbon crossed over it and a bow on top - without them a coloured cube is a
            // toy block, not a present. Built in the gift's own unit space.
            Color ribbon = new Color(0.98f, 0.84f, 0.30f);
            for (int k = 0; k < 2; k++)
            {
                GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "Ribbon" + i + "_" + k;
                band.transform.SetParent(gift.transform, false);
                band.transform.localScale = (k == 0) ? new Vector3(1.04f, 1.04f, 0.22f) : new Vector3(0.22f, 1.04f, 1.04f);
                paint(band, ribbon, false, "Gen_Ribbon");

                GameObject loop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                loop.name = "Bow" + i + "_" + k;
                loop.transform.SetParent(gift.transform, false);
                loop.transform.localScale = new Vector3(0.42f, 0.22f, 0.24f);
                loop.transform.localPosition = new Vector3((k == 0) ? -0.17f : 0.17f, 0.58f, 0f);
                loop.transform.localRotation = Quaternion.Euler(0f, 0f, (k == 0) ? 32f : -32f);
                paint(loop, ribbon, false, "Gen_Ribbon");
            }
        }

        return root;
    }

    /// <summary>Death wand: a dark staff crowned with a skull.</summary>
    /// <summary>
    /// A skull on a staff. The eyes were already there, just buried a millimetre inside the
    /// sphere, so at icon size the skull was a plain white ball. Sunk sockets with the glow
    /// standing proud of them is what makes it read as a face.
    /// </summary>
    public static GameObject DeathWand(PaintFn paint)
    {
        GameObject root = new GameObject("DeathWand");

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.055f, 0.23f, 0.055f);
        shaft.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        paint(shaft, new Color(0.16f, 0.14f, 0.18f), false, "Wand_Shaft");

        GameObject skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skull.name = "Skull";
        skull.transform.SetParent(root.transform, false);
        skull.transform.localScale = new Vector3(0.21f, 0.23f, 0.20f);
        skull.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        paint(skull, new Color(0.90f, 0.89f, 0.84f), false, "Wand_Skull");

        GameObject jaw = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jaw.name = "Jaw";
        jaw.transform.SetParent(root.transform, false);
        jaw.transform.localScale = new Vector3(0.15f, 0.055f, 0.14f);
        jaw.transform.localPosition = new Vector3(0f, 0.145f, 0.015f);
        paint(jaw, new Color(0.86f, 0.85f, 0.80f), false, "Wand_Jaw");

        for (int i = 0; i < 3; i++)
        {
            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "Tooth" + i;
            tooth.transform.SetParent(root.transform, false);
            tooth.transform.localScale = new Vector3(0.022f, 0.04f, 0.022f);
            tooth.transform.localPosition = new Vector3(-0.04f + i * 0.04f, 0.175f, 0.085f);
            paint(tooth, new Color(0.20f, 0.16f, 0.16f), false, "Wand_Gap");
        }

        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0) ? -0.055f : 0.055f;

            GameObject socket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            socket.name = "Socket" + i;
            socket.transform.SetParent(root.transform, false);
            socket.transform.localScale = new Vector3(0.085f, 0.085f, 0.06f);
            socket.transform.localPosition = new Vector3(x, 0.27f, 0.085f);
            paint(socket, new Color(0.14f, 0.12f, 0.13f), false, "Wand_Socket");

            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye" + i;
            eye.transform.SetParent(root.transform, false);
            eye.transform.localScale = Vector3.one * 0.05f;
            eye.transform.localPosition = new Vector3(x, 0.27f, 0.105f);
            paint(eye, new Color(1f, 0.28f, 0.15f), true, "Wand_Eye");
        }

        return root;
    }

    /// <summary>
    /// Armageddon: a rock with a molten core showing through the cracks of its crust, trailing
    /// fire the way a comet is drawn - red outside, orange within, a white-hot streak in the
    /// middle, the outer flame forking into tongues, embers and grit falling behind. It used to
    /// be a smooth ball with an orange egg for a flame, then a single cone that ran straight
    /// at the icon camera and came out as an ice-cream cone.
    /// </summary>
    public static GameObject Meteor(PaintFn paint)
    {
        GameObject root = new GameObject("Meteor");

        Solid(root, "Rock", ShapeKit.Save(ShapeKit.Rock(0.165f, 2, 0.20f, 7, new Vector3(1f, 0.92f, 1f)), "Meteor_Rock"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.27f, 0.20f, 0.17f), false, "Met_Rock");

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.305f;
        paint(core, new Color(0.95f, 0.42f, 0.08f), true, "Met_Magma");

        // The trail has a frame of its own: z runs down the tail, y faces the side the icon is
        // shot from (BundleBuilder's view for this model), x lies across. The tail goes up and
        // away to the right of the icon, and the hotter layers sit on the y side of the cooler
        // ones, so from the front each layer shows inside the last instead of hiding in it.
        GameObject trail = new GameObject("Trail");
        trail.transform.SetParent(root.transform, false);
        trail.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.25f, 0.42f, 0.87f), new Vector3(1f, 0.35f, 0.10f));

        Color outer = new Color(0.97f, 0.34f, 0.07f), middle = new Color(1f, 0.60f, 0.12f), hot = new Color(1f, 0.92f, 0.55f);

        Tongue(trail, "Flame", paint, outer, "Met_Flame", 0.175f,
               new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.20f), new Vector3(0.012f, 0f, 0.40f),
               new Vector3(-0.008f, 0f, 0.56f), new Vector3(0.015f, 0f, 0.70f));
        Tongue(trail, "FlameForkA", paint, outer, "Met_Flame", 0.075f,
               new Vector3(0.03f, 0f, 0.10f), new Vector3(0.09f, 0f, 0.22f), new Vector3(0.125f, 0f, 0.34f),
               new Vector3(0.12f, 0f, 0.45f));
        Tongue(trail, "FlameForkB", paint, outer, "Met_Flame", 0.065f,
               new Vector3(-0.03f, 0f, 0.10f), new Vector3(-0.085f, 0f, 0.20f), new Vector3(-0.115f, 0f, 0.30f),
               new Vector3(-0.105f, 0f, 0.39f));
        Tongue(trail, "FlameMid", paint, middle, "Met_FlameMid", 0.13f,
               new Vector3(0f, 0.05f, 0.02f), new Vector3(0f, 0.05f, 0.18f), new Vector3(0.01f, 0.05f, 0.36f),
               new Vector3(0f, 0.05f, 0.52f));
        Tongue(trail, "FlameHot", paint, hot, "Met_TailCore", 0.09f,
               new Vector3(0f, 0.10f, 0.06f), new Vector3(0f, 0.10f, 0.18f), new Vector3(0.006f, 0.10f, 0.29f),
               new Vector3(0f, 0.10f, 0.40f));

        // Embers and grit left behind, a little off the line of the tail.
        float[,] ember = { { 0.17f, 0.02f, 0.52f, 0.022f }, { -0.14f, 0.03f, 0.47f, 0.017f }, { 0.05f, 0.06f, 0.80f, 0.020f }, { -0.05f, 0f, 0.88f, 0.013f } };
        for (int i = 0; i < ember.GetLength(0); i++)
        {
            GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            e.name = "Ember" + i;
            e.transform.SetParent(trail.transform, false);
            e.transform.localPosition = new Vector3(ember[i, 0], ember[i, 1], ember[i, 2]);
            e.transform.localScale = Vector3.one * (2f * ember[i, 3]);
            paint(e, new Color(1f, 0.78f, 0.30f), true, "Met_Ember");
        }
        Solid(trail, "Grit0", ShapeKit.Save(ShapeKit.Rock(0.032f, 1, 0.20f, 21, Vector3.one), "Meteor_Grit0"),
              new Vector3(0.21f, -0.02f, 0.33f), Quaternion.identity, paint, new Color(0.27f, 0.20f, 0.17f), false, "Met_Rock");
        Solid(trail, "Grit1", ShapeKit.Save(ShapeKit.Rock(0.022f, 1, 0.20f, 22, Vector3.one), "Meteor_Grit1"),
              new Vector3(-0.17f, 0f, 0.64f), Quaternion.identity, paint, new Color(0.27f, 0.20f, 0.17f), false, "Met_Rock");

        return root;
    }

    /// <summary>One tongue of the meteor's fire: a tube through the points, pointed at both ends.</summary>
    private static void Tongue(GameObject trail, string name, PaintFn paint, Color colour, string matName,
                               float width, params Vector3[] points)
    {
        Mesh m = ShapeKit.Sweep(ShapeKit.Smooth(points, 8),
                                t => width * Mathf.Pow(1f - t, 1.2f) * Mathf.Sqrt(Mathf.Clamp01(t / 0.04f)),
                                Vector3.up, 1f, 0.8f, 18);
        Solid(trail, name, ShapeKit.Save(m, "Meteor_" + name), Vector3.zero, Quaternion.identity, paint, colour, true, matName);
    }

    /// <summary>
    /// Key vacuum: a red drum vacuum cleaner with a ribbed hose and a steel wand, its nozzle
    /// down at the floor, and three big gold keys flying up into it on draughts of air.
    ///
    /// The old one was a funnel on a can, seen from the side, with two keys the size of
    /// crumbs - at icon size, a grey cone; a funnel seen from the front reads as a megaphone.
    /// A drum and a hose is what a vacuum cleaner looks like in every picture of one. The keys
    /// are flat, so each is turned to show the camera its face rather than its edge.
    /// The front is +x and the icon looks from -z, so it sucks to the right.
    /// </summary>
    public static GameObject Vacuum(PaintFn paint)
    {
        GameObject root = new GameObject("Vacuum");

        Color red = new Color(0.88f, 0.26f, 0.22f), trim = new Color(0.30f, 0.31f, 0.34f);
        Color steel = new Color(0.82f, 0.84f, 0.87f), gold = new Color(0.98f, 0.80f, 0.26f);

        // The machine has a parent of its own, so its size against the keys is one number.
        GameObject machine = new GameObject("Machine");
        machine.transform.SetParent(root.transform, false);
        const float scale = 1.1f;
        machine.transform.localScale = Vector3.one * scale;

        // The drum: a short round tub with a bumper, a dark domed lid and a carrying handle.
        const float cx = -0.16f;
        Vector2[] drum =
        {
            new Vector2(0f, -0.10f), new Vector2(0.105f, -0.10f), new Vector2(0.117f, -0.093f), new Vector2(0.122f, -0.08f),
            new Vector2(0.122f, 0.045f), new Vector2(0.117f, 0.060f), new Vector2(0.105f, 0.066f), new Vector2(0f, 0.066f),
        };
        Vector2[] lid =
        {
            new Vector2(0f, 0.062f), new Vector2(0.118f, 0.062f), new Vector2(0.121f, 0.070f), new Vector2(0.112f, 0.085f),
            new Vector2(0.085f, 0.100f), new Vector2(0.045f, 0.108f), new Vector2(0f, 0.110f),
        };
        Solid(machine, "Drum", ShapeKit.Save(ShapeKit.Lathe(drum, 40), "Vacuum_Drum"), new Vector3(cx, 0f, 0f), Quaternion.identity,
              paint, red, false, "Vac_Body");
        Solid(machine, "Lid", ShapeKit.Save(ShapeKit.Lathe(lid, 40), "Vacuum_Lid"), new Vector3(cx, 0f, 0f), Quaternion.identity,
              paint, trim, false, "Vac_Handle");
        Solid(machine, "Bumper", ShapeKit.Save(ShapeKit.Torus(0.122f, 0.014f, 64, 10), "Vacuum_Bumper"), new Vector3(cx, -0.082f, 0f),
              Quaternion.identity, paint, trim, false, "Vac_Handle");
        List<Vector3> carry = ShapeKit.Smooth(new[] { new Vector3(cx - 0.045f, 0.098f, 0f), new Vector3(cx - 0.02f, 0.145f, 0f),
                                                      new Vector3(cx + 0.02f, 0.145f, 0f), new Vector3(cx + 0.045f, 0.098f, 0f) }, 6);
        Solid(machine, "Carry", ShapeKit.Save(ShapeKit.Sweep(carry, t => 0.012f, Vector3.forward, 1f, 1f, 10), "Vacuum_Carry"),
              Vector3.zero, Quaternion.identity, paint, trim, false, "Vac_Handle");
        Solid(machine, "Button", ShapeKit.Save(ShapeKit.BevelCylinder(0.016f, 0.016f, 0.012f, 0.004f, 20), "Vacuum_Button"),
              new Vector3(cx + 0.06f, 0.103f, -0.05f), Quaternion.Euler(-25f, 0f, -20f), paint, new Color(0.80f, 0.45f, 0.15f), true, "Vac_Cap");

        // Two wheels at the bottom, the canister vacuum's own tell.
        Mesh vacWheel = ShapeKit.Save(ShapeKit.BevelCylinder(0.030f, 0.030f, 0.022f, 0.006f), "Vacuum_Wheel");
        for (int s2 = -1; s2 <= 1; s2 += 2)
            Solid(machine, s2 < 0 ? "WheelA" : "WheelB", vacWheel, new Vector3(cx + 0.06f, -0.098f, 0.108f * s2),
                  Quaternion.Euler(90f, 0f, 0f), paint, trim, false, "Vac_Handle");

        // The hose: ribbed, out of a port on the front of the drum, up and over to the wand.
        Vector3 port = new Vector3(cx + 0.115f, 0.02f, 0f);
        Solid(machine, "Port", ShapeKit.Save(ShapeKit.BevelCylinder(0.032f, 0.032f, 0.04f, 0.008f), "Vacuum_Port"), port,
              Quaternion.Euler(0f, 0f, -90f), paint, trim, false, "Vac_Handle");
        Vector3 wandTop = new Vector3(0.17f, 0.26f, 0f), wandEnd = new Vector3(0.25f, 0.04f, 0f);
        List<Vector3> hose = ShapeKit.Smooth(new[] { port, new Vector3(0.01f, 0.07f, 0f), new Vector3(0.03f, 0.17f, 0f),
                                                     new Vector3(0.08f, 0.25f, 0f), new Vector3(0.13f, 0.28f, 0f), wandTop }, 10);
        Solid(machine, "Hose", ShapeKit.Save(ShapeKit.Sweep(hose, t => 0.031f + 0.0045f * Mathf.Sin(t * Mathf.PI * 2f * 18f),
                                                         Vector3.forward, 1f, 1f, 14), "Vacuum_Hose"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.24f, 0.25f, 0.28f), false, "Vac_Hose");

        // The wand down to the floor, ending in a small flared nozzle with a dark mouth.
        Vector3 down = (wandEnd - wandTop).normalized;
        Quaternion along = Quaternion.FromToRotation(Vector3.up, down);
        Solid(machine, "Wand", ShapeKit.Save(ShapeKit.BevelCylinder(0.022f, 0.022f, (wandEnd - wandTop).magnitude, 0.005f, 20), "Vacuum_Wand"),
              (wandTop + wandEnd) * 0.5f, along, paint, steel, false, "Vac_Rim");
        Vector2[] nozzle =
        {
            new Vector2(0.024f, -0.01f), new Vector2(0.028f, 0.02f), new Vector2(0.040f, 0.045f), new Vector2(0.050f, 0.058f),
            new Vector2(0.050f, 0.064f), new Vector2(0.047f, 0.068f),
        };
        Vector2[] mouth = { new Vector2(0.047f, 0.068f), new Vector2(0.044f, 0.064f), new Vector2(0.030f, 0.047f), new Vector2(0f, 0.032f) };
        Solid(machine, "Nozzle", ShapeKit.Save(ShapeKit.Lathe(nozzle, 32), "Vacuum_Nozzle"), wandEnd, along, paint, trim, false, "Vac_Handle");
        Solid(machine, "Mouth", ShapeKit.Save(ShapeKit.Lathe(mouth, 32), "Vacuum_Mouth"), wandEnd, along,
              paint, new Color(0.07f, 0.07f, 0.09f), false, "Vac_Mouth");

        // Two big keys on their way in, each turned flat to the camera BundleBuilder puts on
        // this model - the keys are what the item is about, so they get the space.
        Vector3 viewer = new Vector3(0.45f, 0.5f, -1f);
        Mesh bow = ShapeKit.Save(ShapeKit.Torus(0.040f, 0.013f, 36, 10), "Vacuum_KeyBow");
        Mesh shaft = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.022f, 0.014f, 0.13f), 0.005f), "Vacuum_KeyShaft");
        Mesh bit = ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.032f, 0.014f, 0.016f), 0.004f), "Vacuum_KeyBit");
        Vector3[] at = { new Vector3(0.40f, -0.03f, 0f), new Vector3(0.44f, 0.11f, 0.03f) };
        Vector3[] lie = { new Vector3(1f, 0.55f, 0f), new Vector3(0.6f, -1f, 0f) };
        float[] size = { 0.95f, 0.82f };
        for (int i = 0; i < at.Length; i++)
        {
            GameObject key = new GameObject("Key" + i);
            key.transform.SetParent(root.transform, false);
            key.transform.localPosition = at[i];
            key.transform.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(lie[i], viewer), viewer);
            key.transform.localScale = Vector3.one * size[i];

            Solid(key, "Bow", bow, new Vector3(0f, 0f, -0.075f), Quaternion.identity, paint, gold, true, "Vac_Key");
            Solid(key, "Shaft", shaft, Vector3.zero, Quaternion.identity, paint, gold, true, "Vac_Key");
            Solid(key, "BitA", bit, new Vector3(0.022f, 0f, 0.035f), Quaternion.identity, paint, gold, true, "Vac_Key");
            Solid(key, "BitB", bit, new Vector3(0.022f, 0f, 0.056f), Quaternion.identity, paint, gold, true, "Vac_Key");
        }

        // Draughts of air curving into the nozzle, round the keys rather than across them.
        Vector3 inlet = (wandEnd + down * 0.05f) * scale;
        Vector3[] from = { new Vector3(0.53f, 0.21f, 0.02f), new Vector3(0.52f, -0.10f, 0.02f) };
        Vector3[] via = { new Vector3(0.40f, 0.26f, 0.02f), new Vector3(0.39f, -0.12f, 0.02f) };
        for (int i = 0; i < from.Length; i++)
        {
            Solid(root, "Draught" + i, ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Bezier(from[i], via[i], inlet, 20),
                                                                    t => 0.006f * ShapeKit.Capped(t, 0.2f), Vector3.forward, 1f, 1f, 8),
                                                     "Vacuum_Draught" + i),
                  Vector3.zero, Quaternion.identity, paint, new Color(0.78f, 0.92f, 1f), true, "Vac_Air");
        }

        return root;
    }

    /// <summary>
    /// Piggy bank: a glazed pink egg of a pig on four stubby legs that grow out of its belly,
    /// with a snout, ears, shiny eyes, a curly tail and a coin going into the slot.
    ///
    /// The old legs were four separate posts standing under a ball, a few millimetres short of
    /// touching it - a pig on stilts. Here the legs are short and thick, flare into the belly
    /// and are sunk well inside it, so the body sits on them.
    /// </summary>
    public static GameObject Piggy(PaintFn paint)
    {
        GameObject root = new GameObject("Piggy");

        Color pink = new Color(0.96f, 0.62f, 0.70f), deeper = new Color(0.93f, 0.52f, 0.61f), dark = new Color(0.10f, 0.07f, 0.08f);
        Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);

        // The body turned on a lathe along x, snout end at +x: an egg, fuller at the rump,
        // a little taller than it is wide.
        Vector2[] body =
        {
            new Vector2(0f, -0.200f), new Vector2(0.060f, -0.192f), new Vector2(0.105f, -0.165f), new Vector2(0.135f, -0.125f),
            new Vector2(0.150f, -0.070f), new Vector2(0.153f, -0.010f), new Vector2(0.148f, 0.050f), new Vector2(0.132f, 0.105f),
            new Vector2(0.108f, 0.150f), new Vector2(0.080f, 0.180f), new Vector2(0.050f, 0.196f), new Vector2(0f, 0.202f),
        };
        const float tall = 0.95f, wide = 0.92f;
        Solid(root, "Body", ShapeKit.Save(ShapeKit.Lathe(body, 40, tall, wide), "Piggy_Body"),
              Vector3.zero, alongX, paint, pink, false, "Pig_Body");

        // A point on the skin: x along the body, a degrees round from the top towards +z.
        System.Func<float, float, float, Vector3> skin = (x, a, lift) =>
        {
            float r = RadiusAt(body, x) + lift, ra = a * Mathf.Deg2Rad;
            return new Vector3(x, Mathf.Cos(ra) * r * tall, Mathf.Sin(ra) * r * wide);
        };

        // Legs: flat-footed stumps that widen into the belly, leaning out a touch.
        Vector2[] leg =
        {
            new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.039f, 0.007f), new Vector2(0.040f, 0.022f),
            new Vector2(0.038f, 0.050f), new Vector2(0.034f, 0.080f), new Vector2(0.024f, 0.100f), new Vector2(0f, 0.108f),
        };
        Mesh legMesh = ShapeKit.Save(ShapeKit.Lathe(leg, 24), "Piggy_Leg");
        for (int i = 0; i < 4; i++)
        {
            float x = (i < 2) ? 0.095f : -0.095f, side = (i % 2 == 0) ? 1f : -1f;
            Solid(root, "Leg" + i, legMesh, new Vector3(x, -0.165f, 0.072f * side), Quaternion.Euler(-10f * side, 0f, 0f),
                  paint, pink, false, "Pig_Body");
        }

        // Snout: a short oval disc on the front of the face, with two nostrils.
        GameObject snout = Solid(root, "Snout", ShapeKit.Save(ShapeKit.BevelCylinder(0.050f, 0.056f, 0.045f, 0.012f), "Piggy_Snout"),
                                 new Vector3(0.196f, -0.012f, 0f), alongX, paint, deeper, false, "Pig_Snout");
        Mesh nostril = ShapeKit.Save(ShapeKit.BevelCylinder(0.015f, 0.010f, 0.008f, 0.003f, 16), "Piggy_Nostril");
        for (int i = 0; i < 2; i++)
            Solid(snout, "Nostril" + i, nostril, new Vector3(0.002f, 0.022f, (i == 0) ? 0.019f : -0.019f), Quaternion.identity,
                  paint, dark, false, "Pig_Eye");

        // Ears: flattened cones leaning forward over the face, with a darker inside.
        Vector2[] earProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.042f, 0.003f), new Vector2(0.044f, 0.016f), new Vector2(0.034f, 0.040f),
            new Vector2(0.017f, 0.061f), new Vector2(0.004f, 0.071f), new Vector2(0f, 0.073f),
        };
        Mesh ear = ShapeKit.Save(ShapeKit.Lathe(earProfile, 20, 1f, 0.26f), "Piggy_Ear");
        for (int i = 0; i < 2; i++)
        {
            float side = (i == 0) ? 1f : -1f;
            GameObject e = Solid(root, "Ear" + i, ear, skin(0.115f, 40f * side, -0.01f),
                                 Quaternion.LookRotation(new Vector3(1f, -0.65f, 0.30f * side), new Vector3(0.60f, 1f, 0.40f * side)),
                                 paint, pink, false, "Pig_Ear");
            GameObject inner = Solid(e, "Inner", ear, new Vector3(0f, 0.012f, 0.007f), Quaternion.identity, paint, deeper, false, "Pig_EarInner");
            inner.transform.localScale = Vector3.one * 0.68f;

            // Eyes: glossy black, each with a glint so they look at you.
            Vector3 at = skin(0.150f, 52f * side, -0.004f);
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye" + i;
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = at;
            eye.transform.localScale = Vector3.one * 0.034f;
            paint(eye, dark, false, "Pig_Eye");

            GameObject glint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glint.name = "Glint" + i;
            glint.transform.SetParent(root.transform, false);
            glint.transform.localPosition = at + new Vector3(0.007f, 0.008f, 0.011f * side);
            glint.transform.localScale = Vector3.one * 0.011f;
            paint(glint, Color.white, true, "Pig_EyeShine");
        }

        // The slot along the top of the back, and a coin half-way in.
        Vector3 back = skin(-0.01f, 0f, 0f);
        Solid(root, "Slot", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.10f, 0.026f, 0.024f), 0.008f), "Piggy_Slot"),
              back + new Vector3(0f, -0.006f, 0f), Quaternion.identity, paint, new Color(0.30f, 0.20f, 0.24f), false, "Pig_Slot");
        Solid(root, "Coin", ShapeKit.Save(ShapeKit.BevelCylinder(0.052f, 0.052f, 0.012f, 0.0035f), "Piggy_Coin"),
              back + new Vector3(0f, 0.022f, 0f), Quaternion.Euler(90f, 0f, 0f), paint, new Color(0.95f, 0.80f, 0.25f), true, "Pig_Coin");

        // Curly tail, starting inside the rump.
        List<Vector3> curl = new List<Vector3>();
        for (int k = 0; k <= 40; k++)
        {
            float t = k / 40f, a = t * Mathf.PI * 3f;
            curl.Add(new Vector3(-0.185f - t * 0.07f, 0.035f + Mathf.Sin(a) * 0.024f, (Mathf.Cos(a) - 1f) * 0.024f));
        }
        Solid(root, "Tail", ShapeKit.Save(ShapeKit.Sweep(curl, t => 0.0105f * ShapeKit.Capped(t, 0.05f), Vector3.right, 1f, 1f, 8), "Piggy_Tail"),
              Vector3.zero, Quaternion.identity, paint, deeper, false, "Pig_Snout");

        return root;
    }

    /// <summary>Curse: a dark voodoo doll stuck with a pin.</summary>
    /// <summary>
    /// A voodoo doll with a pin in it.
    ///
    /// Body, head and arms were all the same brown, which at icon size is one brown blob in a
    /// vaguely person shape. The pin gives it a subject, and the stitched eyes and the lighter
    /// patch break the mass up enough to see limbs.
    /// </summary>
    public static GameObject Curse(PaintFn paint)
    {
        GameObject root = new GameObject("Curse");

        Color cloth = new Color(0.48f, 0.32f, 0.23f);
        Color patch = new Color(0.70f, 0.56f, 0.36f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.17f, 0.26f, 0.11f);
        paint(body, cloth, false, "Curse_Cloth");

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localScale = Vector3.one * 0.16f;
        head.transform.localPosition = new Vector3(0f, 0.21f, 0f);
        paint(head, patch, false, "Curse_Head");

        for (int i = 0; i < 2; i++)
        {
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm" + i;
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale = new Vector3(0.16f, 0.055f, 0.055f);
            arm.transform.localPosition = new Vector3((i == 0) ? -0.15f : 0.15f, 0.07f, 0f);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, (i == 0) ? 25f : -25f);
            paint(arm, patch, false, "Curse_Limb");

            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "Leg" + i;
            leg.transform.SetParent(root.transform, false);
            leg.transform.localScale = new Vector3(0.055f, 0.14f, 0.055f);
            leg.transform.localPosition = new Vector3((i == 0) ? -0.05f : 0.05f, -0.19f, 0f);
            leg.transform.localRotation = Quaternion.Euler(0f, 0f, (i == 0) ? 9f : -9f);
            paint(leg, patch, false, "Curse_Limb");

            // Crossed stitches for eyes.
            for (int k = 0; k < 2; k++)
            {
                GameObject stitch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stitch.name = "Stitch" + i + k;
                stitch.transform.SetParent(root.transform, false);
                stitch.transform.localScale = new Vector3(0.045f, 0.012f, 0.012f);
                stitch.transform.localPosition = new Vector3((i == 0) ? -0.045f : 0.045f, 0.235f, 0.075f);
                stitch.transform.localRotation = Quaternion.Euler(0f, 0f, (k == 0) ? 45f : -45f);
                paint(stitch, new Color(0.12f, 0.10f, 0.10f), false, "Curse_Stitch");
            }
        }

        // The pin. Long enough to be unmistakable, with a bright head so the eye lands on it.
        GameObject needle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        needle.name = "Needle";
        needle.transform.SetParent(root.transform, false);
        needle.transform.localScale = new Vector3(0.017f, 0.13f, 0.017f);
        needle.transform.localPosition = new Vector3(0.05f, 0.08f, 0.13f);
        // -18, not 18: with +18 the free end of the needle landed 8 cm from the pin head and
        // the head hung in the air beside the doll.
        needle.transform.localRotation = Quaternion.Euler(72f, 0f, -18f);
        paint(needle, new Color(0.85f, 0.87f, 0.90f), false, "Curse_Needle");

        GameObject pinHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pinHead.name = "PinHead";
        pinHead.transform.SetParent(root.transform, false);
        pinHead.transform.localScale = Vector3.one * 0.055f;
        pinHead.transform.localPosition = new Vector3(0.09f, 0.14f, 0.235f);
        paint(pinHead, new Color(0.92f, 0.18f, 0.22f), true, "Curse_PinHead");

        return root;
    }

    /// <summary>
    /// Freeze: a cluster of six-sided ice crystals growing out of a frosted lump, splaying
    /// outward. It was five flat-topped cylinders twisted like a pinwheel - a bundle of pipes.
    /// </summary>
    public static GameObject Freeze(PaintFn paint)
    {
        GameObject root = new GameObject("Freeze");

        // ring radius, angle, length, radius, lean - the centre one tallest and upright
        float[,] spec =
        {
            { 0f,    0f,   0.26f, 0.050f, 0f },
            { 0.09f, 0f,   0.19f, 0.042f, 26f },
            { 0.09f, 72f,  0.16f, 0.037f, 30f },
            { 0.09f, 144f, 0.21f, 0.044f, 24f },
            { 0.09f, 216f, 0.15f, 0.035f, 32f },
            { 0.09f, 288f, 0.18f, 0.040f, 28f },
        };

        for (int i = 0; i < spec.GetLength(0); i++)
        {
            float a = spec[i, 1] * Mathf.Deg2Rad, lean = spec[i, 4] * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 dir = radial * Mathf.Sin(lean) + Vector3.up * Mathf.Cos(lean);
            Mesh crystal = ShapeKit.Save(ShapeKit.Crystal(spec[i, 3], spec[i, 2], spec[i, 3] * 2.2f), "Freeze_Crystal" + i);
            Solid(root, "Spike" + i, crystal, radial * spec[i, 0] + Vector3.up * -0.09f,
                  Quaternion.FromToRotation(Vector3.up, dir), paint, new Color(0.63f, 0.87f, 0.97f), true, "Frz_Ice");
        }

        Solid(root, "Base", ShapeKit.Save(ShapeKit.Rock(0.12f, 1, 0.12f, 3, new Vector3(1.3f, 0.45f, 1.3f)), "Freeze_Base"),
              new Vector3(0f, -0.10f, 0f), Quaternion.identity, paint, new Color(0.86f, 0.94f, 1f), false, "Frz_Base");

        return root;
    }

    /// <summary>
    /// Double move: two dice, every face carrying its value (opposite faces adding up to
    /// seven), edges rounded and pips flat on the faces. Only two faces had pips before - the
    /// "you got an item" popup spins the model - and the pips were half-balls, like rivets.
    /// </summary>
    public static GameObject DoubleDice(PaintFn paint)
    {
        GameObject root = new GameObject("DoubleDice");

        Mesh body = ShapeKit.Save(ShapeKit.RoundedBox(Vector3.one * 0.24f, 0.035f, 3), "DoubleDice_Body");
        Mesh pip = ShapeKit.Save(ShapeKit.BevelCylinder(0.021f, 0.021f, 0.008f, 0.003f, 20), "DoubleDice_Pip");

        for (int i = 0; i < 2; i++)
        {
            Vector3 at = new Vector3(i * 0.10f, i * 0.22f, i * -0.06f);
            Quaternion turn = Quaternion.Euler(0f, i * 22f, i * 8f);
            Solid(root, "Die" + i, body, at, turn, paint,
                  (i == 0) ? new Color(0.93f, 0.90f, 0.82f) : new Color(0.85f, 0.88f, 0.95f), false, "DblDie_Body" + i);

            Face(paint, root, pip, at, turn, i, Vector3.up, 3);
            Face(paint, root, pip, at, turn, i, Vector3.down, 4);
            Face(paint, root, pip, at, turn, i, Vector3.forward, 2);
            Face(paint, root, pip, at, turn, i, Vector3.back, 5);
            Face(paint, root, pip, at, turn, i, Vector3.right, 1);
            Face(paint, root, pip, at, turn, i, Vector3.left, 6);
        }

        return root;
    }

    /// <summary>The pips of one face of a die 0.24 across placed at <paramref name="at"/>.</summary>
    private static void Face(PaintFn paint, GameObject root, Mesh pip, Vector3 at, Quaternion turn,
                             int dieIndex, Vector3 normal, int count)
    {
        const float o = 0.06f, half = 0.12f;
        Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
        Vector3 v = Vector3.Cross(normal, u);

        Vector2[] spots;
        switch (count)
        {
            case 1: spots = new[] { Vector2.zero }; break;
            case 2: spots = new[] { new Vector2(-o, o), new Vector2(o, -o) }; break;
            case 3: spots = new[] { new Vector2(-o, -o), Vector2.zero, new Vector2(o, o) }; break;
            case 4: spots = new[] { new Vector2(-o, -o), new Vector2(-o, o), new Vector2(o, -o), new Vector2(o, o) }; break;
            case 5: spots = new[] { new Vector2(-o, -o), new Vector2(-o, o), Vector2.zero, new Vector2(o, -o), new Vector2(o, o) }; break;
            default: spots = new[] { new Vector2(-o, -o), new Vector2(-o, 0f), new Vector2(-o, o),
                                     new Vector2(o, -o), new Vector2(o, 0f), new Vector2(o, o) }; break;
        }

        Quaternion facing = turn * Quaternion.FromToRotation(Vector3.up, normal);
        for (int p = 0; p < spots.Length; p++)
        {
            Vector3 local = normal * (half - 0.001f) + u * spots[p].x + v * spots[p].y;
            Solid(root, "Pip" + dieIndex + "_" + count + "_" + p, pip, at + turn * local, facing,
                  paint, new Color(0.80f, 0.14f, 0.14f), false, "DblDie_Pip");
        }
    }

    /// <summary>
    /// A grappling hook: a shaft, three hooks curling out and back down towards the rope with
    /// pointed tips, an eye at the bottom and a few loose coils of rope. Prongs splaying up
    /// and out with spear points on them read as a trident; what says "grappling hook" is the
    /// curl back towards the rope.
    /// </summary>
    public static GameObject Grapple(PaintFn paint)
    {
        GameObject root = new GameObject("Grapple");
        Color steel = new Color(0.55f, 0.57f, 0.60f);

        Solid(root, "Shaft", ShapeKit.Save(ShapeKit.BevelCylinder(0.034f, 0.034f, 0.40f, 0.006f), "Grapple_Shaft"),
              new Vector3(0f, 0.02f, 0f), Quaternion.identity, paint, steel, false, "Grp_Metal");

        GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "Knob";
        knob.transform.SetParent(root.transform, false);
        knob.transform.localScale = Vector3.one * 0.10f;
        knob.transform.localPosition = new Vector3(0f, 0.215f, 0f);
        paint(knob, steel, false, "Grp_Metal");

        Vector2[] curve =
        {
            new Vector2(0f, 0.19f), new Vector2(0.07f, 0.255f), new Vector2(0.15f, 0.27f), new Vector2(0.215f, 0.225f),
            new Vector2(0.235f, 0.15f), new Vector2(0.215f, 0.085f), new Vector2(0.18f, 0.05f),
        };
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 15f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            List<Vector3> pts = new List<Vector3>();
            foreach (Vector2 p in curve) pts.Add(dir * p.x + Vector3.up * p.y);
            Mesh hook = ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Smooth(pts, 6),
                                                     t => 0.024f * (1f - 0.3f * t) * Mathf.Sqrt(Mathf.Clamp01((1f - t) / 0.12f)),
                                                     Vector3.Cross(dir, Vector3.up), 1f, 1f, 14), "Grapple_Hook" + i);
            Solid(root, "Prong" + i, hook, Vector3.zero, Quaternion.identity, paint, new Color(0.66f, 0.68f, 0.72f), false, "Grp_Prong");
        }

        Solid(root, "Ring", ShapeKit.Save(ShapeKit.Torus(0.045f, 0.011f, 48, 10), "Grapple_Ring"),
              new Vector3(0f, -0.215f, 0f), Quaternion.Euler(90f, 0f, 0f), paint, new Color(0.50f, 0.52f, 0.56f), false, "Grp_Ring");

        // Loose coils of rope hanging from the eye.
        List<Vector3> rope = new List<Vector3> { new Vector3(0f, -0.255f, 0f) };
        for (int k = 0; k <= 96; k++)
        {
            float t = k / 96f, ang = t * Mathf.PI * 6f;
            rope.Add(new Vector3(Mathf.Cos(ang) * 0.07f, -0.29f - t * 0.11f, Mathf.Sin(ang) * 0.07f));
        }
        Solid(root, "Coil", ShapeKit.Save(ShapeKit.Sweep(rope, t => 0.012f * ShapeKit.Capped(t, 0.01f), Vector3.up, 1f, 1f, 8), "Grapple_Rope"),
              Vector3.zero, Quaternion.identity, paint, new Color(0.72f, 0.60f, 0.36f), false, "Grp_Rope");

        return root;
    }

    /// <summary>Kick: a boot.</summary>
    /// <summary>
    /// A boot. The shaft, foot and sole were all one brown before, so the whole thing fused
    /// into an L-shaped block; the shape only becomes a boot once the cuff, toe and heel are
    /// told apart by tone.
    /// </summary>
    public static GameObject Boot(PaintFn paint)
    {
        GameObject root = new GameObject("Boot");

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.17f, 0.25f, 0.17f);
        shaft.transform.localPosition = new Vector3(-0.06f, 0.07f, 0f);
        paint(shaft, new Color(0.44f, 0.27f, 0.16f), false, "Boot_Leather");

        GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuff.name = "Cuff";
        cuff.transform.SetParent(root.transform, false);
        cuff.transform.localScale = new Vector3(0.21f, 0.07f, 0.21f);
        cuff.transform.localPosition = new Vector3(-0.06f, 0.21f, 0f);
        paint(cuff, new Color(0.78f, 0.66f, 0.48f), false, "Boot_Cuff");

        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foot.name = "Foot";
        foot.transform.SetParent(root.transform, false);
        foot.transform.localScale = new Vector3(0.34f, 0.12f, 0.18f);
        foot.transform.localPosition = new Vector3(0.04f, -0.10f, 0f);
        paint(foot, new Color(0.38f, 0.23f, 0.14f), false, "Boot_Foot");

        GameObject toe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        toe.name = "Toe";
        toe.transform.SetParent(root.transform, false);
        toe.transform.localScale = new Vector3(0.14f, 0.13f, 0.18f);
        toe.transform.localPosition = new Vector3(0.18f, -0.10f, 0f);
        paint(toe, new Color(0.62f, 0.42f, 0.24f), false, "Boot_Toe");

        GameObject sole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sole.name = "Sole";
        sole.transform.SetParent(root.transform, false);
        sole.transform.localScale = new Vector3(0.40f, 0.05f, 0.19f);
        sole.transform.localPosition = new Vector3(0.05f, -0.17f, 0f);
        paint(sole, new Color(0.16f, 0.15f, 0.14f), false, "Boot_Sole");

        GameObject heel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heel.name = "Heel";
        heel.transform.SetParent(root.transform, false);
        heel.transform.localScale = new Vector3(0.11f, 0.07f, 0.19f);
        heel.transform.localPosition = new Vector3(-0.10f, -0.22f, 0f);
        paint(heel, new Color(0.16f, 0.15f, 0.14f), false, "Boot_Sole");

        return root;
    }

    /// <summary>Start ticket: a stamped ticket with an arrow.</summary>
    public static GameObject Ticket(PaintFn paint)
    {
        GameObject root = new GameObject("Ticket");

        GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = "Card";
        card.transform.SetParent(root.transform, false);
        card.transform.localScale = new Vector3(0.38f, 0.02f, 0.22f);
        paint(card, new Color(0.95f, 0.90f, 0.70f), false, "Tick_Card");

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "Stripe";
        stripe.transform.SetParent(root.transform, false);
        stripe.transform.localScale = new Vector3(0.05f, 0.024f, 0.23f);
        stripe.transform.localPosition = new Vector3(-0.13f, 0.001f, 0f);
        paint(stripe, new Color(0.80f, 0.25f, 0.25f), false, "Tick_Stripe");

        // Arrow pointing back the way you came.
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "ArrowShaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.17f, 0.026f, 0.045f);
        shaft.transform.localPosition = new Vector3(0.06f, 0.002f, 0f);
        paint(shaft, new Color(0.25f, 0.35f, 0.60f), false, "Tick_Arrow");

        GameObject headA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        headA.name = "ArrowHead";
        headA.transform.SetParent(root.transform, false);
        headA.transform.localScale = new Vector3(0.09f, 0.026f, 0.09f);
        headA.transform.localPosition = new Vector3(-0.04f, 0.002f, 0f);
        headA.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        paint(headA, new Color(0.25f, 0.35f, 0.60f), false, "Tick_Arrow");

        return root;
    }

    /// <summary>Land mine: a squat disc with a pressure plate and prongs.</summary>
    public static GameObject Mine(PaintFn paint)
    {
        GameObject root = new GameObject("Mine");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.30f, 0.07f, 0.30f);
        paint(body, new Color(0.30f, 0.33f, 0.28f), false, "Mine_Body");

        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plate.name = "Plate";
        plate.transform.SetParent(root.transform, false);
        plate.transform.localScale = new Vector3(0.16f, 0.04f, 0.16f);
        plate.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        paint(plate, new Color(0.75f, 0.20f, 0.18f), true, "Mine_Plate");

        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            GameObject prong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            prong.name = "Prong" + i;
            prong.transform.SetParent(root.transform, false);
            prong.transform.localScale = new Vector3(0.022f, 0.07f, 0.022f);
            // Rooted in the top face. At radius 0.22 they floated 3 cm clear of a body of
            // radius 0.15 - 6.6 cm on the board, where the mine is scaled up 2.2 times.
            prong.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.12f, 0.10f, Mathf.Sin(a) * 0.12f);
            prong.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 25f, 0f, Mathf.Cos(a) * -25f);
            paint(prong, new Color(0.45f, 0.47f, 0.44f), false, "Mine_Prong");
        }

        return root;
    }

    /// <summary>Poison flask for corrupting a board space.</summary>
    public static GameObject Poison(PaintFn paint)
    {
        GameObject root = new GameObject("Poison");

        GameObject flask = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flask.name = "Flask";
        flask.transform.SetParent(root.transform, false);
        flask.transform.localScale = new Vector3(0.26f, 0.24f, 0.26f);
        paint(flask, new Color(0.45f, 0.85f, 0.30f), true, "Poi_Liquid");

        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        neck.name = "Neck";
        neck.transform.SetParent(root.transform, false);
        neck.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
        neck.transform.localPosition = new Vector3(0f, 0.19f, 0f);
        paint(neck, new Color(0.72f, 0.80f, 0.74f), false, "Poi_Glass");

        GameObject cork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cork.name = "Cork";
        cork.transform.SetParent(root.transform, false);
        cork.transform.localScale = new Vector3(0.07f, 0.04f, 0.07f);
        cork.transform.localPosition = new Vector3(0f, 0.29f, 0f);
        paint(cork, new Color(0.60f, 0.44f, 0.24f), false, "Poi_Cork");

        // Bubbles, so it reads as something nasty rather than a green ball. They sit on the
        // surface of the liquid; placed by hand, the top one ended up outside the glass beside
        // a corked neck.
        for (int i = 0; i < 3; i++)
        {
            float x = -0.05f + i * 0.05f, y = 0.03f + i * 0.03f;
            float z = 0.13f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / 0.13f) * (x / 0.13f) - (y / 0.12f) * (y / 0.12f))) * 0.92f;

            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Bubble" + i;
            b.transform.SetParent(root.transform, false);
            b.transform.localScale = Vector3.one * (0.05f - i * 0.008f);
            b.transform.localPosition = new Vector3(x, y, z);
            paint(b, new Color(0.75f, 0.98f, 0.55f), true, "Poi_Bubble");
        }

        return root;
    }

    /// <summary>
    /// Signpost: a post set in a stone, with two arrow boards pointing opposite ways and
    /// knocked crooked, because the whole point is that they are lying. Plain planks read as
    /// a noticeboard; the arrow ends are what make a board point.
    /// </summary>
    public static GameObject Signpost(PaintFn paint)
    {
        GameObject root = new GameObject("Signpost");

        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        post.transform.SetParent(root.transform, false);
        post.transform.localScale = new Vector3(0.05f, 0.26f, 0.05f);
        paint(post, new Color(0.52f, 0.36f, 0.20f), false, "Sign_Post");

        Solid(root, "Cap", ShapeKit.Save(ShapeKit.BevelCylinder(0.034f, 0.034f, 0.022f, 0.009f), "Signpost_Cap"),
              new Vector3(0f, 0.265f, 0f), Quaternion.identity, paint, new Color(0.40f, 0.27f, 0.15f), false, "Sign_Cap");
        Solid(root, "Stone", ShapeKit.Save(ShapeKit.Rock(0.075f, 1, 0.14f, 5, new Vector3(1.35f, 0.55f, 1.25f)), "Signpost_Stone"),
              new Vector3(0f, -0.255f, 0f), Quaternion.identity, paint, new Color(0.58f, 0.58f, 0.56f), false, "Sign_Stone");

        // Each board starts inside the post and runs out to its point along +x, so turning it
        // about the post can never swing it off - the old boards were centred and had to be
        // hung back on by hand after every change of angle.
        const float length = 0.33f, half = 0.043f, point = 0.05f;
        Vector2[] outline =
        {
            new Vector2(-0.012f, -half), new Vector2(length - point, -half), new Vector2(length, 0f),
            new Vector2(length - point, half), new Vector2(-0.012f, half),
        };
        Mesh board = ShapeKit.Save(ShapeKit.Extrude(outline, 0.024f), "Signpost_Board");

        Solid(root, "Arm0", board, new Vector3(0f, 0.17f, 0f), Quaternion.Euler(0f, 198f, -7f),
              paint, new Color(0.88f, 0.82f, 0.62f), false, "Sign_Arm0");
        Solid(root, "Arm1", board, new Vector3(0f, 0.05f, 0f), Quaternion.Euler(0f, -24f, 9f),
              paint, new Color(0.80f, 0.72f, 0.52f), false, "Sign_Arm1");

        return root;
    }

    /// <summary>
    /// Life magnet: a black horseshoe with dark red poles. Reads as a magnet at a glance,
    /// but the colouring says it pulls something other than keys.
    /// </summary>
    public static GameObject LifeMagnet(PaintFn paint)
    {
        GameObject root = MagnetMesh.BuildPrefabRoot("Assets/Meshes/LifeMagnet.asset");
        paint(root.transform.Find("Body").gameObject,
              new Color(0.10f, 0.10f, 0.12f), false, "LifeMag_Body");

        for (int i = 0; i < 2; i++)
        {
            bool left = (i == 0);
            Vector3 pos;
            Quaternion rot;
            MagnetMesh.PoleEnd(left, out pos, out rot);

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tip.name = left ? "PoleL" : "PoleR";
            tip.transform.SetParent(root.transform, false);
            tip.transform.localScale = new Vector3(0.20f, 0.07f, 0.20f);
            tip.transform.localRotation = rot;
            tip.transform.localPosition = pos + rot * new Vector3(0f, 0.05f, 0f);
            paint(tip, new Color(0.62f, 0.06f, 0.10f), true, "LifeMag_Pole");
        }

        return root;
    }

    /// <summary>
    /// Ricochet: a brass blunderbuss - six pellets in a fan is a scattergun's job - with shot
    /// flying out of the bell and one pellet glancing off a brick in a spark.
    ///
    /// The previous model drew the bounce as a diagram, a grey can for the gun and a plank for
    /// the wall. It said "bounce" and nothing else, and in a player's hand it was a plank. The
    /// bounce is still there, but as something happening to a gun rather than instead of one.
    /// The barrel points along +x and the icon looks from -z, so on the icon it points right.
    /// </summary>
    public static GameObject Ricochet(PaintFn paint)
    {
        GameObject root = new GameObject("Ricochet");

        Color brass = new Color(0.86f, 0.66f, 0.26f), iron = new Color(0.26f, 0.27f, 0.30f), wood = new Color(0.50f, 0.30f, 0.15f);

        GameObject gun = new GameObject("Gun");
        gun.transform.SetParent(root.transform, false);
        gun.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);   // muzzle up a little

        // Barrel turned on a lathe from the breech: a plain tube flaring into the bell, and the
        // inside of the bell turned too, so the mouth has a lip and a hole rather than a lid.
        Vector2[] barrel =
        {
            new Vector2(0f, 0f), new Vector2(0.044f, 0f), new Vector2(0.046f, 0.025f), new Vector2(0.037f, 0.05f),
            new Vector2(0.034f, 0.20f), new Vector2(0.038f, 0.26f), new Vector2(0.052f, 0.31f), new Vector2(0.078f, 0.345f),
            new Vector2(0.088f, 0.362f), new Vector2(0.080f, 0.370f), new Vector2(0.066f, 0.352f), new Vector2(0.050f, 0.33f),
            new Vector2(0.030f, 0.31f), new Vector2(0.028f, 0.305f), new Vector2(0f, 0.305f),
        };
        const float breech = -0.05f;
        Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);
        Solid(gun, "Barrel", ShapeKit.Save(ShapeKit.Lathe(barrel, 32), "Ricochet_Barrel"),
              new Vector3(breech, 0f, 0f), alongX, paint, brass, false, "Ric_Brass");
        Solid(gun, "Bore", ShapeKit.Save(ShapeKit.BevelCylinder(0.027f, 0.027f, 0.002f, 0.0005f), "Ricochet_Bore"),
              new Vector3(breech + 0.306f, 0f, 0f), alongX, paint, new Color(0.08f, 0.07f, 0.06f), false, "Ric_Bore");

        Mesh band = ShapeKit.Save(ShapeKit.Torus(0.037f, 0.0055f, 40, 10), "Ricochet_Band");
        Solid(gun, "Band0", band, new Vector3(breech + 0.07f, 0f, 0f), alongX, paint, iron, false, "Ric_Metal");
        Solid(gun, "Band1", band, new Vector3(breech + 0.20f, 0f, 0f), alongX, paint, iron, false, "Ric_Metal");

        // Stock swept back and down from the breech, deepening toward the butt, and a fore-end
        // under the barrel. Flattened across, as a stock is.
        List<Vector3> stock = ShapeKit.Smooth(new[] { new Vector3(-0.02f, -0.005f, 0f), new Vector3(-0.12f, -0.03f, 0f),
                                                      new Vector3(-0.22f, -0.075f, 0f), new Vector3(-0.31f, -0.12f, 0f) }, 8);
        Solid(gun, "Stock", ShapeKit.Save(ShapeKit.Sweep(stock, t => Mathf.Lerp(0.038f, 0.07f, t) * ShapeKit.Capped(t, 0.06f),
                                                         Vector3.forward, 1f, 0.55f, 20), "Ricochet_Stock"),
              Vector3.zero, Quaternion.identity, paint, wood, false, "Ric_Grip");
        List<Vector3> fore = ShapeKit.Smooth(new[] { new Vector3(-0.04f, -0.028f, 0f), new Vector3(0.08f, -0.03f, 0f),
                                                     new Vector3(0.19f, -0.028f, 0f) }, 8);
        Solid(gun, "ForeEnd", ShapeKit.Save(ShapeKit.Sweep(fore, t => 0.028f * ShapeKit.Capped(t, 0.08f), Vector3.forward, 0.8f, 0.95f, 18),
                                            "Ricochet_ForeEnd"),
              Vector3.zero, Quaternion.identity, paint, wood, false, "Ric_Grip");

        // Lock and hammer on the side the icon sees, and the trigger guard below.
        Solid(gun, "Lock", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.075f, 0.032f, 0.012f), 0.005f), "Ricochet_Lock"),
              new Vector3(-0.04f, 0.004f, -0.026f), Quaternion.identity, paint, iron, false, "Ric_Metal");
        List<Vector3> hammer = ShapeKit.Smooth(new[] { new Vector3(-0.03f, 0.012f, -0.02f), new Vector3(-0.05f, 0.045f, -0.02f),
                                                       new Vector3(-0.075f, 0.06f, -0.02f) }, 6);
        Solid(gun, "Hammer", ShapeKit.Save(ShapeKit.Sweep(hammer, t => 0.009f * Mathf.Lerp(0.7f, 1.2f, t) * ShapeKit.Capped(t, 0.15f),
                                                          Vector3.forward, 1f, 0.7f, 10), "Ricochet_Hammer"),
              Vector3.zero, Quaternion.identity, paint, iron, false, "Ric_Metal");
        Solid(gun, "Guard", ShapeKit.Save(ShapeKit.Torus(0.022f, 0.0045f, 32, 8), "Ricochet_Guard"),
              new Vector3(-0.085f, -0.048f, 0f), Quaternion.Euler(90f, 0f, 0f), paint, iron, false, "Ric_Metal");

        // The shot, in the gun's frame: two pellets flying on, the third glancing off the
        // underside of a brick - incoming and outgoing at the same angle to it.
        Vector3 muzzle = new Vector3(breech + 0.37f, 0f, 0f), hit = new Vector3(0.50f, 0.13f, 0f);
        Vector3[][] shots =
        {
            new[] { muzzle, muzzle + new Vector3(Mathf.Cos(4f * Mathf.Deg2Rad), Mathf.Sin(4f * Mathf.Deg2Rad), 0f) * 0.24f },
            new[] { muzzle, muzzle + new Vector3(Mathf.Cos(-14f * Mathf.Deg2Rad), Mathf.Sin(-14f * Mathf.Deg2Rad), 0f) * 0.19f },
            new[] { muzzle, hit, new Vector3(0.62f, 0.043f, 0f) },
        };
        for (int s = 0; s < shots.Length; s++)
        {
            Vector3[] path = shots[s];
            for (int k = 0; k < path.Length - 1; k++)
            {
                Mesh streak = ShapeKit.Sweep(new[] { path[k], Vector3.Lerp(path[k], path[k + 1], 0.5f), path[k + 1] },
                                             t => 0.013f * Mathf.Pow(t, 0.8f), Vector3.forward, 1f, 1f, 12);
                Solid(gun, "Streak" + s + k, ShapeKit.Save(streak, "Ricochet_Streak" + s + k), Vector3.zero, Quaternion.identity,
                      paint, new Color(1f, 0.86f, 0.42f), true, "Ric_Trail");
            }

            GameObject pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pellet.name = "Pellet" + s;
            pellet.transform.SetParent(gun.transform, false);
            pellet.transform.localPosition = path[path.Length - 1];
            pellet.transform.localScale = Vector3.one * 0.044f;
            paint(pellet, new Color(0.95f, 0.72f, 0.20f), true, "Ric_Pellet");
        }

        Solid(gun, "Brick", ShapeKit.Save(ShapeKit.RoundedBox(new Vector3(0.16f, 0.05f, 0.09f), 0.008f), "Ricochet_Brick"),
              hit + new Vector3(0.005f, 0.025f, 0f), Quaternion.identity, paint, new Color(0.72f, 0.38f, 0.27f), false, "Ric_Wall");

        // The spark: a hot dot with short rays fanning down from the brick.
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Spark";
        flash.transform.SetParent(gun.transform, false);
        flash.transform.localPosition = hit;
        flash.transform.localScale = Vector3.one * 0.05f;
        paint(flash, new Color(1f, 0.94f, 0.55f), true, "Ric_Spark");
        for (int k = 0; k < 5; k++)
        {
            float a = (-20f - k * 35f) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Mesh ray = ShapeKit.Sweep(new[] { hit + d * 0.01f, hit + d * 0.035f, hit + d * 0.065f }, t => 0.008f * (1f - t),
                                      Vector3.forward, 1f, 1f, 8);
            Solid(gun, "Ray" + k, ShapeKit.Save(ray, "Ricochet_Ray" + k), Vector3.zero, Quaternion.identity,
                  paint, new Color(1f, 0.94f, 0.55f), true, "Ric_Spark");
        }

        return root;
    }

    /// <summary>
    /// A boomerang: one flat, gently tapering limb swept round a smooth elbow of about 110
    /// degrees, with rounded tips and two painted bands. Two planks at 64 degrees with a ball
    /// at the joint read as a pair of compasses - and this is the model that spins across
    /// half the board.
    /// </summary>
    public static GameObject Boomerang(PaintFn paint)
    {
        GameObject root = new GameObject("Boomerang");

        Vector3 a = new Vector3(-0.32f, 0f, 0.26f), control = new Vector3(0f, 0f, -0.18f), b = new Vector3(0.32f, 0f, 0.26f);
        System.Func<float, float> width = t => 0.055f * (0.85f + 0.15f * Mathf.Sin(Mathf.PI * t));

        Mesh body = ShapeKit.Save(ShapeKit.Sweep(ShapeKit.Bezier(a, control, b, 64), t => width(t) * ShapeKit.Capped(t),
                                                 Vector3.up, 1f, 0.32f, 18), "Boomerang_Body");
        Solid(root, "Body", body, Vector3.zero, Quaternion.identity, paint, new Color(0.62f, 0.41f, 0.21f), false, "Boom_Wood");

        // Painted bands near the tips, a hair proud of the wood.
        for (int i = 0; i < 2; i++)
        {
            float t0 = (i == 0) ? 0.17f : 0.77f, t1 = t0 + 0.06f;
            List<Vector3> part = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = Mathf.Lerp(t0, t1, k / 8f), u = 1f - t;
                part.Add(u * u * a + 2f * u * t * control + t * t * b);
            }
            Mesh band = ShapeKit.Save(ShapeKit.Sweep(part, t => width(Mathf.Lerp(t0, t1, t)) * 1.06f, Vector3.up, 1f, 0.36f, 18),
                                      "Boomerang_Band" + i);
            Solid(root, "Band" + i, band, Vector3.zero, Quaternion.identity, paint, new Color(0.82f, 0.20f, 0.14f), false, "Boom_Paint");
        }

        return root;
    }
}
