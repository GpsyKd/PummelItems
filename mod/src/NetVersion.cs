using System;
using HarmonyLib;

namespace PummelCustomItems
{
    /// <summary>
    /// Adds the mod and its version to the game's version string, so the game's own
    /// compatibility check covers the mod too.
    ///
    /// A joining client sends GameManager.VERSION and the host turns it away if it differs
    /// from its own (NetGameServer, deny code 14). With the tag appended:
    /// <list type="bullet">
    /// <item>players with the same mod version play together as normal;</item>
    /// <item>a different mod version, or no mod at all, is refused with the game's ordinary
    ///   "version mismatch" message - instead of joining and falling apart, because item ids and
    ///   network layouts differ between versions;</item>
    /// <item>the lobby browser and Steam's lobby search, which filter on the same string, only
    ///   offer games that would work.</item>
    /// </list>
    /// It also makes it impossible to wander into a stranger's online game with the mod still
    /// on. And the version label in the menu corner reads "...+PummelItems" exactly when the
    /// mod is loaded, which is a truthful way of telling which mode the game is in.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "VERSION", MethodType.Getter)]
    internal static class Patch_GameVersion
    {
        internal const string Tag = "+PummelItems." + Core.Version;

        // See Patch_Settings_Setup.
        internal static bool Suppress;

        private static void Postfix(ref string __result)
        {
            if (Suppress || string.IsNullOrEmpty(__result)) return;
            if (!__result.EndsWith(Tag, StringComparison.Ordinal)) __result += Tag;
        }
    }

    /// <summary>
    /// Settings.Setup remembers the last version it ran under and, when that changes, resets
    /// a saved lobby setting. With the tag, every switch between modded and stock would count
    /// as a new version and reset it, so for this one check the game sees its own version.
    /// </summary>
    [HarmonyPatch(typeof(global::Settings), "Setup")]
    internal static class Patch_Settings_Setup
    {
        private static void Prefix()
        {
            Patch_GameVersion.Suppress = true;
        }

        // A finalizer runs even if Setup throws, so the tag can never stay switched off.
        private static Exception Finalizer(Exception __exception)
        {
            Patch_GameVersion.Suppress = false;
            return __exception;
        }
    }
}
