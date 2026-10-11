using System;
using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommandQueue.UI
{
    // The row of queued items for one tier. Laid out by a GridLayoutGroup, so it wraps onto more
    // rows and reports its preferred height to the layout it sits in.
    class QueueDisplay : MonoBehaviour
    {
        public const int Padding = 5;
        public const float BaseIconSize = 40f;
        public const int MaxRows = 3;
        private const float MinIconSize = 20f;
        // Room between icons, so the item art and stack counts of neighbors don't touch.
        private const float Spacing = 8f;

        public GameObject itemIconPrefab;
        public ItemTier tier;

        private RectTransform rectTransform;
        private GridLayoutGroup grid;
        private float lastWidth = -1;
        private readonly List<ItemIcon> icons = new List<ItemIcon>();
        private List<QueueManager.QueueEntry> queue;

        private float Step => grid.cellSize.x + grid.spacing.x;

        public void Awake()
        {
            rectTransform = transform as RectTransform;
            grid = gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(Padding, Padding, Padding, Padding);
            grid.spacing = new Vector2(Spacing, Spacing);
            grid.cellSize = new Vector2(BaseIconSize, BaseIconSize);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (grid && !Mathf.Approximately(rectTransform.rect.width, lastWidth))
                UpdateIconSize();
        }

        // Past MaxRows the icons shrink instead of adding rows, so the selection grid keeps its space.
        private void UpdateIconSize()
        {
            float width = rectTransform.rect.width;
            lastWidth = width;
            float usableWidth = Mathf.Max(0, width - 2 * Padding);

            float iconSize = BaseIconSize;
            while (iconSize > MinIconSize)
            {
                int perRow = Mathf.Max(1, Mathf.FloorToInt((usableWidth + Spacing) / (iconSize + Spacing)));
                if (Mathf.CeilToInt(icons.Count / (float)perRow) <= MaxRows)
                    break;
                iconSize = Mathf.Max(MinIconSize, iconSize * 0.9f);
            }

            if (!Mathf.Approximately(grid.cellSize.x, iconSize))
            {
                grid.cellSize = new Vector2(iconSize, iconSize);
                foreach (var icon in icons)
                    ScaleIcon(icon);
            }
        }

        private void ScaleIcon(ItemIcon icon) => UIFactory.FitItemIcon(icon, grid.cellSize.x);

        private int IconsPerRow => Mathf.Max(1, Mathf.FloorToInt((rectTransform.rect.width - 2 * Padding + grid.spacing.x) / Step));

        // Index an icon dropped at this local position would be inserted at.
        private int GetInsertIndex(Vector2 localPos)
        {
            var rect = rectTransform.rect;
            float x = localPos.x - rect.xMin - Padding;
            float y = rect.yMax - localPos.y - Padding;
            int perRow = IconsPerRow;
            int rows = Mathf.Max(1, Mathf.CeilToInt(icons.Count / (float)perRow));
            int column = Mathf.Clamp(Mathf.RoundToInt(x / Step), 0, perRow);
            int row = Mathf.Clamp(Mathf.FloorToInt(y / Step), 0, rows - 1);
            return row * perRow + column;
        }

        private ItemIcon AllocateIcon(int index)
        {
            // The ItemIcon prefab is built for its native size, so it's scaled inside a cell the grid sizes.
            var cell = UIFactory.CreateRect(transform, "QueueCell");
            cell.SetSiblingIndex(index);

            ItemIcon icon = Instantiate(itemIconPrefab, cell, false).GetComponent<ItemIcon>();
            ScaleIcon(icon);

            Button button = icon.gameObject.AddComponent<Button>();
            button.onClick.AddListener(delegate
            {
                int i = icons.IndexOf(icon);
                if (i >= 0)
                    QueueManager.Remove(tier, i);
            });
            button.navigation = new Navigation {
                mode = Navigation.Mode.None
            };

            icon.gameObject.AddComponent<QueueItemBehavior>().Init(this);

            icons.Insert(index, icon);
            UpdateIconSize();
            return icon;
        }

        private void RemoveIcon(int index)
        {
            // Destroy is deferred to the end of the frame, detach first so the grid doesn't keep a slot for it.
            var cell = icons[index].transform.parent;
            cell.SetParent(null, false);
            Destroy(cell.gameObject);
            icons.RemoveAt(index);
            UpdateIconSize();
        }

        private void AllocateIcons(int count)
        {
            for(int i = icons.Count-1; i >= count; i--)
                RemoveIcon(i);

            for(int i = icons.Count; i < count; i++)
                AllocateIcon(i);
        }

        public void UpdateUI()
        {
            AllocateIcons(queue.Count);
            for(int i = 0; i < icons.Count; i++) UpdateIcon(i);
        }

        private void UpdateIcon(int i)
        {
            var entry = queue[i];
            icons[i].SetItemIndex(new ItemIcon.SetItemIndexArgs
            {
                newItemIndex = PickupCatalog.GetPickupDef(entry.pickupIndex).itemIndex,
                newItemCount = entry.count,
            });
        }

        private void HandleQueueChange(QueueManager.QueueChange change, ItemTier tier, int index)
        {
            if (tier != this.tier)
                return;

            switch(change)
            {
                case QueueManager.QueueChange.Changed:
                    UpdateIcon(index);
                    break;
                case QueueManager.QueueChange.Added:
                    AllocateIcon(index);
                    UpdateIcon(index);
                    break;
                case QueueManager.QueueChange.Removed:
                    RemoveIcon(index);
                    break;
                case QueueManager.QueueChange.Moved:
                case QueueManager.QueueChange.Reset:
                    UpdateUI();
                    break;
            }
        }

        public void OnEnable()
        {
            if (!itemIconPrefab || !QueueManager.mainQueues.TryGetValue(tier, out queue))
                return;
            UpdateUI();
            QueueManager.OnQueueChanged += HandleQueueChange;
        }

        public void OnDisable()
        {
            QueueManager.OnQueueChanged -= HandleQueueChange;
        }

        // Code based on example from https://docs.unity3d.com/2018.1/Documentation/ScriptReference/EventSystems.IDragHandler.html
        public class QueueItemBehavior : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            private GameObject m_DraggingIcon;
            private QueueDisplay m_Queue;
            private ItemIcon m_ItemIcon;

            public void Init(QueueDisplay queue)
            {
                m_Queue = queue;
                m_ItemIcon = GetComponent<ItemIcon>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                switch (eventData.button)
                {
                    case PointerEventData.InputButton.Left:
                    case PointerEventData.InputButton.Right:
                        break;
                    default: return;
                }

                m_DraggingIcon = new GameObject("QueueItemDragIcon");

                m_DraggingIcon.transform.SetParent(m_Queue.transform, false);
                m_DraggingIcon.transform.SetAsLastSibling();
                // Keep the grid from laying out the drag ghost as if it were a queued item.
                m_DraggingIcon.AddComponent<LayoutElement>().ignoreLayout = true;

                var image = m_DraggingIcon.AddComponent<RawImage>();

                image.texture = m_ItemIcon.image.texture;
                // Don't let the drag ghost swallow the drop raycast.
                image.raycastTarget = false;
                var imageTransform = image.rectTransform;
                var size = m_Queue.grid.cellSize.x;

                imageTransform.anchorMin = new Vector2(0.5f, 0.5f);
                imageTransform.anchorMax = new Vector2(0.5f, 0.5f);
                imageTransform.sizeDelta = new Vector2(size, size);

                image.color = new Color(1f, 1f, 1f, 0.6f);

                SetDraggedPosition(eventData);
                eventData.Use();
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (m_DraggingIcon)
                {
                    SetDraggedPosition(eventData);
                    eventData.Use();
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (!m_DraggingIcon) return;

                Destroy(m_DraggingIcon);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Queue.rectTransform, eventData.position, eventData.pressEventCamera, out var localPos))
                {
                    int newIndex = m_Queue.GetInsertIndex(localPos);

                    int index = m_Queue.icons.IndexOf(m_ItemIcon);
                    var queue = m_Queue.queue;
                    if (index < 0 || index >= queue.Count)
                        return;
                    int count = queue[index].count;
                    if (eventData.button == PointerEventData.InputButton.Right && count > 1)
                        count /= 2;

                    QueueManager.Move(m_Queue.tier, index, newIndex, count);
                }
                eventData.Use();
            }

            private void SetDraggedPosition(PointerEventData data)
            {
                var rt = m_DraggingIcon.GetComponent<RectTransform>();
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(m_Queue.rectTransform, data.position, data.pressEventCamera, out var globalMousePos))
                    rt.position = globalMousePos;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right && ModConfig.rightClickRemovesStack.Value)
                {
                    int index = m_Queue.icons.IndexOf(m_ItemIcon);
                    if (index >= 0)
                        QueueManager.Remove(m_Queue.tier, index, m_Queue.queue[index].count);
                    eventData.Use();
                }
            }
        }
    }
}
