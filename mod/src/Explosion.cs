using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// A blast built out of several short-lived pieces rather than one expanding shell.
    ///
    /// A single fading sphere reads as a bubble, because a real explosion is not one shape -
    /// it is a hard flash, a boiling cloud that outlives it, a ring running out along the
    /// ground, and debris thrown clear. Each of those is trivial on its own; layering them
    /// with different lifetimes is what makes the whole thing land. Everything here is
    /// primitives and procedural mesh, so nothing is borrowed from the game.
    /// </summary>
    internal class Explosion : MonoBehaviour
    {
        private const float Life = 1.1f;        // longest-lived piece decides when to clean up

        private float m_t;
        private float m_radius;

        private readonly List<Part> m_parts = new List<Part>();
        private Light m_light;
        private static Mesh s_ringMesh;

        /// <summary>One drawn piece plus the little bit of state that animates it.</summary>
        private class Part
        {
            public Transform Tr;
            public Material Mat;
            public float Delay;         // starts this many seconds in
            public float Duration;
            public float FromScale, ToScale;
            public Vector3 Drift;       // world units per second
            public Vector3 Velocity;    // ballistic pieces only
            public bool Ballistic;
            public Color Start, End;
            public float StartAlpha;
        }

        internal static void Play(Vector3 position, float radius)
        {
            GameObject host = new GameObject("PCI_Explosion");
            host.transform.position = position;
            host.AddComponent<Explosion>().Begin(radius);
        }

        private void Begin(float radius)
        {
            m_radius = Mathf.Max(0.5f, radius);

            BuildLight();
            BuildFlash();
            BuildFireball();
            BuildShockwave();
            BuildSparks();
        }

        // ------------------------------------------------------------------ construction

        /// <summary>
        /// The one piece that lights the surroundings rather than just drawing over them.
        /// Cheap, and it does more for the sense of a blast than any of the geometry.
        /// </summary>
        private void BuildLight()
        {
            GameObject go = new GameObject("Light");
            go.transform.SetParent(transform, false);

            m_light = go.AddComponent<Light>();
            m_light.type = LightType.Point;
            m_light.color = new Color(1f, 0.62f, 0.22f);
            m_light.range = m_radius * 4f;
            m_light.intensity = 9f;
            m_light.shadows = LightShadows.None;
        }

        private void BuildFlash()
        {
            Part p = MakeSphere(Vector3.zero, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.7f, 0.3f));
            p.Delay = 0f;
            p.Duration = 0.13f;
            p.FromScale = m_radius * 0.5f;
            p.ToScale = m_radius * 1.25f;
            p.StartAlpha = 1f;
        }

        /// <summary>
        /// Overlapping puffs at slightly different sizes, offsets and start times. The
        /// staggering is the whole trick - identical spheres would just look like one sphere.
        /// </summary>
        private void BuildFireball()
        {
            const int puffs = 7;
            for (int i = 0; i < puffs; i++)
            {
                Vector3 off = Random.insideUnitSphere * m_radius * 0.42f;
                off.y = Mathf.Abs(off.y) * 0.6f;

                Part p = MakeSphere(off,
                                    new Color(1f, 0.55f, 0.12f),
                                    new Color(0.22f, 0.20f, 0.19f));   // burns down to smoke
                p.Delay = i * 0.035f;
                p.Duration = Random.Range(0.55f, 0.85f);
                p.FromScale = m_radius * 0.18f;
                p.ToScale = m_radius * Random.Range(0.55f, 0.95f);
                p.Drift = new Vector3(off.x, Mathf.Abs(off.y) + 0.9f, off.z).normalized * 1.4f;
                p.StartAlpha = 0.95f;
            }
        }

        /// <summary>A flat ring running outward along the ground, past the damage radius.</summary>
        private void BuildShockwave()
        {
            GameObject go = new GameObject("Shockwave");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            go.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Material mat = NewMat();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            m_parts.Add(new Part
            {
                Tr = go.transform,
                Mat = mat,
                Delay = 0.02f,
                Duration = 0.42f,
                FromScale = m_radius * 0.25f,
                ToScale = m_radius * 1.7f,
                Start = new Color(1f, 0.85f, 0.5f),
                End = new Color(1f, 0.45f, 0.15f),
                StartAlpha = 0.75f,
            });
        }

        private void BuildSparks()
        {
            const int sparks = 16;
            for (int i = 0; i < sparks; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Spark";
                go.transform.SetParent(transform, false);
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);

                float s = m_radius * 0.055f;
                go.transform.localScale = new Vector3(s * 0.45f, s * 0.45f, s * 2.6f);

                Material mat = NewMat();
                Renderer r = go.GetComponent<Renderer>();
                r.material = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.25f;
                dir.Normalize();

                m_parts.Add(new Part
                {
                    Tr = go.transform,
                    Mat = mat,
                    Delay = 0f,
                    Duration = Random.Range(0.35f, 0.6f),
                    FromScale = 1f,
                    ToScale = 1f,
                    Ballistic = true,
                    Velocity = dir * m_radius * Random.Range(2.6f, 5.2f),
                    Start = new Color(1f, 0.9f, 0.5f),
                    End = new Color(1f, 0.3f, 0.08f),
                    StartAlpha = 1f,
                });
            }
        }

        // ---------------------------------------------------------------------- helpers

        private Part MakeSphere(Vector3 localPos, Color start, Color end)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Material mat = NewMat();
            Renderer r = go.GetComponent<Renderer>();
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Part p = new Part { Tr = go.transform, Mat = mat, Start = start, End = end };
            m_parts.Add(p);
            return p;
        }

        private static Material NewMat()
        {
            Shader sh = Effects.UnlitShader();
            return new Material(sh != null ? sh : Shader.Find("Standard"));
        }

        /// <summary>Flat annulus of unit outer radius, shared by every blast.</summary>
        private static Mesh RingMesh()
        {
            if (s_ringMesh != null) return s_ringMesh;

            const int segments = 48;
            const float inner = 0.78f;

            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts[i * 2] = new Vector3(Mathf.Cos(a) * inner, 0f, Mathf.Sin(a) * inner);
                verts[i * 2 + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1;
                int c = ((i + 1) % segments) * 2, d = c + 1;
                tris[i * 6] = a; tris[i * 6 + 1] = b; tris[i * 6 + 2] = d;
                tris[i * 6 + 3] = a; tris[i * 6 + 4] = d; tris[i * 6 + 5] = c;
            }

            s_ringMesh = new Mesh { name = "PCI_ShockRing" };
            s_ringMesh.vertices = verts;
            s_ringMesh.triangles = tris;
            s_ringMesh.RecalculateNormals();
            s_ringMesh.RecalculateBounds();
            return s_ringMesh;
        }

        // ----------------------------------------------------------------------- driving

        private void Update()
        {
            float dt = Time.deltaTime;
            m_t += dt;

            if (m_light != null)
            {
                // Gone well before the smoke is, the way a real flash is.
                float k = Mathf.Clamp01(m_t / 0.3f);
                m_light.intensity = 9f * (1f - k) * (1f - k);
                if (k >= 1f) { Destroy(m_light.gameObject); m_light = null; }
            }

            for (int i = 0; i < m_parts.Count; i++)
            {
                Part p = m_parts[i];
                if (p.Tr == null) continue;

                float age = m_t - p.Delay;
                if (age < 0f) { p.Tr.localScale = Vector3.zero; continue; }

                float k = Mathf.Clamp01(age / p.Duration);

                if (p.Ballistic)
                {
                    p.Velocity += Physics.gravity * 0.65f * dt;
                    p.Tr.localPosition += p.Velocity * dt;
                    if (p.Velocity.sqrMagnitude > 0.01f)
                        p.Tr.rotation = Quaternion.LookRotation(p.Velocity);
                }
                else
                {
                    // Quick out of the gate, settling as it grows - a punch, not a balloon.
                    float e = 1f - Mathf.Pow(1f - k, 3f);
                    p.Tr.localScale = Vector3.one * Mathf.Lerp(p.FromScale, p.ToScale, e);
                    p.Tr.localPosition += p.Drift * dt;
                }

                Color c = Color.Lerp(p.Start, p.End, k);
                c.a = p.StartAlpha * (1f - k * k);   // holds its colour, then drops away fast
                p.Mat.color = c;

                if (k >= 1f && p.Tr.gameObject.activeSelf) p.Tr.gameObject.SetActive(false);
            }

            if (m_t >= Life) Destroy(gameObject);
        }
    }
}
