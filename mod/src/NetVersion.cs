using System;
using HarmonyLib;
using I2.Loc;
using TMPro;

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
    /// on.
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

    /// <summary>
    /// The version label in the main menu's corner. The game prints GameManager.VERSION there,
    /// and with the tag the line grew long enough to run off the edge of the Steam Deck's
    /// screen. So the label keeps the game's own version on its line, as long as it always
    /// was, and the mod goes underneath, small, in place of the build stamp. It is there
    /// exactly when the mod is loaded, which makes it a truthful way of telling the two
    /// modes apart.
    /// </summary>
    [HarmonyPatch(typeof(GetGameVersion), "UpdateVersionText")]
    internal static class Patch_GetGameVersion
    {
        private static bool Prefix(GetGameVersion __instance)
        {
            TextMeshProUGUI text;
            if (!__instance.TryGetComponent(out text)) return true;

            string stock;
            Patch_GameVersion.Suppress = true;
            try { stock = GameManager.VERSION; }
            finally { Patch_GameVersion.Suppress = false; }

            text.text = string.Format("{0} {1}\n<size=12>PummelItems {2}</size>",
                                      LocalizationManager.GetTranslation("Version"), stock, Core.Version);
            Core.Log("version label: " + text.text.Replace("\n", " | "));
            return false;
        }
    }
}
