using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    /// <summary>
    /// Flies out along a curve and comes back, hurting whoever it passes on either leg.
    ///
    /// Physics would not give a return arc, so the path is driven by hand: the boomerang is
    /// kinematic and follows a curve computed from the throw. Everything else - who owns it,
    /// how damage is broadcast - follows the same pattern as ThrownProjectile.
    /// </summary>
    public class Boomerang : NetBehaviour
    {
        [NetSend(-1, NetSendOwner.OWNER, NetSendFlags.ALWAYS_SEND)]
        public NetVec3 netPosition = new NetVec3(Vector3.zero);

        [NetSend(-1, NetSendOwner.OWNER, NetSendFlags.ALWAYS_SEND)]
        public NetVec3 netRotation = new NetVec3(Vector3.zero);

        private const int ActorLayerMask = 256;
        private const float FlightTime = 2.4f;
        private const float Reach = 9f;
        private const float SideSwing = 3.5f;

        /// <summary>How high above the thrower it leaves. Shared with the aiming preview.</summary>
        internal const float LaunchHeight = 1.3f;
        private const float HitRadius = 1.5f;
        private const int DamageMin = 7;
        private const int DamageMax = 11;
        private const float SpinSpeed = 1400f;

        private GamePlayer m_thrower;
        private Vector3 m_origin;
        private Vector3 m_forward;
        private Vector3 m_right;
        private float m_t;
        private bool m_armed;
        private bool m_dead;

        // Each actor can be clipped once per leg, so a slow pass does not shred anybody.
        private readonly HashSet<byte> m_hitOutbound = new HashSet<byte>();
        private readonly HashSet<byte> m_hitInbound = new HashSet<byte>();
        private readonly Collider[] m_hits = new Collider[32];

        public override void OnNetInitialize()
        {
            m_thrower = GameManager.GetPlayerWithID((short)base.OwnerSlot);
            base.OnNetInitialize();
        }

        internal void Launch(Vector3 origin, Vector3 direction)
        {
            m_origin = origin;
            m_forward = direction.normalized;
            m_right = Vector3.Cross(Vector3.up, m_forward).normalized;
            m_t = 0f;
            m_armed = true;
            base.transform.position = origin;
            ModAssets.Play("snd_whirl", 0.7f);
        }

        private void Update()
        {
            if (m_dead) return;

            if (!base.IsOwner)
            {
                base.transform.position = netPosition.Value;
                base.transform.eulerAngles = netRotation.Value;
                return;
            }

            if (!m_armed) return;

            m_t += Time.deltaTime / FlightTime;

            if (m_t >= 1f)
            {
                Finish();
                return;
            }

            base.transform.position = PathAt(m_t);
            base.transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);

            netPosition.Value = base.transform.position;
            netRotation.Value = base.transform.eulerAngles;

            ScanForHits();
        }

        /// <summary>
        /// Out and back along the throw direction, with a sideways bulge so the return leg
        /// sweeps a different line than the outbound one.
        /// </summary>
        private Vector3 PathAt(float t)
        {
            return PathAt(m_origin, m_forward, m_right, t);
        }

        /// <summary>
        /// Pure function of the throw, so the aiming preview can draw the same curve the
        /// boomerang will fly instead of guessing at a parabola it never follows.
        /// </summary>
        internal static Vector3 PathAt(Vector3 origin, Vector3 forward, Vector3 right, float t)
        {
            float along = Mathf.Sin(t * Mathf.PI) * Reach;          // 0 -> Reach -> 0
            float across = Mathf.Sin(t * Mathf.PI * 2f) * SideSwing; // one full swing sideways
            float lift = Mathf.Sin(t * Mathf.PI) * 0.6f;

            return origin + forward * along + right * across + Vector3.up * lift;
        }

        private void ScanForHits()
        {
            HashSet<byte> alreadyHit = (m_t < 0.5f) ? m_hitOutbound : m_hitInbound;

            int n = Physics.OverlapSphereNonAlloc(base.transform.position, HitRadius, m_hits,
                                                  ActorLayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                BoardActor actor = m_hits[i].gameObject.GetComponentInParent<BoardActor>();
                if (actor == null || actor.LocalHealth <= 0) continue;
                if (IsThrower(actor)) continue;   // it starts and ends inside its owner
                if (!alreadyHit.Add(actor.ActorID)) continue;
                if (IsProtectedTeammate(actor)) continue;

                byte dmg = (byte)UnityEngine.Random.Range(DamageMin, DamageMax + 1);
                SendRPC("HitRPC", NetRPCDelivery.RELIABLE_ORDERED, actor.ActorID, dmg);
                ApplyHit(actor.ActorID, dmg);
            }
        }

        [NetRPC(true, NetRPCSecurity.OWNER, NetRPCSecurity.ALL)]
        public void HitRPC(NetPlayer sender, byte actorID, byte damage)
        {
            ApplyHit(actorID, damage);
        }

        private void ApplyHit(byte actorID, byte damage)
        {
            BoardActor actor = GameManager.Board.GetActor(actorID);
            if (actor == null) return;

            DamageInstance d = new DamageInstance
            {
                damage = damage,
                origin = base.transform.position,
                blood = true,
                ragdoll = true,
                ragdollVel = 10f,
                bloodVel = 14f,
                bloodAmount = 0.8f,
                details = "Boomerang",
                killer = (m_thrower != null) ? m_thrower.BoardObject : null,
                removeKeys = true,
            };
            actor.ApplyDamage(d);
            ModAssets.Play("snd_whack", 0.8f);
            Core.Log("Boomerang: hit actor " + actorID + " for " + damage);
        }

        /// <summary>The boomerang launches from - and returns into - the thrower, so it can
        /// never be allowed to hit them; otherwise it clips its owner on the first frame.</summary>
        private bool IsThrower(BoardActor actor)
        {
            if (m_thrower == null || m_thrower.BoardObject == null) return false;
            return actor == (BoardActor)m_thrower.BoardObject;
        }

        private bool IsProtectedTeammate(BoardActor actor)
        {
            try
            {
                if (GameManager.PlayingSoloMode) return false;
                if (GameManager.IsBoardItemTeamFriendlyFireEnabled) return false;
                if (m_thrower == null) return false;

                BoardPlayer bp = actor as BoardPlayer;
                if (bp == null || bp.GamePlayer == null || bp.GamePlayer.GameTeam == null) return false;
                if (bp.GamePlayer == m_thrower) return false;

                return bp.GamePlayer.GameTeam.IsInTeam(m_thrower);
            }
            catch
            {
                return false;
            }
        }

        private void Finish()
        {
            if (m_dead) return;
            m_dead = true;
            Core.Log("Boomerang: returned");

            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) rs[i].enabled = false;

            if (NetSystem.IsServer) StartCoroutine(KillLater());
        }

        private IEnumerator KillLater()
        {
            yield return new WaitForSeconds(1f);
            if (NetSystem.IsServer) NetSystem.Kill(this);
        }
    }

    public class BoomerangItem : ThrownItemBase
    {
        protected override string ProjectilePrefabName { get { return Prefabs.Boomerang; } }
        protected override ProjectileKind Kind { get { return ProjectileKind.Banana; } }  // unused
        protected override float AiRange { get { return 12f; } }

        /// <summary>It flies its own curve, not a thrown arc - so that is what gets drawn.</summary>
        protected override TrajectoryPreview Trajectory { get { return new BoomerangPreview(); } }

        /// <summary>Flies a scripted path, so it needs its own component rather than the
        /// generic projectile.</summary>
        protected override void SpawnOne(Vector3 dir)
        {
            Vector3 from = player.BoardObject.transform.position + Vector3.up * Boomerang.LaunchHeight;

            GameObject go = NetSystem.Spawn(Prefabs.Boomerang, from, Quaternion.identity,
                                            base.OwnerSlot, player.NetOwner);
            if (go == null) { Core.Warn("Boomerang: spawn failed"); return; }

            Boomerang b = go.GetComponent<Boomerang>();
            if (b != null) b.Launch(from, dir);
        }
    }
}
