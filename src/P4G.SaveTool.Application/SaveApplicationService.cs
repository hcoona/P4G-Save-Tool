using System.Buffers.Binary;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.SaveFormat;

namespace P4G.SaveTool.Application;

public sealed class SaveApplicationService : ISaveApplicationService
{
    private const int PartyMemberCount = 3;
    private const int SocialLinkSlotStride = 16;
    private const string EditDiagnosticTarget = "Edit";
    private const string NamesDiagnosticTarget = "Names";
    private const string PartyMembersDiagnosticTarget = "PartyMembers";
    private const string SocialStatsDiagnosticTarget = "SocialStats";
    private const string CalendarDiagnosticTarget = "Calendar";
    private const string InventoryDiagnosticTarget = "Inventory";
    private const string EquipmentDiagnosticTarget = "Equipment";
    private const string PersonaDiagnosticTarget = "Persona";
    private const string SocialLinksDiagnosticTarget = "SocialLinks";
    private const string InvalidSocialLinkDiagnosticCode = "P4GAPP016";
    private const string DuplicateSocialLinkDiagnosticCode = "P4GAPP017";
    private const string SocialLinksCapacityDiagnosticCode = "P4GAPP015";
    private const uint LegacyCompendiumMaximumTotalExperience = 999_999_999;

    private readonly IApplicationSaveCodec codec;

    public SaveApplicationService()
        : this(P4GApplicationSaveCodec.Instance)
    {
    }

    internal SaveApplicationService(IApplicationSaveCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);

