using System.Collections.ObjectModel;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Presentation;

public sealed class SaveEditorViewModel : ViewModelBase
{
    private const string PresentationDiagnosticTarget = "Save";

    private static readonly ReadOnlyCollection<SaveDiagnostic> EmptyDiagnostics =
        Array.AsReadOnly(Array.Empty<SaveDiagnostic>());

    private static readonly ReadOnlyCollection<PartyMemberSlotViewState> EmptyPartyMembers =
        Array.AsReadOnly(Array.Empty<PartyMemberSlotViewState>());

    private static readonly ReadOnlyCollection<PersonaSlotViewState> EmptyPersonaSlots =
        Array.AsReadOnly(Array.Empty<PersonaSlotViewState>());

    private static readonly ReadOnlyCollection<InventoryStackViewState> EmptyInventoryStacks =
        Array.AsReadOnly(Array.Empty<InventoryStackViewState>());

    private readonly ISaveApplicationService saveApplicationService;
    private WorkingSave? workingSave;
    private WorkingSaveState? lastPersistedState;
    private PendingSerializedSave? pendingSerializedSave;
    private DiagnosticScope currentDiagnosticsScope;
    private SaveEditorWriteToken? currentDiagnosticsOperationToken;
    private long nextWriteOperationId;
    private string familyName = string.Empty;
    private string givenName = string.Empty;
    private uint yen;
    private IReadOnlyList<PartyMemberSlotViewState> partyMembers = EmptyPartyMembers;
    private IReadOnlyList<PersonaSlotViewState> protagonistPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<PersonaSlotViewState> partyPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<PersonaSlotViewState> compendiumPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<InventoryStackViewState> inventoryEntries = EmptyInventoryStacks;
    private IReadOnlyList<SaveDiagnostic> diagnostics = EmptyDiagnostics;
    private bool isDirty;

    public SaveEditorViewModel(ISaveApplicationService saveApplicationService)
    {
        ArgumentNullException.ThrowIfNull(saveApplicationService);

        this.saveApplicationService = saveApplicationService;
    }

    public bool HasSave => workingSave is not null;

    public bool CanWrite => HasSave && pendingSerializedSave is null;

    public bool IsDirty
    {
        get => isDirty;
        private set => SetProperty(ref isDirty, value);
    }

    public string FamilyName
    {
        get => familyName;
        private set => SetProperty(ref familyName, value);
    }

    public string GivenName
    {
        get => givenName;
        private set => SetProperty(ref givenName, value);
    }

    public uint Yen
    {
        get => yen;
        private set => SetProperty(ref yen, value);
    }

    public IReadOnlyList<PartyMemberSlotViewState> PartyMembers
    {
        get => partyMembers;
        private set => SetProperty(ref partyMembers, value);
    }

    public IReadOnlyList<PersonaSlotViewState> ProtagonistPersonaSlots
    {
        get => protagonistPersonaSlots;
        private set => SetProperty(ref protagonistPersonaSlots, value);
    }

    public IReadOnlyList<PersonaSlotViewState> PartyPersonaSlots
    {
        get => partyPersonaSlots;
        private set => SetProperty(ref partyPersonaSlots, value);
    }

    public IReadOnlyList<PersonaSlotViewState> CompendiumPersonaSlots
    {
        get => compendiumPersonaSlots;
        private set => SetProperty(ref compendiumPersonaSlots, value);
    }

    public IReadOnlyList<InventoryStackViewState> InventoryEntries
    {
        get => inventoryEntries;
        private set => SetProperty(ref inventoryEntries, value);
    }

    public IReadOnlyList<ItemCategoryViewState> InventoryCategories => InventoryCatalogProjection.Categories;

