using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Presentation;

internal static class InventoryCatalogProjection
{
    private const int InventoryItemCount = 2559;

    private static readonly CategoryDefinition[] CategoryDefinitions =
    [
        new((byte)ItemCategoryId.Weapons, 0,
            [(1, 36), (2305, 11), (2434, 1), (39, 32), (2326, 9), (2435, 1), (112, 31), (2367, 9), (2436, 1), (77, 28), (2345, 11), (2437, 1), (150, 26), (2385, 3), (2389, 8), (2438, 1), (183, 15), (2407, 8), (2439, 1), (217, 22), (2425, 8), (2440, 1)]),
        new((byte)ItemCategoryId.Armor, 256,
            [(257, 8), (266, 6), (287, 4), (293, 4), (307, 4), (315, 3), (328, 4), (334, 5), (347, 2), (350, 9), (367, 4), (372, 7), (387, 6), (394, 5), (407, 6), (414, 5), (420, 13)]),
        new((byte)ItemCategoryId.Accessories, 512,
            [(513, 96), (615, 69), (685, 1), (687, 8), (754, 13)]),
        new((byte)ItemCategoryId.Consumables, 768,
            [(769, 51)]),
        new((byte)ItemCategoryId.Materials, 1280,
            [(1281, 128), (1410, 21), (1432, 21), (1454, 20), (1475, 19), (1495, 19), (1513, 21)]),
        new((byte)ItemCategoryId.SkillCards, 1536,
            [(1537, 252)]),
        new((byte)ItemCategoryId.Books, 1024,
            [(1136, 5), (1145, 7), (1259, 20)]),
        new((byte)ItemCategoryId.Veggies, 768,
            [(2089, 16), (2107, 4)]),
        new((byte)ItemCategoryId.Minerals, 2048,
            [(2128, 29)]),
        new((byte)ItemCategoryId.SocialLink, 1024,
            [(1184, 20), (1207, 3), (1224, 3), (1228, 1)]),
        new((byte)ItemCategoryId.Shelf, 1024,
            [(2056, 5), (1234, 13)]),
        new((byte)ItemCategoryId.Costumes, 1792,
            [(1792, 193), (2040, 6)]),
        new((byte)ItemCategoryId.Bugs, 1024,
            [(909, 7)]),
        new((byte)ItemCategoryId.Fish, 1024,
            [(1004, 3), (1008, 7)]),
        new((byte)ItemCategoryId.Other, 1024, Array.Empty<(int Start, int Count)>())
    ];

    private static readonly HashSet<ushort> legacyCategorizedItemIds = CreateLegacyCategorizedItemIds();
    private static readonly ReadOnlyCollection<ItemCategoryViewState> categories = CreateCategories();
    private static readonly ReadOnlyDictionary<ushort, byte> categoryByItemId = CreateCategoryByItemId();
    private static readonly ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> itemsByCategory = CreateItemsByCategory();
    private static readonly ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> weaponChoicesByCharacter = CreateWeaponChoicesByCharacter();
    private static readonly ReadOnlyDictionary<ushort, InventoryItemMetadata> metadataByItemId = CreateMetadataByItemId();
    private static readonly string otherCategoryName = categories[(byte)ItemCategoryId.Other].Name;

    internal static IReadOnlyList<ItemCategoryViewState> Categories => categories;

    internal static IReadOnlyList<InventoryItemChoiceViewState> GetItems(byte categoryId) =>
        itemsByCategory.TryGetValue(categoryId, out ReadOnlyCollection<InventoryItemChoiceViewState>? items)
            ? items
            : Array.AsReadOnly(Array.Empty<InventoryItemChoiceViewState>());

    internal static IReadOnlyList<InventoryItemChoiceViewState> GetWeaponChoices(byte characterId) =>
        weaponChoicesByCharacter.TryGetValue(characterId, out ReadOnlyCollection<InventoryItemChoiceViewState>? items)
            ? items
            : Array.AsReadOnly(Array.Empty<InventoryItemChoiceViewState>());

