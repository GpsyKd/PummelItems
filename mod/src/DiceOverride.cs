using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Forces the value of a player's next dice roll.
    ///
    /// Every roll in the game - human, AI, turn order - is wrapped in an ActionHitDice, so
    /// intercepting that constructor covers all of them without touching
    /// GameBoardController's enormous Update().
    ///
    /// Two timings are needed. A charge armed by an item applies on the player's NEXT turn
    /// (items are used before that turn's roll, so an immediate flag would fire at once),
    /// while an effect landed on someone else applies to whatever they roll next.
    /// </summary>
    internal static class DiceOverride
    {
        private class Pending
        {
            public byte Value;
            public string Label;      // shown over the player when it goes live
            public bool WaitForTurn;

            // When set, the natural roll is multiplied instead of replaced - which is how
            // "roll twice" is expressed without touching the turn machinery.
            public int Multiplier;
        }

        private static readonly Dictionary<short, Pending> s_pending = new Dictionary<short, Pending>();
        private static readonly Dictionary<short, Pending> s_live = new Dictionary<short, Pending>();

        /// <summary>Applies on the player's next turn.</summary>
        internal static void ArmForNextTurn(short playerID, byte value, string label)
        {
            s_pending[playerID] = new Pending { Value = value, Label = label, WaitForTurn = true };
        }

        /// <summary>Applies to the very next roll this player makes.</summary>
        internal static void ArmImmediate(short playerID, byte value, string label)
        {
            s_live[playerID] = new Pending { Value = value, Label = label, WaitForTurn = false };
        }

        internal static bool IsCharged(short playerID)
        {
            return s_pending.ContainsKey(playerID) || s_live.ContainsKey(playerID);
        }

        internal static void OnTurnStarted(short playerID, BoardPlayer who)
        {
            Pending p;
            if (!s_pending.TryGetValue(playerID, out p)) return;

            s_pending.Remove(playerID);
            s_live[playerID] = p;

            if (!string.IsNullOrEmpty(p.Label))
            {
                Announce(who, p.Label);
                ModAssets.Play("snd_dice_ready", 0.9f);
            }
            Core.Log("DiceOverride: live for player " + playerID + " -> " + p.Value);
        }

        /// <summary>Multiplies the natural roll on the player's next turn.</summary>
        internal static void ArmMultiplierForNextTurn(short playerID, int multiplier, string label)
        {
            s_pending[playerID] = new Pending
            {
                Multiplier = multiplier,
                Label = label,
                WaitForTurn = true,
            };
        }

        /// <summary>Returns the value the roll should take, given what it rolled naturally.</summary>
        internal static bool Consume(short playerID, byte natural, out byte value)
        {
            Pending p;
            if (s_live.TryGetValue(playerID, out p))
            {
                s_live.Remove(playerID);

                if (p.Multiplier > 1)
                {
                    int scaled = natural * p.Multiplier;
                    value = (byte)Mathf.Clamp(scaled, 0, 255);
                }
                else
                {
                    value = p.Value;
                }
                return true;
            }
            value = 0;
            return false;
        }

        internal static byte MaxRoll()
        {
            try
            {
                GameRuleset rs = GameManager.RulesetManager.ActiveRuleset;
                if (rs != null && rs.General != null)
                {
                    int max = rs.General.MaxDiceRoll;
                    if (max > 0 && max < 256) return (byte)max;
                }
            }
            catch { }
            return 9;   // the value the game itself falls back to
        }

        internal static void Announce(BoardPlayer who, string text)
        {
            try
            {
                if (who == null || GameManager.UIController == null) return;
                GameManager.UIController.SpawnWorldText(
                    text, who.transform.position + Vector3.up * 2.2f, 2.5f,
                    WorldTextType.DiceRoll, 0.4f);
            }
            catch (Exception e)
            {
                Core.Warn("world text failed: " + e.Message);
            }
        }
    }

    /// <summary>Promotes a charge that was waiting for the player's turn to come round.</summary>
    [HarmonyPatch(typeof(BoardPlayer), "StartTurn")]
    internal static class Patch_BoardPlayer_StartTurn
    {
        private static void Postfix(BoardPlayer __instance)
        {
            try
            {
                if (__instance == null || __instance.GamePlayer == null) return;
                short id = __instance.GamePlayer.GlobalID;

                DiceOverride.OnTurnStarted(id, __instance);
                TempModifiers.OnTurnStarted(id, __instance);
                PiggyBank.OnTurnStarted(id, __instance);
                FakeSignpost.OnTurnStarted(id, __instance);
            }
            catch (Exception e)
            {
                Core.Warn("StartTurn hook failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ActionHitDice), MethodType.Constructor, new[] { typeof(short), typeof(byte) })]
    internal static class Patch_ActionHitDice_ctor
    {
        private static void Prefix(short _player_id, ref byte _roll_number)
        {
            byte forced;
            if (!DiceOverride.Consume(_player_id, _roll_number, out forced)) return;

            Core.Log("DiceOverride: roll " + _roll_number + " -> " + forced +
                     " for player " + _player_id);
            _roll_number = forced;
        }
    }
}
