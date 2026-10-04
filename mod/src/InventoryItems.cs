using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    /// <summary>
    /// Shared shape for items that just do something and finish - no aiming, no projectile.
    ///
    /// Perform() runs on EVERY machine, the way the game's own items work. The owner relays
    /// the use with a seed, and Item.Use reseeds `rand` from it, so every machine draws the
    /// same numbers and reaches the same decision. Health, keys, movement, board rules,
    /// sounds and text are then applied everywhere: the game expects that - a client keeps a
    /// local copy of health, and GiveGold only changes the number on the host but shows its
    /// effect on every screen.
    ///
    /// The exceptions are things the game syncs from the host itself - inventory counts and
    /// spawned network objects. Doing those on a client would double them, so subclasses wrap
    /// them in <see cref="Authority"/>.
    ///
    /// This used to run on the host only. In local play that is the same thing, which is why
    /// it never showed; online, every effect that is not synced by the game happened on the
    /// host's screen and nowhere else.
    /// </summary>
    public abstract class InstantItem : Item
    {
        protected abstract void Perform();
        protected virtual float FinishDelay { get { return 0.5f; } }

        /// <summary>True on the machine whose word is final for synced state.</summary>
        protected static bool Authority { get { return NetSystem.IsServer; } }

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
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            try { Perform(); }
            catch (System.Exception e) { Core.Warn(GetType().Name + " failed: " + e); }

            yield return new WaitForSeconds(FinishDelay);
            Finish(relay: false);
        }

        protected List<BoardPlayer> OtherLivingPlayers()
        {
            List<BoardPlayer> list = new List<BoardPlayer>();
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null) continue;
                if (gp.BoardObject == player.BoardObject) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;
                list.Add(gp.BoardObject);
            }
            return list;
        }

        protected static void Say(BoardPlayer who, string text)
        {
            DiceOverride.Announce(who, text);
        }

        /// <summary>
        /// For an item that has just killed its own user: this use ends the turn, with no roll
        /// after it. When the item finishes the board hands the turn back for the roll, and a
        /// dead player can neither roll nor walk - most likely the freeze after Armageddon.
        /// </summary>
        protected void EndTurnAfterUse()
        {
            NoRollAfterUse.Raise(details);
        }
    }

    /// <summary>
    /// The game's own "this item ends your turn": the flag its swap portal carries, after which
    /// the user cannot roll. Raised on one item for the one use that needs it, and lowered again
    /// when the next turn starts, so every other use of that item plays as usual.
    /// </summary>
    internal static class NoRollAfterUse
    {
        private static readonly List<ItemDetails> s_raised = new List<ItemDetails>();

        internal static void Raise(ItemDetails d)
        {
            if (d == null || d.skipTurnAfterUse) return;   // already the item's own setting
            d.skipTurnAfterUse = true;
            s_raised.Add(d);
            Core.Log("NoRollAfterUse: '" + d.itemNameToken + "' ends this turn");
        }

        internal static void Lower()
        {
            for (int i = 0; i < s_raised.Count; i++)
                if (s_raised[i] != null) s_raised[i].skipTurnAfterUse = false;
            s_raised.Clear();
        }
    }

    /// <summary>Trades your whole inventory with a random opponent.</summary>
    public class SwapInventoryItem : InstantItem
    {
        protected override void Perform()
        {
            List<BoardPlayer> others = OtherLivingPlayers();
            if (others.Count == 0)
            {
                Core.Log("SwapInventory: nobody to trade with");
                return;
            }

            BoardPlayer victim = others[rand.Next(0, others.Count)];
            BoardPlayer me = player.BoardObject;

            int len = me.inventory.Length;
            byte[] mine = new byte[len];
            byte[] theirs = new byte[len];
            for (int i = 0; i < len; i++)
            {
                mine[i] = me.inventory[i];
                theirs[i] = victim.inventory[i];
            }

            // This item is being consumed as we speak, so hand over the count it will have
            // after the fact rather than a copy of itself.
            byte myIndex = details.itemIndex;
            if (mine[myIndex] > 0) mine[myIndex]--;

            // Inventories are synced from the host; a client writing them would only fight it.
            if (Authority)
            {
                me.SetInventory(theirs);
                victim.SetInventory(mine);
            }

            Say(me, "Обмен!");
            Say(victim, "Обмен!");
            ModAssets.Play("snd_shuffle", 0.8f);
            Core.Log("SwapInventory: traded with " + victim.GamePlayer.Name);
        }
    }

    /// <summary>Duplicates one random item you are already carrying.</summary>
    public class CopierItem : InstantItem
    {
        protected override void Perform()
        {
            BoardPlayer me = player.BoardObject;

            List<int> owned = new List<int>();
            for (int i = 0; i < me.inventory.Length; i++)
            {
                if (i == details.itemIndex) continue;   // copying itself would be too easy
                if (me.inventory[i] > 0) owned.Add(i);
            }

            if (owned.Count == 0)
            {
                Say(me, "Копировать нечего");
                Core.Log("Copier: inventory is empty");
                return;
            }

            int pick = owned[rand.Next(0, owned.Count)];
            if (me.inventory[pick] >= byte.MaxValue) return;

            // GiveItem is how the game itself hands out an item: the pop-up effect and its
            // record of what each player has had happen on every machine, the count only on
            // the host. Editing the inventory directly skipped the first two everywhere.
            me.GiveItem((byte)pick);

            ItemDetails copied = GameManager.GetItemFromItemIndex(pick);
            string name = (copied != null) ? copied.TranslatedName : ("#" + pick);
            Say(me, "+1 " + name);
            ModAssets.Play("snd_dice_ready", 0.8f);
            Core.Log("Copier: duplicated " + name);
        }
    }

    /// <summary>Melts everything you carry down into a single random item.</summary>
    public class JunkShopItem : InstantItem
    {
        protected override void Perform()
        {
            BoardPlayer me = player.BoardObject;

            int scrapped = 0;
            for (int i = 0; i < me.inventory.Length; i++)
            {
                byte count = me.inventory[i];
                if (count == 0) continue;
                scrapped += count;
                if (Authority) me.EditInventory(i, 0);
            }

            ItemDetails prize = PickPrize(me);
            if (prize == null)
            {
                Core.Warn("JunkShop: no item to hand out");
                return;
            }

            me.GiveItem((byte)prize.itemIndex);
            Say(me, prize.TranslatedName + "!");
            ModAssets.Play("snd_dice_ready", 0.9f);
            Core.Log("JunkShop: melted " + scrapped + " item(s) into " + prize.itemNameToken);
        }

        /// <summary>
        /// The same pool and weights as ItemList.GetRandomItem - ordinary items only, and ones
        /// the player has already had much less likely - with two differences.
        ///
        /// The junk shop itself is left out. The comment here always claimed it was, but
        /// GetRandomItem never knew to skip it, so melting everything could hand back another
        /// junk shop.
        ///
        /// And the draw comes from this item's own `rand`, which every machine seeds the same
        /// way. GetRandomItem draws from GameManager.rand, the game's shared generator, so
        /// calling it on every machine would advance that on clients the host never asked for.
        /// </summary>
        private ItemDetails PickPrize(BoardPlayer me)
        {
            ItemDetails[] pool = GameManager.ItemList.enabledItems;
            if (pool == null) return null;

            List<ItemDetails> candidates = new List<ItemDetails>();
            List<float> weights = new List<float>();
            float total = 0f;

            for (int i = 0; i < pool.Length; i++)
            {
                ItemDetails d = pool[i];
                if (d == null || d.weaponSpaceItem) continue;
                if (d.itemIndex == details.itemIndex) continue;

                float w = 1f;
                try
                {
                    byte had = me.ObtainedInventory[d.itemIndex];
                    w = (had == 0) ? 1f : (had == 1) ? 0.1f : (had == 2) ? 0.03f : 0.01f;
                }
                catch { }

                candidates.Add(d);
                weights.Add(w);
                total += w;
            }
            if (candidates.Count == 0) return null;

            double roll = rand.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }

    /// <summary>Takes keys off whoever is richest and splits them among everybody else.</summary>
    public class TaxItem : InstantItem
    {
        private const int MaxTake = 12;

        protected override void Perform()
        {
            BoardPlayer me = player.BoardObject;

            // The user is never a payer. Taxing yourself and handing the money to rivals is
            // a gift, not a tax - if you are top of the pile, the next richest pays.
            List<BoardPlayer> others = new List<BoardPlayer>();
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == me) continue;
                others.Add(gp.BoardObject);
            }

            if (others.Count == 0)
            {
                Say(me, "Не с кого брать");
                return;
            }

            int top = int.MinValue;
            for (int i = 0; i < others.Count; i++)
                if (others[i].Gold > top) top = others[i].Gold;

            if (top <= 0)
            {
                Say(me, "Нечего взять");
                Core.Log("Tax: everybody else is broke");
                return;
            }

            // Everyone tied at the top pays. Picking one of several equal leaders would be
            // arbitrary; taxing all of them needs no coin toss, which also keeps the result
            // identical on every machine.
            List<BoardPlayer> payers = new List<BoardPlayer>();
            List<BoardPlayer> receivers = new List<BoardPlayer>();
            for (int i = 0; i < others.Count; i++)
            {
                if (others[i].Gold == top) payers.Add(others[i]);
                else receivers.Add(others[i]);
            }
            receivers.Add(me);   // the user always collects

            int pot = 0;
            for (int i = 0; i < payers.Count; i++)
                pot += Mathf.Min(MaxTake, payers[i].Gold);

            int each = pot / receivers.Count;
            if (each <= 0)
            {
                Say(me, "Слишком мало");
                Core.Log("Tax: pot of " + pot + " will not split between " + receivers.Count);
                return;
            }

            // Keys must neither appear nor vanish, so collect exactly what gets handed out,
            // round-robin so equal leaders lose equal amounts.
            int toCollect = each * receivers.Count;
            int[] taken = new int[payers.Count];
            bool progressed = true;
            while (toCollect > 0 && progressed)
            {
                progressed = false;
                for (int i = 0; i < payers.Count && toCollect > 0; i++)
                {
                    if (taken[i] >= Mathf.Min(MaxTake, payers[i].Gold)) continue;
                    taken[i]++;
                    toCollect--;
                    progressed = true;
                }
            }

            for (int i = 0; i < payers.Count; i++)
            {
                if (taken[i] <= 0) continue;
                payers[i].RemoveGold(taken[i]);
                Say(payers[i], "-" + taken[i]);
            }

            for (int i = 0; i < receivers.Count; i++)
            {
                receivers[i].GiveGold(each);
                Say(receivers[i], "+" + each);
            }

            ModAssets.Play("snd_ping", 0.8f);
            Core.Log("Tax: " + payers.Count + " payer(s) at " + top + " keys -> " +
                     each + " to each of " + receivers.Count + " receiver(s)");
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // Only worth it when somebody else is ahead.
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == user) continue;
                if (gp.BoardObject.Gold > user.Gold + 5) return new ItemAIUse(user, 0.7f);
            }
            return null;
        }
    }
}
