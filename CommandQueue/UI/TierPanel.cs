using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace CommandQueue.UI
{
    // One tier's page: its queue, the repeat toggle, and the scrollable grid of items to add.
    internal class TierPanel
    {
        public readonly ItemTier tier;
        public readonly GameObject root;

        private readonly List<AsyncOperationHandle<Sprite>> iconHandles = new List<AsyncOperationHandle<Sprite>>();

        // The game's command menu buttons are a bit large for a page that also holds the queue.
        private const float ItemButtonScale = 0.9f;
        private const int CtrlClickCount = 10;

        private static bool ControlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public TierPanel(ItemTier tier, PickupIndex[] pickups, Transform parent, GameObject itemIconPrefab, ItemInventoryDisplay inventoryDisplayPrefab)
        {
            this.tier = tier;

            var rootTransform = UIFactory.CreateRect(parent, $"{tier}QueuePanel");
            UIFactory.Stretch(rootTransform);
            root = rootTransform.gameObject;
            UIFactory.AddVerticalLayout(rootTransform, 5);

            BuildQueueRow(rootTransform, itemIconPrefab, inventoryDisplayPrefab);
            BuildItemGrid(rootTransform, pickups);
        }

        private void BuildQueueRow(Transform parent, GameObject itemIconPrefab, ItemInventoryDisplay inventoryDisplayPrefab)
        {
            // The queue fills the width; the repeat button is a fixed square as tall as a one-row queue.
            var row = UIFactory.CreateRect(parent, "QueueRow");
            var rowLayout = row.gameObject.AddComponent<QueueRowLayout>();
            rowLayout.minRowHeight = QueueDisplay.BaseIconSize + 2 * QueueDisplay.Padding;

            // The scoreboard's inventory strip, so the queue looks like the inventories above it.
            GameObject queueBox = Object.Instantiate(inventoryDisplayPrefab.gameObject, row, false);
            queueBox.name = "QueueBox";
            Object.DestroyImmediate(queueBox.GetComponent<ItemInventoryDisplay>());
            foreach (var component in queueBox.GetComponents<LayoutGroup>()) Object.DestroyImmediate(component);
            foreach (var component in queueBox.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(component);
            foreach (Transform child in queueBox.transform)
                UIFactory.Layout(child).ignoreLayout = true;
            UIFactory.AddVerticalLayout(queueBox.transform, 0);

            var gridTransform = UIFactory.CreateRect(queueBox.transform, "QueueGrid");
            // Keep one row's height when the queue is empty.
            gridTransform.gameObject.AddComponent<LayoutElement>().minHeight = QueueDisplay.BaseIconSize + 2 * QueueDisplay.Padding;
            // Set the fields before the first OnEnable runs.
            gridTransform.gameObject.SetActive(false);
            var queueDisplay = gridTransform.gameObject.AddComponent<QueueDisplay>();
            queueDisplay.itemIconPrefab = itemIconPrefab;
            queueDisplay.tier = tier;
            gridTransform.gameObject.SetActive(true);

            UIFactory.ButtonWrapper repeatButton = default; // So we can reference it in the delegate
            repeatButton = UIFactory.CreateButton(row, text: "", onClick: delegate
            {
                QueueManager.ToggleRepeat(tier);
                UIFactory.MarkButton(repeatButton, QueueManager.DoesRepeat(tier));
            });
            UIFactory.SetTooltip(repeatButton.gameObject, "Repeat", "When enabled, items picked from this queue are added back to the end of it.", Color.white);
            UIFactory.MarkButton(repeatButton, QueueManager.DoesRepeat(tier));

            var repeatImageTransform = UIFactory.CreateRect(repeatButton.rectTransform, "RepeatIcon");
            repeatImageTransform.anchorMin = Vector2.zero;
            repeatImageTransform.anchorMax = Vector2.one;
            repeatImageTransform.sizeDelta = new Vector2(-14, -14);
            var repeatImage = repeatImageTransform.gameObject.AddComponent<RawImage>();
            repeatImage.texture = UIFactory.RepeatIcon;
            repeatImage.raycastTarget = false;
        }

        private void BuildItemGrid(Transform parent, PickupIndex[] pickups)
        {
            var content = UIFactory.CreateScrollView(parent, "ItemGridScroll", out var scrollRect);
            UIFactory.Layout(scrollRect, flexibleHeight: 1, flexibleWidth: 1);
            UIFactory.AddVerticalLayout(content, 0);

            var itemButtonsContainer = Object.Instantiate(UIFactory.ItemContainerPrefab, content, false);
            foreach (var component in itemButtonsContainer.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(component);
            var itemButtonsTransform = itemButtonsContainer.transform as RectTransform;
            var gridLayoutGroup = itemButtonsContainer.GetComponent<GridLayoutGroup>();
            // A partly filled last row starts at the left like the others.
            gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            gridLayoutGroup.cellSize *= ItemButtonScale;

            if (ModConfig.bigItemButtonContainer.Value)
            {
                Object.Destroy(itemButtonsContainer.GetComponent<RawImage>());
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.Flexible;
                gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
                gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
                gridLayoutGroup.cellSize *= ModConfig.bigItemButtonScale.Value;
            }
            else
            {
                gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayoutGroup.constraintCount = UIFactory.PickupPickerPanelPrefab.maxColumnCount;
            }

            var buttonAllocator = new UIElementAllocator<MPButton>(itemButtonsTransform, UIFactory.ItemButtonPrefab);
            buttonAllocator.onCreateElement = (index, button) =>
            {
                var pickupIndex = pickups[index];
                bool available = TierInfo.IsAvailable(pickupIndex);
                SetupItemButton(button, pickupIndex, available);

                if (available)
                {
                    button.onClick.AddListener(delegate
                    {
                        QueueManager.TryEnqueue(pickupIndex, ControlHeld ? CtrlClickCount : 1);
                    });
                }
            };
            buttonAllocator.AllocateElements(pickups.Length);
        }

        private void SetupItemButton(MPButton button, PickupIndex pickupIndex, bool available)
        {
            var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            var iconTransform = button.GetComponent<ChildLocator>().FindChild("Icon");
            var image = iconTransform.GetComponent<Image>();

            available &= pickupDef != null;
            button.interactable = available;
            image.color = available ? Color.white : Color.gray;
            image.sprite = available ? pickupDef.iconSprite : UIFactory.LockedIcon;
            // An Image without a sprite draws as a white square, so stay hidden until the icon arrives.
            image.enabled = image.sprite;

            // Since 1.5 the button icon is an AsyncImage fed with an addressable reference, and
            // PickupDef.iconSprite is often null, which is what produced white boxes. AsyncImage itself
            // drops its load callback while its GameObject is inactive, which hidden panels are,
            // so load the sprite ourselves and keep AsyncImage out of the way.
            var asyncImage = iconTransform.GetComponent<AsyncImage>();
            if (asyncImage)
                asyncImage.enabled = false;
            if (available && !image.sprite && pickupDef.iconSpriteReference != null && pickupDef.iconSpriteReference.RuntimeKeyIsValid())
            {
                var iconHandle = Addressables.LoadAssetAsync<Sprite>(pickupDef.iconSpriteReference.RuntimeKey);
                iconHandles.Add(iconHandle);
                iconHandle.Completed += handle =>
                {
                    if (image && handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        image.sprite = handle.Result;
                        image.enabled = true;
                    }
                };
            }

            if (!button.TryGetComponent<TooltipProvider>(out var tooltip))
                tooltip = button.gameObject.AddComponent<TooltipProvider>();
            // Unavailable items show the lock icon, don't reveal them through the tooltip either.
            tooltip.enabled = available;
            // These buttons select themselves on hover, and the scoreboard can take selection back.
            // Removing the tooltip on deselect then removed it as soon as it appeared; pointer exit
            // still removes it.
            tooltip.removeTooltipOnDeselect = false;
            tooltip.AllowTooltipOnNavigationSelect = true;
            if (!available)
                return;

            var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            string body = Language.GetString(itemDef ? itemDef.descriptionToken : pickupDef.pickupToken);
            if (itemDef && itemDef.tier == ItemTier.Hex && itemDef.DoesNotContainTag(ItemTag.WorldUnique))
                body = Language.GetStringFormatted("HEXTIER_PICKUP_SUFFIX", body);

            tooltip.titleToken = pickupDef.nameToken;
            tooltip.titleColor = pickupDef.darkColor;
            tooltip.overrideBodyText = body + $"\n\n<style=cStack>Ctrl+click to queue {CtrlClickCount}.</style>";
            tooltip.bodyColor = UIFactory.SubtleTextColor;
        }

        public void Destroy()
        {
            foreach (var handle in iconHandles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            iconHandles.Clear();
            if (root)
                Object.Destroy(root);
        }
    }
}
