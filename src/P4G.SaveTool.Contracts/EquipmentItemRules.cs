namespace P4G.SaveTool.Contracts;

public static class EquipmentItemRules
{
    private static readonly (ushort Start, ushort End)[][] WeaponRangesByCharacter =
    [
        [(1, 36), (2305, 2315), (2434, 2434)],
        [(39, 70), (2326, 2334), (2435, 2435)],
        [(112, 142), (2367, 2375), (2436, 2436)],
        [(77, 104), (2345, 2355), (2437, 2437)],
        [],
        [(150, 175), (2385, 2387), (2389, 2396), (2438, 2438)],
        [(183, 197), (2407, 2414), (2439, 2439)],
        [(217, 238), (2425, 2432), (2440, 2440)],
    ];

    private static readonly (ushort Start, ushort End)[] ArmorRanges =
    [
        (256, 264),
        (266, 271),
        (287, 290),
        (293, 296),
        (307, 310),
        (315, 317),
        (328, 331),
        (334, 338),
        (347, 348),
        (350, 358),
        (367, 370),
        (372, 378),
        (387, 392),
        (394, 398),
        (407, 412),
        (414, 418),
        (420, 432),
    ];

    private static readonly (ushort Start, ushort End)[] AccessoryRanges =
    [
        (512, 608),
        (615, 683),
        (685, 685),
        (687, 694),
        (754, 766),
    ];

    private static readonly (ushort Start, ushort End)[] CostumeRanges =
    [
        (1792, 1984),
        (2040, 2045),
    ];

    public static bool IsSupportedEquipmentCharacterId(int characterId) =>
        characterId is >= 0 and <= 7 and not 4;

    public static bool IsSupportedWeaponItemId(int characterId, ushort itemId) =>
        IsSupportedEquipmentCharacterId(characterId) &&
        (itemId == 0 || IsInRanges(itemId, WeaponRangesByCharacter[characterId]));

    public static IEnumerable<ushort> EnumerateWeaponItemIds(int characterId)
    {
        if (!IsSupportedEquipmentCharacterId(characterId))
        {
            yield break;
        }

        foreach ((ushort start, ushort end) in WeaponRangesByCharacter[characterId])
        {
            for (ushort itemId = start; itemId <= end; itemId++)
            {
                yield return itemId;
            }
        }
    }

    public static bool IsSupportedArmorItemId(ushort itemId) => itemId == 0 || IsInRanges(itemId, ArmorRanges);

    public static bool IsSupportedAccessoryItemId(ushort itemId) => itemId == 0 || IsInRanges(itemId, AccessoryRanges);

    public static bool IsSupportedCostumeItemId(ushort itemId) => itemId == 0 || IsInRanges(itemId, CostumeRanges);

    private static bool IsInRanges(ushort itemId, ReadOnlySpan<(ushort Start, ushort End)> ranges)
    {
        foreach ((ushort start, ushort end) in ranges)
        {
            if (itemId >= start && itemId <= end)
            {
                return true;
            }
        }

        return false;
    }
}
