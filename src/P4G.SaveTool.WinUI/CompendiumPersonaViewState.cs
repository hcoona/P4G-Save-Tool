using System.Globalization;

namespace P4G.SaveTool.WinUI;

internal sealed class CompendiumPersonaViewState
{
    public CompendiumPersonaViewState(
        int slotIndex,
        ushort personaId,
        string name,
        byte level,
        uint totalExperience,
        byte strength = 0,
        byte magic = 0,
        byte endurance = 0,
        byte agility = 0,
        byte luck = 0)
    {
        SlotIndex = slotIndex;
        PersonaId = personaId;
        Name = name;
        Level = level;
        TotalExperience = totalExperience;
        Strength = strength;
        Magic = magic;
        Endurance = endurance;
        Agility = agility;
        Luck = luck;
    }

    public int SlotIndex { get; }

    public ushort PersonaId { get; }

    public string Name { get; }

    public byte Level { get; }

    public uint TotalExperience { get; }

    public byte Strength { get; }

    public byte Magic { get; }

    public byte Endurance { get; }

    public byte Agility { get; }

    public byte Luck { get; }

    public string DisplayName => string.Create(CultureInfo.InvariantCulture, $"#{SlotIndex + 1} {Name}");

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{DisplayName}  Lv {Level}  XP {TotalExperience}  ST {Strength}  MA {Magic}  DE {Endurance}  AG {Agility}  LU {Luck}");
}
