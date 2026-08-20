using System.Collections;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Charges the player's die: the roll on their NEXT turn comes up at the ruleset maximum.
    ///
    /// The interception itself lives in DiceOverride, which every dice-meddling item shares.
    /// The charge waits for the next turn on purpose - items are used before the current
    /// turn's roll, so firing immediately would make this a same-turn effect.
    /// </summary>
    public class LoadedDiceItem : Item
    {
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
            StartCoroutine(Charge());
        }

        private IEnumerator Charge()
        {
            short id = player.GlobalID;
            byte max = DiceOverride.MaxRoll();

            DiceOverride.ArmForNextTurn(id, max, "Бросок " + max + "!");

            // Nothing happens on this turn, so say so - otherwise the item looks broken.
            DiceOverride.Announce(player.BoardObject, "Кубик заряжен");
            ModAssets.Play("snd_dice_charge", 0.9f);
            Core.Log("LoadedDice: pending for player " + id + ", fires on their next turn");

            yield return new WaitForSeconds(0.6f);
            Finish(relay: false);
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // No point stacking a second charge on top of an unused one.
            if (DiceOverride.IsCharged(user.GamePlayer.GlobalID)) return null;
            return new ItemAIUse(user, 0.6f);
        }
    }
}
