using System.Collections.ObjectModel;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.SaveFormat;

public sealed class SaveSnapshot
{
    private const int EquipmentSlotCount = 8;
    private const int SocialStatCount = P4G.SaveTool.Contracts.SocialStatRules.StatCount;

    private readonly byte[] originalBytes;
    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<ushort> equippedWeapons;
    private readonly ReadOnlyCollection<ushort> equippedArmors;
    private readonly ReadOnlyCollection<ushort> equippedAccessories;
    private readonly ReadOnlyCollection<ushort> equippedCostumes;
    private readonly ReadOnlyCollection<ushort> socialStats;
    private readonly ReadOnlyCollection<SocialLinkState> socialLinks;
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
        IReadOnlyList<ushort> equippedWeapons,
        IReadOnlyList<ushort> equippedArmors,
        IReadOnlyList<ushort> equippedAccessories,
        IReadOnlyList<ushort> equippedCostumes,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack>? inventoryStacks = null,
        IReadOnlyList<ushort>? socialStats = null,
        IReadOnlyList<SocialLinkState>? socialLinks = null,
        byte day = 0,
        byte dayPhase = 0,
        byte nextDay = 0,
        byte nextDayPhase = 0)
    {
        LayoutKind = layoutKind;
        this.originalBytes = (byte[])originalBytes.Clone();
        Names = names;
        Yen = yen;
        this.partyMembers = Array.AsReadOnly(partyMembers.ToArray());
        this.equippedWeapons = CopyFixedLength(equippedWeapons, EquipmentSlotCount, nameof(equippedWeapons));
        this.equippedArmors = CopyFixedLength(equippedArmors, EquipmentSlotCount, nameof(equippedArmors));
        this.equippedAccessories = CopyFixedLength(equippedAccessories, EquipmentSlotCount, nameof(equippedAccessories));
        this.equippedCostumes = CopyFixedLength(equippedCostumes, EquipmentSlotCount, nameof(equippedCostumes));
        this.socialStats = CopyFixedLength(socialStats ?? new ushort[SocialStatCount], SocialStatCount, nameof(socialStats));
        this.socialLinks = Array.AsReadOnly((socialLinks ?? Array.Empty<SocialLinkState>()).ToArray());
        this.protagonistPersonaSlots = Array.AsReadOnly(protagonistPersonaSlots.ToArray());
        this.partyPersonaSlots = Array.AsReadOnly(partyPersonaSlots.ToArray());
        this.compendiumPersonaSlots = Array.AsReadOnly(compendiumPersonaSlots.ToArray());
        this.inventoryStacks = Array.AsReadOnly((inventoryStacks ?? Array.Empty<InventoryStack>()).ToArray());
        Day = day;
        DayPhase = dayPhase;
        NextDay = nextDay;
        NextDayPhase = nextDayPhase;
    }

    public P4GSaveLayoutKind LayoutKind { get; }

    public ReadOnlyMemory<byte> OriginalBytes => CopyOriginalBytes();

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

    public IReadOnlyList<ushort> EquippedWeapons => equippedWeapons;

    public IReadOnlyList<ushort> EquippedArmors => equippedArmors;

    public IReadOnlyList<ushort> EquippedAccessories => equippedAccessories;

    public IReadOnlyList<ushort> EquippedCostumes => equippedCostumes;

    public IReadOnlyList<ushort> SocialStats => socialStats;

    public IReadOnlyList<SocialLinkState> SocialLinks => socialLinks;

    public IReadOnlyList<PersonaSlot> ProtagonistPersonaSlots => protagonistPersonaSlots;

    public IReadOnlyList<PersonaSlot> PartyPersonaSlots => partyPersonaSlots;

    public IReadOnlyList<PersonaSlot> CompendiumPersonaSlots => compendiumPersonaSlots;

    public IReadOnlyList<InventoryStack> InventoryStacks => inventoryStacks;

    public byte Day { get; }

    public byte DayPhase { get; }

    public byte NextDay { get; }

    public byte NextDayPhase { get; }

    internal byte[] CopyOriginalBytes() => (byte[])originalBytes.Clone();

    private static ReadOnlyCollection<T> CopyFixedLength<T>(IReadOnlyCollection<T> values, int expectedLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count != expectedLength)
        {
            throw new ArgumentException(
                $"Field must contain exactly {expectedLength} values.",
                parameterName);
        }

        return Array.AsReadOnly(values.ToArray());
    }
}
