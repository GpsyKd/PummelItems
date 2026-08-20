using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Moving players between board nodes, and walking the board graph.
    ///
    /// The relocation sequence is the one SwapItem uses for its two-player swap: free the
    /// slot, reassign CurrentNode, then drop the player in with a ragdoll. Skipping
    /// LeaveNode leaves two players fighting over one slot.
    /// </summary>
    internal static class BoardMove
    {
        internal static readonly BoardNodeTransition[] AllTransitions =
        {
            BoardNodeTransition.Walking,
            BoardNodeTransition.Jumping,
            BoardNodeTransition.Teleport,
            BoardNodeTransition.RandomTeleport,
            BoardNodeTransition.Cannon,
        };

        internal static IEnumerator MoveTo(MonoBehaviour host, BoardPlayer p, BoardNode node)
        {
            if (p == null || node == null || p.CurrentNode == node) yield break;

            if (p.CactusScript != null) p.RemoveCactus(p.transform.position, 5f);

            p.CurrentNode.LeaveNode(p);
            p.CurrentNode = node;

            yield return new WaitForSeconds(0.15f);
            yield return host.StartCoroutine(p.StartRagdoll(0f, 22f, setAnim: false, 0.2f));

            // The dice hovers over the player; without this it stays at the old spot.
            p.diceEffect.startPos = p.DicePosition();
        }

        /// <summary>
        /// The chain of nodes from <paramref name="from"/> to <paramref name="to"/>,
        /// index 0 being <paramref name="from"/>. Empty when there is no route.
        /// </summary>
        internal static List<BoardNode> PathNodes(BoardNode from, BoardNode to)
        {
            List<BoardNode> path = new List<BoardNode>();
            if (from == null || to == null) return path;

            SearchNode search = GameManager.Board.GetPath(
                from, to, AllTransitions, BoardNodeConnectionDirection.Both);

            while (search != null && search.node != null)
            {
                path.Add(search.node);
                search = search.next;
            }

            // GetPath's orientation is not documented anywhere; normalise it rather than
            // trust it, so callers can always treat index 0 as the starting point.
            if (path.Count > 1 && path[0] != from && path[path.Count - 1] == from)
                path.Reverse();

            return path;
        }

        /// <summary>Node <paramref name="steps"/> along the route, clamped to its ends.</summary>
        internal static BoardNode StepAlong(BoardNode from, BoardNode to, int steps)
        {
            List<BoardNode> path = PathNodes(from, to);
            if (path.Count == 0) return null;

            int index = Mathf.Clamp(steps, 0, path.Count - 1);
            return path[index];
        }

        /// <summary>Walks backwards from a node against the direction of travel.</summary>
        internal static BoardNode StepBack(BoardNode from, int steps)
        {
            BoardNode current = from;

            for (int i = 0; i < steps; i++)
            {
                BoardNode previous = FirstBackConnection(current);
                if (previous == null) break;
                current = previous;
            }
            return current;
        }

        private static BoardNode FirstBackConnection(BoardNode node)
        {
            if (node == null) return null;

            List<BoardNode> back = node.GetBackNodes();
            if (back == null || back.Count == 0) return null;

            // Forks going backwards are rare; taking the first is enough and keeps the
            // result the same on every machine.
            return back[0];
        }

        internal static BoardNode FindStartNode()
        {
            BoardNode[] all = GameManager.Board.BoardNodes;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].CurrentNodeType == BoardNodeType.Start) return all[i];
            }
            return (all.Length > 0) ? all[0] : null;
        }
    }
}
