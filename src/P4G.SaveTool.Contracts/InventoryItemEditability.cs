using System.Collections.ObjectModel;
using System.Linq;

namespace P4G.SaveTool.Contracts;

public static class InventoryItemEditability
{
    private static readonly ReadOnlyCollection<ushort> placeholderItemIds =
        Array.AsReadOnly(new ushort[]
        {
            0, 256, 512, 768, 1024, 1280, 1536, 1792, 1797, 1805, 1808, 1813, 1821, 1824, 1829,
            1835, 1836, 1837, 1839, 1845, 1853, 1861, 1865, 1866, 1869, 1870, 1872, 1877,
            1883, 1884, 1885, 1887, 1893, 1901, 1909, 1917, 1925, 1933, 1941, 1949, 1957,
            1965, 1973, 1981, 1984, 2048,
        });

    public static IReadOnlyList<ushort> PlaceholderItemIds => placeholderItemIds;

    public static bool IsPlaceholderItemId(ushort itemId) =>
        placeholderItemIds.Contains(itemId);

    public static bool IsWritableItemId(ushort itemId) =>
        !IsPlaceholderItemId(itemId);
}
