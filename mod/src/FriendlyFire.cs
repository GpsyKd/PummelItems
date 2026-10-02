namespace PummelCustomItems
{
    /// <summary>
    /// The game's team rule for board items, in one place: with friendly fire off, an item
    /// does not hurt its user's teammates.
    ///
    /// Thrown items always followed it, while the mine, the death wand and Armageddon did not -
    /// they went through damage on their own and nobody had asked the question there. One
    /// helper means every item gives the same answer.
    ///
    /// The user is never their own teammate here. Items that hurt their user on purpose -
    /// Armageddon, a backfiring death wand - still do, because that is the item's price, not
    /// friendly fire.
    /// </summary>
    internal static class FriendlyFire
    {
        internal static bool Spares(BoardActor victim, GamePlayer attacker)
        {
            try
            {
                if (attacker == null) return false;
                if (GameManager.PlayingSoloMode) return false;
                if (GameManager.IsBoardItemTeamFriendlyFireEnabled) return false;

                BoardPlayer bp = victim as BoardPlayer;
                if (bp == null || bp.GamePlayer == null || bp.GamePlayer.GameTeam == null) return false;
                if (bp.GamePlayer == attacker) return false;

                return bp.GamePlayer.GameTeam.IsInTeam(attacker);
            }
            catch
            {
                return false;
            }
        }
    }
}
