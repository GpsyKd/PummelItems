using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    public enum ProjectileKind
    {
        Banana,          // arcs, bounces, blows up on a fuse, scatters fragments
        BananaPiece,     // one of those fragments
        Frag,            // heavier blast, barely bounces, scatters a lot more
        FragPiece,
        Sticky,          // clings to the first player it touches, then detonates
        Icicle,          // bursts on contact, freezes whoever it hits
        Pellet,          // fast ricocheting shot, hurts on contact
    }

    /// <summary>Everything that separates one projectile from another.</summary>
    internal sealed class ProjectileSpec
    {
        /// <summary>
        /// Linear drag every thrown body gets. Lives here because the aiming preview has to
        /// integrate the same value the rigidbody uses - two copies would silently drift and
        /// the drawn arc would stop matching the throw.
        /// </summary>
        internal const float LinearDrag = 0.05f;

        public float Fuse = 2.5f;
        public float BlastRadius = 4f;
        public int DamageMin = 10;
        public int DamageMax = 16;

        public bool ExplodeOnActorHit;      // burst the moment it touches a player
        public bool StickToActor;           // ride along until the fuse runs out
        public bool FreezeTarget;           // the victim's next roll becomes zero
        public float Scale = 1f;

        // Trigger distance checked every frame. Player hit volumes appear to be triggers,
        // so OnCollisionEnter never fires against them - relying on contact alone made the
        // pellets and the icicle fly straight through people. 0 disables it.
        public float ProximityRadius;

        public string ExplodeSound = "snd_explode";
        public string BounceSound = "snd_bounce";

        public ProjectileKind FragmentKind = ProjectileKind.Banana;
        public int FragmentCount;
        public float FragmentSpread = 6f;
        public string FragmentPrefab;

        public int MaxBounces = -1;         // -1 = unlimited; otherwise dies after N

        internal static ProjectileSpec For(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Banana:
                    return new ProjectileSpec
                    {
                        Fuse = 2.6f, BlastRadius = 4.5f, DamageMin = 12, DamageMax = 19,
                        FragmentKind = ProjectileKind.BananaPiece, FragmentCount = 5,
                        FragmentPrefab = Prefabs.Banana,
                        ExplodeSound = "snd_explode", BounceSound = "snd_bounce",
                    };

                case ProjectileKind.BananaPiece:
                    return new ProjectileSpec
                    {
                        Fuse = 1.15f, BlastRadius = 2.6f, DamageMin = 4, DamageMax = 8,
                        Scale = 0.55f,
                    };

                case ProjectileKind.Frag:
                    return new ProjectileSpec
                    {
                        // Lands heavy and goes off fast - less of a trick shot than the banana.
                        Fuse = 1.9f, BlastRadius = 5.2f, DamageMin = 8, DamageMax = 13,
                        FragmentKind = ProjectileKind.FragPiece, FragmentCount = 9,
                        FragmentSpread = 8.5f, FragmentPrefab = Prefabs.Shard,
                        ExplodeSound = "snd_blast", BounceSound = "snd_clink",
                    };

                case ProjectileKind.FragPiece:
                    return new ProjectileSpec
                    {
                        Fuse = 0.9f, BlastRadius = 1.9f, DamageMin = 2, DamageMax = 3,
                        Scale = 0.8f, ExplodeSound = "snd_blast", BounceSound = "snd_clink",
                    };

                case ProjectileKind.Sticky:
                    return new ProjectileSpec
                    {
                        Fuse = 3.4f, BlastRadius = 3.8f, DamageMin = 15, DamageMax = 23,
                        StickToActor = true, ProximityRadius = 0.9f,
                        ExplodeSound = "snd_blast", BounceSound = "snd_squelch",
                    };

                case ProjectileKind.Icicle:
                    return new ProjectileSpec
                    {
                        Fuse = 6f, BlastRadius = 1.8f, DamageMin = 5, DamageMax = 9,
                        ExplodeOnActorHit = true, FreezeTarget = true, MaxBounces = 2,
                        ProximityRadius = 1.1f,
                        ExplodeSound = "snd_shatter", BounceSound = "snd_clink",
                    };

                case ProjectileKind.Pellet:
                    return new ProjectileSpec
                    {
                        Fuse = 2.2f, BlastRadius = 1.4f, DamageMin = 3, DamageMax = 5,
                        ExplodeOnActorHit = true, MaxBounces = 3, Scale = 1f,
                        ProximityRadius = 0.85f,
                        ExplodeSound = "snd_ping", BounceSound = "snd_ping",
                    };
            }
            return new ProjectileSpec();
        }
    }

    internal static class Prefabs
    {
        internal const string Banana = "PCI_P_Banana";
        internal const string Grenade = "PCI_P_Grenade";
        internal const string Shard = "PCI_P_Shard";
        internal const string Boomerang = "PCI_P_Boomerang";
        internal const string Sticky = "PCI_P_Sticky";
        internal const string Icicle = "PCI_P_Icicle";
        internal const string Pellet = "PCI_P_Pellet";
    }

    /// <summary>
    /// One projectile class covers every thrown item - only the spec and the model differ.
    ///
    /// Built to the pattern the game uses for its own physics objects (see Eggplant): the
    /// owner simulates locally and streams the transform, and the blast is resolved once by
    /// the owner then broadcast as a finished list of victims, so nobody disagrees about
    /// who got hit.
    /// </summary>
    public class ThrownProjectile : NetBehaviour
    {
        [NetSend(-1, NetSendOwner.OWNER, NetSendFlags.ALWAYS_SEND)]
        public NetVec3 netPosition = new NetVec3(Vector3.zero);

        [NetSend(-1, NetSendOwner.OWNER, NetSendFlags.ALWAYS_SEND)]
        public NetVec3 netRotation = new NetVec3(Vector3.zero);

        // Layer 8 carries the players' hit volumes - the mask the game's own blasts use.
        private const int ActorLayerMask = 256;

        private Rigidbody m_rb;
        private GamePlayer m_thrower;
        private ProjectileSpec m_spec = ProjectileSpec.For(ProjectileKind.Banana);
        private ProjectileKind m_kind = ProjectileKind.Banana;

        private float m_fuse;
        private bool m_dead;
        private bool m_armed;               // Launch() has run; before that it just sits
        private float m_bounceCooldown;
        private int m_bounces;
        private Transform m_stuckTo;
        private Vector3 m_stickOffset;
        private float m_age;


        private readonly Collider[] m_hits = new Collider[32];

        public override void OnNetInitialize()
        {
            m_rb = GetComponent<Rigidbody>();
            m_thrower = GameManager.GetPlayerWithID((short)base.OwnerSlot);
            base.OnNetInitialize();
        }

        internal void Launch(Vector3 velocity, ProjectileKind kind)
        {
            m_kind = kind;
            m_spec = ProjectileSpec.For(kind);
            m_fuse = m_spec.Fuse;
            m_armed = true;

            if (m_spec.Scale != 1f)
                base.transform.localScale = base.transform.localScale * m_spec.Scale;

            if (m_rb == null) m_rb = GetComponent<Rigidbody>();
            if (m_rb == null) return;

            m_rb.velocity = velocity;
            m_rb.angularVelocity = new Vector3(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-4f, 4f),
                UnityEngine.Random.Range(-8f, 8f));
        }

        private void Update()
        {
            if (m_dead) return;

            if (m_bounceCooldown > 0f) m_bounceCooldown -= Time.deltaTime;

            if (m_stuckTo != null)
                base.transform.position = m_stuckTo.position + m_stickOffset;

            if (base.IsOwner)
            {
                if (m_armed)
                {
                    m_age += Time.deltaTime;
                    m_fuse -= Time.deltaTime;

                    if (m_spec.ProximityRadius > 0f && CheckProximity()) return;
                    if (m_fuse <= 0f) OwnerExplode("fuse ran out");
                }
                netPosition.Value = base.transform.position;
                netRotation.Value = base.transform.eulerAngles;
            }
            else
            {
                base.transform.position = netPosition.Value;
                base.transform.eulerAngles = netRotation.Value;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_dead || !m_armed) return;

            BoardActor actor = collision.collider.GetComponentInParent<BoardActor>();

            if (actor != null && m_spec.StickToActor && m_stuckTo == null)
            {
                Stick(actor);
                return;
            }

            if (actor != null && m_spec.ExplodeOnActorHit && base.IsOwner)
            {
                OwnerExplode("hit an actor");
                return;
            }

            if (m_bounceCooldown <= 0f)
            {
                m_bounceCooldown = 0.08f;   // one tick per landing, not per contact point
                m_bounces++;
                ModAssets.Play(m_spec.BounceSound, 0.4f);

                if (m_spec.MaxBounces >= 0 && m_bounces > m_spec.MaxBounces && base.IsOwner)
                    OwnerExplode("out of bounces");
            }
        }

        /// <summary>
        /// Looks for somebody close enough to trigger on. Player hit volumes are triggers,
        /// so physical contact is never reported - this is what actually makes the pellets
        /// and the icicle land.
        /// </summary>
        private bool CheckProximity()
        {
            int n = Physics.OverlapSphereNonAlloc(
                base.transform.position, m_spec.ProximityRadius, m_hits,
                ActorLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < n; i++)
            {
                BoardActor actor = m_hits[i].gameObject.GetComponentInParent<BoardActor>();
                if (actor == null || actor.LocalHealth <= 0) continue;
                if (IsThrower(actor)) continue;
                if (IsProtectedTeammate(actor)) continue;

                if (m_spec.StickToActor && m_stuckTo == null)
                {
                    Stick(actor);
                    return false;   // it rides along; the fuse still decides when it goes off
                }

                OwnerExplode("proximity");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Your own throw never hurts you.
        ///
        /// This used to be a 0.35 second grace window, which only ever solved half the
        /// problem: it stopped the projectile detonating inside the thrower the instant it
        /// left their hand, but a grenade that bounced back was free to hurt them a second
        /// later, and fragments - same owner, but each starting its own clock - sailed past
        /// the window entirely. Whose throw it was does not change while it is in the air,
        /// so neither should this.
        /// </summary>
        private bool IsThrower(BoardActor actor)
        {
            if (m_thrower == null || m_thrower.BoardObject == null) return false;
            return actor == (BoardActor)m_thrower.BoardObject;
        }

        private void Stick(BoardActor actor)
        {
            m_stuckTo = actor.transform;
            m_stickOffset = base.transform.position - actor.transform.position;
            if (m_rb != null)
            {
                m_rb.velocity = Vector3.zero;
                m_rb.angularVelocity = Vector3.zero;
                m_rb.isKinematic = true;
            }
            ModAssets.Play("snd_squelch", 0.8f);
            Core.Log("Projectile: stuck to actor " + actor.ActorID);
        }

        // ------------------------------------------------------------------- blast

        /// <summary>
        /// <paramref name="reason"/> and the armed time are logged together: a frag grenade
        /// went off at roughly half its fuse and nothing in the spec could account for it,
        /// so the next run should say plainly which trigger fired and what the clock read.
        /// </summary>
        private void OwnerExplode(string reason)
        {
            if (m_dead) return;

            Core.Log("Projectile(" + m_kind + "): " + reason + " after " + m_age.ToString("F2") +
                     "s armed (fuse " + m_spec.Fuse.ToString("F2") + "s, bounces " + m_bounces + ")");

            List<byte> victims = new List<byte>();
            List<byte> damage = new List<byte>();

            try
            {
                int n = Physics.OverlapSphereNonAlloc(
                    base.transform.position, m_spec.BlastRadius, m_hits,
                    ActorLayerMask, QueryTriggerInteraction.Collide);

                HashSet<byte> seen = new HashSet<byte>();
                for (int i = 0; i < n; i++)
                {
                    BoardActor actor = m_hits[i].gameObject.GetComponentInParent<BoardActor>();
                    if (actor == null || actor.LocalHealth <= 0) continue;
                    if (!seen.Add(actor.ActorID)) continue;
                    if (IsThrower(actor)) continue;
                    if (IsProtectedTeammate(actor)) continue;

                    victims.Add(actor.ActorID);
                    damage.Add((byte)UnityEngine.Random.Range(m_spec.DamageMin, m_spec.DamageMax + 1));
                }
            }
            catch (Exception e)
            {
                Core.Warn("blast scan failed: " + e);
            }

            if (base.IsOwner)
                SendRPC("ExplodeRPC", NetRPCDelivery.RELIABLE_ORDERED,
                        victims.ToArray(), damage.ToArray());

            Explode(victims.ToArray(), damage.ToArray());
        }

        [NetRPC(true, NetRPCSecurity.OWNER, NetRPCSecurity.ALL)]
        public void ExplodeRPC(NetPlayer sender, byte[] hitPlayers, byte[] hitDamage)
        {
            Explode(hitPlayers, hitDamage);
        }

        private void Explode(byte[] hitPlayers, byte[] hitDamage)
        {
            if (m_dead) return;
            m_dead = true;

            Core.Log("Projectile(" + m_kind + "): exploded, " + hitPlayers.Length + " hit");

            for (int i = 0; i < hitPlayers.Length; i++)
            {
                BoardActor actor = GameManager.Board.GetActor(hitPlayers[i]);
                if (actor == null) continue;

                DamageInstance d = new DamageInstance
                {
                    damage = hitDamage[i],
                    origin = base.transform.position,
                    blood = true,
                    ragdoll = true,
                    ragdollVel = 14f,
                    bloodVel = 18f,
                    bloodAmount = 1f,
                    details = m_kind.ToString(),
                    killer = (m_thrower != null) ? m_thrower.BoardObject : null,
                    removeKeys = true,
                };
                actor.ApplyDamage(d);

                if (m_spec.FreezeTarget) Freeze(actor);
            }

            if (m_spec.FragmentCount > 0 && NetSystem.IsServer) SpawnFragments();

            ModAssets.Play(m_spec.ExplodeSound, m_spec.BlastRadius > 3f ? 0.8f : 0.45f);
            Effects.Blast(base.transform.position, m_spec.BlastRadius);

            if (m_rb != null) m_rb.isKinematic = true;
            SetVisible(false);

            if (m_spec.BlastRadius > 3f)
            {
                try { GameManager.Board.boardCamera.AddShake(0.55f); } catch { }
            }

            if (NetSystem.IsServer) StartCoroutine(KillLater());
        }

        /// <summary>
        /// "Skips a turn" by forcing the victim's next roll to zero - they still take their
        /// turn, they just do not go anywhere.
        /// </summary>
        private void Freeze(BoardActor actor)
        {
            BoardPlayer bp = actor as BoardPlayer;
            if (bp == null || bp.GamePlayer == null) return;

            DiceOverride.ArmImmediate(bp.GamePlayer.GlobalID, 0, "Заморожен!");
            DiceOverride.Announce(bp, "Заморожен!");
            Core.Log("Projectile: froze player " + bp.GamePlayer.GlobalID);
        }

        private void SpawnFragments()
        {
            string prefab = m_spec.FragmentPrefab;
            if (string.IsNullOrEmpty(prefab)) return;

            Vector3 origin = base.transform.position + Vector3.up * 0.35f;

            for (int i = 0; i < m_spec.FragmentCount; i++)
            {
                try
                {
                    float angle = (360f / m_spec.FragmentCount) * i + UnityEngine.Random.Range(-18f, 18f);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                    GameObject go = NetSystem.Spawn(prefab, origin + dir * 0.35f,
                                                    Quaternion.identity, base.OwnerSlot, base.Owner);
                    if (go == null) continue;

                    ThrownProjectile frag = go.GetComponent<ThrownProjectile>();
                    if (frag == null) continue;

                    Vector3 vel = dir * UnityEngine.Random.Range(m_spec.FragmentSpread * 0.6f, m_spec.FragmentSpread)
                                + Vector3.up * UnityEngine.Random.Range(5f, 7f);
                    frag.Launch(vel, m_spec.FragmentKind);
                }
                catch (Exception e)
                {
                    Core.Warn("fragment spawn failed: " + e.Message);
                }
            }

            Core.Log("Projectile: spawned " + m_spec.FragmentCount + " fragments");
        }

        /// <summary>Mirrors the game's rule: no hurting teammates unless it is allowed.</summary>
        private bool IsProtectedTeammate(BoardActor actor)
        {
            try
            {
                if (GameManager.PlayingSoloMode) return false;
                if (GameManager.IsBoardItemTeamFriendlyFireEnabled) return false;
                if (m_thrower == null) return false;

                BoardPlayer bp = actor as BoardPlayer;
                if (bp == null || bp.GamePlayer == null || bp.GamePlayer.GameTeam == null) return false;
                if (bp.GamePlayer == m_thrower) return false;   // hurting yourself stays allowed

                return bp.GamePlayer.GameTeam.IsInTeam(m_thrower);
            }
            catch
            {
                return false;
            }
        }

        private void SetVisible(bool visible)
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) rs[i].enabled = visible;
        }

        private IEnumerator KillLater()
        {
            yield return new WaitForSeconds(2f);
            if (NetSystem.IsServer) NetSystem.Kill(this);
        }
    }
}
