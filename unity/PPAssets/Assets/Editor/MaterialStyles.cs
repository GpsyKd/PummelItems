using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How each material catches the light, kept in one table so the look of the whole set is
/// tuned in one place, the way a palette is.
///
/// Smoothness and metallic are the Standard shader's own two knobs; without them every model
/// was the same mid-gloss plastic, metal and paper alike. Emission is a multiplier on the
/// glow the model code asks for (0.6 x its colour): 0 switches it off. Only things that
/// shine by themselves should glow - a self-lit surface cannot be shaded, so a glowing body
/// loses its volume and turns into a flat light patch.
///
/// The game itself shows no glow at all: its build has no Standard shader variant with
/// _EMISSION (a black ball with white emission drew black in its icon renderer, 2026-10-02).
/// So the glow stays in the materials for any renderer that has it, but whatever is meant to
/// glow has to read from its own bright colour.
///
/// Metallic is 0 throughout. A metallic surface is coloured by what it reflects, and the
/// game's icon renderer lights a model from behind with full-strength shadows and little
/// around it to reflect: metal there comes out near black - gold olive, steel charcoal.
/// Metal is told by colour and a tight highlight instead.
/// </summary>
public struct MaterialStyle
{
    public readonly float Smoothness;
    public readonly float Metallic;
    public readonly float Emission;

    public MaterialStyle(float smoothness, float metallic, float emission)
    {
        Smoothness = smoothness;
        Metallic = metallic;
        Emission = emission;
    }

    public static readonly MaterialStyle Default = new MaterialStyle(0.5f, 0f, 1f);

    private static readonly MaterialStyle Metal   = new MaterialStyle(0.65f, 0f, 0f);    // steel: grey and glossy, not metallic - see above
    private static readonly MaterialStyle Gold    = new MaterialStyle(0.80f, 0f, 0.2f);
    private static readonly MaterialStyle Wood    = new MaterialStyle(0.20f, 0f, 0f);
    private static readonly MaterialStyle Cloth   = new MaterialStyle(0.10f, 0f, 0f);
    private static readonly MaterialStyle Paper   = new MaterialStyle(0.15f, 0f, 0f);
    private static readonly MaterialStyle Plastic = new MaterialStyle(0.60f, 0f, 0f);
    private static readonly MaterialStyle Gloss   = new MaterialStyle(0.85f, 0f, 0f);
    private static readonly MaterialStyle Glass   = new MaterialStyle(0.95f, 0f, 0.25f);
    private static readonly MaterialStyle Ice     = new MaterialStyle(0.90f, 0f, 0.35f);

