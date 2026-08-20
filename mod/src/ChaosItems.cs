using System.Collections.Generic;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>
    /// Rules that switch on for a single round and switch off again.
    ///
    /// The game keeps its active rules in BoardModifier.ActiveModifiers - a plain public
    /// list - and consults them through virtual hooks, so a rule can be added and removed
    /// while a game is running. We subclass rather than reuse the shipped modifiers: their
    /// ids are what other code checks for, but their extra behaviour is not always wanted.
    /// </summary>
    internal static class TempModifiers
    {
        private class Entry
        {
            public BoardModifier Modifier;
            public short UntilPlayer;
            public string EndLabel;

            // Counts the owner's turn starts. It has to survive the FIRST one, because
            // that is the turn the owner gets to act under their own rule - expiring there
            // made these items impossible to use on purpose.
            public int TurnsLeft;
        }

        private static readonly List<Entry> s_active = new List<Entry>();

        internal static bool IsActive(int modifierID)
        {
            for (int i = 0; i < s_active.Count; i++)
                if (s_active[i].Modifier.GetGameModifierID() == modifierID) return true;
            return false;
        }

        internal static void Apply(BoardModifier mod, short untilPlayer, string endLabel)
        {
            BoardModifier.ActiveModifiers.Add(mod);
            mod.Initialize();
            s_active.Add(new Entry
            {
                Modifier = mod,
                UntilPlayer = untilPlayer,
                EndLabel = endLabel,
                TurnsLeft = 2,
            });
            Core.Log("TempModifier: " + mod.GetType().Name + " on, through player " +
                     untilPlayer + "'s next turn");
        }

        /// <summary>Called from the shared StartTurn hook.</summary>
        internal static void OnTurnStarted(short playerID, BoardPlayer who)
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                if (s_active[i].UntilPlayer != playerID) continue;

                s_active[i].TurnsLeft--;
                if (s_active[i].TurnsLeft > 0) continue;

                BoardModifier.ActiveModifiers.Remove(s_active[i].Modifier);
                Core.Log("TempModifier: " + s_active[i].Modifier.GetType().Name + " expired");

                if (!string.IsNullOrEmpty(s_active[i].EndLabel))
                    DiceOverride.Announce(who, s_active[i].EndLabel);

                s_active.RemoveAt(i);
            }
        }

        /// <summary>A new board means none of ours should survive.</summary>
        internal static void Clear()
        {
            for (int i = 0; i < s_active.Count; i++)
                BoardModifier.ActiveModifiers.Remove(s_active[i].Modifier);
            s_active.Clear();
        }
    }

    /// <summary>Everyone dies to a single hit. Same id the shipped rule uses.</summary>
    public class TempOneHitKill : BoardModifier
    {
        protected override int GetModifierID() { return 5; }

        public override void OnApplyDamage(BoardPlayer target, ref DamageInstance d)
        {
            d.damage = target.LocalHealth;
        }
    }

    /// <summary>Wounded players spill keys. Behaviour lives in the game; the id is the switch.</summary>
    public class TempPinata : BoardModifier
    {
        protected override int GetModifierID() { return 9; }
    }

    /// <summary>
    /// Items are not consumed when used. Deliberately NOT the shipped Modifier_UnlimitedItems:
    /// that one also hands every player every item on returning from a minigame, which would
    /// be a very different item than the one advertised.
    /// </summary>
    public class TempUnlimitedItems : BoardModifier
    {
        protected override int GetModifierID() { return 4; }

        public override bool OnShouldConsumeItems() { return false; }
    }

    // ---------------------------------------------------------------------- the items

    public abstract class ChaosItem : InstantItem
    {
        protected abstract BoardModifier MakeModifier();
        protected abstract string StartLabel { get; }
        protected abstract string EndLabel { get; }

        protected override void Perform()
        {
            TempModifiers.Apply(MakeModifier(), player.GlobalID, EndLabel);

            // Everybody needs to know the rules changed, not just the one who did it.
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp != null && gp.BoardObject != null) Say(gp.BoardObject, StartLabel);
            }

            ModAssets.Play("snd_chaos", 0.9f);

            try { GameManager.UIController.ShowLargeText(StartLabel, LargeTextType.RollTurnOrder); }
            catch { }
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            return new ItemAIUse(user, 0.5f);
        }
    }

    public class GlassCannonsItem : ChaosItem
    {
        protected override BoardModifier MakeModifier() { return new TempOneHitKill(); }
        protected override string StartLabel { get { return "Стеклянные пушки!"; } }
        protected override string EndLabel { get { return "Пушки остыли"; } }
    }

    public class PinataRoundItem : ChaosItem
    {
        protected override BoardModifier MakeModifier() { return new TempPinata(); }
        protected override string StartLabel { get { return "Пиньята!"; } }
        protected override string EndLabel { get { return "Пиньята кончилась"; } }
    }

    public class GenerosityItem : ChaosItem
    {
        protected override BoardModifier MakeModifier() { return new TempUnlimitedItems(); }
        protected override string StartLabel { get { return "Щедрость!"; } }
        protected override string EndLabel { get { return "Щедрость кончилась"; } }
    }
}