        this.codec = codec;
    }

    public SaveOpenResult<WorkingSave> Open(ReadOnlyMemory<byte> bytes)
    {
        SaveOpenResult<SaveSnapshot> openResult = codec.Open(bytes);
        if (!openResult.Succeeded || openResult.Snapshot is null)
        {
            return new SaveOpenResult<WorkingSave>(null, openResult.Diagnostics);
        }

        ApplicationWorkingSave save = new(openResult.Snapshot, CreateState(openResult.Snapshot));
        return new SaveOpenResult<WorkingSave>(save, openResult.Diagnostics);
    }

    public SaveOpenResult<WorkingSave> CreateBlankSave()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        return Open(new byte[layout.MinimumLength]);
    }

    public SaveEditResult<WorkingSave> ApplyEdits(WorkingSave save, IEnumerable<SaveEditCommand> edits)
    {
        ApplicationWorkingSave applicationSave = GetApplicationSave(save);
        ArgumentNullException.ThrowIfNull(edits);

        WorkingSaveState updatedState = applicationSave.State;
        List<SaveDiagnostic> diagnostics = [];
        P4GSaveLayout layout = P4GSaveLayout.For(applicationSave.Snapshot.LayoutKind);

        foreach (SaveEditCommand? edit in edits)
        {
            updatedState = ApplyEdit(updatedState, layout, edit, diagnostics);
        }

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new SaveEditResult<WorkingSave>(null, diagnostics);
        }

        WorkingSave updatedSave = ReferenceEquals(updatedState, applicationSave.State)
            ? applicationSave
            : applicationSave.WithState(updatedState);
        return new SaveEditResult<WorkingSave>(updatedSave, diagnostics);
    }

    public SaveWriteResult Write(WorkingSave save)
    {
        ApplicationWorkingSave applicationSave = GetApplicationSave(save);
        P4GSaveLayout layout = P4GSaveLayout.For(applicationSave.Snapshot.LayoutKind);

        if (applicationSave.State.SocialLinks.Count > GetSocialLinkSlotCount(layout))
        {
            return SaveWriteResult.Failure(
                [
                    new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        SocialLinksCapacityDiagnosticCode,
                        "Social link state exceeds the available slot capacity.",
                        SocialLinksDiagnosticTarget),
                ]);
        }

        return codec.Write(applicationSave.Snapshot, CreatePatches(applicationSave));
    }

    private static WorkingSaveState ApplyEdit(
        WorkingSaveState state,
        P4GSaveLayout layout,
        SaveEditCommand? edit,
        List<SaveDiagnostic> diagnostics)
    {
        switch (edit)
        {
            case SetSaveNamesEdit setNames:
                SaveNames names = new(setNames.FamilyName, setNames.GivenName);
                if (!AreSupportedNames(names))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP002",
                        "Save names contain unsupported text.",
                        NamesDiagnosticTarget));
                    return state;
                }

                return state.WithNames(names);

            case SetYenEdit setYen:
                return state.WithYen(setYen.Yen);

            case SetMainCharacterLevelEdit setMainCharacterLevel:
                return state.WithMainCharacterLevel(setMainCharacterLevel.Level);

            case SetMainCharacterTotalExperienceEdit setMainCharacterTotalExperience:
                return state.WithMainCharacterTotalExperience(setMainCharacterTotalExperience.TotalExperience);

            case SetPartyMemberEdit setPartyMember:
                if ((uint)setPartyMember.SlotIndex >= PartyMemberCount)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP003",
                        "Party member edit targets an unsupported slot.",
                        PartyMembersDiagnosticTarget));
                    return state;
                }

                return state.WithPartyMember(setPartyMember.SlotIndex, new PartyMemberId(setPartyMember.MemberValue));

            case SetSocialStatRankEdit setSocialStatRank:
                if (!SocialStatRules.IsSupportedStatIndex(setSocialStatRank.StatIndex))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP010",
                        "Social stat edit targets an unsupported stat slot.",
                        SocialStatsDiagnosticTarget));
                    return state;
                }

                if (!SocialStatRules.IsSupportedRank(setSocialStatRank.Rank))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP011",
                        "Social stat edit targets an unsupported rank.",
                        SocialStatsDiagnosticTarget));
                    return state;
                }

                return state.WithSocialStat(
                    setSocialStatRank.StatIndex,
                    SocialStatRules.RankToPoints(setSocialStatRank.StatIndex, setSocialStatRank.Rank));

            case SetDayEdit setDay:
                if (!IsSupportedDayValue(setDay.Day))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP012",
                        "Calendar day edit targets an unsupported day value.",
                        CalendarDiagnosticTarget));
                    return state;
                }

                return state.WithDay((byte)setDay.Day);

            case SetDayPhaseEdit setDayPhase:
                if (!CalendarPhaseRules.IsSupportedPhaseId(setDayPhase.PhaseId))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP013",
                        "Calendar phase edit targets an unsupported phase id.",
                        CalendarDiagnosticTarget));
                    return state;
                }

                return state.WithDayPhase((byte)setDayPhase.PhaseId);

            case SetNextDayEdit setNextDay:
                if (!IsSupportedDayValue(setNextDay.Day))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP012",
                        "Calendar day edit targets an unsupported day value.",
                        CalendarDiagnosticTarget));
                    return state;
                }

                return state.WithNextDay((byte)setNextDay.Day);

            case SetNextDayPhaseEdit setNextDayPhase:
                if (!CalendarPhaseRules.IsSupportedPhaseId(setNextDayPhase.PhaseId))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP013",
                        "Calendar phase edit targets an unsupported phase id.",
                        CalendarDiagnosticTarget));
                    return state;
                }

                return state.WithNextDayPhase((byte)setNextDayPhase.PhaseId);

            case SetEquippedWeaponEdit setEquippedWeapon:
                return ApplyEquipmentEdit(
                    state,
                    diagnostics,
                    setEquippedWeapon.CharacterId,
                    setEquippedWeapon.ItemId,
                    EquipmentItemEditability.IsSupportedWeaponItemId,
                    static (currentState, characterId, itemId) => currentState.WithEquippedWeapon(characterId, itemId));

            case SetEquippedArmorEdit setEquippedArmor:
                return ApplyEquipmentEdit(
                    state,
                    diagnostics,
                    setEquippedArmor.CharacterId,
                    setEquippedArmor.ItemId,
                    EquipmentItemEditability.IsSupportedArmorItemId,
                    static (currentState, characterId, itemId) => currentState.WithEquippedArmor(characterId, itemId));

            case SetEquippedAccessoryEdit setEquippedAccessory:
                return ApplyEquipmentEdit(
                    state,
                    diagnostics,
                    setEquippedAccessory.CharacterId,
                    setEquippedAccessory.ItemId,
                    EquipmentItemEditability.IsSupportedAccessoryItemId,
                    static (currentState, characterId, itemId) => currentState.WithEquippedAccessory(characterId, itemId));

            case SetEquippedCostumeEdit setEquippedCostume:
                return ApplyEquipmentEdit(
                    state,
                    diagnostics,
                    setEquippedCostume.CharacterId,
                    setEquippedCostume.ItemId,
                    EquipmentItemEditability.IsSupportedCostumeItemId,
                    static (currentState, characterId, itemId) => currentState.WithEquippedCostume(characterId, itemId));

            case SetInventoryItemQuantityEdit setInventoryItemQuantity:
                if (!IsSupportedInventoryItem(layout, setInventoryItemQuantity.ItemId))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP004",
                        "Inventory item edit targets an unsupported item id.",
                        InventoryDiagnosticTarget));
                    return state;
                }

                return state.WithInventoryItemQuantity(setInventoryItemQuantity.ItemId, setInventoryItemQuantity.Quantity);

            case RemoveInventoryItemEdit removeInventoryItem:
                if (!IsSupportedInventoryItem(layout, removeInventoryItem.ItemId))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP004",
                        "Inventory item edit targets an unsupported item id.",
                        InventoryDiagnosticTarget));
                    return state;
                }

                return state.WithInventoryItemRemoved(removeInventoryItem.ItemId);

            case SetProtagonistPersonaSlotEdit setProtagonistPersonaSlot:
                return ApplyPersonaSlotEdit(
                    state,
                    diagnostics,
                    setProtagonistPersonaSlot.SlotIndex,
                    setProtagonistPersonaSlot.PersonaSlot,
                    static (currentState, slotIndex, personaSlot) => currentState.WithProtagonistPersonaSlot(slotIndex, personaSlot),
                    state.ProtagonistPersonaSlots,
                    "ProtagonistPersonaSlots",
                    allowBlankPersonaId: true);

            case SetPartyPersonaSlotEdit setPartyPersonaSlot:
                return ApplyPersonaSlotEdit(
                    state,
                    diagnostics,
                    setPartyPersonaSlot.SlotIndex,
                    setPartyPersonaSlot.PersonaSlot,
                    static (currentState, slotIndex, personaSlot) => currentState.WithPartyPersonaSlot(slotIndex, personaSlot),
                    state.PartyPersonaSlots,
                    "PartyPersonaSlots",
                    allowBlankPersonaId: true);

            case SetCompendiumPersonaSlotEdit setCompendiumPersonaSlot:
                return ApplyPersonaSlotEdit(
                    state,
                    diagnostics,
                    setCompendiumPersonaSlot.SlotIndex,
                    setCompendiumPersonaSlot.PersonaSlot,
                    static (currentState, slotIndex, personaSlot) => currentState.WithCompendiumPersonaSlot(slotIndex, personaSlot),
                    state.CompendiumPersonaSlots,
                    "CompendiumPersonaSlots",
                    allowBlankPersonaId: false,
                    maximumTotalExperience: LegacyCompendiumMaximumTotalExperience);

            case ClearCompendiumPersonaSlotEdit clearCompendiumPersonaSlot:
                return ApplyCompendiumClearEdit(state, diagnostics, clearCompendiumPersonaSlot.SlotIndex);

            case ClearCompendiumPersonaSlotsEdit:
                return ApplyCompendiumClearAllEdit(state);

            case AddSocialLinkEdit addSocialLink:
                if (addSocialLink.LinkId == 0)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        InvalidSocialLinkDiagnosticCode,
                        "Social link edit targets an unsupported link id.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                if (state.SocialLinks.Any(existing => existing.LinkId == addSocialLink.LinkId))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        DuplicateSocialLinkDiagnosticCode,
                        "Social link edit targets a duplicate link id.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                if (state.SocialLinks.Count >= GetSocialLinkSlotCount(layout))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        SocialLinksCapacityDiagnosticCode,
                        "Social link edits cannot exceed the available slot capacity.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                return state.WithSocialLinkAdded(new SocialLinkState(addSocialLink.LinkId, 1, 0, 0));

            case RemoveSocialLinkEdit removeSocialLink:
                if ((uint)removeSocialLink.SlotIndex >= (uint)state.SocialLinks.Count)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP014",
                        "Social link edit targets an unsupported slot.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                return state.WithSocialLinkRemoved(removeSocialLink.SlotIndex);

            case SetSocialLinkLevelEdit setSocialLinkLevel:
                if ((uint)setSocialLinkLevel.SlotIndex >= (uint)state.SocialLinks.Count)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP014",
                        "Social link edit targets an unsupported slot.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                if (state.SocialLinks[setSocialLinkLevel.SlotIndex].Level == setSocialLinkLevel.Level)
                {
                    return state;
                }

                return state.WithSocialLink(
                    setSocialLinkLevel.SlotIndex,
                    state.SocialLinks[setSocialLinkLevel.SlotIndex] with { Level = setSocialLinkLevel.Level });

            case SetSocialLinkProgressEdit setSocialLinkProgress:
                if ((uint)setSocialLinkProgress.SlotIndex >= (uint)state.SocialLinks.Count)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP014",
                        "Social link edit targets an unsupported slot.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                if (state.SocialLinks[setSocialLinkProgress.SlotIndex].Progress == setSocialLinkProgress.Progress)
                {
                    return state;
                }

                return state.WithSocialLink(
                    setSocialLinkProgress.SlotIndex,
                    state.SocialLinks[setSocialLinkProgress.SlotIndex] with { Progress = setSocialLinkProgress.Progress });

            case SetSocialLinkFlagEdit setSocialLinkFlag:
                if ((uint)setSocialLinkFlag.SlotIndex >= (uint)state.SocialLinks.Count)
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP014",
                        "Social link edit targets an unsupported slot.",
                        SocialLinksDiagnosticTarget));
                    return state;
                }

                if (state.SocialLinks[setSocialLinkFlag.SlotIndex].Flag == setSocialLinkFlag.Flag)
                {
                    return state;
                }

                return state.WithSocialLink(
                    setSocialLinkFlag.SlotIndex,
                    state.SocialLinks[setSocialLinkFlag.SlotIndex] with { Flag = setSocialLinkFlag.Flag });

            default:
                diagnostics.Add(new SaveDiagnostic(
                    DiagnosticSeverity.Error,
                    "P4GAPP001",
                    "Save edit command is not supported.",
                    EditDiagnosticTarget));
                return state;
        }
    }

    private static bool AreSupportedNames(SaveNames? names) =>
        names is not null &&
        IsSupportedNameComponent(names.FamilyName) &&
        IsSupportedNameComponent(names.GivenName);

    private static bool IsSupportedNameComponent(string? value) =>
        value is not null &&
        value.Length <= SaveStringCodec.EncodedNameCharacterLength;

    private static WorkingSaveState CreateState(SaveSnapshot snapshot) =>
        new(
            snapshot.Names,
            snapshot.Yen,
            snapshot.PartyMembers,
            snapshot.EquippedWeapons,
            snapshot.EquippedArmors,
            snapshot.EquippedAccessories,
            snapshot.EquippedCostumes,
            snapshot.ProtagonistPersonaSlots,
            snapshot.PartyPersonaSlots,
            snapshot.CompendiumPersonaSlots,
            snapshot.InventoryStacks,
            snapshot.SocialStats,
            snapshot.SocialLinks,
            snapshot.MainCharacterLevel,
            snapshot.MainCharacterTotalExperience,
            snapshot.Day,
            snapshot.DayPhase,
            snapshot.NextDay,
            snapshot.NextDayPhase);

    private static ApplicationWorkingSave GetApplicationSave(WorkingSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        return save as ApplicationWorkingSave ??
            throw new ArgumentException("Working save was not opened by this application service.", nameof(save));
    }

    private static List<SaveFieldPatch> CreatePatches(ApplicationWorkingSave save)
    {
        SaveSnapshot snapshot = save.Snapshot;
        WorkingSaveState state = save.State;
        P4GSaveLayout layout = P4GSaveLayout.For(snapshot.LayoutKind);
        List<SaveFieldPatch> patches = [];

        if (state.Names != snapshot.Names)
        {
            patches.Add(CreateJStringPatch(layout.FamilyNameJString, state.Names.FamilyName));
            patches.Add(CreateJStringPatch(layout.GivenNameJString, state.Names.GivenName));
            patches.Add(CreatePStringPatch(layout.FamilyNamePString, state.Names.FamilyName));
            patches.Add(CreatePStringPatch(layout.GivenNamePString, state.Names.GivenName));
        }

        if (state.Yen != snapshot.Yen)
        {
            patches.Add(CreateUInt32Patch(layout.Yen, state.Yen));
        }

        if (state.MainCharacterLevel != snapshot.MainCharacterLevel)
        {
            patches.Add(CreateBytePatch(layout.MainCharacterLevel, state.MainCharacterLevel));
        }

        if (state.MainCharacterTotalExperience != snapshot.MainCharacterTotalExperience)
        {
            patches.Add(CreateUInt32Patch(layout.MainCharacterTotalExperience, state.MainCharacterTotalExperience));
        }

        if (!PartyMembersEqual(state.PartyMembers, snapshot.PartyMembers))
        {
            patches.Add(CreatePartyMembersPatch(snapshot, layout.PartyMembers, state.PartyMembers));
        }

        AddEquipmentPatches(patches, layout, state, snapshot);

        if (!InventoryStacksEqual(state.InventoryStacks, snapshot.InventoryStacks))
        {
            patches.Add(CreateInventoryPatch(layout.Inventory, state.InventoryStacks));
        }

        if (!SocialStatsEqual(state.SocialStats, snapshot.SocialStats))
        {
            patches.Add(CreateSocialStatsPatch(layout.SocialStats, state.SocialStats, snapshot));
        }

        if (!SocialLinksEqual(state.SocialLinks, snapshot.SocialLinks))
        {
            patches.Add(CreateSocialLinksPatch(layout.SocialLinks, state.SocialLinks));
        }

        if (!CalendarEqual(state, snapshot))
        {
            patches.Add(CreateCalendarPatch(layout.Calendar, state, snapshot));
        }

        AddPersonaSlotPatches(
            patches,
            snapshot.ProtagonistPersonaSlots,
            state.ProtagonistPersonaSlots,
            layout.ProtagonistPersonaSlots);
        AddPersonaSlotPatches(
            patches,
            snapshot.PartyPersonaSlots,
            state.PartyPersonaSlots,
            layout.PartyPersonaSlots);
        if (!PersonaSlotsEqual(snapshot.CompendiumPersonaSlots, state.CompendiumPersonaSlots))
        {
            patches.Add(CreateCompendiumPersonaBlockPatch(layout.CompendiumPersonaSlots, state.CompendiumPersonaSlots));
        }

        return patches;
    }

    private static SaveFieldPatch CreateUInt32Patch(SaveFieldDescriptor field, uint value)
    {
        byte[] bytes = new byte[field.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreateBytePatch(SaveFieldDescriptor field, byte value) =>
        new(field.Name, new[] { value });

    private static SaveFieldPatch CreateJStringPatch(SaveFieldDescriptor field, string value)
    {
        byte[] bytes = new byte[field.Length];
        SaveStringCodec.EncodeJString(value, bytes);
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreatePStringPatch(SaveFieldDescriptor field, string value)
    {
        byte[] bytes = new byte[field.Length];
        SaveStringCodec.EncodePString(value, bytes);
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreatePartyMembersPatch(
        SaveSnapshot snapshot,
        SaveFieldDescriptor field,
        IReadOnlyList<PartyMemberId> partyMembers)
    {
        byte[] bytes = snapshot.OriginalBytes.Slice(field.Offset, field.Length).ToArray();
        bytes[0] = partyMembers[0].Value;
        bytes[2] = partyMembers[1].Value;
        bytes[4] = partyMembers[2].Value;
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreateInventoryPatch(
        SaveFieldDescriptor field,
        IReadOnlyList<InventoryStack> inventoryStacks)
    {
        byte[] bytes = new byte[field.Length];
        foreach (InventoryStack stack in NormalizeInventoryStacks(inventoryStacks))
        {
            bytes[stack.ItemId] = stack.Quantity;
        }

        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreateSocialStatsPatch(
        SaveFieldDescriptor field,
        IReadOnlyList<ushort> socialStats,
        SaveSnapshot snapshot)
    {
        byte[] bytes = snapshot.OriginalBytes.Slice(field.Offset, field.Length).ToArray();
        for (int index = 0; index < socialStats.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)), socialStats[index]);
        }

        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreateSocialLinksPatch(
        SaveFieldDescriptor field,
        IReadOnlyList<SocialLinkState> socialLinks)
    {
        byte[] bytes = new byte[field.Length];
        int slotCount = field.Length / SocialLinkSlotStride;
        for (int index = 0; index < slotCount; index++)
        {
            SocialLinkState socialLink = index < socialLinks.Count ? socialLinks[index] : default;
            int offset = index * SocialLinkSlotStride;
            bytes[offset] = socialLink.LinkId;
            bytes[offset + 2] = socialLink.Level;
            bytes[offset + 4] = socialLink.Progress;
            bytes[offset + 12] = socialLink.Flag;
        }

        return new SaveFieldPatch(field.Name, bytes);
    }

    private static SaveFieldPatch CreateCompendiumPersonaBlockPatch(
        PersonaBlockDescriptor block,
        IReadOnlyList<PersonaSlot> compendiumSlots)
    {
        byte[] bytes = new byte[block.EffectiveBlockPatchLength];
        bool[] occupiedSlots = new bool[block.Count];

        WriteCompendiumPersonaSlots(bytes, block, compendiumSlots, occupiedSlots, canonicalKnownPersonas: true);
        WriteCompendiumPersonaSlots(bytes, block, compendiumSlots, occupiedSlots, canonicalKnownPersonas: false);

        return new SaveFieldPatch(block.Name, bytes);
    }

    private static void WriteCompendiumPersonaSlots(
        byte[] bytes,
        PersonaBlockDescriptor block,
        IReadOnlyList<PersonaSlot> compendiumSlots,
        bool[] occupiedSlots,
        bool canonicalKnownPersonas)
    {
        int slotCount = Math.Min(compendiumSlots.Count, block.Count);
        for (int sourceSlotIndex = 0; sourceSlotIndex < slotCount; sourceSlotIndex++)
        {
            PersonaSlot slot = compendiumSlots[sourceSlotIndex];
            if (slot.PersonaId == 0)
            {
                continue;
            }

            bool isCanonicalPersonaId = IsCanonicalCompendiumPersonaId(slot.PersonaId, block.Count);
            if (isCanonicalPersonaId != canonicalKnownPersonas)
            {
                continue;
            }

            int targetSlotIndex = isCanonicalPersonaId
                ? slot.PersonaId - 1
                : sourceSlotIndex;
            if ((uint)targetSlotIndex >= (uint)block.Count ||
                (!canonicalKnownPersonas && occupiedSlots[targetSlotIndex]))
            {
                continue;
            }

            SaveWriteResult writeResult = PersonaSlotBinaryCodec.Write(slot);
            ReadOnlyMemory<byte> personaBytes = writeResult.Bytes ?? Array.Empty<byte>();
            int targetOffset = (targetSlotIndex * block.Stride) + block.PersonaOffsetWithinStride;
            if (targetOffset + PersonaSlotBinaryCodec.BinaryLength > bytes.Length)
            {
                continue;
            }

            personaBytes.Span.CopyTo(bytes.AsSpan(targetOffset, PersonaSlotBinaryCodec.BinaryLength));
            occupiedSlots[targetSlotIndex] = true;
        }
    }

    private static bool IsCanonicalCompendiumPersonaId(ushort personaId, int slotCount) =>
        personaId is not 0 &&
        personaId <= slotCount;

    private static SaveFieldPatch CreateCalendarPatch(
        SaveFieldDescriptor field,
        WorkingSaveState state,
        SaveSnapshot snapshot)
    {
        byte[] bytes = snapshot.OriginalBytes.Slice(field.Offset, field.Length).ToArray();
        bytes[0] = state.Day;
        bytes[2] = state.DayPhase;
        bytes[8] = state.NextDay;
        bytes[10] = state.NextDayPhase;
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static void AddPersonaSlotPatches(
        List<SaveFieldPatch> patches,
        IReadOnlyList<PersonaSlot> snapshotSlots,
        IReadOnlyList<PersonaSlot> stateSlots,
        PersonaBlockDescriptor block)
    {
        int slotCount = Math.Min(snapshotSlots.Count, stateSlots.Count);
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            if (snapshotSlots[slotIndex].Equals(stateSlots[slotIndex]))
            {
                continue;
            }

            patches.Add(CreatePersonaSlotPatch(block, slotIndex, stateSlots[slotIndex]));
        }
    }

    private static bool PersonaSlotsEqual(
        IReadOnlyList<PersonaSlot> left,
        IReadOnlyList<PersonaSlot> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static SaveFieldPatch CreatePersonaSlotPatch(
        PersonaBlockDescriptor block,
        int slotIndex,
        PersonaSlot personaSlot)
    {
        SaveWriteResult writeResult = PersonaSlotBinaryCodec.Write(personaSlot);
        return new SaveFieldPatch(
            $"{block.Name}[{slotIndex}]",
            writeResult.Bytes ?? Array.Empty<byte>());
    }

    private static void AddEquipmentPatches(
        List<SaveFieldPatch> patches,
        P4GSaveLayout layout,
        WorkingSaveState state,
        SaveSnapshot snapshot)
    {
        if (!EquipmentSlotEqual(state, snapshot, 0))
        {
            patches.Add(CreateEquipmentPatch(
                layout.ProtagonistEquipment,
                state.EquippedWeapons[0],
                state.EquippedArmors[0],
                state.EquippedAccessories[0],
                state.EquippedCostumes[0]));
        }

        for (int characterIndex = 1; characterIndex < 8; characterIndex++)
        {
            if (EquipmentSlotEqual(state, snapshot, characterIndex))
            {
                continue;
            }

            patches.Add(CreateEquipmentPatch(
                layout.PartyEquipmentSlots[characterIndex - 1],
                state.EquippedWeapons[characterIndex],
                state.EquippedArmors[characterIndex],
                state.EquippedAccessories[characterIndex],
                state.EquippedCostumes[characterIndex]));
        }
    }

    private static bool EquipmentSlotEqual(WorkingSaveState state, SaveSnapshot snapshot, int characterIndex) =>
        state.EquippedWeapons[characterIndex] == snapshot.EquippedWeapons[characterIndex] &&
        state.EquippedArmors[characterIndex] == snapshot.EquippedArmors[characterIndex] &&
        state.EquippedAccessories[characterIndex] == snapshot.EquippedAccessories[characterIndex] &&
        state.EquippedCostumes[characterIndex] == snapshot.EquippedCostumes[characterIndex];

    private static SaveFieldPatch CreateEquipmentPatch(
        SaveFieldDescriptor field,
        ushort weaponId,
        ushort armorId,
        ushort accessoryId,
        ushort costumeId)
    {
        byte[] bytes = new byte[field.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, sizeof(ushort)), weaponId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, sizeof(ushort)), armorId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, sizeof(ushort)), accessoryId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6, sizeof(ushort)), costumeId);
        return new SaveFieldPatch(field.Name, bytes);
    }

    private static bool PartyMembersEqual(
        IReadOnlyList<PartyMemberId> left,
        IReadOnlyList<PartyMemberId> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static bool InventoryStacksEqual(
        IReadOnlyList<InventoryStack> left,
        IReadOnlyList<InventoryStack> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static IReadOnlyList<InventoryStack> NormalizeInventoryStacks(IReadOnlyList<InventoryStack> inventoryStacks)
    {
        InventoryStack[] normalizedStacks = inventoryStacks
            .Where(static stack => InventoryItemEditability.IsWritableItemId(stack.ItemId))
            .ToArray();
        return normalizedStacks.Length == inventoryStacks.Count ? inventoryStacks : normalizedStacks;
    }

    private static bool SocialStatsEqual(
        IReadOnlyList<ushort> left,
        IReadOnlyList<ushort> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static bool SocialLinksEqual(
        IReadOnlyList<SocialLinkState> left,
        IReadOnlyList<SocialLinkState> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);

    private static int GetSocialLinkSlotCount(P4GSaveLayout layout) =>
        layout.SocialLinks.Length / SocialLinkSlotStride;

    private static bool CalendarEqual(WorkingSaveState state, SaveSnapshot snapshot) =>
        state.Day == snapshot.Day &&
        state.DayPhase == snapshot.DayPhase &&
        state.NextDay == snapshot.NextDay &&
        state.NextDayPhase == snapshot.NextDayPhase;

    private static WorkingSaveState ApplyEquipmentEdit(
        WorkingSaveState state,
        List<SaveDiagnostic> diagnostics,
        int characterId,
        ushort itemId,
        Func<int, ushort, bool> itemValidator,
        Func<WorkingSaveState, int, ushort, WorkingSaveState> apply)
    {
        if (!EquipmentItemEditability.IsSupportedEquipmentCharacterId(characterId))
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP005",
                "Equipment edit targets an unsupported character slot.",
                EquipmentDiagnosticTarget));
            return state;
        }

        if (!itemValidator(characterId, itemId))
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP006",
                "Equipment edit targets an unsupported item id.",
                EquipmentDiagnosticTarget));
            return state;
        }

        return apply(state, characterId, itemId);
    }

    private static WorkingSaveState ApplyPersonaSlotEdit(
        WorkingSaveState state,
        List<SaveDiagnostic> diagnostics,
        int slotIndex,
        PersonaSlotEdit personaSlotEdit,
        Func<WorkingSaveState, int, PersonaSlot, WorkingSaveState> apply,
        IReadOnlyList<PersonaSlot> currentSlots,
        string diagnosticTarget,
        bool allowBlankPersonaId,
        uint? maximumTotalExperience = null)
    {
        if ((uint)slotIndex >= (uint)currentSlots.Count)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP007",
                "Persona slot edit targets an unsupported slot.",
                PersonaDiagnosticTarget));
            return state;
        }

        PersonaSlot currentSlot = currentSlots[slotIndex];

        if (personaSlotEdit.SkillIds is null || personaSlotEdit.SkillIds.Count != PersonaSlot.SkillCount)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP008",
                "Persona slot edit contains an invalid skill list.",
                PersonaDiagnosticTarget));
            return state;
        }

        if (maximumTotalExperience.HasValue &&
            personaSlotEdit.TotalExperience > maximumTotalExperience.Value &&
            personaSlotEdit.TotalExperience != currentSlot.TotalExperience)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP018",
                $"Persona slot edit total experience must be at most {maximumTotalExperience.Value}.",
                PersonaDiagnosticTarget));
            return state;
        }

        if (personaSlotEdit.PersonaId == 0)
        {
            if (allowBlankPersonaId)
            {
                return apply(state, slotIndex, CreateBlankPersonaSlot(currentSlot, personaSlotEdit));
            }

            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP009",
                "Persona slot edit must specify a persona id.",
                PersonaDiagnosticTarget));
            return state;
        }

        bool personaIdChanged = currentSlot.PersonaId != personaSlotEdit.PersonaId;
        byte existsRawByte = !personaIdChanged || currentSlot.ExistsRawByte != 0
            ? currentSlot.ExistsRawByte
            : (byte)1;
        byte level = personaSlotEdit.Level == 0 && personaIdChanged && !currentSlot.Exists
            ? (byte)1
            : personaSlotEdit.Level;
        PersonaSlot updatedSlot = new(
            existsRawByte,
            currentSlot.Unknown0,
            personaSlotEdit.PersonaId,
            level,
            currentSlot.ReservedAfterLevel,
            personaSlotEdit.TotalExperience,
            personaSlotEdit.SkillIds,
            personaSlotEdit.Strength,
            personaSlotEdit.Magic,
            personaSlotEdit.Endurance,
            personaSlotEdit.Agility,
            personaSlotEdit.Luck);

        return apply(state, slotIndex, updatedSlot);
    }

    private static WorkingSaveState ApplyCompendiumClearEdit(
        WorkingSaveState state,
        List<SaveDiagnostic> diagnostics,
        int slotIndex)
    {
        if ((uint)slotIndex >= (uint)state.CompendiumPersonaSlots.Count)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP007",
                "Persona slot edit targets an unsupported slot.",
                PersonaDiagnosticTarget));
            return state;
        }

        return state.WithCompendiumPersonaSlot(slotIndex, CreateBlankPersonaSlot());
    }

    private static WorkingSaveState ApplyCompendiumClearAllEdit(WorkingSaveState state)
    {
        WorkingSaveState updatedState = state;
        for (int slotIndex = 0; slotIndex < state.CompendiumPersonaSlots.Count; slotIndex++)
        {
            updatedState = updatedState.WithCompendiumPersonaSlot(slotIndex, CreateBlankPersonaSlot());
        }

        return updatedState;
    }

    private static PersonaSlot CreateBlankPersonaSlot() =>
        new(
            exists: false,
            unknown0: 0,
            personaId: 0,
            level: 0,
            reservedAfterLevel: [0, 0, 0],
            totalExperience: 0,
            skillIds: [0, 0, 0, 0, 0, 0, 0, 0],
            strength: 0,
            magic: 0,
            endurance: 0,
            agility: 0,
            luck: 0);

    private static PersonaSlot CreateBlankPersonaSlot(PersonaSlot currentSlot, PersonaSlotEdit personaSlotEdit) =>
        new(
            existsRawByte: 0,
            currentSlot.Unknown0,
            personaId: 0,
            personaSlotEdit.Level,
            currentSlot.ReservedAfterLevel,
            personaSlotEdit.TotalExperience,
            personaSlotEdit.SkillIds,
            personaSlotEdit.Strength,
            personaSlotEdit.Magic,
            personaSlotEdit.Endurance,
            personaSlotEdit.Agility,
            personaSlotEdit.Luck);

    private static bool IsSupportedInventoryItem(P4GSaveLayout layout, ushort itemId) =>
        itemId < (ushort)layout.Inventory.Length &&
        InventoryItemEditability.IsWritableItemId(itemId);

    private static bool IsSupportedDayValue(int day) =>
        day is >= byte.MinValue and <= byte.MaxValue;
}
