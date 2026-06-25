using System.Collections.ObjectModel;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Contracts;

public sealed class WorkingSaveState
{
    private const int EquipmentSlotCount = 8;

    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<ushort> equippedWeapons;
    private readonly ReadOnlyCollection<ushort> equippedArmors;
    private readonly ReadOnlyCollection<ushort> equippedAccessories;
    private readonly ReadOnlyCollection<ushort> equippedCostumes;
    private readonly ReadOnlyCollection<PersonaSlot> protagonistPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> partyPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> compendiumPersonaSlots;
    private readonly ReadOnlyCollection<InventoryStack> inventoryStacks;

    public WorkingSaveState(
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<ushort> equippedWeapons,
        IReadOnlyList<ushort> equippedArmors,
        IReadOnlyList<ushort> equippedAccessories,
        IReadOnlyList<ushort> equippedCostumes,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack>? inventoryStacks = null)
    {
        ArgumentNullException.ThrowIfNull(names);

        Names = names;
        Yen = yen;
        this.partyMembers = CopyReadOnly(partyMembers, nameof(partyMembers));
        this.equippedWeapons = CopyFixedLength(equippedWeapons, EquipmentSlotCount, nameof(equippedWeapons));
        this.equippedArmors = CopyFixedLength(equippedArmors, EquipmentSlotCount, nameof(equippedArmors));
        this.equippedAccessories = CopyFixedLength(equippedAccessories, EquipmentSlotCount, nameof(equippedAccessories));
        this.equippedCostumes = CopyFixedLength(equippedCostumes, EquipmentSlotCount, nameof(equippedCostumes));
        this.protagonistPersonaSlots = CopyReadOnly(protagonistPersonaSlots, nameof(protagonistPersonaSlots));
        this.partyPersonaSlots = CopyReadOnly(partyPersonaSlots, nameof(partyPersonaSlots));
        this.compendiumPersonaSlots = CopyReadOnly(compendiumPersonaSlots, nameof(compendiumPersonaSlots));
        this.inventoryStacks = CopyReadOnly(inventoryStacks ?? Array.Empty<InventoryStack>(), nameof(inventoryStacks));
    }

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

    public IReadOnlyList<ushort> EquippedWeapons => equippedWeapons;

    public IReadOnlyList<ushort> EquippedArmors => equippedArmors;

    public IReadOnlyList<ushort> EquippedAccessories => equippedAccessories;

    public IReadOnlyList<ushort> EquippedCostumes => equippedCostumes;

    public IReadOnlyList<PersonaSlot> ProtagonistPersonaSlots => protagonistPersonaSlots;

    public IReadOnlyList<PersonaSlot> PartyPersonaSlots => partyPersonaSlots;

    public IReadOnlyList<PersonaSlot> CompendiumPersonaSlots => compendiumPersonaSlots;

    public IReadOnlyList<InventoryStack> InventoryStacks => inventoryStacks;

    public WorkingSaveState WithNames(SaveNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new(
            names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks);
    }

    public WorkingSaveState WithYen(uint yen) =>
        new(
            Names,
            yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks);

    public WorkingSaveState WithPartyMember(int slotIndex, PartyMemberId memberId)
    {
        if ((uint)slotIndex >= (uint)partyMembers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Party member slot is out of range.");
        }

        PartyMemberId[] updatedPartyMembers = partyMembers.ToArray();
        updatedPartyMembers[slotIndex] = memberId;
        return new(
            Names,
            Yen,
            updatedPartyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks);
    }

    public WorkingSaveState WithEquippedWeapon(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Weapon);

    public WorkingSaveState WithEquippedArmor(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Armor);

    public WorkingSaveState WithEquippedAccessory(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Accessory);

    public WorkingSaveState WithEquippedCostume(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Costume);

