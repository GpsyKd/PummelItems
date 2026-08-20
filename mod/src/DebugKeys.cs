using System;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Debug-only shortcuts. Board items are normally handed out by the board,
    /// which makes testing a specific item slow.
    ///
    ///   F8  - hurt the local player by 10 (so healing is visible)
    ///   F9  - give the local player one Shuffle
    ///   F10 - log the current enabled-item table
    /// </summary>
    internal static class DebugKeys
    {
        internal static void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7)) ShowHealth();
            if (Input.GetKeyDown(KeyCode.F8)) HurtSelf();
            if (Input.GetKeyDown(KeyCode.F9)) GiveCustomItem();
            if (Input.GetKeyDown(KeyCode.F10)) LogItemTable();
        }

        /// <summary>
        /// Health of everyone, on screen and in the log. Press before and after using an
        /// item to see whether it actually did anything - watching a bot's health bar mid
        /// explosion is not a reliable test.
        /// </summary>
        private static void ShowHealth()
        {
            try
            {
                if (GameManager.Board == null)
                {
                    Core.Warn("F7: not on the board right now");
                    return;
                }

                Core.Log("--- health ---");
                for (int i = 0; i < GameManager.PlayerCount; i++)
                {
                    GamePlayer gp = GameManager.GetPlayerAt(i);
                    if (gp == null || gp.BoardObject == null) continue;

                    string line = gp.Name + " (actor " + gp.BoardObject.ActorID + "): " +
                                  gp.BoardObject.LocalHealth + " hp";
                    Core.Log("  " + line);

                    GameManager.UIController.SpawnWorldText(
                        gp.BoardObject.LocalHealth + " hp",
                        gp.BoardObject.transform.position + Vector3.up * 2.6f,
                        2f, WorldTextType.Heal, 0.2f);
                }
            }
            catch (Exception e)
            {
                Core.Warn("F7 failed: " + e);
            }
        }

        private static void HurtSelf()
        {
            try
            {
                BoardPlayer me = LocalBoardPlayer();
                if (me == null || GameManager.Board == null)
                {
                    Core.Warn("F8: not on the board right now");
                    return;
                }

                DamageInstance d = default(DamageInstance);
                d.damage = 10;
                d.origin = me.transform.position;
                d.hitAnim = false;
                d.sound = false;
                d.ragdoll = false;
                d.blood = false;
                d.killer = null;
                d.details = "debug";

                me.ApplyDamage(d);
                Core.Log("F8: applied 10 damage to self");
            }
            catch (Exception e)
            {
                Core.Warn("F8 failed: " + e);
            }
        }

        private static BoardPlayer LocalBoardPlayer()
        {
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer p = GameManager.GetPlayerAt(i);
                if (p != null && p.IsLocalPlayer && !p.IsAI && p.BoardObject != null)
                    return p.BoardObject;
            }
            return null;
        }

        private static int IndexOfCustom(ulong itemID)
        {
            ItemList list = GameManager.ItemList;
            if (list == null || list.enabledItems == null) return -1;
            for (int i = 0; i < list.enabledItems.Length; i++)
            {
                if (list.enabledItems[i] != null && list.enabledItems[i].itemID == itemID)
                    return i;
            }
            return -1;
        }

        private static void GiveCustomItem()
        {
            try
            {
                if (GameManager.Board == null)
                {
                    Core.Warn("F9: not on the board right now");
                    return;
                }

                BoardPlayer me = LocalBoardPlayer();
                if (me == null)
                {
                    Core.Warn("F9: no local board player");
                    return;
                }

                // Inventory writes are server-authoritative; on a local game the host is us.
                if (!ZP.Net.NetSystem.IsServer)
                {
                    Core.Warn("F9: only the host can grant items - ask the host to press it");
                    return;
                }

                foreach (ulong id in ItemRegistry.AllIds())
                {
                    int idx = IndexOfCustom(id);
                    if (idx < 0)
                    {
                        Core.Warn("F9: item id=" + id + " is not in enabledItems");
                        continue;
                    }
                    me.GiveItem((byte)idx);
                    Core.Log("F9: gave id=" + id + " (itemIndex=" + idx + "), now holding " +
                             me.GetItemCount((byte)idx));
                }
            }
            catch (Exception e)
            {
                Core.Warn("F9 failed: " + e);
            }
        }

        private static void LogItemTable()
        {
            try
            {
                ItemList list = GameManager.ItemList;
                if (list == null || list.enabledItems == null)
                {
                    Core.Warn("F10: no item list yet");
                    return;
                }
                Core.Log("--- enabledItems (" + list.enabledItems.Length + ") ---");
                for (int i = 0; i < list.enabledItems.Length; i++)
                {
                    ItemDetails d = list.enabledItems[i];
                    if (d == null) { Core.Log("  [" + i + "] (null)"); continue; }
                    Core.Log("  [" + i + "] idx=" + d.itemIndex + " id=" + d.itemID +
                             " net=" + d.netPrefabName + " name=" + d.itemNameToken);
                }
            }
            catch (Exception e)
            {
                Core.Warn("F10 failed: " + e);
            }
        }
    }
}
