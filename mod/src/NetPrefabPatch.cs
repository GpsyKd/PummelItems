using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using ZP.Net;

namespace PummelCustomItems
{
    /// <summary>
    /// NetObjectManager.GetNetPrefab() resolves a spawn request into a prefab. For runtime
    /// prefabs (the ones SetupNetPrefabs() registers from prefabRoot) it cannot work:
    /// SetupNetPrefabs never fills NetPrefabDefinition.resource_location, so the method ends
    /// up calling Addressables.LoadAssetAsync(null) which throws
    ///
    ///     ArgumentNullException: Value cannot be null. Parameter name: key
    ///
    /// *before* reaching its own prefabRoot.FindObject(name) fallback. The exception escapes
    /// into GameBoardController.DoAction(), the board action queue never advances and the
    /// game locks up.
    ///
    /// So we answer the lookup ourselves for prefabs we planted, and let everything else
    /// go through untouched.
    /// </summary>
    [HarmonyPatch(typeof(NetObjectManager), "GetNetPrefab")]
    internal static class Patch_GetNetPrefab
    {
        private static FieldInfo s_mapField;

        private static bool Prefix(NetObjectManager __instance, string name, ref NetPrefab __result)
        {
            if (string.IsNullOrEmpty(name)) return true;

            GameObject go = ItemRegistry.GetPlantedPrefab(name);
            if (go == null) return true;          // not ours - run the original

            try
            {
                __result = new NetPrefab(go, PrefabIdOf(__instance, name),
                                         default(AsyncOperationHandle<GameObject>));
                return false;                     // skip the original
            }
            catch (Exception e)
            {
                Core.Warn("GetNetPrefab override failed for '" + name + "': " + e);
                return true;
            }
        }

        /// <summary>
        /// SetupNetPrefabs() already assigned an id when it scanned prefabRoot; reuse it so
        /// host and clients agree on the wire.
        /// </summary>
        private static ushort PrefabIdOf(NetObjectManager mgr, string name)
        {
            if (s_mapField == null)
            {
                s_mapField = typeof(NetObjectManager).GetField(
                    "net_prefab_map", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (s_mapField == null) return 0;

            IDictionary map = s_mapField.GetValue(mgr) as IDictionary;
            if (map == null || !map.Contains(name))
            {
                Core.Warn("prefab '" + name + "' is not in net_prefab_map - " +
                          "SetupNetPrefabs() may not have run yet");
                return 0;
            }

            NetPrefabDefinition def = map[name] as NetPrefabDefinition;
            if (def == null || def.prefab_id < 0) return 0;
            return (ushort)def.prefab_id;
        }
    }
}
