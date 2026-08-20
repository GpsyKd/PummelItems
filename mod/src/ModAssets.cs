using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Loads our AssetBundle - models, materials and sounds built in Unity 2021.3.45f2,
    /// the exact build the game runs (changeset 88f88f591b2e).
    ///
    /// Expected on disk at:
    ///   &lt;game&gt;/UserData/PummelCustomItems/pciassets
    ///
    /// Everything degrades gracefully: with no bundle the items still work, they just
    /// fall back to placeholder shapes.
    /// </summary>
    internal static class ModAssets
    {
        internal const string BundleFileName = "pciassets";

        private static AssetBundle s_bundle;
        private static bool s_tried;

        private static readonly Dictionary<string, GameObject> s_prefabs =
            new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, AudioClip> s_clips =
            new Dictionary<string, AudioClip>();

        internal static string BundlePath
        {
            get
            {
                return Path.Combine(
                    Path.Combine(Directory.GetCurrentDirectory(), "UserData"),
                    Path.Combine("PummelCustomItems", BundleFileName));
            }
        }

        internal static bool Loaded { get { return s_bundle != null; } }

        internal static void EnsureLoaded()
        {
            if (s_tried) return;
            s_tried = true;

            string path = BundlePath;
            if (!File.Exists(path))
            {
                Core.Warn("asset bundle not found at " + path + " - using placeholder art");
                return;
            }

            try
            {
                s_bundle = AssetBundle.LoadFromFile(path);
                if (s_bundle == null)
                {
                    Core.Warn("AssetBundle.LoadFromFile returned null for " + path);
                    return;
                }

                string[] names = s_bundle.GetAllAssetNames();
                Core.Log("asset bundle loaded, " + names.Length + " asset(s)");
                LogAudio(names);
                DumpMaterials(names);
            }
            catch (Exception e)
            {
                Core.Warn("asset bundle load failed: " + e);
            }
        }

        /// <summary>Prefab by asset name, without path or extension. Null if missing.</summary>
        internal static GameObject Prefab(string name)
        {
            EnsureLoaded();
            if (s_bundle == null) return null;

            GameObject cached;
            if (s_prefabs.TryGetValue(name, out cached)) return cached;

            GameObject go = null;
            try { go = s_bundle.LoadAsset<GameObject>(name); }
            catch (Exception e) { Core.Warn("LoadAsset<GameObject>(" + name + ") failed: " + e); }

            if (go == null) Core.Warn("prefab '" + name + "' not in bundle");
            else RebindShaders(go);

            s_prefabs[name] = go;
            return go;
        }

        internal static AudioClip Clip(string name)
        {
            EnsureLoaded();
            if (s_bundle == null) return null;

            AudioClip cached;
            if (s_clips.TryGetValue(name, out cached)) return cached;

            AudioClip clip = null;
            try { clip = s_bundle.LoadAsset<AudioClip>(name); }
            catch (Exception e) { Core.Warn("LoadAsset<AudioClip>(" + name + ") failed: " + e); }

            if (clip == null) Core.Warn("clip '" + name + "' not in bundle");
            s_clips[name] = clip;
            return clip;
        }

        /// <summary>
        /// Re-links every material to the shader instance that lives in the running game.
        ///
        /// A shader reference inside an AssetBundle points at the copy compiled with the
        /// bundle, which does not resolve against another build - Unity then falls back to
        /// its error material and the object turns bright magenta. Looking the shader up by
        /// name in the host process and reassigning it fixes that, and is the standard price
        /// of shipping bundles into a game you did not build.
        /// </summary>
        /// <summary>
        /// Names of the clips the bundle actually carries. Sounds are looked up by name at
        /// the moment they play, so a clip that failed to make it in stays invisible until
        /// something is silent mid-game - listing them at load turns that into one glance.
        /// </summary>
        private static void LogAudio(string[] assetNames)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int n = 0;

            for (int i = 0; i < assetNames.Length; i++)
            {
                string a = assetNames[i];
                if (!a.EndsWith(".ogg") && !a.EndsWith(".wav") && !a.EndsWith(".mp3")) continue;

                int slash = a.LastIndexOf('/');
                if (n++ > 0) sb.Append(", ");
                sb.Append(a.Substring(slash + 1));
            }

            Core.Log("bundled audio (" + n + "): " + sb);
        }

        /// <summary>
        /// One-shot report of what every bundled model actually carries: submesh count per
        /// renderer against material count, plus each material's shader and colour. A
        /// renderer with more submeshes than materials is drawn with the error material -
        /// the bright magenta - and no amount of recolouring would fix that, so it has to be
        /// measured rather than guessed at.
        /// </summary>
        private static void DumpMaterials(string[] assetNames)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("# material / submesh report");
                sb.AppendLine();

                int mismatches = 0;

                for (int i = 0; i < assetNames.Length; i++)
                {
                    if (!assetNames[i].EndsWith(".prefab")) continue;

                    GameObject go = s_bundle.LoadAsset<GameObject>(assetNames[i]);
                    if (go == null) continue;

                    sb.AppendLine("== " + go.name + " ==");

                    Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rs.Length; r++)
                    {
                        MeshFilter mf = rs[r].GetComponent<MeshFilter>();
                        int sub = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.subMeshCount : -1;
                        Material[] mats = rs[r].sharedMaterials;

                        bool bad = (sub >= 0 && sub != mats.Length);
                        if (bad) mismatches++;

                        sb.AppendLine("  " + rs[r].gameObject.name +
                                      "  submeshes=" + sub + " materials=" + mats.Length +
                                      (bad ? "   <<< MISMATCH" : ""));

                        for (int j = 0; j < mats.Length; j++)
                        {
                            Material m = mats[j];
                            if (m == null) { sb.AppendLine("      [" + j + "] <null material>"); continue; }
                            sb.AppendLine("      [" + j + "] " + m.name +
                                          "  shader=" + (m.shader != null ? m.shader.name : "<null>") +
                                          "  color=" + ReadColor(m, "_Color", Color.white));
                        }
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("mismatched renderers: " + mismatches);

                string dir = Path.Combine(Directory.GetCurrentDirectory(),
                                          Path.Combine("UserData", "PummelCustomItems"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "material_dump.txt");
                File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));

                Core.Log("material report written to " + path + " (mismatches: " + mismatches + ")");
            }
            catch (Exception e)
            {
                Core.Warn("material report failed: " + e);
            }
        }

        private static Shader s_standard;
        private static bool s_loggedSample;

        private static void RebindShaders(GameObject prefab)
        {
            try
            {
                if (s_standard == null) s_standard = Shader.Find("Standard");
                if (s_standard == null)
                {
                    Core.Warn("no Standard shader in this game - models will stay untinted");
                    return;
                }

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                int rebuilt = 0;

                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] mats = renderers[i].sharedMaterials;
                    bool touched = false;

                    for (int j = 0; j < mats.Length; j++)
                    {
                        Material src = mats[j];
                        if (src == null) continue;

                        Color baseColor = ReadColor(src, "_Color", Color.white);
                        Color emission = ReadColor(src, "_EmissionColor", Color.black);

                        if (!s_loggedSample)
                        {
                            s_loggedSample = true;
                            Core.Log("material sample: name=" + src.name +
                                     " shader=" + (src.shader != null ? src.shader.name : "<null>") +
                                     " has_Color=" + src.HasProperty("_Color") +
                                     " color=" + baseColor);
                        }

                        // Built fresh against the running game's shader rather than patched.
                        // A material serialised into a bundle carries state from the build it
                        // came from, and anything the host build does not accept lands on the
                        // error material - the bright magenta.
                        Material m = new Material(s_standard);
                        m.name = src.name + "_live";
                        m.color = baseColor;

                        if (emission.maxColorComponent > 0.01f)
                        {
                            m.EnableKeyword("_EMISSION");
                            m.SetColor("_EmissionColor", emission);
                        }

                        mats[j] = m;
                        touched = true;
                        rebuilt++;
                    }

                    if (touched) renderers[i].sharedMaterials = mats;
                }

                if (rebuilt > 0)
                    Core.Log("materials rebuilt on " + prefab.name + ": " + rebuilt);
            }
            catch (Exception e)
            {
                Core.Warn("material rebuild failed for " + prefab.name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Colour of a bundled material, tolerating a material whose shader never resolved -
        /// HasProperty answers against the shader, so it can lie about a value that is
        /// perfectly well serialised.
        /// </summary>
        private static Color ReadColor(Material m, string property, Color fallback)
        {
            try
            {
                if (m.HasProperty(property)) return m.GetColor(property);
            }
            catch { }
            return fallback;
        }

        /// <summary>
        /// Plays a bundled clip through the game's own audio system, so it respects the
        /// player's volume settings. Silently does nothing if the clip is missing.
        /// </summary>
        internal static void Play(string clipName, float volume = 1f)
        {
            AudioClip clip = Clip(clipName);
            if (clip == null) return;
            try { AudioSystem.PlayOneShot(clip, volume); }
            catch (Exception e) { Core.Warn("PlayOneShot(" + clipName + ") failed: " + e.Message); }
        }

        /// <summary>
        /// A model from the bundle, or the given placeholder when the bundle has no such
        /// asset. Instantiating decouples us from the bundle's own copy.
        /// </summary>
        internal static GameObject ModelOrPlaceholder(string assetName, Func<GameObject> placeholder)
        {
            GameObject src = Prefab(assetName);
            if (src != null)
            {
                GameObject copy = UnityEngine.Object.Instantiate(src);
                copy.name = assetName;
                return copy;
            }
            return placeholder != null ? placeholder() : null;
        }
    }
}
