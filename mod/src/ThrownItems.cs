using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    /// <summary>
    /// Shared behaviour for every aim-and-throw item: a ground arrow while aiming, the
    /// direction sent to the server, and the projectile spawned there.
    ///
    /// Subclasses supply what to throw and how hard.
    /// </summary>
    public abstract class AimedItem : Item
    {
        /// <summary>Runs on the server once the direction is known.</summary>
        protected abstract void PerformAimed(Vector3 dir);

        protected virtual float AiRange { get { return 14f; } }

        /// <summary>
        /// Items that sweep a wedge show the wedge itself. Zero means "not a sector item" -
        /// those keep the arrow, which is the right picture for something thrown.
        /// </summary>
        protected virtual float IndicatorReach { get { return 0f; } }
        protected virtual float IndicatorHalfAngle { get { return 0f; } }

        /// <summary>Non-zero for items that reach along a lane rather than sweeping an arc.</summary>
        protected virtual float IndicatorHalfWidth { get { return 0f; } }

        /// <summary>
        /// Non-null for items that fly a ballistic arc worth previewing. Where a lobbed item
        /// comes down is the question the player actually has, and an arrow cannot answer it.
        /// </summary>
        protected virtual TrajectoryPreview Trajectory { get { return null; } }

        private Vector3 m_aim = Vector3.forward;
        private AimArrow m_arrow;
        private SectorIndicator m_sector;
        private BeamIndicator m_beam;
        private TrajectoryArc m_arc;

        public override void Setup()
        {
            base.Setup();
            player.BoardObject.PlayerAnimation.Carrying = true;
            m_aim = player.BoardObject.transform.forward;

            if (player.IsLocalPlayer && !player.IsAI)
            {
                Color tint = new Color(1f, 0.85f, 0.2f);
                TrajectoryPreview arc = Trajectory;

                if (arc != null)
                    m_arc = TrajectoryArc.Create(tint, arc);
                else if (IndicatorReach > 0f && IndicatorHalfWidth > 0f)
                    m_beam = BeamIndicator.Create(tint, IndicatorReach, IndicatorHalfWidth);
                else if (IndicatorReach > 0f)
                    m_sector = SectorIndicator.Create(tint, IndicatorReach, IndicatorHalfAngle);
                else
                    m_arrow = AimArrow.Create(tint);
            }

            SetNetworkState(ItemState.Setup);
        }

        public override void Unequip(bool endingTurn)
        {
            player.BoardObject.PlayerAnimation.Carrying = false;
            DismissIndicator();
            base.Unequip(endingTurn);
        }

        public override void Update()
        {
            base.Update();

            if (CurState == ItemState.Aiming && player != null && player.IsLocalPlayer && !player.IsAI)
            {
                // DirectionalAim covers stick and mouse both, and returns zero when there
                // is no input - so the last good direction is kept rather than snapping back.
                Vector3 a = DirectionalAim(1f);
                a.y = 0f;
                if (a.sqrMagnitude > 0.04f) m_aim = a.normalized;

                Vector3 from = player.BoardObject.transform.position;
                if (m_arrow != null) m_arrow.Aim(from, m_aim);
                if (m_sector != null) m_sector.Aim(from, m_aim);
                if (m_beam != null) m_beam.Aim(from, m_aim);
                if (m_arc != null) m_arc.Aim(from, m_aim);
            }
            else
            {
                DismissIndicator();
            }
        }

        private void DismissIndicator()
        {
            if (m_arrow != null) { m_arrow.Dismiss(); m_arrow = null; }
            if (m_sector != null) { m_sector.Dismiss(); m_sector = null; }
            if (m_beam != null) { m_beam.Dismiss(); m_beam = null; }
            if (m_arc != null) { m_arc.Dismiss(); m_arc = null; }
        }

        protected override void Use(int seed)
        {
            base.Use(seed);
            if (!base.IsOwner) return;

            if (player.IsAI) m_aim = AimAtNearestOpponent();

            // The owner knows the aim; the server does the spawning. Locally that is the
            // same machine, but keeping the split means this still holds over the network.
            SendRPC("RPCThrow", NetRPCDelivery.RELIABLE_ORDERED, m_aim.x, m_aim.z);
            DoThrow(m_aim);
        }

        [NetRPC(true, NetRPCSecurity.OWNER, NetRPCSecurity.ALL)]
        public void RPCThrow(NetPlayer sender, float dirX, float dirZ)
        {
            DoThrow(new Vector3(dirX, 0f, dirZ));
        }

        private void DoThrow(Vector3 dir)
        {
            if (!NetSystem.IsServer) return;

            if (dir.sqrMagnitude < 0.01f) dir = player.BoardObject.transform.forward;

            try { PerformAimed(dir.normalized); }
            catch (System.Exception e) { Core.Warn(GetType().Name + " failed: " + e); }

            Finish(relay: false);
        }
        /// <summary>
        /// The opponent best lined up with the aim: inside the wedge, nearest first.
        /// Null when nobody qualifies, which the caller should report rather than silently
        /// waste the item.
        /// </summary>
        protected BoardPlayer PickTarget(Vector3 dir, float reach, float halfAngle)
        {
            BoardPlayer me = player.BoardObject;
            BoardPlayer best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == me) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                if (IsActorATeammateThatShouldBeIgnored(gp.BoardObject, player)) continue;

                Vector3 delta = gp.BoardObject.transform.position - me.transform.position;
                delta.y = 0f;

                float dist = delta.magnitude;
                if (dist > reach) continue;
                if (Vector3.Angle(dir, delta) > halfAngle) continue;

                if (dist < bestDist) { bestDist = dist; best = gp.BoardObject; }
            }
            return best;
        }

        /// <summary>
        /// The nearest opponent standing in a lane of the given width ahead of the player.
        /// Distance along the aim decides who is first; distance to the side decides whether
        /// they are in it at all.
        /// </summary>
        protected BoardPlayer PickAlongLine(Vector3 dir, float reach, float halfWidth)
        {
            BoardPlayer me = player.BoardObject;
            BoardPlayer best = null;
            float bestAlong = float.MaxValue;

            dir = dir.normalized;

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == me) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                if (IsActorATeammateThatShouldBeIgnored(gp.BoardObject, player)) continue;

                Vector3 delta = gp.BoardObject.transform.position - me.transform.position;
                delta.y = 0f;

                float along = Vector3.Dot(delta, dir);
                if (along <= 0f || along > reach) continue;               // behind, or too far
                if ((delta - dir * along).magnitude > halfWidth) continue; // off to the side

                if (along < bestAlong) { bestAlong = along; best = gp.BoardObject; }
            }
            return best;
        }

        protected Vector3 AimAtNearestOpponent()
        {
            BoardPlayer me = player.BoardObject;
            Vector3 best = me.transform.forward;
            float bestDist = float.MaxValue;

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == me) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                if (IsActorATeammateThatShouldBeIgnored(gp.BoardObject, player)) continue;

                Vector3 delta = gp.BoardObject.transform.position - me.transform.position;
                float d = delta.sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = delta; }
            }

            best.y = 0f;
            return best.sqrMagnitude > 0.01f ? best.normalized : me.transform.forward;
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == user) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                if (IsActorATeammateThatShouldBeIgnored(gp.BoardObject, user.GamePlayer)) continue;

                float dist = Vector3.Distance(gp.BoardObject.transform.position, user.transform.position);
                if (dist < AiRange) return new ItemAIUse(gp.BoardObject, Mathf.Clamp01(1f - dist / AiRange));
            }
            return null;
        }
    }

    /// <summary>An aimed item whose payload is a projectile (or several).</summary>
    public abstract class ThrownItemBase : AimedItem
    {
        protected abstract string ProjectilePrefabName { get; }
        protected abstract ProjectileKind Kind { get; }

        protected virtual float ThrowSpeed { get { return 11f; } }
        protected virtual float ThrowLift { get { return 6.5f; } }
        protected virtual int ProjectileCount { get { return 1; } }
        protected virtual float SpreadDegrees { get { return 0f; } }

        /// <summary>
        /// Only worth drawing for a real lob. A flat, fast shot travels almost in a straight
        /// line, so the curve would say nothing the arrow does not - and a spread of pellets
        /// has no single path to draw at all.
        /// </summary>
        protected override TrajectoryPreview Trajectory
        {
            get
            {
                if (ThrowLift < 3f || ProjectileCount > 1) return null;
                return new BallisticPreview(ThrowSpeed, ThrowLift,
                                            ProjectileSpec.For(Kind).BlastRadius);
            }
        }

        protected override void PerformAimed(Vector3 dir)
        {
            int count = Mathf.Max(1, ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 shotDir = dir;
                if (count > 1 && SpreadDegrees > 0f)
                {
                    float t = ((float)i / (count - 1)) - 0.5f;
                    shotDir = Quaternion.Euler(0f, t * SpreadDegrees, 0f) * dir;
                }
                SpawnOne(shotDir);
            }
            Core.Log(GetType().Name + ": thrown by player " + player.GlobalID);
        }

        /// <summary>Overridable: the boomerang spawns a different component entirely.</summary>
        protected virtual void SpawnOne(Vector3 dir)
        {
            Vector3 from = player.BoardObject.transform.position + Vector3.up * 1.4f + dir * 0.6f;

            GameObject go = NetSystem.Spawn(ProjectilePrefabName, from, Quaternion.identity,
                                            base.OwnerSlot, player.NetOwner);
            if (go == null)
            {
                Core.Warn(GetType().Name + ": spawn failed for " + ProjectilePrefabName);
                return;
            }

            ThrownProjectile p = go.GetComponent<ThrownProjectile>();
            if (p != null) p.Launch(dir * ThrowSpeed + Vector3.up * ThrowLift, Kind);
        }
    }

    public class BananaBombItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Banana; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Banana; } }
    }

    public class FragGrenadeItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Grenade; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Frag; } }
        protected override float ThrowSpeed { get { return 10f; } }
        protected override float ThrowLift { get { return 5.5f; } }
    }

    public class StickyBombItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Sticky; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Sticky; } }
        protected override float ThrowSpeed { get { return 13f; } }
        protected override float ThrowLift { get { return 4.5f; } }
    }

    public class IcicleItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Icicle; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Icicle; } }
        // Flat and fast: this one is aimed straight at somebody, not lobbed.
        protected override float ThrowSpeed { get { return 20f; } }
        protected override float ThrowLift { get { return 1.6f; } }
        protected override float AiRange { get { return 18f; } }
    }

    /// <summary>A spray of bouncing pellets rather than one projectile.</summary>
    public class RicochetItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Pellet; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Pellet; } }
        protected override float ThrowSpeed { get { return 22f; } }
        protected override float ThrowLift { get { return 1.2f; } }
        protected override int ProjectileCount { get { return 6; } }
        protected override float SpreadDegrees { get { return 34f; } }
        protected override float AiRange { get { return 16f; } }
    }
}
