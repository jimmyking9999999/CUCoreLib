using System;
using System.Collections.Generic;
using System.Linq;

namespace CUCoreLib.Util;

public static class PlayerInventoryUtils
{
    public static bool IsSlotOccupied(int slot)
    {
        return PlayerUtils.GetBody()?.HoldingItem(slot) ?? false;
    }

    public static bool IsSlotEmpty(int slot)
    {
        return !IsSlotOccupied(slot);
    }

    public static Item GetItem(int slot)
    {
        return PlayerUtils.GetBody()?.GetItem(slot);
    }

    public static ItemInfo GetItemInfo(int slot)
    {
        return GetItem(slot)?.Stats;
    }

    public static string GetItemId(int slot)
    {
        return GetItem(slot)?.id;
    }

    public static bool HasItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return PlayerUtils.GetBody()?.HoldingItem(id) ?? false;
    }

    public static bool HasItemThorough(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return PlayerUtils.GetBody()?.FindByIdThorough(id, out _) ?? false;
    }

    public static bool HasAnyItem(params string[] ids)
    {
        if (ids is not { Length: > 0 }) return false;
        var b = PlayerUtils.GetBody();
        return b != null && ids.Any(b.HoldingItem);
    }

    public static bool HasItem(Predicate<ItemInfo> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        var b = PlayerUtils.GetBody();
        if (b == null) return false;
        for (var i = 0; i < b.slots.Length; i++)
        {
            var info = b.GetItem(i)?.Stats;
            if (info != null && predicate(info)) return true;
        }

        return false;
    }

    public static bool HasItemByTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && HasItem(info => info.HasTag(tag));
    }

    public static bool HasItemByCategory(string category)
    {
        return !string.IsNullOrWhiteSpace(category) && HasItem(info => info.category == category);
    }

    public static bool HasWearableItem()
    {
        return HasItem(info => info.wearable);
    }

    public static int CountItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;
        var b = PlayerUtils.GetBody();
        if (b == null) return 0;
        var count = 0;
        for (var i = 0; i < b.slots.Length; i++)
            if (b.GetItem(i)?.id == id)
                count++;
        return count;
    }

    public static List<Item> GetAllItems()
    {
        return PlayerUtils.GetBody()?.GetAllItems() ?? new List<Item>();
    }

    public static List<Item> GetAllItemsThorough()
    {
        return PlayerUtils.GetBody()?.GetAllItemsThorough() ?? new List<Item>();
    }

    public static List<ItemInfo> GetAllItemInfos()
    {
        return GetAllItems().Select(i => i.Stats).Where(i => i != null).ToList();
    }

    public static List<ItemInfo> GetAllItemInfosThorough()
    {
        return GetAllItemsThorough().Select(i => i.Stats).Where(i => i != null).ToList();
    }

    public static List<string> GetAllItemIds()
    {
        return GetAllItems().Select(i => i.id).ToList();
    }

    public static List<Item> GetWearables()
    {
        return PlayerUtils.GetBody()?.GetAllWearables() ?? new List<Item>();
    }

    public static List<ItemInfo> GetWearableInfos()
    {
        return GetWearables().Select(i => i.Stats).Where(i => i != null).ToList();
    }

    public static int? FindFirstEmptySlot()
    {
        return PlayerUtils.GetBody()?.FirstEmptySlot();
    }

    public static bool FindById(string id, out Item item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return PlayerUtils.GetBody()?.FindByIdSurface(id, out item) ?? false;
    }

    public static bool FindByIdThorough(string id, out Item item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return PlayerUtils.GetBody()?.FindByIdThorough(id, out item) ?? false;
    }

    public static int GetHandSlot()
    {
        return PlayerUtils.GetBody()?.handSlot ?? 0;
    }

    public static Item GetItemInHand()
    {
        var b = PlayerUtils.GetBody();
        return b != null ? b.GetItem(b.handSlot) : null;
    }

    public static ItemInfo GetItemInfoInHand()
    {
        return GetItemInHand()?.Stats;
    }

    public static string GetItemIdInHand()
    {
        return GetItemInHand()?.id;
    }

    public static int GetSlotCount()
    {
        return PlayerUtils.GetBody()?.slots.Length ?? 0;
    }

    public static string GetItemIdsString()
    {
        var ids = GetAllItemIds();
        return ids.Count > 0 ? string.Join(", ", ids) : LocaleUtils.GetLog("inventory.empty");
    }
}
