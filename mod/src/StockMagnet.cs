using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PummelCustomItems
{
    /// <summary>
    /// The game's own magnet, borrowed at runtime: its model, recoloured, is the life magnet's,
    /// and its aiming cone - the marker and the rule behind it - is every wedge of ours.
    ///
    /// Nothing of it is shipped. The game loads it from its own files, the same way it does for
    /// its own magnet, and the mod copies only the mesh and a few numbers.
    /// </summary>
    internal static class StockMagnet
    {
        private const string HeldPath = "Prefabs/Items/HeldPrefabs/MagnetItemHeldPrefab";
        private const string ItemPath = "Prefabs/Items/MagnetItem";

        // MagnetItem.Use: a player is caught when nearer than hitDist and the cosine of the angle
        // between the aim and them is above this - a cone of about 38 degrees either side.
        internal const float HitCos = 0.78888f;

        private static bool s_loaded;
        private static GameObject s_held;
        private static GameObject s_cone;
        // The class says 10, but the magnet the game ships is set to 12.5.
        private static float s_hitDist = 12.5f;
        private static Material s_material;

        /// <summary>How far the magnet reaches.</summary>
        internal static float HitDist { get { Load(); return s_hitDist; } }

        /// <summary>Half the cone's opening, in degrees, for whatever has to draw it by hand.</summary>
        internal static float HalfAngle { get { return Mathf.Acos(HitCos) * Mathf.Rad2Deg; } }

        private static void Load()
        {
            if (s_loaded) return;
            s_loaded = true;

            try { s_held = Addressables.LoadAssetAsync<GameObject>(HeldPath).WaitForCompletion(); }
            catch (Exception e) { Core.Warn("stock magnet model unavailable: " + e.Message); }

            try
            {
                GameObject item = Addressables.LoadAssetAsync<GameObject>(ItemPath).WaitForCompletion();
                MagnetItem magnet = item != null ? item.GetComponent<MagnetItem>() : null;
                if (magnet != null)
                {
                    if (magnet.hitDist > 0f) s_hitDist = magnet.hitDist;
                    s_cone = magnet.magnetItemTargeterPrefab;
                }
            }
            catch (Exception e) { Core.Warn("stock magnet item unavailable: " + e.Message); }

            Core.Log("stock magnet: model " + (s_held != null ? "found" : "MISSING") +
                     ", cone " + (s_cone != null ? s_cone.name : "MISSING") + ", reach " + s_hitDist);
        }

        // ------------------------------------------------------------------ the cone

        /// <summary>Whether the magnet, at <paramref name="from"/> and aimed along <paramref name="aim"/>, catches <paramref name="at"/>.</summary>
        internal static bool InCone(Vector3 from, Vector3 aim, Vector3 at)
        {
            Vector3 v = at - from;
            float reach = HitDist;
            return v.sqrMagnitude < reach * reach && Vector3.Dot(aim, v.normalized) > HitCos;
        }

        /// <summary>A copy of the magnet's aiming marker, or null when the game's could not be had.</summary>
        internal static GameObject CreateCone()
        {
            Load();
            if (s_cone == null) return null;

            GameObject cone = UnityEngine.Object.Instantiate(s_cone);
            cone.name = "PCI_MagnetCone";
            // It carries a spinner the magnet itself overrides: the magnet sets the marker's
            // rotation every frame after it has turned, so it never visibly turns. Ours is placed
            // in an Update of its own, which may run before the spinner's - so switch it off.
            Rotator spin = cone.GetComponent<Rotator>();
            if (spin != null) spin.enabled = false;
            return cone;
        }

        /// <summary>Puts the marker where MagnetItem's aiming loop puts it.</summary>
        internal static void PlaceCone(Transform cone, Vector3 from, Vector3 aim)
        {
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            Quaternion yaw = Quaternion.LookRotation(aim.normalized, Vector3.up);
            cone.SetPositionAndRotation(from + yaw * Vector3.forward * (HitDist / 2f) + Vector3.up * 0.5f,
                                        yaw * Quaternion.Euler(90f, 0f, 0f));
        }

        // ----------------------------------------------------------------- the model

        // Bump when the recolouring or the icon's angle changes: it is part of the icon's
        // cache name, and nothing else about it would change.
        private const int Look = 1;

        /// <summary>
        /// The magnet's mesh, laid out the way the game's prefab lays it out, in the life magnet's
        /// colours - just the mesh: none of the prefab's scripts, particles or light come along.
        /// Null when the game's model could not be had.
        /// </summary>
        internal static GameObject Model(string name)
        {
            Load();
            MeshFilter src = s_held != null ? s_held.GetComponentInChildren<MeshFilter>(true) : null;
            if (src == null || src.sharedMesh == null) return null;

            GameObject root = new GameObject(name);
            GameObject body = new GameObject(src.name);
            body.transform.SetParent(root.transform, false);
            Matrix4x4 m = s_held.transform.worldToLocalMatrix * src.transform.localToWorldMatrix;
            body.transform.localPosition = m.GetColumn(3);
            body.transform.localRotation = m.rotation;
            body.transform.localScale = m.lossyScale;

            body.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = Paint(src.GetComponent<MeshRenderer>());
            return root;
        }

        // Where the game's icon camera stands, seen from the model (PreviewRenderCamera's offset).
        private static readonly Vector3 IconCamera = new Vector3(-0.5f, 0.6f, 1f).normalized;

        /// <summary>
        /// The same magnet as one mesh, turned the way the game's own magnet icon shows it - the
        /// bend at the lower left, the poles up and to the right and a little towards the viewer.
        /// The icon renderer ignores an object's rotation, so the angle has to be in the vertices.
        /// </summary>
        internal static GameObject IconModel(string name)
        {
            return IconModel(name, 40f, 0.35f, false);
        }

        internal static GameObject IconModel(string name, float screenAngle, float towardViewer, bool flip)
        {
            Load();
            MeshFilter src = s_held != null ? s_held.GetComponentInChildren<MeshFilter>(true) : null;
            if (src == null || src.sharedMesh == null || !src.sharedMesh.isReadable) return null;

            // The mesh's poles point along its +z, its flat faces along x. Aim the poles at the
            // screen angle asked for, tipped towards the camera, with a flat face to the camera.
            Vector3 look = -IconCamera;
            Vector3 right = Vector3.Cross(Vector3.up, look).normalized;
            Vector3 up = Vector3.Cross(look, right);
            float a = screenAngle * Mathf.Deg2Rad;
            Vector3 poles = (right * Mathf.Cos(a) + up * Mathf.Sin(a) + IconCamera * towardViewer).normalized;
            Vector3 face = Vector3.ProjectOnPlane(IconCamera, poles).normalized;
            if (flip) face = -face;
            Quaternion view = Quaternion.LookRotation(poles, Vector3.Cross(poles, face));

            Mesh from = src.sharedMesh;
            Vector3[] verts = from.vertices;
            Vector3[] normals = from.normals;
            for (int i = 0; i < verts.Length; i++) verts[i] = view * verts[i];
            for (int i = 0; i < normals.Length; i++) normals[i] = view * normals[i];

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = from.uv;
            mesh.triangles = from.triangles;
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name + "_v" + Look);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Paint(src.GetComponent<MeshRenderer>());
            return go;
        }

        /// <summary>
        /// The game's material on a recoloured copy of its texture. The texture is a palette - an
        /// 8-pixel red swatch the body is mapped to, pale ones for the poles with "N" and "S" on
        /// them - so swapping the colours swaps the parts: the body takes a near-black, the poles
        /// the magnet's own red, and the letters go black.
        /// </summary>
        private static Material Paint(MeshRenderer src)
        {
            if (s_material != null) return s_material;

            Material game = src != null ? src.sharedMaterial : null;
            s_material = game != null ? new Material(game) : new Material(Shader.Find("Standard"));
            s_material.name = "PCI_LifeMagnet_Mat";
            s_material.hideFlags = HideFlags.HideAndDontSave;

            Texture2D tex = game != null ? Recoloured(game.mainTexture) : null;
            if (tex != null) s_material.mainTexture = tex;
            else s_material.color = Body;
            return s_material;
        }

        private static readonly Color32 Body = new Color32(26, 26, 28, 255);

        private static Texture2D Recoloured(Texture src)
        {
            if (src == null) return null;

            // The game's texture cannot be read directly; drawn into a render texture it can.
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Texture2D tex;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                // No mipmaps: the swatches are 8 pixels wide on a grey ground, and the smaller
                // mip levels would bleed that grey into them.
                tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            Color32[] px = tex.GetPixels32();

            // The magnet's own red, taken from its texture, becomes the poles' colour.
            int rs = 0, gs = 0, bs = 0, n = 0;
            for (int i = 0; i < px.Length; i++)
                if (IsRed(px[i])) { rs += px[i].r; gs += px[i].g; bs += px[i].b; n++; }
            Color32 pole = n > 0 ? new Color32((byte)(rs / n), (byte)(gs / n), (byte)(bs / n), 255)
                                 : new Color32(192, 62, 63, 255);

            for (int i = 0; i < px.Length; i++)
            {
                Color32 p = px[i];
                if (IsRed(p)) { px[i] = Body; continue; }
                // Pale is pole, dark is lettering or unused ground; the edges of the letters
                // blend between the two.
                float k = Mathf.InverseLerp(150f, 200f, Mathf.Min(p.r, Mathf.Min(p.g, p.b)));
                px[i] = Color32.Lerp(Body, pole, k);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.name = "PCI_LifeMagnet_Albedo";
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static bool IsRed(Color32 p)
        {
            return p.r - Mathf.Max(p.g, p.b) > 60;
        }
    }
}
