using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Super item: every living player is teleported to a different player's node.
    ///
    /// Built on the same moves SwapItem uses for its two-player swap - LeaveNode, reassign
    /// CurrentNode, then drop everyone in with a ragdoll - generalised to all players and a
    /// random derangement instead of a straight exchange.
    /// </summary>
    public class ShuffleItem : Item
    {
        private const float DropForce = 24f;

        public override void Setup()
        {
            base.Setup();
            player.BoardObject.PlayerAnimation.Carrying = true;
            SetNetworkState(ItemState.Setup);
        }

        public override void Unequip(bool endingTurn)
        {
            player.BoardObject.PlayerAnimation.Carrying = false;
            base.Unequip(endingTurn);
        }

        protected override void Use(int seed)
        {
            base.Use(seed);
            StartCoroutine(DoShuffle());
        }

        private IEnumerator DoShuffle()
        {
            List<BoardPlayer> players = LivingPlayers();
            if (players.Count < 2)
            {
                Core.Log("Shuffle: fewer than 2 living players, nothing to do");
                Finish(relay: false);
                yield break;
            }

            // Where everyone stands right now.
            BoardNode[] nodes = new BoardNode[players.Count];
            for (int i = 0; i < players.Count; i++) nodes[i] = players[i].CurrentNode;

            int[] perm = Derangement(players.Count);

            Core.Log("Shuffle: moving " + players.Count + " players");
            ModAssets.Play("snd_shuffle", 0.9f);

            // A player disguised as a cactus is dropped out of the disguise first, the same
            // way SwapItem handles it, otherwise the disguise follows them across the board.
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].CactusScript != null)
                    players[i].RemoveCactus(players[i].transform.position, 5f);
            }

            // Free every slot before claiming new ones, or two players can end up fighting
            // over the same spot on a node.
            for (int i = 0; i < players.Count; i++)
                players[i].CurrentNode.LeaveNode(players[i]);

            for (int i = 0; i < players.Count; i++)
                players[i].CurrentNode = nodes[perm[i]];

            yield return new WaitForSeconds(0.2f);

            Coroutine[] drops = new Coroutine[players.Count];
            for (int i = 0; i < players.Count; i++)
                drops[i] = StartCoroutine(players[i].StartRagdoll(0f, DropForce, setAnim: false, 0.2f));

            for (int i = 0; i < players.Count; i++)
                yield return drops[i];

            // The dice hovers over the player; without this it stays at the old position.
            for (int i = 0; i < players.Count; i++)
                players[i].diceEffect.startPos = players[i].DicePosition();

            Core.Log("Shuffle: done");
            Finish(relay: false);
        }

        private static List<BoardPlayer> LivingPlayers()
        {
            List<BoardPlayer> list = new List<BoardPlayer>();
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                list.Add(gp.BoardObject);
            }
            return list;
        }

        /// <summary>
        /// A permutation where nobody keeps their own place. Uses the item's shared seed so
        /// every machine produces the same arrangement.
        /// </summary>
        private int[] Derangement(int n)
        {
            int[] p = new int[n];
            for (int i = 0; i < n; i++) p[i] = i;

            // Sattolo's algorithm: a single cycle through all n positions, which by
            // construction leaves no element in place.
            for (int i = n - 1; i > 0; i--)
            {
                int j = rand.Next(i);      // strictly less than i - that is what makes it a cycle
                int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
            }
            return p;
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // Worth using when the user is behind: the further from first place, the better.
            int ahead = 0;
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == user) continue;
                if (gp.BoardObject.Gold > user.Gold) ahead++;
            }
            float priority = (GameManager.PlayerCount <= 1)
                ? 0.1f
                : Mathf.Clamp01((float)ahead / (GameManager.PlayerCount - 1));
            return new ItemAIUse(user, Mathf.Max(0.1f, priority));
        }
    }
}