    internal static bool IsPlaceholderItemId(ushort itemId) => InventoryItemEditability.IsPlaceholderItemId(itemId);

    internal static InventoryStackViewState ProjectStack(int slotIndex, InventoryStack stack)
    {
        if (metadataByItemId.TryGetValue(stack.ItemId, out InventoryItemMetadata? metadata))
        {
            return new InventoryStackViewState(
                slotIndex,
                metadata.ItemId,
                metadata.ItemName,
                metadata.CategoryId,
                metadata.CategoryName,
                stack.Quantity,
                IsPlaceholderItemId(stack.ItemId));
        }

        return new InventoryStackViewState(
            slotIndex,
            stack.ItemId,
            $"Unknown item ({stack.ItemId})",
            (byte)ItemCategoryId.Other,
            otherCategoryName,
            stack.Quantity,
            IsPlaceholderItemId(stack.ItemId));
    }

    private static ReadOnlyCollection<ItemCategoryViewState> CreateCategories()
    {
        ItemCategoryViewState[] items = P4GCatalog.ItemCategories
            .Select(static category => new ItemCategoryViewState((byte)category.Id, category.Name))
            .ToArray();
        return Array.AsReadOnly(items);
    }

    private static ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> CreateItemsByCategory()
    {
        Dictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> lookup = new();
        foreach (CategoryDefinition definition in CategoryDefinitions)
        {
            lookup.Add(
                definition.CategoryId,
                definition.CategoryId == (byte)ItemCategoryId.Other
                    ? CreateOtherChoices(definition)
                    : CreateChoices(definition));
        }

        return new ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>>(lookup);
    }

    private static ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> CreateWeaponChoicesByCharacter()
    {
        Dictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>> lookup = new();
        foreach (PartyMemberCatalogEntry member in P4GCatalog.PartyMembers)
        {
            if (!EquipmentItemRules.IsSupportedEquipmentCharacterId(member.Id))
            {
                continue;
            }

            lookup.Add(member.Id, CreateWeaponChoices(member.Id));
        }

        return new ReadOnlyDictionary<byte, ReadOnlyCollection<InventoryItemChoiceViewState>>(lookup);
    }

    private static ReadOnlyCollection<InventoryItemChoiceViewState> CreateWeaponChoices(byte characterId)
    {
        List<InventoryItemChoiceViewState> items = [];
        HashSet<ushort> seen = [];
        AddPlaceholderChoice(items, 0, (byte)ItemCategoryId.Weapons);
        foreach (ushort itemId in EquipmentItemRules.EnumerateWeaponItemIds(characterId))
        {
            AddChoice(items, seen, itemId, (byte)ItemCategoryId.Weapons);
        }

        return Array.AsReadOnly(items.ToArray());
    }

    private static HashSet<ushort> CreateLegacyCategorizedItemIds()
    {
        HashSet<ushort> lookup = [];
        foreach (CategoryDefinition definition in CategoryDefinitions)
        {
            if (definition.PlaceholderItemId < InventoryItemCount)
            {
                lookup.Add(definition.PlaceholderItemId);
            }

            if (definition.CategoryId == (byte)ItemCategoryId.Other)
            {
                continue;
            }

            foreach (ushort itemId in EnumerateItemIds(definition))
            {
                if (itemId < InventoryItemCount)
                {
                    lookup.Add(itemId);
                }
            }
        }

        return lookup;
    }

    private static ReadOnlyDictionary<ushort, InventoryItemMetadata> CreateMetadataByItemId()
    {
        Dictionary<ushort, InventoryItemMetadata> lookup = new();
        foreach (ushort itemId in Enumerable.Range(0, InventoryItemCount).Select(static value => (ushort)value))
        {
            if (!P4GCatalog.ItemsById.TryGetValue(itemId, out ItemCatalogEntry item))
            {
                continue;
            }

            byte categoryId = categoryByItemId.TryGetValue(itemId, out byte mappedCategoryId)
                ? mappedCategoryId
                : (byte)ItemCategoryId.Other;
            lookup[itemId] = new InventoryItemMetadata(
                itemId,
                categoryId,
                GetCategoryName(categoryId),
                item.Name);
        }

        return new ReadOnlyDictionary<ushort, InventoryItemMetadata>(lookup);
    }