    public IReadOnlyList<SaveDiagnostic> Diagnostics
    {
        get => diagnostics;
        private set
        {
            if (SetProperty(ref diagnostics, value))
            {
                OnPropertyChanged(nameof(HasDiagnostics));
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasErrors => Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public IReadOnlyList<InventoryItemChoiceViewState> GetInventoryItemsForCategory(byte categoryId) =>
        InventoryCatalogProjection.GetItems(categoryId);

    public SaveEditorOperationResult OpenSave(ReadOnlyMemory<byte> bytes)
    {
        SaveOpenResult<WorkingSave> result = saveApplicationService.Open(bytes);

        if (!result.Succeeded || result.Snapshot is null)
        {
            SetDiagnostics(result.Diagnostics);
            return new SaveEditorOperationResult(false, result.Diagnostics);
        }

        workingSave = result.Snapshot;
        lastPersistedState = result.Snapshot.State;
        pendingSerializedSave = null;
        ProjectionChange projectionChange = ProjectStateBacking(result.Snapshot.State);
        bool isDirtyChanged = SetDirtyStateBacking(false);
        SetDiagnosticsBacking(result.Diagnostics, DiagnosticScope.General, null);

        NotifyProjectionChanged(projectionChange);
        OnPropertyChanged(nameof(HasSave));
        NotifyLifecycleStateChanged(isDirtyChanged, canWriteChanged: true, diagnosticsChanged: true);

        return new SaveEditorOperationResult(true, result.Diagnostics);
    }

    public SaveEditorOperationResult SetNames(string familyName, string givenName)
    {
        if (familyName is null || givenName is null)
        {
            return FailOperation("P4GPRES002", "Save names cannot be null.", "Names");
        }

        return SetNames(new SaveNames(familyName, givenName));
    }

    public SaveEditorOperationResult SetNames(SaveNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return ApplyEdits([new SetSaveNamesEdit(names)]);
    }

    public SaveEditorOperationResult SetYen(uint yen) =>
        ApplyEdits([new SetYenEdit(yen)]);

    public SaveEditorOperationResult ApplyEditorValues(
        string familyName,
        string givenName,
        uint yen,
        IReadOnlyList<byte> partyMemberValues)
    {
        if (familyName is null || givenName is null)
        {
            return FailOperation("P4GPRES002", "Save names cannot be null.", "Names");
        }

        if (partyMemberValues is null)
        {
            return FailOperation("P4GPRES007", "Party member values cannot be null.", "PartyMembers");
        }

        List<SaveEditCommand> edits =
        [
            new SetSaveNamesEdit(new SaveNames(familyName, givenName)),
            new SetYenEdit(yen),
        ];

        for (int index = 0; index < partyMemberValues.Count; index++)
        {
            edits.Add(new SetPartyMemberEdit(index, new PartyMemberId(partyMemberValues[index])));
        }

        return ApplyEdits(edits);
    }

    public SaveEditorOperationResult SetPartyMember(int slotIndex, PartyMemberId memberId) =>
        ApplyEdits([new SetPartyMemberEdit(slotIndex, memberId)]);

    public SaveEditorOperationResult SetInventoryItemQuantity(ushort itemId, byte quantity)
    {
        if (!InventoryItemEditability.IsWritableItemId(itemId))
        {
            return FailOperation("P4GPRES008", "Placeholder inventory items cannot be modified.", "Inventory.Item");
        }

        return ApplyEdits([new SetInventoryItemQuantityEdit(itemId, quantity)]);
    }

    public SaveEditorOperationResult RemoveInventoryItem(ushort itemId)
    {
        if (!InventoryItemEditability.IsWritableItemId(itemId))
        {
            return FailOperation("P4GPRES008", "Placeholder inventory items cannot be modified.", "Inventory.Item");
        }

        return ApplyEdits([new RemoveInventoryItemEdit(itemId)]);
    }

    public SaveEditorOperationResult ApplyEdits(IEnumerable<SaveEditCommand> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        if (workingSave is null)
        {
            return FailNoOpenSave();
        }

        SaveEditResult<WorkingSave> result = saveApplicationService.ApplyEdits(workingSave, edits);

        if (!result.Succeeded || result.Save is null)
        {
            SetDiagnostics(result.Diagnostics);
            return new SaveEditorOperationResult(false, result.Diagnostics);
        }

        workingSave = result.Save;
        ProjectionChange projectionChange = ProjectStateBacking(result.Save.State);
        bool isDirtyChanged = SetDirtyStateBacking(CalculateDirtyState());
        SetDiagnosticsBacking(result.Diagnostics, DiagnosticScope.General, null);

        NotifyProjectionChanged(projectionChange);
        NotifyLifecycleStateChanged(isDirtyChanged, canWriteChanged: false, diagnosticsChanged: true);

        return new SaveEditorOperationResult(true, result.Diagnostics);
    }

    public SaveEditorWriteResult WriteSave()
    {
        if (workingSave is null)
        {
            SaveDiagnostic diagnostic = NoOpenSaveDiagnostic();
            SetDiagnostics([diagnostic]);
            return SaveEditorWriteResult.Failure([diagnostic]);
        }

        if (pendingSerializedSave is not null)
        {
            SaveDiagnostic diagnostic = new(
                DiagnosticSeverity.Error,
                "P4GPRES005",
                "A serialized save is already awaiting persistence acknowledgement.",
                PresentationDiagnosticTarget);
            SetDiagnostics([diagnostic], DiagnosticScope.PendingWrite, pendingSerializedSave.OperationToken);
            return SaveEditorWriteResult.Failure([diagnostic]);
        }

        SaveWriteResult result = saveApplicationService.Write(workingSave);

        SaveEditorWriteToken? operationToken = null;
        if (result.Succeeded)
        {
            operationToken = CreateWriteToken();
            pendingSerializedSave = new PendingSerializedSave(operationToken.Value, workingSave.State);
            bool isDirtyChanged = SetDirtyStateBacking(CalculateDirtyState());
            SetDiagnosticsBacking(result.Diagnostics, DiagnosticScope.PendingWrite, operationToken);
            NotifyLifecycleStateChanged(isDirtyChanged, canWriteChanged: true, diagnosticsChanged: true);
        }
        else
        {
            SetDiagnostics(result.Diagnostics);
        }

        return SaveEditorWriteResult.FromApplicationResult(result, operationToken);
    }

    public SaveEditorOperationResult AcknowledgeSaved(SaveEditorWriteToken operationToken)
    {
        if (workingSave is null)
        {
            return FailNoOpenSave();
        }

        if (pendingSerializedSave is null)
        {
            return FailOperation(
                "P4GPRES003",
                "No serialized save is awaiting persistence acknowledgement.",
                PresentationDiagnosticTarget);
        }

        if (pendingSerializedSave.OperationToken != operationToken)
        {
            return FailOperation(
                "P4GPRES004",
                "Serialized save acknowledgement is stale or does not match the latest pending write.",
                PresentationDiagnosticTarget,
                DiagnosticScope.PendingWrite,
                pendingSerializedSave.OperationToken);
        }

        PendingSerializedSave acknowledgedSave = pendingSerializedSave;
        bool clearPendingWriteDiagnostics = DiagnosticsBelongToPendingWrite(acknowledgedSave.OperationToken);

        lastPersistedState = acknowledgedSave.State;
        pendingSerializedSave = null;
        bool isDirtyChanged = SetDirtyStateBacking(CalculateDirtyState());
        if (clearPendingWriteDiagnostics)
        {
            SetDiagnosticsBacking([], DiagnosticScope.General, null);
        }

        NotifyLifecycleStateChanged(isDirtyChanged, canWriteChanged: true, clearPendingWriteDiagnostics);

        return new SaveEditorOperationResult(true, []);
    }

    public SaveEditorOperationResult ReportSaveFailed(
        SaveEditorWriteToken operationToken,
        IReadOnlyList<SaveDiagnostic>? diagnostics = null)
    {
        if (workingSave is null)
        {
            return FailNoOpenSave();
        }

        if (pendingSerializedSave is null)
        {
            return FailOperation(
                "P4GPRES003",
                "No serialized save is awaiting persistence acknowledgement.",
                PresentationDiagnosticTarget);
        }

        if (pendingSerializedSave.OperationToken != operationToken)
        {
            return FailOperation(
                "P4GPRES004",
                "Serialized save acknowledgement is stale or does not match the latest pending write.",
                PresentationDiagnosticTarget,
                DiagnosticScope.PendingWrite,
                pendingSerializedSave.OperationToken);
        }

        IReadOnlyList<SaveDiagnostic> failureDiagnostics = diagnostics is null || diagnostics.Count == 0
            ? [new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GPRES006",
                "Serialized save persistence failed.",
                PresentationDiagnosticTarget)]
            : diagnostics;

        pendingSerializedSave = null;
        bool isDirtyChanged = SetDirtyStateBacking(CalculateDirtyState());
        SetDiagnosticsBacking(failureDiagnostics, DiagnosticScope.General, null);
        NotifyLifecycleStateChanged(isDirtyChanged, canWriteChanged: true, diagnosticsChanged: true);

        return new SaveEditorOperationResult(false, failureDiagnostics);
    }

    private SaveEditorWriteToken CreateWriteToken() =>
        new(++nextWriteOperationId);

    private SaveEditorOperationResult FailNoOpenSave() =>
        FailOperation("P4GPRES001", "No save is open.", PresentationDiagnosticTarget);

    private SaveEditorOperationResult FailOperation(
        string code,
        string message,
        string target,
        DiagnosticScope diagnosticScope = DiagnosticScope.General,
        SaveEditorWriteToken? operationToken = null)
    {
        SaveDiagnostic diagnostic = new(DiagnosticSeverity.Error, code, message, target);
        SetDiagnostics([diagnostic], diagnosticScope, operationToken);
        return new SaveEditorOperationResult(false, [diagnostic]);
    }

    private static SaveDiagnostic NoOpenSaveDiagnostic() =>
        new(DiagnosticSeverity.Error, "P4GPRES001", "No save is open.", PresentationDiagnosticTarget);

    private ProjectionChange ProjectStateBacking(WorkingSaveState state)
    {
        ProjectionChange changes = ProjectionChange.None;
        IReadOnlyList<PartyMemberSlotViewState> nextPartyMembers = ProjectPartyMembers(state.PartyMembers);
        IReadOnlyList<PersonaSlotViewState> nextProtagonistPersonaSlots = ProjectPersonaSlots(state.ProtagonistPersonaSlots);
        IReadOnlyList<PersonaSlotViewState> nextPartyPersonaSlots = ProjectPersonaSlots(state.PartyPersonaSlots);
        IReadOnlyList<PersonaSlotViewState> nextCompendiumPersonaSlots = ProjectPersonaSlots(state.CompendiumPersonaSlots);
        IReadOnlyList<InventoryStackViewState> nextInventoryEntries = ProjectInventoryStacks(state.InventoryStacks);

        if (SetBacking(ref familyName, state.Names.FamilyName))
        {
            changes |= ProjectionChange.FamilyName;
        }

        if (SetBacking(ref givenName, state.Names.GivenName))
        {
            changes |= ProjectionChange.GivenName;
        }

        if (SetBacking(ref yen, state.Yen))
        {
            changes |= ProjectionChange.Yen;
        }

        if (SetBacking(ref partyMembers, nextPartyMembers))
        {
            changes |= ProjectionChange.PartyMembers;
        }

        if (SetBacking(ref protagonistPersonaSlots, nextProtagonistPersonaSlots))
        {
            changes |= ProjectionChange.ProtagonistPersonaSlots;
        }

        if (SetBacking(ref partyPersonaSlots, nextPartyPersonaSlots))
        {
            changes |= ProjectionChange.PartyPersonaSlots;
        }

        if (SetBacking(ref compendiumPersonaSlots, nextCompendiumPersonaSlots))
        {
            changes |= ProjectionChange.CompendiumPersonaSlots;
        }

        if (SetBacking(ref inventoryEntries, nextInventoryEntries, InventoryEntriesEqual))
        {
            changes |= ProjectionChange.InventoryEntries;
        }

        return changes;
    }

    private void SetDiagnostics(
        IReadOnlyList<SaveDiagnostic> diagnostics,
        DiagnosticScope diagnosticScope = DiagnosticScope.General,
        SaveEditorWriteToken? operationToken = null)
    {
        SetDiagnosticsBacking(diagnostics, diagnosticScope, operationToken);
        NotifyDiagnosticsChanged();
    }

    private void SetDiagnosticsBacking(
        IReadOnlyList<SaveDiagnostic> diagnostics,
        DiagnosticScope diagnosticScope,
        SaveEditorWriteToken? operationToken)
    {
        this.diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        currentDiagnosticsScope = diagnosticScope;
        currentDiagnosticsOperationToken = operationToken;
    }

    private void NotifyDiagnosticsChanged()
    {
        OnPropertyChanged(nameof(Diagnostics));
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasErrors));
    }

