using System.Collections.ObjectModel;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Contracts;

public sealed class WorkingSaveState
{
    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<PersonaSlot> protagonistPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> partyPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> compendiumPersonaSlots;
    private readonly ReadOnlyCollection<InventoryStack> inventoryStacks;

    public WorkingSaveState(
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack>? inventoryStacks = null)
    {
        ArgumentNullException.ThrowIfNull(names);

        Names = names;
        Yen = yen;
        this.partyMembers = CopyReadOnly(partyMembers, nameof(partyMembers));
        this.protagonistPersonaSlots = CopyReadOnly(protagonistPersonaSlots, nameof(protagonistPersonaSlots));
        this.partyPersonaSlots = CopyReadOnly(partyPersonaSlots, nameof(partyPersonaSlots));
        this.compendiumPersonaSlots = CopyReadOnly(compendiumPersonaSlots, nameof(compendiumPersonaSlots));
        this.inventoryStacks = CopyReadOnly(inventoryStacks ?? Array.Empty<InventoryStack>(), nameof(inventoryStacks));
    }

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

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
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks);
    }

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
                protagonistPersonaSlots,
                partyPersonaSlots,
                compendiumPersonaSlots,
                updatedInventory);
        }

        List<InventoryStack> insertedInventory = inventoryStacks.ToList();
        insertedInventory.Insert(FindInventoryInsertIndex(itemId), updatedStack);
        return new(
            Names,
            Yen,
            partyMembers,
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

    private WorkingSaveState WithInventoryItemRemoved(int itemIndex)
    {
        List<InventoryStack> updatedInventory = inventoryStacks.ToList();
        updatedInventory.RemoveAt(itemIndex);
        return new(
            Names,
            Yen,
            partyMembers,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            updatedInventory);
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

    private int FindInventoryInsertIndex(ushort itemId)
    {
        for (int index = 0; index < inventoryStacks.Count; index++)
        {
            if (inventoryStacks[index].ItemId > itemId)
            {
                return index;
            }
        }

        return inventoryStacks.Count;
    }
}
