using System;
using System.IO;
using System.Text;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PummelCustomItems.Core), "PummelCustomItems", PummelCustomItems.Core.Version, "local")]
[assembly: MelonGame("Rebuilt Games", "Pummel Party")]

namespace PummelCustomItems
{
    public class Core : MelonMod
    {
        /// <summary>Also part of the network version tag - see Patch_GameVersion.</summary>
        internal const string Version = "0.53.1";

        internal static Core Instance;

        public override void OnInitializeMelon()
        {
            Instance = this;
            ModLog.Open();
            HeldPoses.ReloadFile();
            LoggerInstance.Msg("=== PummelCustomItems loaded ===");
            LoggerInstance.Msg("Unity: " + Application.unityVersion);
            LoggerInstance.Msg("Game dir: " + Directory.GetCurrentDirectory());
            LoggerInstance.Msg("Debug keys: " + DebugKeys.Help);

            // The game's own Debug.LogError output goes to Unity's Player.log, not here.
            // Mirror errors into the Melon log so a broken board action is visible at once.
            Application.logMessageReceived += OnUnityLog;
        }

        public override void OnDeinitializeMelon()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        private static string s_lastUnityError;
        private static int s_repeatCount;

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (Instance == null) return;

            // DoAction() can throw every single frame - collapse the spam.
            if (condition == s_lastUnityError)
            {
                s_repeatCount++;
                if (s_repeatCount == 10)
                    Instance.LoggerInstance.Warning("  (repeating every frame, further copies suppressed)");
                if (s_repeatCount >= 10) return;
            }
            else
            {
                s_lastUnityError = condition;
                s_repeatCount = 0;
            }

            Warn("[UNITY " + type + "] " + condition);
            if (!string.IsNullOrEmpty(stackTrace))
            {
                string[] lines = stackTrace.Split('\n');
                for (int i = 0; i < lines.Length && i < 6; i++)
                {
                    if (lines[i].Trim().Length > 0)
                        Warn("    " + lines[i].TrimEnd());
                }
            }
        }

        public override void OnUpdate()
        {
            DebugKeys.Update();
            PoseStudio.Update();
        }

        internal static void Log(string msg)
        {
            if (Instance != null) Instance.LoggerInstance.Msg(msg);
            ModLog.Write("", msg);
        }

