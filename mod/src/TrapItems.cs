using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    // ------------------------------------------------------------------------ mine

    /// <summary>
    /// A mine sitting on a board node until somebody walks onto it.
    ///
    /// It watches where players physically are, not which node they are "on". The game's
    /// BoardPlayer.StartMove plans a whole stretch of the route at once and sets CurrentNode
    /// straight to the END of it before the walk even starts, so the nodes in between are
    /// never anybody's CurrentNode: a node check let players walk through the mine, and when
    /// a stretch did end on it, the mine went off while its victim was still several nodes
    /// away - outside the blast. A distance check catches walking, jumping, teleports and
    /// being kicked onto it, and fires with the victim right there.
    /// </summary>
    public class LandMine : NetBehaviour
    {
        internal const float BlastRadius = 3.6f;
        internal const int DamageMin = 10;
        internal const int DamageMax = 16;

        private const int ActorLayerMask = 256;

        private GamePlayer m_owner;
        private int m_nodeID = -1;
        private bool m_dead;

        // Half the distance to the nearest neighbouring node: close enough to count as "on
        // this node" wherever a player stands in its slots, never reaching the next node.
        private float m_triggerRadius = 1f;

        // Players already standing on the node when it was placed. They have to step off and
        // come back - a mine laid under somebody's feet going off at once is not a trap.
        private readonly HashSet<short> m_waitForLeave = new HashSet<short>();

        private readonly Collider[] m_hits = new Collider[32];

        public override void OnNetInitialize()
        {
            m_owner = GameManager.GetPlayerWithID((short)base.OwnerSlot);
            PaintForOwner();
            base.OnNetInitialize();
        }

        /// <summary>
        /// Tints the pressure plate with the owner's colour. Whose mine it is decides whether
        /// stepping on it hurts, so that has to be readable on the board rather than
        /// remembered - the game colours dropped keys the same way.
        ///
        /// Runs on every peer, not just the owner: OnNetInitialize fires everywhere.
        /// </summary>
        private void PaintForOwner()
        {
            try
            {
                if (m_owner == null) return;

                Transform plate = transform.Find("Plate");
                if (plate == null) return;

                Renderer r = plate.GetComponent<Renderer>();
                if (r == null) return;

                Color c = m_owner.Color.skinColor1;

                // .material clones the shared asset, so tinting one mine leaves the rest alone
                // - and the clone is this mine's to clean up.
                Material m = OwnedAssets.Own(gameObject, r.material);
                m.color = c;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.55f);
            }
            catch (Exception e)
            {
                Core.Warn("mine colouring failed: " + e.Message);
            }
        }

        internal void Arm(BoardNode node)
        {
            if (node == null) return;
            m_nodeID = node.NodeID;
            transform.position = node.transform.position + Vector3.up * 0.22f;
            m_triggerRadius = TriggerRadiusFor(node);

            m_waitForLeave.Clear();
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || OwnedBy(gp.BoardObject)) continue;
                if (IsOnMine(gp.BoardObject)) m_waitForLeave.Add(gp.GlobalID);
            }

            Core.Log("Mine: armed on node " + m_nodeID + ", trigger radius " +
                     m_triggerRadius.ToString("F2") +
                     (m_waitForLeave.Count > 0 ? ", " + m_waitForLeave.Count + " player(s) must step off first" : ""));
        }

        private static float TriggerRadiusFor(BoardNode node)
        {
            float nearest = float.MaxValue;
            Vector3 here = node.transform.position;

            List<BoardNode> around = new List<BoardNode>();
            try
            {
                List<BoardNode> f = node.GetForwardNodes();
                List<BoardNode> b = node.GetBackNodes();
                if (f != null) around.AddRange(f);
                if (b != null) around.AddRange(b);
            }
            catch (Exception e)
            {
                Core.Warn("Mine: neighbour lookup failed: " + e.Message);
            }

            for (int i = 0; i < around.Count; i++)
            {
                if (around[i] == null || around[i] == node) continue;
                Vector3 d = around[i].transform.position - here;
                d.y = 0f;
                nearest = Mathf.Min(nearest, d.magnitude);
            }

            if (nearest == float.MaxValue) return 1f;
            return Mathf.Clamp(nearest * 0.5f, 0.8f, 2.5f);
        }

        private bool IsOnMine(BoardPlayer bp)
        {
            Vector3 d = bp.transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= m_triggerRadius * m_triggerRadius;
        }

        internal bool OwnedBy(BoardPlayer p)
        {
            return m_owner != null && p != null && m_owner.BoardObject == p;
        }

        internal void Trigger(BoardPlayer victim)
        {
            if (m_dead || !base.IsOwner) return;

            List<byte> victims = new List<byte>();
            List<byte> damage = new List<byte>();

            int n = Physics.OverlapSphereNonAlloc(transform.position, BlastRadius, m_hits,
                                                  ActorLayerMask, QueryTriggerInteraction.Collide);
            HashSet<byte> seen = new HashSet<byte>();
            for (int i = 0; i < n; i++)
            {
                BoardActor actor = m_hits[i].gameObject.GetComponentInParent<BoardActor>();
                if (actor == null || actor.LocalHealth <= 0) continue;
                if (!seen.Add(actor.ActorID)) continue;
                if (FriendlyFire.Spares(actor, m_owner)) continue;

                victims.Add(actor.ActorID);
                damage.Add((byte)UnityEngine.Random.Range(DamageMin, DamageMax + 1));
            }

            SendRPC("BlowRPC", NetRPCDelivery.RELIABLE_ORDERED, victims.ToArray(), damage.ToArray());
            Blow(victims.ToArray(), damage.ToArray());
        }

        [NetRPC(true, NetRPCSecurity.OWNER, NetRPCSecurity.ALL)]
        public void BlowRPC(NetPlayer sender, byte[] hitPlayers, byte[] hitDamage)
        {
            Blow(hitPlayers, hitDamage);
        }

        private void Blow(byte[] hitPlayers, byte[] hitDamage)
        {
            if (m_dead) return;
            m_dead = true;

            for (int i = 0; i < hitPlayers.Length; i++)
            {
                BoardActor actor = GameManager.Board.GetActor(hitPlayers[i]);
                if (actor == null) continue;

                actor.ApplyDamage(new DamageInstance
                {
                    damage = hitDamage[i],
                    origin = transform.position,
                    blood = true,
                    ragdoll = true,
                    ragdollVel = 15f,
                    bloodVel = 18f,
                    bloodAmount = 1f,
                    details = "Mine",
                    killer = (m_owner != null) ? m_owner.BoardObject : null,
                    removeKeys = true,
                });
            }

            ModAssets.Play("snd_blast", 0.85f);
            Effects.Blast(transform.position, BlastRadius);
            try { GameManager.Board.boardCamera.AddShake(0.5f); } catch { }

            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) rs[i].enabled = false;

            Core.Log("Mine: blew up, " + hitPlayers.Length + " hit");
            if (NetSystem.IsServer) StartCoroutine(KillLater());
        }

        private IEnumerator KillLater()
        {
            yield return new WaitForSeconds(2f);
            if (NetSystem.IsServer) NetSystem.Kill(this);
        }

        /// <summary>Server-side watch for anybody reaching the mined node.</summary>
        private void Update()
        {
            if (m_dead || m_nodeID < 0 || !NetSystem.IsServer) return;

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null) continue;

                BoardPlayer bp = gp.BoardObject;
                if (bp == null || bp.LocalHealth <= 0) continue;
                if (OwnedBy(bp)) continue;          // you do not step on your own mine

                if (!IsOnMine(bp))
                {
                    m_waitForLeave.Remove(gp.GlobalID);
                    continue;
                }
                if (m_waitForLeave.Contains(gp.GlobalID)) continue;

                Core.Log("Mine: " + gp.Name + " stepped on node " + m_nodeID);
                Trigger(bp);
                return;
            }
        }

        private void OnDestroy()
        {
        }
    }

    public class MineItem : InstantItem
    {
        protected override void Perform()
        {
            BoardNode node = player.BoardObject.CurrentNode;
            if (node == null) return;

            // Spawned by the host and OWNED by the host; the network sends it to everyone.
            // The placer is recorded in the owner slot, which is what decides whose mine it is
            // - the net owner only decides which machine runs it. It used to be owned by the
            // placer's machine, and only the host watched for players stepping on it; for a
            // mine placed from a client those were two different machines, so it never fired.
            if (Authority)
            {
                GameObject go = NetSystem.Spawn(TrapPrefabs.Mine,
                                                node.transform.position + Vector3.up * 0.22f,
                                                Quaternion.identity, base.OwnerSlot, NetSystem.MyPlayer);
                if (go == null) { Core.Warn("Mine: spawn failed"); return; }

                LandMine mine = go.GetComponent<LandMine>();
                if (mine != null) mine.Arm(node);
            }

            Say(player.BoardObject, "Мина установлена");
            ModAssets.Play("snd_clink", 0.9f);
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            return new ItemAIUse(user, 0.4f);
        }
    }

    // ---------------------------------------------------------------- poison a node

    /// <summary>
    /// Turns the node ahead into a hazard. CurrentNodeType has a public setter that swaps
    /// the node's own object for the new type's, so the board updates itself.
    /// </summary>
    public class PoisonNodeItem : InstantItem
    {
        protected override void Perform()
        {
            BoardPlayer me = player.BoardObject;
            BoardNode here = me.CurrentNode;
            if (here == null) return;

            List<BoardNode> ahead = here.GetForwardNodes();
            if (ahead == null || ahead.Count == 0)
            {
                Say(me, "Впереди некуда");
                Core.Log("PoisonNode: no node ahead");
                return;
            }

            BoardNode victimNode = ahead[rand.Next(0, ahead.Count)];
            if (victimNode.CurrentNodeType == BoardNodeType.Hazard)
            {
                Say(me, "Уже опасная");
                return;
            }

            victimNode.CurrentNodeType = BoardNodeType.Hazard;

            Say(me, "Клетка испорчена");
            ModAssets.Play("snd_squelch", 0.9f);
            Effects.Blast(victimNode.transform.position, 1.4f);
            Core.Log("PoisonNode: node " + victimNode.NodeID + " is now a hazard");
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            return new ItemAIUse(user, 0.35f);
        }
    }

    // ------------------------------------------------------------- fake signpost

    /// <summary>
    /// Until the user's next turn, every fork sends players somewhere other than where
    /// they picked. Applies to everyone, the user included.
    /// </summary>
    internal static class FakeSignpost
    {
        private static short s_ownerID = -1;
        private static bool s_active;

        // Survives the owner's first turn start - see TempModifiers for why.
        private static int s_turnsLeft;

        // Where the lies come from. Every machine plays the same forks in the same order, so
        // a seed drawn from the item's own generator plus a count of decisions so far gives
        // every machine the same answer at the same fork - while still being a different
        // answer at the next fork, and at this one on a later turn.
        private static int s_seed;
        private static int s_decisions;

        internal static bool Active { get { return s_active; } }

        internal static void Arm(short ownerID, int seed)
        {
            s_ownerID = ownerID;
            s_active = true;
            s_turnsLeft = 2;
            s_seed = seed;
            s_decisions = 0;
        }

        /// <summary>A new board starts honest, whatever the last one ended with.</summary>
        internal static void Reset()
        {
            s_active = false;
            s_ownerID = -1;
            s_turnsLeft = 0;
            s_decisions = 0;
        }

        /// <summary>
        /// Which branch the signpost sends this player down. The same on every machine.
        ///
        /// It used to come from UnityEngine.Random, which is separate on each machine. The
        /// direction choice is played out on all of them, so online every player would have
        /// been sent down a different road on every screen from the first fork on.
        /// </summary>
        internal static int Pick(BoardPlayer who, int choices)
        {
            unchecked
            {
                int h = s_seed;
                h = h * 31 + s_decisions++;
                h = h * 31 + who.GamePlayer.GlobalID;
                h = h * 31 + (who.CurrentNode != null ? who.CurrentNode.NodeID : -1);
                h = h * 31 + who.MoveStepsRemaining;
                return new System.Random(h).Next(0, choices);
            }
        }

        internal static void OnTurnStarted(short playerID, BoardPlayer who)
        {
            if (!s_active || s_ownerID != playerID) return;

            s_turnsLeft--;
            if (s_turnsLeft > 0) return;

            s_active = false;
            s_ownerID = -1;
            DiceOverride.Announce(who, "Указатели починены");
            Core.Log("FakeSignpost: expired");
        }
    }

    [HarmonyPatch(typeof(BoardPlayer), "ChooseDirection")]
    internal static class Patch_ChooseDirection
    {
        private static void Prefix(BoardPlayer __instance, ref int direction)
        {
            try
            {
                if (!FakeSignpost.Active) return;

                List<BoardNode> choices = __instance.NodeChoices;
                if (choices == null || choices.Count < 2) return;

                // Every branch is equally likely, the one that was picked included.
                //
                // It used to exclude the chosen branch on the grounds that a signpost which
                // sometimes tells the truth is just noise. That reasoning does not survive
                // contact with the board: forks here have two ways out, so "never the one you
                // picked" is "always the other one" - perfectly predictable, and a trap you
                // can plan around is not a trap. Being unable to trust it is the whole point.
                int picked = FakeSignpost.Pick(__instance, choices.Count);

                Core.Log("FakeSignpost: " + direction + " -> " + picked +
                         " (of " + choices.Count + ")");

                if (picked == direction) return;   // it happened to agree; say nothing
                direction = picked;

                DiceOverride.Announce(__instance, "Не туда!");
                ModAssets.Play("snd_ping", 0.7f);
            }
            catch (Exception e)
            {
                Core.Warn("signpost swap failed: " + e);
            }
        }
    }

    public class FakeSignpostItem : InstantItem
    {
        protected override void Perform()
        {
            FakeSignpost.Arm(player.GlobalID, rand.Next());

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp != null && gp.BoardObject != null) Say(gp.BoardObject, "Указатели врут!");
            }

            ModAssets.Play("snd_chaos", 0.85f);
            try { GameManager.UIController.ShowLargeText("Указатели врут!", LargeTextType.RollTurnOrder); }
            catch { }

            Core.Log("FakeSignpost: armed by player " + player.GlobalID);
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            return new ItemAIUse(user, 0.4f);
        }
    }

    internal static class TrapPrefabs
    {
        internal const string Mine = "PCI_P_Mine";
    }
}
