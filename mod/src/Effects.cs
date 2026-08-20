using System.Collections;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Stand-in visual effects built from primitives. The game's own explosion prefabs are
    /// off-limits by design (nothing borrowed from the game), and a particle system cannot
    /// be described in code as cheaply as a mesh - so for now a blast is an expanding,
    /// fading shell. Good enough to read at a glance; replaceable later.
    /// </summary>
    internal static class Effects
    {
        private static Shader s_unlit;

        /// <summary>
        /// An unlit, alpha-friendly shader that this build actually has. Shader.Find only
        /// sees what was compiled into the game, and a name that is not there yields null -
        /// which then renders as the bright magenta error material.
        /// </summary>
        internal static Shader UnlitShader()
        {
            if (s_unlit != null) return s_unlit;

            string[] candidates =
            {
                "Sprites/Default",
                "Unlit/Transparent",
                "Particles/Standard Unlit",
                "Legacy Shaders/Transparent/Diffuse",
                "Standard",
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Shader s = Shader.Find(candidates[i]);
                if (s != null)
                {
                    s_unlit = s;
                    Core.Log("effects shader: " + candidates[i]);
                    return s_unlit;
                }
            }

            Core.Warn("no usable effects shader found - effects will look wrong");
            return null;
        }

        internal static void Blast(Vector3 position, float radius)
        {
            Explosion.Play(position, radius);
        }
    }

    /// <summary>
    /// Ground wedge showing exactly what an item will sweep - reach and angle both. An arrow
    /// only says "that way", which for a cone leaves the player guessing how wide it is; the
    /// game's own magnet lays a shaped marker down for the same reason.
    /// </summary>
    internal class SectorIndicator : MonoBehaviour
    {
        private Material m_mat;

        internal static SectorIndicator Create(Color color, float reach, float halfAngle)
        {
            GameObject host = new GameObject("PCI_Sector");
            SectorIndicator s = host.AddComponent<SectorIndicator>();
            s.Build(color, reach, halfAngle);
            return s;
        }

        private void Build(Color color, float reach, float halfAngle)
        {
            const int steps = 24;

            Vector3[] verts = new Vector3[steps + 2];
            int[] tris = new int[steps * 3];

            verts[0] = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, (float)i / steps) * Mathf.Deg2Rad;
                // Flat on the ground: the wedge opens along +Z, which is the aim direction.
                verts[i + 1] = new Vector3(Mathf.Sin(a) * reach, 0f, Mathf.Cos(a) * reach);
            }
            for (int i = 0; i < steps; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new Mesh();
            mesh.name = "PCI_SectorMesh";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();

            Shader sh = Effects.UnlitShader();
            m_mat = (sh != null) ? new Material(sh) : null;
            if (m_mat != null)
            {
                m_mat.color = new Color(color.r, color.g, color.b, 0.30f);
                mr.material = m_mat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Drawn on top of the board so it is not swallowed by the ground.
            mr.sortingOrder = 10;
        }

        internal void Aim(Vector3 origin, Vector3 direction)
        {
            transform.position = origin + Vector3.up * 0.14f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        internal void Dismiss()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            if (m_mat == null) return;
            Color c = m_mat.color;
            c.a = 0.22f + 0.12f * Mathf.Sin(Time.time * 5f);
            m_mat.color = c;
        }
    }

    /// <summary>
    /// Ground arrow showing where a thrown item will go. Without it the player is aiming
    /// blind - the direction was always being read, there was just nothing to look at.
    /// </summary>
    internal class AimArrow : MonoBehaviour
    {
        private Transform m_shaft;
        private Transform m_head;
        private Material m_mat;

        internal static AimArrow Create(Color color)
        {
            GameObject host = new GameObject("PCI_AimArrow");
            AimArrow a = host.AddComponent<AimArrow>();
            a.Build(color);
            return a;
        }

        private void Build(Color color)
        {
            Shader sh = Effects.UnlitShader();
            m_mat = new Material(sh != null ? sh : Shader.Find("Standard"));
            m_mat.color = new Color(color.r, color.g, color.b, 0.75f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Shaft";
            shaft.transform.SetParent(transform, false);
            shaft.transform.localScale = new Vector3(0.28f, 0.02f, 2.4f);
            shaft.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            Dress(shaft);
            m_shaft = shaft.transform;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localScale = new Vector3(0.62f, 0.02f, 0.62f);
            head.transform.localPosition = new Vector3(0f, 0f, 3.0f);
            head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Dress(head);
            m_head = head.transform;
        }

        private void Dress(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            r.material = m_mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Re-points the arrow. Origin is lifted slightly to avoid z-fighting.</summary>
        internal void Aim(Vector3 origin, Vector3 direction)
        {
            transform.position = origin + Vector3.up * 0.12f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        internal void Dismiss()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            // Gentle pulse so it reads as "live" rather than a decal on the floor.
            if (m_mat == null) return;
            Color c = m_mat.color;
            c.a = 0.55f + 0.25f * Mathf.Sin(Time.time * 6f);
            m_mat.color = c;
        }
    }
}
