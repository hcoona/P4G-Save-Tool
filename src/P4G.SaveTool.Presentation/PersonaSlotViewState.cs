using System.Collections.ObjectModel;

namespace P4G.SaveTool.Presentation;

public sealed class PersonaSlotViewState
{
    private readonly ReadOnlyCollection<ushort> skillIds;

    public PersonaSlotViewState(
        int slotIndex,
        bool exists,
        ushort personaId,
        byte level,
        uint totalExperience,
        IReadOnlyList<ushort> skillIds,
        byte strength,
        byte magic,
        byte endurance,
        byte agility,
        byte luck)
    {
        ArgumentNullException.ThrowIfNull(skillIds);

        SlotIndex = slotIndex;
        Exists = exists;
        PersonaId = personaId;
        Level = level;
        TotalExperience = totalExperience;
        this.skillIds = Array.AsReadOnly(skillIds.ToArray());
        Strength = strength;
        Magic = magic;
        Endurance = endurance;
        Agility = agility;
        Luck = luck;
    }

    public int SlotIndex { get; }

    public bool Exists { get; }

    public ushort PersonaId { get; }

    public byte Level { get; }

    public uint TotalExperience { get; }

    public IReadOnlyList<ushort> SkillIds => skillIds;

    public byte Strength { get; }

    public byte Magic { get; }

    public byte Endurance { get; }

    public byte Agility { get; }

    public byte Luck { get; }
}