    private static ReadOnlyDictionary<ushort, byte> CreateCategoryByItemId()
    {
        Dictionary<ushort, byte> lookup = new();
        foreach (CategoryDefinition definition in CategoryDefinitions)
        {
            if (definition.PlaceholderItemId < InventoryItemCount)
            {
                lookup.TryAdd(definition.PlaceholderItemId, definition.CategoryId);
            }

            foreach (ushort itemId in EnumerateItemIds(definition))
            {
                if (itemId >= InventoryItemCount)
                {
                    continue;
                }

                lookup.TryAdd(itemId, definition.CategoryId);
            }
        }

        foreach (ushort itemId in EnumerateOtherItemIds())
        {
            lookup.TryAdd(itemId, (byte)ItemCategoryId.Other);
        }

        return new ReadOnlyDictionary<ushort, byte>(lookup);
    }

    private static ReadOnlyCollection<InventoryItemChoiceViewState> CreateChoices(CategoryDefinition definition)
    {
        List<InventoryItemChoiceViewState> items = [];
        HashSet<ushort> seen = [];
        AddPlaceholderChoice(items, definition.PlaceholderItemId, definition.CategoryId);
        foreach (ushort itemId in EnumerateItemIds(definition))
        {
            if (itemId == definition.PlaceholderItemId)
            {
                continue;
            }

            AddChoice(items, seen, itemId, definition.CategoryId);
        }

        return Array.AsReadOnly(items.ToArray());
    }

    private static ReadOnlyCollection<InventoryItemChoiceViewState> CreateOtherChoices(CategoryDefinition definition)
    {
        List<InventoryItemChoiceViewState> items = [];
        AddPlaceholderChoice(items, definition.PlaceholderItemId, definition.CategoryId);

        return Array.AsReadOnly(items.ToArray());
    }

    private static void AddChoice(
        List<InventoryItemChoiceViewState> items,
        HashSet<ushort> seen,
        ushort itemId,
        byte categoryId,
        bool isPlaceholder = false)
    {
        if (itemId >= InventoryItemCount || !seen.Add(itemId))
        {
            return;
        }

        if (!P4GCatalog.ItemsById.TryGetValue(itemId, out ItemCatalogEntry item))
        {
            return;
        }

        items.Add(new InventoryItemChoiceViewState(itemId, categoryId, item.Name, isPlaceholder));
    }

    private static void AddPlaceholderChoice(List<InventoryItemChoiceViewState> items, ushort itemId, byte categoryId)
    {
        if (itemId >= InventoryItemCount)
        {
            return;
        }

        if (!P4GCatalog.ItemsById.TryGetValue(itemId, out ItemCatalogEntry item))
        {
            return;
        }

        items.Add(new InventoryItemChoiceViewState(itemId, categoryId, item.Name, true));
    }

    private static IEnumerable<ushort> EnumerateItemIds(CategoryDefinition definition)
    {
        foreach ((int start, int count) in definition.Ranges)
        {
            for (int offset = 0; offset < count; offset++)
            {
                yield return (ushort)(start + offset);
            }
        }
    }

    private static IEnumerable<ushort> EnumerateOtherItemIds()
    {
        foreach (ushort itemId in P4GCatalog.ItemsById.Keys.OrderBy(static itemId => itemId))
        {
            if (itemId >= InventoryItemCount || legacyCategorizedItemIds.Contains(itemId))
            {
                continue;
            }

            yield return itemId;
        }
    }

    private static string GetCategoryName(byte categoryId) =>
        categoryId < categories.Count ? categories[categoryId].Name : otherCategoryName;

    private sealed record CategoryDefinition(byte CategoryId, ushort PlaceholderItemId, (int Start, int Count)[] Ranges);

    private sealed record InventoryItemMetadata(ushort ItemId, byte CategoryId, string CategoryName, string ItemName);
}
