using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;

namespace CommandQueue
{
    public static class TierInfo
    {
        // Internal tier names don't match what players call them, e.g. DLC4's "Hallowed" items are ItemTier.Cursed.
        private static readonly Dictionary<ItemTier, string> displayNameOverrides = new Dictionary<ItemTier, string>
        {
            { ItemTier.Tier1, "White" },
            { ItemTier.Tier2, "Green" },
            { ItemTier.Tier3, "Red" },
            { ItemTier.Boss, "Boss" },
            { ItemTier.Lunar, "Lunar" },
            { ItemTier.VoidTier1, "Void White" },
            { ItemTier.VoidTier2, "Void Green" },
            { ItemTier.VoidTier3, "Void Red" },
            { ItemTier.VoidBoss, "Void Boss" },
            { ItemTier.FoodTier, "Food" },
            { ItemTier.Cursed, "Hallowed" },
            { ItemTier.Page, "Page" },
            { ItemTier.Hex, "Hex" },
        };

        public static string GetDisplayName(ItemTier tier)
        {
            if (displayNameOverrides.TryGetValue(tier, out var name))
                return name;

            name = ModConfig.GetTierName(tier);
            if (name.EndsWith("TierDef") && name.Length > "TierDef".Length)
                return name.Substring(0, name.Length - "TierDef".Length);
            if (name.EndsWith("Def") && name.Length > "Def".Length)
                return name.Substring(0, name.Length - "Def".Length);
            return name;
        }

        // The tier a void tier corrupts items of, null for every other tier.
        public static ItemTier? GetMirroredTier(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.VoidTier1: return ItemTier.Tier1;
                case ItemTier.VoidTier2: return ItemTier.Tier2;
                case ItemTier.VoidTier3: return ItemTier.Tier3;
                case ItemTier.VoidBoss: return ItemTier.Boss;
                default: return null;
            }
        }

        public static Color GetColor(ItemTier tier)
        {
            var tierDef = ItemTierCatalog.GetItemTierDef(tier);
            return tierDef ? (Color)ColorCatalog.GetColor(tierDef.colorIndex) : Color.white;
        }

        private static readonly Dictionary<ItemTier, PickupIndex[]> pickupCache = new Dictionary<ItemTier, PickupIndex[]>();

        // Catalogs don't change during a run; clear at run start in case content changed between runs.
        internal static void Init()
        {
            Run.onRunStartGlobal += _ => pickupCache.Clear();
        }

        // Every pickup a command essence of this tier can offer. Built from the game's transmutation
        // groups so it matches the real command menu, and merges all groups of the tier, so Boss
        // includes the separate power shape group.
        public static PickupIndex[] GetPickups(ItemTier tier)
        {
            if (!pickupCache.TryGetValue(tier, out var pickups))
                pickupCache[tier] = pickups = BuildPickups(tier);
            return pickups;
        }

        private static PickupIndex[] BuildPickups(ItemTier tier)
        {
            var result = new List<PickupIndex>();
            var seen = new HashSet<PickupIndex>();
            var seenGroups = new HashSet<PickupIndex[]>();

            foreach (var itemDef in ItemCatalog.allItemDefs)
            {
                if (!itemDef || itemDef.tier != tier)
                    continue;

                var group = PickupTransmutationManager.GetGroupFromPickupIndex(PickupCatalog.FindPickupIndex(itemDef.itemIndex));
                if (group == null || !seenGroups.Add(group))
                    continue;

                foreach (var pickupIndex in group)
                {
                    if (TierOf(pickupIndex) == tier && seen.Add(pickupIndex))
                        result.Add(pickupIndex);
                }
            }

            return result.ToArray();
        }

        public static bool IsAvailable(PickupIndex pickupIndex)
        {
            return !Run.instance || Run.instance.IsPickupAvailable(pickupIndex);
        }

        private static ItemTier? TierOf(PickupIndex pickupIndex)
        {
            return QueueManager.TryGetTier(pickupIndex, out var tier) ? tier : (ItemTier?)null;
        }

        public static bool AnyAvailable(IEnumerable<PickupIndex> pickups) => pickups.Any(IsAvailable);
    }
}
