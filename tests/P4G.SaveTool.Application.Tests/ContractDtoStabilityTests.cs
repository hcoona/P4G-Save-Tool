using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using Xunit;

namespace P4G.SaveTool.Application.Tests;

public sealed class ContractDtoStabilityTests
{
    [Fact]
    public void SaveEditResultCopiesDiagnosticsAndFreezesSucceeded()
    {
        object save = new();
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "WARN", "Original warning.", "Edit");
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "ERR", "Later error.", "Edit");
        List<SaveDiagnostic> diagnostics = [warning];

        SaveEditResult<object> result = new(save, diagnostics);
        diagnostics[0] = error;
        diagnostics.Add(error);

        Assert.Same(save, result.Save);
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { warning }, result.Diagnostics);
        AssertReadOnlyListDoesNotAllowMutation(result.Diagnostics, error);
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { warning }, result.Diagnostics);
    }

    [Fact]
    public void SaveEditResultFailureRemainsStableWhenCallerDiagnosticsChange()
    {
        object save = new();
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "ERR", "Original error.", "Edit");
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "WARN", "Later warning.", "Edit");
        SaveDiagnostic[] diagnostics = [error];

        SaveEditResult<object> result = new(save, diagnostics);
        diagnostics[0] = warning;

        Assert.Same(save, result.Save);
        Assert.False(result.Succeeded);
        Assert.Equal(new[] { error }, result.Diagnostics);
        AssertReadOnlyListDoesNotAllowMutation(result.Diagnostics, warning);
        Assert.False(result.Succeeded);
        Assert.Equal(new[] { error }, result.Diagnostics);
    }

    [Fact]
    public void WorkingSaveStateCopiesConstructorCollections()
    {
        List<PartyMemberId> partyMembers = [new(0x01), new(0x02), new(0x03)];
        PersonaSlot protagonistSlot = CreatePersonaSlot(0x101);
        PersonaSlot partySlot = CreatePersonaSlot(0x202);
        PersonaSlot compendiumSlot = CreatePersonaSlot(0x303);
        PersonaSlot replacementSlot = CreatePersonaSlot(0x404);
        InventoryStack inventoryStack = new(0x101, 3);
        InventoryStack replacementInventoryStack = new(0x202, 4);
        PersonaSlot[] protagonistSlots = [protagonistSlot];
        List<PersonaSlot> partySlots = [partySlot];
        List<PersonaSlot> compendiumSlots = [compendiumSlot];
        List<InventoryStack> inventoryStacks = [inventoryStack];

        WorkingSaveState state = new(
            new SaveNames("Sato", "Yu"),
            123456u,
            partyMembers,
            protagonistSlots,
            partySlots,
            compendiumSlots,
            inventoryStacks);

        partyMembers[0] = new PartyMemberId(0xfe);
        partyMembers.Add(new PartyMemberId(0xfd));
        protagonistSlots[0] = replacementSlot;
        partySlots[0] = replacementSlot;
        partySlots.Add(replacementSlot);
        compendiumSlots.Clear();
        inventoryStacks[0] = replacementInventoryStack;
        inventoryStacks.Add(replacementInventoryStack);

        Assert.Equal(new SaveNames("Sato", "Yu"), state.Names);
        Assert.Equal(123456u, state.Yen);
        Assert.Equal(new[] { new PartyMemberId(0x01), new PartyMemberId(0x02), new PartyMemberId(0x03) }, state.PartyMembers);
        Assert.Equal(new[] { protagonistSlot }, state.ProtagonistPersonaSlots);
        Assert.Equal(new[] { partySlot }, state.PartyPersonaSlots);
        Assert.Equal(new[] { compendiumSlot }, state.CompendiumPersonaSlots);
        AssertReadOnlyListDoesNotAllowMutation(state.PartyMembers, new PartyMemberId(0xee));
        AssertReadOnlyListDoesNotAllowMutation(state.ProtagonistPersonaSlots, replacementSlot);
        AssertReadOnlyListDoesNotAllowMutation(state.PartyPersonaSlots, replacementSlot);
        AssertReadOnlyListDoesNotAllowMutation(state.CompendiumPersonaSlots, replacementSlot);
        AssertReadOnlyListDoesNotAllowMutation(state.InventoryStacks, replacementInventoryStack);
        Assert.Equal(new[] { new PartyMemberId(0x01), new PartyMemberId(0x02), new PartyMemberId(0x03) }, state.PartyMembers);
        Assert.Equal(new[] { protagonistSlot }, state.ProtagonistPersonaSlots);
        Assert.Equal(new[] { partySlot }, state.PartyPersonaSlots);
        Assert.Equal(new[] { compendiumSlot }, state.CompendiumPersonaSlots);
        Assert.Equal(new[] { inventoryStack }, state.InventoryStacks);
    }

    private static PersonaSlot CreatePersonaSlot(ushort personaId) =>
        new(
            exists: true,
            unknown0: 0xa5,
            personaId,
            level: 77,
            reservedAfterLevel: [0xc1, 0xc2, 0xc3],
            totalExperience: 0x01020304,
            skillIds: [0x1001, 0x1002, 0x1003, 0x1004, 0x1005, 0x1006, 0x1007, 0x1008],
            strength: 11,
            magic: 22,
            endurance: 33,
            agility: 44,
            luck: 55);

    private static void AssertReadOnlyListDoesNotAllowMutation<T>(IReadOnlyList<T> collection, T replacement)
    {
        Assert.False(collection.GetType().IsArray, $"Collection exposes mutable array type {collection.GetType()}.");
        if (collection is IList<T> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.NotEmpty(list);
            Assert.Throws<NotSupportedException>(() => list[0] = replacement);
            Assert.Throws<NotSupportedException>(() => list.Add(replacement));
        }
    }
}
