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
        string familyName = DecodeSaveNameComponent(bytes.Slice(layout.FamilyNamePString.Offset, layout.FamilyNamePString.Length));
        string givenName = DecodeSaveNameComponent(bytes.Slice(layout.GivenNamePString.Offset, layout.GivenNamePString.Length));

        EquipmentArrays equipmentArrays = ReadEquipmentArrays(span, layout);
        CalendarValues calendar = ReadCalendar(span, layout.Calendar);
        SaveSnapshot snapshot = new(
            layout.Kind,
            bytes.ToArray(),
            new SaveNames(familyName, givenName),
            BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(layout.Yen.Offset, layout.Yen.Length)),
            ReadPartyMembers(span, layout),
            equipmentArrays.Weapons,
            equipmentArrays.Armors,
            equipmentArrays.Accessories,
            equipmentArrays.Costumes,
            ReadPersonaBlock(span, layout.ProtagonistPersonaSlots),
            ReadPersonaBlock(span, layout.PartyPersonaSlots),
            ReadPersonaBlock(span, layout.CompendiumPersonaSlots),
            ReadInventory(span, layout.Inventory),
            ReadSocialStats(span, layout.SocialStats),
            ReadSocialLinks(span, layout.SocialLinks),
            span[layout.MainCharacterLevel.Offset],
            BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(layout.MainCharacterTotalExperience.Offset, layout.MainCharacterTotalExperience.Length)),
            calendar.Day,
            calendar.DayPhase,
            calendar.NextDay,
            calendar.NextDayPhase);

        return new SaveOpenResult<SaveSnapshot>(snapshot, diagnostics);
    }

    private static string DecodeSaveNameComponent(ReadOnlyMemory<byte> source)
    {
        string pStringValue = SaveStringCodec.DecodePString(source);
        return pStringValue.Length == 0
            ? SaveStringCodec.DecodeJString(source)
            : pStringValue;
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
                if (TryGetPersonaBlockPatch(layout, patch.FieldName, out PersonaBlockDescriptor? personaBlock))
                {
                    ApplyPersonaBlockPatch(output, patch, personaBlock!, diagnostics);
                    continue;
                }

                if (TryGetPersonaSlotPatch(layout, patch.FieldName, out PersonaBlockDescriptor? block, out int slotIndex))
                {
                    ApplyPersonaSlotPatch(output, patch, block!, slotIndex, diagnostics);
                    continue;
                }

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

    private static void ApplyPersonaSlotPatch(
        byte[] output,
        SaveFieldPatch patch,
        PersonaBlockDescriptor block,
        int slotIndex,
        List<SaveDiagnostic> diagnostics)
    {
        if ((uint)slotIndex >= (uint)block.Count)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4G004",
                "Save field patch data is invalid for the target.",
                PatchDiagnosticTarget));
            return;
        }

        if (patch.ByteLength != PersonaSlotBinaryCodec.BinaryLength)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4G004",
                "Save field patch data is invalid for the target.",
                PatchDiagnosticTarget));
            return;
        }

        int offset = block.Offset + (slotIndex * block.Stride) + block.PersonaOffsetWithinStride;
        patch.BytesSpan.CopyTo(output.AsSpan(offset, PersonaSlotBinaryCodec.BinaryLength));
    }

    private static void ApplyPersonaBlockPatch(
        byte[] output,
        SaveFieldPatch patch,
        PersonaBlockDescriptor block,
        List<SaveDiagnostic> diagnostics)
    {
        int blockLength = block.EffectiveBlockPatchLength;
        if (patch.ByteLength != blockLength)
        {
            diagnostics.Add(new SaveDiagnostic(
                DiagnosticSeverity.Error,
                "P4G004",
                "Save field patch data is invalid for the target.",
                PatchDiagnosticTarget));
            return;
        }

        patch.BytesSpan.CopyTo(output.AsSpan(block.Offset, blockLength));
    }

    private static bool TryGetPersonaBlockPatch(
        P4GSaveLayout layout,
        string patchFieldName,
        out PersonaBlockDescriptor? block)
    {
        block = layout.PersonaBlocks.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, patchFieldName, StringComparison.Ordinal));
        return block is not null;
    }

    private static bool TryGetPersonaSlotPatch(
        P4GSaveLayout layout,
        string patchFieldName,
        out PersonaBlockDescriptor? block,
        out int slotIndex)
    {
        block = null;
        slotIndex = 0;

        int openBracketIndex = patchFieldName.LastIndexOf('[');
        if (openBracketIndex <= 0 || !patchFieldName.EndsWith(']'))
        {
            return false;
        }

        string blockName = patchFieldName[..openBracketIndex];
        string slotIndexText = patchFieldName[(openBracketIndex + 1)..^1];
        if (!int.TryParse(slotIndexText, out slotIndex))
        {
            return false;
        }

        block = layout.PersonaBlocks.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, blockName, StringComparison.Ordinal));
        return block is not null;
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

    private static ushort[] ReadSocialStats(ReadOnlySpan<byte> source, SaveFieldDescriptor field)
    {
        ushort[] socialStats = new ushort[SocialStatRules.StatCount];
        for (int index = 0; index < socialStats.Length; index++)
        {
            socialStats[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                source.Slice(field.Offset + (index * sizeof(ushort)), sizeof(ushort)));
        }

        return socialStats;
    }

    private static List<SocialLinkState> ReadSocialLinks(ReadOnlySpan<byte> source, SaveFieldDescriptor field)
    {
        List<SocialLinkState> socialLinks = [];
        for (int offset = 0; offset < field.Length; offset += 16)
        {
            byte linkId = source[field.Offset + offset];
            if (linkId == 0)
            {
                continue;
            }

            socialLinks.Add(new SocialLinkState(
                linkId,
                source[field.Offset + offset + 2],
                source[field.Offset + offset + 4],
                source[field.Offset + offset + 12]));
        }

        return socialLinks;
    }

    private static CalendarValues ReadCalendar(ReadOnlySpan<byte> source, SaveFieldDescriptor field) =>
        new(
            source[field.Offset],
            source[field.Offset + 2],
            source[field.Offset + 8],
            source[field.Offset + 10]);

    private static EquipmentArrays ReadEquipmentArrays(ReadOnlySpan<byte> source, P4GSaveLayout layout)
    {
        ushort[] weapons = new ushort[8];
        ushort[] armors = new ushort[8];
        ushort[] accessories = new ushort[8];
        ushort[] costumes = new ushort[8];
        for (int characterIndex = 0; characterIndex < 8; characterIndex++)
        {
            SaveFieldDescriptor field = GetEquipmentField(layout, characterIndex);
            weapons[characterIndex] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(field.Offset, sizeof(ushort)));
            armors[characterIndex] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(field.Offset + 2, sizeof(ushort)));
            accessories[characterIndex] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(field.Offset + 4, sizeof(ushort)));
            costumes[characterIndex] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(field.Offset + 6, sizeof(ushort)));
        }

        return new EquipmentArrays(weapons, armors, accessories, costumes);
    }

    private static SaveFieldDescriptor GetEquipmentField(P4GSaveLayout layout, int characterIndex) =>
        characterIndex == 0
            ? layout.ProtagonistEquipment
            : layout.PartyEquipmentSlots[characterIndex - 1];

    private sealed record EquipmentArrays(
        IReadOnlyList<ushort> Weapons,
        IReadOnlyList<ushort> Armors,
        IReadOnlyList<ushort> Accessories,
        IReadOnlyList<ushort> Costumes);

    private static List<InventoryStack> ReadInventory(ReadOnlySpan<byte> source, SaveFieldDescriptor field)
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

    private readonly record struct CalendarValues(byte Day, byte DayPhase, byte NextDay, byte NextDayPhase);
}
