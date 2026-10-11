using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using RoR2;
using UnityEngine;
using Path = System.IO.Path;

namespace CommandQueue
{
    public static class SaveManager
    {
        [Serializable]
        public class SavedEntry
        {
            // PickupDef.internalName, e.g. "ItemIndex.Hoof". Stable across sessions, unlike PickupIndex values.
            public string pickup;
            public int count;
        }

        [Serializable]
        public class SavedQueue
        {
            public string id;
            public string name;
            public long createdAt;
            public long updatedAt;
            public List<SavedEntry> entries = new List<SavedEntry>();
            public List<string> repeatTiers = new List<string>();

            public int ItemCount => entries.Sum(e => Math.Max(0, e.count));
        }

        [Serializable]
        private class SaveFileData
        {
            public int version = 1;
            public bool importedLegacySlots;
            public bool importedCommandQueueExtended;
            public List<SavedQueue> saves = new List<SavedQueue>();
        }

        public struct LoadResult
        {
            public int loadedItems;
            public int unknownItems;
            public int disabledTierItems;
        }

        public const int MaxStackCount = 999;
        public const int MaxNameLength = 40;

        public static event Action OnSavesChanged;

        private static SaveFileData data;

        private static string SaveFilePath => Path.Combine(Paths.ConfigPath, "CommandQueue.saves.json");

        public static IReadOnlyList<SavedQueue> Saves
        {
            get
            {
                EnsureLoaded();
                return data.saves;
            }
        }

        private static void EnsureLoaded()
        {
            if (data != null)
                return;

            data = ReadSaveFile() ?? new SaveFileData();

            bool changed = false;
            if (!data.importedLegacySlots)
            {
                ImportLegacySlots();
                data.importedLegacySlots = true;
                changed = true;
            }
            if (!data.importedCommandQueueExtended)
            {
                ImportCommandQueueExtended();
                data.importedCommandQueueExtended = true;
                changed = true;
            }
            if (changed)
                WriteSaveFile();
        }

        private static SaveFileData ReadSaveFile()
        {
            var path = SaveFilePath;
            if (!File.Exists(path))
                return null;

            try
            {
                var parsed = JsonUtility.FromJson<SaveFileData>(File.ReadAllText(path));
                if (parsed?.saves == null)
                    throw new FormatException("missing saves list");
                parsed.saves.RemoveAll(s => s == null);
                foreach (var save in parsed.saves)
                {
                    if (string.IsNullOrEmpty(save.id)) save.id = NewId();
                    if (save.entries == null) save.entries = new List<SavedEntry>();
                    if (save.repeatTiers == null) save.repeatTiers = new List<string>();
                }
                return parsed;
            }
            catch (Exception e)
            {
                // Keep the unreadable file around instead of overwriting the player's saves with an empty list.
                var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                Debug.LogError($"[CommandQueue] Could not read {path} ({e.Message}). Moving it to {backup} and starting with no saves.");
                try { File.Move(path, backup); } catch (Exception moveError) { Debug.LogError(moveError); }
                return null;
            }
        }

        private static void WriteSaveFile()
        {
            var path = SaveFilePath;
            var tempPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
                // Write-then-swap, so a crash mid-write can't leave a truncated save file.
                if (!File.Exists(path))
                    File.Move(tempPath, path);
                else
                {
                    try
                    {
                        File.Replace(tempPath, path, path + ".bak");
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(path, path + ".bak", true);
                        File.Delete(path);
                        File.Move(tempPath, path);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandQueue] Failed to write saves to {path}: {e}");
            }
            OnSavesChanged?.Invoke();
        }

