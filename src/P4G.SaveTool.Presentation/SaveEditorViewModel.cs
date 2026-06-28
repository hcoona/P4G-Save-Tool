using System.Collections.ObjectModel;
using P4G.SaveTool.Catalog;
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

    private static readonly ReadOnlyCollection<EquipmentCharacterViewState> EmptyEquipmentCharacters =
        Array.AsReadOnly(Array.Empty<EquipmentCharacterViewState>());

    private static readonly ReadOnlyCollection<SocialStatViewState> EmptySocialStats =
        Array.AsReadOnly(Array.Empty<SocialStatViewState>());

    private static readonly ReadOnlyCollection<SocialLinkViewState> EmptySocialLinks =
        Array.AsReadOnly(Array.Empty<SocialLinkViewState>());

    private static readonly ReadOnlyCollection<PartyMemberChoiceViewState> EmptyPartyMemberChoices =
        Array.AsReadOnly(Array.Empty<PartyMemberChoiceViewState>());

    private static readonly PersonaSlot BlankPersonaSlot = new(
        false,
        0,
        0,
        0,
        [0, 0, 0],
        0,
        [0, 0, 0, 0, 0, 0, 0, 0],
        0,
        0,
        0,
        0,
        0);

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
    private byte mainCharacterLevel;
    private uint mainCharacterTotalExperience;
    private IReadOnlyList<PartyMemberSlotViewState> partyMembers = EmptyPartyMembers;
    private IReadOnlyList<PartyMemberChoiceViewState> partyMemberChoices = EmptyPartyMemberChoices;
    private IReadOnlyList<PersonaSlotViewState> protagonistPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<PersonaSlotViewState> partyPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<PersonaSlotViewState> compendiumPersonaSlots = EmptyPersonaSlots;
    private IReadOnlyList<InventoryStackViewState> inventoryEntries = EmptyInventoryStacks;
    private IReadOnlyList<EquipmentCharacterViewState> equipmentCharacters = EmptyEquipmentCharacters;
    private IReadOnlyList<SocialStatViewState> socialStats = EmptySocialStats;
    private IReadOnlyList<SocialLinkViewState> socialLinks = EmptySocialLinks;
    private CalendarViewState calendar = new(0, 0, 0, 0);
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

    public byte MainCharacterLevel
    {
        get => mainCharacterLevel;
        private set => SetProperty(ref mainCharacterLevel, value);
    }

    public uint MainCharacterTotalExperience
    {
        get => mainCharacterTotalExperience;
        private set => SetProperty(ref mainCharacterTotalExperience, value);
    }

    public IReadOnlyList<PartyMemberSlotViewState> PartyMembers
    {
        get => partyMembers;
        private set => SetProperty(ref partyMembers, value);
    }

    public IReadOnlyList<PartyMemberChoiceViewState> PartyMemberChoices
    {
        get => partyMemberChoices;
        private set => SetProperty(ref partyMemberChoices, value);
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

    public IReadOnlyList<EquipmentCharacterViewState> EquipmentCharacters
    {
        get => equipmentCharacters;
        private set => SetProperty(ref equipmentCharacters, value);
    }

    public IReadOnlyList<SocialStatViewState> SocialStats
    {
        get => socialStats;
        private set => SetProperty(ref socialStats, value);
    }

    public IReadOnlyList<SocialLinkViewState> SocialLinks
    {
        get => socialLinks;
        private set => SetProperty(ref socialLinks, value);
    }

    public CalendarViewState Calendar
    {
        get => calendar;
        private set => SetProperty(ref calendar, value);
    }

    public static IReadOnlyList<CalendarPhaseChoiceViewState> CalendarPhaseChoices => CalendarProjection.PhaseChoices;

    public static IReadOnlyList<ItemCategoryViewState> InventoryCategories => InventoryCatalogProjection.Categories;

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

    public static IReadOnlyList<InventoryItemChoiceViewState> GetInventoryItemsForCategory(byte categoryId) =>
        InventoryCatalogProjection.GetItems(categoryId);

    public static IReadOnlyList<InventoryItemChoiceViewState> GetWeaponChoices(byte characterId) =>
        InventoryCatalogProjection.GetWeaponChoices(characterId);

    public static IReadOnlyList<InventoryItemChoiceViewState> GetArmorChoices() =>
        InventoryCatalogProjection.GetItems((byte)ItemCategoryId.Armor);

    public static IReadOnlyList<InventoryItemChoiceViewState> GetAccessoryChoices() =>
        InventoryCatalogProjection.GetItems((byte)ItemCategoryId.Accessories);

    public static IReadOnlyList<InventoryItemChoiceViewState> GetCostumeChoices() =>
        InventoryCatalogProjection.GetItems((byte)ItemCategoryId.Costumes);

    public static IReadOnlyList<SocialStatRankChoiceViewState> GetSocialStatChoices(int statIndex, ushort currentPoints, out SocialStatRankChoiceViewState selectedChoice) =>
        SocialStatProjection.GetRankChoices(statIndex, currentPoints, out selectedChoice);

    public static IReadOnlyList<SocialLinkChoiceViewState> GetSocialLinkChoices(byte currentLinkId, out SocialLinkChoiceViewState selectedChoice) =>
        SocialLinkProjection.GetChoices(currentLinkId, out selectedChoice);

    public static IReadOnlyList<CalendarPhaseChoiceViewState> GetCalendarPhaseChoices(int currentPhaseId, out CalendarPhaseChoiceViewState selectedChoice) =>
        CalendarProjection.GetPhaseChoices(currentPhaseId, out selectedChoice);

    public static IReadOnlyList<PersonaChoiceViewState> GetPersonaChoices(ushort currentPersonaId, out PersonaChoiceViewState selectedChoice) =>
        PersonaSelectionProjection.GetPersonaChoices(currentPersonaId, out selectedChoice);

    public static IReadOnlyList<SkillChoiceViewState> GetSkillChoices(ushort currentSkillId, out SkillChoiceViewState selectedChoice) =>
        PersonaSelectionProjection.GetSkillChoices(currentSkillId, out selectedChoice);

    public SaveEditorOperationResult OpenSave(ReadOnlyMemory<byte> bytes)
    {
        return LoadSave(saveApplicationService.Open(bytes));
    }

    public SaveEditorOperationResult CreateBlankSave()
    {
        if (HasSave)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return LoadSave(saveApplicationService.CreateBlankSave());
    }

    public SaveEditorOperationResult ClearSave()
    {
        workingSave = null;
        lastPersistedState = null;
        pendingSerializedSave = null;
        familyName = string.Empty;
        givenName = string.Empty;
        yen = 0;
        mainCharacterLevel = 0;
        mainCharacterTotalExperience = 0;
        partyMembers = EmptyPartyMembers;
        partyMemberChoices = EmptyPartyMemberChoices;
        protagonistPersonaSlots = EmptyPersonaSlots;
        partyPersonaSlots = EmptyPersonaSlots;
        compendiumPersonaSlots = EmptyPersonaSlots;
        inventoryEntries = EmptyInventoryStacks;
        equipmentCharacters = EmptyEquipmentCharacters;
        socialStats = EmptySocialStats;
        socialLinks = EmptySocialLinks;
        calendar = new CalendarViewState(0, 0, 0, 0);
        SetDirtyStateBacking(false);
        SetDiagnosticsBacking([], DiagnosticScope.General, null);

        OnPropertyChanged(nameof(HasSave));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(IsDirty));
        NotifyProjectionChanged(
            ProjectionChange.FamilyName |
            ProjectionChange.GivenName |
            ProjectionChange.Yen |
            ProjectionChange.MainCharacter |
            ProjectionChange.PartyMembers |
            ProjectionChange.PartyMemberChoices |
            ProjectionChange.EquipmentCharacters |
            ProjectionChange.SocialStats |
            ProjectionChange.Calendar |
            ProjectionChange.ProtagonistPersonaSlots |
            ProjectionChange.PartyPersonaSlots |
            ProjectionChange.CompendiumPersonaSlots |
            ProjectionChange.InventoryEntries |
            ProjectionChange.SocialLinks);
        NotifyDiagnosticsChanged();

        return new SaveEditorOperationResult(true, []);
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

        return ApplyEdits([new SetSaveNamesEdit(names.FamilyName, names.GivenName)]);
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
            new SetSaveNamesEdit(familyName, givenName),
            new SetYenEdit(yen),
        ];

        for (int index = 0; index < partyMemberValues.Count; index++)
        {
            edits.Add(new SetPartyMemberEdit(index, partyMemberValues[index]));
        }

        return ApplyEdits(edits);
    }

    private SaveEditorOperationResult LoadSave(SaveOpenResult<WorkingSave> result)
    {
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

    public SaveEditorOperationResult SetPartyMember(int slotIndex, PartyMemberId memberId) =>
        ApplyEdits([new SetPartyMemberEdit(slotIndex, memberId.Value)]);

    public SaveEditorOperationResult SetEquippedWeapon(int characterId, ushort itemId) =>
        ApplyEdits([new SetEquippedWeaponEdit(characterId, itemId)]);

    public SaveEditorOperationResult SetEquippedArmor(int characterId, ushort itemId) =>
        ApplyEdits([new SetEquippedArmorEdit(characterId, itemId)]);

    public SaveEditorOperationResult SetEquippedAccessory(int characterId, ushort itemId) =>
        ApplyEdits([new SetEquippedAccessoryEdit(characterId, itemId)]);

    public SaveEditorOperationResult SetEquippedCostume(int characterId, ushort itemId) =>
        ApplyEdits([new SetEquippedCostumeEdit(characterId, itemId)]);

    public SaveEditorOperationResult SetSocialStatRank(int statIndex, int rank)
    {
        if (workingSave is not null &&
            SocialStatRules.IsSupportedStatIndex(statIndex) &&
            SocialStatRules.IsSupportedRank(rank) &&
            socialStats[statIndex].Rank == rank)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetSocialStatRankEdit(statIndex, rank)]);
    }

    public SaveEditorOperationResult AddSocialLink(byte linkId)
    {
        return ApplyEdits([new AddSocialLinkEdit(linkId)]);
    }

    public SaveEditorOperationResult RemoveSocialLink(int slotIndex) =>
        ApplyEdits([new RemoveSocialLinkEdit(slotIndex)]);

    public SaveEditorOperationResult SetSocialLinkLevel(int slotIndex, byte level)
    {
        if (workingSave is not null &&
            (uint)slotIndex < (uint)socialLinks.Count &&
            socialLinks[slotIndex].Level == level)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetSocialLinkLevelEdit(slotIndex, level)]);
    }

    public SaveEditorOperationResult SetSocialLinkProgress(int slotIndex, byte progress)
    {
        if (workingSave is not null &&
            (uint)slotIndex < (uint)socialLinks.Count &&
            socialLinks[slotIndex].Progress == progress)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetSocialLinkProgressEdit(slotIndex, progress)]);
    }

    public SaveEditorOperationResult SetSocialLinkFlag(int slotIndex, byte flag)
    {
        if (workingSave is not null &&
            (uint)slotIndex < (uint)socialLinks.Count &&
            socialLinks[slotIndex].Flag == flag)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetSocialLinkFlagEdit(slotIndex, flag)]);
    }

    public SaveEditorOperationResult SetDay(int day) =>
        ApplyEdits([new SetDayEdit(day)]);

    public SaveEditorOperationResult SetDayPhase(int phaseId)
    {
        if (workingSave is not null && calendar.DayPhaseId == phaseId)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetDayPhaseEdit(phaseId)]);
    }

    public SaveEditorOperationResult SetNextDay(int day) =>
        ApplyEdits([new SetNextDayEdit(day)]);

    public SaveEditorOperationResult SetNextDayPhase(int phaseId)
    {
        if (workingSave is not null && calendar.NextDayPhaseId == phaseId)
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetNextDayPhaseEdit(phaseId)]);
    }

    public SaveEditorOperationResult SetMainCharacterLevel(byte level) =>
        ApplyEdits([new SetMainCharacterLevelEdit(level)]);

    public SaveEditorOperationResult SetMainCharacterTotalExperience(uint totalExperience) =>
        ApplyEdits([new SetMainCharacterTotalExperienceEdit(totalExperience)]);

    public SaveEditorOperationResult SetProtagonistPersonaSlot(int slotIndex, PersonaSlotEdit personaSlot) =>
        ApplyEdits([new SetProtagonistPersonaSlotEdit(slotIndex, personaSlot)]);

    public SaveEditorOperationResult SetPartyPersonaSlot(int slotIndex, PersonaSlotEdit personaSlot) =>
        ApplyEdits([new SetPartyPersonaSlotEdit(slotIndex, personaSlot)]);

    public SaveEditorOperationResult SetCompendiumPersonaSlot(int slotIndex, PersonaSlotEdit personaSlot)
    {
        if (workingSave is not null &&
            (uint)slotIndex < (uint)workingSave.State.CompendiumPersonaSlots.Count &&
            PersonaSlotMatchesVisibleValues(workingSave.State.CompendiumPersonaSlots[slotIndex], personaSlot))
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new SetCompendiumPersonaSlotEdit(slotIndex, personaSlot)]);
    }

    public SaveEditorOperationResult ClearCompendiumPersonaSlot(int slotIndex)
    {
        if (workingSave is not null &&
            (uint)slotIndex < (uint)workingSave.State.CompendiumPersonaSlots.Count &&
            workingSave.State.CompendiumPersonaSlots[slotIndex].Equals(BlankPersonaSlot))
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new ClearCompendiumPersonaSlotEdit(slotIndex)]);
    }

    public SaveEditorOperationResult ClearCompendiumPersonaSlots()
    {
        if (workingSave is not null &&
            workingSave.State.CompendiumPersonaSlots.All(static slot => slot.Equals(BlankPersonaSlot)))
        {
            return new SaveEditorOperationResult(true, Array.Empty<SaveDiagnostic>());
        }

        return ApplyEdits([new ClearCompendiumPersonaSlotsEdit()]);
    }

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
        IReadOnlyList<PartyMemberChoiceViewState> nextPartyMemberChoices = ProjectPartyMemberChoices(state);
        IReadOnlyList<EquipmentCharacterViewState> nextEquipmentCharacters = ProjectEquipmentCharacters(state);
        IReadOnlyList<SocialStatViewState> nextSocialStats = ProjectSocialStats(state.SocialStats);
        IReadOnlyList<SocialLinkViewState> nextSocialLinks = ProjectSocialLinks(state.SocialLinks);
        CalendarViewState nextCalendar = ProjectCalendar(state);
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

        bool mainCharacterChanged = SetBacking(ref mainCharacterLevel, state.MainCharacterLevel);
        mainCharacterChanged = SetBacking(ref mainCharacterTotalExperience, state.MainCharacterTotalExperience) || mainCharacterChanged;
        if (mainCharacterChanged)
        {
            changes |= ProjectionChange.MainCharacter;
        }

        if (SetBacking(ref partyMembers, nextPartyMembers))
        {
            changes |= ProjectionChange.PartyMembers;
        }

        if (SetBacking(ref partyMemberChoices, nextPartyMemberChoices))
        {
            changes |= ProjectionChange.PartyMemberChoices;
        }

        if (SetBacking(ref equipmentCharacters, nextEquipmentCharacters, EquipmentCharactersEqual))
        {
            changes |= ProjectionChange.EquipmentCharacters;
        }

        if (SetBacking(ref socialStats, nextSocialStats, SocialStatsEqual))
        {
            changes |= ProjectionChange.SocialStats;
        }

        if (SetBacking(ref socialLinks, nextSocialLinks, SocialLinksEqual))
        {
            changes |= ProjectionChange.SocialLinks;
        }

        if (SetBacking(ref calendar, nextCalendar))
        {
            changes |= ProjectionChange.Calendar;
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

        if ((changes & ProjectionChange.MainCharacter) != 0)
        {
            OnPropertyChanged(nameof(MainCharacterLevel));
            OnPropertyChanged(nameof(MainCharacterTotalExperience));
        }

        if ((changes & ProjectionChange.PartyMembers) != 0)
        {
            OnPropertyChanged(nameof(PartyMembers));
        }

        if ((changes & ProjectionChange.PartyMemberChoices) != 0)
        {
            OnPropertyChanged(nameof(PartyMemberChoices));
        }

        if ((changes & ProjectionChange.EquipmentCharacters) != 0)
        {
            OnPropertyChanged(nameof(EquipmentCharacters));
        }

        if ((changes & ProjectionChange.SocialStats) != 0)
        {
            OnPropertyChanged(nameof(SocialStats));
        }

        if ((changes & ProjectionChange.SocialLinks) != 0)
        {
            OnPropertyChanged(nameof(SocialLinks));
        }

        if ((changes & ProjectionChange.Calendar) != 0)
        {
            OnPropertyChanged(nameof(Calendar));
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

    private static ReadOnlyCollection<PartyMemberChoiceViewState> ProjectPartyMemberChoices(WorkingSaveState state) =>
        Array.AsReadOnly(PersonaSelectionProjection.ProjectPartyMemberChoices(state).ToArray());

    private static ReadOnlyCollection<EquipmentCharacterViewState> ProjectEquipmentCharacters(WorkingSaveState state)
    {
        List<EquipmentCharacterViewState> characters = [];
        foreach (PartyMemberCatalogEntry member in P4GCatalog.PartyMembers)
        {
            if (member.Id == 4)
            {
                continue;
            }

            characters.Add(new EquipmentCharacterViewState(
                member.Id,
                member.Id == 0
                    ? $"{state.Names.GivenName} {state.Names.FamilyName}"
                    : member.Name,
                state.EquippedWeapons[member.Id],
                state.EquippedArmors[member.Id],
                state.EquippedAccessories[member.Id],
                state.EquippedCostumes[member.Id]));
        }

        return Array.AsReadOnly(characters.ToArray());
    }

    private static ReadOnlyCollection<SocialStatViewState> ProjectSocialStats(IReadOnlyList<ushort> socialStats) =>
        SocialStatProjection.ProjectSocialStats(socialStats);

    private static ReadOnlyCollection<SocialLinkViewState> ProjectSocialLinks(IReadOnlyList<SocialLinkState> socialLinks) =>
        SocialLinkProjection.ProjectSocialLinks(socialLinks);

    private static CalendarViewState ProjectCalendar(WorkingSaveState state) =>
        CalendarProjection.ProjectCalendar(state);

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

    private static bool EquipmentCharactersEqual(
        IReadOnlyList<EquipmentCharacterViewState> left,
        IReadOnlyList<EquipmentCharacterViewState> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static bool SocialStatsEqual(
        IReadOnlyList<SocialStatViewState> left,
        IReadOnlyList<SocialStatViewState> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static bool SocialLinksEqual(
        IReadOnlyList<SocialLinkViewState> left,
        IReadOnlyList<SocialLinkViewState> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static bool PersonaSlotMatchesVisibleValues(PersonaSlot currentSlot, PersonaSlotEdit personaSlotEdit) =>
        currentSlot.PersonaId == personaSlotEdit.PersonaId &&
        currentSlot.Level == personaSlotEdit.Level &&
        currentSlot.TotalExperience == personaSlotEdit.TotalExperience &&
        currentSlot.SkillIds.SequenceEqual(personaSlotEdit.SkillIds) &&
        currentSlot.Strength == personaSlotEdit.Strength &&
        currentSlot.Magic == personaSlotEdit.Magic &&
        currentSlot.Endurance == personaSlotEdit.Endurance &&
        currentSlot.Agility == personaSlotEdit.Agility &&
        currentSlot.Luck == personaSlotEdit.Luck;

    private static bool StatesEqual(WorkingSaveState left, WorkingSaveState right) =>
        left.Names == right.Names &&
        left.Yen == right.Yen &&
        left.MainCharacterLevel == right.MainCharacterLevel &&
        left.MainCharacterTotalExperience == right.MainCharacterTotalExperience &&
        left.PartyMembers.SequenceEqual(right.PartyMembers) &&
        left.SocialStats.SequenceEqual(right.SocialStats) &&
        left.SocialLinks.SequenceEqual(right.SocialLinks) &&
        left.Day == right.Day &&
        left.DayPhase == right.DayPhase &&
        left.NextDay == right.NextDay &&
        left.NextDayPhase == right.NextDayPhase &&
        left.ProtagonistPersonaSlots.SequenceEqual(right.ProtagonistPersonaSlots) &&
        left.PartyPersonaSlots.SequenceEqual(right.PartyPersonaSlots) &&
        left.CompendiumPersonaSlots.SequenceEqual(right.CompendiumPersonaSlots) &&
        left.EquippedWeapons.SequenceEqual(right.EquippedWeapons) &&
        left.EquippedArmors.SequenceEqual(right.EquippedArmors) &&
        left.EquippedAccessories.SequenceEqual(right.EquippedAccessories) &&
        left.EquippedCostumes.SequenceEqual(right.EquippedCostumes) &&
        InventoryStacksEqual(left.InventoryStacks, right.InventoryStacks);

    private static bool InventoryStacksEqual(
        IReadOnlyList<InventoryStack> left,
        IReadOnlyList<InventoryStack> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        InventoryStack[] sortedLeft = left
            .OrderBy(static stack => stack.ItemId)
            .ThenBy(static stack => stack.Quantity)
            .ToArray();

        InventoryStack[] sortedRight = right
            .OrderBy(static stack => stack.ItemId)
            .ThenBy(static stack => stack.Quantity)
            .ToArray();

        return sortedLeft.SequenceEqual(sortedRight);
    }

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
        MainCharacter = 8,
        PartyMembers = 16,
        PartyMemberChoices = 32,
        EquipmentCharacters = 64,
        SocialStats = 128,
        Calendar = 256,
        ProtagonistPersonaSlots = 512,
        PartyPersonaSlots = 1024,
        CompendiumPersonaSlots = 2048,
        InventoryEntries = 4096,
        SocialLinks = 8192,
    }

    private sealed record PendingSerializedSave(SaveEditorWriteToken OperationToken, WorkingSaveState State);
}
