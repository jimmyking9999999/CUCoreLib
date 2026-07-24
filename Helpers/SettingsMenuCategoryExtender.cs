using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Registries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    internal sealed class SettingsMenuCategoryExtender : MonoBehaviour
    {
        private const int BuiltInTabCount = 5;
        private const float MinimumInterButtonGap = 2f;
        private const float ScrollPixelsPerWheelStep = 48f;
        private readonly Dictionary<Button, int> buttonCategoryIndices = new Dictionary<Button, int>();

        private readonly List<Button> customButtons = new List<Button>();
        private readonly List<TMP_Dropdown> cachedDropdowns = new List<TMP_Dropdown>();
        private readonly List<Vector2> builtInAnchoredPositions = new List<Vector2>();
        private readonly List<Vector2> builtInSizes = new List<Vector2>();
        private int activeCategoryIndex;
        private string activeOwnedCategoryKey;
        private bool capturedBuiltInLayout;
        private SettingsMenu menu;

        private void Update()
        {
            if (!menu || !menu.content) return;

            if (IsMouseOverExpandedDropdown()) return;

            var maxScroll = GetMaxScroll();
            if (maxScroll <= 0f)
            {
                ClampScrollPosition();
                return;
            }

            var viewport = menu.content.parent as RectTransform;
            if (!viewport ||
                !RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition)) return;

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            var anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y - scroll * ScrollPixelsPerWheelStep, 0f, maxScroll);
            menu.content.anchoredPosition = anchoredPosition;
        }

        private bool IsMouseOverExpandedDropdown()
        {
            var mousePos = Input.mousePosition;
            return (from dd in cachedDropdowns
                where dd && dd.IsExpanded && dd.template
                select dd.template).Any(templateRect =>
                templateRect && templateRect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(templateRect, mousePos));
        }

        // fixes dropdown templates created by the game's SettingsMenu.
        // vanilla prefab has a cramped viewport (only ~4 items visible)
        internal void FixDropdownsInContent(Transform content)
        {
            cachedDropdowns.Clear();
            if (!content) return;

            foreach (var dropdown in content.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (!dropdown) continue;
                cachedDropdowns.Add(dropdown);
                FixDropdown(dropdown);
            }
        }

        // the overlay is too strange, i don't get it lol
        private static void FixDropdown(TMP_Dropdown dropdown)
        {
            var template = dropdown.template;
            if (!template) return;

            var templateCanvas = template.GetComponent<Canvas>();
            if (templateCanvas)
                templateCanvas.overrideSorting = true;

            var scrollRect = template.GetComponent<ScrollRect>();
            if (!scrollRect)
                scrollRect = template.gameObject.AddComponent<ScrollRect>();

            var viewport = template.Find("Viewport");
            if (viewport)
            {
                scrollRect.viewport = viewport as RectTransform;
                var viewportRect = viewport as RectTransform;
                if (viewportRect)
                    viewportRect.sizeDelta = new Vector2(viewportRect.sizeDelta.x, 200f);

                var content = viewport.Find("Content");
                if (content)
                    scrollRect.content = content as RectTransform;
            }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
        }

        internal static void EnsureAttached(SettingsMenu menu)
        {
            if (!menu) return;

            var helper = menu.GetComponent<SettingsMenuCategoryExtender>();
            if (!helper) helper = menu.gameObject.AddComponent<SettingsMenuCategoryExtender>();

            helper.Initialize(menu);
        }

        internal static void RefreshLiveMenu()
        {
            if (!SettingsMenu.instance) return;

            EnsureAttached(SettingsMenu.instance);
            var helper = SettingsMenu.instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.RefreshVisibleTab();
        }

        internal void Initialize(SettingsMenu settingsMenu)
        {
            menu = settingsMenu;
            if (menu == null) return;

            if (menu.buttons == null) menu.buttons = new List<Button>();

            activeCategoryIndex = Mathf.Clamp(activeCategoryIndex, 0, int.MaxValue);
            CaptureBuiltInLayoutIfNeeded();
            RegisterBuiltInButtons();
            RebuildButtons();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void OnTabSelected(Setting.SettingCategory category)
        {
            activeCategoryIndex = (int)category;
            if (ModOptionsRegistry.TryGetOwnedCustomCategory(category, out var entry) && entry != null)
                activeOwnedCategoryKey = ModOptionsRegistry.NormalizeCustomCategoryKey(entry.DisplayName);
            else
                activeOwnedCategoryKey = null;
            SnapContentToTop();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void RefreshVisibleTab()
        {
            if (menu == null) return;

            RebuildButtons();
            if (!string.IsNullOrWhiteSpace(activeOwnedCategoryKey) &&
                ModOptionsRegistry.TryGetOwnedCustomCategory(activeOwnedCategoryKey, out var activeEntry) &&
                activeEntry != null)
                activeCategoryIndex = activeEntry.CategoryIndex;
            menu.SelectTab(activeCategoryIndex);
        }

        private void RebuildButtons()
        {
            RemoveCustomButtons();
            buttonCategoryIndices.Clear();
            RestoreBuiltInLayout();
            RegisterBuiltInButtons();
            ModOptionsRegistry.ReconcileCustomCategoryOwnership(Settings.settings);

            var categories = ModOptionsRegistry.GetCustomCategories();
            if (menu == null || menu.buttons == null || menu.buttons.Count == 0)
            {
                return;
            }

            if (categories.Count == 0)
            {
                return;
            }

            var template = FindTemplateButton();
            if (!template) return;

            var templateRect = template.transform as RectTransform;
            if (templateRect == null) return;

            var parent = template.transform.parent;
            var origin = templateRect.anchoredPosition;

            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var clone = Instantiate(template.gameObject, parent, false);
                clone.name = $"CUCoreLibSettingsTab_{category.DisplayName}";
                var cloneRect = clone.transform as RectTransform;
                if (cloneRect != null) cloneRect.anchoredPosition = origin;

                var button = clone.GetComponent<Button>();
                if (!button)
                {
                    Destroy(clone);
                    continue;
                }

                button.onClick.RemoveAllListeners();
                var categoryIndex = category.CategoryIndex;
                button.onClick.AddListener(delegate { menu.SelectTab(categoryIndex); });

                var label = clone.GetComponentInChildren<TextMeshProUGUI>(false)
                            ?? clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label)
                {
                    label.text = category.DisplayName;
                    foreach (var localizer in label.GetComponents<MonoBehaviour>()
                                 .Where(component => component && component.GetType().Name.Contains("Local")))
                    {
                        Destroy(localizer);
                    }

                    NormalizeTabLabel(label);
                }

                menu.buttons.Add(button);
                customButtons.Add(button);
                buttonCategoryIndices[button] = categoryIndex;
            }

            ReflowButtonsIntoOriginalBand();
        }

        private void RemoveCustomButtons()
        {
            if (menu != null && menu.buttons != null)
                foreach (var button in customButtons)
                    menu.buttons.Remove(button);

            foreach (var button in customButtons)
                if (button)
                {
                    buttonCategoryIndices.Remove(button);
                    Destroy(button.gameObject);
                }

            customButtons.Clear();
        }

        private void RegisterBuiltInButtons()
        {
            var builtInCount = Mathf.Min(BuiltInTabCount, menu.buttons.Count);
            for (var i = 0; i < builtInCount; i++)
            {
                var button = menu.buttons[i];
                if (button != null) buttonCategoryIndices[button] = i;
            }
        }

        private Button FindTemplateButton()
        {
            if (menu == null || menu.buttons == null || menu.buttons.Count == 0) return null;

            return menu.buttons.LastOrDefault(button => button != null && !customButtons.Contains(button)) ??
                   menu.buttons.LastOrDefault();
        }

        private void ApplyButtonSprites()
        {
            if (menu == null || menu.buttons == null) return;

            foreach (var button in menu.buttons)
            {
                if (!button) continue;

                var image = button.GetComponent<Image>();
                if (image == null) continue;
                if (!buttonCategoryIndices.ContainsKey(button)) continue;
                var isActive = buttonCategoryIndices.TryGetValue(button, out var categoryIndex)
                               && categoryIndex == activeCategoryIndex;
                image.sprite = isActive ? menu.buttonOpen : menu.buttonClosed;
            }
        }

        private void SnapContentToTop()
        {
            if (menu?.content == null) return;

            var anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = 0f;
            menu.content.anchoredPosition = anchoredPosition;
        }

        private void ClampScrollPosition()
        {
            if (menu?.content == null) return;

            var anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, 0f, GetMaxScroll());
            menu.content.anchoredPosition = anchoredPosition;
        }

        private float GetMaxScroll()
        {
            if (menu?.content == null) return 0f;

            var viewport = menu.content.parent as RectTransform;
            if (viewport == null) return 0f;

            return Mathf.Max(0f, menu.content.sizeDelta.y - viewport.rect.height);
        }

        private void CaptureBuiltInLayoutIfNeeded()
        {
            if (capturedBuiltInLayout || menu == null || menu.buttons == null || menu.buttons.Count < BuiltInTabCount)
            {
                return;
            }

            builtInAnchoredPositions.Clear();
            builtInSizes.Clear();

            for (var i = 0; i < BuiltInTabCount; i++)
            {
                var rect = menu.buttons[i] != null ? menu.buttons[i].transform as RectTransform : null;
                if (rect == null)
                {
                    builtInAnchoredPositions.Clear();
                    builtInSizes.Clear();
                    return;
                }

                builtInAnchoredPositions.Add(rect.anchoredPosition);
                builtInSizes.Add(rect.sizeDelta);
            }

            capturedBuiltInLayout = builtInAnchoredPositions.Count == BuiltInTabCount;
        }

        private void RestoreBuiltInLayout()
        {
            if (!capturedBuiltInLayout || menu == null || menu.buttons == null) return;

            var builtInCount = Mathf.Min(Mathf.Min(BuiltInTabCount, menu.buttons.Count), builtInAnchoredPositions.Count);
            for (var i = 0; i < builtInCount; i++)
            {
                var rect = menu.buttons[i] != null ? menu.buttons[i].transform as RectTransform : null;
                if (rect == null) continue;

                rect.anchoredPosition = builtInAnchoredPositions[i];
                rect.sizeDelta = builtInSizes[i];
            }
        }

        private void ReflowButtonsIntoOriginalBand()
        {
            if (!capturedBuiltInLayout || menu == null || menu.buttons == null || menu.buttons.Count == 0 ||
                builtInAnchoredPositions.Count < BuiltInTabCount || builtInSizes.Count < BuiltInTabCount)
                return;

            var totalButtons = menu.buttons.Count;
            var firstLeft = builtInAnchoredPositions[0].x - builtInSizes[0].x * 0.5f;
            var lastRight = builtInAnchoredPositions[BuiltInTabCount - 1].x +
                            builtInSizes[BuiltInTabCount - 1].x * 0.5f;
            var availableWidth = Mathf.Max(0f, lastRight - firstLeft);
            var gap = totalButtons > 1 ? MinimumInterButtonGap : 0f;
            var targetWidth = totalButtons > 0
                ? Mathf.Max(1f, (availableWidth - gap * (totalButtons - 1)) / totalButtons)
                : availableWidth;
            var currentX = firstLeft;

            for (var i = 0; i < totalButtons; i++)
            {
                var button = menu.buttons[i];
                var rect = button != null ? button.transform as RectTransform : null;
                if (rect == null) continue;

                var baselineSize = i < builtInSizes.Count ? builtInSizes[i] : builtInSizes[builtInSizes.Count - 1];
                rect.sizeDelta = new Vector2(targetWidth, baselineSize.y);
                rect.anchoredPosition = new Vector2(currentX + targetWidth * 0.5f, rect.anchoredPosition.y);
                currentX += targetWidth + gap;

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label) NormalizeTabLabel(label);
            }
        }

        private static void NormalizeTabLabel(TMP_Text label)
        {
            if (label == null) return;

            var labelRect = label.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                labelRect.anchoredPosition = Vector2.zero;
            }

            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
        }
    }
}
