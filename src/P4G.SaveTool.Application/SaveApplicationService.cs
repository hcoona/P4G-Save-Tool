using System.Buffers.Binary;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.SaveFormat;

namespace P4G.SaveTool.Application;

public sealed class SaveApplicationService : ISaveApplicationService
{
    private const int PartyMemberCount = 3;
    private const string EditDiagnosticTarget = "Edit";
    private const string NamesDiagnosticTarget = "Names";
    private const string PartyMembersDiagnosticTarget = "PartyMembers";
    private const string InventoryDiagnosticTarget = "Inventory";
    private const string EquipmentDiagnosticTarget = "Equipment";
    private const string PersonaDiagnosticTarget = "Persona";

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
                    "ProtagonistPersonaSlots");

            case SetPartyPersonaSlotEdit setPartyPersonaSlot:
                return ApplyPersonaSlotEdit(
                    state,
                    diagnostics,
                    setPartyPersonaSlot.SlotIndex,
                    setPartyPersonaSlot.PersonaSlot,
                    static (currentState, slotIndex, personaSlot) => currentState.WithPartyPersonaSlot(slotIndex, personaSlot),
                    state.PartyPersonaSlots,
                    "PartyPersonaSlots");

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

    private static bool IsSupportedNameComponent(string? value)
    {
        if (value is null || value.Length > SaveStringCodec.EncodedNameCharacterLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is < '!' or > '~')
            {
                return false;
            }
        }

        return true;
    }

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
            snapshot.InventoryStacks);

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

        if (!PartyMembersEqual(state.PartyMembers, snapshot.PartyMembers))
        {
            patches.Add(CreatePartyMembersPatch(snapshot, layout.PartyMembers, state.PartyMembers));
        }

        AddEquipmentPatches(patches, layout, state, snapshot);

        if (!InventoryStacksEqual(state.InventoryStacks, snapshot.InventoryStacks))
        {
            patches.Add(CreateInventoryPatch(layout.Inventory, state.InventoryStacks));
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

        return patches;
    }

    private static SaveFieldPatch CreateUInt32Patch(SaveFieldDescriptor field, uint value)
    {
        byte[] bytes = new byte[field.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return new SaveFieldPatch(field.Name, bytes);
    }

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
        foreach (InventoryStack stack in inventoryStacks)
        {
            bytes[stack.ItemId] = stack.Quantity;
        }

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
        string diagnosticTarget)
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

        if (personaSlotEdit.SkillIds is null || personaSlotEdit.SkillIds.Count != PersonaSlot.SkillCount)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP008",
                "Persona slot edit contains an invalid skill list.",
                PersonaDiagnosticTarget));
            return state;
        }

        if (personaSlotEdit.PersonaId == 0)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4GAPP009",
                "Persona slot edit must specify a persona id.",
                PersonaDiagnosticTarget));
            return state;
        }

        PersonaSlot currentSlot = currentSlots[slotIndex];
        byte existsRawByte = currentSlot.ExistsRawByte != 0
            ? currentSlot.ExistsRawByte
            : (byte)1;
        byte level = personaSlotEdit.Level == 0
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

    private static bool IsSupportedInventoryItem(P4GSaveLayout layout, ushort itemId) =>
        itemId < (ushort)layout.Inventory.Length &&
        InventoryItemEditability.IsWritableItemId(itemId);
}