        private static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 8);
        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static string SanitizeName(string name)
        {
            name = (name ?? "").Trim();
            name = new string(name.Where(c => !char.IsControl(c)).ToArray());
            if (name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            return name;
        }

        private static List<SavedEntry> CaptureCurrentQueues(out List<string> repeatTiers)
        {
            repeatTiers = QueueManager.mainQueues.Keys
                .Where(QueueManager.DoesRepeat)
                .Select(ModConfig.GetTierName)
                .ToList();

            return QueueManager.mainQueues
                .SelectMany(pair => pair.Value)
                .Select(entry => new SavedEntry { pickup = entry.pickupIndex.ToString(), count = entry.count })
                .ToList();
        }

        public static SavedQueue CreateSave(string name)
        {
            EnsureLoaded();
            name = SanitizeName(name);
            if (name.Length == 0)
                name = $"Save {data.saves.Count + 1}";

            var save = new SavedQueue
            {
                id = NewId(),
                name = name,
                createdAt = Now(),
                updatedAt = Now(),
                entries = CaptureCurrentQueues(out var repeatTiers),
                repeatTiers = repeatTiers
            };
            data.saves.Insert(0, save);
            WriteSaveFile();
            return save;
        }

        public static void OverwriteSave(string id)
        {
            var save = Find(id);
            if (save == null)
                return;
            save.entries = CaptureCurrentQueues(out var repeatTiers);
            save.repeatTiers = repeatTiers;
            save.updatedAt = Now();
            WriteSaveFile();
        }

        public static void RenameSave(string id, string newName)
        {
            var save = Find(id);
            newName = SanitizeName(newName);
            if (save == null || newName.Length == 0)
                return;
            save.name = newName;
            WriteSaveFile();
        }

        public static void DeleteSave(string id)
        {
            EnsureLoaded();
            if (data.saves.RemoveAll(s => s.id == id) > 0)
                WriteSaveFile();
        }

        public static SavedQueue Find(string id)
        {
            EnsureLoaded();
            return data.saves.FirstOrDefault(s => s.id == id);
        }

        // Validates the whole save before touching the queues, so a broken or outdated save
        // (removed mods, renamed items, disabled tiers) loads what it can instead of wiping the queue
        // and stopping at the first bad entry.
        public static LoadResult LoadSave(string id)
        {
            var result = new LoadResult();
            var save = Find(id);
            if (save == null)
                return result;

            var resolved = new List<(PickupIndex pickup, int count)>();
            foreach (var entry in save.entries)
            {
                int count = Mathf.Clamp(entry.count, 0, MaxStackCount);
                if (count == 0)
                    continue;

                var pickupIndex = string.IsNullOrEmpty(entry.pickup) ? PickupIndex.none : PickupCatalog.FindPickupIndex(entry.pickup);
                if (!QueueManager.TryGetTier(pickupIndex, out var tier))
                {
                    result.unknownItems += count;
                    continue;
                }
                if (!QueueManager.mainQueues.ContainsKey(tier))
                {
                    result.disabledTierItems += count;
                    continue;
                }
                resolved.Add((pickupIndex, count));
            }

            // Nothing in a non-empty save could be resolved: leave the current queue alone.
            if (resolved.Count == 0 && save.entries.Count > 0)
                return result;

            QueueManager.ClearAllQueues();
            foreach (var (pickup, count) in resolved)
            {
                if (QueueManager.TryEnqueue(pickup, count))
                    result.loadedItems += count;
            }

            QueueManager.queueRepeat.Clear();
            foreach (var tierName in save.repeatTiers)
            {
                var tier = ModConfig.ItemTierSet.Deserialize(tierName);
                foreach (var t in tier)
                    QueueManager.queueRepeat[t] = true;
            }

            if (result.unknownItems > 0 || result.disabledTierItems > 0)
                Debug.LogWarning($"[CommandQueue] Loaded save '{save.name}' partially: {result.unknownItems} unknown item(s), {result.disabledTierItems} item(s) in disabled tiers were skipped.");

            return result;
        }

        // Legacy format: "ItemIndex.Hoof*3,ItemIndex.Syringe*1,"
        private static List<SavedEntry> ParseLegacyQueue(string queueData)
        {
            var entries = new List<SavedEntry>();
            if (string.IsNullOrEmpty(queueData))
                return entries;

            foreach (var raw in queueData.Split(','))
            {
                var part = raw.Trim();
                int star = part.LastIndexOf('*');
                if (star <= 0)
                    continue;
                if (!int.TryParse(part.Substring(star + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
                    continue;
                entries.Add(new SavedEntry { pickup = part.Substring(0, star), count = Math.Min(count, MaxStackCount) });
            }
            return entries;
        }

        // Pre-2.0 builds of this mod wrote commandQueueSlot_N.save into the game's working directory.
        private static void ImportLegacySlots()
        {
            var directories = new[] { Directory.GetCurrentDirectory(), Paths.GameRootPath }.Distinct();
            foreach (var directory in directories)
            {
                for (int slot = 1; slot <= 10; slot++)
                {
                    var path = Path.Combine(directory, $"commandQueueSlot_{slot}.save");
                    try
                    {
                        if (!File.Exists(path))
                            continue;
                        var entries = ParseLegacyQueue(File.ReadAllText(path).Trim());
                        if (entries.Count == 0)
                            continue;
                        data.saves.Add(new SavedQueue
                        {
                            id = NewId(),
                            name = $"Slot {slot}",
                            createdAt = Now(),
                            updatedAt = Now(),
                            entries = entries
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CommandQueue] Could not import {path}: {e.Message}");
                    }
                }
            }
        }

        // CommandQueueExtended stores "id|name|queueData|createdAt" lines. Names may contain '|', so
        // the fixed fields are taken from both ends.
        private static void ImportCommandQueueExtended()
        {
            var path = Path.Combine(Paths.ConfigPath, "commandQueueSaves.json");
            try
            {
                if (!File.Exists(path))
                    return;
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('|');
                    if (parts.Length < 4)
                        continue;
                    var entries = ParseLegacyQueue(parts[parts.Length - 2]);
                    if (entries.Count == 0)
                        continue;
                    long.TryParse(parts[parts.Length - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long createdAt);
                    var name = SanitizeName(string.Join("|", parts, 1, parts.Length - 3));
                    data.saves.Add(new SavedQueue
                    {
                        id = NewId(),
                        name = name.Length > 0 ? name : "Imported",
                        createdAt = createdAt,
                        updatedAt = createdAt,
                        entries = entries
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CommandQueue] Could not import CommandQueueExtended saves from {path}: {e.Message}");
            }
        }
    }
}
