using System.IO;
using System.Linq;
using System.Reflection;
using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CommandQueue.UI
{
    // Builds UI elements out of the game's own command menu prefabs, so everything the mod adds
    // looks like the rest of RoR2's UI.
    internal static class UIFactory
    {
        public static GameObject CancelButtonPrefab { get; private set; }
        public static PickupPickerPanel PickupPickerPanelPrefab { get; private set; }
        public static GameObject ItemButtonPrefab { get; private set; }
        public static GameObject ItemContainerPrefab { get; private set; }
        public static Sprite LockedIcon { get; private set; }
        public static Texture2D RepeatIcon { get; private set; }
        public static ColorBlock NormalColors { get; private set; }
        public static ColorBlock SelectedColors { get; private set; }

        private static TextMeshProUGUI templateText;

        // Gold highlight used for selected tabs and enabled toggles.
        public static readonly Color SelectedColor = new Color(0.7f, 0.55f, 0.1f, 1f);
        private static readonly Color selectedColorMult = new Color(0.3f, 0.3f, 0.3f);
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color SubtleTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        public static bool Ready => CancelButtonPrefab;

        // The command cube prefab loads asynchronously, so this is retried until it succeeds.
        public static bool TryInit()
        {
            if (Ready)
                return true;

            var commandUIPrefab = CommandQueue.CommandUIPrefab;
            if (!commandUIPrefab)
                return false;

            var cancelButton = commandUIPrefab.GetComponentsInChildren<HGButton>(true).FirstOrDefault(a => a.name == "CancelButton");
            if (!cancelButton)
                return false;

            PickupPickerPanelPrefab = commandUIPrefab.GetComponent<PickupPickerPanel>();
            ItemButtonPrefab = PickupPickerPanelPrefab.buttonPrefab;
            ItemContainerPrefab = PickupPickerPanelPrefab.buttonContainer.gameObject;
            LockedIcon = LegacyResourcesAPI.Load<Sprite>("Textures/MiscIcons/texUnlockIcon");
            templateText = cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);

            var colors = cancelButton.colors;
            NormalColors = colors;
            colors.disabledColor *= selectedColorMult;
            colors.pressedColor *= selectedColorMult;
            colors.highlightedColor = colors.normalColor = SelectedColor;
            SelectedColors = colors;

            if (!RepeatIcon)
            {
                RepeatIcon = new Texture2D(0, 0);
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CommandQueue.repeat-solid.png"))
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    RepeatIcon.LoadImage(memoryStream.ToArray());
                }
            }

            CancelButtonPrefab = cancelButton.gameObject;
            return true;
        }

        public struct ButtonWrapper
        {
            public HGButton hgButton { get; private set; }
            public TextMeshProUGUI textComponent { get; private set; }
            public RectTransform rectTransform { get; private set; }
            public LanguageTextMeshController languageTextMeshController { get; private set; }

            public ButtonWrapper(HGButton button)
            {
                hgButton = button;
                textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                rectTransform = button.GetComponent<RectTransform>();
                languageTextMeshController = button.GetComponent<LanguageTextMeshController>();
            }

            public GameObject gameObject => hgButton.gameObject;

            public event UnityAction onClick
            {
                add => hgButton.onClick.AddListener(value);
                remove => hgButton.onClick.RemoveListener(value);
            }

            // A language token, or plain text that isn't a token (Language returns unknown tokens as-is).
            public string text
            {
                set
                {
                    if (languageTextMeshController && languageTextMeshController.enabled)
                        languageTextMeshController.token = value;
                    else if (textComponent)
                        textComponent.text = value;
                }
            }

            // For player-provided text (save names) and rich text, which must not go through the language system.
            public string rawText
            {
                set
                {
                    if (languageTextMeshController)
                        languageTextMeshController.enabled = false;
                    if (textComponent)
                        textComponent.text = value;
                }
            }
        }

        public static ButtonWrapper CreateButton(Transform parent, string text = null, UnityAction onClick = null, string rawText = null)
        {
            var buttonObject = Object.Instantiate(CancelButtonPrefab, parent);
            buttonObject.name = "CommandQueueButton";
            HGButton button = buttonObject.GetComponent<HGButton>();

            button.onSelect = new UnityEvent();
            button.onDeselect = new UnityEvent();
            button.onClick = new Button.ButtonClickedEvent();

            // The skin resizes the button to its recommended size when it wakes up. Our pages are built
            // while hidden, so that would happen after our own sizing and undo it.
            if (buttonObject.TryGetComponent<ButtonSkinController>(out var skin))
            {
                skin.useRecommendedButtonWidth = false;
                skin.useRecommendedButtonHeight = false;
            }

            var wrapped = new ButtonWrapper(button);
            if (text != null) wrapped.text = text;
            if (rawText != null) wrapped.rawText = rawText;
            if (onClick != null) wrapped.onClick += onClick;
            return wrapped;
        }

        // The button prefab's ButtonSkinController re-applies the skin's font size, color and alignment to
        // its label every frame. Buttons that need a different label style take it over from the skin.
        public static void SetLabelStyle(ButtonWrapper button, float? fontSize = null, TextAlignmentOptions? alignment = null, Color? color = null)
        {
            if (button.gameObject.TryGetComponent<ButtonSkinController>(out var skin))
            {
                skin.useRecommendedLabel = false;
                skin.useRecommendedAlignment = false;
            }
            var label = button.textComponent;
            if (!label)
                return;
            if (fontSize.HasValue)
            {
                label.enableAutoSizing = false;
                label.fontSize = fontSize.Value;
            }
            if (alignment.HasValue)
                label.alignment = alignment.Value;
            if (color.HasValue)
                label.color = color.Value;
        }

        public static void MarkButton(ButtonWrapper button, bool selected)
        {
            if (!button.hgButton)
                return;
            button.hgButton.colors = selected ? SelectedColors : NormalColors;
            // Only the tint: HGButton animates this outline's alpha and scale itself. Setting alpha to 1
            // here made every re-marked button play the hover grow-and-fade.
            var outline = button.hgButton.imageOnHover;
            if (outline)
            {
                var tint = selected ? selectedColorMult : Color.white;
                outline.color = new Color(tint.r, tint.g, tint.b, outline.color.a);
            }
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize = 18f, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            if (templateText)
            {
                label.font = templateText.font;
                label.fontSharedMaterial = templateText.fontSharedMaterial;
                label.color = templateText.color;
            }
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = text;
            return label;
        }

        // A tier's color bar. Void tiers are split between the void color and the tier they mirror
        // (Void Green: void + green), since the game gives all four void tiers the same color.
        // Vertical bars split top/bottom, horizontal ones left/right.
        public static RectTransform CreateTierStripe(Transform parent, ItemTier tier, bool vertical)
        {
            var stripe = CreateRect(parent, "TierStripe");
            var baseTier = TierInfo.GetMirroredTier(tier);
            if (baseTier == null)
            {
                Stretch(CreateImage(stripe, TierInfo.GetColor(tier), "Color").rectTransform);
                return stripe;
            }

            var voidPart = CreateImage(stripe, TierInfo.GetColor(tier), "VoidColor").rectTransform;
            var basePart = CreateImage(stripe, TierInfo.GetColor(baseTier.Value), "BaseColor").rectTransform;
            Stretch(voidPart);
            Stretch(basePart);
            if (vertical)
            {
                voidPart.anchorMin = new Vector2(0, 0.5f);
                basePart.anchorMax = new Vector2(1, 0.5f);
            }
            else
            {
                voidPart.anchorMax = new Vector2(0.5f, 1);
                basePart.anchorMin = new Vector2(0.5f, 0);
            }
            return stripe;
        }

        // The ItemIcon prefab's art (its ItemImage) is offset up and left from the icon's own rect, which is
        // why the game spaces icons on a grid wider than the icon. Scale and shift the icon so the art
        // itself fills a size x size square centered in the parent, whatever the prefab's numbers are.
        public static void FitItemIcon(ItemIcon icon, float size)
        {
            var root = (RectTransform)icon.transform;
            var art = icon.image ? icon.image.rectTransform : root;
            float artSize = Mathf.Max(art.rect.width, art.rect.height);
            if (artSize <= 0)
                artSize = Mathf.Max(root.rect.width, 1);

            Vector2 artCenter = root.InverseTransformPoint(art.TransformPoint(art.rect.center));
            Vector2 offset = artCenter - root.rect.center;

            float scale = size / artSize;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.localScale = new Vector3(scale, scale, 1);
            root.anchoredPosition = -offset * scale;
        }

        public static Image CreateImage(Transform parent, Color color, string name = "Image")
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            return rectObject.GetComponent<RectTransform>();
        }

        public static TMP_InputField CreateInputField(Transform parent, string placeholder, int characterLimit, float fontSize = 18f)
        {
            var root = CreateImage(parent, new Color(0.1f, 0.1f, 0.1f, 0.9f), "InputField");
            root.raycastTarget = true;

            var textArea = CreateRect(root.transform, "TextArea");
            textArea.gameObject.AddComponent<RectMask2D>();
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(10, 2);
            textArea.offsetMax = new Vector2(-10, -2);

            TextMeshProUGUI CreateText(string text, Color color)
            {
                var label = CreateLabel(textArea, text, fontSize, TextAlignmentOptions.MidlineLeft);
                label.color = color;
                Stretch(label.rectTransform);
                return label;
            }

            var placeholderText = CreateText(placeholder, new Color(0.5f, 0.5f, 0.5f, 0.8f));
            var inputText = CreateText("", Color.white);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.fontAsset = inputText.font;
            input.pointSize = fontSize;
            input.characterLimit = characterLimit;
            return input;
        }

        // A vertical scroll view: mouse wheel and a thin scrollbar, content grows downwards.
        public static RectTransform CreateScrollView(Transform parent, string name, out ScrollRect scrollRect)
        {
            var root = CreateRect(parent, name);
            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.inertia = false;

            var viewport = CreateRect(root, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-10, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Transparent, so the wheel scrolls anywhere over the view, not only over buttons.
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarTrack = CreateImage(root, PanelColor, "Scrollbar");
            scrollbarTrack.raycastTarget = true;
            var trackTransform = scrollbarTrack.rectTransform;
            trackTransform.anchorMin = new Vector2(1, 0);
            trackTransform.anchorMax = new Vector2(1, 1);
            trackTransform.pivot = new Vector2(1, 0.5f);
            trackTransform.sizeDelta = new Vector2(6, 0);

            var slidingArea = CreateRect(trackTransform, "SlidingArea");
            Stretch(slidingArea);
            var handle = CreateImage(slidingArea, UIFactory.SubtleTextColor, "Handle");
            handle.raycastTarget = true;
            Stretch(handle.rectTransform);

            var scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 4;

            return content;
        }

        public static void SetTooltip(GameObject target, string title, string body, Color titleColor)
        {
            if (!target.TryGetComponent<TooltipProvider>(out var tooltip))
                tooltip = target.AddComponent<TooltipProvider>();
            tooltip.overrideTitleText = title;
            tooltip.overrideBodyText = body;
            tooltip.titleColor = titleColor;
            tooltip.bodyColor = SubtleTextColor;
            tooltip.enabled = true;
        }

        public static LayoutElement Layout(Component component, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            if (!component.TryGetComponent<LayoutElement>(out var element))
                element = component.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0;
            element.minHeight = preferredHeight > 0 ? preferredHeight : -1;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(Component component, float spacing = 5)
        {
            var layout = component.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static VerticalLayoutGroup AddVerticalLayout(Component component, float spacing = 5)
        {
            var layout = component.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void Stretch(RectTransform transform)
        {
            transform.anchorMin = Vector2.zero;
            transform.anchorMax = Vector2.one;
            transform.offsetMin = Vector2.zero;
            transform.offsetMax = Vector2.zero;
        }

        public static void SetAnchors(RectTransform transform, float xMin, float yMin, float xMax, float yMax)
        {
            transform.anchorMin = new Vector2(xMin, yMin);
            transform.anchorMax = new Vector2(xMax, yMax);
            transform.anchoredPosition = Vector2.zero;
            transform.sizeDelta = Vector2.zero;
        }
    }
}
