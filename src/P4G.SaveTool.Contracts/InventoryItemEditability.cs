using System.Collections.ObjectModel;
using System.Linq;

namespace P4G.SaveTool.Contracts;

public static class InventoryItemEditability
{
    private static readonly ReadOnlyCollection<ushort> placeholderItemIds =
        Array.AsReadOnly(new ushort[] { 0, 256, 512, 768, 1024, 1280, 1536, 1792, 2048 });

    public static IReadOnlyList<ushort> PlaceholderItemIds => placeholderItemIds;

    public static bool IsPlaceholderItemId(ushort itemId) =>
        placeholderItemIds.Contains(itemId);

    public static bool IsWritableItemId(ushort itemId) =>
        !IsPlaceholderItemId(itemId);
}
