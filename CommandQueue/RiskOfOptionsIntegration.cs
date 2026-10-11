using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using JetBrains.Annotations;
using RiskOfOptions;
using RiskOfOptions.Components.Options;
using RiskOfOptions.Lib;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;

namespace CommandQueue
{
    public class RiskOfOptionsIntegration
    {
        private static Texture2D modIcon;
        private static Sprite modIconSprite;
        public void Enable()
        {
            if (ModConfig.Ready)
                InitInternal();
            else
                ModConfig.OnReady += InitInternal;
        }

        internal void InitInternal()
        {
            modIcon = new Texture2D(0, 0);

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CommandQueue.icon.png"))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                byte[] data = memoryStream.ToArray();
                modIcon.LoadImage(data);
            }
            modIconSprite = Sprite.Create(modIcon, new Rect(0f, 0f, modIcon.width, modIcon.height), new Vector2(0.5f, 0.5f));
            
            ModSettingsManager.SetModIcon(modIconSprite);
            ModSettingsManager.SetModDescription("Queue up items to automatically pick from command drops");
            
            ModSettingsManager.AddOption(new CheckBoxOption(ModConfig.rightClickRemovesStack));
            ModSettingsManager.AddOption(new CheckBoxOption(ModConfig.bigItemButtonContainer));
            ModSettingsManager.AddOption(new SliderOption(ModConfig.bigItemButtonScale, new SliderConfig{ min = 0.1f, max = 10f, FormatString = "{0:F2}"}));
            ModSettingsManager.AddOption(new CheckBoxOption(ModConfig.matchItemBarWidth));
            ModSettingsManager.AddOption(new CheckBoxOption(ModConfig.pauseWhileOpen));
            // The HUD preview gets its own clearly named category; the config keys alone ("Enabled",
            // "PositionX") read ambiguously in the options menu.
            const string previewCategory = "Command Queue Preview";
            BaseOptionConfig.IsDisabledDelegate previewDisabled = () => !ModConfig.showQueuePreview.Value;
            ModSettingsManager.AddOption(new CheckBoxOption(ModConfig.showQueuePreview, new CheckBoxConfig
            {
                category = previewCategory,
                name = "Show Command Queue Preview",
            }));
            ModSettingsManager.AddOption(new SliderOption(ModConfig.queuePreviewX, new SliderConfig
            {
                category = previewCategory,
                name = "Fallback Position X",
                min = 0f, max = 1f, FormatString = "{0:F3}",
                checkIfDisabled = previewDisabled,
            }));
            ModSettingsManager.AddOption(new SliderOption(ModConfig.queuePreviewY, new SliderConfig
            {
                category = previewCategory,
                name = "Fallback Position Y",
                min = 0f, max = 1f, FormatString = "{0:F3}",
                checkIfDisabled = previewDisabled,
            }));

            foreach (var itemTierDef in ItemTierCatalog.allItemTierDefs)
            {
                if (itemTierDef.tier == ItemTier.NoTier || itemTierDef.tier == ItemTier.AssignedAtRuntime)
                    continue;
                ModSettingsManager.AddOption(new ItemTierCheckboxOption(ModConfig.enabledTabs, itemTierDef.tier, "Enabled Tabs"));
            }
            
            ModConfig.OnReady -= InitInternal;
        }
        
        public void Disable()
        {
            ModConfig.OnReady -= InitInternal;
        }
        
        public class ItemTierCheckboxOption: CheckBoxOption
        {
            public ConfigEntry<ModConfig.ItemTierSet> SourceConfigEntry;
            public ItemTier BoundItemTier;
            public string CategoryName;

            public string BoundItemTierName;
            
            public ItemTierCheckboxOption(ConfigEntry<ModConfig.ItemTierSet> sourceConfigEntry, ItemTier itemTier, string categoryName) : base(new CheckBoxConfig(), sourceConfigEntry.Value.Contains(itemTier))
            {
                SourceConfigEntry = sourceConfigEntry;
                BoundItemTier = itemTier;
                CategoryName = categoryName;

                BoundItemTierName = TierInfo.GetDisplayName(itemTier);
            }

            public override GameObject CreateOptionGameObject(GameObject prefab, Transform parent)
            {
                GameObject button = Object.Instantiate(prefab, parent);

                var controller = button.GetComponentInChildren<ModSettingsBool>();

                controller.nameToken = GetNameToken();
                controller.settingToken = Identifier;
            
                button.name = $"Mod Option ItemTierCheckBox, {Name} {BoundItemTierName}";

                return button;
            }

            // Risk of Options reads a checkbox's default as (bool)ConfigEntry.DefaultValue. Returning the
            // shared EnabledQueues entry (a tier set) made that cast throw for every tier, which aborted the
            // controls' setup and left them all unchecked. Without an entry it uses the current value.
            public override ConfigEntryBase ConfigEntry => null;

            public override bool Value
            {
                get => SourceConfigEntry.Value.Contains(BoundItemTier);
                set
                {
                    // Assign a new set rather than mutating the current one: that saves the config and
                    // fires SettingChanged, and doesn't modify the entry's default value in place.
                    var tiers = new ModConfig.ItemTierSet(SourceConfigEntry.Value);
                    if (value)
                        tiers.Add(BoundItemTier);
                    else
                        tiers.Remove(BoundItemTier);
                    SourceConfigEntry.Value = tiers;
                }
            }

            public override void SetProperties()
            {
                SetCategoryName(CategoryName, config);
                SetName(BoundItemTierName, config);
                SetDescription($"Show a queue tab for {BoundItemTierName} items ({ModConfig.GetTierName(BoundItemTier)})", config);
            }
        }
    }
}