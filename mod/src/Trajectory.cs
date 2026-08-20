using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// The flight an item will make, sampled into points so it can be drawn before the throw.
    ///
    /// This is deliberately not "a parabola": not everything flies one. The boomerang follows
    /// a scripted curve out and back, and drawing an arc for it would be a confident lie. So
    /// each kind of flight supplies its own sampling and the drawing code stays ignorant of
    /// which is which.
    /// </summary>
    public abstract class TrajectoryPreview
    {
        /// <summary>Ring drawn where it comes down. Zero means nothing lands - draw no ring.</summary>
        internal virtual float BlastRadius { get { return 0f; } }

        /// <summary>
        /// Writes the path into <paramref name="points"/> and returns how many were written.
        /// </summary>
        internal abstract int Sample(Vector3 playerPos, Vector3 dir, Vector3[] points,
                                     out Vector3 landing, out bool landed);
    }

    /// <summary>Thrown and left to gravity: grenade, banana, sticky bomb.</summary>
    public sealed class BallisticPreview : TrajectoryPreview
    {
        private const int MuzzleSteps = 3;      // the arc starts inside the thrower; ignore hits there

        private readonly float m_speed;
        private readonly float m_lift;
        private readonly float m_blastRadius;

        public BallisticPreview(float speed, float lift, float blastRadius)
        {
            m_speed = speed;
            m_lift = lift;
            m_blastRadius = blastRadius;
        }

        internal override float BlastRadius { get { return m_blastRadius; } }

        /// <summary>
        /// Steps the same integration Unity's rigidbody does - gravity, then linear drag, then
        /// position, once per fixed frame - from the same launch velocity the throw will use.
        /// What gets drawn is what will happen, rather than an approximation of it.
        /// </summary>
        internal override int Sample(Vector3 playerPos, Vector3 dir, Vector3[] points,
                                     out Vector3 landing, out bool landed)
        {
            landing = Vector3.zero;
            landed = false;

            // Exactly the launch state ThrownItemBase.SpawnOne will use.
            Vector3 p = playerPos + Vector3.up * 1.4f + dir * 0.6f;
            Vector3 v = dir * m_speed + Vector3.up * m_lift;

            float dt = Time.fixedDeltaTime;
            Vector3 g = Physics.gravity;
            float dragFactor = 1f / (1f + ProjectileSpec.LinearDrag * dt);

            // Everything except the players themselves - the arc should stop at the ground or
            // a wall, not at whoever happens to be standing in the way.
            int mask = Physics.DefaultRaycastLayers & ~(1 << 8);

            points[0] = p;
            int count = 1;

            while (count < points.Length)
            {
                v += g * dt;
                v *= dragFactor;
                Vector3 next = p + v * dt;

                RaycastHit hit;
                if (count > MuzzleSteps &&
                    Physics.Linecast(p, next, out hit, mask, QueryTriggerInteraction.Ignore))
                {
                    points[count++] = hit.point;
                    landing = hit.point;
                    landed = true;
                    break;
                }

                p = next;
                points[count++] = p;
            }

            return count;
        }
    }

    /// <summary>
    /// Out and back along a scripted curve. Nothing lands, so no ring is drawn - the useful
    /// information here is the shape of the sweep and which side the return leg comes back on.
    /// </summary>
    public sealed class BoomerangPreview : TrajectoryPreview
    {
        internal override int Sample(Vector3 playerPos, Vector3 dir, Vector3[] points,
                                     out Vector3 landing, out bool landed)
        {
            landing = Vector3.zero;
            landed = false;

            Vector3 origin = playerPos + Vector3.up * Boomerang.LaunchHeight;
            Vector3 forward = dir.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            int steps = Mathf.Min(60, points.Length);
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                points[i] = Boomerang.PathAt(origin, forward, right, t);
            }
            return steps;
        }
    }

    /// <summary>
    /// The flight an item will make, drawn while aiming, with a ring on the ground where it
    /// comes down.
    ///
    /// An arrow only gives a direction, which for something lobbed over a distance says very
    /// little - the interesting question is where it lands, and how much of the board that
    /// takes with it.
    /// </summary>
    internal class TrajectoryArc : MonoBehaviour
    {
        private const int MaxPoints = 90;

        private LineRenderer m_line;
        private Material m_lineMat;
        private Transform m_marker;
        private Material m_markerMat;

        private TrajectoryPreview m_spec;
        private Color m_color;

        private readonly Vector3[] m_points = new Vector3[MaxPoints];

        internal static TrajectoryArc Create(Color color, TrajectoryPreview spec)
        {
            GameObject host = new GameObject("PCI_Trajectory");
            TrajectoryArc a = host.AddComponent<TrajectoryArc>();
            a.Build(color, spec);
            return a;
        }

        private void Build(Color color, TrajectoryPreview spec)
        {
            m_spec = spec;
            m_color = color;

            Shader sh = Effects.UnlitShader();

            m_line = gameObject.AddComponent<LineRenderer>();
            m_line.useWorldSpace = true;
            m_line.alignment = LineAlignment.View;
            m_line.numCapVertices = 2;
            m_line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_line.receiveShadows = false;

            m_lineMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            m_line.material = m_lineMat;

            // Thick and solid where the item leaves the hand, thin and faint at the far end -
            // reads as a direction of travel instead of a static rope.
            m_line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.16f), new Keyframe(1f, 0.05f));
            m_line.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0.25f, 1f),
                },
            };

            if (m_spec.BlastRadius > 0f) BuildMarker(sh);
        }

        /// <summary>Ring on the ground: centre is the landing spot, edge is the blast reach.</summary>
        private void BuildMarker(Shader sh)
        {
            const int segments = 40;
            float outer = Mathf.Max(0.6f, m_spec.BlastRadius);
            float inner = outer * 0.86f;

            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts[i * 2] = new Vector3(Mathf.Cos(a) * inner, 0f, Mathf.Sin(a) * inner);
                verts[i * 2 + 1] = new Vector3(Mathf.Cos(a) * outer, 0f, Mathf.Sin(a) * outer);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1;
                int c = ((i + 1) % segments) * 2, d = c + 1;
                tris[i * 6] = a; tris[i * 6 + 1] = b; tris[i * 6 + 2] = d;
                tris[i * 6 + 3] = a; tris[i * 6 + 4] = d; tris[i * 6 + 5] = c;
            }

            Mesh mesh = new Mesh { name = "PCI_LandRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = ring.AddComponent<MeshRenderer>();
            m_markerMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            m_markerMat.color = new Color(m_color.r, m_color.g, m_color.b, 0.4f);
            mr.material = m_markerMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            m_marker = ring.transform;
            m_marker.gameObject.SetActive(false);
        }

        internal void Aim(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            Vector3 landing;
            bool landed;
            int count = m_spec.Sample(origin, direction.normalized, m_points, out landing, out landed);

            m_line.positionCount = count;
            m_line.SetPositions(m_points);

            if (m_marker == null) return;
            m_marker.gameObject.SetActive(landed);
            if (landed) m_marker.position = landing + Vector3.up * 0.06f;
        }

        internal void Dismiss()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            // Same slow pulse the other indicators use, so they feel like one family.
            if (m_markerMat == null) return;
            Color c = m_markerMat.color;
            c.a = 0.30f + 0.16f * Mathf.Sin(Time.time * 5f);
            m_markerMat.color = c;
        }
    }
}
