using System;
using System.Collections.Generic;
using System.Linq;
using CommandQueue.UI;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommandQueue.Features
{
    // Optional HUD widget showing the next queued item of each tier during Command runs.
    // Self-contained: to remove it, delete this file and the QueuePreviewHud.Enable/Disable calls in
    // CommandQueue.cs (and the QueuePreview config entries in ModConfig.cs if you want them gone too).
    internal static class QueuePreviewHud
    {
        private static bool enabled;

        public static void Enable()
        {
            if (enabled)
                return;
            enabled = true;
            On.RoR2.UI.HUD.Awake += HUD_Awake;
            foreach (var hud in UnityEngine.Object.FindObjectsOfType<HUD>())
                Attach(hud);
        }

        public static void Disable()
        {
            if (!enabled)
                return;
            enabled = false;
            On.RoR2.UI.HUD.Awake -= HUD_Awake;
            foreach (var host in UnityEngine.Object.FindObjectsOfType<QueuePreviewHost>(true))
                UnityEngine.Object.Destroy(host);
        }

        private static void HUD_Awake(On.RoR2.UI.HUD.orig_Awake orig, HUD self)
        {
            orig(self);
            Attach(self);
        }

        private static void Attach(HUD hud)
        {
            if (!hud.GetComponent<QueuePreviewHost>())
                hud.gameObject.AddComponent<QueuePreviewHost>().hud = hud;
        }
    }

    // Lives on the HUD and owns the widget. The widget is parented into the run's HUD panel, which is
    // rebuilt independently, and is hidden while empty, so the logic can't live on the widget itself.
    internal class QueuePreviewHost : MonoBehaviour
    {
        // Each entry: [tier bar] [icon] [+N more queued], three entries per row. Sizes at full scale; when
        // three don't fit between the objectives and the screen edge, everything is scaled down.
        private const int Columns = 3;
        private const float IconSize = 30f;
        private const float StripeWidth = 4f;
        private const float StripeGap = 3f;
        private const float CountGap = 6f;
        // Fits "+999" at the full font size; the label also shrinks rather than cutting off.
        private const float CountWidth = 34f;
        private const float CountFontSize = 13f;
        private const float CellWidth = StripeWidth + StripeGap + IconSize + CountGap + CountWidth;
        private const float ColumnSpacing = 6f;
        private const float RowSpacing = 4f;
        private const float MinScale = 0.6f;
        // Kept between the panel and the screen edge.
        private const float ScreenMargin = 4f;
        private const string PanelName = "CommandQueuePreview";
        private const string Title = "Command Queue Preview";

        public HUD hud;

        // Docked: an empty slot in the info bar that reserves the panel's height, with the panel inside it.
        // The panel is as wide as three columns need, which can be wider than the bar; a layout child
        // can't be wider than its layout group, so the slot is the layout child instead.
        private RectTransform slot;
        private RectTransform panel;
        private RectTransform entries;
        private bool docked;
        private float nextDockAttempt;
        private bool dirty = true;
        private float lastPanelHeight = -1;
        private readonly Vector3[] corners = new Vector3[4];
        private GridLayoutGroup grid;
        // Applied to every size in the entries.
        private float scale = 1f;

        private GameObject itemIconPrefab;

        private GameObject Root => slot ? slot.gameObject : panel ? panel.gameObject : null;

        private void Awake()
        {
            var hudSimple = LegacyResourcesAPI.Load<GameObject>("Prefabs/HUDSimple").GetComponent<HUD>();
            itemIconPrefab = hudSimple.itemInventoryDisplay.itemIconPrefab;
        }

        private void OnEnable()
        {
            QueueManager.OnQueueChanged += HandleQueueChanged;
            RunArtifactManager.onArtifactEnabledGlobal += HandleArtifactChanged;
            RunArtifactManager.onArtifactDisabledGlobal += HandleArtifactChanged;
            ModConfig.queuePreviewX.SettingChanged += HandleLayoutSettingChanged;
            ModConfig.queuePreviewY.SettingChanged += HandleLayoutSettingChanged;
            dirty = true;
        }

        private void OnDisable()
        {
            QueueManager.OnQueueChanged -= HandleQueueChanged;
            RunArtifactManager.onArtifactEnabledGlobal -= HandleArtifactChanged;
            RunArtifactManager.onArtifactDisabledGlobal -= HandleArtifactChanged;
            ModConfig.queuePreviewX.SettingChanged -= HandleLayoutSettingChanged;
            ModConfig.queuePreviewY.SettingChanged -= HandleLayoutSettingChanged;
        }

        private void OnDestroy() => DestroyPanel();

        private void HandleQueueChanged(QueueManager.QueueChange change, ItemTier tier, int index) => dirty = true;
        private void HandleArtifactChanged(RunArtifactManager manager, ArtifactDef artifact) => dirty = true;

        // Rebuilt from scratch on the next frame with the new settings.
        private void HandleLayoutSettingChanged(object sender, EventArgs args)
        {
            DestroyPanel();
            nextDockAttempt = 0;
        }

        private void DestroyPanel()
        {
            var root = Root;
            if (root)
            {
                root.transform.SetParent(null, false);
                Destroy(root);
            }
            slot = null;
            panel = null;
            entries = null;
            docked = false;
            lastPanelHeight = -1;
            grid = null;
            scale = 1f;
        }

        private void Update()
        {
            if (!panel)
                DestroyPanel();

            // Docking needs a hierarchy search, so retry about once a second rather than every frame.
            if (!docked && Time.unscaledTime >= nextDockAttempt)
            {
                nextDockAttempt = Time.unscaledTime + 1f;
                TryDock();
            }

            if (!panel)
            {
                if (!hud || !hud.mainUIPanel || !UIFactory.TryInit())
                    return;
                CreateFloatingPanel();
            }

            if (dirty)
                Refresh();

            if (docked)
                UpdateDockedSize();
        }

        private int PanelPadding => panel.TryGetComponent<VerticalLayoutGroup>(out var layout) ? layout.padding.horizontal : 0;

        // The panel's left edge stays lined up with the objectives, so it never reaches towards the
        // scoreboard; it extends right towards the screen edge. If three entries don't fit in that space
        // (narrow aspect ratio, large HUD scale) the entries are scaled down instead of moving the panel.
        private void UpdateDockedSize()
        {
            float fullWidth = Columns * CellWidth + (Columns - 1) * ColumnSpacing;
            float available = fullWidth;
            if (hud.mainContainer && hud.mainContainer.transform is RectTransform screen)
            {
                screen.GetWorldCorners(corners);
                float screenRight = slot.InverseTransformPoint(corners[2]).x - ScreenMargin;
                available = screenRight - slot.rect.xMin - PanelPadding;
            }

            float newScale = Mathf.Clamp(available / fullWidth, MinScale, 1f);
            if (Mathf.Abs(newScale - scale) > 0.01f)
            {
                scale = newScale;
                ApplyGridSize();
                dirty = true;
            }
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelPadding + fullWidth * scale, panel.sizeDelta.y);

            // The slot reserves the panel's height in the info bar; re-layout the bar when it changes.
            float panelHeight = panel.rect.height;
            if (!Mathf.Approximately(panelHeight, lastPanelHeight))
            {
                lastPanelHeight = panelHeight;
                LayoutRebuilder.MarkLayoutForRebuild(slot);
            }
        }

        private void ApplyGridSize()
        {
            if (!grid)
                return;
            grid.cellSize = new Vector2(CellWidth * scale, IconSize * scale);
            grid.spacing = new Vector2(ColumnSpacing * scale, RowSpacing * scale);
        }

        // Classic runs show the timer and difficulty bar, and under them a "RightInfoBar" layout group
        // with the artifact and objective panels. Like LookingGlass's stats panel, the preview is a copy
        // of the objective panel placed right under it: the game positions and scales it for any
        // resolution or aspect ratio, it moves down as objectives are added, and it uses the panel's
        // own skin. Game modes or HUD mods without that panel get the floating fallback instead.
        private void TryDock()
        {
            if (!hud || !hud.gameModeUiRoot)
                return;

            var infoBar = hud.gameModeUiRoot.GetComponentsInChildren<VerticalLayoutGroup>(true).FirstOrDefault(group => group.name == "RightInfoBar");
            var objectivePanel = infoBar ? infoBar.transform.Find("ObjectivePanel") : null;
            if (!objectivePanel)
                return;

            var newSlot = UIFactory.CreateRect(infoBar.transform, PanelName + "Slot");
            newSlot.SetSiblingIndex(objectivePanel.GetSiblingIndex() + 1);

            var copy = CopyObjectivePanel(objectivePanel, newSlot);
            if (!copy)
            {
                Destroy(newSlot.gameObject);
                return;
            }

            DestroyPanel();
            slot = newSlot;
            slot.gameObject.AddComponent<ReserveChildHeight>().child = copy;
            panel = copy;
            docked = true;
            entries = CreateEntries(panel);
            dirty = true;
        }

        private static RectTransform CopyObjectivePanel(Transform objectivePanel, RectTransform slot)
        {
            // Copy under an inactive holder so the objective logic on the copy never wakes up.
            var holder = new GameObject("CommandQueuePreviewHolder");
            holder.SetActive(false);
            try
            {
                var copy = Instantiate(objectivePanel.gameObject, holder.transform, false);
                copy.name = PanelName;
                foreach (var objectiveController in copy.GetComponents<ObjectivePanelController>()) DestroyImmediate(objectiveController);
                foreach (var targetSetter in copy.GetComponents<HudObjectiveTargetSetter>()) DestroyImmediate(targetSetter);
                foreach (var childName in new[] { "StripContainer", "Minimap" })
                {
                    var child = copy.transform.Find(childName);
                    if (child)
                        DestroyImmediate(child.gameObject);
                }

                var title = copy.transform.Find("Label");
                if (title)
                {
                    title.gameObject.SetActive(true);
                    if (title.TryGetComponent<LanguageTextMeshController>(out var language))
                        language.token = Title;
                    else if (title.TryGetComponent<TextMeshProUGUI>(out var titleText))
                        titleText.text = Title;
                }

                // Pinned to the slot's top left, so the extra width extends towards the screen edge.
                var copyTransform = (RectTransform)copy.transform;
                copyTransform.SetParent(slot, false);
                copyTransform.localPosition = Vector3.zero;
                copyTransform.localEulerAngles = Vector3.zero;
                copyTransform.localScale = Vector3.one;
                copyTransform.anchorMin = copyTransform.anchorMax = copyTransform.pivot = new Vector2(0, 1);
                copyTransform.anchoredPosition = Vector2.zero;
                if (!copy.TryGetComponent<ContentSizeFitter>(out var fitter))
                    fitter = copy.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                return copyTransform;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CommandQueue] Couldn't attach the queue preview to the objective panel, using the floating position: {e}");
                return null;
            }
            finally
            {
                Destroy(holder);
            }
        }

        private void CreateFloatingPanel()
        {
            panel = UIFactory.CreateRect(hud.mainUIPanel.transform, PanelName);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = UIFactory.PanelColor;
            background.raycastTarget = false;

            var layout = UIFactory.AddVerticalLayout(panel, 3);
            layout.padding = new RectOffset(6, 6, 4, 6);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UIFactory.CreateLabel(panel, Title, 16f, TextAlignmentOptions.MidlineLeft);
            UIFactory.Layout(header, preferredHeight: 20);

            // Pivot follows the anchor, so the panel stays on screen at any edge.
            var position = new Vector2(ModConfig.queuePreviewX.Value, ModConfig.queuePreviewY.Value);
            panel.anchorMin = panel.anchorMax = panel.pivot = position;
            panel.anchoredPosition = Vector2.zero;

            entries = CreateEntries(panel);
            dirty = true;
        }

        private RectTransform CreateEntries(RectTransform parent)
        {
            // Purely informational, never take clicks from the scoreboard or other HUD elements.
            if (!parent.TryGetComponent<CanvasGroup>(out var canvasGroup))
                canvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // A grid rather than a list, so many queued tiers don't make the panel long.
            var container = UIFactory.CreateRect(parent, "Entries");
            grid = container.gameObject.AddComponent<GridLayoutGroup>();
            ApplyGridSize();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            return container;
        }

        private static bool CommandEnabled =>
            RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.commandArtifactDef);

        private void Refresh()
        {
            dirty = false;

            foreach (var child in entries.Cast<Transform>().ToList())
            {
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            var heads = CommandEnabled
                ? QueueManager.mainQueues.Where(pair => pair.Value.Count > 0).ToList()
                : new List<KeyValuePair<ItemTier, List<QueueManager.QueueEntry>>>();

            // Hidden while empty, so it doesn't leave a gap under the objectives.
            Root.SetActive(heads.Count > 0);

            foreach (var pair in heads)
                CreateEntry(pair.Key, pair.Value);
        }

        private static int Remaining(List<QueueManager.QueueEntry> queue) => queue.Skip(1).Sum(e => e.count);

        private void CreateEntry(ItemTier tier, List<QueueManager.QueueEntry> queue)
        {
            var cell = UIFactory.CreateRect(entries, $"{tier}Preview");
            float stripeWidth = Mathf.Max(2f, Mathf.Round(StripeWidth * scale));
            float iconLeft = stripeWidth + StripeGap * scale;
            float iconSize = IconSize * scale;

            // Whole-pixel width in its own column, so the bar renders as an even rectangle and the
            // icon's art can't cover part of it.
            var stripe = UIFactory.CreateTierStripe(cell, tier, vertical: true);
            stripe.anchorMin = new Vector2(0, 0);
            stripe.anchorMax = new Vector2(0, 1);
            stripe.pivot = new Vector2(0, 0.5f);
            stripe.sizeDelta = new Vector2(stripeWidth, 0);
            stripe.anchoredPosition = Vector2.zero;

            // Same icon as the HUD inventory: tier glow and stack count included.
            var iconHolder = UIFactory.CreateRect(cell, "Icon");
            iconHolder.anchorMin = iconHolder.anchorMax = iconHolder.pivot = new Vector2(0, 0.5f);
            iconHolder.sizeDelta = new Vector2(iconSize, iconSize);
            iconHolder.anchoredPosition = new Vector2(iconLeft, 0);
            var icon = Instantiate(itemIconPrefab, iconHolder, false).GetComponent<ItemIcon>();
            UIFactory.FitItemIcon(icon, iconSize);
            var head = queue[0];
            icon.SetItemIndex(new ItemIcon.SetItemIndexArgs
            {
                newItemIndex = PickupCatalog.GetPickupDef(head.pickupIndex).itemIndex,
                newItemCount = head.count,
            });

            // Kept clear of the icon, whose own stack count sits in its corner.
            int remaining = Remaining(queue);
            if (remaining > 0)
            {
                var more = UIFactory.CreateLabel(cell, $"+{remaining}", CountFontSize * scale, TextAlignmentOptions.MidlineLeft);
                more.color = UIFactory.SubtleTextColor;
                // Shrink to fit instead of cutting the number off.
                more.overflowMode = TextOverflowModes.Overflow;
                more.enableAutoSizing = true;
                more.fontSizeMax = CountFontSize * scale;
                more.fontSizeMin = 7f;
                var moreTransform = more.rectTransform;
                moreTransform.anchorMin = new Vector2(0, 0);
                moreTransform.anchorMax = new Vector2(0, 1);
                moreTransform.pivot = new Vector2(0, 0.5f);
                moreTransform.sizeDelta = new Vector2(CountWidth * scale, 0);
                moreTransform.anchoredPosition = new Vector2(iconLeft + iconSize + CountGap * scale, 0);
            }
        }

        // Makes an empty layout child as tall as its (non-layout) child, so the info bar leaves room for it.
        private class ReserveChildHeight : UIBehaviour, ILayoutElement
        {
            public RectTransform child;

            public void CalculateLayoutInputHorizontal() { }
            public void CalculateLayoutInputVertical() { }
            public float minWidth => -1;
            public float preferredWidth => -1;
            public float flexibleWidth => -1;
            public float minHeight => preferredHeight;
            public float preferredHeight => child ? LayoutUtility.GetPreferredHeight(child) : 0;
            public float flexibleHeight => -1;
            public int layoutPriority => 1;
        }
    }
}
