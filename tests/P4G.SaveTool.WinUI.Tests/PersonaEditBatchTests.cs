using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class PersonaEditBatchTests
{
    [Fact]
    public void PersonaEditBuilderBuildsSelectedCompendiumPersonaWithRoundedStats()
    {
        bool succeeded = MainWindow.TryBuildPersonaSlotEditCore(
            0x0404,
            "1234",
            [0x4401, 0x4402, 0x4403, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408],
            55.5,
            11.5,
            22.5,
            33.5,
            44.5,
            45.5,
            out PersonaSlotEdit personaSlotEdit,
            out SaveDiagnostic diagnostic);

        Assert.True(succeeded);
        Assert.Equal(0x0404, personaSlotEdit.PersonaId);
        Assert.Equal((uint)1234, personaSlotEdit.TotalExperience);
        Assert.Equal([0x4401, 0x4402, 0x4403, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408], personaSlotEdit.SkillIds);
        Assert.Equal((byte)56, personaSlotEdit.Level);
        Assert.Equal((byte)12, personaSlotEdit.Strength);
        Assert.Equal((byte)23, personaSlotEdit.Magic);
        Assert.Equal((byte)34, personaSlotEdit.Endurance);
        Assert.Equal((byte)45, personaSlotEdit.Agility);
        Assert.Equal((byte)46, personaSlotEdit.Luck);
        Assert.Equal("P4GWINUI014", diagnostic.Code);
    }

    [Fact]
    public void PersonaEditBuilderReportsInvalidExperienceForSelectedCompendiumPersona()
    {
        bool succeeded = MainWindow.TryBuildPersonaSlotEditCore(
            0x0404,
            "not-a-number",
            [0x4401, 0x4402, 0x4403, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408],
            55,
            11,
            22,
            33,
            44,
            45,
            out _,
            out SaveDiagnostic diagnostic);

        Assert.False(succeeded);
        Assert.Equal("P4GWINUI015", diagnostic.Code);
        Assert.Equal("Persona.Xp", diagnostic.Target);
    }

    [Fact]
    public void PersonaEditBuilderReportsMissingSkillSelectionForSelectedCompendiumPersona()
    {
        bool succeeded = MainWindow.TryBuildPersonaSlotEditCore(
            0x0404,
            "1234",
            [0x4401, 0x4402, ushort.MaxValue, 0x4404, 0x4405, 0x4406, 0x4407, 0x4408],
            55,
            11,
            22,
            33,
            44,
            45,
            out _,
            out SaveDiagnostic diagnostic);

        Assert.False(succeeded);
        Assert.Equal("P4GWINUI016", diagnostic.Code);
        Assert.Equal("Persona.Skills", diagnostic.Target);
    }

    [Fact]
    public void UnchangedBlankPersonaSlotsAreSkipped()
    {
        PersonaSlotViewState currentSlot = CreateBlankPersonaSlotViewState();
        PersonaSlotEdit proposedEdit = CreateBlankPersonaSlotEdit();

        Assert.True(MainWindow.ShouldSkipPersonaEdit(currentSlot, proposedEdit));
    }

    [Fact]
    public void ModifiedBlankPersonaSlotsAreStillApplied()
    {
        PersonaSlotViewState currentSlot = CreateBlankPersonaSlotViewState();
        PersonaSlotEdit proposedEdit = new(0, 1, 10, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0);

        Assert.False(MainWindow.ShouldSkipPersonaEdit(currentSlot, proposedEdit));
    }

    private static PersonaSlotViewState CreateBlankPersonaSlotViewState() =>
        new(
            slotIndex: 2,
            exists: false,
            personaId: 0,
            level: 0,
            totalExperience: 0,
            skillIds: [0, 0, 0, 0, 0, 0, 0, 0],
            strength: 0,
            magic: 0,
            endurance: 0,
            agility: 0,
            luck: 0);

    private static PersonaSlotEdit CreateBlankPersonaSlotEdit() =>
        new(0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0);
}
