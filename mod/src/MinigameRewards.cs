using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Puts our items into the rewards handed out after a minigame.
    ///
    /// Those rewards do not come from the normal item pool at all: MinigameResults holds
    /// hard-coded loot tables per placement, listing vanilla ids 0..10 plus 255 for "no
    /// reward". Two consequences we have to work around:
    ///
    ///  * our items can never appear there, since the tables are compiled in;
    ///  * the tables resolve entries with GameManager.GetItemFromEnum((Items)id), so simply
    ///    inserting our ids would cast a number with no enum member and crash on null.
    ///
    /// So the entries are left alone and the RESULT is substituted instead.
    ///
    /// The vanilla tables list only ids 0..10 - the four supers (11..14) never appear.
    /// Minigames are not meant to hand out supers, so ours are excluded the same way.
    /// </summary>
    internal static class MinigameRewards
    {
        /// <summary>
        /// Share of rewards that become one of ours, in percent. An even split: half the
        /// minigame rewards stay vanilla, half become ours. Nothing subtler is justified -
        /// any other number would be a made-up preference.
        /// </summary>
        private const int OurShare = 50;

        private const int NoItem = 255;   // ItemDetails.NO_ITEM

        /// <summary>
        /// Power grade of each vanilla reward item, read off the tables themselves: ids that
        /// only ever appear in the first-place table are grade 1, and so on down to the
        /// health kit, which is all a fourth-place finisher can hope for.
        /// </summary>
        private static readonly Dictionary<int, int> s_vanillaTier = new Dictionary<int, int>
        {
            { 8, 1 },   // Ракета-шампур
            { 5, 1 },   // Баклажан
            { 6, 1 },   // Портал замены
            { 10, 1 },  // Подарок
            { 7, 2 },   // Шаровой таран
            { 4, 2 },   // Магнит
            { 3, 2 },   // Улей
            { 9, 3 },   // Тактический кактус
            { 0, 3 },   // Боксёрская перчатка
            { 1, 3 },   // Дробовик
            { 2, 4 },   // Аптечка
        };

        private static FieldInfo s_itemsField;

        internal static void Apply(MinigameItemLootTable table, System.Random rand,
                                   BoardPlayer player, ref int result)
        {
            int realWeight, blankWeight, tier;
            bool anyActive;
            Inspect(table, out realWeight, out blankWeight, out anyActive, out tier);

            List<ItemDetails> ours = ItemRegistry.MinigameEligible(tier);
            if (ours.Count == 0) return;

            if (result != NoItem)
            {
                if (rand.Next(100) < OurShare) result = Pick(ours, rand, player);
                return;
            }

            // A 255 has two very different meanings: the roll landed on a deliberate blank,
            // or every real entry is disabled and the table had nothing to give. Only the
            // second one should be filled in - which is exactly what happens when all the
            // vanilla items are switched off.
            if (anyActive || realWeight <= 0) return;

            // Roll the same odds the table would have used, so a dead table still hands out
            // rewards at the frequency its placement was designed for.
            if (rand.Next(realWeight + blankWeight) < realWeight)
                result = Pick(ours, rand, player);
        }

        /// <summary>Prefers items the player has not been given yet, like the game does.</summary>
        private static int Pick(List<ItemDetails> ours, System.Random rand, BoardPlayer player)
        {
            List<ItemDetails> fresh = new List<ItemDetails>();
            if (player != null)
            {
                for (int i = 0; i < ours.Count; i++)
                {
                    int idx = ours[i].itemIndex;
                    if (idx >= 0 && idx < player.ObtainedInventory.Length &&
                        player.ObtainedInventory[idx] == 0)
                        fresh.Add(ours[i]);
                }
            }

            List<ItemDetails> pool = (fresh.Count > 0) ? fresh : ours;
            return (int)pool[rand.Next(pool.Count)].itemID;
        }

        /// <summary>
        /// Reads the table's entries: how much weight sits on real items versus blanks,
        /// whether any real entry is still active, and the table's overall power grade -
        /// the weight-averaged grade of its vanilla entries, which is what tells a
        /// first-place table apart from a fourth-place one.
        /// </summary>
        private static void Inspect(MinigameItemLootTable table, out int realWeight,
                                    out int blankWeight, out bool anyActive, out int tier)
        {
            realWeight = 0;
            blankWeight = 0;
            anyActive = false;
            tier = 3;

            try
            {
                if (s_itemsField == null)
                {
                    s_itemsField = typeof(MinigameItemLootTable).GetField(
                        "m_items", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (s_itemsField == null) return;

                IEnumerable entries = s_itemsField.GetValue(table) as IEnumerable;
                if (entries == null) return;

                int tierWeight = 0;
                int tierSum = 0;

                foreach (object entry in entries)
                {
                    LootTableItem<int> e = entry as LootTableItem<int>;
                    if (e == null) continue;

                    if (e.ItemID == NoItem) { blankWeight += e.Weight; continue; }

                    realWeight += e.Weight;

                    int t;
                    if (s_vanillaTier.TryGetValue(e.ItemID, out t))
                    {
                        tierSum += t * e.Weight;
                        tierWeight += e.Weight;
                    }

                    // Active state is checked the same way the table itself does it.
                    ItemDetails d = GameManager.GetItemFromEnum((Items)e.ItemID);
                    if (d != null && d.GetIsActive()) anyActive = true;
                }

                if (tierWeight > 0)
                    tier = Mathf.Clamp(Mathf.RoundToInt((float)tierSum / tierWeight), 1, 4);
            }
            catch (Exception ex)
            {
                Core.Warn("loot table inspection failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MinigameItemLootTable), "ChooseItemIndex")]
    internal static class Patch_ChooseItemIndex
    {
        private static void Postfix(MinigameItemLootTable __instance, System.Random rand,
                                    BoardPlayer player, ref int __result)
        {
            try
            {
                int before = __result;
                MinigameRewards.Apply(__instance, rand, player, ref __result);
                if (before != __result)
                    Core.Log("minigame reward: " + before + " -> " + __result);
            }
            catch (Exception e)
            {
                Core.Warn("minigame reward swap failed: " + e);
            }
        }
    }
}
