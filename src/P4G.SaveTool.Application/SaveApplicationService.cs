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

        foreach (SaveEditCommand? edit in edits)
        {
            updatedState = ApplyEdit(updatedState, edit, diagnostics);
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
        SaveEditCommand? edit,
        List<SaveDiagnostic> diagnostics)
    {
        switch (edit)
        {
            case SetSaveNamesEdit setNames:
                if (!AreSupportedNames(setNames.Names))
                {
                    diagnostics.Add(new SaveDiagnostic(
                        DiagnosticSeverity.Error,
                        "P4GAPP002",
                        "Save names contain unsupported text.",
                        NamesDiagnosticTarget));
                    return state;
                }

                return state.WithNames(setNames.Names);

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

                return state.WithPartyMember(setPartyMember.SlotIndex, setPartyMember.MemberId);

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
            snapshot.ProtagonistPersonaSlots,
            snapshot.PartyPersonaSlots,
            snapshot.CompendiumPersonaSlots);

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

    private static bool PartyMembersEqual(
        IReadOnlyList<PartyMemberId> left,
        IReadOnlyList<PartyMemberId> right) =>
        left.Count == right.Count &&
        left.SequenceEqual(right);
}
