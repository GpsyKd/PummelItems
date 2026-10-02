using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>How an item sits in the player's hand while it is being used.</summary>
    internal struct HeldPose
    {
        public PlayerBone Bone;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale;

        public HeldPose(PlayerBone bone, Vector3 position, Vector3 rotation, float scale)
        {
            Bone = bone;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    /// <summary>
    /// The held pose of every item, in one table, plus an optional file that overrides it.
    ///
    /// The table is what ships. The file, UserData/PummelCustomItems/held_poses.txt, is how the
    /// table gets tuned: PoseStudio watches it and re-renders every item in the player's hand
    /// whenever it changes, so a pose is adjusted by editing numbers and looking - no rebuild,
    /// no restart. Once a pose looks right it is copied back into the table.
    ///
    /// Poses used to be one default for all 29 items - left hand, no rotation - so each model
    /// stuck out of the hand however its own axes happened to point. Items missing from the
    /// table keep the pose they were declared with.
    /// </summary>
    internal static class HeldPoses
    {
        internal const string FileName = "held_poses.txt";

        // Worked out from the carrying pose every item of ours switches on while it is held: both
        // hands up in front, thumbs up, palms facing each other. Bulky things sit between the
        // palms the way the game's own present does; everything else is in the left hand, the
        // one further forward - compact things perched on it, long ones through the fist. All
        // hang off the left hand bone, so they move with it.
        private static readonly Dictionary<string, HeldPose> s_table = new Dictionary<string, HeldPose>
        {
            // between both hands
            { "PCI_ShuffleItem", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0787f, 0.1143f, 0.2759f), new Vector3(-72.96f, 116.5f, 131.52f), 0.569f) },
            { "PCI_I_SwapInv", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.1319f, 0.1338f, 0.2487f), new Vector3(-72.96f, 116.5f, 131.52f), 0.907f) },
            { "PCI_I_Copier", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.3207f, 0.2053f, 0.1996f), new Vector3(-72.96f, 116.5f, 131.52f), 1.066f) },
            { "PCI_I_Junk", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.1378f, 0.1898f, 0.214f), new Vector3(-58.13f, 90.5f, 155.44f), 1.092f) },
            { "PCI_I_Glass", new HeldPose(PlayerBone.LeftHand, new Vector3(0.052f, 0.3698f, 0.1249f), new Vector3(-72.96f, 116.5f, 131.52f), 0.874f) },
            { "PCI_I_Pinata", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0735f, 0.1489f, 0.2136f), new Vector3(-72.96f, 116.5f, 131.52f), 0.987f) },
            { "PCI_I_Generosity", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.1154f, 0.2321f, 0.2016f), new Vector3(12.68f, 159.29f, 113.49f), 1.17f) },
            { "PCI_I_Armageddon", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0518f, 0.1689f, 0.2409f), new Vector3(-27.4f, 98.01f, 154.36f), 0.754f) },
            { "PCI_I_Piggy", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0958f, 0.1618f, 0.2636f), new Vector3(-72.96f, 116.5f, 131.52f), 1.244f) },

            // in the left hand
            { "PCI_LoadedDiceItem", new HeldPose(PlayerBone.LeftHand, new Vector3(0.127f, 0.055f, 0.1253f), new Vector3(-42.06f, -121.01f, 37.7f), 0.553f) },
            { "PCI_I_DoubleMove", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0988f, 0.0675f, 0.0437f), new Vector3(-51.02f, 142.57f, 107.99f), 0.733f) },
            { "PCI_I_Grenade", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1173f, 0.0545f, 0.12f), new Vector3(36.75f, 165.24f, 104.03f), 0.663f) },
            { "PCI_I_Sticky", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0664f, 0.0711f, 0.1222f), new Vector3(-51.02f, 142.57f, 107.99f), 0.813f) },
            { "PCI_I_Curse", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1282f, 0.0289f, 0.1279f), new Vector3(-51.02f, 142.57f, 107.99f), 0.719f) },
            { "PCI_I_Freeze", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1011f, 0.0587f, 0.108f), new Vector3(-51.02f, 142.57f, 107.99f), 0.739f) },
            { "PCI_I_Poison", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1261f, 0.0502f, 0.1232f), new Vector3(-51.02f, 142.57f, 107.99f), 0.832f) },
            { "PCI_I_Mine", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0621f, 0.0233f, 0.1739f), new Vector3(-33.01f, -133.78f, 45.52f), 1.1f) },
            { "PCI_I_Kick", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0566f, 0.1015f, 0.0016f), new Vector3(36.75f, 165.24f, 104.03f), 0.749f) },
            { "PCI_I_Tax", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1353f, 0.0341f, 0.2307f), new Vector3(33.01f, 46.22f, -45.52f), 1.002f) },
            { "PCI_I_Ticket", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0732f, 0.0976f, 0.1889f), new Vector3(33.01f, 46.22f, -45.52f), 0.923f) },
            { "PCI_I_Banana", new HeldPose(PlayerBone.LeftHand, new Vector3(0.168f, 0.1053f, 0.0958f), new Vector3(50.19f, 11.64f, -142.66f), 0.443f) },
            { "PCI_I_Icicle", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1183f, 0.1006f, 0.0492f), new Vector3(-22.12f, 105.14f, 127.61f), 0.757f) },
            { "PCI_I_Boomerang", new HeldPose(PlayerBone.LeftHand, new Vector3(0.2967f, 0.0958f, 0.0514f), new Vector3(21.2f, -141.2f, 7.18f), 0.769f) },
            { "PCI_I_Ricochet", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0568f, 0.1205f, 0.0286f), new Vector3(36.75f, 165.24f, 104.03f), 0.733f) },
            { "PCI_I_DeathWand", new HeldPose(PlayerBone.LeftHand, new Vector3(0.146f, 0.07f, 0.0455f), new Vector3(-56.53f, -166.37f, 78.57f), 1.143f) },
            { "PCI_I_Vacuum", new HeldPose(PlayerBone.LeftHand, new Vector3(-0.0246f, 0.1866f, -0.0673f), new Vector3(36.75f, 165.24f, 104.03f), 0.82f) },
            { "PCI_I_Grapple", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0435f, 0.0673f, 0.0472f), new Vector3(-50.45f, -141.08f, 58.1f), 0.731f) },
            { "PCI_I_Signpost", new HeldPose(PlayerBone.LeftHand, new Vector3(0.1029f, 0.035f, 0.088f), new Vector3(-51.02f, 142.57f, 107.99f), 1.031f) },
            { "PCI_I_LifeMagnet", new HeldPose(PlayerBone.LeftHand, new Vector3(0.0869f, 0.2114f, -0.0337f), new Vector3(11.2f, 66.74f, 142.42f), 0.592f) },
        };

        private static readonly Dictionary<string, HeldPose> s_file = new Dictionary<string, HeldPose>();

        internal static string FilePath
        {
            get
            {
                return Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "UserData"),
                                                 "PummelCustomItems"), FileName);
            }
        }

        /// <summary>Writes the pose for <paramref name="key"/> into the details, if one is known.</summary>
        internal static void ApplyTo(string key, ItemDetails d)
        {
            HeldPose p;
            if (!s_file.TryGetValue(key, out p) && !s_table.TryGetValue(key, out p)) return;

            d.heldBone = p.Bone;
            d.heldPosition = p.Position;
            d.heldRotation = p.Rotation;
            d.heldScale = Vector3.one * p.Scale;
        }

        /// <summary>
        /// Reads the override file. Lines are
        /// <c>key  bone  px py pz  rx ry rz  scale</c>, and # starts a comment.
        /// </summary>
        internal static void ReloadFile()
        {
            s_file.Clear();
            string path = FilePath;
            if (!File.Exists(path)) return;

            int n = 0;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                string[] f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length == 0) continue;

                try
                {
                    if (f.Length != 9) throw new FormatException("expected 9 fields, got " + f.Length);
                    PlayerBone bone = (PlayerBone)Enum.Parse(typeof(PlayerBone), f[1], true);
                    s_file[f[0]] = new HeldPose(bone,
                        new Vector3(F(f[2]), F(f[3]), F(f[4])),
                        new Vector3(F(f[5]), F(f[6]), F(f[7])),
                        F(f[8]));
                    n++;
                }
                catch (Exception e)
                {
                    Core.Warn("held_poses.txt: skipped '" + raw.Trim() + "': " + e.Message);
                }
            }
            Core.Log("held_poses.txt: " + n + " pose(s) loaded");
        }

        private static float F(string s)
        {
            return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
