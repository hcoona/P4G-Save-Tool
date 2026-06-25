namespace P4G.SaveTool.Contracts;

public abstract record SaveEditCommand;

public sealed record SetSaveNamesEdit(string FamilyName, string GivenName) : SaveEditCommand;

public sealed record SetYenEdit(uint Yen) : SaveEditCommand;

public sealed record SetPartyMemberEdit(int SlotIndex, byte MemberValue) : SaveEditCommand;

public sealed record SetSocialStatRankEdit(int StatIndex, int Rank) : SaveEditCommand;

public sealed record SetDayEdit(int Day) : SaveEditCommand;

public sealed record SetDayPhaseEdit(int PhaseId) : SaveEditCommand;

public sealed record SetNextDayEdit(int Day) : SaveEditCommand;

public sealed record SetNextDayPhaseEdit(int PhaseId) : SaveEditCommand;

public sealed record SetEquippedWeaponEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedArmorEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedAccessoryEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetEquippedCostumeEdit(int CharacterId, ushort ItemId) : SaveEditCommand;

public sealed record SetInventoryItemQuantityEdit(ushort ItemId, byte Quantity) : SaveEditCommand;

public sealed record RemoveInventoryItemEdit(ushort ItemId) : SaveEditCommand;

public sealed record SetProtagonistPersonaSlotEdit(int SlotIndex, PersonaSlotEdit PersonaSlot) : SaveEditCommand;

public sealed record SetPartyPersonaSlotEdit(int SlotIndex, PersonaSlotEdit PersonaSlot) : SaveEditCommand;
