using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.SaveFormat;
using Xunit;
using Xunit.Abstractions;

namespace P4G.SaveTool.SaveFormat.Tests;

public sealed class P4GSaveCodecTests
{
    private const int LegacyNameByteLength = 18;
    private const int LegacyFamilyNameJStringOffset = 16;
    private const int LegacyGivenNameJStringOffset = 34;
    private const int LegacyYenOffset = 88;
    private const int LegacyYenLength = 4;
    private const int LegacyPartyMembersOffset = 92;
    private const int LegacyPartyMembersLength = 5;
    private const int LegacyFamilyNamePStringOffset = 100;
    private const int LegacyGivenNamePStringOffset = 118;
    private const int LegacyInventoryOffset = 136;
    private const int LegacyInventoryLength = 2559;
    private const int LegacyProtagonistPersonaSlotsOffset = 2700;
    private const int LegacyProtagonistPersonaSlotsCount = 12;
    private const int LegacyProtagonistPersonaSlotStride = 48;
    private const int LegacyMainCharacterLevelOffset = 3290;
    private const int LegacyMainCharacterLevelLength = 1;
    private const int LegacySocialStatsOffset = 3336;
    private const int LegacySocialStatsLength = 10;
    private const int LegacyMainCharacterTotalExperienceOffset = 3348;
    private const int LegacyMainCharacterTotalExperienceLength = 4;
    private const int LegacyPartyPersonaSlotsOffset = 3492;
    private const int LegacyPartyPersonaSlotsCount = 7;
    private const int LegacyPartyPersonaSlotStride = 132;
    private const int LegacyPartyPersonaOffsetWithinStride = 8;
    private const int LegacyCalendarOffset = 6484;
    private const int LegacyCalendarLength = 11;
    private const int LegacySocialLinksOffset = 6512;
    private const int LegacySocialLinksLength = 368;
    private const int LegacyCompendiumPersonaSlotsOffset = 9688;
    private const int LegacyCompendiumPersonaSlotsCount = 249;
    private const int LegacyCompendiumPersonaSlotStride = 48;

    private readonly ITestOutputHelper output;

    public P4GSaveCodecTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void OpenReportsDiagnosticsForShortInput()
    {
        SaveOpenResult<SaveSnapshot> result = P4GSaveCodec.Open(new byte[15]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        SaveDiagnostic diagnostic = AssertSingleErrorDiagnostic(
            result.Diagnostics,
            "P4G002",
            "Save",
            "Save data is incomplete or uses an unsupported format.");
        AssertDiagnosticDoesNotExposeDetails(diagnostic, "FamilyNameJString", "bytes", "16", "33");
    }

    [Fact]
    public void PersonaSlotOpenReportsDiagnosticForTruncatedSlot()
    {
        SaveOpenResult<PersonaSlot> result = PersonaSlotBinaryCodec.Open(new byte[PersonaSlotBinaryCodec.BinaryLength - 1]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        SaveDiagnostic diagnostic = AssertSingleErrorDiagnostic(
            result.Diagnostics,
            "P4G001",
            "PersonaSlot",
            "Persona slot data is incomplete or uses an unsupported format.");
        AssertDiagnosticDoesNotExposeDetails(
            diagnostic,
            "bytes",
            PersonaSlotBinaryCodec.BinaryLength.ToString(CultureInfo.InvariantCulture),
            (PersonaSlotBinaryCodec.BinaryLength - 1).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PersonaSlotRoundtripPreservesRawExistsByte()
    {
        byte[] input = new byte[PersonaSlotBinaryCodec.BinaryLength];
        input[0] = 0x02;
        input[1] = 0xa5;
        input[4] = 77;

        SaveOpenResult<PersonaSlot> openResult = PersonaSlotBinaryCodec.Open(input);

        Assert.True(openResult.Succeeded, FormatDiagnostics(openResult.Diagnostics));
        PersonaSlot slot = Assert.IsType<PersonaSlot>(openResult.Snapshot);
        Assert.True(slot.Exists);
        Assert.Equal((byte)0x02, slot.ExistsRawByte);
        SaveWriteResult writeResult = PersonaSlotBinaryCodec.Write(slot);
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        Assert.Equal(input, writeResult.Bytes);
    }

    [Fact]
    public void OpenParsesSyntheticSaveAndWriteWithoutPatchPreservesOriginalBytes()
    {
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);

        AssertParsedSyntheticSave(snapshot);

        SaveWriteResult result = P4GSaveCodec.Write(snapshot);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.NotNull(result.Bytes);
        Assert.NotSame(input, result.Bytes);
        Assert.Equal(input, result.Bytes);
    }

    [Fact]
    public void OpenFallsBackToLegacyJStringNamesWhenPStringNamesAreEmpty()
    {
        byte[] input = CreateSyntheticSave();
        input.AsSpan(LegacyFamilyNamePStringOffset, LegacyNameByteLength).Clear();
        input.AsSpan(LegacyGivenNamePStringOffset, LegacyNameByteLength).Clear();

        SaveSnapshot snapshot = OpenOrThrow(input);

        Assert.Equal(new SaveNames("Sato", "Yu"), snapshot.Names);
    }

    [Fact]
    public void FieldPatchChangesOnlyYenRegion()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);
        const uint expectedYen = 7654321;

        SaveWriteResult result = P4GSaveCodec.Write(snapshot, [CreateUInt32Patch(layout.Yen, expectedYen)]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(result.Bytes);
        Assert.Equal(expectedYen, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(layout.Yen.Offset, layout.Yen.Length)));
        AssertOnlyRangesChanged(input, output, (layout.Yen.Offset, layout.Yen.Length));
    }

    [Fact]
    public void FieldPatchChangesOnlyInventoryRegion()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);
        byte[] inventoryBytes = new byte[layout.Inventory.Length];
        inventoryBytes[1] = 9;
        inventoryBytes[257] = 7;
        inventoryBytes[1184] = 5;