    private void NotifyLifecycleStateChanged(bool isDirtyChanged, bool canWriteChanged, bool diagnosticsChanged)
    {
        if (isDirtyChanged)
        {
            OnPropertyChanged(nameof(IsDirty));
        }

        if (canWriteChanged)
        {
            OnPropertyChanged(nameof(CanWrite));
        }

        if (diagnosticsChanged)
        {
            NotifyDiagnosticsChanged();
        }
    }

    private void NotifyProjectionChanged(ProjectionChange changes)
    {
        if ((changes & ProjectionChange.FamilyName) != 0)
        {
            OnPropertyChanged(nameof(FamilyName));
        }

        if ((changes & ProjectionChange.GivenName) != 0)
        {
            OnPropertyChanged(nameof(GivenName));
        }

        if ((changes & ProjectionChange.Yen) != 0)
        {
            OnPropertyChanged(nameof(Yen));
        }

        if ((changes & ProjectionChange.PartyMembers) != 0)
        {
            OnPropertyChanged(nameof(PartyMembers));
        }

        if ((changes & ProjectionChange.ProtagonistPersonaSlots) != 0)
        {
            OnPropertyChanged(nameof(ProtagonistPersonaSlots));
        }

        if ((changes & ProjectionChange.PartyPersonaSlots) != 0)
        {
            OnPropertyChanged(nameof(PartyPersonaSlots));
        }

        if ((changes & ProjectionChange.CompendiumPersonaSlots) != 0)
        {
            OnPropertyChanged(nameof(CompendiumPersonaSlots));
        }

        if ((changes & ProjectionChange.InventoryEntries) != 0)
        {
            OnPropertyChanged(nameof(InventoryEntries));
        }
    }

