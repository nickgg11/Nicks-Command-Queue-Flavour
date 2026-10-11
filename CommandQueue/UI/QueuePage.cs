using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace CommandQueue.UI
{
    // The Command Queue page: a sidebar listing the item tiers, and the selected tier's panel.
    internal class QueuePage
    {
        private class SidebarEntry
        {
            public UIFactory.ButtonWrapper button;
            public TextMeshProUGUI countLabel;
        }

        private readonly RectTransform panelArea;
        private readonly GameObject itemIconPrefab;
        private readonly ItemInventoryDisplay inventoryDisplayPrefab;

        private readonly Dictionary<ItemTier, PickupIndex[]> tierPickups = new Dictionary<ItemTier, PickupIndex[]>();
        private readonly Dictionary<ItemTier, SidebarEntry> sidebarEntries = new Dictionary<ItemTier, SidebarEntry>();
        // Built the first time their tier is opened, rather than every tier up front.
        private readonly Dictionary<ItemTier, TierPanel> panels = new Dictionary<ItemTier, TierPanel>();

        // Remembered across rebuilds and runs.
        private static ItemTier selectedTier = ItemTier.Tier1;

        // Fixed, so the tier names always fit; the queue and item grid take the rest of the width.
        private const float SidebarWidth = 210f;

        public QueuePage(RectTransform root, GameObject itemIconPrefab, ItemInventoryDisplay inventoryDisplayPrefab)
        {
            this.itemIconPrefab = itemIconPrefab;
            this.inventoryDisplayPrefab = inventoryDisplayPrefab;

            var layout = UIFactory.AddHorizontalLayout(root, 10);
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(10, 10, 10, 10);

            var visibleTiers = GetVisibleTiers();
            BuildSidebar(root, visibleTiers);

            // Tier panels stretch over this area; only the selected one is active.
            panelArea = UIFactory.CreateRect(root, "TierPanelArea");
            UIFactory.Layout(panelArea, preferredWidth: 0, flexibleWidth: 1);

            if (visibleTiers.Count == 0)
            {
                var label = UIFactory.CreateLabel(panelArea, "No item tiers are enabled. Enable some in the config (EnabledQueues).");
                UIFactory.Stretch(label.rectTransform);
            }
            else
            {
                if (!tierPickups.ContainsKey(selectedTier))
                    selectedTier = visibleTiers[0].tier;
                Select(selectedTier);
            }

            QueueManager.OnQueueChanged += HandleQueueChanged;
        }

        // Tiers get an entry if a command essence of that tier can offer something this run, or if the
        // player already queued items in it.
        private List<(ItemTier tier, PickupIndex[] pickups)> GetVisibleTiers()
        {
            var result = new List<(ItemTier, PickupIndex[])>();
            foreach (ItemTier tier in ModConfig.enabledTabs.Value)
            {
                var pickups = TierInfo.GetPickups(tier);
                bool hasQueuedItems = QueueManager.mainQueues.TryGetValue(tier, out var queue) && queue.Count > 0;
                if (pickups.Length > 0 && (TierInfo.AnyAvailable(pickups) || hasQueuedItems))
                {
                    result.Add((tier, pickups));
                    tierPickups[tier] = pickups;
                }
            }
            return result;
        }

        private void BuildSidebar(Transform parent, List<(ItemTier tier, PickupIndex[] pickups)> tiers)
        {
            var list = UIFactory.CreateScrollView(parent, "TierSidebar", out var scrollRect);
            UIFactory.Layout(scrollRect, preferredWidth: SidebarWidth).minWidth = SidebarWidth;
            UIFactory.AddVerticalLayout(list, 4);

            foreach (var (tier, _) in tiers)
            {
                var button = UIFactory.CreateButton(list, text: TierInfo.GetDisplayName(tier), onClick: () => Select(tier));
                UIFactory.Layout(button.rectTransform, preferredHeight: 40);
                // Room for the color stripe and the count. Alignment and color stay the skin's.
                if (button.textComponent)
                    button.textComponent.margin = new Vector4(16, 0, 34, 0);

                // Tier color stripe, like the colored edge of the game's own pickup menus.
                var stripeTransform = UIFactory.CreateTierStripe(button.rectTransform, tier, vertical: true);
                stripeTransform.anchorMin = new Vector2(0, 0);
                stripeTransform.anchorMax = new Vector2(0, 1);
                stripeTransform.pivot = new Vector2(0, 0.5f);
                stripeTransform.sizeDelta = new Vector2(5, -10);
                stripeTransform.anchoredPosition = new Vector2(5, 0);

                var countLabel = UIFactory.CreateLabel(button.rectTransform, "", 16f, TextAlignmentOptions.MidlineRight);
                countLabel.color = UIFactory.SubtleTextColor;
                var countTransform = countLabel.rectTransform;
                countTransform.anchorMin = new Vector2(1, 0);
                countTransform.anchorMax = new Vector2(1, 1);
                countTransform.pivot = new Vector2(1, 0.5f);
                countTransform.sizeDelta = new Vector2(40, 0);
                countTransform.anchoredPosition = new Vector2(-8, 0);

                sidebarEntries[tier] = new SidebarEntry { button = button, countLabel = countLabel };
                UpdateCount(tier);
            }
        }

        private void Select(ItemTier tier)
        {
            if (!tierPickups.TryGetValue(tier, out var pickups))
                return;

            foreach (var panel in panels.Values)
                panel.root.SetActive(panel.tier == tier);

            if (!panels.ContainsKey(tier))
                panels[tier] = new TierPanel(tier, pickups, panelArea, itemIconPrefab, inventoryDisplayPrefab);

            foreach (var pair in sidebarEntries)
                UIFactory.MarkButton(pair.Value.button, pair.Key == tier);

            selectedTier = tier;
        }

        // Re-applies the selected state, which the button's own transitions can reset when shown.
        public void RefreshSelection()
        {
            if (sidebarEntries.TryGetValue(selectedTier, out var entry))
                UIFactory.MarkButton(entry.button, true);
        }

        private void UpdateCount(ItemTier tier)
        {
            if (!sidebarEntries.TryGetValue(tier, out var entry))
                return;
            int count = QueueManager.mainQueues.TryGetValue(tier, out var queue) ? queue.Sum(e => e.count) : 0;
            entry.countLabel.text = count > 0 ? count.ToString() : "";
        }

        private void HandleQueueChanged(QueueManager.QueueChange change, ItemTier tier, int index) => UpdateCount(tier);

        public void Destroy()
        {
            QueueManager.OnQueueChanged -= HandleQueueChanged;
            foreach (var panel in panels.Values)
                panel.Destroy();
            panels.Clear();
        }
    }
}
