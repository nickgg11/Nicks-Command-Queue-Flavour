using System;
using System.Collections.Generic;
using RoR2;
using System.Linq;
using BepInEx.Configuration;

namespace CommandQueue
{
    public static class ModConfig
    {
        public static bool Ready = false;
        public static event Action OnReady;

        private static ConfigFile config;

        internal static void InitConfig(ConfigFile _config)
        {
            config = _config; // No config available at the moment

            TomlTypeConverter.AddConverter(typeof(ItemTierSet), new TypeConverter
            {
                ConvertToObject = (str, type) => ItemTierSet.Deserialize(str),
                ConvertToString = (obj, type) => obj.ToString()
            });

            ItemTierCatalog.availability.CallWhenAvailable(InitConfigInternal);
        }

        internal static void InitConfigInternal()
        {
            if (Ready)
                return;
            ItemTierSet.Initialize();

            enabledTabs = config.Bind(new ConfigDefinition("General", "EnabledQueues"), new ItemTierSet(DefaultTiers), new ConfigDescription($"Which item tiers should have queues?\nValid values: {string.Join(", ", ItemTierCatalog.allItemTierDefs.Select(def => $"{GetTierName(def.tier)} ({TierInfo.GetDisplayName(def.tier)})"))}"));
            bigItemButtonContainer = config.Bind(new ConfigDefinition("General", "BigItemSelectionContainer"), true, new ConfigDescription("false: Default command button layout\ntrue: Increase the space for buttons, helps avoid overflow with modded items"));
            bigItemButtonScale = config.Bind(new ConfigDefinition("General", "BigItemSelectionScale"), 1f, new ConfigDescription("Scale applied to item buttons in the menu - decrease it if your buttons don't fit\nApplies only if BigItemSelectionContainer is true"));
            rightClickRemovesStack = config.Bind(new ConfigDefinition("General", "RightClickRemovesStack"), true, new ConfigDescription("Should right-clicking an item in the queue remove the whole stack?"));
            pauseWhileOpen = config.Bind(new ConfigDefinition("General", "PauseWhileQueueOpen"), true, new ConfigDescription("Pause the game while the Command Queue or Saves page is open. Single player only, like the game's inspect auto-pause.\nTip: the game's \"Toggle Inventory\" control setting keeps the scoreboard open without holding the key."));
            matchItemBarWidth = config.Bind(new ConfigDefinition("General", "MatchItemBarWidth"), true, new ConfigDescription("Narrow the scoreboard (and the Command Queue and Saves pages) to the width and position of the item bar at the top of the screen, so it doesn't overlap the HUD on the sides."));
            showQueuePreview = config.Bind(new ConfigDefinition("QueuePreview", "Enabled"), true, new ConfigDescription("Show the next queued item of each tier on the HUD during Command runs, under the objectives panel."));
            queuePreviewX = config.Bind(new ConfigDefinition("QueuePreview", "PositionX"), 0.985f, new ConfigDescription("Only used when the preview can't attach under the objectives panel (other game modes, HUD mods). Horizontal position, 0 = left edge, 1 = right edge.", new AcceptableValueRange<float>(0f, 1f)));
            queuePreviewY = config.Bind(new ConfigDefinition("QueuePreview", "PositionY"), 0.6f, new ConfigDescription("Only used when the preview can't attach under the objectives panel (other game modes, HUD mods). Vertical position, 0 = bottom, 1 = top.", new AcceptableValueRange<float>(0f, 1f)));
            configVersion = config.Bind(new ConfigDefinition("Internal", "ConfigVersion"), 0, new ConfigDescription("Used to migrate settings between mod versions, don't edit."));

            MigrateConfig();

            Ready = true;
            OnReady?.Invoke();
        }

        public static ConfigEntry<ItemTierSet> enabledTabs;
        public static ConfigEntry<bool> bigItemButtonContainer;
        public static ConfigEntry<float> bigItemButtonScale;
        public static ConfigEntry<bool> rightClickRemovesStack;
        public static ConfigEntry<bool> pauseWhileOpen;
        public static ConfigEntry<bool> matchItemBarWidth;
        public static ConfigEntry<bool> showQueuePreview;
        public static ConfigEntry<float> queuePreviewX;
        public static ConfigEntry<float> queuePreviewY;
        private static ConfigEntry<int> configVersion;

        private const int CurrentConfigVersion = 3;

        private static readonly ItemTier[] DefaultTiers =
        {
            ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Boss, ItemTier.Lunar,
            ItemTier.VoidTier1, ItemTier.VoidTier2, ItemTier.VoidTier3, ItemTier.VoidBoss,
            ItemTier.Cursed, ItemTier.Hex
        };

        // Existing configs keep the player's tier choices, but get the tiers added by game updates once.
        private static void MigrateConfig()
        {
            if (configVersion.Value >= CurrentConfigVersion)
                return;

            if (configVersion.Value < 2)
                enabledTabs.Value = new ItemTierSet(enabledTabs.Value) { ItemTier.Cursed, ItemTier.Hex };

            // Configs still on the old default (White/Green/Red, plus what version 2 added) get every
            // default tier. A list the player actually changed is left alone.
            if (configVersion.Value < 3)
            {
                var oldDefault = new ItemTierSet { ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Cursed, ItemTier.Hex };
                var current = enabledTabs.Value;
                if (current.IsSubsetOf(oldDefault) && current.Contains(ItemTier.Tier1) && current.Contains(ItemTier.Tier2) && current.Contains(ItemTier.Tier3))
                    enabledTabs.Value = new ItemTierSet(DefaultTiers);
            }

            configVersion.Value = CurrentConfigVersion;
        }

        public static string GetTierName(ItemTier tier)
        {
            string name;
            if (ItemTierSet.ItemTierToName.TryGetValue(tier, out name))
                return name;

            return tier.ToString();
        }

        public class ItemTierSet : SortedSet<ItemTier>
        {
            public static Dictionary<ItemTier, string> ItemTierToName = new Dictionary<ItemTier, string>();
            public static Dictionary<string, ItemTier> NameToItemTier = new Dictionary<string, ItemTier>();

            public ItemTierSet() { }
            public ItemTierSet(IEnumerable<ItemTier> tiers) : base(tiers) { }

            internal static void Initialize()
            {
                ItemTierToName.Clear();
                NameToItemTier.Clear();

                foreach (var itemTierDef in ItemTierCatalog.allItemTierDefs)
                {
                    ItemTierToName[itemTierDef.tier] = itemTierDef.name;
                    NameToItemTier[itemTierDef.name] = itemTierDef.tier;
                }
            }

            public static string Serialize(ItemTierSet self)
            {
                return string.Join(", ", self.Select(x => ItemTierToName.TryGetValue(x, out var name) ? name : x.ToString()));
            }
            public static ItemTierSet Deserialize(string src)
            {
                ItemTierSet self = new ItemTierSet();
                foreach(var entry in src.Split(',').Select(s => s.Trim()))
                {
                    if(NameToItemTier.TryGetValue(entry, out var result))
                        self.Add(result);
                    else if(Enum.TryParse(entry, out result))
                        self.Add(result);
                    else if(int.TryParse(entry, out int index))
                        self.Add((ItemTier)index);
                }
                return self;
            }

            public override string ToString()
            {
                return Serialize(this);
            }
        }
    }
}
