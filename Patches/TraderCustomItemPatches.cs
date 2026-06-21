using System;
using System.Collections.Generic;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// TODO I don't like this, will need to refactor to not override so much vanilla game behaviour
namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class TraderCustomItemPatches
    {
        private static readonly HashSet<string> WarnedTraderIssues =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly FieldInfo FreeAmountField =
            AccessTools.Field(typeof(TraderScript), "freeAmount");

        private static readonly FieldInfo FreeDressingField =
            AccessTools.Field(typeof(TraderScript), "freeDressing");

        private static readonly FieldInfo FillSpriteField =
            AccessTools.Field(typeof(WaterContainerItem), "fillSprite");

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.RefreshTraderInventories))]
        [HarmonyPrefix]
        private static bool RefreshTraderInventories(PlayerCamera __instance)
        {
            if (__instance == null || __instance.tradeMenu == null || !__instance.tradeMenu.activeSelf) return false;

            __instance.ClearTraderInventories();

            var currentTrader = __instance.currentTrader;
            if (currentTrader == null || currentTrader.items == null || __instance.traderInventory == null)
                return false;

            var traderItemPreference = TraderScript.TraderItemPreference.WantsKeep;
            var yOffset = 0f;
            var hasRenderedEntries = false;

            for (var i = 0; i < currentTrader.items.Count; i++)
            {
                var traderItem = currentTrader.items[i];
                ItemInfo info;
                Sprite sprite;
                GameObject template;
                if (!TryResolveTraderListing(traderItem, out info, out sprite, out template)) continue;

                if (!hasRenderedEntries || traderItem.preference != traderItemPreference)
                {
                    traderItemPreference = traderItem.preference;
                    var split = CreateResource("Special/TraderInvSplit", __instance.traderInventory);
                    if (split == null) continue;

                    split.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f - yOffset);
                    split.GetComponentInChildren<TextMeshProUGUI>().text =
                        Locale.GetOther(traderItemPreference.ToString().ToLower());
                    split.GetComponent<UITooltip>().localeName =
                        Locale.GetOther(traderItemPreference.ToString().ToLower() + "tip");
                    split.transform.GetChild(1).localEulerAngles = new Vector3(
                        0f,
                        0f,
                        currentTrader.collapsedCategories.Contains(traderItemPreference) ? -90f : 0f);
                    split.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(delegate
                    {
                        __instance.TraderToggleCategory(traderItem.preference);
                    });
                    yOffset += 50f;
                }

                if (currentTrader.collapsedCategories.Contains(traderItemPreference))
                {
                    hasRenderedEntries = true;
                    continue;
                }

                var panel = CreateResource("Special/TraderItemPanel", __instance.traderInventory);
                if (panel == null) continue;

                panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f - yOffset);

                ApplyLiquidFill(panel, info, template);
                ApplyItemSprite(panel, sprite);
                ApplyText(panel, currentTrader, traderItem, info);

                yOffset += 120f;
                hasRenderedEntries = true;
            }

            var inventoryRect = __instance.traderInventory.GetComponent<RectTransform>();
            inventoryRect.sizeDelta = new Vector2(inventoryRect.sizeDelta.x, yOffset);
            return false;
        }

        [HarmonyPatch(typeof(TraderScript), nameof(TraderScript.TryPurchase))]
        [HarmonyPrefix]
        private static bool TryPurchase(TraderScript __instance, TraderItem item)
        {
            if (__instance == null || item == null) return false;

            var build = __instance.GetComponent<BuildingEntity>();
            if (build != null && build.health < 200f) return false;

            var camera = PlayerCamera.main;
            var body = camera != null ? camera.body : null;
            if (camera == null || body == null) return false;

            ItemInfo info;
            if (!TryResolveTraderItemInfo(item.id, out info))
            {
                WarnTraderIssue(item.id, "purchase skipped because no ItemInfo could be resolved (?)");
                // Not sure if this will occur, it'd probably become the large geofruit unity default prefab
                camera.PlayUISound(PlayerCamera.UISoundType.Deny);
                return false;
            }

            int price;
            if (!TryGetTraderItemPrice(__instance, item, info, out price))
            {
                WarnTraderIssue(item.id, "purchase skipped because price was invalid...");
                camera.PlayUISound(PlayerCamera.UISoundType.Deny);
                return false;
            }

            if (__instance.valueGiven >= price)
            {
                var spawned =
                    CustomInstantiate.InstantiateReturn(item.id, __instance.transform.position, Quaternion.identity);
                var boughtItem = spawned != null ? spawned.GetComponent<Item>() : null;
                if (boughtItem == null)
                {
                    if (spawned != null) Object.Destroy(spawned);

                    WarnTraderIssue(item.id, "purchase skipped because the trader item could not be spawned safely.!");
                    camera.PlayUISound(PlayerCamera.UISoundType.Deny);
                    camera.RefreshTraderInventories();
                    camera.UpdateTradeTexts();
                    return false;
                }

                __instance.valueGiven -= price;
                if (price > 0)
                {
                    if (item.preference == TraderScript.TraderItemPreference.WantsTrade)
                        __instance.reputation += 7f;
                    else if (item.preference == TraderScript.TraderItemPreference.Indifferent)
                        __instance.reputation += 4f;
                }

                var freeAmount = GetFreeAmount(__instance);
                if (freeAmount > 0)
                {
                    SetFreeAmount(__instance, freeAmount - 1);
                }
                else
                {
                    __instance.talker.Talk(Locale.GetCharacter("traderbuy", __instance.character));
                    body.happiness += 0.75f;
                    body.skills.AddExp(2, 1f);
                }

                SetFreeDressing(__instance, false);
                __instance.items.Remove(item);

                spawned.AddComponent<BoughtItem>();
                AmmoScript ammo;
                if (spawned.TryGetComponent(out ammo) &&
                    ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                    ammo.rounds = Random.Range(
                        (int)(ammo.maxRounds * 0.5f),
                        ammo.maxRounds);

                body.AutoPickUpItem(boughtItem);
                camera.PlayUISound(PlayerCamera.UISoundType.Click);
            }
            else
            {
                if (__instance.totalValueGiven != TraderScript.MAX_VALUE_GIVEN)
                    __instance.talker.Talk(Locale.GetCharacter("traderbuyfail", __instance.character));
                else
                    __instance.talker.Talk(Locale.GetCharacter("traderbuyfailmaxvalue", __instance.character));

                camera.PlayUISound(PlayerCamera.UISoundType.Deny);
                __instance.reputation -= 2f;
            }

            camera.RefreshTraderInventories();
            camera.UpdateTradeTexts();
            return false;
        }

        [HarmonyPatch(typeof(TraderScript), nameof(TraderScript.GiveItem))]
        [HarmonyPrefix]
        private static bool GiveItem(TraderScript __instance, Item item)
        {
            if (__instance == null || item == null) return false;

            Container container;
            if (item.TryGetComponent(out container) && container.GetHoldingWeight() > 0f)
            {
                if (PlayerCamera.main != null) PlayerCamera.main.DoAlert(Locale.GetOther("alertsellcontainer"));

                return false;
            }

            ItemInfo info;
            if (!TryResolveTraderItemInfo(item.id, out info))
            {
                WarnTraderIssue(item.id, "sell skipped because no ItemInfo could be resolved.");
                return false;
            }

            var itemValue = info.GetValue(item);
            if (itemValue <= 0 || item.GetComponent<BoughtItem>() != null) return false;

            if (__instance.totalValueGiven == TraderScript.MAX_VALUE_GIVEN)
            {
                __instance.talker.Talk(Locale.GetCharacter("tradergiveitemrefuse", __instance.character));
                return false;
            }

            __instance.talker.Talk(Locale.GetCharacter("tradergiveitem", __instance.character));

            var roomLeft = TraderScript.MAX_VALUE_GIVEN - __instance.totalValueGiven;
            if (itemValue > roomLeft) itemValue = roomLeft;

            __instance.valueGiven += itemValue;
            __instance.totalValueGiven += itemValue;

            if (__instance.valueGiven > TraderScript.MAX_VALUE_GIVEN)
                __instance.valueGiven = TraderScript.MAX_VALUE_GIVEN;

            if (__instance.totalValueGiven > TraderScript.MAX_VALUE_GIVEN)
                __instance.totalValueGiven = TraderScript.MAX_VALUE_GIVEN;

            if (PlayerCamera.main != null) PlayerCamera.main.UpdateTradeTexts();

            Object.Destroy(item.gameObject);
            return false;
        }

        [HarmonyPatch(typeof(TraderScript), nameof(TraderScript.DropInventory))]
        [HarmonyPrefix]
        private static bool DropInventory(TraderScript __instance)
        {
            if (__instance == null || __instance.items == null) return false;

            foreach (var traderItem in __instance.items)
            {
                if (traderItem == null || Random.value >= 0.66f) continue;

                var spawned = CustomInstantiate.InstantiateReturn(
                    traderItem.id,
                    __instance.transform.position,
                    Quaternion.Euler(0f, 0f, Random.value * 360f));

                var droppedItem = spawned != null ? spawned.GetComponent<Item>() : null;
                if (droppedItem == null)
                {
                    if (spawned != null) Object.Destroy(spawned);

                    WarnTraderIssue(traderItem.id, "drop skipped because the trader item could not be spawned safely.");
                    continue;
                }

                if (droppedItem.gameObject.GetComponent<Rigidbody2D>() != null &&
                    droppedItem.gameObject.GetComponent<SpriteRenderer>() != null)
                    droppedItem.gameObject.AddComponent<FreshItemDrop>();

                AmmoScript ammo;
                if (droppedItem.TryGetComponent(out ammo) &&
                    ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                    ammo.rounds = (int)Mathf.Lerp(0f, ammo.maxRounds, Random.value);
            }

            __instance.items.Clear();
            return false;
        }

        private static bool TryResolveTraderListing(
            TraderItem traderItem,
            out ItemInfo info,
            out Sprite sprite,
            out GameObject template)
        {
            info = null;
            sprite = null;
            template = null;

            var itemId = NormalizeId(traderItem != null ? traderItem.id : null);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                WarnTraderIssue(itemId, "skipped trader entry because the item ID was blank.");
                return false;
            }

            if (!TryResolveTraderItemInfo(itemId, out info))
            {
                WarnTraderIssue(itemId, "skipped trader entry because no ItemInfo was found.");
                return false;
            }

            template = GetTemplate(itemId);
            if (template == null)
            {
                WarnTraderIssue(itemId, "skipped trader entry because no spawnable template was found.");
                return false;
            }

            sprite = GetItemSprite(itemId, template);
            if (sprite == null)
            {
                WarnTraderIssue(itemId, "skipped trader entry because no renderable sprite was found.");
                return false;
            }

            return true;
        }

        private static void ApplyLiquidFill(GameObject panel, ItemInfo info, GameObject template)
        {
            var fillImage = panel.transform.GetChild(0).GetComponent<Image>();
            fillImage.enabled = false;

            var waterContainer = template != null ? template.GetComponent<WaterContainerItem>() : null;
            var fillSprite = GetFillSprite(waterContainer);
            var liquidInfo = info as LiquidItemInfo;
            if (fillSprite == null || liquidInfo == null || liquidInfo.defaultContents == null ||
                liquidInfo.defaultContents.Count == 0) return;

            var liquidId = liquidInfo.defaultContents[0].liquidId;
            LiquidType liquidType;
            if (string.IsNullOrWhiteSpace(liquidId) || !Liquids.Registry.TryGetValue(liquidId, out liquidType)) return;

            fillImage.enabled = true;
            fillImage.sprite = fillSprite;
            fillImage.color = liquidType.color;
            fillImage.GetComponent<RectTransform>().sizeDelta =
                PlayerCamera.ImageSizeDelta(fillSprite.texture, 8f, 64f);
        }

        private static void ApplyItemSprite(GameObject panel, Sprite sprite)
        {
            var image = panel.transform.GetChild(1).GetComponent<Image>();
            image.enabled = true;
            image.sprite = sprite;
            image.GetComponent<RectTransform>().sizeDelta =
                PlayerCamera.ImageSizeDelta(sprite.texture, 8f, 64f);
        }

        private static void ApplyText(GameObject panel, TraderScript trader, TraderItem traderItem, ItemInfo info)
        {
            panel.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = info.fullName;
            panel.transform.GetChild(3).GetComponent<TextMeshProUGUI>().text = info.description;

            var tooltip = panel.transform.GetChild(3).GetComponent<UITooltip>();
            tooltip.skipLocale = true;
            tooltip.tipName = info.fullName;
            tooltip.tipDesc = info.description;

            int price;
            if (!TryGetTraderItemPrice(trader, traderItem, info, out price)) price = 0;

            var priceText = panel.transform.GetChild(4).GetComponent<TextMeshProUGUI>();
            priceText.text = string.Format("{0}{1}", Locale.GetOther("costs"), price);
            if (price == 0) priceText.text = Locale.GetOther("free");

            panel.transform.GetChild(5).GetComponent<TextMeshProUGUI>().text =
                string.Format("{0}{1:0.##}u", Locale.GetOther("weighs"), info.weight);
            panel.transform.GetChild(6).GetComponent<Button>().onClick.AddListener(delegate
            {
                trader.TryPurchase(traderItem);
            });
            panel.transform.GetChild(7).GetComponent<Image>().color =
                TraderScript.PrefToColor(traderItem.preference);
        }

        private static bool TryGetTraderItemPrice(TraderScript trader, TraderItem traderItem, ItemInfo info,
            out int price)
        {
            price = 0;
            if (trader == null || traderItem == null || info == null) return false;

            var adjustedValue = traderItem.value * trader.ValueMultiplier();
            if (traderItem.preference == TraderScript.TraderItemPreference.WantsTrade) adjustedValue *= 0.7f;

            if (traderItem.preference == TraderScript.TraderItemPreference.WantsKeep) adjustedValue *= 1.5f;

            if (GetFreeDressing(trader) && info.HasTag("dressing")) adjustedValue = 0f;

            if (GetFreeAmount(trader) > 0) adjustedValue = 0f;

            price = Mathf.RoundToInt(adjustedValue);
            return true;
        }

        private static bool TryResolveTraderItemInfo(string id, out ItemInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var normalizedId = NormalizeId(id);
            return ItemRegistry.TryGetItemInfo(normalizedId, out info) && info != null;
        }

        private static Sprite GetItemSprite(string id, GameObject template)
        {
            CustomItemInfo customInfo;
            if (ItemRegistry.TryGetCustomInfo(id, out customInfo) && customInfo.Icon != null) return customInfo.Icon;

            var renderer = template != null ? template.GetComponent<SpriteRenderer>() : null;
            return renderer != null ? renderer.sprite : null;
        }

        private static GameObject GetTemplate(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var vanilla = Resources.Load<GameObject>(id);
            if (vanilla != null) return vanilla;

            return CustomInstantiate.GetOrCreateTemplate(id);
        }

        private static GameObject CreateResource(string id, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(id);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }

        private static Sprite GetFillSprite(WaterContainerItem waterContainer)
        {
            if (waterContainer == null || FillSpriteField == null) return null;

            return FillSpriteField.GetValue(waterContainer) as Sprite;
        }

        private static int GetFreeAmount(TraderScript trader)
        {
            if (trader == null || FreeAmountField == null) return 0;

            var value = FreeAmountField.GetValue(trader);
            return value is int ? (int)value : 0;
        }

        private static void SetFreeAmount(TraderScript trader, int value)
        {
            if (trader == null || FreeAmountField == null) return;

            FreeAmountField.SetValue(trader, value);
        }

        private static bool GetFreeDressing(TraderScript trader)
        {
            if (trader == null || FreeDressingField == null) return false;

            var value = FreeDressingField.GetValue(trader);
            return value is bool && (bool)value;
        }

        private static void SetFreeDressing(TraderScript trader, bool value)
        {
            if (trader == null || FreeDressingField == null) return;

            FreeDressingField.SetValue(trader, value);
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private static void WarnTraderIssue(string id, string issue)
        {
            var normalizedId = string.IsNullOrWhiteSpace(id) ? "<blank>" : id.Trim();
            var key = normalizedId + "|" + issue;
            if (!WarnedTraderIssues.Add(key)) return;

            CUCoreLibPlugin.Log.LogWarning("CUCoreLib Trader: '" + normalizedId + "' " + issue);
        }
    }
}