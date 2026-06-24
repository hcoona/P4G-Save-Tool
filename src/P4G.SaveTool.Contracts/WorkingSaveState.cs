using System.Collections.ObjectModel;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Contracts;

public sealed class WorkingSaveState
{
    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<PersonaSlot> protagonistPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> partyPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> compendiumPersonaSlots;

    public WorkingSaveState(
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots)
    {
        ArgumentNullException.ThrowIfNull(names);

        Names = names;
        Yen = yen;
        this.partyMembers = CopyReadOnly(partyMembers, nameof(partyMembers));
        this.protagonistPersonaSlots = CopyReadOnly(protagonistPersonaSlots, nameof(protagonistPersonaSlots));
        this.partyPersonaSlots = CopyReadOnly(partyPersonaSlots, nameof(partyPersonaSlots));
        this.compendiumPersonaSlots = CopyReadOnly(compendiumPersonaSlots, nameof(compendiumPersonaSlots));
    }

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

    public IReadOnlyList<PersonaSlot> ProtagonistPersonaSlots => protagonistPersonaSlots;

    public IReadOnlyList<PersonaSlot> PartyPersonaSlots => partyPersonaSlots;

    public IReadOnlyList<PersonaSlot> CompendiumPersonaSlots => compendiumPersonaSlots;

    public WorkingSaveState WithNames(SaveNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new(
            names,
            Yen,
            partyMembers,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots);
    }

    public WorkingSaveState WithYen(uint yen) =>
        new(
            Names,
            yen,
            partyMembers,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots);

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
            compendiumPersonaSlots);
    }

    private static ReadOnlyCollection<T> CopyReadOnly<T>(IReadOnlyCollection<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        return Array.AsReadOnly(values.ToArray());
    }
}
