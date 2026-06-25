using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Application;

internal static class EquipmentItemEditability
{
    public static bool IsSupportedEquipmentCharacterId(int characterId) =>
        EquipmentItemRules.IsSupportedEquipmentCharacterId(characterId);

    public static bool IsSupportedWeaponItemId(int characterId, ushort itemId) =>
        EquipmentItemRules.IsSupportedWeaponItemId(characterId, itemId);

    public static IEnumerable<ushort> EnumerateWeaponItemIds(int characterId) =>
        EquipmentItemRules.EnumerateWeaponItemIds(characterId);

    public static bool IsSupportedArmorItemId(int characterId, ushort itemId) =>
        EquipmentItemRules.IsSupportedArmorItemId(itemId);

    public static bool IsSupportedAccessoryItemId(int characterId, ushort itemId) =>
        EquipmentItemRules.IsSupportedAccessoryItemId(itemId);

    public static bool IsSupportedCostumeItemId(int characterId, ushort itemId) =>
        EquipmentItemRules.IsSupportedCostumeItemId(itemId);
}
