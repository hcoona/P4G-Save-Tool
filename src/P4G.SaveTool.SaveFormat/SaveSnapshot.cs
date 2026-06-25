using System.Collections.ObjectModel;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.SaveFormat;

public sealed class SaveSnapshot
{
    private readonly byte[] originalBytes;
    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<PersonaSlot> protagonistPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> partyPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> compendiumPersonaSlots;
    private readonly ReadOnlyCollection<InventoryStack> inventoryStacks;

    internal SaveSnapshot(
        P4GSaveLayoutKind layoutKind,
        byte[] originalBytes,
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack>? inventoryStacks = null)
    {
        LayoutKind = layoutKind;
        this.originalBytes = (byte[])originalBytes.Clone();
        Names = names;
        Yen = yen;
        this.partyMembers = Array.AsReadOnly(partyMembers.ToArray());
        this.protagonistPersonaSlots = Array.AsReadOnly(protagonistPersonaSlots.ToArray());
        this.partyPersonaSlots = Array.AsReadOnly(partyPersonaSlots.ToArray());
        this.compendiumPersonaSlots = Array.AsReadOnly(compendiumPersonaSlots.ToArray());
        this.inventoryStacks = Array.AsReadOnly((inventoryStacks ?? Array.Empty<InventoryStack>()).ToArray());
    }

    public P4GSaveLayoutKind LayoutKind { get; }

    public ReadOnlyMemory<byte> OriginalBytes => CopyOriginalBytes();

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

    public IReadOnlyList<PersonaSlot> ProtagonistPersonaSlots => protagonistPersonaSlots;

    public IReadOnlyList<PersonaSlot> PartyPersonaSlots => partyPersonaSlots;

    public IReadOnlyList<PersonaSlot> CompendiumPersonaSlots => compendiumPersonaSlots;

    public IReadOnlyList<InventoryStack> InventoryStacks => inventoryStacks;

    internal byte[] CopyOriginalBytes() => (byte[])originalBytes.Clone();
}
