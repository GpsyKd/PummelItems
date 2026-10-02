using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PummelCustomItems
{
    /// <summary>
    /// A developer tool for tuning how items sit in the hand. Off unless the file
    /// UserData/PummelCustomItems/pose_studio.on exists, so players never meet it.
    ///
    /// It renders a player holding every item into pose_sheet.png, next to the file above, and
    /// again every time held_poses.txt changes - which turns pose tuning into "edit numbers,
    /// look at the picture", with no rebuild and no restart. Each item gets three views: from
    /// the front, from the side of the hand that holds it, and from behind and above at a
    /// distance, the way the board camera usually sees a player.
    ///
    /// On a board the player is the local one. Anywhere else - the main menu - it borrows the
    /// character model the cosmetics screens use and puts a copy far below the scene, out of
    /// sight, so nothing has to be started for it.
    ///
    /// The first items on the sheet are the game's own ones that are held in the same carrying
    /// pose, for comparison. Axes are drawn at the hand bone (red X, green Y, blue Z), so a
    /// rotation can be worked out from where the bone actually points.
    /// </summary>
    internal static class PoseStudio
    {
        private const int Views = 3;

        // Sheet layout; pose_only.txt can change both with "tile=NNN" and "perrow=N" lines.
        private static int Tile = 180;
        private static int ItemsPerRow = 3;

        private struct Entry
        {
            public string Key;
            public GameObject Prefab;
            public PlayerBone Bone;
            public Vector3 Position, Rotation, Scale;
        }

        // key | addressable path | bone | position | rotation | scale, as the game's item list has them.
        private static readonly string[] s_reference =
        {
            "TacticalCactus|Prefabs/Items/HeldPrefabs/TacticalCactusHeldPrefab|LeftHand|0.05 -0.04 0.08|0 0 0|1",
            "HealthKit|Prefabs/Items/HeldPrefabs/HealthKitHeldPrefab|LeftHand|0.08 0.01 0.09|28.28 -325.06 19.04|1",
            "Present|Prefabs/Items/HeldPrefabs/PresentHeldPrefab|RightHand|0.09 0.14 0.34|98.44 43 104.05|1",
        };

        private static readonly Dictionary<string, GameObject> s_referenceModels = new Dictionary<string, GameObject>();

        private static float s_nextCheck;
        private static DateTime s_seen = DateTime.MinValue;
        private static int s_renderedFor;
        private static bool s_busy;

        private static GameObject s_puppet;
        private static PlayerAnimation s_puppetAnim;

        private static string Dir
        {
            get { return Path.GetDirectoryName(HeldPoses.FilePath); }
        }

        internal static void Update()
        {
            if (s_busy || Time.unscaledTime < s_nextCheck) return;
            s_nextCheck = Time.unscaledTime + 0.5f;

            if (!File.Exists(Path.Combine(Dir, "pose_studio.on")))
            {
                DropPuppet();
                return;
            }

            PlayerAnimation anim;
            if (GameManager.Board != null)
            {
                DropPuppet();
                BoardPlayer me = LocalHuman();
                if (me == null) return;
                anim = me.PlayerAnimation;
            }
            else
            {
                anim = Puppet();
            }
            if (anim == null) return;

            DateTime stamp = Stamp(HeldPoses.FilePath);
            DateTime only = Stamp(Path.Combine(Dir, "pose_only.txt"));
            if (only > stamp) stamp = only;
            if (anim.GetInstanceID() == s_renderedFor && stamp == s_seen) return;
            s_seen = stamp;
            s_renderedFor = anim.GetInstanceID();

            HeldPoses.ReloadFile();
            ItemRegistry.ReapplyHeldPoses();
            MelonCoroutines.Start(Render(anim, anim == s_puppetAnim));
        }

        private static DateTime Stamp(string path)
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }

        private static BoardPlayer LocalHuman()
        {
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp != null && gp.IsLocalPlayer && !gp.IsAI && gp.BoardObject != null) return gp.BoardObject;
            }
            return null;
        }

        // ------------------------------------------------------------------ the stand-in player

        private static PlayerAnimation Puppet()
        {
            if (s_puppetAnim != null) return s_puppetAnim;
            DropPuppet();

            // Whatever player models the game has loaded. Prefer an asset over a live copy (the
            // cosmetics screens rescale bones on theirs), a working animator, no ragdoll physics
            // and nothing that belongs to the network.
            PlayerAnimation best = null;
            int bestScore = int.MinValue;
            foreach (PlayerAnimation pa in Resources.FindObjectsOfTypeAll<PlayerAnimation>())
            {
                if (pa == null) continue;
                GameObject root = pa.transform.root.gameObject;
                Animator a = pa.GetComponent<Animator>();
                bool asset = !root.scene.IsValid();
                bool animated = a != null && a.runtimeAnimatorController != null;
                bool ragdoll = root.GetComponentInChildren<Rigidbody>(true) != null;
                bool networked = root.GetComponentInChildren<BoardPlayer>(true) != null;

                int score = (animated ? 10 : -100) + (asset ? 4 : 0) + (ragdoll ? -50 : 0) + (networked ? -20 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pa;
                }
            }
            if (best == null || bestScore < 0)
            {
                // Early in start-up nothing is loaded yet; the main menu has one.
                Core.Log("pose studio: no player model loaded yet, looking again in 30 s");
                s_nextCheck = Time.unscaledTime + 30f;
                return null;
            }

            // Instantiated under an inactive holder, so nothing on it wakes up before it is placed.
            s_puppet = new GameObject("PCI_PosePuppet");
            s_puppet.SetActive(false);
            s_puppet.transform.position = new Vector3(0f, -500f, 0f);
            GameObject copy = UnityEngine.Object.Instantiate(best.transform.root.gameObject, s_puppet.transform, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            foreach (Rigidbody rb in copy.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

            s_puppetAnim = copy.GetComponentInChildren<PlayerAnimation>(true);
            if (s_puppetAnim == null)
            {
                DropPuppet();
                return null;
            }

            // The game keeps this model switched off until a screen shows it.
            for (Transform t = s_puppetAnim.transform; t != null && t != s_puppet.transform; t = t.parent)
                t.gameObject.SetActive(true);
            s_puppet.SetActive(true);

            // The cosmetics screens switch these on when they show the model; nothing will here.
            s_puppetAnim.enabled = true;
            s_puppetAnim.Setup();
            s_puppetAnim.DisableIdleAnimations();
            Animator animator = s_puppetAnim.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            try
            {
                CharacterSkinCosmetic skin = CosmeticsDatabase.Instance.GetCosmeticWithId(CosmeticType.Skin, 0) as CharacterSkinCosmetic;
                if (skin != null) s_puppetAnim.SetSkin(skin);
                CharacterColorCosmetic colour = CosmeticsDatabase.Instance.GetCosmeticWithId(CosmeticType.Color, 16) as CharacterColorCosmetic;
                if (colour != null) s_puppetAnim.SetPlayerColor(colour.Color);
            }
            catch (Exception e)
            {
                Core.Warn("pose studio: could not dress the stand-in: " + e.Message);
            }

            Core.Log("pose studio: stand-in player made from '" + best.transform.root.name + "'");
            return s_puppetAnim;
        }

        private static void DropPuppet()
        {
            if (s_puppet != null) UnityEngine.Object.Destroy(s_puppet);
            s_puppet = null;
            s_puppetAnim = null;
        }

        // ------------------------------------------------------------------ rendering

        private static IEnumerator Render(PlayerAnimation anim, bool puppet)
        {
            s_busy = true;

            // The pose that matters is the carrying one: every item of ours switches it on while
            // it is held. The game only lets it be set, not read, so it is switched off again
            // afterwards; this is a tool for when nobody is holding anything.
            // Said twice: an animator that has only just been switched on (the stand-in) drops
            // parameters set before its first update.
            // Standing still on the ground, as a board player always is when it uses an item -
            // the stand-in otherwise counts as falling, and the fall pose wins over carrying.
            anim.Carrying = true;
            yield return new WaitForSecondsRealtime(0.2f);
            if (anim != null)
            {
                anim.Grounded = true;
                anim.MovementAxis = Vector2.zero;
                anim.Velocity = 0f;
                anim.VelocityY = 0f;
                anim.Carrying = true;
            }
            yield return new WaitForSecondsRealtime(1.2f);

            if (anim == null)
            {
                s_busy = false;
                yield break;
            }

            Animator animator = anim.GetComponent<Animator>();
            if (animator != null && !HasParameter(animator, "Carrying"))
                Core.Warn("pose studio: this player model has no carrying pose - the sheet shows some other pose");

            List<Entry> items = Filter(Entries());
            if (items.Count == 0) items = Entries();   // a list of layout lines only
            int rows = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)ItemsPerRow));

            Texture2D sheet = new Texture2D(ItemsPerRow * Views * Tile, rows * Tile, TextureFormat.RGB24, false);
            Texture2D tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            RenderTexture rt = new RenderTexture(Tile, Tile, 24);

            // Framed by the character's own height: the stand-in is smaller than a board player.
            float height = anim.skinnedRenderer != null ? Mathf.Clamp(anim.skinnedRenderer.bounds.size.y, 0.3f, 5f) : 1f;
            float near = height * 1.05f, far = height * 1.7f;

            Vector3 fwd = Vector3.ProjectOnPlane(anim.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            GameObject camGo = new GameObject("PCI_PoseCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.21f, 0.24f);
            cam.targetTexture = rt;
            cam.useOcclusionCulling = false;

            // Down there nothing lights the stand-in, so it brings a lamp of its own - a short
            // one, which cannot reach the menu 500 units above.
            GameObject lightGo = null;
            if (puppet)
            {
                lightGo = new GameObject("PCI_PoseLight");
                Light lamp = lightGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 12f;
                lamp.intensity = 1.6f;
                lamp.shadows = LightShadows.None;
                lightGo.transform.position = anim.transform.position + fwd * 2.5f - right * 1.5f + Vector3.up * 3f;
            }

            StringBuilder index = new StringBuilder();
            index.AppendLine("views per item: front | side of the holding hand | behind and above (board camera)");

            for (int i = 0; i < items.Count; i++)
            {
                if (anim == null) break;
                Entry e = items[i];
                Transform bone = anim.GetBone(e.Bone);
                if (bone == null || e.Prefab == null) continue;

                GameObject held = UnityEngine.Object.Instantiate(e.Prefab);
                held.transform.parent = bone;
                held.transform.localScale = e.Scale;
                held.transform.localPosition = e.Position;
                held.transform.localRotation = Quaternion.Euler(e.Rotation);

                GameObject axes = Axes(bone);
                yield return null;

                Vector3 focus = Focus(held, bone.position);
                if (i == 0) Describe(anim, bone, held, focus);
                Vector3 side = e.Bone.ToString().StartsWith("Left", StringComparison.Ordinal) ? -right : right;
                int row = i / ItemsPerRow, col = (i % ItemsPerRow) * Views;

                Shoot(cam, focus, fwd - right * 0.25f + Vector3.up * 0.45f, near, rt, tile, sheet, col, row, rows);
                Shoot(cam, focus, side + Vector3.up * 0.15f, near, rt, tile, sheet, col + 1, row, rows);
                Shoot(cam, focus, -fwd * 0.7f + right * 0.3f + Vector3.up * 1.1f, far, rt, tile, sheet, col + 2, row, rows);

                UnityEngine.Object.Destroy(held);
                UnityEngine.Object.Destroy(axes);
                Bounds mb = ModelBounds(e.Prefab);
                index.AppendLine("row " + (row + 1) + ", item " + (i % ItemsPerRow + 1) + ": " + e.Key +
                                 "  [" + e.Bone + " " + V3(e.Position) + " / " + V3(e.Rotation) + " / " + V3(e.Scale) + "]" +
                                 "  model centre " + F4(mb.center) + " size " + F4(mb.size));
            }

            if (anim != null) anim.Carrying = false;
            cam.targetTexture = null;
            UnityEngine.Object.Destroy(camGo);
            if (lightGo != null) UnityEngine.Object.Destroy(lightGo);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            try
            {
                File.WriteAllBytes(Path.Combine(Dir, "pose_sheet.png"), sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(Dir, "pose_sheet.txt"), index.ToString());
                Core.Log("pose sheet written: " + items.Count + " item(s)" + (puppet ? " on the stand-in" : ""));
            }
            catch (Exception ex)
            {
                Core.Warn("pose sheet failed: " + ex.Message);
            }

            UnityEngine.Object.Destroy(sheet);
            UnityEngine.Object.Destroy(tile);
            s_busy = false;
        }

        private static List<Entry> Entries()
        {
            List<Entry> list = new List<Entry>();

            foreach (string spec in s_reference)
            {
                string[] f = spec.Split('|');
                GameObject model = ReferenceModel(f[1]);
                if (model == null) continue;
                list.Add(new Entry
                {
                    Key = "vanilla:" + f[0],
                    Prefab = model,
                    Bone = (PlayerBone)Enum.Parse(typeof(PlayerBone), f[2]),
                    Position = ParseV3(f[3]),
                    Rotation = ParseV3(f[4]),
                    Scale = ParseV3(f[5]),
                });
            }

            foreach (KeyValuePair<string, ItemDetails> kv in ItemRegistry.HeldItems())
            {
                ItemDetails d = kv.Value;
                list.Add(new Entry
                {
                    Key = kv.Key,
                    Prefab = d.heldPrefab,
                    Bone = d.heldBone,
                    Position = d.heldPosition,
                    Rotation = d.heldRotation,
                    Scale = d.heldScale,
                });
            }
            return list;
        }

        private static GameObject ReferenceModel(string path)
        {
            GameObject model;
            if (s_referenceModels.TryGetValue(path, out model) && model != null) return model;
            try
            {
                model = Addressables.LoadAssetAsync<GameObject>(path).WaitForCompletion();
            }
            catch (Exception e)
            {
                Core.Warn("pose studio: no reference model at " + path + ": " + e.Message);
                model = null;
            }
            s_referenceModels[path] = model;
            return model;
        }

        /// <summary>
        /// pose_only.txt, if present, lists the item keys to render - one per line. A line
        /// "vanilla" keeps the game's reference items too.
        /// </summary>
        private static List<Entry> Filter(List<Entry> all)
        {
            string only = Path.Combine(Dir, "pose_only.txt");
            Tile = 180;
            ItemsPerRow = 3;
            if (!File.Exists(only)) return all;

            HashSet<string> keep = new HashSet<string>();
            foreach (string l in File.ReadAllLines(only))
            {
                string t = l.Trim();
                int n;
                if (t.StartsWith("tile=", StringComparison.Ordinal) && int.TryParse(t.Substring(5), out n)) Tile = Mathf.Clamp(n, 64, 512);
                else if (t.StartsWith("perrow=", StringComparison.Ordinal) && int.TryParse(t.Substring(7), out n)) ItemsPerRow = Mathf.Clamp(n, 1, 8);
                else if (t.Length > 0) keep.Add(t);
            }

            List<Entry> some = new List<Entry>();
            for (int i = 0; i < all.Count; i++)
            {
                bool vanilla = all[i].Key.StartsWith("vanilla:", StringComparison.Ordinal);
                if (keep.Contains(all[i].Key) || (vanilla && keep.Contains("vanilla"))) some.Add(all[i]);
            }
            return some;
        }

        /// <summary>Halfway between the hand and the middle of the item.</summary>
        private static Vector3 Focus(GameObject held, Vector3 hand)
        {
            Renderer[] rs = held.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return hand;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return Vector3.Lerp(hand, b.center, 0.5f);
        }

        /// <summary>Renders one tile looking at <paramref name="focus"/> from the direction <paramref name="eye"/>.</summary>
        private static void Shoot(Camera cam, Vector3 focus, Vector3 eye, float distance, RenderTexture rt,
                                  Texture2D tile, Texture2D sheet, int col, int row, int rows)
        {
            Vector3 toEye = eye.normalized;
            cam.transform.position = focus + toEye * distance;
            cam.transform.rotation = Quaternion.LookRotation(-toEye);
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tile.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
            tile.Apply();
            RenderTexture.active = prev;

            sheet.SetPixels(col * Tile, (rows - 1 - row) * Tile, Tile, Tile, tile.GetPixels());
        }

        /// <summary>Red X, green Y, blue Z sticks along the bone's own axes.</summary>
        private static GameObject Axes(Transform bone)
        {
            GameObject root = new GameObject("PCI_BoneAxes");
            root.transform.SetParent(bone, false);

            Shader sh = Effects.UnlitShader();
            Color[] colours = { Color.red, Color.green, new Color(0.2f, 0.45f, 1f) };
            Vector3[] dirs = { Vector3.right, Vector3.up, Vector3.forward };
            for (int i = 0; i < 3; i++)
            {
                GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.Destroy(stick.GetComponent<Collider>());
                stick.transform.SetParent(root.transform, false);
                stick.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, dirs[i]);
                stick.transform.localPosition = dirs[i] * 0.12f;

                // Bones can be scaled; undo it so a stick is always about 0.24 long.
                Vector3 s = bone.lossyScale;
                float k = 1f / Mathf.Max(0.0001f, (s.x + s.y + s.z) / 3f);
                stick.transform.localScale = new Vector3(0.012f, 0.012f, 0.24f) * k;
                stick.transform.localPosition *= k;

                Renderer r = stick.GetComponent<Renderer>();
                Material m = OwnedAssets.Own(root, new Material(sh != null ? sh : Shader.Find("Standard")));
                m.color = colours[i];
                r.sharedMaterial = m;
            }
            return root;
        }

        /// <summary>The model's extent in its own space, before any held scale.</summary>
        private static Bounds ModelBounds(GameObject prefab)
        {
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                Bounds part = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = part.center + Vector3.Scale(part.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static bool HasParameter(Animator animator, string name)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }

        /// <summary>Where things are, for when a sheet comes out empty.</summary>
        private static void Describe(PlayerAnimation anim, Transform bone, GameObject held, Vector3 focus)
        {
            Renderer[] rs = held.GetComponentsInChildren<Renderer>();
            Core.Log("pose studio: player at " + anim.transform.position + " active=" + anim.gameObject.activeInHierarchy +
                     ", bone at " + bone.position + " scale " + bone.lossyScale + ", item renderers " + rs.Length +
                     (rs.Length > 0 ? " bounds " + rs[0].bounds + " enabled=" + rs[0].enabled : "") +
                     ", item active=" + held.activeInHierarchy + ", focus " + focus);
            Animator a = anim.GetComponent<Animator>();
            if (a != null)
                Core.Log("pose studio: animator enabled=" + a.isActiveAndEnabled + " initialized=" + a.isInitialized +
                         " parameters=" + a.parameterCount);
            // The hands in the body's own frame (x right, y up, z forward; world units), which
            // is what a pose is worked out from.
            Quaternion toBody = Quaternion.Inverse(anim.transform.rotation);
            foreach (PlayerBone hb in new[] { PlayerBone.LeftHand, PlayerBone.RightHand })
            {
                Transform h = anim.GetBone(hb);
                if (h == null) continue;
                Vector3 at = toBody * (h.position - anim.transform.position);
                Quaternion q = toBody * h.rotation;
                Core.Log("pose studio: frame " + hb + " at " + F4(at) + " rot " +
                         q.x.ToString("0.00000", CultureInfo.InvariantCulture) + " " + q.y.ToString("0.00000", CultureInfo.InvariantCulture) + " " +
                         q.z.ToString("0.00000", CultureInfo.InvariantCulture) + " " + q.w.ToString("0.00000", CultureInfo.InvariantCulture) +
                         " scale " + h.lossyScale.x.ToString("0.00000", CultureInfo.InvariantCulture));
            }
            // Where the fingers and the thumb are, in the hand's own units: that is where a grip goes.
            foreach (PlayerBone[] chain in new[]
            {
                new[] { PlayerBone.LeftHand, PlayerBone.LeftHandIndex1, PlayerBone.LeftHandIndex4End, PlayerBone.LeftHandThumb1, PlayerBone.LeftHandThumb4End },
                new[] { PlayerBone.RightHand, PlayerBone.RightHandIndex1, PlayerBone.RightHandIndex4End, PlayerBone.RightHandThumb1, PlayerBone.RightHandThumb4End },
            })
            {
                Transform h = anim.GetBone(chain[0]);
                if (h == null) continue;
                StringBuilder sb = new StringBuilder();
                for (int k = 1; k < chain.Length; k++)
                {
                    Transform c = anim.GetBone(chain[k]);
                    if (c != null) sb.Append(chain[k] + " " + F4(h.InverseTransformPoint(c.position)) + "; ");
                }
                Core.Log("pose studio: " + chain[0] + " parts " + sb);
            }
            if (anim.skinnedRenderer != null)
                Core.Log("pose studio: body mesh '" + (anim.skinnedRenderer.sharedMesh != null ? anim.skinnedRenderer.sharedMesh.name : "none") +
                         "' enabled=" + anim.skinnedRenderer.enabled + " bounds " + anim.skinnedRenderer.bounds);
        }

        private static Vector3 ParseV3(string s)
        {
            string[] f = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float x = float.Parse(f[0], CultureInfo.InvariantCulture);
            if (f.Length == 1) return Vector3.one * x;
            return new Vector3(x, float.Parse(f[1], CultureInfo.InvariantCulture), float.Parse(f[2], CultureInfo.InvariantCulture));
        }

        private static string F4(Vector3 v)
        {
            return v.x.ToString("0.0000", CultureInfo.InvariantCulture) + " " +
                   v.y.ToString("0.0000", CultureInfo.InvariantCulture) + " " +
                   v.z.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static string V3(Vector3 v)
        {
            return v.x.ToString("0.###", CultureInfo.InvariantCulture) + " " +
                   v.y.ToString("0.###", CultureInfo.InvariantCulture) + " " +
                   v.z.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