    private static readonly Dictionary<string, MaterialStyle> s_byName = new Dictionary<string, MaterialStyle>
    {
        // Grenade and its fragments
        { "Gren_Body", new MaterialStyle(0.45f, 0f, 0f) }, { "Gren_Band", new MaterialStyle(0.40f, 0f, 0f) },
        { "Gren_Cap", Metal }, { "Gren_Lever", Metal }, { "Shard_Metal", new MaterialStyle(0.55f, 0f, 0f) },

        // Sticky bomb: slime is the shiniest thing in the set
        { "Sticky_Goo", Gloss }, { "Sticky_Goo2", Gloss }, { "Sticky_Charge", new MaterialStyle(0.7f, 0f, 1f) },

        // Ice
        { "Ice_Body", Ice }, { "Ice_Tip", Ice }, { "Ice_Cap", new MaterialStyle(0.85f, 0f, 0f) },
        { "Frz_Ice", Ice }, { "Frz_Core", new MaterialStyle(0.9f, 0f, 0.5f) },

        // Ricochet pellet keeps a little glow so a fast shot stays visible in flight
        { "Pellet_Brass", new MaterialStyle(0.8f, 0f, 0.4f) },

        { "Swap_Patch0", Cloth }, { "Swap_Patch1", Cloth },
        { "Swap_Bag0", Cloth }, { "Swap_Bag1", Cloth }, { "Swap_Neck", Cloth },
        { "Swap_Arrow0", new MaterialStyle(0.55f, 0f, 0.3f) }, { "Swap_Arrow1", new MaterialStyle(0.55f, 0f, 0.3f) },

        // Photocopier: the glass is lit by the scanning lamp
        { "Copy_Body", Plastic }, { "Copy_Drawer", new MaterialStyle(0.5f, 0f, 0f) }, { "Copy_Lid", new MaterialStyle(0.55f, 0f, 0f) },
        { "Copy_Panel", new MaterialStyle(0.4f, 0f, 0f) }, { "Copy_Glass", new MaterialStyle(0.9f, 0f, 1f) },
        { "Copy_Button", new MaterialStyle(0.6f, 0f, 1f) }, { "Copy_Screen", new MaterialStyle(0.8f, 0f, 0.8f) },
        { "Copy_Paper", Paper }, { "Copy_Print", new MaterialStyle(0.3f, 0f, 0f) }, { "Copy_Token", Gold },

        { "Junk_Inside", Cloth }, { "Junk_Flap", Paper }, { "Junk_Tin", Metal }, { "Junk_Label", Paper }, { "Junk_Spring", Metal }, { "Junk_Cog", Gold }, { "Junk_Hole", Wood }, { "Junk_Bottle", Gloss }, { "Junk_Tag", Paper }, { "Junk_String", Cloth },
        { "Junk_Crate", Wood }, { "Junk_Bit0", Plastic }, { "Junk_Bit1", Plastic }, { "Junk_Bit2", Metal },

        { "Tax_Paper", Paper }, { "Tax_Ink", new MaterialStyle(0.3f, 0f, 0f) }, { "Tax_Stamp", new MaterialStyle(0.4f, 0f, 0f) },
        { "Tax_Coin", Gold },

        // Glass cannon: opaque on purpose - transparency needs shader variants the game may
        // not ship - so "glass" is carried by gloss and a faint glow instead
        { "Glass_Shine", Gloss }, { "Glass_Iron", Metal },
        { "Glass_Core", Glass }, { "Glass_Muzzle", Glass }, { "Glass_Breech", Glass }, { "Glass_Chip", Glass },
        { "Glass_Carriage", Wood }, { "Glass_Wheel", Wood }, { "Glass_Crack", new MaterialStyle(0.7f, 0f, 0f) },
        { "Glass_Bore", new MaterialStyle(0.1f, 0f, 0f) },

        // Pinata is papier-mache and tissue fringe; the googly eyes are plastic
        { "Pin_Body", Paper }, { "Pin_Paper0", Paper }, { "Pin_Paper1", Paper }, { "Pin_Paper2", Paper },
        { "Pin_Paper3", Paper }, { "Pin_Paper4", Paper }, { "Pin_Paper5", Paper }, { "Pin_Muzzle", Paper },
        { "Pin_Hoof", new MaterialStyle(0.3f, 0f, 0f) }, { "Pin_EyeWhite", Gloss }, { "Pin_Eye", Gloss },

        { "Gen_Box", new MaterialStyle(0.5f, 0f, 0f) }, { "Gen_Lid", new MaterialStyle(0.5f, 0f, 0f) },
        { "Gen_Gift0", Plastic }, { "Gen_Gift1", Plastic }, { "Gen_Gift2", Plastic },

        { "Wand_Shaft", Wood }, { "Wand_Skull", new MaterialStyle(0.4f, 0f, 0f) }, { "Wand_Jaw", new MaterialStyle(0.4f, 0f, 0f) },
        { "Wand_Gap", Cloth }, { "Wand_Socket", Cloth }, { "Wand_Eye", new MaterialStyle(0.7f, 0f, 1.6f) },

        // Meteor fire in three layers: the outer one glows least, so it stays red instead of
        // washing out to the same yellow as the rest
        { "Met_Rock", new MaterialStyle(0.05f, 0f, 0f) }, { "Met_Pit", new MaterialStyle(0.05f, 0f, 0f) },
        { "Met_Flame", new MaterialStyle(0.4f, 0f, 1f) }, { "Met_FlameMid", new MaterialStyle(0.4f, 0f, 1.2f) },
        { "Met_Ember", new MaterialStyle(0.4f, 0f, 1.6f) },

        { "Vac_Body", Plastic }, { "Vac_Rim", Plastic }, { "Vac_Handle", new MaterialStyle(0.5f, 0f, 0f) },
        { "Vac_Cap", new MaterialStyle(0.6f, 0f, 1f) }, { "Vac_Key", Gold }, { "Vac_Mouth", Cloth },
        { "Vac_Air", new MaterialStyle(0.5f, 0f, 1f) }, { "Vac_Hose", new MaterialStyle(0.35f, 0f, 0f) },

        { "Pig_Body", new MaterialStyle(0.75f, 0f, 0f) }, { "Pig_Snout", new MaterialStyle(0.7f, 0f, 0f) },
        { "Pig_Slot", Cloth }, { "Pig_Coin", Gold },

        { "Curse_Cloth", Cloth }, { "Curse_Head", Cloth }, { "Curse_Limb", Cloth }, { "Curse_Stitch", Cloth },
        { "Curse_Needle", new MaterialStyle(0.8f, 0f, 0f) }, { "Curse_PinHead", new MaterialStyle(0.75f, 0f, 0.3f) },

        { "DblDie_Body0", new MaterialStyle(0.7f, 0f, 0f) }, { "DblDie_Body1", new MaterialStyle(0.7f, 0f, 0f) },
        { "DblDie_Pip", new MaterialStyle(0.7f, 0f, 0f) }, { "Die_Body", new MaterialStyle(0.7f, 0f, 0f) },
        { "Die_Pip", new MaterialStyle(0.7f, 0f, 0f) },

        { "Grp_Metal", Metal }, { "Grp_Prong", Metal }, { "Grp_Ring", Metal },
        { "Grp_Barb", new MaterialStyle(0.75f, 0f, 0f) }, { "Grp_Rope", Cloth },

        { "Boot_Leather", new MaterialStyle(0.45f, 0f, 0f) }, { "Boot_Foot", new MaterialStyle(0.45f, 0f, 0f) },
        { "Boot_Cuff", Cloth }, { "Boot_Toe", new MaterialStyle(0.55f, 0f, 0f) }, { "Boot_Sole", new MaterialStyle(0.3f, 0f, 0f) },

        { "Tick_Card", Paper }, { "Tick_Stripe", Paper }, { "Tick_Arrow", Paper },

        // The plate is recoloured per owner at runtime and keeps a glow there
        { "Mine_Body", new MaterialStyle(0.45f, 0f, 0f) }, { "Mine_Plate", new MaterialStyle(0.65f, 0f, 0.5f) },
        { "Mine_Prong", Metal },

        { "Poi_Liquid", new MaterialStyle(0.9f, 0f, 0.25f) }, { "Poi_Glass", Glass },
        { "Poi_Cork", new MaterialStyle(0.1f, 0f, 0f) }, { "Poi_Bubble", new MaterialStyle(0.9f, 0f, 0.3f) },

        { "Sign_Post", Wood }, { "Sign_Arm0", Wood }, { "Sign_Arm1", Wood }, { "Sign_Cap", Wood },
        { "Sign_Stone", new MaterialStyle(0.15f, 0f, 0f) },

        { "LifeMag_Body", new MaterialStyle(0.7f, 0f, 0f) }, { "LifeMag_Pole", new MaterialStyle(0.65f, 0f, 0.4f) },

        // Ricochet: a brass blunderbuss; the streaks and the spark are light, not objects
        { "Ric_Brass", Gold }, { "Ric_Bore", new MaterialStyle(0.1f, 0f, 0f) }, { "Ric_Metal", Metal }, { "Ric_Grip", Wood },
        { "Ric_Wall", new MaterialStyle(0.25f, 0f, 0f) },
        { "Ric_Trail", new MaterialStyle(0.5f, 0f, 1f) }, { "Ric_Spark", new MaterialStyle(0.5f, 0f, 1.4f) }, { "Ric_Pellet", Gold },

        { "Boom_Wood", Wood }, { "Boom_Hub", Wood }, { "Boom_Paint", new MaterialStyle(0.45f, 0f, 0f) },
        { "Banana_Body", new MaterialStyle(0.45f, 0f, 0f) }, { "Banana_Stem", Wood }, { "Banana_Tip", new MaterialStyle(0.3f, 0f, 0f) },
        { "Orb_Core", new MaterialStyle(0.85f, 0f, 0.45f) }, { "Orb_Ring", Gold },
        { "Orb_Bead0", Plastic }, { "Orb_Bead1", Plastic }, { "Orb_Bead2", Plastic }, { "Orb_Bead3", Plastic },

        // Parts added with the shape kit
        { "Gren_Pin", Metal }, { "Sticky_Device", new MaterialStyle(0.6f, 0f, 0f) },
        { "Met_Magma", new MaterialStyle(0.4f, 0f, 1.4f) }, { "Met_TailCore", new MaterialStyle(0.4f, 0f, 1.6f) },
        { "Frz_Base", new MaterialStyle(0.45f, 0f, 0f) }, { "Gen_Ribbon", new MaterialStyle(0.65f, 0f, 0f) },
        { "Pig_Ear", new MaterialStyle(0.6f, 0f, 0f) }, { "Pig_EarInner", new MaterialStyle(0.6f, 0f, 0f) },
        { "Pig_Eye", new MaterialStyle(0.85f, 0f, 0f) }, { "Pig_EyeShine", new MaterialStyle(0.9f, 0f, 1f) },
        { "Die_PipDark", new MaterialStyle(0.4f, 0f, 0f) }, { "Die_PipRed", new MaterialStyle(0.6f, 0f, 0f) },
        { "Die_PipGold", new MaterialStyle(0.8f, 0f, 0.6f) },
    };

    public static MaterialStyle For(string matName)
    {
        MaterialStyle s;
        return s_byName.TryGetValue(matName, out s) ? s : Default;
    }

    public static bool IsKnown(string matName)
    {
        return s_byName.ContainsKey(matName);
    }
}
