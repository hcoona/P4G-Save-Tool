namespace P4G.SaveTool.Contracts;

public sealed record PersonaSlotEdit(
    ushort PersonaId,
    byte Level,
    uint TotalExperience,
    IReadOnlyList<ushort> SkillIds,
    byte Strength,
    byte Magic,
    byte Endurance,
    byte Agility,
    byte Luck);
