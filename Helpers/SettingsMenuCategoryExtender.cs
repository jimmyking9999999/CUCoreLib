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
        private const float FallbackButtonSpacing = 116f;
        private const float ScrollPixelsPerWheelStep = 48f;
        private readonly Dictionary<Button, int> buttonCategoryIndices = new Dictionary<Button, int>();

        private readonly List<Button> customButtons = new List<Button>();
        private int activeCategoryIndex;
        private SettingsMenu menu;

        private void Update()
        {
            if (menu == null || menu.content == null) return;

            var maxScroll = GetMaxScroll();
            if (maxScroll <= 0f)
            {
                ClampScrollPosition();
                return;
            }

            var viewport = menu.content.parent as RectTransform;
            if (viewport == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition)) return;

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            var anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y - scroll * ScrollPixelsPerWheelStep, 0f, maxScroll);
            menu.content.anchoredPosition = anchoredPosition;
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
            RegisterBuiltInButtons();
            RebuildButtons();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void OnTabSelected(Setting.SettingCategory category)
        {
            activeCategoryIndex = (int)category;
            SnapContentToTop();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void RefreshVisibleTab()
        {
            if (menu == null) return;

            RebuildButtons();
            menu.SelectTab(activeCategoryIndex);
        }

        private void RebuildButtons()
        {
            RemoveCustomButtons();

            var categories = ModOptionsRegistry.GetCustomCategories();
            if (menu == null || menu.buttons == null || menu.buttons.Count == 0 || categories.Count == 0) return;

            var template = menu.buttons.LastOrDefault();
            if (!template) return;

            var templateRect = template.transform as RectTransform;
            if (templateRect == null) return;

            var spacing = GetButtonSpacing();
            var parent = template.transform.parent;
            var origin = templateRect.anchoredPosition;

            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var clone = Instantiate(template.gameObject, parent, false);
                clone.name = $"CUCoreLibSettingsTab_{category.DisplayName}";
                var cloneRect = clone.transform as RectTransform;
                if (cloneRect != null) cloneRect.anchoredPosition = origin + new Vector2(spacing * (i + 1), 0f);

                var button = clone.GetComponent<Button>();
                if (!button)
                {
                    Destroy(clone);
                    continue;
                }

                button.onClick.RemoveAllListeners();
                var categoryIndex = category.CategoryIndex;
                button.onClick.AddListener(delegate { menu.SelectTab(categoryIndex); });

                var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = category.DisplayName;

                menu.buttons.Add(button);
                customButtons.Add(button);
                buttonCategoryIndices[button] = categoryIndex;
            }
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
            var builtInCount = Mathf.Min(5, menu.buttons.Count);
            for (var i = 0; i < builtInCount; i++)
            {
                var button = menu.buttons[i];
                if (button != null) buttonCategoryIndices[button] = i;
            }
        }

        private float GetButtonSpacing()
        {
            if (menu == null || menu.buttons == null || menu.buttons.Count < 2) return FallbackButtonSpacing;

            var last = menu.buttons[menu.buttons.Count - 1].transform as RectTransform;
            var previous = menu.buttons[menu.buttons.Count - 2].transform as RectTransform;
            if (last == null || previous == null) return FallbackButtonSpacing;

            var spacing = last.anchoredPosition.x - previous.anchoredPosition.x;
            return Mathf.Abs(spacing) > 0.01f ? spacing : FallbackButtonSpacing;
        }

        private void ApplyButtonSprites()
        {
            if (menu == null || menu.buttons == null) return;

            for (var i = 0; i < menu.buttons.Count; i++)
            {
                var button = menu.buttons[i];
                if (!button) continue;

                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    var isActive = buttonCategoryIndices.TryGetValue(button, out var categoryIndex) &&
                                   categoryIndex == activeCategoryIndex;
                    image.sprite = isActive ? menu.buttonOpen : menu.buttonClosed;
                }
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
    }
}