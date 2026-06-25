using P4G.SaveTool.Contracts;
using P4G.SaveTool.Presentation;
using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class PersonaEditBatchTests
{
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
