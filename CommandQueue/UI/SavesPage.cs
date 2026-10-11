using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CommandQueue.UI
{
    // Each row: [load button, stretched] [gap] [overwrite][rename][delete], the gap three times the
    // spacing between the edit buttons. The header lines up with it: the name box matches the load
    // buttons, Save matches the edit buttons.
    internal class SavesPage
    {
        private const int SavesPerPage = 5;
        private const float RowHeight = 52;
        private const float FontSize = 20;
        // Narrow, but as tall as the rest of the page.
        private const float PagerHeight = RowHeight;
        private const float PagerFontSize = 14;
        private const float EditButtonWidth = 110;
        private const float EditButtonSpacing = 12;
        private const float EditGroupWidth = 3 * EditButtonWidth + 2 * EditButtonSpacing;
        private const float GroupGap = 3 * EditButtonSpacing;

        private readonly RectTransform list;
        private readonly TMP_InputField nameInput;
        private readonly TextMeshProUGUI statusLabel;
        private UIFactory.ButtonWrapper saveButton;
        private UIFactory.ButtonWrapper pageIndicator;
        private readonly List<GameObject> rows = new List<GameObject>();

        // Kept across rebuilds so reopening the page returns to the same place.
        private static int pageIndex;
        private string renamingSaveId;
        // Destructive actions (overwrite, delete) need a second click, this holds the pending one.
        private string pendingConfirmation;

        public SavesPage(RectTransform root)
        {
            var layout = UIFactory.AddVerticalLayout(root, 10);
            layout.padding = new RectOffset(24, 24, 16, 16);

            var header = CreateRow(root, "Header");
            nameInput = UIFactory.CreateInputField(header, "Save name...", SaveManager.MaxNameLength, FontSize);
            PlaceLeft(nameInput.GetComponent<RectTransform>());
            saveButton = CreateButton(header, "Save current queue", HandleSaveClicked);
            PlaceRight(saveButton.rectTransform, EditGroupWidth);

            statusLabel = UIFactory.CreateLabel(root, "", FontSize);
            statusLabel.color = UIFactory.SubtleTextColor;
            UIFactory.Layout(statusLabel, preferredHeight: 22);

            list = UIFactory.CreateRect(root, "SaveList");
            UIFactory.Layout(list, flexibleHeight: 1);
            UIFactory.AddVerticalLayout(list, 8);

            var pager = UIFactory.CreateRect(root, "Pager");
            UIFactory.Layout(pager, preferredHeight: PagerHeight);
            UIFactory.AddHorizontalLayout(pager, 6).childAlignment = TextAnchor.MiddleCenter;

            var previous = CreateButton(pager, "<", () => { pageIndex--; Refresh(); }, fontSize: PagerFontSize);
            UIFactory.Layout(previous.rectTransform, preferredWidth: 32);
            pageIndicator = CreateButton(pager, null, null, rawText: "Page 1/1", fontSize: PagerFontSize);
            pageIndicator.hgButton.interactable = false;
            UIFactory.Layout(pageIndicator.rectTransform, preferredWidth: 84);
            var next = CreateButton(pager, ">", () => { pageIndex++; Refresh(); }, fontSize: PagerFontSize);
            UIFactory.Layout(next.rectTransform, preferredWidth: 32);

            Refresh();
        }

        private static RectTransform CreateRow(Transform parent, string name)
        {
            var row = UIFactory.CreateRect(parent, name);
            UIFactory.Layout(row, preferredHeight: RowHeight);
            return row;
        }

        private static UIFactory.ButtonWrapper CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, string rawText = null, float fontSize = FontSize)
        {
            var button = UIFactory.CreateButton(parent, text: text, onClick: onClick, rawText: rawText);
            UIFactory.SetLabelStyle(button, fontSize: fontSize);
            return button;
        }

        // From the left edge up to the gap before the edit buttons.
        private static void PlaceLeft(RectTransform transform)
        {
            transform.anchorMin = new Vector2(0, 0);
            transform.anchorMax = new Vector2(1, 1);
            transform.offsetMin = Vector2.zero;
            transform.offsetMax = new Vector2(-(EditGroupWidth + GroupGap), 0);
        }

        // A fixed width against the right edge.
        private static void PlaceRight(RectTransform transform, float width, float rightOffset = 0)
        {
            transform.anchorMin = new Vector2(1, 0);
            transform.anchorMax = new Vector2(1, 1);
            transform.pivot = new Vector2(1, 0.5f);
            transform.sizeDelta = new Vector2(width, 0);
            transform.anchoredPosition = new Vector2(-rightOffset, 0);
        }

        private void HandleSaveClicked()
        {
            if (renamingSaveId != null)
            {
                SaveManager.RenameSave(renamingSaveId, nameInput.text);
                SetStatus("Renamed save.");
                renamingSaveId = null;
            }
            else
            {
                var save = SaveManager.CreateSave(nameInput.text);
                pageIndex = 0;
                SetStatus($"Saved '{save.name}' ({save.ItemCount} items).");
            }
            nameInput.text = "";
            Refresh();
        }

        private void SetStatus(string text) => statusLabel.text = text;

        public void Refresh()
        {
            foreach (var row in rows)
            {
                // Detach first: Destroy is deferred, and the layout would keep the old rows this frame.
                row.transform.SetParent(null, false);
                Object.Destroy(row);
            }
            rows.Clear();

            var saves = SaveManager.Saves;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(saves.Count / (float)SavesPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            pageIndicator.rawText = $"Page {pageIndex + 1}/{pageCount}";
            saveButton.text = renamingSaveId != null ? "Apply rename" : "Save current queue";

            if (saves.Count == 0)
            {
                var emptyLabel = UIFactory.CreateLabel(list, "No saves yet. Type a name and press Save.", FontSize);
                emptyLabel.color = UIFactory.SubtleTextColor;
                UIFactory.Layout(emptyLabel, preferredHeight: RowHeight);
                rows.Add(emptyLabel.gameObject);
                return;
            }

            foreach (var save in saves.Skip(pageIndex * SavesPerPage).Take(SavesPerPage).ToList())
                rows.Add(CreateSaveRow(save));
        }

        private GameObject CreateSaveRow(SaveManager.SavedQueue save)
        {
            string saveId = save.id;
            var row = CreateRow(list, "SaveRow");

            var loadButton = CreateButton(row, null, () =>
            {
                var result = SaveManager.LoadSave(saveId);
                var status = $"Loaded '{save.name}': {result.loadedItems} items.";
                if (result.unknownItems > 0)
                    status += $" {result.unknownItems} unknown skipped.";
                if (result.disabledTierItems > 0)
                    status += $" {result.disabledTierItems} in disabled tiers skipped.";
                SetStatus(status);
            }, rawText: $"{save.name} ({save.ItemCount} items)");
            PlaceLeft(loadButton.rectTransform);
            UIFactory.SetTooltip(loadButton.gameObject, save.name, "Replace the current queue with this save.", Color.white);

            float Offset(int index) => (2 - index) * (EditButtonWidth + EditButtonSpacing);

            var overwrite = CreateConfirmedButton(row, "overwrite:" + saveId, "Overwrite", () =>
            {
                SaveManager.OverwriteSave(saveId);
                SetStatus($"Overwrote '{save.name}' with the current queue.");
            });
            PlaceRight(overwrite.rectTransform, EditButtonWidth, Offset(0));

            var renameButton = CreateButton(row, renamingSaveId == saveId ? "Cancel" : "Rename", () =>
            {
                if (renamingSaveId == saveId)
                {
                    renamingSaveId = null;
                    nameInput.text = "";
                }
                else
                {
                    renamingSaveId = saveId;
                    nameInput.text = save.name;
                    nameInput.Select();
                    nameInput.ActivateInputField();
                }
                Refresh();
            });
            PlaceRight(renameButton.rectTransform, EditButtonWidth, Offset(1));

            var delete = CreateConfirmedButton(row, "delete:" + saveId, "Delete", () =>
            {
                SaveManager.DeleteSave(saveId);
                SetStatus($"Deleted '{save.name}'.");
                if (renamingSaveId == saveId)
                    renamingSaveId = null;
            });
            PlaceRight(delete.rectTransform, EditButtonWidth, Offset(2));

            return row.gameObject;
        }

        private UIFactory.ButtonWrapper CreateConfirmedButton(Transform row, string confirmationKey, string label, System.Action action)
        {
            bool confirming = pendingConfirmation == confirmationKey;
            var button = CreateButton(row, confirming ? "Confirm?" : label, () =>
            {
                if (pendingConfirmation == confirmationKey)
                {
                    action();
                    pendingConfirmation = null;
                }
                else
                    pendingConfirmation = confirmationKey;
                Refresh();
            });
            if (confirming)
                UIFactory.MarkButton(button, true);
            return button;
        }
    }
}
