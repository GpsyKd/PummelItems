using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Aimed at another player rather than at a spot: the wedge picks whoever is best lined
    /// up, and the effect lands on them directly. Reach and shape follow the magnet.
    /// </summary>
    public abstract class TargetedItem : AimedItem
    {
        protected virtual float Reach { get { return 9f; } }
        protected virtual float HalfAngle { get { return 28f; } }

        protected abstract void Affect(BoardPlayer target);
        protected virtual string MissLabel { get { return "Мимо"; } }

        // Show the wedge that actually decides who gets picked, not just a direction.
        protected override float IndicatorReach { get { return Reach; } }
        protected override float IndicatorHalfAngle { get { return HalfAngle; } }

        protected override void PerformAimed(Vector3 dir)
        {
            BoardPlayer target = PickTarget(dir, Reach, HalfAngle);

            if (target == null)
            {
                DiceOverride.Announce(player.BoardObject, MissLabel);
                Core.Log(GetType().Name + ": nobody in the aimed wedge");
                return;
            }

            Affect(target);
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
                if (dist <= Reach) return new ItemAIUse(gp.BoardObject, Mathf.Clamp01(1f - dist / Reach));
            }
            return null;
        }
    }

    /// <summary>
    /// Aimed along a lane instead of across a wedge: whoever is standing in the line gets it,
    /// nearest first.
    ///
    /// A wedge suits something that sweeps - a vacuum, a magnet. A grappling hook is thrown at
    /// one person a long way off, and a wedge models that badly: it is generous up close, where
    /// the arc is narrow in absolute terms, and absurdly wide at range. A lane is the same
    /// width wherever the target stands, which is what "I am aiming at that player" means.
    /// </summary>
    public abstract class BeamTargetedItem : AimedItem
    {
        protected virtual float Reach { get { return 20f; } }
        protected virtual float HalfWidth { get { return 1.7f; } }

        protected abstract void Affect(BoardPlayer target);
        protected virtual string MissLabel { get { return "Мимо"; } }

        protected override float IndicatorReach { get { return Reach; } }
        protected override float IndicatorHalfWidth { get { return HalfWidth; } }
        protected override float AiRange { get { return Reach; } }

        protected override void PerformAimed(Vector3 dir)
        {
            BoardPlayer target = PickAlongLine(dir, Reach, HalfWidth);

            if (target == null)
            {
                DiceOverride.Announce(player.BoardObject, MissLabel);
                Core.Log(GetType().Name + ": nobody in the line");
                return;
            }

            Affect(target);
        }
    }

    // ------------------------------------------------------------------ dice meddling

    /// <summary>The victim's next roll comes up as one.</summary>
    public class CurseItem : TargetedItem
    {
        protected override void Affect(BoardPlayer target)
        {
            short id = target.GamePlayer.GlobalID;
            DiceOverride.ArmForNextTurn(id, 1, "Проклятие!");
            DiceOverride.Announce(target, "Проклят");
            ModAssets.Play("snd_chaos", 0.7f);
            Core.Log("Curse: player " + id + " will roll 1");
        }
    }

    /// <summary>The victim's next roll comes up as zero - a turn that goes nowhere.</summary>
    public class FreezeItem : TargetedItem
    {
        protected override float Reach { get { return 12f; } }

        protected override void Affect(BoardPlayer target)
        {
            short id = target.GamePlayer.GlobalID;
            DiceOverride.ArmForNextTurn(id, 0, "Заморожен!");
            DiceOverride.Announce(target, "Заморожен");
            ModAssets.Play("snd_shatter", 0.9f);
            Effects.Blast(target.transform.position + Vector3.up, 1.6f);
            Core.Log("Freeze: player " + id + " will roll 0");
        }
    }

    /// <summary>Your own next roll counts double.</summary>
    public class DoubleMoveItem : InstantItem
    {
        protected override void Perform()
        {
            short id = player.GlobalID;
            DiceOverride.ArmMultiplierForNextTurn(id, 2, "Двойной ход!");
            DiceOverride.Announce(player.BoardObject, "Заряжено на двойной");
            ModAssets.Play("snd_dice_charge", 0.9f);
            Core.Log("DoubleMove: player " + id + " next roll doubled");
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            if (DiceOverride.IsCharged(user.GamePlayer.GlobalID)) return null;
            return new ItemAIUse(user, 0.6f);
        }
    }

    // ----------------------------------------------------------------------- vampiric

    /// <summary>
    /// Drains a share of the target's health and hands it to the user.
    ///
    /// Taken as a percentage of what the target currently has, so it never finishes anybody
    /// off - a full-health victim loses a lot, a nearly-dead one loses almost nothing. That
    /// keeps it a theft rather than an execution.
    /// </summary>
    public class LifeMagnetItem : TargetedItem
    {
        private const float DrainShare = 0.30f;
        protected override float Reach { get { return 8.5f; } }
        protected override float HalfAngle { get { return 31.5f; } }

        protected override void Affect(BoardPlayer target)
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(target.LocalHealth * DrainShare));

            target.ApplyDamage(new DamageInstance
            {
                damage = amount,
                origin = player.BoardObject.transform.position,
                blood = true,
                ragdoll = false,       // it is a siphon, not a punch
                bloodVel = 12f,
                bloodAmount = 0.9f,
                details = "Life Magnet",
                killer = player.BoardObject,
                removeKeys = false,    // health only; keys are the plain magnet's business
            });

            // Healing clamps to the maximum on its own and prints its own "+N".
            player.BoardObject.ApplyHeal(amount);

            ModAssets.Play("snd_drain", 0.9f);
            Effects.Blast(target.transform.position + Vector3.up, 1.2f);

            Core.Log("LifeMagnet: drained " + amount + " hp from actor " + target.ActorID);
        }
    }

    // ------------------------------------------------------------------ displacement

    /// <summary>Drags the target most of the way to you.</summary>
    public class GrappleItem : BeamTargetedItem
    {
        protected override void Affect(BoardPlayer target)
        {
            BoardNode from = target.CurrentNode;
            BoardNode mine = player.BoardObject.CurrentNode;

            List<BoardNode> path = BoardMove.PathNodes(from, mine);
            if (path.Count < 2)
            {
                DiceOverride.Announce(player.BoardObject, "Не дотянуться");
                Core.Log("Grapple: no route to the target");
                return;
            }

            // Stop one short so the two never end up on the same node.
            BoardNode dest = path[Mathf.Max(1, path.Count - 2)];

            ModAssets.Play("snd_whirl", 0.8f);
            DiceOverride.Announce(target, "Сюда!");
            Core.Log("Grapple: pulling target " + (path.Count - 2) + " node(s) closer");

            StartCoroutine(BoardMove.MoveTo(this, target, dest));
        }
    }

    /// <summary>Boots the target several nodes back down the track.</summary>
    public class KickItem : TargetedItem
    {
        private const int Nodes = 4;
        protected override float Reach { get { return 7f; } }

        protected override void Affect(BoardPlayer target)
        {
            BoardNode dest = BoardMove.StepBack(target.CurrentNode, Nodes);
            if (dest == null || dest == target.CurrentNode)
            {
                DiceOverride.Announce(player.BoardObject, "Некуда толкать");
                Core.Log("Kick: no node behind the target");
                return;
            }

            ModAssets.Play("snd_whack", 0.9f);
            DiceOverride.Announce(target, "Назад!");
            Core.Log("Kick: knocking target back toward node " + dest.NodeID);

            StartCoroutine(BoardMove.MoveTo(this, target, dest));
        }
    }

    /// <summary>Sends the target all the way back to the start.</summary>
    public class StartTicketItem : TargetedItem
    {
        protected override float Reach { get { return 12f; } }

        protected override void Affect(BoardPlayer target)
        {
            BoardNode start = BoardMove.FindStartNode();
            if (start == null)
            {
                Core.Warn("StartTicket: no start node on this board");
                return;
            }

            ModAssets.Play("snd_shuffle", 0.9f);
            DiceOverride.Announce(target, "На старт!");
            Core.Log("StartTicket: sending target to node " + start.NodeID);

            StartCoroutine(BoardMove.MoveTo(this, target, start));
        }
    }
}