    private bool DiagnosticsBelongToPendingWrite(SaveEditorWriteToken operationToken) =>
        currentDiagnosticsScope == DiagnosticScope.PendingWrite &&
        currentDiagnosticsOperationToken == operationToken;

    private bool SetDirtyStateBacking(bool value)
    {
        if (isDirty == value)
        {
            return false;
        }

        isDirty = value;
        return true;
    }

    private static bool SetBacking<T>(ref T field, T value, Func<T, T, bool>? areEqual = null)
    {
        bool isEqual = areEqual is null
            ? EqualityComparer<T>.Default.Equals(field, value)
            : areEqual(field, value);

        if (isEqual)
        {
            return false;
        }

        field = value;
        return true;
    }

    private bool CalculateDirtyState() =>
        workingSave is not null &&
            (pendingSerializedSave is not null ||
                lastPersistedState is null ||
                !StatesEqual(workingSave.State, lastPersistedState));

    private static ReadOnlyCollection<PartyMemberSlotViewState> ProjectPartyMembers(
        IReadOnlyList<PartyMemberId> members) =>
        Array.AsReadOnly(members
            .Select(static (member, index) => new PartyMemberSlotViewState(index, member.Value))
            .ToArray());

    private static ReadOnlyCollection<PersonaSlotViewState> ProjectPersonaSlots(
        IReadOnlyList<PersonaSlot> slots) =>
        Array.AsReadOnly(slots
            .Select(static (slot, index) => new PersonaSlotViewState(
                index,
                slot.Exists,
                slot.PersonaId,
                slot.Level,
                slot.TotalExperience,
                slot.SkillIds,
                slot.Strength,
                slot.Magic,
                slot.Endurance,
                slot.Agility,
                slot.Luck))
            .ToArray());