        internal static void Warn(string msg)
        {
            if (Instance != null) Instance.LoggerInstance.Warning(msg);
            ModLog.Write("WARN ", msg);
        }
    }

    /// <summary>
    /// The mod's own log, one file per session in UserData/PummelCustomItems/logs.
    ///
    /// MelonLoader's log cannot be relied on to still exist after the next launch. It keeps
    /// ten and deletes the "oldest" by FILE NAME - and its names have no leading zeros, so
    /// from October "26-10-1_..." sorts before "26-8-20_...", and every launch deleted the
    /// newest previous log while the August ones stayed. That is how the one log that would
    /// have explained the grenade timing was lost.
    ///
    /// Here the name is a zero-padded timestamp, so name order and time order agree, old
    /// files are pruned by their write time anyway, and every line is flushed as it is
    /// written so a crash does not take the end of the log with it.
    /// </summary>
    internal static class ModLog
    {
        private const int Keep = 40;
        private static StreamWriter s_writer;

        internal static void Open()
        {
            try
            {
                string dir = Path.Combine(Path.Combine(Path.Combine(
                    Directory.GetCurrentDirectory(), "UserData"), "PummelCustomItems"), "logs");
                Directory.CreateDirectory(dir);
                Prune(dir);

                string file = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                s_writer = new StreamWriter(file, false, new UTF8Encoding(false));
                s_writer.AutoFlush = true;
                s_writer.WriteLine("PummelItems " + Core.Version + ", started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception e)
            {
                s_writer = null;
                if (Core.Instance != null) Core.Instance.LoggerInstance.Warning("own log unavailable: " + e.Message);
            }
        }

        internal static void Write(string level, string msg)
        {
            if (s_writer == null) return;
            try { s_writer.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + level + msg); }
            catch { }
        }

        private static void Prune(string dir)
        {
            FileInfo[] files = new DirectoryInfo(dir).GetFiles("*.log");
            if (files.Length < Keep) return;

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = Keep - 1; i < files.Length; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }
    }

    // ItemList.Setup() runs when the board builds its enabled-item table.
    // Best place to see the real, fully-loaded item data.
    [HarmonyPatch(typeof(ItemList), "Setup")]
    internal static class Patch_ItemList_Setup
    {
        private static bool s_dumped;

        private static void Postfix(ItemList __instance)
        {
            try
            {
                Core.Log("ItemList.Setup() ran. items=" + Len(__instance.items) +
                         " enabled=" + Len(__instance.enabledItems));

                if (s_dumped) return;
                s_dumped = true;

                var sb = new StringBuilder();
                sb.AppendLine("# Pummel Party item dump");
                sb.AppendLine("# generated by PummelCustomItems 0.1.0");
                sb.AppendLine();

                sb.AppendLine("## ItemList.items (" + Len(__instance.items) + ")");
                DumpArray(sb, __instance.items);

                sb.AppendLine();
                sb.AppendLine("## ItemList.enabledItems (" + Len(__instance.enabledItems) + ")");
                DumpArray(sb, __instance.enabledItems);

                sb.AppendLine();
                sb.AppendLine("## fallbackItem");
                DumpOne(sb, __instance.fallbackItem);

                string path = Path.Combine(Directory.GetCurrentDirectory(), "item_dump.txt");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Core.Log("Item dump written to " + path);
            }
            catch (Exception e)
            {
                Core.Warn("dump failed: " + e);
            }
        }

        private static int Len(Array a)
        {
            return a == null ? -1 : a.Length;
        }

        private static void DumpArray(StringBuilder sb, ItemDetails[] arr)
        {
            if (arr == null) { sb.AppendLine("(null)"); return; }
            for (int i = 0; i < arr.Length; i++)
            {
                sb.AppendLine("--- [" + i + "] ---");
                DumpOne(sb, arr[i]);
            }
        }

        private static void DumpOne(StringBuilder sb, ItemDetails d)
        {
            if (d == null) { sb.AppendLine("(null)"); return; }

            sb.AppendLine("  enumReference   : " + d.enumReference);
            sb.AppendLine("  itemID          : " + d.itemID);
            sb.AppendLine("  itemIndex       : " + d.itemIndex);
            sb.AppendLine("  enabled         : " + d.enabled);
            sb.AppendLine("  isActive        : " + SafeIsActive(d));
            sb.AppendLine("  weaponSpaceItem : " + d.weaponSpaceItem);
            sb.AppendLine("  skipTurnAfterUse: " + d.skipTurnAfterUse);
            sb.AppendLine("  nameToken       : " + d.itemNameToken + "  (\"" + d.TranslatedName + "\")");
            sb.AppendLine("  descToken       : " + d.descriptionToken);
            sb.AppendLine("  netPrefabName   : " + d.netPrefabName);
            sb.AppendLine("  prefabPath      : " + d.prefabPath);
            sb.AppendLine("  prefab          : " + NameOf(d.prefab) + ItemComponentOf(d.prefab));
            sb.AppendLine("  recievePrefab   : " + NameOf(d.recievePrefab) + "  path=" + d.recievePrefabPath);
            sb.AppendLine("  heldPrefab      : " + NameOf(d.heldPrefab) + "  path=" + d.heldPrefabPath);
            sb.AppendLine("  heldBone        : " + d.heldBone);
            sb.AppendLine("  heldPos/Rot/Scl : " + d.heldPosition + " / " + d.heldRotation + " / " + d.heldScale);
            sb.AppendLine("  icon            : " + NameOf(d.icon) + "  tex=" + NameOf(d.iconTexture));
            sb.AppendLine("  isWorkshop      : " + d.isWorkshop + "  publishedFileId=" + d.publishedFileId);
        }

        private static string SafeIsActive(ItemDetails d)
        {
            try { return d.GetIsActive().ToString(); }
            catch (Exception e) { return "<error: " + e.GetType().Name + ">"; }
        }

        private static string NameOf(UnityEngine.Object o)
        {
            return o == null ? "(null)" : o.name;
        }

        // Which Item subclass actually drives this item?
        private static string ItemComponentOf(GameObject prefab)
        {
            if (prefab == null) return "";
            try
            {
                Item it = prefab.GetComponentInChildren<Item>(true);
                return it == null ? "  [no Item component]" : "  [Item class: " + it.GetType().Name + "]";
            }
            catch (Exception e)
            {
                return "  [component lookup failed: " + e.GetType().Name + "]";
            }
        }
    }
}