        SaveWriteResult result = P4GSaveCodec.Write(snapshot, [new SaveFieldPatch(layout.Inventory.Name, inventoryBytes)]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(result.Bytes);
        Assert.Equal((byte)9, output[layout.Inventory.Offset + 1]);
        Assert.Equal((byte)7, output[layout.Inventory.Offset + 257]);
        Assert.Equal((byte)5, output[layout.Inventory.Offset + 1184]);
        AssertOnlyRangesChanged(input, output, (layout.Inventory.Offset, layout.Inventory.Length));
    }

    [Fact]
    public void FieldPatchesUpdateBothLegacyNameEncodings()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);
        const string expectedFamilyName = "Amagi";
        const string expectedGivenName = "Chie";
        SaveFieldPatch familyNameJStringPatch = CreateJStringPatch(layout.FamilyNameJString, expectedFamilyName);
        SaveFieldPatch givenNameJStringPatch = CreateJStringPatch(layout.GivenNameJString, expectedGivenName);
        SaveFieldPatch familyNamePStringPatch = CreatePStringPatch(layout.FamilyNamePString, expectedFamilyName);
        SaveFieldPatch givenNamePStringPatch = CreatePStringPatch(layout.GivenNamePString, expectedGivenName);

        SaveWriteResult result = P4GSaveCodec.Write(
            snapshot,
            [
                familyNameJStringPatch,
                givenNameJStringPatch,
                familyNamePStringPatch,
                givenNamePStringPatch,
            ]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(result.Bytes);
        Assert.Equal(expectedFamilyName, SaveStringCodec.DecodeJString(output.AsMemory(layout.FamilyNameJString.Offset, layout.FamilyNameJString.Length)));
        Assert.Equal(expectedGivenName, SaveStringCodec.DecodeJString(output.AsMemory(layout.GivenNameJString.Offset, layout.GivenNameJString.Length)));
        Assert.Equal(expectedFamilyName, SaveStringCodec.DecodePString(output.AsMemory(layout.FamilyNamePString.Offset, layout.FamilyNamePString.Length)));
        Assert.Equal(expectedGivenName, SaveStringCodec.DecodePString(output.AsMemory(layout.GivenNamePString.Offset, layout.GivenNamePString.Length)));
        AssertFieldBytesChangedTo(input, output, layout.GivenNameJString, givenNameJStringPatch);
        AssertFieldBytesChangedTo(input, output, layout.GivenNamePString, givenNamePStringPatch);
        AssertOnlyRangesChanged(
            input,
            output,
            (layout.FamilyNameJString.Offset, layout.FamilyNameJString.Length),
            (layout.GivenNameJString.Offset, layout.GivenNameJString.Length),
            (layout.FamilyNamePString.Offset, layout.FamilyNamePString.Length),
            (layout.GivenNamePString.Offset, layout.GivenNamePString.Length));
    }

    [Fact]
    public void WriteReportsDiagnosticForUnknownPatchTarget()
    {
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);

        SaveWriteResult result = P4GSaveCodec.Write(snapshot, [new SaveFieldPatch("NotAField", Array.Empty<byte>())]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        SaveDiagnostic diagnostic = AssertSingleErrorDiagnostic(
            result.Diagnostics,
            "P4G003",
            "Patch",
            "Save field patch target is not supported.");
        AssertDiagnosticDoesNotExposeDetails(diagnostic, "NotAField", "FamilyNameJString", "Yen", "bytes");
    }

    [Fact]
    public void WriteReportsDiagnosticForWrongLengthPatch()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);
        byte[] wrongLengthBytes = new byte[layout.Yen.Length - 1];

        SaveWriteResult result = P4GSaveCodec.Write(snapshot, [new SaveFieldPatch(layout.Yen.Name, wrongLengthBytes)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        SaveDiagnostic diagnostic = AssertSingleErrorDiagnostic(
            result.Diagnostics,
            "P4G004",
            "Patch",
            "Save field patch data is invalid for the target.");
        AssertDiagnosticDoesNotExposeDetails(
            diagnostic,
            layout.Yen.Name,
            "bytes",
            layout.Yen.Length.ToString(CultureInfo.InvariantCulture),
            wrongLengthBytes.Length.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void OriginalBytesReturnsCopyThatCannotMutateSnapshot()
    {
        byte[] input = CreateSyntheticSave();
        SaveSnapshot snapshot = OpenOrThrow(input);

        Assert.True(MemoryMarshal.TryGetArray(snapshot.OriginalBytes, out ArraySegment<byte> exposed));
        Assert.NotNull(exposed.Array);
        exposed.Array[exposed.Offset] ^= 0xff;

        SaveWriteResult result = P4GSaveCodec.Write(snapshot);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(input, result.Bytes);
        Assert.NotEqual(input[0], exposed.Array[exposed.Offset]);
    }

    [Fact]
    public void LayoutCollectionsDoNotExposeMutableBackingArrays()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);

        AssertReadOnlyListDoesNotExposeArray(layout.FieldRegions, layout.Yen);
        AssertReadOnlyListDoesNotExposeArray(layout.PersonaBlocks, layout.CompendiumPersonaSlots);
    }

    [Fact]
    public void GoldenVitaLayoutMatchesImplementedLegacyOffsetsAndSizes()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);

        AssertField(layout.FamilyNameJString, "FamilyNameJString", LegacyFamilyNameJStringOffset, LegacyNameByteLength);
        AssertField(layout.GivenNameJString, "GivenNameJString", LegacyGivenNameJStringOffset, LegacyNameByteLength);
        AssertField(layout.Yen, "Yen", LegacyYenOffset, LegacyYenLength);
        AssertField(layout.PartyMembers, "PartyMembers", LegacyPartyMembersOffset, LegacyPartyMembersLength);
        AssertField(layout.FamilyNamePString, "FamilyNamePString", LegacyFamilyNamePStringOffset, LegacyNameByteLength);
        AssertField(layout.GivenNamePString, "GivenNamePString", LegacyGivenNamePStringOffset, LegacyNameByteLength);
        AssertField(layout.Inventory, "Inventory", LegacyInventoryOffset, LegacyInventoryLength);
        AssertPersonaBlock(
            layout.ProtagonistPersonaSlots,
            "ProtagonistPersonaSlots",
            LegacyProtagonistPersonaSlotsOffset,
            LegacyProtagonistPersonaSlotsCount,
            LegacyProtagonistPersonaSlotStride,
            0);
        AssertField(
            layout.MainCharacterLevel,
            "MainCharacterLevel",
            LegacyMainCharacterLevelOffset,
            LegacyMainCharacterLevelLength);
        AssertField(layout.SocialStats, "SocialStats", LegacySocialStatsOffset, LegacySocialStatsLength);
        AssertField(
            layout.MainCharacterTotalExperience,
            "MainCharacterTotalExperience",
            LegacyMainCharacterTotalExperienceOffset,
            LegacyMainCharacterTotalExperienceLength);
        AssertPersonaBlock(
            layout.PartyPersonaSlots,
            "PartyPersonaSlots",
            LegacyPartyPersonaSlotsOffset,
            LegacyPartyPersonaSlotsCount,
            LegacyPartyPersonaSlotStride,
            LegacyPartyPersonaOffsetWithinStride);
        AssertField(layout.Calendar, "Calendar", LegacyCalendarOffset, LegacyCalendarLength);
        AssertField(layout.SocialLinks, "SocialLinks", LegacySocialLinksOffset, LegacySocialLinksLength);
        AssertPersonaBlock(
            layout.CompendiumPersonaSlots,
            "CompendiumPersonaSlots",
            LegacyCompendiumPersonaSlotsOffset,
            LegacyCompendiumPersonaSlotsCount,
            LegacyCompendiumPersonaSlotStride,
            0);
        Assert.Equal(
            LegacyCompendiumPersonaSlotsOffset
                + ((LegacyCompendiumPersonaSlotsCount - 1) * LegacyCompendiumPersonaSlotStride)
                + PersonaSlotBinaryCodec.BinaryLength,
            layout.MinimumLength);
    }

    [Fact]
    public void SnapshotCollectionsDoNotExposeMutableBackingArrays()
    {
        SaveSnapshot snapshot = OpenOrThrow(CreateSyntheticSave());

        AssertReadOnlyListDoesNotExposeArray(snapshot.PartyMembers, new PartyMemberId(0x42));
        AssertReadOnlyListDoesNotExposeArray(snapshot.ProtagonistPersonaSlots, snapshot.CompendiumPersonaSlots[0]);
        AssertReadOnlyListDoesNotExposeArray(snapshot.PartyPersonaSlots, snapshot.CompendiumPersonaSlots[0]);
        AssertReadOnlyListDoesNotExposeArray(snapshot.CompendiumPersonaSlots, snapshot.ProtagonistPersonaSlots[0]);
    }

    [Fact]
    public void OpenResultDefensivelyCopiesDiagnosticsAndFreezesSucceeded()
    {
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "TEST000", "warning", "Target");
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "TEST001", "error", "Target");
        List<SaveDiagnostic> diagnostics = [warning];
        SaveOpenResult<string> result = new("snapshot", diagnostics);

        diagnostics.Add(error);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { warning }, result.Diagnostics);
        AssertReadOnlyListDoesNotExposeArray(result.Diagnostics, error);
    }

    [Fact]
    public void WriteResultDefensivelyCopiesCollectionsAndFreezesSucceeded()
    {
        byte[] bytes = [0x01, 0x02, 0x03];
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "TEST000", "warning", "Target");
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "TEST001", "error", "Target");
        List<SaveDiagnostic> diagnostics = [warning];
        SaveWriteResult result = SaveWriteResult.Success(bytes, diagnostics);

        bytes[0] = 0xff;
        diagnostics.Add(error);
        byte[] exposedBytes = Assert.IsType<byte[]>(result.Bytes);
        exposedBytes[0] = 0xee;

        Assert.True(result.Succeeded);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, Assert.IsType<byte[]>(result.Bytes));
        Assert.Equal(new[] { warning }, result.Diagnostics);
        AssertReadOnlyListDoesNotExposeArray(result.Diagnostics, error);
    }

    [Fact]
    public void PersonaSlotDefensivelyCopiesCallerCollectionsBeforeSerialization()
    {
        byte[] reservedAfterLevel = [0xc1, 0xc2, 0xc3];
        ushort[] skillIds = [0x1001, 0x1002, 0x1003, 0x1004, 0x1005, 0x1006, 0x1007, 0x1008];
        PersonaSlot slot = new(
            true,
            0xa5,
            0xf123,
            77,
            reservedAfterLevel,
            0x01020304,
            skillIds,
            11,
            22,
            33,
            44,
            55);
        byte[] expectedBytes = Assert.IsType<byte[]>(PersonaSlotBinaryCodec.Write(slot).Bytes);

        reservedAfterLevel[0] = 0xff;
        skillIds[0] = 0xffff;

        SaveWriteResult result = PersonaSlotBinaryCodec.Write(slot);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(expectedBytes, result.Bytes);
        Assert.Equal((byte)0xc1, slot.ReservedAfterLevel[0]);
        Assert.Equal((ushort)0x1001, slot.SkillIds[0]);
    }

    [Fact]
    public void PersonaSlotRejectsIncorrectFixedLengthCollections()
    {
        Assert.Throws<ArgumentException>(() => CreatePersonaSlot([0xc1, 0xc2], CreateSkillIds(0x1000)));
        Assert.Throws<ArgumentException>(() => CreatePersonaSlot([0xc1, 0xc2, 0xc3], [0x1001]));
    }

    [PrivateFixtureFact]
    public void PrivateFixturesRoundtripWhenPresent()
    {
        PrivateFixture[] fixtures = PrivateFixtureDiscovery.GetUsablePrivateFixtures();
        Assert.NotEmpty(fixtures);
        output.WriteLine($"Private fixture regression is exercising {fixtures.Length} manifest-backed save file(s).");

        foreach (PrivateFixture fixture in fixtures)
        {
            byte[] bytes = File.ReadAllBytes(fixture.FullPath);
            SaveSnapshot snapshot = OpenOrThrow(bytes);
            AssertPrivateFixtureExpectations(fixture, snapshot);
            SaveWriteResult result = P4GSaveCodec.Write(snapshot);

            Assert.True(result.Succeeded, $"{fixture.FullPath}:{Environment.NewLine}{FormatDiagnostics(result.Diagnostics)}");
            Assert.Equal(bytes, result.Bytes);
        }
    }

    private static SaveSnapshot OpenOrThrow(byte[] input)
    {
        SaveOpenResult<SaveSnapshot> result = P4GSaveCodec.Open(input);
        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        return Assert.IsType<SaveSnapshot>(result.Snapshot);
    }

    private static SaveDiagnostic AssertSingleErrorDiagnostic(
        IReadOnlyList<SaveDiagnostic> diagnostics,
        string code,
        string target,
        string message)
    {
        SaveDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(target, diagnostic.Target);
        Assert.Equal(message, diagnostic.Message);
        return diagnostic;
    }

    private static void AssertDiagnosticDoesNotExposeDetails(SaveDiagnostic diagnostic, params string[] disallowedFragments)
    {
        foreach (string fragment in disallowedFragments)
        {
            Assert.False(
                diagnostic.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"Diagnostic message exposed '{fragment}': {diagnostic.Message}");
            Assert.False(
                diagnostic.Target?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true,
                $"Diagnostic target exposed '{fragment}': {diagnostic.Target}");
        }
    }

    private static void AssertFieldBytesChangedTo(
        byte[] original,
        byte[] actual,
        SaveFieldDescriptor field,
        SaveFieldPatch expectedPatch)
    {
        byte[] originalFieldBytes = original.AsSpan(field.Offset, field.Length).ToArray();
        byte[] actualFieldBytes = actual.AsSpan(field.Offset, field.Length).ToArray();

        Assert.NotEqual(originalFieldBytes, actualFieldBytes);
        Assert.Equal(expectedPatch.Bytes.ToArray(), actualFieldBytes);
    }

    private static void AssertPrivateFixtureExpectations(PrivateFixture fixture, SaveSnapshot snapshot)
    {
        Assert.Equal(fixture.ExpectedNames, snapshot.Names);
        Assert.Equal(fixture.ExpectedYen, snapshot.Yen);
        Assert.Equal(fixture.ExpectedPartyMembers, snapshot.PartyMembers.Select(static member => member.Value).ToArray());
    }

    private static void AssertField(SaveFieldDescriptor field, string name, int offset, int length)
    {
        Assert.Equal(name, field.Name);
        Assert.Equal(offset, field.Offset);
        Assert.Equal(length, field.Length);
    }

    private static void AssertPersonaBlock(
        PersonaBlockDescriptor block,
        string name,
        int offset,
        int count,
        int stride,
        int personaOffsetWithinStride)
    {
        Assert.Equal(name, block.Name);
        Assert.Equal(offset, block.Offset);
        Assert.Equal(count, block.Count);
        Assert.Equal(stride, block.Stride);
        Assert.Equal(personaOffsetWithinStride, block.PersonaOffsetWithinStride);
    }

    private static void AssertReadOnlyListDoesNotExposeArray<T>(IReadOnlyList<T> collection, T replacement)
    {
        Assert.False(collection.GetType().IsArray, $"Collection exposes mutable array type {collection.GetType()}.");
        if (collection is IList<T> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.NotEmpty(list);
            Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        }
    }

    private static void AssertParsedSyntheticSave(SaveSnapshot snapshot)
    {
        Assert.Equal(P4GSaveLayoutKind.P4GGoldenVitaFixed, snapshot.LayoutKind);
        Assert.Equal(new SaveNames("Sato", "Yu"), snapshot.Names);
        Assert.Equal(123456u, snapshot.Yen);
        Assert.Collection(
            snapshot.PartyMembers,
            static member => Assert.Equal((byte)0x01, member.Value),
            static member => Assert.Equal((byte)0xfe, member.Value),
            static member => Assert.Equal((byte)0x80, member.Value));
        Assert.Collection(
            snapshot.InventoryStacks,
            static stack =>
            {
                Assert.Equal((ushort)1, stack.ItemId);
                Assert.Equal((byte)2, stack.Quantity);
            },
            static stack =>
            {
                Assert.Equal((ushort)257, stack.ItemId);
                Assert.Equal((byte)3, stack.Quantity);
            },
            static stack =>
            {
                Assert.Equal((ushort)1184, stack.ItemId);
                Assert.Equal((byte)4, stack.Quantity);
            },
            static stack =>
            {
                Assert.Equal((ushort)2056, stack.ItemId);
                Assert.Equal((byte)5, stack.Quantity);
            });
        AssertPersonaSlot(
            snapshot.ProtagonistPersonaSlots[0],
            true,
            0xa5,
            0xf123,
            77,
            [0xc1, 0xc2, 0xc3],
            0x01020304,
            [0x1001, 0x1002, 0x1003, 0x1004, 0x1005, 0x1006, 0x1007, 0x1008],
            11,
            22,
            33,
            44,
            55);
        AssertPersonaSlot(
            snapshot.PartyPersonaSlots[0],
            true,
            0x5a,
            0x0bad,
            65,
            [0xd1, 0xd2, 0xd3],
            0xa0b0c0d0,
            [0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008],
            12,
            23,
            34,
            45,
            56);
        AssertPersonaSlot(
            snapshot.CompendiumPersonaSlots[0],
            false,
            0xee,
            0xffff,
            99,
            [0xe1, 0xe2, 0xe3],
            0xffffffff,
            [0x0000, 0xffff, 0xf00d, 0x3004, 0x3005, 0x3006, 0x3007, 0x3008],
            1,
            2,
            3,
            4,
            5);
        AssertReadOnlyListDoesNotExposeArray(snapshot.InventoryStacks, new InventoryStack(1, 0));
        Assert.Equal(
            new[]
            {
                new InventoryStack(1, 2),
                new InventoryStack(257, 3),
                new InventoryStack(1184, 4),
                new InventoryStack(2056, 5),
            },
            snapshot.InventoryStacks);
    }

    private static byte[] CreateSyntheticSave()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] bytes = new byte[layout.MinimumLength + 32];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index * 37);
        }

        SaveStringCodec.EncodeJString("Sato", bytes.AsMemory(LegacyFamilyNameJStringOffset, LegacyNameByteLength));
        SaveStringCodec.EncodeJString("Yu", bytes.AsMemory(LegacyGivenNameJStringOffset, LegacyNameByteLength));
        SaveStringCodec.EncodePString("Sato", bytes.AsMemory(LegacyFamilyNamePStringOffset, LegacyNameByteLength));
        SaveStringCodec.EncodePString("Yu", bytes.AsMemory(LegacyGivenNamePStringOffset, LegacyNameByteLength));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(LegacyYenOffset, LegacyYenLength), 123456);
        bytes[LegacyPartyMembersOffset] = 0x01;
        bytes[LegacyPartyMembersOffset + 2] = 0xfe;
        bytes[LegacyPartyMembersOffset + 4] = 0x80;
        bytes.AsSpan(LegacyInventoryOffset, LegacyInventoryLength).Clear();
        bytes[LegacyInventoryOffset + 1] = 2;
        bytes[LegacyInventoryOffset + 257] = 3;
        bytes[LegacyInventoryOffset + 1184] = 4;
        bytes[LegacyInventoryOffset + 2056] = 5;
        WritePersonaSlotBytes(
            bytes.AsSpan(LegacyProtagonistPersonaSlotsOffset, PersonaSlotBinaryCodec.BinaryLength),
            true,
            0xa5,
            0xf123,
            77,
            [0xc1, 0xc2, 0xc3],
            0x01020304,
            [0x1001, 0x1002, 0x1003, 0x1004, 0x1005, 0x1006, 0x1007, 0x1008],
            11,
            22,
            33,
            44,
            55);
        WritePersonaSlotBytes(
            bytes.AsSpan(
                LegacyPartyPersonaSlotsOffset + LegacyPartyPersonaOffsetWithinStride,
                PersonaSlotBinaryCodec.BinaryLength),
            true,
            0x5a,
            0x0bad,
            65,
            [0xd1, 0xd2, 0xd3],
            0xa0b0c0d0,
            [0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008],
            12,
            23,
            34,
            45,
            56);
        WritePersonaSlotBytes(
            bytes.AsSpan(LegacyCompendiumPersonaSlotsOffset, PersonaSlotBinaryCodec.BinaryLength),
            false,
            0xee,
            0xffff,
            99,
            [0xe1, 0xe2, 0xe3],
            0xffffffff,
            [0x0000, 0xffff, 0xf00d, 0x3004, 0x3005, 0x3006, 0x3007, 0x3008],
            1,
            2,
            3,
            4,
            5);

        return bytes;
    }

    private static PersonaSlot CreatePersonaSlot(IReadOnlyList<byte> reservedAfterLevel, IReadOnlyList<ushort> skillIds) =>
        new(
            true,
            0xa5,
            0xf123,
            77,
            reservedAfterLevel,
            0x01020304,
            skillIds,
            11,
            22,
            33,
            44,
            55);

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

    private static ushort[] CreateSkillIds(ushort first)
    {
        ushort[] skillIds = new ushort[PersonaSlot.SkillCount];
        for (int index = 0; index < skillIds.Length; index++)
        {
            skillIds[index] = (ushort)(first + index);
        }

        return skillIds;
    }

    private static void AssertPersonaSlot(
        PersonaSlot slot,
        bool exists,
        byte unknown0,
        ushort personaId,
        byte level,
        IReadOnlyList<byte> reservedAfterLevel,
        uint totalExperience,
        IReadOnlyList<ushort> skillIds,
        byte strength,
        byte magic,
        byte endurance,
        byte agility,
        byte luck)
    {
        Assert.Equal(exists, slot.Exists);
        Assert.Equal(exists ? (byte)1 : (byte)0, slot.ExistsRawByte);
        Assert.Equal(unknown0, slot.Unknown0);
        Assert.Equal(personaId, slot.PersonaId);
        Assert.Equal(level, slot.Level);
        Assert.Equal(reservedAfterLevel, slot.ReservedAfterLevel);
        Assert.Equal(totalExperience, slot.TotalExperience);
        Assert.Equal(skillIds, slot.SkillIds);
        Assert.Equal(strength, slot.Strength);
        Assert.Equal(magic, slot.Magic);
        Assert.Equal(endurance, slot.Endurance);
        Assert.Equal(agility, slot.Agility);
        Assert.Equal(luck, slot.Luck);
    }

    private static void WritePersonaSlotBytes(
        Span<byte> destination,
        bool exists,
        byte unknown0,
        ushort personaId,
        byte level,
        IReadOnlyList<byte> reservedAfterLevel,
        uint totalExperience,
        IReadOnlyList<ushort> skillIds,
        byte strength,
        byte magic,
        byte endurance,
        byte agility,
        byte luck)
    {
        destination[..PersonaSlotBinaryCodec.BinaryLength].Clear();
        destination[0] = exists ? (byte)1 : (byte)0;
        destination[1] = unknown0;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2, sizeof(ushort)), personaId);
        destination[4] = level;
        for (int index = 0; index < PersonaSlot.ReservedAfterLevelLength; index++)
        {
            destination[5 + index] = reservedAfterLevel[index];
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, sizeof(uint)), totalExperience);
        for (int index = 0; index < PersonaSlot.SkillCount; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination.Slice(12 + (index * sizeof(ushort)), sizeof(ushort)),
                skillIds[index]);
        }

        destination[28] = strength;
        destination[29] = magic;
        destination[30] = endurance;
        destination[31] = agility;
        destination[32] = luck;
    }

    private static void AssertOnlyRangesChanged(
        byte[] expectedOriginal,
        byte[] actual,
        params (int Offset, int Length)[] changedRanges)
    {
        Assert.Equal(expectedOriginal.Length, actual.Length);
        for (int index = 0; index < expectedOriginal.Length; index++)
        {
            bool shouldDiffer = changedRanges.Any(range => index >= range.Offset && index < range.Offset + range.Length);
            if (shouldDiffer)
            {
                continue;
            }

            Assert.Equal(expectedOriginal[index], actual[index]);
        }
    }

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.Message));
}

