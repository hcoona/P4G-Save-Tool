using System.Text.Json;
using System.Xml.Linq;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.Presentation;
using Xunit;

namespace P4G.SaveTool.Presentation.Tests;

public sealed class SaveEditorViewModelTests
{
    private static readonly string[] ForbiddenPresentationDependencyIds = ["Application", "Catalog", "SaveFormat"];

    [Fact]
    public void OpenSaveProjectsWorkingStateAndWarnings()
    {
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "WARN", "Opened with a warning.", "Open");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState()),
                [new SaveDiagnostic(DiagnosticSeverity.Warning, "WARN", "Opened with a warning.", "Open")]),
        };
        SaveEditorViewModel viewModel = new(service);
        byte[] input = [0x01, 0x02, 0x03];

        SaveEditorOperationResult result = viewModel.OpenSave(input);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("Sato", viewModel.FamilyName);
        Assert.Equal("Yu", viewModel.GivenName);
        Assert.Equal(123456u, viewModel.Yen);
        Assert.Equal(input, service.OpenInputs.Single());
        Assert.Equal(new[] { warning }, viewModel.Diagnostics);
        Assert.False(viewModel.HasErrors);
        Assert.Collection(
            viewModel.PartyMembers,
            static member =>
            {
                Assert.Equal(0, member.SlotIndex);
                Assert.Equal((byte)0x01, member.MemberValue);
            },
            static member =>
            {
                Assert.Equal(1, member.SlotIndex);
                Assert.Equal((byte)0xfe, member.MemberValue);
            },
            static member =>
            {
                Assert.Equal(2, member.SlotIndex);
                Assert.Equal((byte)0x80, member.MemberValue);
            });
        PersonaSlotViewState protagonistPersona = Assert.Single(viewModel.ProtagonistPersonaSlots);
        Assert.True(protagonistPersona.Exists);
        Assert.Equal(0, protagonistPersona.SlotIndex);
        Assert.Equal((ushort)0x0101, protagonistPersona.PersonaId);
        Assert.Equal((byte)77, protagonistPersona.Level);
        Assert.Equal(0x01010101u, protagonistPersona.TotalExperience);
        Assert.Equal(new ushort[] { 0x1101, 0x1102, 0x1103, 0x1104, 0x1105, 0x1106, 0x1107, 0x1108 }, protagonistPersona.SkillIds);
        Assert.Equal((byte)11, protagonistPersona.Strength);
        Assert.Equal((byte)22, protagonistPersona.Magic);
        Assert.Equal((byte)33, protagonistPersona.Endurance);
        Assert.Equal((byte)44, protagonistPersona.Agility);
        Assert.Equal((byte)55, protagonistPersona.Luck);
        PersonaSlotViewState partyPersona = Assert.Single(viewModel.PartyPersonaSlots);
        Assert.True(partyPersona.Exists);
        Assert.Equal(0, partyPersona.SlotIndex);
        Assert.Equal((ushort)0x0202, partyPersona.PersonaId);
        Assert.Equal((byte)44, partyPersona.Level);
        Assert.Equal(0x02020202u, partyPersona.TotalExperience);
        Assert.Equal(new ushort[] { 0x2201, 0x2202, 0x2203, 0x2204, 0x2205, 0x2206, 0x2207, 0x2208 }, partyPersona.SkillIds);
        PersonaSlotViewState compendiumPersona = Assert.Single(viewModel.CompendiumPersonaSlots);
        Assert.True(compendiumPersona.Exists);
        Assert.Equal(0, compendiumPersona.SlotIndex);
        Assert.Equal((ushort)0x0303, compendiumPersona.PersonaId);
        Assert.Equal((byte)22, compendiumPersona.Level);
        Assert.Equal(0x03030303u, compendiumPersona.TotalExperience);
        Assert.Equal(new ushort[] { 0x3301, 0x3302, 0x3303, 0x3304, 0x3305, 0x3306, 0x3307, 0x3308 }, compendiumPersona.SkillIds);
        AssertReadOnlyListDoesNotAllowMutation(viewModel.PartyMembers, new PartyMemberSlotViewState(0, 0xff));
        AssertReadOnlyListDoesNotAllowMutation(viewModel.ProtagonistPersonaSlots, protagonistPersona);
        AssertReadOnlyListDoesNotAllowMutation(viewModel.PartyPersonaSlots, partyPersona);
        AssertReadOnlyListDoesNotAllowMutation(viewModel.CompendiumPersonaSlots, compendiumPersona);
    }

    [Fact]
    public void OpenSaveFailureSurfacesDiagnosticsWithoutLoadingState()
    {
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "ERR", "Invalid save.", "Open");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(null, [error]),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(new byte[] { 0xff });

        Assert.False(result.Succeeded);
        Assert.False(viewModel.HasSave);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.FamilyName);
        Assert.Equal(string.Empty, viewModel.GivenName);
        Assert.Equal(0u, viewModel.Yen);
        Assert.Equal(new[] { error }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public void EditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult namesResult = viewModel.SetNames("Dojima", "Nanako");
        SaveEditorOperationResult yenResult = viewModel.SetYen(9_999_999);
        SaveEditorOperationResult partyResult = viewModel.SetPartyMember(1, new PartyMemberId(0x07));

        Assert.True(namesResult.Succeeded, FormatDiagnostics(namesResult.Diagnostics));
        Assert.True(yenResult.Succeeded, FormatDiagnostics(yenResult.Diagnostics));
        Assert.True(partyResult.Succeeded, FormatDiagnostics(partyResult.Diagnostics));
        Assert.Equal("Dojima", viewModel.FamilyName);
        Assert.Equal("Nanako", viewModel.GivenName);
        Assert.Equal(9_999_999u, viewModel.Yen);
        Assert.Equal((byte)0x07, viewModel.PartyMembers[1].MemberValue);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetSaveNamesEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetYenEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetPartyMemberEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void ApplyEditorValuesConvertsPrimitiveValuesToSingleEditBatch()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.ApplyEditorValues(
            "Dojima",
            "Nanako",
            9_999_999u,
            [(byte)0x01, (byte)0x07, (byte)0x80]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal("Dojima", viewModel.FamilyName);
        Assert.Equal("Nanako", viewModel.GivenName);
        Assert.Equal(9_999_999u, viewModel.Yen);
        Assert.Equal((byte)0x07, viewModel.PartyMembers[1].MemberValue);
        SaveEditCommand[] editBatch = Assert.Single(service.AppliedEdits);
        Assert.Collection(
            editBatch,
            static edit => Assert.Equal(new SaveNames("Dojima", "Nanako"), Assert.IsType<SetSaveNamesEdit>(edit).Names),
            static edit => Assert.Equal(9_999_999u, Assert.IsType<SetYenEdit>(edit).Yen),
            static edit => AssertPartyMemberEdit(edit, 0, 0x01),
            static edit => AssertPartyMemberEdit(edit, 1, 0x07),
            static edit => AssertPartyMemberEdit(edit, 2, 0x80));
    }

    [Fact]
    public void EditFailureSurfacesDiagnosticsAndPreservesPreviousProjection()
    {
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "ERR", "Edit failed.", "Edit");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = (_, _) => new SaveEditResult<WorkingSave>(null, [error]),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetNames("Bad", "Name");

        Assert.False(result.Succeeded);
        Assert.Equal("Sato", viewModel.FamilyName);
        Assert.Equal("Yu", viewModel.GivenName);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(new[] { error }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public void EditMethodsWithoutOpenSaveReturnDiagnosticAndDoNotCallService()
    {
        (string Name, Func<SaveEditorViewModel, SaveEditorOperationResult> Act)[] cases =
        [
            ("SetNames strings", static viewModel => viewModel.SetNames("Dojima", "Nanako")),
            ("SetNames value", static viewModel => viewModel.SetNames(new SaveNames("Dojima", "Nanako"))),
            ("SetYen", static viewModel => viewModel.SetYen(500_000u)),
            ("ApplyEditorValues", static viewModel => viewModel.ApplyEditorValues("Dojima", "Nanako", 500_000u, [0x01, 0x02, 0x03])),
            ("SetPartyMember", static viewModel => viewModel.SetPartyMember(1, new PartyMemberId(0x07))),
            ("ApplyEdits", static viewModel => viewModel.ApplyEdits([new SetYenEdit(500_000u)])),
        ];

        foreach ((string name, Func<SaveEditorViewModel, SaveEditorOperationResult> act) in cases)
        {
            FakeSaveApplicationService service = new()
            {
                ApplyEditsHandler = (_, _) => throw new InvalidOperationException($"{name} should not call the edit service."),
            };
            SaveEditorViewModel viewModel = new(service);

            SaveEditorOperationResult result = act(viewModel);

            Assert.False(result.Succeeded);
            SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("P4GPRES001", diagnostic.Code);
            Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
            Assert.True(viewModel.HasErrors);
            Assert.False(viewModel.HasSave);
            Assert.False(viewModel.CanWrite);
            Assert.False(viewModel.IsDirty);
            Assert.Equal(string.Empty, viewModel.FamilyName);
            Assert.Equal(string.Empty, viewModel.GivenName);
            Assert.Equal(0u, viewModel.Yen);
            Assert.Empty(viewModel.PartyMembers);
            Assert.Empty(viewModel.ProtagonistPersonaSlots);
            Assert.Empty(viewModel.PartyPersonaSlots);
            Assert.Empty(viewModel.CompendiumPersonaSlots);
            Assert.Empty(service.OpenInputs);
            Assert.Empty(service.AppliedEdits);
            Assert.Empty(service.WrittenSaves);
        }
    }

    [Fact]
    public void WriteSaveReturnsBytesAndKeepsDirtyUntilAcknowledged()
    {
        byte[] output = [0x10, 0x20, 0x30];
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = save =>
            {
                Assert.Equal(new SaveNames("Amagi", "Chie"), save.State.Names);
                return SaveWriteResult.Success(output, [new SaveDiagnostic(DiagnosticSeverity.Info, "INFO", "Saved.", "Write")]);
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        Assert.True(viewModel.IsDirty);

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(output, result.Bytes);
        SaveEditorWriteToken operationToken = AssertOperationToken(result);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
        Assert.Single(service.WrittenSaves);
        Assert.Equal("INFO", Assert.Single(viewModel.Diagnostics).Code);
        Assert.False(viewModel.HasErrors);

        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);

        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.CanWrite);
        Assert.Empty(viewModel.Diagnostics);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void PendingWriteKeepsDirtyWhenLaterEditMatchesPreviousBaseline()
    {
        byte[] output = [0x10, 0x20, 0x30];
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken operationToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult revertResult = viewModel.SetNames("Sato", "Yu");

        Assert.True(revertResult.Succeeded, FormatDiagnostics(revertResult.Diagnostics));
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);

        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanWrite);

        SaveEditorOperationResult restoreResult = viewModel.SetNames("Amagi", "Chie");

        Assert.True(restoreResult.Succeeded, FormatDiagnostics(restoreResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void AcknowledgeSavedUsesSerializedStateAndKeepsLaterEditsDirty()
    {
        byte[] output = [0x10, 0x20, 0x30];
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        SaveEditorWriteToken operationToken = AssertOperationToken(writeResult);

        viewModel.SetYen(42);
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);

        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.True(viewModel.IsDirty);
        Assert.Equal(42u, viewModel.Yen);
        Assert.Empty(viewModel.Diagnostics);

        SaveEditorOperationResult revertResult = viewModel.SetYen(123456u);

        Assert.True(revertResult.Succeeded, FormatDiagnostics(revertResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
        Assert.Equal(123456u, viewModel.Yen);
    }

    [Fact]
    public void AcknowledgeSavedPreservesDiagnosticsFromLaterFailedEdit()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic editError = new(DiagnosticSeverity.Error, "EDIT001", "Edit failed.", "Edit");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = (save, edits) => edits.OfType<SetYenEdit>().Any()
                ? new SaveEditResult<WorkingSave>(null, [editError])
                : ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken operationToken = AssertOperationToken(viewModel.WriteSave());

        SaveEditorOperationResult editResult = viewModel.SetYen(42);
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);

        Assert.False(editResult.Succeeded);
        Assert.Equal(new[] { editError }, editResult.Diagnostics);
        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.Equal(new[] { editError }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
        Assert.True(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(123456u, viewModel.Yen);
    }

    [Fact]
    public void EditAfterAcknowledgedSaveRefreshesProjectionAndMarksDirty()
    {
        byte[] output = [0x10, 0x20, 0x30];
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        SaveEditorWriteToken operationToken = AssertOperationToken(writeResult);
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);
        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.False(viewModel.IsDirty);

        SaveEditorOperationResult editResult = viewModel.SetYen(42);

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        Assert.Equal(42u, viewModel.Yen);
        Assert.True(viewModel.IsDirty);
        Assert.Empty(viewModel.Diagnostics);
    }

    [Fact]
    public void AcknowledgeSavedRejectsStaleOrWrongWriteTokenAndKeepsPendingWrite()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic persistenceError = new(DiagnosticSeverity.Error, "PERSIST001", "Save was not persisted.", "Write");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken firstToken = AssertOperationToken(viewModel.WriteSave());
        SaveEditorOperationResult reportFailedResult = viewModel.ReportSaveFailed(firstToken, [persistenceError]);
        Assert.False(reportFailedResult.Succeeded);
        Assert.True(viewModel.CanWrite);
        viewModel.SetYen(42);
        SaveEditorWriteToken secondToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult wrongAcknowledgeResult = viewModel.AcknowledgeSaved(default);

        Assert.False(wrongAcknowledgeResult.Succeeded);
        SaveDiagnostic wrongDiagnostic = Assert.Single(wrongAcknowledgeResult.Diagnostics);
        Assert.Equal("P4GPRES004", wrongDiagnostic.Code);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(42u, viewModel.Yen);

        SaveEditorOperationResult staleAcknowledgeResult = viewModel.AcknowledgeSaved(firstToken);

        Assert.False(staleAcknowledgeResult.Succeeded);
        SaveDiagnostic diagnostic = Assert.Single(staleAcknowledgeResult.Diagnostics);
        Assert.Equal("P4GPRES004", diagnostic.Code);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(42u, viewModel.Yen);

        SaveEditorOperationResult currentAcknowledgeResult = viewModel.AcknowledgeSaved(secondToken);

        Assert.True(currentAcknowledgeResult.Succeeded, FormatDiagnostics(currentAcknowledgeResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
        Assert.Empty(viewModel.Diagnostics);
    }

    [Fact]
    public void WriteSaveWhilePendingReturnsDiagnosticAndDoesNotCallServiceAgain()
    {
        byte[] output = [0x10, 0x20, 0x30];
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken firstToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        Assert.False(result.OperationToken.HasValue);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES005", diagnostic.Code);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.Single(service.WrittenSaves);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(firstToken);

        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.Empty(viewModel.Diagnostics);
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.CanWrite);
    }

    [Fact]
    public void WriteSaveSetsPendingBeforeSuccessNotificationsBlockReentrantWrite()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic info = new(DiagnosticSeverity.Info, "INFO", "Saved.", "Write");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            WriteHandler = _ => SaveWriteResult.Success(output, [info]),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        SaveEditorWriteResult? reentrantResult = null;
        int reentrantAttempts = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (reentrantAttempts > 0)
            {
                return;
            }

            reentrantAttempts++;
            reentrantResult = viewModel.WriteSave();
        };

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(output, result.Bytes);
        Assert.True(result.OperationToken.HasValue);
        Assert.Equal(1, reentrantAttempts);
        Assert.NotNull(reentrantResult);
        Assert.False(reentrantResult.Succeeded);
        Assert.Null(reentrantResult.Bytes);
        Assert.False(reentrantResult.OperationToken.HasValue);
        SaveDiagnostic diagnostic = Assert.Single(reentrantResult.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES005", diagnostic.Code);
        Assert.Single(service.WrittenSaves);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
        Assert.Equal(new[] { info }, result.Diagnostics);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
    }

    [Fact]
    public void ApplyEditsUpdatesWorkingSaveBeforeNotificationsSoReentrantWriteSerializesEditedState()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveNames? writtenNames = null;
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = save =>
            {
                writtenNames = save.State.Names;
                return SaveWriteResult.Success(output);
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        SaveEditorWriteResult? reentrantResult = null;
        int reentrantAttempts = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (reentrantAttempts > 0)
            {
                return;
            }

            reentrantAttempts++;
            reentrantResult = viewModel.WriteSave();
        };

        SaveEditorOperationResult result = viewModel.SetNames("Amagi", "Chie");

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(1, reentrantAttempts);
        Assert.NotNull(reentrantResult);
        Assert.True(reentrantResult.Succeeded, FormatDiagnostics(reentrantResult.Diagnostics));
        Assert.Equal(output, reentrantResult.Bytes);
        Assert.Equal(new SaveNames("Amagi", "Chie"), writtenNames);
        Assert.Equal(new SaveNames("Amagi", "Chie"), Assert.Single(service.WrittenSaves).State.Names);
        Assert.Equal("Amagi", viewModel.FamilyName);
        Assert.Equal("Chie", viewModel.GivenName);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
    }

    [Fact]
    public void WriteSaveDoesNotOverwriteReentrantFailedEditDiagnostics()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic writeInfo = new(DiagnosticSeverity.Info, "INFO", "Saved.", "Write");
        SaveDiagnostic editError = new(DiagnosticSeverity.Error, "EDIT001", "Edit failed.", "Edit");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = (_, _) => new SaveEditResult<WorkingSave>(null, [editError]),
            WriteHandler = _ => SaveWriteResult.Success(output, [writeInfo]),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        SaveEditorOperationResult? reentrantResult = null;
        int reentrantAttempts = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (reentrantAttempts > 0 || args.PropertyName != nameof(SaveEditorViewModel.IsDirty))
            {
                return;
            }

            reentrantAttempts++;
            reentrantResult = viewModel.SetYen(42);
        };

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(output, result.Bytes);
        Assert.Equal(new[] { writeInfo }, result.Diagnostics);
        Assert.Equal(1, reentrantAttempts);
        Assert.NotNull(reentrantResult);
        Assert.False(reentrantResult.Succeeded);
        Assert.Equal(new[] { editError }, reentrantResult.Diagnostics);
        Assert.Equal(new[] { editError }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
    }

    [Fact]
    public void AcknowledgeSavedDoesNotOverwriteReentrantWriteDiagnostics()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic retryInfo = new(DiagnosticSeverity.Info, "RETRY", "Retried.", "Write");
        int writeCount = 0;
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ =>
            {
                writeCount++;
                return SaveWriteResult.Success(output, writeCount == 1 ? [] : [retryInfo]);
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken operationToken = AssertOperationToken(viewModel.WriteSave());
        SaveEditorWriteResult? reentrantResult = null;
        int reentrantAttempts = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (reentrantAttempts > 0)
            {
                return;
            }

            reentrantAttempts++;
            reentrantResult = viewModel.WriteSave();
        };

        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(operationToken);

        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.Equal(1, reentrantAttempts);
        Assert.NotNull(reentrantResult);
        Assert.True(reentrantResult.Succeeded, FormatDiagnostics(reentrantResult.Diagnostics));
        Assert.Equal(new[] { retryInfo }, viewModel.Diagnostics);
        Assert.Equal(2, service.WrittenSaves.Count);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
    }

    [Fact]
    public void ReportSaveFailedDoesNotOverwriteReentrantRetryDiagnostics()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic persistenceError = new(DiagnosticSeverity.Error, "PERSIST001", "Save was not persisted.", "Write");
        SaveDiagnostic retryInfo = new(DiagnosticSeverity.Info, "RETRY", "Retried.", "Write");
        int writeCount = 0;
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ =>
            {
                writeCount++;
                return SaveWriteResult.Success(output, writeCount == 1 ? [] : [retryInfo]);
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken operationToken = AssertOperationToken(viewModel.WriteSave());
        SaveEditorWriteResult? reentrantResult = null;
        int reentrantAttempts = 0;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (reentrantAttempts > 0)
            {
                return;
            }

            reentrantAttempts++;
            reentrantResult = viewModel.WriteSave();
        };

        SaveEditorOperationResult reportResult = viewModel.ReportSaveFailed(operationToken, [persistenceError]);

        Assert.False(reportResult.Succeeded);
        Assert.Equal(new[] { persistenceError }, reportResult.Diagnostics);
        Assert.Equal(1, reentrantAttempts);
        Assert.NotNull(reentrantResult);
        Assert.True(reentrantResult.Succeeded, FormatDiagnostics(reentrantResult.Diagnostics));
        Assert.Equal(new[] { retryInfo }, viewModel.Diagnostics);
        Assert.Equal(2, service.WrittenSaves.Count);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
    }

    [Fact]
    public void ReportSaveFailedClearsPendingWriteAndKeepsDirtyForRetry()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic persistenceError = new(DiagnosticSeverity.Error, "PERSIST001", "Save was not persisted.", "Write");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken operationToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult result = viewModel.ReportSaveFailed(operationToken, [persistenceError]);

        Assert.False(result.Succeeded);
        Assert.Equal(new[] { persistenceError }, result.Diagnostics);
        Assert.Equal(new[] { persistenceError }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
        Assert.True(viewModel.CanWrite);
        Assert.True(viewModel.IsDirty);

        SaveEditorWriteResult retryResult = viewModel.WriteSave();

        Assert.True(retryResult.Succeeded, FormatDiagnostics(retryResult.Diagnostics));
        Assert.Equal(2, service.WrittenSaves.Count);
        Assert.False(viewModel.CanWrite);
    }

    [Fact]
    public void ReportSaveFailedRejectsStaleOrWrongWriteTokenAndKeepsPendingWrite()
    {
        byte[] output = [0x10, 0x20, 0x30];
        SaveDiagnostic persistenceError = new(DiagnosticSeverity.Error, "PERSIST001", "Save was not persisted.", "Write");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Success(output),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        viewModel.SetNames("Amagi", "Chie");
        SaveEditorWriteToken firstToken = AssertOperationToken(viewModel.WriteSave());
        SaveEditorOperationResult firstFailureResult = viewModel.ReportSaveFailed(firstToken, [persistenceError]);
        Assert.False(firstFailureResult.Succeeded);
        Assert.True(viewModel.CanWrite);
        viewModel.SetYen(42);
        SaveEditorWriteToken secondToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);

        SaveEditorOperationResult wrongTokenResult = viewModel.ReportSaveFailed(default, [persistenceError]);

        Assert.False(wrongTokenResult.Succeeded);
        SaveDiagnostic wrongTokenDiagnostic = Assert.Single(wrongTokenResult.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, wrongTokenDiagnostic.Severity);
        Assert.Equal("P4GPRES004", wrongTokenDiagnostic.Code);
        Assert.Equal(new[] { wrongTokenDiagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
        Assert.Equal(42u, viewModel.Yen);

        SaveEditorOperationResult staleTokenResult = viewModel.ReportSaveFailed(firstToken, [persistenceError]);

        Assert.False(staleTokenResult.Succeeded);
        SaveDiagnostic staleTokenDiagnostic = Assert.Single(staleTokenResult.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, staleTokenDiagnostic.Severity);
        Assert.Equal("P4GPRES004", staleTokenDiagnostic.Code);
        Assert.Equal(new[] { staleTokenDiagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanWrite);
        Assert.Equal(42u, viewModel.Yen);

        SaveEditorOperationResult currentTokenResult = viewModel.ReportSaveFailed(secondToken, [persistenceError]);

        Assert.False(currentTokenResult.Succeeded);
        Assert.Equal(new[] { persistenceError }, currentTokenResult.Diagnostics);
        Assert.Equal(new[] { persistenceError }, viewModel.Diagnostics);
        Assert.True(viewModel.CanWrite);
        Assert.True(viewModel.IsDirty);

        SaveEditorWriteResult retryResult = viewModel.WriteSave();

        Assert.True(retryResult.Succeeded, FormatDiagnostics(retryResult.Diagnostics));
        Assert.False(viewModel.CanWrite);
        Assert.Equal(3, service.WrittenSaves.Count);
    }

    [Fact]
    public void WriteSaveFailureAfterDirtyEditSurfacesDiagnosticsAndKeepsDirtyState()
    {
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "ERR", "Write failed.", "Write");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
            WriteHandler = _ => SaveWriteResult.Failure([error]),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        SaveEditorOperationResult editResult = viewModel.SetNames("Amagi", "Chie");
        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        Assert.True(viewModel.IsDirty);

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        Assert.Equal(new[] { error }, result.Diagnostics);
        Assert.Equal(new[] { error }, viewModel.Diagnostics);
        Assert.True(viewModel.HasErrors);
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Amagi", viewModel.FamilyName);
        Assert.Equal("Chie", viewModel.GivenName);
        WorkingSave writtenSave = Assert.Single(service.WrittenSaves);
        Assert.Equal(new SaveNames("Amagi", "Chie"), writtenSave.State.Names);
    }

    [Fact]
    public void WriteSaveWithoutOpenSaveReturnsDiagnosticAndDoesNotCallService()
    {
        FakeSaveApplicationService service = new();
        SaveEditorViewModel viewModel = new(service);

        SaveEditorWriteResult result = viewModel.WriteSave();

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES001", diagnostic.Code);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.WrittenSaves);
    }

    [Fact]
    public void AcknowledgeSavedWithoutOpenSaveReturnsDiagnosticAndKeepsClear()
    {
        FakeSaveApplicationService service = new();
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.AcknowledgeSaved(default);

        Assert.False(result.Succeeded);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES001", diagnostic.Code);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.False(viewModel.HasSave);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.WrittenSaves);
    }

    [Fact]
    public void ReportSaveFailedWithoutOpenSaveReturnsDiagnosticAndKeepsClear()
    {
        FakeSaveApplicationService service = new();
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.ReportSaveFailed(default);

        Assert.False(result.Succeeded);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES001", diagnostic.Code);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.False(viewModel.HasSave);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.OpenInputs);
        Assert.Empty(service.AppliedEdits);
        Assert.Empty(service.WrittenSaves);
    }

    [Fact]
    public void PresentationAssemblyDoesNotReferenceApplicationCatalogOrSaveFormat()
    {
        HashSet<string?> referencedAssemblies = typeof(SaveEditorViewModel)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("P4G.SaveTool.Contracts", referencedAssemblies);
        Assert.DoesNotContain("P4G.SaveTool.Application", referencedAssemblies);
        Assert.DoesNotContain("P4G.SaveTool.Catalog", referencedAssemblies);
        Assert.DoesNotContain("P4G.SaveTool.SaveFormat", referencedAssemblies);
    }

    [Fact]
    public void PresentationProjectDoesNotReferenceApplicationCatalogOrSaveFormat()
    {
        string projectPath = FindRepositoryFile("src", "P4G.SaveTool.Presentation", "P4G.SaveTool.Presentation.csproj");
        XDocument project = XDocument.Load(projectPath);
        string[] references = project
            .Descendants()
            .Where(static element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(static element =>
                (string?)element.Attribute("Include") ??
                (string?)element.Attribute("Update") ??
                string.Empty)
            .Where(static reference => reference.Length > 0)
            .ToArray();

        foreach (string forbiddenReference in new[]
        {
            "P4G.SaveTool.Application",
            "P4G.SaveTool.Catalog",
            "P4G.SaveTool.SaveFormat",
        })
        {
            Assert.DoesNotContain(
                references,
                reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PresentationResolvedLockFileGraphDoesNotContainApplicationCatalogOrSaveFormat()
    {
        string lockFilePath = FindRepositoryFile("src", "P4G.SaveTool.Presentation", "packages.lock.json");
        using FileStream stream = File.OpenRead(lockFilePath);
        using JsonDocument lockFile = JsonDocument.Parse(stream);

        HashSet<string> dependencyIds = ReadLockFileDependencyGraph(lockFile);

        Assert.Contains(dependencyIds, static dependencyId => IsDependencyId(dependencyId, "P4G.SaveTool.Contracts"));
        Assert.Contains(dependencyIds, static dependencyId => IsDependencyId(dependencyId, "P4G.SaveTool.Domain"));
        foreach (string forbiddenDependencyId in ForbiddenPresentationDependencyIds)
        {
            Assert.DoesNotContain(
                dependencyIds,
                dependencyId => IsForbiddenPresentationDependencyId(dependencyId, forbiddenDependencyId));
        }
    }

    private static string FindRepositoryFile(params string[] relativePathSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativePathSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find {Path.Combine(relativePathSegments)} from {AppContext.BaseDirectory}.");
    }

    private static HashSet<string> ReadLockFileDependencyGraph(JsonDocument lockFile)
    {
        HashSet<string> dependencyIds = new(StringComparer.OrdinalIgnoreCase);
        JsonElement dependencies = lockFile.RootElement.GetProperty("dependencies");

        foreach (JsonProperty targetFramework in dependencies.EnumerateObject())
        {
            foreach (JsonProperty dependency in targetFramework.Value.EnumerateObject())
            {
                dependencyIds.Add(dependency.Name);
                if (!dependency.Value.TryGetProperty("dependencies", out JsonElement dependencyReferences))
                {
                    continue;
                }

                foreach (JsonProperty dependencyReference in dependencyReferences.EnumerateObject())
                {
                    dependencyIds.Add(dependencyReference.Name);
                }
            }
        }

        return dependencyIds;
    }

    private static bool IsDependencyId(string dependencyId, string expectedDependencyId) =>
        dependencyId.Equals(expectedDependencyId, StringComparison.OrdinalIgnoreCase);

    private static bool IsForbiddenPresentationDependencyId(string dependencyId, string forbiddenDependencyId)
    {
        string normalizedDependencyId = dependencyId.Replace('\\', '.').Replace('/', '.');
        return normalizedDependencyId.Equals(forbiddenDependencyId, StringComparison.OrdinalIgnoreCase) ||
            normalizedDependencyId.Equals($"P4G.SaveTool.{forbiddenDependencyId}", StringComparison.OrdinalIgnoreCase) ||
            normalizedDependencyId.EndsWith($".{forbiddenDependencyId}", StringComparison.OrdinalIgnoreCase);
    }

    private static SaveEditResult<WorkingSave> ApplyCommands(WorkingSave save, IEnumerable<SaveEditCommand> edits)
    {
        WorkingSaveState state = save.State;
        foreach (SaveEditCommand edit in edits)
        {
            state = edit switch
            {
                SetSaveNamesEdit setNames => state.WithNames(setNames.Names),
                SetYenEdit setYen => state.WithYen(setYen.Yen),
                SetPartyMemberEdit setPartyMember => state.WithPartyMember(setPartyMember.SlotIndex, setPartyMember.MemberId),
                _ => state,
            };
        }

        return new SaveEditResult<WorkingSave>(new FakeWorkingSave(state), []);
    }

    private static WorkingSaveState CreateState(
        string familyName = "Sato",
        string givenName = "Yu",
        uint yen = 123456u) =>
        new(
            new SaveNames(familyName, givenName),
            yen,
            [new PartyMemberId(0x01), new PartyMemberId(0xfe), new PartyMemberId(0x80)],
            [CreatePersonaSlot(0x0101, 77, 0x01010101, 0x1101)],
            [CreatePersonaSlot(0x0202, 44, 0x02020202, 0x2201)],
            [CreatePersonaSlot(0x0303, 22, 0x03030303, 0x3301)]);

    private static PersonaSlot CreatePersonaSlot(
        ushort personaId,
        byte level,
        uint totalExperience,
        ushort firstSkillId) =>
        new(
            exists: true,
            unknown0: 0,
            personaId,
            level,
            reservedAfterLevel: [0, 0, 0],
            totalExperience,
            skillIds: Enumerable.Range(firstSkillId, PersonaSlot.SkillCount).Select(static skillId => (ushort)skillId).ToArray(),
            strength: 11,
            magic: 22,
            endurance: 33,
            agility: 44,
            luck: 55);

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(static diagnostic =>
                $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Target}: {diagnostic.Message}"));

    private static void AssertPartyMemberEdit(SaveEditCommand edit, int slotIndex, byte memberValue)
    {
        SetPartyMemberEdit partyMemberEdit = Assert.IsType<SetPartyMemberEdit>(edit);
        Assert.Equal(slotIndex, partyMemberEdit.SlotIndex);
        Assert.Equal(new PartyMemberId(memberValue), partyMemberEdit.MemberId);
    }

    private static SaveEditorWriteToken AssertOperationToken(SaveEditorWriteResult result)
    {
        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.True(result.OperationToken.HasValue);
        return result.OperationToken.GetValueOrDefault();
    }

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

    private sealed class FakeWorkingSave(WorkingSaveState state) : WorkingSave(state);

    private sealed class FakeSaveApplicationService : ISaveApplicationService
    {
        public Func<ReadOnlyMemory<byte>, SaveOpenResult<WorkingSave>>? OpenHandler { get; init; }

        public Func<WorkingSave, IEnumerable<SaveEditCommand>, SaveEditResult<WorkingSave>>? ApplyEditsHandler { get; init; }

        public Func<WorkingSave, SaveWriteResult>? WriteHandler { get; init; }

        public List<byte[]> OpenInputs { get; } = [];

        public List<SaveEditCommand[]> AppliedEdits { get; } = [];

        public List<WorkingSave> WrittenSaves { get; } = [];

        public SaveOpenResult<WorkingSave> Open(ReadOnlyMemory<byte> bytes)
        {
            OpenInputs.Add(bytes.ToArray());
            return OpenHandler is null
                ? new SaveOpenResult<WorkingSave>(null, [new SaveDiagnostic(DiagnosticSeverity.Error, "FAKE001", "No open handler.", "Fake")])
                : OpenHandler(bytes);
        }

        public SaveEditResult<WorkingSave> ApplyEdits(WorkingSave save, IEnumerable<SaveEditCommand> edits)
        {
            SaveEditCommand[] editArray = edits.ToArray();
            AppliedEdits.Add(editArray);
            return ApplyEditsHandler is null
                ? new SaveEditResult<WorkingSave>(save, [])
                : ApplyEditsHandler(save, editArray);
        }

        public SaveWriteResult Write(WorkingSave save)
        {
            WrittenSaves.Add(save);
            return WriteHandler is null
                ? SaveWriteResult.Failure([new SaveDiagnostic(DiagnosticSeverity.Error, "FAKE002", "No write handler.", "Fake")])
                : WriteHandler(save);
        }
    }
}
