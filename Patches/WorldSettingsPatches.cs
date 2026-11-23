using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CUCoreLib.Data;
using static CUCoreLib.Registries.WorldSettingsRegistry;

namespace CUCoreLib.Patches
{
    [AppliesToBuild(GameBuild.Full)]
    [HarmonyPatch(typeof(PreRunScript))] // HarmonyX skips classes without a class-level [HarmonyPatch]
    internal static class WorldSettingsPatches
    {
        private const string RowPrefix = "CUCoreLibWorldSetting_";
        private const string LegacyQoLRowPrefix = "QoLWorldSetting_"; // QoL was the test run for this feature! Hehe.

        // Clever application of hiding out payload into a comment, whilst being super annoying for future mods that might do the same >.<

        private const string PresetTagOpen = "/*cclWorld:";
        private const string PresetTagClose = "*/";
        private static readonly Regex PresetTag = new Regex(@"/\*(?:cclWorld|qolWorld):([A-Za-z0-9+/=]*)\*/\s*$");

        [HarmonyPatch(typeof(PreRunScript), "CopyToClipboard")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)] 
        private static void CopyToClipboard_Postfix()
        {
            if (!HasPersistedSettings) return;
            try
            {
                string json = PreRunScript.DecompressString(GUIUtility.systemCopyBuffer);
                json = PresetTag.Replace(json, string.Empty).TrimEnd();
                string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(WriteValues().ToString(Newtonsoft.Json.Formatting.None)));
                GUIUtility.systemCopyBuffer = PreRunScript.CompressString(json + PresetTagOpen + payload + PresetTagClose);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Could not add world settings to preset code! Most likely a mod incompatiblity: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PreRunScript), "PasteFromClipboard")]
        [HarmonyPostfix]
        private static void PasteFromClipboard_Postfix()
        {
            try
            {
                Match match = PresetTag.Match(PreRunScript.DecompressString(GUIUtility.systemCopyBuffer));
                if (!match.Success) return;
                ReadValues(JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value))));
                RefreshDisplays();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Could not read world settings from preset code: {ex.Message}");
            }
        }

        // Picking a vanilla preset resets every row, then applies that preset's WithPresetDefault values.
        [HarmonyPatch(typeof(PreRunScript), "ApplyPreset")]
        [HarmonyPostfix]
        private static void ApplyPreset_Postfix(string presetName)
        {
            if (RegisteredSettings.Count == 0) return;
            ApplyPresetDefaults(presetName);
            RefreshDisplays();
        }

        // Been a half year, still hate code-based UI just as much :p

        [HarmonyPatch(typeof(PreRunScript), "Start")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter("org.bepinex.plugins.qol.worldsettings")] 
        private static void PreRunScript_Start_Postfix(PreRunScript __instance)
        {
            if (RegisteredSettings.Count == 0 || __instance == null || __instance.customSettingContent == null) return;

            // Vanilla starts the panel on the "normal" preset.
            ApplyPresetDefaults("normal");

            try
            {
                BuildRows(__instance.customSettingContent);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError($"[WorldSettings] Failed to build rows: {ex}");
            }
        }

        private static void BuildRows(RectTransform content)
        {
            List<GameGroup> gameGroups = CollectGameGroups(content);
            var knownGroups = new HashSet<string>(gameGroups.Where(g => g.Name != null).Select(g => g.Name));

            Refreshers.Clear();
            var built = new Dictionary<WorldSettingDefinition, RectTransform>();
            foreach (WorldSettingDefinition setting in RegisteredSettings)
            {
                GameObject row = CreateRow(setting, content);
                if (row != null) built[setting] = row.GetComponent<RectTransform>();
            }

            IEnumerable<RectTransform> Rows(Func<WorldSettingDefinition, bool> match) =>
                RegisteredSettings.Where(s => match(s) && built.ContainsKey(s)).Select(s => built[s]);

            List<RectTransform> Block(CategoryEntry category)
            {
                GameObject header = Utils.Create("Special/RunSettingDivider", content);
                header.name = RowPrefix + "category_" + category.Name;
                header.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = Localize(CategoryKey(category.Name), category.Name);
                var block = new List<RectTransform> { header.GetComponent<RectTransform>() };
                block.AddRange(Rows(s => s.CustomCategory == category.Name));
                return block;
            }

            string Section(WorldSettingCategory? category) => category?.ToString().ToLowerInvariant();
            foreach (CategoryEntry category in CustomCategories.Where(c => c.Anchor != null && !knownGroups.Contains(Section(c.Anchor))))
            {
                CUCoreLibPlugin.Log?.LogWarning($"[WorldSettings] Vanilla section '{category.Anchor}' not found for '{category.Name}'; placing it at the top.");
            }
            bool AtTop(CategoryEntry category) => category.Anchor == null || !knownGroups.Contains(Section(category.Anchor));

            var order = new List<RectTransform>();
            foreach (CategoryEntry category in CustomCategories.Where(AtTop)) order.AddRange(Block(category));
            foreach (GameGroup group in gameGroups)
            {
                foreach (CategoryEntry category in CustomCategories.Where(x => !AtTop(x) && Section(x.Anchor) == group.Name && !x.Below)) order.AddRange(Block(category));
                order.AddRange(group.Rows);
                order.AddRange(Rows(s => !s.UsesCustomCategory && s.GameSection == group.Name));
                foreach (CategoryEntry category in CustomCategories.Where(x => !AtTop(x) && Section(x.Anchor) == group.Name && x.Below)) order.AddRange(Block(category));
            }
            order.AddRange(Rows(s => !s.UsesCustomCategory && !knownGroups.Contains(s.GameSection)));

            float y = 0f;
            foreach (RectTransform rect in order)
            {
                rect.anchoredPosition = new Vector2(0f, -(y + rect.sizeDelta.y * 0.5f));
                y += rect.sizeDelta.y - 3f;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, y);
        }

        private class GameGroup
        {
            public string Name;
            public readonly List<RectTransform> Rows = new List<RectTransform>();
        }

        private static List<GameGroup> CollectGameGroups(RectTransform content)
        {
            var dividerNames = new Queue<string>(RunSettings.settingTypes.Where(t => !string.IsNullOrEmpty(t.divider)).Select(t => t.divider));
            var groups = new List<GameGroup>();
            var current = new GameGroup();
            var children = content.Cast<Transform>()
                .Where(child => !child.name.StartsWith(RowPrefix))
                .OrderByDescending(child => ((RectTransform)child).anchoredPosition.y)
                .ToList();
            foreach (Transform child in children)
            {
                bool isVanillaDivider = child.GetComponent<RunSettingDisplay>() == null && !child.name.StartsWith(LegacyQoLRowPrefix);
                if (isVanillaDivider)
                {
                    if (current.Rows.Count > 0) groups.Add(current);
                    current = new GameGroup { Name = dividerNames.Count > 0 ? dividerNames.Dequeue() : null };
                }
                current.Rows.Add((RectTransform)child);
            }
            if (current.Rows.Count > 0) groups.Add(current);
            return groups;
        }

        private static GameObject CreateRow(WorldSettingDefinition setting, RectTransform content)
        {
            switch (setting.Kind)
            {
                case WorldSettingKind.Bool:
                {
                    GameObject obj = CreateLabeledRow("Special/RunSettingBool", setting, content);
                    Toggle toggle = obj.transform.GetChild(1).GetComponent<Toggle>();
                    toggle.SetIsOnWithoutNotify(Get<bool>(setting.Id));
                    Refreshers.Add(() => toggle.SetIsOnWithoutNotify(Get<bool>(setting.Id)));
                    toggle.onValueChanged.AddListener(v => Set(setting.Id, v));
                    return obj;
                }
                case WorldSettingKind.Float:
                {
                    GameObject obj = CreateLabeledRow("Special/RunSettingFloat", setting, content);
                    Slider slider = obj.transform.GetChild(1).GetComponent<Slider>();
                    TextMeshProUGUI valueText = obj.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
                    slider.minValue = setting.Min;
                    slider.maxValue = setting.Max;
                    string Format(float v) => setting.FloatFormatter != null ? setting.FloatFormatter(v) : FormatSlider(v);
                    void Show()
                    {
                        float current = Get(setting.Id, setting.Min);
                        slider.SetValueWithoutNotify(current);
                        valueText.text = Format(current);
                    }
                    Show();
                    Refreshers.Add(Show);
                    slider.onValueChanged.AddListener(v =>
                    {
                        Set(setting.Id, v);
                        valueText.text = Format(v);
                    });
                    return obj;
                }
                case WorldSettingKind.Dropdown:
                {
                    GameObject obj = CreateLabeledRow("Special/RunSettingDropdown", setting, content);
                    TMP_Dropdown dropdown = obj.transform.GetChild(1).GetComponent<TMP_Dropdown>();
                    dropdown.ClearOptions();
                    dropdown.AddOptions(setting.Choices.Select(choice => Localize(ChoiceKey(setting, choice), choice.Label)).ToList());
                    MakeDropdownScrollable(dropdown);
                    dropdown.SetValueWithoutNotify(Get(setting.Id, 0));
                    Refreshers.Add(() => dropdown.SetValueWithoutNotify(Get(setting.Id, 0)));
                    dropdown.onValueChanged.AddListener(v => Set(setting.Id, v));
                    return obj;
                }
                case WorldSettingKind.Int:
                case WorldSettingKind.Text:
                {
                    GameObject obj = CreateLabeledRow("Special/RunSettingFloat", setting, content);
                    TMP_InputField input = CreateInputField(obj, obj.transform.GetChild(0).GetComponent<TextMeshProUGUI>()?.font, setting);
                    Refreshers.Add(() => input.SetTextWithoutNotify(InputText(setting)));
                    // Commit while typing too, so starting the run without deselecting keeps the value.
                    input.onValueChanged.AddListener(text => Set(setting.Id, ParseInput(setting, text)));
                    input.onEndEdit.AddListener(text =>
                    {
                        Set(setting.Id, ParseInput(setting, text));
                        input.SetTextWithoutNotify(InputText(setting)); 
                    });
                    return obj;
                }
                case WorldSettingKind.Button:
                {
                    GameObject obj = CreateLabeledRow("Special/RunSettingFloat", setting, content);
                    CreateButton(ReplaceControlSlot(obj, typeof(Image), typeof(Button)), obj.transform.GetChild(0).GetComponent<TextMeshProUGUI>()?.font, setting);
                    return obj;
                }
            }
            return null;
        }

        private static GameObject CreateLabeledRow(string prefab, WorldSettingDefinition setting, RectTransform content)
        {
            GameObject obj = Utils.Create(prefab, content);
            obj.name = RowPrefix + setting.Id;
            GameObject label = obj.transform.GetChild(0).gameObject;
            string name = Localize(NameKey(setting), setting.Name);
            label.GetComponent<TextMeshProUGUI>().text = name;
            if (!string.IsNullOrEmpty(setting.Description))
            {
                UITooltip tip = label.AddComponent<UITooltip>();
                tip.skipLocale = true; // already localized here
                tip.tipName = name;
                tip.tipDesc = Localize(DescriptionKey(setting), setting.Description);
            }
            return obj;
        }

        private static void MakeDropdownScrollable(TMP_Dropdown dropdown, float maxListHeight = 220f)
        {
            ScrollRect scrollRect = dropdown != null && dropdown.template != null ? dropdown.template.GetComponent<ScrollRect>() : null;
            if (scrollRect == null) return;

            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 20f;
            if (scrollRect.content != null)
            {
                scrollRect.content.anchorMin = new Vector2(0f, 1f);
                scrollRect.content.anchorMax = new Vector2(1f, 1f);
                scrollRect.content.pivot = new Vector2(0.5f, 1f);
            }
            dropdown.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxListHeight);
        }

        private const float ControlMinWidth = 190f;
        private const float ControlMinHeight = 36f;

        private static RectTransform ReplaceControlSlot(GameObject row, params Type[] components)
        {
            RectTransform rowRect = row.GetComponent<RectTransform>();
            Bounds bounds = LocalBounds(rowRect, row.transform.GetChild(1) as RectTransform);
            if (row.transform.childCount > 2)
            {
                bounds.Encapsulate(LocalBounds(rowRect, row.transform.GetChild(2) as RectTransform));
                row.transform.GetChild(2).gameObject.SetActive(false);
            }
            row.transform.GetChild(1).gameObject.SetActive(false);

            var go = new GameObject("Control", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(row.transform, false);
            foreach (Type component in components) go.AddComponent(component);
            var slot = go.GetComponent<RectTransform>();
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
            float width = Mathf.Max(bounds.size.x, ControlMinWidth);
            float height = Mathf.Max(bounds.size.y + 8f, ControlMinHeight);
            slot.anchoredPosition = new Vector2(bounds.max.x - width * 0.5f, bounds.center.y) - rowRect.rect.center;
            slot.sizeDelta = new Vector2(width, height);
            return slot;
        }

        private static Bounds LocalBounds(RectTransform space, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < 4; i++) bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
            return bounds;
        }

        private static Sprite controlSprite;
        private static TMP_FontAsset controlFont;

        private static Sprite ControlSprite()
        {
            Transform start = PreRunScript.instance?.mainCanvas?.transform.Find("RunSettings/CharSelect/Bottom/Start");
            Sprite sprite = start?.GetComponent<Image>()?.sprite;
            if (sprite != null) return sprite;
            if (controlSprite == null)
                controlSprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s != null && s.name.IndexOf("uiBlockNano", StringComparison.OrdinalIgnoreCase) >= 0);
            return controlSprite;
        }

        private static TMP_FontAsset ControlFont(TMP_FontAsset fallback)
        {
            if (controlFont == null)
                controlFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f != null && f.name.IndexOf("Retro Gaming", StringComparison.OrdinalIgnoreCase) >= 0);
            return controlFont ?? fallback;
        }

        private static TMP_InputField CreateInputField(GameObject row, TMP_FontAsset font, WorldSettingDefinition setting)
        {
            RectTransform slot = ReplaceControlSlot(row, typeof(Image), typeof(TMP_InputField));
            font = ControlFont(font);

            Image background = slot.GetComponent<Image>();
            background.sprite = ControlSprite();
            background.type = Image.Type.Sliced;
            background.color = Color.white;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            textArea.SetParent(slot, false);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(12f, 5f);
            textArea.offsetMax = new Vector2(-12f, -5f);

            TextMeshProUGUI text = CreateText(textArea, "Text", string.Empty, font, new Color(0.33f, 0.7f, 1f));
            TextMeshProUGUI placeholder = CreateText(textArea, "Placeholder", setting.Placeholder ?? string.Empty, font, new Color(1f, 1f, 1f, 0.45f));

            TMP_InputField input = slot.GetComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 64;
            input.targetGraphic = background;
            input.selectionColor = new Color(0.35f, 0.62f, 1f, 0.45f);
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.caretWidth = 3;
            input.caretBlinkRate = 0.65f;
            input.contentType = setting.Kind == WorldSettingKind.Int
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.Standard;

            input.SetTextWithoutNotify(InputText(setting));
            slot.gameObject.SetActive(true);
            return input;
        }

        private static void CreateButton(RectTransform slot, TMP_FontAsset font, WorldSettingDefinition setting)
        {
            Image background = slot.GetComponent<Image>();
            background.sprite = ControlSprite();
            background.type = Image.Type.Sliced;
            Button button = slot.GetComponent<Button>();
            button.targetGraphic = background;
            CreateText(slot, "Text", setting.Placeholder ?? Localize(NameKey(setting), setting.Name), ControlFont(font), Color.white);
            slot.gameObject.SetActive(true);
            button.onClick.AddListener(() =>
            {
                try { setting.OnClick?.Invoke(); }
                catch (Exception ex) { CUCoreLibPlugin.Log?.LogError($"[WorldSettings] Button '{setting.Id}' failed: {ex}"); }
            });
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = value;
            if (font != null) tmp.font = font;
            tmp.fontSize = 20f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static string InputText(WorldSettingDefinition setting) =>
            Convert.ToString(Get(setting.Id), CultureInfo.InvariantCulture) ?? string.Empty;

        // Empty (or unparsable) input resets to the default.
        private static object ParseInput(WorldSettingDefinition setting, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (setting.Kind != WorldSettingKind.Int) return text;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? (object)i : null;
        }

        private static string FormatSlider(float v) => v >= 10f || v < 0f ? v.ToString("0.#") : v.ToString("0.##");
    }
}