    public WorkingSaveState WithInventoryItemQuantity(ushort itemId, byte quantity)
    {
        int itemIndex = FindInventoryItemIndex(itemId);
        if (quantity == 0)
        {
            return itemIndex < 0 ? this : WithInventoryItemRemoved(itemIndex);
        }

        InventoryStack updatedStack = new(itemId, quantity);
        if (itemIndex >= 0)
        {
            if (inventoryStacks[itemIndex].Equals(updatedStack))
            {
                return this;
            }

            List<InventoryStack> updatedInventory = inventoryStacks.ToList();
            updatedInventory[itemIndex] = updatedStack;
            return new(
                Names,
                Yen,
                partyMembers,
                equippedWeapons,
                equippedArmors,
                equippedAccessories,
                equippedCostumes,
                protagonistPersonaSlots,
                partyPersonaSlots,
                compendiumPersonaSlots,
                updatedInventory);
        }

        List<InventoryStack> insertedInventory = inventoryStacks.ToList();
        insertedInventory.Add(updatedStack);
        return new(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            insertedInventory);
    }

    public WorkingSaveState WithInventoryItemRemoved(ushort itemId)
    {
        int itemIndex = FindInventoryItemIndex(itemId);
        return itemIndex < 0 ? this : WithInventoryItemRemoved(itemIndex);
    }

    private static ReadOnlyCollection<T> CopyReadOnly<T>(IReadOnlyCollection<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        return Array.AsReadOnly(values.ToArray());
    }

    private static ReadOnlyCollection<T> CopyFixedLength<T>(IReadOnlyCollection<T> values, int expectedLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count != expectedLength)
        {
            throw new ArgumentException(
                $"Equipment field must contain exactly {expectedLength} values.",
                parameterName);
        }

        return Array.AsReadOnly(values.ToArray());
    }

    private WorkingSaveState WithInventoryItemRemoved(int itemIndex)
    {
        List<InventoryStack> updatedInventory = inventoryStacks.ToList();
        updatedInventory.RemoveAt(itemIndex);
        return new(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            updatedInventory);
    }

    private WorkingSaveState WithEquipment(int characterId, ushort itemId, EquipmentKind kind)
    {
        if ((uint)characterId >= (uint)equippedWeapons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(characterId), characterId, "Equipment character slot is out of range.");
        }

        bool isUnchanged = kind switch
        {
            EquipmentKind.Weapon => equippedWeapons[characterId] == itemId,
            EquipmentKind.Armor => equippedArmors[characterId] == itemId,
            EquipmentKind.Accessory => equippedAccessories[characterId] == itemId,
            EquipmentKind.Costume => equippedCostumes[characterId] == itemId,
            _ => false,
        };
        if (isUnchanged)
        {
            return this;
        }

        ushort[] updatedWeapons = equippedWeapons.ToArray();
        ushort[] updatedArmors = equippedArmors.ToArray();
        ushort[] updatedAccessories = equippedAccessories.ToArray();
        ushort[] updatedCostumes = equippedCostumes.ToArray();
        switch (kind)
        {
            case EquipmentKind.Weapon:
                updatedWeapons[characterId] = itemId;
                break;
            case EquipmentKind.Armor:
                updatedArmors[characterId] = itemId;
                break;
            case EquipmentKind.Accessory:
                updatedAccessories[characterId] = itemId;
                break;
            case EquipmentKind.Costume:
                updatedCostumes[characterId] = itemId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported equipment kind.");
        }

        return new(
            Names,
            Yen,
            partyMembers,
            updatedWeapons,
            updatedArmors,
            updatedAccessories,
            updatedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks);
    }

    private enum EquipmentKind
    {
        Weapon,
        Armor,
        Accessory,
        Costume,
    }

    private int FindInventoryItemIndex(ushort itemId)
    {
        for (int index = 0; index < inventoryStacks.Count; index++)
        {
            if (inventoryStacks[index].ItemId == itemId)
            {
                return index;
            }
        }

        return -1;
    }
}
