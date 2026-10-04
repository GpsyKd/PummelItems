using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Aimed at players rather than at a spot: everyone the wedge takes in gets the effect,
    /// directly. Reach and shape follow the magnet.
    /// </summary>
    public abstract class TargetedItem : AimedItem
    {
        protected virtual float Reach { get { return 9f; } }
        protected virtual float HalfAngle { get { return 28f; } }

        /// <summary>The effect on one player in the wedge; false when it could not land on them.</summary>
        protected abstract bool Affect(BoardPlayer target);

        /// <summary>Runs once everyone in the wedge is done, for whatever adds up across them.</summary>
        protected virtual void AfterAll() { }

        /// <summary>
        /// Played once for the whole wedge. The same clip started for every player at once is
        /// still the one sound, only several times as loud.
        /// </summary>
        protected virtual string HitSound { get { return null; } }
        protected virtual float HitVolume { get { return 0.9f; } }

        protected virtual string MissLabel { get { return "Мимо"; } }

        // Show the wedge that actually decides who gets hit, not just a direction.
        protected override float IndicatorReach { get { return Reach; } }
        protected override float IndicatorHalfAngle { get { return HalfAngle; } }

        protected override void PerformAimed(Vector3 dir)
        {
            // Everyone in the wedge, not just whoever stands nearest: the wedge on screen is
            // what the player aims, so it is what gets hit.
            List<BoardPlayer> targets = PickTargets(dir, Reach, HalfAngle);

            if (targets.Count == 0)
            {
                DiceOverride.Announce(player.BoardObject, MissLabel);
                Core.Log(GetType().Name + ": nobody in the aimed wedge");
                return;
            }

            int landed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                // One player going wrong must not cost the rest of the wedge their hit.
                try { if (Affect(targets[i])) landed++; }
                catch (System.Exception e) { Core.Warn(GetType().Name + " failed on actor " + targets[i].ActorID + ": " + e); }
            }

            if (landed > 0 && HitSound != null) ModAssets.Play(HitSound, HitVolume);
            AfterAll();

            Core.Log(GetType().Name + ": hit " + landed + " of " + targets.Count + " in the wedge");
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

    /// <summary>The victims' next roll comes up as one.</summary>
    public class CurseItem : TargetedItem
    {
        protected override string HitSound { get { return "snd_chaos"; } }
        protected override float HitVolume { get { return 0.7f; } }

        protected override bool Affect(BoardPlayer target)
        {
            short id = target.GamePlayer.GlobalID;
            DiceOverride.ArmForNextTurn(id, 1, "Проклятие!");
            DiceOverride.Announce(target, "Проклят");
            Core.Log("Curse: player " + id + " will roll 1");
            return true;
        }
    }

    /// <summary>The victims' next roll comes up as zero - a turn that goes nowhere.</summary>
    public class FreezeItem : TargetedItem
    {
        protected override float Reach { get { return 12f; } }
        protected override string HitSound { get { return "snd_shatter"; } }

        protected override bool Affect(BoardPlayer target)
        {
            short id = target.GamePlayer.GlobalID;
            DiceOverride.ArmForNextTurn(id, 0, "Заморожен!");
            DiceOverride.Announce(target, "Заморожен");
            Effects.Blast(target.transform.position + Vector3.up, 1.6f);
            Core.Log("Freeze: player " + id + " will roll 0");
            return true;
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
    /// Drains a share of every target's health and hands it to the user.
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
        protected override string HitSound { get { return "snd_drain"; } }

        private int m_drained;

        protected override bool Affect(BoardPlayer target)
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
            m_drained += amount;

            Effects.Blast(target.transform.position + Vector3.up, 1.2f);

            Core.Log("LifeMagnet: drained " + amount + " hp from actor " + target.ActorID);
            return true;
        }

        protected override void AfterAll()
        {
            // One heal for the whole wedge. Healing clamps to the maximum on its own and prints
            // its own "+N"; a heal per victim would stack those on the same spot, reading as one.
            if (m_drained > 0) player.BoardObject.ApplyHeal(m_drained);
            m_drained = 0;
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

            // The path runs from the target's node to ours. With just those two in it the
            // target is already next to us - "one node short of ours" is where they stand.
            // The old index arithmetic clamped that case to path[1], our own node, and dragged
            // them on top of us.
            if (path.Count == 2)
            {
                DiceOverride.Announce(player.BoardObject, "Уже рядом");
                Core.Log("Grapple: target is already on the next node");
                return;
            }

            // Stop one short so the two never end up on the same node.
            BoardNode dest = path[path.Count - 2];

            ModAssets.Play("snd_whirl", 0.8f);
            DiceOverride.Announce(target, "Сюда!");
            Core.Log("Grapple: pulling target " + (path.Count - 2) + " node(s) closer");

            StartCoroutine(BoardMove.MoveTo(this, target, dest));
        }
    }

    /// <summary>Boots the targets several nodes back down the track.</summary>
    public class KickItem : TargetedItem
    {
        private const int Nodes = 4;
        protected override float Reach { get { return 7f; } }
        protected override string HitSound { get { return "snd_whack"; } }

        protected override bool Affect(BoardPlayer target)
        {
            BoardNode dest = BoardMove.StepBack(target.CurrentNode, Nodes);
            if (dest == null || dest == target.CurrentNode)
            {
                // Over the one who stays put: the others in the wedge may well have gone.
                DiceOverride.Announce(target, "Некуда толкать");
                Core.Log("Kick: no node behind actor " + target.ActorID);
                return false;
            }

            DiceOverride.Announce(target, "Назад!");
            Core.Log("Kick: knocking actor " + target.ActorID + " back toward node " + dest.NodeID);

            StartCoroutine(BoardMove.MoveTo(this, target, dest));
            return true;
        }
    }

    /// <summary>Sends the targets all the way back to the start.</summary>
    public class StartTicketItem : TargetedItem
    {
        protected override float Reach { get { return 12f; } }
        protected override string HitSound { get { return "snd_shuffle"; } }

        protected override bool Affect(BoardPlayer target)
        {
            BoardNode start = BoardMove.FindStartNode();
            if (start == null)
            {
                Core.Warn("StartTicket: no start node on this board");
                return false;
            }

            DiceOverride.Announce(target, "На старт!");
            Core.Log("StartTicket: sending actor " + target.ActorID + " to node " + start.NodeID);

            StartCoroutine(BoardMove.MoveTo(this, target, start));
            return true;
        }
    }
}
