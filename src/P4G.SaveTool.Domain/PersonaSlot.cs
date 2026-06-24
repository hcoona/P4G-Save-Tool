namespace P4G.SaveTool.Domain;

public sealed class PersonaSlot : IEquatable<PersonaSlot>
{
    public const int ReservedAfterLevelLength = 3;
    public const int SkillCount = 8;

    private readonly byte[] reservedAfterLevel;
    private readonly ushort[] skillIds;

    public PersonaSlot(
        bool exists,
        byte unknown0,
        ushort personaId,
        byte level,
        IReadOnlyList<byte> reservedAfterLevel,
        uint totalExperience,
        IReadOnlyList<ushort> skillIds,
        byte strength,
        byte magic,
        byte endurance,
        byte agility,
        byte luck)
        : this(
            exists ? (byte)1 : (byte)0,
            unknown0,
            personaId,
            level,
            reservedAfterLevel,
            totalExperience,
            skillIds,
            strength,
            magic,
            endurance,
            agility,
            luck)
    {
    }

    public PersonaSlot(
        byte existsRawByte,
        byte unknown0,
        ushort personaId,
        byte level,
        IReadOnlyList<byte> reservedAfterLevel,
        uint totalExperience,
        IReadOnlyList<ushort> skillIds,
        byte strength,
        byte magic,
        byte endurance,
        byte agility,
        byte luck)
    {
        ExistsRawByte = existsRawByte;
        Exists = existsRawByte != 0;
        Unknown0 = unknown0;
        PersonaId = personaId;
        Level = level;
        this.reservedAfterLevel = CopyFixedLength(reservedAfterLevel, ReservedAfterLevelLength, nameof(reservedAfterLevel));
        TotalExperience = totalExperience;
        this.skillIds = CopyFixedLength(skillIds, SkillCount, nameof(skillIds));
        Strength = strength;
        Magic = magic;
        Endurance = endurance;
        Agility = agility;
        Luck = luck;
    }

    public bool Exists { get; }

    public byte ExistsRawByte { get; }

    public byte Unknown0 { get; }

    public ushort PersonaId { get; }

    public byte Level { get; }

    public IReadOnlyList<byte> ReservedAfterLevel => Array.AsReadOnly(reservedAfterLevel);

    public uint TotalExperience { get; }

    public IReadOnlyList<ushort> SkillIds => Array.AsReadOnly(skillIds);

    public byte Strength { get; }

    public byte Magic { get; }

    public byte Endurance { get; }

    public byte Agility { get; }

    public byte Luck { get; }

    public bool Equals(PersonaSlot? other) =>
        other is not null &&
        Exists == other.Exists &&
        ExistsRawByte == other.ExistsRawByte &&
        Unknown0 == other.Unknown0 &&
        PersonaId == other.PersonaId &&
        Level == other.Level &&
        reservedAfterLevel.SequenceEqual(other.reservedAfterLevel) &&
        TotalExperience == other.TotalExperience &&
        skillIds.SequenceEqual(other.skillIds) &&
        Strength == other.Strength &&
        Magic == other.Magic &&
        Endurance == other.Endurance &&
        Agility == other.Agility &&
        Luck == other.Luck;

    public override bool Equals(object? obj) => Equals(obj as PersonaSlot);

    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(Exists);
        hashCode.Add(ExistsRawByte);
        hashCode.Add(Unknown0);
        hashCode.Add(PersonaId);
        hashCode.Add(Level);
        foreach (byte value in reservedAfterLevel)
        {
            hashCode.Add(value);
        }

        hashCode.Add(TotalExperience);
        foreach (ushort value in skillIds)
        {
            hashCode.Add(value);
        }

        hashCode.Add(Strength);
        hashCode.Add(Magic);
        hashCode.Add(Endurance);
        hashCode.Add(Agility);
        hashCode.Add(Luck);
        return hashCode.ToHashCode();
    }

    private static T[] CopyFixedLength<T>(IReadOnlyCollection<T> values, int expectedLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != expectedLength)
        {
            throw new ArgumentException(
                $"Persona slot field must contain exactly {expectedLength} values.",
                parameterName);
        }

        return values.ToArray();
    }
}
