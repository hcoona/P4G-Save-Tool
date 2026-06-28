namespace P4G.SaveTool.Presentation;

public sealed record EquipmentCharacterViewState(
    byte CharacterId,
    string Name,
    ushort WeaponItemId,
    ushort ArmorItemId,
    ushort AccessoryItemId,
    ushort CostumeItemId)
{
    public override string ToString() => Name;
}
