using System.Globalization;

namespace P4G.SaveTool.WinUI;

internal sealed class CompendiumPersonaViewState
{
    public CompendiumPersonaViewState(int slotIndex, ushort personaId, string name, byte level, uint totalExperience)
    {
        SlotIndex = slotIndex;
        PersonaId = personaId;
        Name = name;
        Level = level;
        TotalExperience = totalExperience;
    }

    public int SlotIndex { get; }

    public ushort PersonaId { get; }

    public string Name { get; }

    public byte Level { get; }

    public uint TotalExperience { get; }

    public string DisplayName => string.Create(CultureInfo.InvariantCulture, $"#{SlotIndex + 1} {Name}");
}
