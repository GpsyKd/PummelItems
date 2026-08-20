using UnityEngine;

/// <summary>
/// Models for the thrown-projectile family. Each is assembled from primitives; the shapes
/// are deliberately simple and strongly coloured so they read at board-camera distance.
/// </summary>
public static class GroupAModels
{
    public delegate void PaintFn(GameObject go, Color c, bool emissive, string matName);

    /// <summary>Pineapple grenade: body, ridges and a spoon.</summary>
    public static GameObject Grenade(PaintFn paint)
    {
        GameObject root = new GameObject("Grenade");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.34f, 0.42f, 0.34f);
        paint(body, new Color(0.24f, 0.32f, 0.20f), false, "Gren_Body");

        for (int i = 0; i < 3; i++)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Band" + i;
            band.transform.SetParent(root.transform, false);
            band.transform.localScale = new Vector3(0.36f, 0.012f, 0.36f);
            band.transform.localPosition = new Vector3(0f, -0.10f + i * 0.10f, 0f);
            paint(band, new Color(0.15f, 0.20f, 0.13f), false, "Gren_Band");
        }

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f);
        cap.transform.localPosition = new Vector3(0f, 0.23f, 0f);
        paint(cap, new Color(0.55f, 0.50f, 0.42f), false, "Gren_Cap");

        GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lever.name = "Lever";
        lever.transform.SetParent(root.transform, false);
        lever.transform.localScale = new Vector3(0.05f, 0.20f, 0.02f);
        lever.transform.localPosition = new Vector3(0.15f, 0.16f, 0f);
        lever.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        paint(lever, new Color(0.62f, 0.58f, 0.48f), false, "Gren_Cap");

        return root;
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

    /// <summary>Sticky bomb: a gooey blob with a blinking charge on top.</summary>
    public static GameObject Sticky(PaintFn paint)
    {
        GameObject root = new GameObject("Sticky");

        GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blob.name = "Blob";
        blob.transform.SetParent(root.transform, false);
        blob.transform.localScale = new Vector3(0.42f, 0.34f, 0.42f);
        paint(blob, new Color(0.55f, 0.85f, 0.25f), false, "Sticky_Goo");

        // Irregular lumps so it looks soft rather than like a ball.
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            GameObject lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lump.name = "Lump" + i;
            lump.transform.SetParent(root.transform, false);
            lump.transform.localScale = Vector3.one * (0.16f + 0.04f * (i % 3));
            lump.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.18f, -0.06f, Mathf.Sin(a) * 0.18f);
            paint(lump, new Color(0.48f, 0.78f, 0.22f), false, "Sticky_Goo2");
        }

        GameObject charge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        charge.name = "Charge";
        charge.transform.SetParent(root.transform, false);
        charge.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
        charge.transform.localPosition = new Vector3(0f, 0.19f, 0f);
        paint(charge, new Color(0.85f, 0.15f, 0.12f), true, "Sticky_Charge");

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
        paint(cap, new Color(0.55f, 0.80f, 0.94f), false, "Ice_Body");

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

    /// <summary>Swap inventories: two bags with arrows between them, suggested by two
    /// interlocking rings.</summary>
    /// <summary>
    /// Two containers and a pair of arrows crossing between them.
    ///
    /// The single straight bar it had before was not an arrow - no head, no second direction -
    /// so the picture was two boxes with a stick over them. An exchange needs both arrows.
    /// </summary>
    public static GameObject SwapBag(PaintFn paint)
    {
        GameObject root = new GameObject("SwapBag");

        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0) ? -0.19f : 0.19f;

            GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.name = "Bag" + i;
            bag.transform.SetParent(root.transform, false);
            bag.transform.localScale = new Vector3(0.22f, 0.24f, 0.20f);
            bag.transform.localPosition = new Vector3(x, -0.10f, 0f);
            bag.transform.localRotation = Quaternion.Euler(0f, 0f, (i == 0) ? 8f : -8f);
            paint(bag, (i == 0) ? new Color(0.75f, 0.35f, 0.25f) : new Color(0.25f, 0.45f, 0.75f),
                  false, "Swap_Bag" + i);

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck" + i;
            neck.transform.SetParent(root.transform, false);
            neck.transform.localScale = new Vector3(0.10f, 0.035f, 0.10f);
            neck.transform.localPosition = new Vector3(x, 0.02f, 0f);
            paint(neck, new Color(0.72f, 0.62f, 0.40f), false, "Swap_Neck");
        }

        // One arrow each way, offset so they read as two rather than one double-ended bar.
        for (int i = 0; i < 2; i++)
        {
            float y = (i == 0) ? 0.22f : 0.10f;
            float sign = (i == 0) ? 1f : -1f;

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar" + i;
            bar.transform.SetParent(root.transform, false);
            bar.transform.localScale = new Vector3(0.30f, 0.045f, 0.045f);
            bar.transform.localPosition = new Vector3(0f, y, 0f);
            paint(bar, (i == 0) ? new Color(0.95f, 0.85f, 0.25f) : new Color(0.60f, 0.78f, 0.95f),
                  true, "Swap_Arrow" + i);

            GameObject head = ConeMesh.Object("SwapHead" + i, 0.085f, 0.004f, 0.13f);
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(sign * 0.15f, y, 0f);
            head.transform.localRotation = Quaternion.Euler(0f, sign * 90f, 0f);
            paint(head, (i == 0) ? new Color(0.95f, 0.85f, 0.25f) : new Color(0.60f, 0.78f, 0.95f),
                  true, "Swap_Arrow" + i);
        }

        return root;
    }

    /// <summary>Copier: a sheet with a duplicate peeling off it.</summary>
    /// <summary>
    /// Two of the same thing, one behind the other - the ordinary way a copy is drawn.
    ///
    /// The previous version put a green cross on a white sheet, which every player reads as a
    /// medical kit before they read it as anything else. Colour carries meaning whether or
    /// not it was meant to, so the cross is gone and the duplication is now in the shape.
    /// </summary>
    public static GameObject Copier(PaintFn paint)
    {
        GameObject root = new GameObject("Copier");

        for (int i = 0; i < 2; i++)
        {
            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.name = "Card" + i;
            card.transform.SetParent(root.transform, false);
            card.transform.localScale = new Vector3(0.28f, 0.025f, 0.36f);
            card.transform.localPosition = new Vector3(-0.06f + i * 0.12f, i * 0.06f, 0.07f - i * 0.14f);
            card.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            paint(card, (i == 0) ? new Color(0.66f, 0.70f, 0.78f) : new Color(0.94f, 0.94f, 0.90f),
                  false, "Copy_Card" + i);
        }

        // A token on the front card, so it is plainly a copy of an item and not of paperwork.
        GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gem.name = "Token";
        gem.transform.SetParent(root.transform, false);
        gem.transform.localScale = new Vector3(0.12f, 0.05f, 0.12f);
        gem.transform.localPosition = new Vector3(0.06f, 0.10f, -0.07f);
        gem.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        paint(gem, new Color(0.98f, 0.74f, 0.18f), true, "Copy_Token");

        GameObject gemGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gemGhost.name = "TokenGhost";
        gemGhost.transform.SetParent(root.transform, false);
        gemGhost.transform.localScale = new Vector3(0.10f, 0.04f, 0.10f);
        gemGhost.transform.localPosition = new Vector3(-0.06f, 0.04f, 0.07f);
        gemGhost.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        paint(gemGhost, new Color(0.55f, 0.48f, 0.30f), false, "Copy_TokenGhost");

        return root;
    }

    /// <summary>Junk shop: a crate of odds and ends.</summary>
    public static GameObject Junk(PaintFn paint)
    {
        GameObject root = new GameObject("Junk");

        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.SetParent(root.transform, false);
        crate.transform.localScale = new Vector3(0.36f, 0.24f, 0.30f);
        paint(crate, new Color(0.52f, 0.36f, 0.20f), false, "Junk_Crate");

        // A few mismatched bits poking out of the top.
        Color[] bits = {
            new Color(0.80f, 0.25f, 0.25f),
            new Color(0.30f, 0.60f, 0.85f),
            new Color(0.85f, 0.80f, 0.30f),
        };
        PrimitiveType[] shapes = { PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Cylinder };

        for (int i = 0; i < 3; i++)
        {
            GameObject bit = GameObject.CreatePrimitive(shapes[i]);
            bit.name = "Bit" + i;
            bit.transform.SetParent(root.transform, false);
            bit.transform.localScale = Vector3.one * 0.13f;
            bit.transform.localPosition = new Vector3(-0.10f + i * 0.10f, 0.15f, 0.02f * i);
            bit.transform.localRotation = Quaternion.Euler(20f * i, 30f * i, 15f * i);
            paint(bit, bits[i], false, "Junk_Bit" + i);
        }

        return root;
    }

    /// <summary>Tax: a stamped document with a coin on it.</summary>
    public static GameObject Tax(PaintFn paint)
    {
        GameObject root = new GameObject("Tax");

        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Paper";
        paper.transform.SetParent(root.transform, false);
        paper.transform.localScale = new Vector3(0.30f, 0.02f, 0.38f);
        paint(paper, new Color(0.94f, 0.92f, 0.85f), false, "Tax_Paper");

        GameObject stamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stamp.name = "Stamp";
        stamp.transform.SetParent(root.transform, false);
        stamp.transform.localScale = new Vector3(0.11f, 0.008f, 0.11f);
        stamp.transform.localPosition = new Vector3(-0.07f, 0.02f, -0.11f);
        paint(stamp, new Color(0.80f, 0.20f, 0.20f), false, "Tax_Stamp");

        GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = "Coin";
        coin.transform.SetParent(root.transform, false);
        coin.transform.localScale = new Vector3(0.15f, 0.02f, 0.15f);
        coin.transform.localPosition = new Vector3(0.08f, 0.05f, 0.10f);
        coin.transform.localRotation = Quaternion.Euler(72f, 0f, 12f);
        paint(coin, new Color(0.95f, 0.78f, 0.22f), true, "Tax_Coin");

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
    /// A cannon made of glass, on a carriage.
    ///
    /// A bare tapered barrel turned out to be the same silhouette as the vacuum funnel, and
    /// two items that read alike at a glance is worse than either reading poorly on its own.
    /// The wheels settle it: nothing else in the set is artillery.
    /// </summary>
    public static GameObject GlassCannon(PaintFn paint)
    {
        GameObject root = new GameObject("GlassCannon");

        GameObject barrel = ConeMesh.Object("GcBarrel", 0.135f, 0.165f, 0.48f);
        barrel.transform.SetParent(root.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0.06f, -0.16f);
        paint(barrel, new Color(0.70f, 0.90f, 0.98f), true, "Glass_Core");

        GameObject muzzle = ConeMesh.Object("GcMuzzle", 0.175f, 0.195f, 0.055f);
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.06f, 0.32f);
        paint(muzzle, new Color(0.90f, 0.98f, 1f), true, "Glass_Muzzle");

        GameObject breech = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        breech.name = "Breech";
        breech.transform.SetParent(root.transform, false);
        breech.transform.localScale = new Vector3(0.21f, 0.21f, 0.17f);
        breech.transform.localPosition = new Vector3(0f, 0.06f, -0.19f);
        paint(breech, new Color(0.45f, 0.70f, 0.84f), false, "Glass_Breech");

        // Carriage: a trail block and two wheels, which is what makes it a cannon.
        GameObject trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trail.name = "Trail";
        trail.transform.SetParent(root.transform, false);
        trail.transform.localScale = new Vector3(0.10f, 0.075f, 0.40f);
        trail.transform.localPosition = new Vector3(0f, -0.11f, -0.16f);
        trail.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
        paint(trail, new Color(0.44f, 0.30f, 0.19f), false, "Glass_Carriage");

        for (int i = 0; i < 2; i++)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel" + i;
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localScale = new Vector3(0.17f, 0.022f, 0.17f);
            wheel.transform.localPosition = new Vector3((i == 0) ? 0.11f : -0.11f, -0.11f, -0.02f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            paint(wheel, new Color(0.34f, 0.23f, 0.15f), false, "Glass_Wheel");
        }

        // Cracks laid just proud of the surface, where they can actually be seen.
        for (int i = 0; i < 3; i++)
        {
            float a = (40f + i * 105f) * Mathf.Deg2Rad;
            GameObject crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crack.name = "Crack" + i;
            crack.transform.SetParent(root.transform, false);
            crack.transform.localScale = new Vector3(0.014f, 0.014f, 0.27f);
            crack.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.15f, 0.06f + Mathf.Sin(a) * 0.15f, 0.02f);
            crack.transform.localRotation = Quaternion.Euler(10f * i - 10f, 14f * i - 14f, 0f);
            paint(crack, new Color(0.14f, 0.34f, 0.46f), false, "Glass_Crack");
        }

        for (int i = 0; i < 3; i++)
        {
            float a = (70f + i * 130f) * Mathf.Deg2Rad;
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "Chip" + i;
            chip.transform.SetParent(root.transform, false);
            chip.transform.localScale = new Vector3(0.08f, 0.022f, 0.08f);
            chip.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.27f, 0.06f + Mathf.Sin(a) * 0.27f, 0.30f);
            chip.transform.localRotation = Quaternion.Euler(38f * i, 24f * i, 51f * i);
            paint(chip, new Color(0.82f, 0.95f, 1f), true, "Glass_Chip");
        }

        return root;
    }

    /// <summary>Pinata: a stubby striped animal with a stick.</summary>
    /// <summary>
    /// A donkey pinata. The stripes used to be wider than the body they were meant to wrap,
    /// so the whole thing came out as a stack of coloured discs with the animal buried inside;
    /// each band is now cut to the body radius at the point it sits, and legs, ears and a tail
    /// carry the silhouette.
    /// </summary>
    public static GameObject Pinata(PaintFn paint)
    {
        GameObject root = new GameObject("Pinata");

        const float bodyX = 0.24f;   // half-length along the spine
        Color shell = new Color(0.95f, 0.35f, 0.45f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(bodyX * 2f, 0.34f, 0.30f);
        paint(body, shell, false, "Pin_Body");

        Color[] stripes = {
            new Color(0.98f, 0.80f, 0.20f),
            new Color(0.30f, 0.75f, 0.55f),
            new Color(0.40f, 0.55f, 0.95f),
        };

        for (int i = 0; i < 3; i++)
        {
            float x = -0.12f + i * 0.12f;

            // Follow the body inwards, then stand just proud of it so the band shows.
            float k = Mathf.Sqrt(Mathf.Max(0.02f, 1f - (x / bodyX) * (x / bodyX))) * 1.05f;

            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Stripe" + i;
            band.transform.SetParent(root.transform, false);
            band.transform.localScale = new Vector3(0.34f * k, 0.022f, 0.30f * k);
            band.transform.localPosition = new Vector3(x, 0f, 0f);
            band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            paint(band, stripes[i], false, "Pin_Stripe" + i);
        }

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localScale = new Vector3(0.19f, 0.19f, 0.17f);
        head.transform.localPosition = new Vector3(0.26f, 0.15f, 0f);
        paint(head, shell, false, "Pin_Body");

        GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
        snout.name = "Snout";
        snout.transform.SetParent(root.transform, false);
        snout.transform.localScale = new Vector3(0.10f, 0.08f, 0.09f);
        snout.transform.localPosition = new Vector3(0.36f, 0.11f, 0f);
        paint(snout, new Color(0.80f, 0.26f, 0.36f), false, "Pin_Snout");

        for (int i = 0; i < 2; i++)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ear.name = "Ear" + i;
            ear.transform.SetParent(root.transform, false);
            ear.transform.localScale = new Vector3(0.045f, 0.13f, 0.035f);
            ear.transform.localPosition = new Vector3(0.24f, 0.27f, (i == 0) ? 0.05f : -0.05f);
            ear.transform.localRotation = Quaternion.Euler(0f, 0f, (i == 0) ? 16f : 22f);
            paint(ear, new Color(0.98f, 0.80f, 0.20f), false, "Pin_Ear");
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = "Leg" + i;
            leg.transform.SetParent(root.transform, false);
            leg.transform.localScale = new Vector3(0.055f, 0.11f, 0.055f);
            leg.transform.localPosition = new Vector3((i < 2) ? 0.13f : -0.13f, -0.21f,
                                                      (i % 2 == 0) ? 0.08f : -0.08f);
            paint(leg, new Color(0.40f, 0.55f, 0.95f), false, "Pin_Leg");
        }

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tail.name = "Tail";
        tail.transform.SetParent(root.transform, false);
        tail.transform.localScale = new Vector3(0.035f, 0.10f, 0.035f);
        tail.transform.localPosition = new Vector3(-0.28f, 0.06f, 0f);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 52f);
        paint(tail, new Color(0.30f, 0.75f, 0.55f), false, "Pin_Tail");

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
        paint(jaw, new Color(0.86f, 0.85f, 0.80f), false, "Wand_Skull");

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

    /// <summary>Armageddon: a burning rock trailing fire.</summary>
    public static GameObject Meteor(PaintFn paint)
    {
        GameObject root = new GameObject("Meteor");

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.SetParent(root.transform, false);
        rock.transform.localScale = new Vector3(0.34f, 0.30f, 0.32f);
        paint(rock, new Color(0.26f, 0.19f, 0.17f), false, "Met_Rock");

        // Craters, as shallow dents of a lighter shade.
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            GameObject pit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pit.name = "Pit" + i;
            pit.transform.SetParent(root.transform, false);
            pit.transform.localScale = Vector3.one * 0.11f;
            pit.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.13f, Mathf.Sin(a) * 0.11f, -0.10f);
            paint(pit, new Color(0.38f, 0.30f, 0.26f), false, "Met_Pit");
        }

        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Flame";
        flame.transform.SetParent(root.transform, false);
        flame.transform.localScale = new Vector3(0.22f, 0.20f, 0.40f);
        flame.transform.localPosition = new Vector3(0f, 0.02f, 0.26f);
        paint(flame, new Color(1f, 0.55f, 0.12f), true, "Met_Flame");

        return root;
    }

    /// <summary>Vacuum: a nozzle with a hose and a key being sucked in.</summary>
    /// <summary>
    /// Suction funnel with a canister behind it and keys being drawn in.
    ///
    /// The old one was a flat disc with a cube stuck to it, which at icon size read as a dark
    /// blob and nothing else. A silhouette has to survive being 80 pixels wide, so the shape
    /// carries the meaning here: a wide mouth tapering back to a body, with two keys caught in
    /// the draught in front of it.
    /// </summary>
    public static GameObject Vacuum(PaintFn paint)
    {
        GameObject root = new GameObject("Vacuum");

        GameObject funnel = ConeMesh.Object("VacFunnel", 0.12f, 0.30f, 0.30f);
        funnel.transform.SetParent(root.transform, false);
        funnel.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        paint(funnel, new Color(0.32f, 0.36f, 0.44f), false, "Vac_Funnel");

        // Bright lip: without it the mouth is a dark hole against a dark body.
        GameObject rim = ConeMesh.Object("VacRim", 0.30f, 0.335f, 0.045f);
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0f, 0.42f);
        paint(rim, new Color(0.85f, 0.88f, 0.92f), false, "Vac_Rim");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.17f, 0.16f, 0.17f);
        body.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        paint(body, new Color(0.24f, 0.27f, 0.33f), false, "Vac_Body");

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localScale = new Vector3(0.145f, 0.035f, 0.145f);
        cap.transform.localPosition = new Vector3(0f, 0f, -0.23f);
        cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        paint(cap, new Color(0.80f, 0.45f, 0.15f), true, "Vac_Cap");

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.name = "Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localScale = new Vector3(0.05f, 0.05f, 0.24f);
        handle.transform.localPosition = new Vector3(0f, 0.19f, -0.05f);
        paint(handle, new Color(0.18f, 0.20f, 0.24f), false, "Vac_Handle");

        for (int i = 0; i < 2; i++)
        {
            GameObject strut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strut.name = "Strut" + i;
            strut.transform.SetParent(root.transform, false);
            strut.transform.localScale = new Vector3(0.04f, 0.11f, 0.04f);
            strut.transform.localPosition = new Vector3(0f, 0.13f, -0.05f + i * 0.20f);
            paint(strut, new Color(0.18f, 0.20f, 0.24f), false, "Vac_Handle");
        }

        // Keys on their way in - the one detail that says what the funnel is for.
        GameObject key0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        key0.name = "Key0";
        key0.transform.SetParent(root.transform, false);
        key0.transform.localScale = new Vector3(0.045f, 0.045f, 0.13f);
        key0.transform.localPosition = new Vector3(0.10f, 0.09f, 0.60f);
        key0.transform.localRotation = Quaternion.Euler(10f, -28f, 20f);
        paint(key0, new Color(0.95f, 0.80f, 0.25f), true, "Vac_Key");

        GameObject key1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        key1.name = "Key1";
        key1.transform.SetParent(root.transform, false);
        key1.transform.localScale = new Vector3(0.032f, 0.032f, 0.095f);
        key1.transform.localPosition = new Vector3(-0.11f, -0.06f, 0.70f);
        key1.transform.localRotation = Quaternion.Euler(-14f, 22f, -18f);
        paint(key1, new Color(0.95f, 0.80f, 0.25f), true, "Vac_Key");

        return root;
    }

    /// <summary>Piggy bank: a pig with a coin slot.</summary>
    public static GameObject Piggy(PaintFn paint)
    {
        GameObject root = new GameObject("Piggy");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.38f, 0.30f, 0.30f);
        paint(body, new Color(0.96f, 0.62f, 0.70f), false, "Pig_Body");

        GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        snout.name = "Snout";
        snout.transform.SetParent(root.transform, false);
        snout.transform.localScale = new Vector3(0.09f, 0.03f, 0.09f);
        snout.transform.localPosition = new Vector3(0.20f, 0f, 0f);
        snout.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        paint(snout, new Color(0.90f, 0.50f, 0.58f), false, "Pig_Snout");

        for (int i = 0; i < 4; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = "Leg" + i;
            leg.transform.SetParent(root.transform, false);
            leg.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
            leg.transform.localPosition = new Vector3(
                (i < 2) ? 0.11f : -0.11f, -0.17f, (i % 2 == 0) ? 0.09f : -0.09f);
            paint(leg, new Color(0.90f, 0.50f, 0.58f), false, "Pig_Snout");
        }

        GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slot.name = "Slot";
        slot.transform.SetParent(root.transform, false);
        slot.transform.localScale = new Vector3(0.12f, 0.02f, 0.03f);
        slot.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        paint(slot, new Color(0.30f, 0.20f, 0.24f), false, "Pig_Slot");

        GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = "Coin";
        coin.transform.SetParent(root.transform, false);
        coin.transform.localScale = new Vector3(0.10f, 0.012f, 0.10f);
        coin.transform.localPosition = new Vector3(0f, 0.24f, 0f);
        coin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        paint(coin, new Color(0.95f, 0.80f, 0.25f), true, "Pig_Coin");

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
        needle.transform.localRotation = Quaternion.Euler(72f, 0f, 18f);
        paint(needle, new Color(0.85f, 0.87f, 0.90f), false, "Curse_Needle");

        GameObject pinHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pinHead.name = "PinHead";
        pinHead.transform.SetParent(root.transform, false);
        pinHead.transform.localScale = Vector3.one * 0.055f;
        pinHead.transform.localPosition = new Vector3(0.09f, 0.14f, 0.235f);
        paint(pinHead, new Color(0.92f, 0.18f, 0.22f), true, "Curse_PinHead");

        return root;
    }

    /// <summary>Freeze: a cluster of ice shards.</summary>
    public static GameObject Freeze(PaintFn paint)
    {
        GameObject root = new GameObject("Freeze");

        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spike.name = "Spike" + i;
            spike.transform.SetParent(root.transform, false);
            spike.transform.localScale = new Vector3(0.07f, 0.17f - i * 0.015f, 0.07f);
            spike.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.10f, 0.02f, Mathf.Sin(a) * 0.10f);
            spike.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * 22f, 0f, Mathf.Sin(a) * -22f);
            paint(spike, new Color(0.63f, 0.87f, 0.97f), true, "Frz_Ice");
        }

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.16f;
        paint(core, new Color(0.78f, 0.93f, 1f), true, "Frz_Core");

        return root;
    }

    /// <summary>Double move: two dice stacked.</summary>
    /// <summary>
    /// Two dice. One pip each was not enough - a cube with a single dot on it reads as a
    /// block with a dot, so both visible faces get a proper face value.
    /// </summary>
    public static GameObject DoubleDice(PaintFn paint)
    {
        GameObject root = new GameObject("DoubleDice");

        for (int i = 0; i < 2; i++)
        {
            GameObject die = GameObject.CreatePrimitive(PrimitiveType.Cube);
            die.name = "Die" + i;
            die.transform.SetParent(root.transform, false);
            die.transform.localScale = Vector3.one * 0.24f;
            die.transform.localPosition = new Vector3(i * 0.10f, i * 0.22f, i * -0.06f);
            die.transform.localRotation = Quaternion.Euler(0f, i * 22f, i * 8f);
            paint(die, (i == 0) ? new Color(0.93f, 0.90f, 0.82f) : new Color(0.85f, 0.88f, 0.95f),
                  false, "DblDie_Body" + i);

            // Top face: three pips on the diagonal. Front face: two.
            float[,] top = { { -0.26f, -0.26f }, { 0f, 0f }, { 0.26f, 0.26f } };
            for (int p = 0; p < 3; p++)
                Pip(paint, die, "TopPip" + i + "_" + p, new Vector3(top[p, 0], 0.52f, top[p, 1]));

            float[,] front = { { -0.24f, 0.24f }, { 0.24f, -0.24f } };
            for (int p = 0; p < 2; p++)
                Pip(paint, die, "FrontPip" + i + "_" + p, new Vector3(front[p, 0], front[p, 1], 0.52f));
        }

        return root;
    }

    private static void Pip(PaintFn paint, GameObject die, string name, Vector3 localPos)
    {
        GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pip.name = name;
        pip.transform.SetParent(die.transform, false);
        pip.transform.localScale = Vector3.one * 0.22f;
        pip.transform.localPosition = localPos;
        paint(pip, new Color(0.85f, 0.15f, 0.15f), false, "DblDie_Pip");
    }

    /// <summary>Grapple: a three-pronged hook on a rope.</summary>
    /// <summary>
    /// A grappling hook with enough mass to survive being 80 pixels wide. The first version
    /// was built from 4-centimetre sticks, which at icon size turned into a spider.
    /// </summary>
    public static GameObject Grapple(PaintFn paint)
    {
        GameObject root = new GameObject("Grapple");

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.085f, 0.19f, 0.085f);
        shaft.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        paint(shaft, new Color(0.55f, 0.57f, 0.60f), false, "Grp_Metal");

        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;

            GameObject prong = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prong.name = "Prong" + i;
            prong.transform.SetParent(root.transform, false);
            prong.transform.localScale = new Vector3(0.075f, 0.15f, 0.075f);
            prong.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.14f, 0.15f, Mathf.Sin(a) * 0.14f);
            prong.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 58f, 0f, Mathf.Cos(a) * -58f);
            paint(prong, new Color(0.66f, 0.68f, 0.72f), false, "Grp_Prong");

            GameObject barb = ConeMesh.Object("GrpBarb" + i, 0.048f, 0.005f, 0.11f);
            barb.transform.SetParent(root.transform, false);
            barb.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.235f, 0.255f, Mathf.Sin(a) * 0.235f);
            barb.transform.localRotation = Quaternion.LookRotation(
                new Vector3(Mathf.Cos(a) * 0.6f, 0.8f, Mathf.Sin(a) * 0.6f));
            paint(barb, new Color(0.80f, 0.82f, 0.86f), false, "Grp_Barb");
        }

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Ring";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localScale = new Vector3(0.13f, 0.022f, 0.13f);
        ring.transform.localPosition = new Vector3(0f, -0.19f, 0f);
        paint(ring, new Color(0.50f, 0.52f, 0.56f), false, "Grp_Metal");

        // Coiled rope rather than one straight length, which read as a second shaft.
        for (int i = 0; i < 3; i++)
        {
            GameObject coil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coil.name = "Coil" + i;
            coil.transform.SetParent(root.transform, false);
            coil.transform.localScale = new Vector3(0.15f - i * 0.012f, 0.026f, 0.15f - i * 0.012f);
            coil.transform.localPosition = new Vector3(0.012f * i, -0.245f - i * 0.055f, 0f);
            coil.transform.localRotation = Quaternion.Euler(0f, 0f, 7f * i);
            paint(coil, new Color(0.72f, 0.60f, 0.36f), false, "Grp_Rope");
        }

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
        paint(foot, new Color(0.38f, 0.23f, 0.14f), false, "Boot_Leather");

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
            prong.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.22f, 0.06f, Mathf.Sin(a) * 0.22f);
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

        // Bubbles, so it reads as something nasty rather than a green ball.
        for (int i = 0; i < 3; i++)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Bubble" + i;
            b.transform.SetParent(root.transform, false);
            b.transform.localScale = Vector3.one * (0.05f - i * 0.008f);
            b.transform.localPosition = new Vector3(-0.05f + i * 0.05f, 0.05f + i * 0.05f, 0.10f);
            paint(b, new Color(0.75f, 0.98f, 0.55f), true, "Poi_Bubble");
        }

        return root;
    }

    /// <summary>Signpost with two arms pointing opposite ways.</summary>
    public static GameObject Signpost(PaintFn paint)
    {
        GameObject root = new GameObject("Signpost");

        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        post.transform.SetParent(root.transform, false);
        post.transform.localScale = new Vector3(0.05f, 0.26f, 0.05f);
        paint(post, new Color(0.52f, 0.36f, 0.20f), false, "Sign_Post");

        for (int i = 0; i < 2; i++)
        {
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm" + i;
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale = new Vector3(0.30f, 0.09f, 0.03f);
            arm.transform.localPosition = new Vector3((i == 0) ? -0.13f : 0.13f, 0.18f - i * 0.13f, 0f);
            // Twisted, because the whole point is that they are lying.
            arm.transform.localRotation = Quaternion.Euler(0f, (i == 0) ? 18f : -24f, (i == 0) ? 6f : -5f);
            paint(arm, (i == 0) ? new Color(0.88f, 0.82f, 0.62f) : new Color(0.80f, 0.72f, 0.52f),
                  false, "Sign_Arm" + i);
        }

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
    /// Ricochet: a stubby muzzle, a wall, and a pellet bouncing off it along a zigzag.
    /// The single pellet the item used to show read as an ordinary shotgun slug - the
    /// bounce is the whole point, so the path is what the icon has to say.
    /// </summary>
    /// <summary>
    /// A gun and a shot bouncing off a wall into cover.
    ///
    /// The point of the item is the bounce, so the diagram is the icon. The first attempt had
    /// the gun small and dark against a bright trail, which left the trail reading as the
    /// whole object; here the weapon carries real mass and the trail ends in an arrowhead so
    /// the direction is not in question.
    /// </summary>
    public static GameObject Ricochet(PaintFn paint)
    {
        GameObject root = new GameObject("Ricochet");

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(root.transform, false);
        barrel.transform.localScale = new Vector3(0.115f, 0.21f, 0.115f);
        barrel.transform.localPosition = new Vector3(-0.27f, -0.20f, 0f);
        barrel.transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
        paint(barrel, new Color(0.58f, 0.61f, 0.66f), false, "Ric_Metal");

        GameObject muzzle = ConeMesh.Object("RicMuzzle", 0.075f, 0.105f, 0.10f);
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(-0.12f, -0.05f, 0f);
        muzzle.transform.localRotation = Quaternion.Euler(0f, 90f, 48f);
        paint(muzzle, new Color(0.30f, 0.32f, 0.36f), false, "Ric_Muzzle");

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(root.transform, false);
        grip.transform.localScale = new Vector3(0.10f, 0.20f, 0.09f);
        grip.transform.localPosition = new Vector3(-0.41f, -0.36f, 0f);
        grip.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        paint(grip, new Color(0.42f, 0.28f, 0.18f), false, "Ric_Grip");

        // The surface being bounced off, tilted so the deflection makes sense.
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(root.transform, false);
        wall.transform.localScale = new Vector3(0.52f, 0.09f, 0.20f);
        wall.transform.localPosition = new Vector3(0.10f, 0.34f, 0f);
        wall.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
        paint(wall, new Color(0.50f, 0.46f, 0.40f), false, "Ric_Wall");

        Vector3[] path =
        {
            new Vector3(-0.06f, 0.01f, 0f),
            new Vector3( 0.10f, 0.24f, 0f),
            new Vector3( 0.40f, -0.10f, 0f),
        };

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 a = path[i], b = path[i + 1];
            Vector3 dir = b - a;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "Trail" + i;
            seg.transform.SetParent(root.transform, false);
            seg.transform.localScale = new Vector3(dir.magnitude, 0.055f, 0.055f);
            seg.transform.localPosition = (a + b) * 0.5f;
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
            paint(seg, new Color(0.98f, 0.78f, 0.25f), true, "Ric_Trail");
        }

        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "Spark";
        spark.transform.SetParent(root.transform, false);
        spark.transform.localScale = Vector3.one * 0.13f;
        spark.transform.localPosition = path[1];
        paint(spark, new Color(1f, 0.94f, 0.55f), true, "Ric_Spark");

        // Arrowhead on the outgoing leg - a bare line does not say which way it went.
        GameObject head = ConeMesh.Object("RicHead", 0.10f, 0.004f, 0.17f);
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = path[2];
        head.transform.localRotation = Quaternion.LookRotation(path[2] - path[1]);
        paint(head, new Color(0.95f, 0.72f, 0.20f), true, "Ric_Pellet");

        return root;
    }

    /// <summary>Boomerang: two arms meeting at an angle.</summary>
    public static GameObject Boomerang(PaintFn paint)
    {
        GameObject root = new GameObject("Boomerang");

        for (int i = 0; i < 2; i++)
        {
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm" + i;
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale = new Vector3(0.10f, 0.05f, 0.46f);
            arm.transform.localRotation = Quaternion.Euler(0f, (i == 0) ? -32f : 32f, 0f);
            arm.transform.localPosition =
                arm.transform.localRotation * new Vector3(0f, 0f, 0.20f);
            paint(arm, new Color(0.55f, 0.35f, 0.16f), false, "Boom_Wood");
        }

        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hub.name = "Hub";
        hub.transform.SetParent(root.transform, false);
        hub.transform.localScale = new Vector3(0.13f, 0.06f, 0.13f);
        paint(hub, new Color(0.42f, 0.26f, 0.12f), false, "Boom_Hub");

        return root;
    }
}
