using System;
using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace CommandQueue.UI
{
    // Lives on the scoreboard: adds the Scoreboard / Command Queue / Saves buttons and switches pages.
    class UIManager : MonoBehaviour
    {
        private enum Page { Queue, Scoreboard, Saves }

        // Remembered across scoreboard openings.
        private static Page displayedPage = Page.Scoreboard;

        private GameObject itemIconPrefab;
        private ItemInventoryDisplay itemInventoryDisplayPrefab;
        private ScoreboardController scoreboard;

        //Scoreboard contents, moved into a wrapper so they can be hidden
        private GameObject container;
        private GameObject queueContainer;
        private GameObject saveLoadContainer;
        private readonly List<GameObject> topButtons = new List<GameObject>();

        private UIFactory.ButtonWrapper scoreboardButton;
        private UIFactory.ButtonWrapper queueButton;
        private UIFactory.ButtonWrapper saveLoadButton;

        private QueuePage queuePage;
        private SavesPage savesPage;

        private bool pausedByUs;

        private HUD hud;
        private RectTransform scoreboardTransform;
        private Vector2? originalOffsetMin;
        private Vector2? originalOffsetMax;
        private readonly Vector3[] corners = new Vector3[4];

        private void ShowPage(Page page)
        {
            displayedPage = page;
            ApplyDisplayedPage();
            if (page == Page.Saves)
                savesPage?.Refresh();
        }

        private void ApplyDisplayedPage()
        {
            if (container && queueContainer && saveLoadContainer)
            {
                container.SetActive(displayedPage == Page.Scoreboard);
                queueContainer.SetActive(displayedPage == Page.Queue);
                saveLoadContainer.SetActive(displayedPage == Page.Saves);
            }
            UIFactory.MarkButton(queueButton, displayedPage == Page.Queue);
            UIFactory.MarkButton(scoreboardButton, displayedPage == Page.Scoreboard);
            UIFactory.MarkButton(saveLoadButton, displayedPage == Page.Saves);
            if (displayedPage == Page.Queue)
                queuePage?.RefreshSelection();
            UpdatePause();
        }

        // Single player only, same rule as the game's inspect auto-pause. When the scoreboard closes the
        // game unpauses on its own, we only need to unpause when switching back to the scoreboard page.
        private void UpdatePause()
        {
            var pauseController = PauseStopController.instance;
            if (!pauseController)
                return;

            bool canPause = RoR2Application.isInSinglePlayer && !RoR2Application.isInLocalMultiPlayer;
            bool shouldPause = canPause && ModConfig.pauseWhileOpen.Value && isActiveAndEnabled && displayedPage != Page.Scoreboard;

            if (shouldPause && !pauseController.isPaused)
            {
                pauseController.Pause(true);
                pausedByUs = true;
            }
            else if (!shouldPause && pausedByUs)
            {
                if (pauseController.isPaused)
                    pauseController.Pause(false);
                pausedByUs = false;
            }
        }

        private void DestroyUI()
        {
            queuePage?.Destroy();
            queuePage = null;
            savesPage = null;

            foreach (var button in topButtons)
            {
                if (button)
                    Destroy(button);
            }
            topButtons.Clear();

            if(container != null)
            {
                // Put the scoreboard's own contents back where they were.
                RectTransform origTransform = scoreboard.GetComponent<RectTransform>();
                ReparentChildren(origTransform, container.transform);
                Destroy(container);
                Destroy(queueContainer);
                Destroy(saveLoadContainer);
                container = null;
            }
        }

        private static void ReparentChildren(Transform dest, Transform source)
        {
            var children = new List<Transform>();
            foreach(Transform child in source)
            {
                if(child != dest)
                    children.Add(child);
            }
            foreach(var child in children)
                child.SetParent(dest, false);
        }

        private RectTransform CreatePageRoot(string name, Transform parent)
        {
            var page = UIFactory.CreateRect(parent, name);
            UIFactory.SetAnchors(page, 0, 0, 1, 0.93f);
            page.gameObject.SetActive(false);
            return page;
        }

        public void RebuildUI()
        {
            DestroyUI();

            if (!gameObject.activeSelf || !UIFactory.TryInit())
                return; //Defer rebuild, OnEnable retries

            RectTransform origTransform = scoreboard.GetComponent<RectTransform>();

            // Move the scoreboard's contents into a wrapper below our page buttons.
            container = new GameObject("CommandQueueScoreboardWrapper", typeof(RectTransform));
            var containerTransform = container.GetComponent<RectTransform>();
            containerTransform.SetParent(origTransform, false);
            UIFactory.SetAnchors(containerTransform, 0, 0, 1, 0.93f);
            ReparentChildren(containerTransform, origTransform);

            var queueTransform = CreatePageRoot("CommandQueueContainer", origTransform);
            queueContainer = queueTransform.gameObject;
            queuePage = new QueuePage(queueTransform, itemIconPrefab, itemInventoryDisplayPrefab);

            var savesTransform = CreatePageRoot("SaveLoadContainer", origTransform);
            saveLoadContainer = savesTransform.gameObject;
            savesPage = new SavesPage(savesTransform);

            UIFactory.ButtonWrapper CreateTopButton(string text, float xMin, float xMax, Page page)
            {
                var button = UIFactory.CreateButton(origTransform, text: text, onClick: () => ShowPage(page));
                UIFactory.SetAnchors(button.rectTransform, xMin, 0.93f, xMax, 1);
                topButtons.Add(button.gameObject);
                return button;
            }

            scoreboardButton = CreateTopButton("Scoreboard", 0, 0.333f, Page.Scoreboard);
            queueButton = CreateTopButton("Command Queue", 0.333f, 0.667f, Page.Queue);
            saveLoadButton = CreateTopButton("Saves", 0.667f, 1, Page.Saves);

            ApplyDisplayedPage();
            // Button transitions reset colors on the first frame they're shown.
            StartCoroutine(ApplyNextFrame());
        }

        private System.Collections.IEnumerator ApplyNextFrame()
        {
            yield return null;
            ApplyDisplayedPage();
        }

        public void Awake()
        {
            if(!CommandQueue.IsLoaded)
            {
                Destroy(this);
                return;
            }

            scoreboard = GetComponent<ScoreboardController>();
            scoreboardTransform = (RectTransform)scoreboard.transform;
            hud = GetComponentInParent<HUD>();
            itemInventoryDisplayPrefab = scoreboard.stripPrefab.GetComponent<ScoreboardStrip>().itemInventoryDisplay;
            var hudSimple = LegacyResourcesAPI.Load<GameObject>("Prefabs/HUDSimple").GetComponent<HUD>();
            itemIconPrefab = hudSimple.itemInventoryDisplay.itemIconPrefab;
            CommandQueue.PluginUnloaded += HandlePluginUnloaded;
        }

        private void HandlePluginUnloaded()
        {
            DestroyImmediate(this);
        }

        private void OnDestroy()
        {
            CommandQueue.PluginUnloaded -= HandlePluginUnloaded;
            DestroyUI();
            RestoreScoreboardSize();
        }

        // The vanilla scoreboard is wider than the item bar at the top of the screen and runs into the
        // HUD on both sides. Match the item bar's left and right edges instead. They're measured on
        // screen rather than hard-coded, since the bar depends on the aspect ratio and the HUD scale setting.
        private void LateUpdate()
        {
            if (!ModConfig.matchItemBarWidth.Value)
            {
                RestoreScoreboardSize();
                return;
            }

            var itemBar = hud && hud.itemInventoryDisplay ? hud.itemInventoryDisplay.transform as RectTransform : null;
            var parent = scoreboardTransform.parent as RectTransform;
            if (!itemBar || !parent || !itemBar.gameObject.activeInHierarchy)
                return;

            itemBar.GetWorldCorners(corners);
            float left = parent.InverseTransformPoint(corners[0]).x;
            float right = parent.InverseTransformPoint(corners[2]).x;
            // Guard against a collapsed or replaced bar (HUD mods), which would squash the menu.
            if (right - left < 400f)
                return;

            if (originalOffsetMin == null)
            {
                originalOffsetMin = scoreboardTransform.offsetMin;
                originalOffsetMax = scoreboardTransform.offsetMax;
            }

            var parentRect = parent.rect;
            var offsetMin = scoreboardTransform.offsetMin;
            var offsetMax = scoreboardTransform.offsetMax;
            offsetMin.x = left - (parentRect.xMin + parentRect.width * scoreboardTransform.anchorMin.x);
            offsetMax.x = right - (parentRect.xMin + parentRect.width * scoreboardTransform.anchorMax.x);
            if ((offsetMin - scoreboardTransform.offsetMin).sqrMagnitude > 0.25f || (offsetMax - scoreboardTransform.offsetMax).sqrMagnitude > 0.25f)
            {
                scoreboardTransform.offsetMin = offsetMin;
                scoreboardTransform.offsetMax = offsetMax;
            }
        }

        private void RestoreScoreboardSize()
        {
            if (originalOffsetMin == null || !scoreboardTransform)
                return;
            scoreboardTransform.offsetMin = originalOffsetMin.Value;
            scoreboardTransform.offsetMax = originalOffsetMax.Value;
            originalOffsetMin = originalOffsetMax = null;
        }

        private void OnEnable()
        {
            if (container == null)
                TryRebuild();
            else
                StartCoroutine(ApplyNextFrame());
        }

        private void OnDisable()
        {
            // The game unpauses itself when the scoreboard closes in single player.
            pausedByUs = false;
        }

        private void TryRebuild()
        {
            if (UIFactory.TryInit())
            {
                RebuildUI();
                return;
            }

            IEnumerator<object> retry()
            {
                yield return null;
                if (isActiveAndEnabled && container == null)
                    TryRebuild();
            }
            StartCoroutine(retry());
        }
    }
}
