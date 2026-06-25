using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Contracts;

public abstract record SaveEditCommand;

public sealed record SetSaveNamesEdit(SaveNames Names) : SaveEditCommand;

public sealed record SetYenEdit(uint Yen) : SaveEditCommand;

public sealed record SetPartyMemberEdit(int SlotIndex, PartyMemberId MemberId) : SaveEditCommand;

public sealed record SetEquippedWeaponEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedArmorEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedAccessoryEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedCostumeEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetInventoryItemQuantityEdit(ushort ItemId, byte Quantity) : SaveEditCommand;

public sealed record RemoveInventoryItemEdit(ushort ItemId) : SaveEditCommand;