internal sealed class PrivateFixtureFactAttribute : FactAttribute
{
    public PrivateFixtureFactAttribute()
    {
        string? skipReason = PrivateFixtureDiscovery.GetSkipReason();
        if (skipReason is not null)
        {
            Skip = skipReason;
        }
    }
}

internal sealed record PrivateFixture(
    string FullPath,
    SaveNames ExpectedNames,
    uint ExpectedYen,
    IReadOnlyList<byte> ExpectedPartyMembers);

internal static class PrivateFixtureDiscovery
{
    private const string ManifestFileName = "manifest.json";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string GetPrivateFixtureRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "P4G.SaveTool.sln")))
            {
                return Path.Combine(directory.FullName, "tests", "fixtures", "private");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "tests", "fixtures", "private");
    }

    public static string? GetSkipReason()
    {
        string fixtureRoot = GetPrivateFixtureRoot();
        if (!Directory.Exists(fixtureRoot))
        {
            return $"Private fixture directory was not found: {fixtureRoot}";
        }

        string manifestPath = GetPrivateFixtureManifestPath();
        if (!File.Exists(manifestPath))
        {
            return $"Private fixture manifest was not found: {manifestPath}";
        }

        if (GetUsablePrivateFixtures().Length == 0)
        {
            return $"No manifest-backed usable private save fixtures were found under: {fixtureRoot}";
        }

        return null;
    }

    public static PrivateFixture[] GetUsablePrivateFixtures()
    {
        string fixtureRoot = GetPrivateFixtureRoot();
        if (!Directory.Exists(fixtureRoot) ||
            !TryReadManifest(out PrivateFixtureManifest? manifest) ||
            manifest is null)
        {
            return [];
        }

        string fixtureRootFullPath = Path.GetFullPath(fixtureRoot);
        long minimumLength = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed).MinimumLength;
        return (manifest.Fixtures ?? [])
            .Select(entry => TryCreateFixture(fixtureRootFullPath, minimumLength, entry))
            .OfType<PrivateFixture>()
            .OrderBy(static fixture => fixture.FullPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetPrivateFixtureManifestPath() =>
        Path.Combine(GetPrivateFixtureRoot(), ManifestFileName);

    private static bool TryReadManifest(out PrivateFixtureManifest? manifest)
    {
        try
        {
            string json = File.ReadAllText(GetPrivateFixtureManifestPath());
            manifest = JsonSerializer.Deserialize<PrivateFixtureManifest>(json, ManifestJsonOptions);
            return manifest is not null;
        }
        catch (IOException)
        {
            manifest = null;
            return false;
        }
        catch (JsonException)
        {
            manifest = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            manifest = null;
            return false;
        }
    }

    private static PrivateFixture? TryCreateFixture(
        string fixtureRootFullPath,
        long minimumLength,
        PrivateFixtureManifestEntry entry)
    {
        if (entry.Path is null ||
            string.IsNullOrWhiteSpace(entry.FamilyName) ||
            string.IsNullOrWhiteSpace(entry.GivenName) ||
            entry.Yen is null ||
            entry.PartyMembers is not { Length: 3 } partyMembers)
        {
            return null;
        }

        if (Path.IsPathRooted(entry.Path))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(Path.Combine(fixtureRootFullPath, entry.Path));
        if (!IsUnderDirectory(fullPath, fixtureRootFullPath) ||
            !File.Exists(fullPath) ||
            new FileInfo(fullPath).Length < minimumLength)
        {
            return null;
        }

        return new PrivateFixture(
            fullPath,
            new SaveNames(entry.FamilyName, entry.GivenName),
            entry.Yen.Value,
            Array.AsReadOnly(partyMembers.ToArray()));
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        string normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PrivateFixtureManifest
    {
        public PrivateFixtureManifestEntry[]? Fixtures { get; set; } = [];
    }

    private sealed class PrivateFixtureManifestEntry
    {
        public string? Path { get; set; }

        public string? FamilyName { get; set; }

        public string? GivenName { get; set; }

        public uint? Yen { get; set; }

        public byte[]? PartyMembers { get; set; }
    }
}
