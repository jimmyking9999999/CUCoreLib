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
        private const float RailWidth = 240f;

        private const float FramePadding = 10f;

        // Frame's own left border tucks under the window's (looks better then a gap)
        private const float RailEdgeGap = -FramePadding;
        private const float RailButtonHeight = 48f;
        private const float RailSpacing = 6f;
        private const float ScrollbarWidth = 20f;
        private const float ScrollbarGap = 6f;
        private const float RailWheelRowStep = RailButtonHeight + RailSpacing;
        private const float RailScrollTime = 0.18f;
        private const float ScrollPixelsPerWheelStep = 48f;
        private const string RailFrameName = "CUCoreLibModOptionTabFrame";
        private const string VanillaScrollbarPath = "Scroll/Scrollbar Vertical";
        private const string RailName = "CUCoreLibModOptionTabs";
        private const string CustomTabNamePrefix = "CUCoreLibSettingsTab_";

        private readonly Dictionary<Button, int> buttonCategoryIndices =
            new Dictionary<Button, int>();

        private readonly List<TMP_Dropdown> cachedDropdowns = new List<TMP_Dropdown>();
        private int activeCategoryIndex;
        private string activeOwnedCategoryKey;
        private GameObject frame;
        private SettingsMenu menu;
        private RectTransform panel;
        private Vector2 panelHomePosition;
        private RectTransform railContent;
        private ScrollRect railScroll;
        private float railScrollTarget = 1f;
        private float railScrollVelocity;
        private RectTransform railViewport;

        private void Update()
        {
            if (!menu || !menu.content) return;

            ClampScrollPosition();
            EaseRailToTarget();

            if (IsMouseOverExpandedDropdown()) return;

            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            if (railScroll
                && railViewport
                && RectTransformUtility.RectangleContainsScreenPoint(railViewport,
                    Input.mousePosition))
            {
                StepRail(wheel);
                return;
            }

            var viewport = menu.content.parent as RectTransform;
            if (!viewport
                || !RectTransformUtility.RectangleContainsScreenPoint(viewport,
                    Input.mousePosition)) return;

            ScrollBy(menu.content, viewport, wheel);
        }

        private bool IsMouseOverExpandedDropdown()
        {
            var mousePos = Input.mousePosition;
            return (
                from dd in cachedDropdowns
                where dd && dd.IsExpanded && dd.template
                select dd.template).Any(templateRect =>
                templateRect
                && templateRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(templateRect,
                    mousePos));
        }

        // Fix cloned viewports/scroll rects
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
            if (!helper)
                helper = menu.gameObject.AddComponent<SettingsMenuCategoryExtender>();

            helper.Initialize(menu);
        }

        internal static void RefreshLiveMenu()
        {
            if (!SettingsMenu.instance) return;

            EnsureAttached(SettingsMenu.instance);
            var helper =
                SettingsMenu.instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.RefreshVisibleTab();
        }

        internal void Initialize(SettingsMenu settingsMenu)
        {
            menu = settingsMenu;
            if (menu == null) return;

            if (menu.buttons == null) menu.buttons = new List<Button>();

            RebuildRail();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void OnTabSelected(Setting.SettingCategory category)
        {
            activeCategoryIndex = (int)category;
            if (ModOptionsRegistry.TryGetOwnedCustomCategory(category, out var entry)
                && entry != null)
                activeOwnedCategoryKey =
                    ModOptionsRegistry.NormalizeCustomCategoryKey(entry.DisplayName);
            else
                activeOwnedCategoryKey = null;
            SnapContentToTop();
            ApplyButtonSprites();
            ClampScrollPosition();
        }

        internal void RefreshVisibleTab()
        {
            if (menu == null) return;

            RebuildRail();
            if (!string.IsNullOrWhiteSpace(activeOwnedCategoryKey)
                && ModOptionsRegistry.TryGetOwnedCustomCategory(activeOwnedCategoryKey,
                    out var activeEntry)
                && activeEntry != null)
                activeCategoryIndex = activeEntry.CategoryIndex;
            // Prevent snapping
            var scrollY = menu.content != null
                ? menu.content.anchoredPosition.y
                : 0f;
            menu.SelectTab(activeCategoryIndex);
            if (menu.content != null)
            {
                var anchoredPosition = menu.content.anchoredPosition;
                anchoredPosition.y = scrollY;
                menu.content.anchoredPosition = anchoredPosition;
                ClampScrollPosition();
            }
        }

        private void RebuildRail()
        {
            DestroyRail();
            if (menu == null) return;

            ModOptionsRegistry.ReconcileCustomCategoryOwnership(Settings.settings);

            var categories = ModOptionsRegistry.GetCustomCategories();
            if (categories.Count == 0) return;

            var template = FindTemplateButton();
            if (!template) return;

            if (!BuildRail(template)) return;

            foreach (var category in categories)
            {
                var clone = Instantiate(template.gameObject, railContent, false);
                clone.name = CustomTabNamePrefix + category.DisplayName;

                var button = clone.GetComponent<Button>();
                if (!button)
                {
                    Destroy(clone);
                    continue;
                }

                button.onClick = new Button.ButtonClickedEvent();
                var categoryIndex = category.CategoryIndex;
                button.onClick.AddListener(delegate { menu.SelectTab(categoryIndex); });

                var templateLabel =
                    template.GetComponentInChildren<TextMeshProUGUI>(true);
                var templateFontSize = templateLabel
                    ? templateLabel.fontSize
                    : 24f;

                var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label)
                {
                    label.text = category.DisplayName;
                    foreach (var localizer in label.GetComponents<MonoBehaviour>()
                        .Where(component =>
                            component && component.GetType().Name.Contains("Local")))
                        Destroy(localizer);

                    NormalizeTabLabel(label, templateFontSize);
                }

                var layout = clone.AddComponent<LayoutElement>();
                layout.minHeight = RailButtonHeight;
                layout.preferredHeight = RailButtonHeight;

                buttonCategoryIndices[button] = categoryIndex;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(railContent);
            railScrollVelocity = 0f;
            railScroll.verticalNormalizedPosition = railScrollTarget;
        }

        private bool BuildRail(Button template)
        {
            var rootRect = menu.transform as RectTransform;
            var panelRect = template.transform.parent as RectTransform;
            if (rootRect == null || panelRect == null) return false;

            if (panel != panelRect)
            {
                panel = panelRect;
                panelHomePosition = panelRect.anchoredPosition;
            }

            var corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            var panelTopLeft = rootRect.InverseTransformPoint(corners[1]);
            var panelTopRight = rootRect.InverseTransformPoint(corners[2]);
            var panelBottomLeft = rootRect.InverseTransformPoint(corners[0]);

            // The rail hangs past the panel's right edge, so slide the panel left by half of that
            // overhang. Panel and rail then read as one centred block.
            var overhang = Mathf.Min(RailWidth + RailEdgeGap,
                rootRect.rect.xMax - panelTopRight.x);
            var slide = overhang * 0.5f;
            panelRect.anchoredPosition = panelHomePosition - Vector2.right * slide;
            panelTopLeft -= Vector3.right * slide;
            panelTopRight -= Vector3.right * slide;
            panelBottomLeft -= Vector3.right * slide;

            frame = new GameObject(RailFrameName, typeof(RectTransform));
            frame.transform.SetParent(rootRect, false);
            frame.transform.SetAsFirstSibling();

            var frameRect = frame.transform as RectTransform;
            if (frameRect != null)
            {
                frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                frameRect.pivot = new Vector2(1f, 1f);
                frameRect.sizeDelta =
                    new Vector2(RailWidth, panelTopLeft.y - panelBottomLeft.y);
                frameRect.anchoredPosition = new Vector2(
                    Mathf.Min(panelTopRight.x + RailEdgeGap + RailWidth,
                        rootRect.rect.xMax)
                    - rootRect.rect.center.x,
                    panelTopLeft.y - rootRect.rect.center.y);

                CopyImage(panelRect.GetComponent<Image>(), frameRect);

                var rail = new GameObject(RailName, typeof(RectTransform),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                rail.transform.SetParent(frameRect, false);

                var railRect = rail.transform as RectTransform;
                if (railRect != null)
                {
                    railRect.anchorMin = Vector2.zero;
                    railRect.anchorMax = Vector2.one;
                    railRect.offsetMin = new Vector2(FramePadding, FramePadding);
                    railRect.offsetMax =
                        new Vector2(-(FramePadding + ScrollbarWidth + ScrollbarGap),
                            -FramePadding);

                    railViewport = railRect;

                    railScroll = railRect.GetComponent<ScrollRect>();
                    railScroll.horizontal = false;
                    railScroll.vertical = true;
                    railScroll.movementType = ScrollRect.MovementType.Clamped;
                    railScroll.inertia = false;
                    railScroll.viewport = railRect;

                    var contentGo = new GameObject("Content", typeof(RectTransform),
                        typeof(VerticalLayoutGroup),
                        typeof(ContentSizeFitter));
                    contentGo.transform.SetParent(railRect, false);

                    railContent = contentGo.transform as RectTransform;
                    if (railContent != null)
                    {
                        railContent.anchorMin = new Vector2(0f, 1f);
                        railContent.anchorMax = new Vector2(1f, 1f);
                        railContent.pivot = new Vector2(0.5f, 1f);
                        railContent.anchoredPosition = Vector2.zero;
                        railContent.sizeDelta = Vector2.zero;

                        railScroll.content = railContent;
                    }

                    var layoutGroup = contentGo.GetComponent<VerticalLayoutGroup>();
                    layoutGroup.childAlignment = TextAnchor.UpperCenter;
                    layoutGroup.spacing = RailSpacing;
                    layoutGroup.childControlWidth = true;
                    layoutGroup.childForceExpandWidth = true;
                    layoutGroup.childControlHeight = true;
                    layoutGroup.childForceExpandHeight = false;

                    contentGo.GetComponent<ContentSizeFitter>().verticalFit =
                        ContentSizeFitter.FitMode.PreferredSize;
                }

                AttachScrollbar(panelRect, frameRect);
            }

            return true;
        }

        private void AttachScrollbar(RectTransform panelRect, RectTransform frameRect)
        {
            var source = panelRect.Find(VanillaScrollbarPath) as RectTransform;
            if (!source) return;

            // Cloning the window's own scrollbar keeps the handle art and track metrics identical.
            var clone = Instantiate(source.gameObject, frameRect, false);
            var rect = clone.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.offsetMin =
                    new Vector2(-(FramePadding + ScrollbarWidth), FramePadding);
                rect.offsetMax = new Vector2(-FramePadding, -FramePadding);
            }

            railScroll.verticalScrollbar = clone.GetComponent<Scrollbar>();
            railScroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
        }

        private static void CopyImage(Image source, RectTransform target)
        {
            if (!source) return;

            var image = target.gameObject.AddComponent<Image>();
            image.sprite = source.sprite;
            image.type = source.type;
            image.material = source.material;
            image.color = source.color;
            image.raycastTarget = false;
        }

        private void DestroyRail()
        {
            buttonCategoryIndices.Clear();
            railContent = null;
            railViewport = null;
            railScroll = null;

            if (panel)
            {
                panel.anchoredPosition = panelHomePosition;
                panel = null;
            }

            if (!frame) return;

            Destroy(frame);
            frame = null;
        }

        private Button FindTemplateButton()
        {
            if (menu == null || menu.buttons == null) return null;

            return menu.buttons.FirstOrDefault(button => button != null);
        }

        private void ApplyButtonSprites()
        {
            if (menu == null) return;

            foreach (var pair in buttonCategoryIndices)
            {
                if (!pair.Key) continue;

                var image = pair.Key.GetComponent<Image>();
                if (image)
                    image.sprite = pair.Value == activeCategoryIndex
                        ? menu.buttonOpen
                        : menu.buttonClosed;
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
            if (!menu?.content) return;

            ScrollBy(menu.content, menu.content.parent as RectTransform, 0f);
        }

        private void StepRail(float wheel)
        {
            if (!railContent || !railViewport) return;

            var range = railContent.rect.height - railViewport.rect.height;
            if (range <= 0f)
            {
                railScrollTarget = 1f;
                return;
            }

            railScrollTarget =
                Mathf.Clamp01(railScrollTarget + wheel * RailWheelRowStep / range);
        }

        // Normalised position is the only ScrollRect surface that moves the content and the
        // scrollbar together, so the easing has to route through it rather than the content rect.
        private void EaseRailToTarget()
        {
            if (!railScroll || !railContent) return;

            railScroll.verticalNormalizedPosition = Mathf.SmoothDamp(
                railScroll.verticalNormalizedPosition,
                railScrollTarget, ref railScrollVelocity, RailScrollTime);
        }

        private static void ScrollBy(RectTransform content, RectTransform viewport,
            float wheel)
        {
            if (!content || !viewport) return;

            var maxScroll = Mathf.Max(0f, content.rect.height - viewport.rect.height);
            var anchoredPosition = content.anchoredPosition;
            anchoredPosition.y =
                Mathf.Clamp(anchoredPosition.y - wheel * ScrollPixelsPerWheelStep, 0f,
                    maxScroll);
            content.anchoredPosition = anchoredPosition;
        }

        private static void NormalizeTabLabel(TMP_Text label, float vanillaFontSize)
        {
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
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = vanillaFontSize;
        }
    }
}
