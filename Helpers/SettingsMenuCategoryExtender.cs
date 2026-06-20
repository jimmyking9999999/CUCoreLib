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

        private readonly List<Button> customButtons = new List<Button>();
        private readonly Dictionary<Button, int> buttonCategoryIndices = new Dictionary<Button, int>();
        private SettingsMenu menu;
        private int activeCategoryIndex;

        internal static void EnsureAttached(SettingsMenu menu)
        {
            if (!menu)
            {
                return;
            }

            SettingsMenuCategoryExtender helper = menu.GetComponent<SettingsMenuCategoryExtender>();
            if (!helper)
            {
                helper = menu.gameObject.AddComponent<SettingsMenuCategoryExtender>();
            }

            helper.Initialize(menu);
        }

        internal static void RefreshLiveMenu()
        {
            if (!SettingsMenu.instance)
            {
                return;
            }

            EnsureAttached(SettingsMenu.instance);
            SettingsMenuCategoryExtender helper = SettingsMenu.instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.RefreshVisibleTab();
        }

        internal void Initialize(SettingsMenu settingsMenu)
        {
            menu = settingsMenu;
            if (menu == null)
            {
                return;
            }

            if (menu.buttons == null)
            {
                menu.buttons = new List<Button>();
            }

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
            if (menu == null)
            {
                return;
            }

            RebuildButtons();
            menu.SelectTab(activeCategoryIndex);
        }

        private void Update()
        {
            if (menu == null || menu.content == null)
            {
                return;
            }

            float maxScroll = GetMaxScroll();
            if (maxScroll <= 0f)
            {
                ClampScrollPosition();
                return;
            }

            RectTransform viewport = menu.content.parent as RectTransform;
            if (viewport == null || !RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition))
            {
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            Vector2 anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y - scroll * ScrollPixelsPerWheelStep, 0f, maxScroll);
            menu.content.anchoredPosition = anchoredPosition;
        }

        private void RebuildButtons()
        {
            RemoveCustomButtons();

            List<ModOptionCategoryEntry> categories = ModOptionsRegistry.GetCustomCategories();
            if (menu == null || menu.buttons == null || menu.buttons.Count == 0 || categories.Count == 0)
            {
                return;
            }

            Button template = menu.buttons.LastOrDefault();
            if (!template)
            {
                return;
            }

            RectTransform templateRect = template.transform as RectTransform;
            if (templateRect == null)
            {
                return;
            }

            float spacing = GetButtonSpacing();
            Transform parent = template.transform.parent;
            Vector2 origin = templateRect.anchoredPosition;

            for (int i = 0; i < categories.Count; i++)
            {
                ModOptionCategoryEntry category = categories[i];
                GameObject clone = Instantiate(template.gameObject, parent, false);
                clone.name = $"CUCoreLibSettingsTab_{category.DisplayName}";
                RectTransform cloneRect = clone.transform as RectTransform;
                if (cloneRect != null)
                {
                    cloneRect.anchoredPosition = origin + new Vector2(spacing * (i + 1), 0f);
                }

                Button button = clone.GetComponent<Button>();
                if (!button)
                {
                    Destroy(clone);
                    continue;
                }

                button.onClick.RemoveAllListeners();
                int categoryIndex = category.CategoryIndex;
                button.onClick.AddListener(delegate
                {
                    menu.SelectTab(categoryIndex);
                });

                TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = category.DisplayName;
                }

                menu.buttons.Add(button);
                customButtons.Add(button);
                buttonCategoryIndices[button] = categoryIndex;
            }
        }

        private void RemoveCustomButtons()
        {
            if (menu != null && menu.buttons != null)
            {
                foreach (Button button in customButtons)
                {
                    menu.buttons.Remove(button);
                }
            }

            foreach (Button button in customButtons)
            {
                if (button)
                {
                    buttonCategoryIndices.Remove(button);
                    Destroy(button.gameObject);
                }
            }

            customButtons.Clear();
        }

        private void RegisterBuiltInButtons()
        {
            int builtInCount = Mathf.Min(5, menu.buttons.Count);
            for (int i = 0; i < builtInCount; i++)
            {
                Button button = menu.buttons[i];
                if (button != null)
                {
                    buttonCategoryIndices[button] = i;
                }
            }
        }

        private float GetButtonSpacing()
        {
            if (menu == null || menu.buttons == null || menu.buttons.Count < 2)
            {
                return FallbackButtonSpacing;
            }

            RectTransform last = menu.buttons[menu.buttons.Count - 1].transform as RectTransform;
            RectTransform previous = menu.buttons[menu.buttons.Count - 2].transform as RectTransform;
            if (last == null || previous == null)
            {
                return FallbackButtonSpacing;
            }

            float spacing = last.anchoredPosition.x - previous.anchoredPosition.x;
            return Mathf.Abs(spacing) > 0.01f ? spacing : FallbackButtonSpacing;
        }

        private void ApplyButtonSprites()
        {
            if (menu == null || menu.buttons == null)
            {
                return;
            }

            for (int i = 0; i < menu.buttons.Count; i++)
            {
                Button button = menu.buttons[i];
                if (!button)
                {
                    continue;
                }

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    bool isActive = buttonCategoryIndices.TryGetValue(button, out int categoryIndex) && categoryIndex == activeCategoryIndex;
                    image.sprite = isActive ? menu.buttonOpen : menu.buttonClosed;
                }
            }
        }

        private void SnapContentToTop()
        {
            if (menu?.content == null)
            {
                return;
            }

            Vector2 anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = 0f;
            menu.content.anchoredPosition = anchoredPosition;
        }

        private void ClampScrollPosition()
        {
            if (menu?.content == null)
            {
                return;
            }

            Vector2 anchoredPosition = menu.content.anchoredPosition;
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, 0f, GetMaxScroll());
            menu.content.anchoredPosition = anchoredPosition;
        }

        private float GetMaxScroll()
        {
            if (menu?.content == null)
            {
                return 0f;
            }

            RectTransform viewport = menu.content.parent as RectTransform;
            if (viewport == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, menu.content.sizeDelta.y - viewport.rect.height);
        }
    }
}