    private static ReadOnlyCollection<InventoryStackViewState> ProjectInventoryStacks(
        IReadOnlyList<InventoryStack> stacks) =>
        Array.AsReadOnly(stacks
            .Select(static (stack, index) => InventoryCatalogProjection.ProjectStack(index, stack))
            .ToArray());

    private static bool InventoryEntriesEqual(
        IReadOnlyList<InventoryStackViewState> left,
        IReadOnlyList<InventoryStackViewState> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            InventoryStackViewState leftEntry = left[index];
            InventoryStackViewState rightEntry = right[index];
            if (leftEntry.SlotIndex != rightEntry.SlotIndex ||
                leftEntry.ItemId != rightEntry.ItemId ||
                leftEntry.ItemName != rightEntry.ItemName ||
                leftEntry.CategoryId != rightEntry.CategoryId ||
                leftEntry.CategoryName != rightEntry.CategoryName ||
                leftEntry.Quantity != rightEntry.Quantity ||
                leftEntry.IsPlaceholder != rightEntry.IsPlaceholder)
            {
                return false;
            }
        }

        return true;
    }

    private static bool StatesEqual(WorkingSaveState left, WorkingSaveState right) =>
        left.Names == right.Names &&
        left.Yen == right.Yen &&
        left.PartyMembers.SequenceEqual(right.PartyMembers) &&
        left.ProtagonistPersonaSlots.SequenceEqual(right.ProtagonistPersonaSlots) &&
        left.PartyPersonaSlots.SequenceEqual(right.PartyPersonaSlots) &&
        left.CompendiumPersonaSlots.SequenceEqual(right.CompendiumPersonaSlots) &&
        left.InventoryStacks.SequenceEqual(right.InventoryStacks);

    private enum DiagnosticScope
    {
        General,
        PendingWrite,
    }

    [Flags]
    private enum ProjectionChange
    {
        None = 0,
        FamilyName = 1,
        GivenName = 2,
        Yen = 4,
        PartyMembers = 8,
        ProtagonistPersonaSlots = 16,
        PartyPersonaSlots = 32,
        CompendiumPersonaSlots = 64,
        InventoryEntries = 128,
    }

    private sealed record PendingSerializedSave(SaveEditorWriteToken OperationToken, WorkingSaveState State);
}
