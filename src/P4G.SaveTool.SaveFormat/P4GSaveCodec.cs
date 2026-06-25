using System.Buffers.Binary;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.SaveFormat;

public static class P4GSaveCodec
{
    private const string SaveDiagnosticTarget = "Save";
    private const string PatchDiagnosticTarget = "Patch";

    public static SaveOpenResult<SaveSnapshot> Open(
        ReadOnlyMemory<byte> bytes,
        P4GSaveLayoutKind layoutKind = P4GSaveLayoutKind.P4GGoldenVitaFixed)
    {
        P4GSaveLayout layout = P4GSaveLayout.For(layoutKind);
        List<SaveDiagnostic> diagnostics = ValidateLength(bytes.Length, layout);
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new SaveOpenResult<SaveSnapshot>(null, diagnostics);
        }

        ReadOnlySpan<byte> span = bytes.Span;
        string familyName = SaveStringCodec.DecodePString(bytes.Slice(layout.FamilyNamePString.Offset, layout.FamilyNamePString.Length));
        string givenName = SaveStringCodec.DecodePString(bytes.Slice(layout.GivenNamePString.Offset, layout.GivenNamePString.Length));
        if (familyName.Length == 0 && givenName.Length == 0)
        {
            familyName = SaveStringCodec.DecodeJString(bytes.Slice(layout.FamilyNameJString.Offset, layout.FamilyNameJString.Length));
            givenName = SaveStringCodec.DecodeJString(bytes.Slice(layout.GivenNameJString.Offset, layout.GivenNameJString.Length));
        }

        SaveSnapshot snapshot = new(
            layout.Kind,
            bytes.ToArray(),
            new SaveNames(familyName, givenName),
            BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(layout.Yen.Offset, layout.Yen.Length)),
            ReadPartyMembers(span, layout),
            ReadPersonaBlock(span, layout.ProtagonistPersonaSlots),
            ReadPersonaBlock(span, layout.PartyPersonaSlots),
            ReadPersonaBlock(span, layout.CompendiumPersonaSlots),
            ReadInventory(span, layout.Inventory));

        return new SaveOpenResult<SaveSnapshot>(snapshot, diagnostics);
    }

    public static SaveWriteResult Write(SaveSnapshot snapshot, IEnumerable<SaveFieldPatch>? patches = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        P4GSaveLayout layout = P4GSaveLayout.For(snapshot.LayoutKind);
        byte[] output = snapshot.CopyOriginalBytes();
        List<SaveDiagnostic> diagnostics = ValidateLength(output.Length, layout);
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return SaveWriteResult.Failure(diagnostics);
        }

        ApplyFieldPatches(output, layout, patches, diagnostics);
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return SaveWriteResult.Failure(diagnostics);
        }

        return SaveWriteResult.Success(output, diagnostics);
    }

    private static List<SaveDiagnostic> ValidateLength(int byteLength, P4GSaveLayout layout)
    {
        if (byteLength >= layout.MinimumLength)
        {
            return [];
        }

        return
        [
            new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4G002",
                "Save data is incomplete or uses an unsupported format.",
                SaveDiagnosticTarget),
        ];
    }

    private static void ApplyFieldPatches(
        byte[] output,
        P4GSaveLayout layout,
        IEnumerable<SaveFieldPatch>? patches,
        List<SaveDiagnostic> diagnostics)
    {
        if (patches is null)
        {
            return;
        }

        foreach (SaveFieldPatch patch in patches)
        {
            ArgumentNullException.ThrowIfNull(patch);

            SaveFieldDescriptor? field = layout.FieldRegions.FirstOrDefault(region =>
                string.Equals(region.Name, patch.FieldName, StringComparison.Ordinal));
            if (field is null)
            {
                diagnostics.Add(new SaveDiagnostic(
                    DiagnosticSeverity.Error,
                    "P4G003",
                    "Save field patch target is not supported.",
                    PatchDiagnosticTarget));
                continue;
            }

            if (patch.ByteLength != field.Length)
            {
                diagnostics.Add(new SaveDiagnostic(
                    DiagnosticSeverity.Error,
                    "P4G004",
                    "Save field patch data is invalid for the target.",
                    PatchDiagnosticTarget));
                continue;
            }

            patch.BytesSpan.CopyTo(output.AsSpan(field.Offset, field.Length));
        }
    }

    private static IReadOnlyList<PartyMemberId> ReadPartyMembers(ReadOnlySpan<byte> source, P4GSaveLayout layout)
    {
        return
        [
            new PartyMemberId(source[layout.PartyMembers.Offset]),
            new PartyMemberId(source[layout.PartyMembers.Offset + 2]),
            new PartyMemberId(source[layout.PartyMembers.Offset + 4]),
        ];
    }

    private static IReadOnlyList<InventoryStack> ReadInventory(ReadOnlySpan<byte> source, SaveFieldDescriptor field)
    {
        List<InventoryStack> stacks = [];
        for (int index = 0; index < field.Length; index++)
        {
            byte quantity = source[field.Offset + index];
            if (quantity == 0)
            {
                continue;
            }

            stacks.Add(new InventoryStack((ushort)index, quantity));
        }

        return stacks;
    }

    private static PersonaSlot[] ReadPersonaBlock(ReadOnlySpan<byte> source, PersonaBlockDescriptor block)
    {
        PersonaSlot[] slots = new PersonaSlot[block.Count];
        for (int index = 0; index < block.Count; index++)
        {
            int offset = block.Offset + (index * block.Stride) + block.PersonaOffsetWithinStride;
            slots[index] = PersonaSlotBinaryCodec.Read(source.Slice(offset, PersonaSlotBinaryCodec.BinaryLength));
        }

        return slots;
    }
}
