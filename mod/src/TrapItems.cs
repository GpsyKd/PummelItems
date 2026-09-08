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
    /// The game's PersistentItem is built around per-turn events, not "somebody stepped
    /// here", so the mine watches for itself: every frame the server asks which node each
    /// player is on. Hooking BoardNode.EnterNode looked tidier but does not fire reliably -
    /// several movement paths assign the curNode field directly and skip the property setter
    /// that raises it. Reading the value instead of listening for the event cannot miss.
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

                // .material clones the shared asset, so tinting one mine leaves the rest alone.
                Material m = r.material;
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
            Core.Log("Mine: armed on node " + m_nodeID);
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

        /// <summary>Server-side watch for anybody standing on the mined node.</summary>
        private void Update()
        {
            if (m_dead || m_nodeID < 0 || !NetSystem.IsServer) return;

            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null) continue;

                BoardPlayer bp = gp.BoardObject;
                if (bp == null || bp.LocalHealth <= 0) continue;
                if (bp.CurrentNode == null || bp.CurrentNode.NodeID != m_nodeID) continue;
                if (OwnedBy(bp)) continue;          // you do not step on your own mine

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

            GameObject go = NetSystem.Spawn(TrapPrefabs.Mine,
                                            node.transform.position + Vector3.up * 0.22f,
                                            Quaternion.identity, base.OwnerSlot, player.NetOwner);
            if (go == null) { Core.Warn("Mine: spawn failed"); return; }

            LandMine mine = go.GetComponent<LandMine>();
            if (mine != null) mine.Arm(node);

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

        internal static bool Active { get { return s_active; } }

        internal static void Arm(short ownerID)
        {
            s_ownerID = ownerID;
            s_active = true;
            s_turnsLeft = 2;
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
                int picked = UnityEngine.Random.Range(0, choices.Count);

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
            FakeSignpost.Arm(player.GlobalID);

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
