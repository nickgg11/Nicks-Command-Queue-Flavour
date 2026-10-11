using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace CommandQueue
{
    public static class QueueManager
    {
        public struct QueueEntry
        {
            public PickupIndex pickupIndex;
            public int count;
        }

        public enum QueueChange
        {
            Removed, Added, Changed, Moved,
            // The whole queue was replaced (cleared or loaded), listeners should rebuild from scratch.
            Reset
        }

        public static event Action<Run> OnRunQueueInit;
        public static event Action<QueueChange, ItemTier, int> OnQueueChanged;

        public static Dictionary<ItemTier, List<QueueEntry>> mainQueues = new Dictionary<ItemTier, List<QueueEntry>>
        {
            { ItemTier.Tier1, new List<QueueEntry>() },
            { ItemTier.Tier2, new List<QueueEntry>() },
            { ItemTier.Tier3, new List<QueueEntry>() }
        };

        public static Dictionary<ItemTier, bool> queueRepeat = new Dictionary<ItemTier, bool>();

        internal static void Enable()
        {
            Run.onRunStartGlobal += InitQueues;
            ModConfig.OnReady += UpdateQueueAvailability;
            UpdateQueueAvailability();
        }

        internal static void Disable()
        {
            Run.onRunStartGlobal -= InitQueues;
            ModConfig.OnReady -= UpdateQueueAvailability;
        }

        public static void UpdateQueueAvailability()
        {
            if (!ModConfig.Ready)
                return;

            var enabledTabs = ModConfig.enabledTabs.Value;
            foreach (var key in mainQueues.Keys.ToArray())
            {
                if (!enabledTabs.Contains(key))
                    mainQueues.Remove(key);
            }
            foreach(ItemTier tier in enabledTabs)
            {
                if (!mainQueues.ContainsKey(tier))
                    mainQueues.Add(tier, new List<QueueEntry>());
            }
        }

        private static void InitQueues(Run run)
        {
            ClearAllQueues();

            OnRunQueueInit?.Invoke(run);
        }

        public static bool DoesRepeat(ItemTier tier)
        {
            return queueRepeat.TryGetValue(tier, out var value) && value;
        }

        public static void ToggleRepeat(ItemTier tier)
        {
            queueRepeat[tier] = !DoesRepeat(tier);
        }

        public static bool TryGetTier(PickupIndex pickupIndex, out ItemTier tier)
        {
            tier = ItemTier.NoTier;
            var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null)
                return false;
            var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            if (!itemDef)
                return false;
            tier = itemDef.tier;
            return true;
        }

        public static void Enqueue(PickupIndex pickupIndex)
        {
            TryEnqueue(pickupIndex, 1);
        }

        // Returns false (and changes nothing) if the pickup isn't an item or its tier has no queue.
        public static bool TryEnqueue(PickupIndex pickupIndex, int count)
        {
            if (count <= 0 || !TryGetTier(pickupIndex, out var tier) || !mainQueues.TryGetValue(tier, out var queue))
                return false;

            if (queue.Count > 0)
            {
                QueueEntry lastElement = queue[queue.Count - 1];
                if(lastElement.pickupIndex == pickupIndex)
                {
                    lastElement.count += count;
                    queue[queue.Count - 1] = lastElement;
                    OnQueueChanged?.Invoke(QueueChange.Changed, tier, queue.Count-1);
                    return true;
                }
            }

            QueueEntry newElement = new QueueEntry
            {
                pickupIndex = pickupIndex,
                count = count
            };

            queue.Add(newElement);
            OnQueueChanged?.Invoke(QueueChange.Added, tier, queue.Count-1);
            return true;
        }

        public static PickupIndex Peek(ItemTier tier)
        {
            if (!mainQueues.TryGetValue(tier, out var queue) || queue.Count == 0)
                return PickupIndex.none;
            return queue[0].pickupIndex;
        }

        public static PickupIndex Pop(ItemTier tier)
        {
            var index = Remove(tier, 0);
            if(DoesRepeat(tier))
                Enqueue(index);
            return index;
        }

        public static PickupIndex Remove(ItemTier tier, int index, int count = 1)
        {
            var queue = mainQueues[tier];
            var entry = queue[index];
            entry.count -= count;
            if(entry.count <= 0)
            {
                queue.RemoveAt(index);
                OnQueueChanged?.Invoke(QueueChange.Removed, tier, index);

                if (TryMerge(tier, index - 1, index))
                {
                    OnQueueChanged?.Invoke(QueueChange.Removed, tier, index);
                    OnQueueChanged?.Invoke(QueueChange.Changed, tier, index-1);
                }
            }
            else
            {
                queue[index] = entry;
                OnQueueChanged?.Invoke(QueueChange.Changed, tier, index);
            }
            return entry.pickupIndex;
        }

        private static bool TryMerge(ItemTier tier, int into, int from)
        {
            var queue = mainQueues[tier];

            if (into < 0 || into >= queue.Count || from < 0 || from >= queue.Count) return false;

            var entry1 = queue[into];
            var entry2 = queue[from];

            if (entry1.pickupIndex != entry2.pickupIndex) return false;

            entry1.count += entry2.count;

            queue[into] = entry1;
            queue.RemoveAt(from);
            return true;
        }

        // Lists are cleared in place: QueueDisplay keeps a reference to its tier's list, so replacing
        // the lists would leave the UI reading a stale queue.
        public static void ClearAllQueues() {
            UpdateQueueAvailability();
            foreach (var pair in mainQueues)
            {
                pair.Value.Clear();
                OnQueueChanged?.Invoke(QueueChange.Reset, pair.Key, -1);
            }
        }

        public static void Move(ItemTier tier, int oldIndex, int newIndex, int count = 1)
        {
            var queue = mainQueues[tier];

            newIndex = Math.Max(0, Math.Min(queue.Count, newIndex));

            if (newIndex == oldIndex)
                return;

            var entry = queue[oldIndex];

            if (entry.count < count) return;

            if (entry.count == count)
            {
                queue.RemoveAt(oldIndex);

                if (newIndex > oldIndex)
                    newIndex--;
            }
            else
            {
                var _entry = queue[oldIndex];
                _entry.count -= count;
                queue[oldIndex] = _entry;
            }

            entry.count = count;

            if (TryMerge(tier, oldIndex - 1, oldIndex) && newIndex > oldIndex) newIndex--;

            queue.Insert(newIndex, entry);
            TryMerge(tier, newIndex - 1, newIndex);
            TryMerge(tier, newIndex, newIndex + 1);

            // Do a full update instead of up to 6 smaller updates.
            OnQueueChanged?.Invoke(QueueChange.Moved, tier, -1);
        }

        public static IEnumerable<(ItemTier, PickupIndex)> PeekAll()
        {
            return mainQueues.Where(entry => entry.Value.Count > 0)
                .Select(entry => (entry.Key, entry.Value.First().pickupIndex));
        }
    }
}
