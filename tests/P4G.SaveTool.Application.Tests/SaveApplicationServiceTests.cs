using System.Buffers.Binary;
using P4G.SaveTool.Application;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.SaveFormat;
using Xunit;

namespace P4G.SaveTool.Application.Tests;

public sealed class SaveApplicationServiceTests
{
    private const int LegacyNameByteLength = 18;
    private const int LegacyFamilyNameJStringOffset = 16;
    private const int LegacyGivenNameJStringOffset = 34;
    private const int LegacyYenOffset = 88;
    private const int LegacyYenLength = 4;
    private const int LegacyPartyMembersOffset = 92;
    private const int LegacyFamilyNamePStringOffset = 100;
    private const int LegacyGivenNamePStringOffset = 118;
    private const int LegacyInventoryOffset = 136;
    private const int LegacyInventoryLength = 2559;
    private const int LegacyProtagonistEquipmentOffset = 3360;
    private const int LegacyPartyEquipmentOffset = 3492;
    private const int LegacyPartyEquipmentStride = 132;
    private const int LegacyProtagonistPersonaSlotCount = 12;
    private const int LegacyPartyPersonaSlotCount = 7;
    private const int LegacyCompendiumPersonaSlotCount = 249;
    private static readonly PersonaSlotSentinel ProtagonistPersonaSlot0 = new(0x1111, 0x11, 0x10101010, 0x2101);
    private static readonly PersonaSlotSentinel ProtagonistPersonaSlot1 = new(0x1112, 0x12, 0x11111111, 0x2111, 0x02);
    private static readonly PersonaSlotSentinel PartyPersonaSlot0 = new(0x2221, 0x21, 0x20202020, 0x2201);
    private static readonly PersonaSlotSentinel PartyPersonaSlot1 = new(0x2222, 0x22, 0x22222222, 0x2211);
    private static readonly PersonaSlotSentinel CompendiumPersonaSlot0 = new(0x3331, 0x31, 0x30303030, 0x2301);
    private static readonly PersonaSlotSentinel CompendiumPersonaSlot1 = new(0x3332, 0x32, 0x33333333, 0x2311);

    [Fact]
    public void OpenProducesWorkingSaveStateWithoutExposingSaveFormat()
    {
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();

        SaveOpenResult<WorkingSave> result = ((ISaveApplicationService)service).Open(input);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        WorkingSave save = Assert.IsAssignableFrom<WorkingSave>(result.Snapshot);
        Assert.Equal(new SaveNames("Sato", "Yu"), save.State.Names);
        Assert.Equal(123456u, save.State.Yen);
        Assert.Collection(
            save.State.PartyMembers,
            static member => Assert.Equal((byte)0x01, member.Value),
            static member => Assert.Equal((byte)0xfe, member.Value),
            static member => Assert.Equal((byte)0x80, member.Value));
        Assert.Equal(new ushort[] { 1, 39, 112, 150, 183, 217, 2305, 2434 }, save.State.EquippedWeapons);
        Assert.Equal(new ushort[] { 256, 266, 287, 293, 307, 315, 328, 334 }, save.State.EquippedArmors);
        Assert.Equal(new ushort[] { 512, 615, 685, 687, 754, 512, 615, 754 }, save.State.EquippedAccessories);
        Assert.Equal(new ushort[] { 1792, 2040, 1792, 2040, 1792, 2040, 1792, 2040 }, save.State.EquippedCostumes);
        Assert.Collection(
            save.State.InventoryStacks,
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
        AssertPersonaSlots(
            save.State.ProtagonistPersonaSlots,
            LegacyProtagonistPersonaSlotCount,
            ProtagonistPersonaSlot0,
            ProtagonistPersonaSlot1);
        AssertPersonaSlots(
            save.State.PartyPersonaSlots,
            LegacyPartyPersonaSlotCount,
            PartyPersonaSlot0,
            PartyPersonaSlot1);
        AssertPersonaSlots(
            save.State.CompendiumPersonaSlots,
            LegacyCompendiumPersonaSlotCount,
            CompendiumPersonaSlot0,
            CompendiumPersonaSlot1);
        AssertReadOnlyListDoesNotExposeArray(save.State.InventoryStacks, new InventoryStack(0, 0));
        Assert.DoesNotContain(
            typeof(WorkingSave).GetProperties().Select(static property => property.PropertyType.Namespace),
            static namespaceName => string.Equals(namespaceName, typeof(SaveSnapshot).Namespace, StringComparison.Ordinal));
        Assert.Same(typeof(P4G.SaveTool.Contracts.AssemblyMarker).Assembly, typeof(ISaveApplicationService).Assembly);
        Assert.Same(typeof(P4G.SaveTool.Contracts.AssemblyMarker).Assembly, typeof(WorkingSave).Assembly);
        Assert.DoesNotContain(
            typeof(ISaveApplicationService).Assembly.GetReferencedAssemblies(),
            static assembly => string.Equals(assembly.Name, "P4G.SaveTool.SaveFormat", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyEditsAndWritePatchesSupportedFieldsOnly()
    {
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);
        SaveEditCommand[] edits =
        [
            new SetSaveNamesEdit("Amagi", "Chie"),
            new SetYenEdit(7654321),
            new SetPartyMemberEdit(1, 0x07),
            new SetEquippedWeaponEdit(1, 2435),
            new SetEquippedArmorEdit(0, 257),
            new SetEquippedCostumeEdit(3, 1792),
            new SetInventoryItemQuantityEdit(257, 9),
        ];

        SaveEditResult<WorkingSave> editResult = service.ApplyEdits(save, edits);
        SaveWriteResult writeResult = service.Write(Assert.IsAssignableFrom<WorkingSave>(editResult.Save));

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(writeResult.Bytes);
        Assert.Equal("Amagi", SaveStringCodec.DecodeJString(output.AsMemory(LegacyFamilyNameJStringOffset, LegacyNameByteLength)));
        Assert.Equal("Chie", SaveStringCodec.DecodeJString(output.AsMemory(LegacyGivenNameJStringOffset, LegacyNameByteLength)));
        Assert.Equal("Amagi", SaveStringCodec.DecodePString(output.AsMemory(LegacyFamilyNamePStringOffset, LegacyNameByteLength)));
        Assert.Equal("Chie", SaveStringCodec.DecodePString(output.AsMemory(LegacyGivenNamePStringOffset, LegacyNameByteLength)));
        Assert.Equal(7654321u, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(LegacyYenOffset, LegacyYenLength)));
        Assert.Equal((byte)0x01, output[LegacyPartyMembersOffset]);
        Assert.Equal((byte)0x07, output[LegacyPartyMembersOffset + 2]);
        Assert.Equal((byte)0x80, output[LegacyPartyMembersOffset + 4]);
        Assert.Equal((ushort)257, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(LegacyProtagonistEquipmentOffset + 2, sizeof(ushort))));
        Assert.Equal((ushort)2435, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(LegacyPartyEquipmentOffset, sizeof(ushort))));
        Assert.Equal((ushort)1792, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(LegacyPartyEquipmentOffset + (2 * LegacyPartyEquipmentStride) + 6, sizeof(ushort))));
        Assert.Equal((byte)9, output[LegacyInventoryOffset + 257]);
        AssertOnlyRangesChanged(
            input,
            output,
            (LegacyFamilyNameJStringOffset, LegacyNameByteLength),
            (LegacyGivenNameJStringOffset, LegacyNameByteLength),
            (LegacyYenOffset, LegacyYenLength),
            (LegacyPartyMembersOffset + 2, 1),
            (LegacyProtagonistEquipmentOffset, 8),
            (LegacyPartyEquipmentOffset, 8),
            (LegacyPartyEquipmentOffset + (2 * LegacyPartyEquipmentStride), 8),
            (LegacyFamilyNamePStringOffset, LegacyNameByteLength),
            (LegacyGivenNamePStringOffset, LegacyNameByteLength),
            (LegacyInventoryOffset + 257, 1));

        WorkingSave reopenedSave = OpenOrThrow(service, output);
        Assert.Equal(new SaveNames("Amagi", "Chie"), reopenedSave.State.Names);
        Assert.Equal(7654321u, reopenedSave.State.Yen);
        Assert.Equal(new PartyMemberId(0x07), reopenedSave.State.PartyMembers[1]);
        Assert.Equal(new InventoryStack(257, 9), reopenedSave.State.InventoryStacks.Single(stack => stack.ItemId == 257));
    }

    [Fact]
    public void ApplyEditsAppendsNewInventoryItemsToVisibleOrder()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetInventoryItemQuantityEdit(2, 9)]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        WorkingSave editedSave = Assert.IsAssignableFrom<WorkingSave>(result.Save);
        Assert.Equal(
            new[]
            {
                new InventoryStack(1, 2),
                new InventoryStack(257, 3),
                new InventoryStack(1184, 4),
                new InventoryStack(2056, 5),
                new InventoryStack(2, 9),
            },
            editedSave.State.InventoryStacks);
    }

    [Fact]
    public void ApplyEditsUsesLastCommandForRepeatedFields()
    {
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);
        SaveEditCommand[] edits =
        [
            new SetSaveNamesEdit("Amagi", "Chie"),
            new SetYenEdit(7654321),
            new SetPartyMemberEdit(1, 0x07),
            new SetSaveNamesEdit("Dojima", "Nanako"),
            new SetYenEdit(9999999),
            new SetPartyMemberEdit(1, 0x05),
            new SetInventoryItemQuantityEdit(257, 7),
            new SetInventoryItemQuantityEdit(257, 9),
        ];

        SaveEditResult<WorkingSave> editResult = service.ApplyEdits(save, edits);
        WorkingSave editedSave = Assert.IsAssignableFrom<WorkingSave>(editResult.Save);
        SaveWriteResult writeResult = service.Write(editedSave);

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        Assert.Equal(new SaveNames("Dojima", "Nanako"), editedSave.State.Names);
        Assert.Equal(9999999u, editedSave.State.Yen);
        Assert.Equal(new PartyMemberId(0x05), editedSave.State.PartyMembers[1]);
        Assert.Equal(new InventoryStack(257, 9), editedSave.State.InventoryStacks.Single(stack => stack.ItemId == 257));
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(writeResult.Bytes);
        Assert.Equal("Dojima", SaveStringCodec.DecodeJString(output.AsMemory(LegacyFamilyNameJStringOffset, LegacyNameByteLength)));
        Assert.Equal("Nanako", SaveStringCodec.DecodeJString(output.AsMemory(LegacyGivenNameJStringOffset, LegacyNameByteLength)));
        Assert.Equal("Dojima", SaveStringCodec.DecodePString(output.AsMemory(LegacyFamilyNamePStringOffset, LegacyNameByteLength)));
        Assert.Equal("Nanako", SaveStringCodec.DecodePString(output.AsMemory(LegacyGivenNamePStringOffset, LegacyNameByteLength)));
        Assert.Equal(9999999u, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(LegacyYenOffset, LegacyYenLength)));
        Assert.Equal((byte)0x01, output[LegacyPartyMembersOffset]);
        Assert.Equal((byte)0x05, output[LegacyPartyMembersOffset + 2]);
        Assert.Equal((byte)0x80, output[LegacyPartyMembersOffset + 4]);
        Assert.Equal((byte)9, output[LegacyInventoryOffset + 257]);
        AssertOnlyRangesChanged(
            input,
            output,
            (LegacyFamilyNameJStringOffset, LegacyNameByteLength),
            (LegacyGivenNameJStringOffset, LegacyNameByteLength),
            (LegacyYenOffset, LegacyYenLength),
            (LegacyPartyMembersOffset + 2, 1),
            (LegacyFamilyNamePStringOffset, LegacyNameByteLength),
            (LegacyGivenNamePStringOffset, LegacyNameByteLength),
            (LegacyInventoryOffset + 257, 1));

        WorkingSave reopenedSave = OpenOrThrow(service, output);
        Assert.Equal(new SaveNames("Dojima", "Nanako"), reopenedSave.State.Names);
        Assert.Equal(9999999u, reopenedSave.State.Yen);
        Assert.Equal(new PartyMemberId(0x05), reopenedSave.State.PartyMembers[1]);
    }

    [Fact]
    public void WriteWithoutEditsPreservesOriginalBytes()
    {
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);

        SaveWriteResult result = service.Write(save);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.NotSame(input, result.Bytes);
        Assert.Equal(input, result.Bytes);
    }

    [Fact]
    public void OpenPropagatesCodecFailureDiagnostics()
    {
        SaveApplicationService service = new();

        SaveOpenResult<WorkingSave> result = service.Open(new byte[15]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4G002", diagnostic.Code);
        Assert.Equal("Save", diagnostic.Target);
    }

    [Fact]
    public void WritePropagatesCodecFailureDiagnostics()
    {
        SaveDiagnostic expectedDiagnostic = new(
            DiagnosticSeverity.Error,
            "TESTWRITE",
            "Codec write failed.",
            "Save");
        SaveApplicationService service = new(new FailingWriteCodec(expectedDiagnostic));
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveWriteResult result = service.Write(save);

        Assert.False(result.Succeeded);
        Assert.Null(result.Bytes);
        Assert.Equal(new[] { expectedDiagnostic }, result.Diagnostics);
    }

    [Fact]
    public void ApplyEditsRejectsUnsupportedCommandWithoutMutatingSave()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new UnsupportedSaveEditCommand()]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP001", diagnostic.Code);
        Assert.Equal("Edit", diagnostic.Target);
        Assert.Equal(new SaveNames("Sato", "Yu"), save.State.Names);
    }

    [Fact]
    public void ApplyEditsRejectsInvalidNamesAndPartySlot()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());
        SaveEditCommand[] edits =
        [
            new SetSaveNamesEdit("TooLongName", "Yu"),
            new SetPartyMemberEdit(3, 0x01),
        ];

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, edits);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        Assert.Collection(
            result.Diagnostics,
            static diagnostic =>
            {
                Assert.Equal("P4GAPP002", diagnostic.Code);
                Assert.Equal("Names", diagnostic.Target);
            },
            static diagnostic =>
            {
                Assert.Equal("P4GAPP003", diagnostic.Code);
                Assert.Equal("PartyMembers", diagnostic.Target);
            });
    }

    [Fact]
    public void ApplyEditsRejectsInvalidInventoryItemId()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetInventoryItemQuantityEdit(2559, 1)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP004", diagnostic.Code);
        Assert.Equal("Inventory", diagnostic.Target);
        Assert.Equal(new InventoryStack(1, 2), save.State.InventoryStacks[0]);
    }

    [Theory]
    [InlineData((ushort)1024)]
    [InlineData((ushort)1792)]
    public void ApplyEditsRejectsPlaceholderInventoryQuantityEdits(ushort itemId)
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetInventoryItemQuantityEdit(itemId, 1)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP004", diagnostic.Code);
        Assert.Equal("Inventory", diagnostic.Target);
        Assert.Equal(new InventoryStack(1, 2), save.State.InventoryStacks[0]);
    }

    [Theory]
    [InlineData((ushort)1024)]
    [InlineData((ushort)1792)]
    public void ApplyEditsRejectsPlaceholderInventoryRemovalEdits(ushort itemId)
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new RemoveInventoryItemEdit(itemId)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP004", diagnostic.Code);
        Assert.Equal("Inventory", diagnostic.Target);
        Assert.Equal(new InventoryStack(1, 2), save.State.InventoryStacks[0]);
    }

    [Fact]
    public void ApplyEditsRejectsUnsupportedEquipmentCharacterSlot()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetEquippedWeaponEdit(4, 1)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP005", diagnostic.Code);
        Assert.Equal("Equipment", diagnostic.Target);
    }

    [Theory]
    [InlineData(0, (ushort)0, true)]
    [InlineData(0, (ushort)2434, true)]
    [InlineData(0, (ushort)2435, false)]
    [InlineData(1, (ushort)2435, true)]
    [InlineData(1, (ushort)2434, false)]
    [InlineData(5, (ushort)2438, true)]
    [InlineData(5, (ushort)2434, false)]
    public void ApplyEditsValidatesWeaponsPerCharacter(int characterId, ushort itemId, bool expectedSuccess)
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetEquippedWeaponEdit(characterId, itemId)]);

        Assert.Equal(expectedSuccess, result.Succeeded);
        if (expectedSuccess)
        {
            WorkingSave updatedSave = Assert.IsAssignableFrom<WorkingSave>(result.Save);
            Assert.Equal(itemId, updatedSave.State.EquippedWeapons[characterId]);
            Assert.Empty(result.Diagnostics);
            return;
        }

        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP006", diagnostic.Code);
        Assert.Equal("Equipment", diagnostic.Target);
    }

    [Fact]
    public void ApplyEditsAcceptsArmorItem264()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetEquippedArmorEdit(0, 264)]);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        WorkingSave updatedSave = Assert.IsAssignableFrom<WorkingSave>(result.Save);
        Assert.Equal((ushort)264, updatedSave.State.EquippedArmors[0]);
    }

    [Fact]
    public void ApplyEditsRejectsUnsupportedEquipmentItemId()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetEquippedWeaponEdit(0, 2441)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP006", diagnostic.Code);
        Assert.Equal("Equipment", diagnostic.Target);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(-1)]
    public void ApplyEditsRejectsUnsupportedProtagonistPersonaSlotIndex(int slotIndex)
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetProtagonistPersonaSlotEdit(slotIndex, CreatePersonaSlotEdit(0x0102, 88, 0x11111111, 0x1401))]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP007", diagnostic.Code);
        Assert.Equal("Persona", diagnostic.Target);
    }

    [Fact]
    public void ApplyEditsRejectsPersonaEditWithInvalidSkillCount()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());
        PersonaSlotEdit invalidPersonaSlot = new(0x0102, 88, 0x11111111, Array.Empty<ushort>(), 11, 22, 33, 44, 55);

        SaveEditResult<WorkingSave> result = service.ApplyEdits(save, [new SetPartyPersonaSlotEdit(0, invalidPersonaSlot)]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP008", diagnostic.Code);
        Assert.Equal("Persona", diagnostic.Target);
    }

    [Fact]
    public void ApplyEditsRejectsBlankPersonaSlotEditWithPersonaIdZero()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        int protagonistOffset = layout.ProtagonistPersonaSlots.Offset + (2 * layout.ProtagonistPersonaSlots.Stride) + layout.ProtagonistPersonaSlots.PersonaOffsetWithinStride;
        WritePersonaSlotBytes(input.AsSpan(protagonistOffset, PersonaSlotBinaryCodec.BinaryLength), new PersonaSlotSentinel(0, 0, 0, 0, 0));

        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);

        SaveEditResult<WorkingSave> result = service.ApplyEdits(
            save,
            [new SetProtagonistPersonaSlotEdit(2, CreatePersonaSlotEdit(0, 88, 0x11111111, 0x1401))]);

        Assert.False(result.Succeeded);
        Assert.Null(result.Save);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GAPP009", diagnostic.Code);
        Assert.Equal("Persona", diagnostic.Target);
        Assert.Equal("Persona slot edit must specify a persona id.", diagnostic.Message);
        Assert.Equal((byte)0, save.State.ProtagonistPersonaSlots[2].ExistsRawByte);
        Assert.Equal((ushort)0, save.State.ProtagonistPersonaSlots[2].PersonaId);
    }

    [Fact]
    public void ApplyEditsAndWritePatchesPersonaSlotsOnly()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);
        SaveEditResult<WorkingSave> editResult = service.ApplyEdits(
            save,
            [
                new SetProtagonistPersonaSlotEdit(3, CreatePersonaSlotEdit(0x0102, 88, 0x11111111, 0x1401)),
                new SetPartyPersonaSlotEdit(6, CreatePersonaSlotEdit(0x0203, 66, 0x22222222, 0x1501)),
            ]);

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        WorkingSave editedSave = Assert.IsAssignableFrom<WorkingSave>(editResult.Save);
        SaveWriteResult writeResult = service.Write(editedSave);

        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(writeResult.Bytes);
        int protagonistOffset = layout.ProtagonistPersonaSlots.Offset + (3 * layout.ProtagonistPersonaSlots.Stride) + layout.ProtagonistPersonaSlots.PersonaOffsetWithinStride;
        int partyOffset = layout.PartyPersonaSlots.Offset + (6 * layout.PartyPersonaSlots.Stride) + layout.PartyPersonaSlots.PersonaOffsetWithinStride;
        Assert.Equal((ushort)0x0102, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(protagonistOffset + 2, sizeof(ushort))));
        Assert.Equal((ushort)0x0203, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(partyOffset + 2, sizeof(ushort))));
        Assert.Equal(input[protagonistOffset + PersonaSlotBinaryCodec.BinaryLength], output[protagonistOffset + PersonaSlotBinaryCodec.BinaryLength]);
        Assert.Equal(input[partyOffset + PersonaSlotBinaryCodec.BinaryLength], output[partyOffset + PersonaSlotBinaryCodec.BinaryLength]);
        AssertOnlyRangesChanged(
            input,
            output,
            (protagonistOffset, PersonaSlotBinaryCodec.BinaryLength),
            (partyOffset, PersonaSlotBinaryCodec.BinaryLength));
    }

    [Fact]
    public void ApplyEditsAndWriteActivatesBlankPersonaSlotWithExistsByteOne()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        int protagonistOffset = layout.ProtagonistPersonaSlots.Offset + (2 * layout.ProtagonistPersonaSlots.Stride) + layout.ProtagonistPersonaSlots.PersonaOffsetWithinStride;
        WritePersonaSlotBytes(input.AsSpan(protagonistOffset, PersonaSlotBinaryCodec.BinaryLength), new PersonaSlotSentinel(0, 0, 0, 0, 0));

        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);
        Assert.False(save.State.ProtagonistPersonaSlots[2].Exists);
        Assert.Equal((byte)0, save.State.ProtagonistPersonaSlots[2].ExistsRawByte);

        SaveEditResult<WorkingSave> editResult = service.ApplyEdits(
            save,
            [new SetProtagonistPersonaSlotEdit(2, CreatePersonaSlotEdit(0x0102, 88, 0x11111111, 0x1401))]);

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        WorkingSave editedSave = Assert.IsAssignableFrom<WorkingSave>(editResult.Save);
        SaveWriteResult writeResult = service.Write(editedSave);
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(writeResult.Bytes);
        Assert.Equal((byte)1, output[protagonistOffset]);
        Assert.Equal((ushort)0x0102, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(protagonistOffset + 2, sizeof(ushort))));
        AssertOnlyRangesChanged(input, output, (protagonistOffset, PersonaSlotBinaryCodec.BinaryLength));

        WorkingSave reopenedSave = OpenOrThrow(service, output);
        Assert.True(reopenedSave.State.ProtagonistPersonaSlots[2].Exists);
        Assert.Equal((byte)1, reopenedSave.State.ProtagonistPersonaSlots[2].ExistsRawByte);
        Assert.Equal((ushort)0x0102, reopenedSave.State.ProtagonistPersonaSlots[2].PersonaId);
        Assert.Equal((byte)88, reopenedSave.State.ProtagonistPersonaSlots[2].Level);
    }

    [Fact]
    public void ApplyEditsAndWritePreservesPersonaSlotExistsRawByte()
    {
        P4GSaveLayout layout = P4GSaveLayout.For(P4GSaveLayoutKind.P4GGoldenVitaFixed);
        byte[] input = CreateSyntheticSave();
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, input);
        int protagonistOffset = layout.ProtagonistPersonaSlots.Offset + (1 * layout.ProtagonistPersonaSlots.Stride) + layout.ProtagonistPersonaSlots.PersonaOffsetWithinStride;

        SaveEditResult<WorkingSave> editResult = service.ApplyEdits(
            save,
            [new SetProtagonistPersonaSlotEdit(1, CreatePersonaSlotEdit(0x0102, 88, 0x11111111, 0x1401))]);

        Assert.True(editResult.Succeeded, FormatDiagnostics(editResult.Diagnostics));
        WorkingSave editedSave = Assert.IsAssignableFrom<WorkingSave>(editResult.Save);
        SaveWriteResult writeResult = service.Write(editedSave);
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        byte[] output = Assert.IsType<byte[]>(writeResult.Bytes);
        Assert.Equal((byte)0x02, output[protagonistOffset]);
        Assert.Equal((ushort)0x0102, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(protagonistOffset + 2, sizeof(ushort))));
        AssertOnlyRangesChanged(input, output, (protagonistOffset, PersonaSlotBinaryCodec.BinaryLength));

        WorkingSave reopenedSave = OpenOrThrow(service, output);
        Assert.Equal((byte)0x02, reopenedSave.State.ProtagonistPersonaSlots[1].ExistsRawByte);
    }

    [Fact]
    public void WorkingSaveStateCollectionsAreImmutable()
    {
        SaveApplicationService service = new();
        WorkingSave save = OpenOrThrow(service, CreateSyntheticSave());

        AssertReadOnlyListDoesNotExposeArray(save.State.PartyMembers, new PartyMemberId(0x42));
        AssertReadOnlyListDoesNotExposeArray(save.State.EquippedWeapons, (ushort)0x42);
        AssertReadOnlyListDoesNotExposeArray(save.State.EquippedArmors, (ushort)0x42);
        AssertReadOnlyListDoesNotExposeArray(save.State.EquippedAccessories, (ushort)0x42);
        AssertReadOnlyListDoesNotExposeArray(save.State.EquippedCostumes, (ushort)0x42);
        AssertReadOnlyListDoesNotExposeArray(save.State.ProtagonistPersonaSlots, save.State.CompendiumPersonaSlots[0]);
        AssertReadOnlyListDoesNotExposeArray(save.State.PartyPersonaSlots, save.State.CompendiumPersonaSlots[0]);
        AssertReadOnlyListDoesNotExposeArray(save.State.CompendiumPersonaSlots, save.State.ProtagonistPersonaSlots[0]);
        AssertReadOnlyListDoesNotExposeArray(save.State.InventoryStacks, new InventoryStack(0x42, 9));
    }

    private static WorkingSave OpenOrThrow(SaveApplicationService service, byte[] input)
    {
        SaveOpenResult<WorkingSave> result = service.Open(input);
        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        return Assert.IsAssignableFrom<WorkingSave>(result.Snapshot);
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
        WriteEquipmentSlot(bytes, LegacyProtagonistEquipmentOffset, 1, 256, 512, 1792);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (0 * LegacyPartyEquipmentStride), 39, 266, 615, 2040);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (1 * LegacyPartyEquipmentStride), 112, 287, 685, 1792);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (2 * LegacyPartyEquipmentStride), 150, 293, 687, 2040);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (3 * LegacyPartyEquipmentStride), 183, 307, 754, 1792);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (4 * LegacyPartyEquipmentStride), 217, 315, 512, 2040);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (5 * LegacyPartyEquipmentStride), 2305, 328, 615, 1792);
        WriteEquipmentSlot(bytes, LegacyPartyEquipmentOffset + (6 * LegacyPartyEquipmentStride), 2434, 334, 754, 2040);
        WritePersonaSlotBytes(bytes, layout.ProtagonistPersonaSlots, 0, ProtagonistPersonaSlot0);
        WritePersonaSlotBytes(bytes, layout.ProtagonistPersonaSlots, 1, ProtagonistPersonaSlot1);
        WritePersonaSlotBytes(bytes, layout.PartyPersonaSlots, 0, PartyPersonaSlot0);
        WritePersonaSlotBytes(bytes, layout.PartyPersonaSlots, 1, PartyPersonaSlot1);
        WritePersonaSlotBytes(bytes, layout.CompendiumPersonaSlots, 0, CompendiumPersonaSlot0);
        WritePersonaSlotBytes(bytes, layout.CompendiumPersonaSlots, 1, CompendiumPersonaSlot1);
        return bytes;
    }

    private static void WriteEquipmentSlot(byte[] bytes, int offset, ushort weaponId, ushort armorId, ushort accessoryId, ushort costumeId)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), weaponId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2, sizeof(ushort)), armorId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4, sizeof(ushort)), accessoryId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6, sizeof(ushort)), costumeId);
    }

    private static void WritePersonaSlotBytes(
        byte[] bytes,
        PersonaBlockDescriptor block,
        int index,
        PersonaSlotSentinel sentinel)
    {
        int offset = block.Offset + (index * block.Stride) + block.PersonaOffsetWithinStride;
        WritePersonaSlotBytes(bytes.AsSpan(offset, PersonaSlotBinaryCodec.BinaryLength), sentinel);
    }

    private static void WritePersonaSlotBytes(Span<byte> destination, PersonaSlotSentinel sentinel)
    {
        destination[..PersonaSlotBinaryCodec.BinaryLength].Clear();
        destination[0] = sentinel.ExistsRawByte;
        destination[1] = (byte)(0x80 | sentinel.Level);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2, sizeof(ushort)), sentinel.PersonaId);
        destination[4] = sentinel.Level;
        destination[5] = (byte)(sentinel.Level + 1);
        destination[6] = (byte)(sentinel.Level + 2);
        destination[7] = (byte)(sentinel.Level + 3);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, sizeof(uint)), sentinel.TotalExperience);
        for (int index = 0; index < PersonaSlot.SkillCount; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination.Slice(12 + (index * sizeof(ushort)), sizeof(ushort)),
                (ushort)(sentinel.FirstSkillId + index));
        }

        destination[28] = (byte)(sentinel.Level + 4);
        destination[29] = (byte)(sentinel.Level + 5);
        destination[30] = (byte)(sentinel.Level + 6);
        destination[31] = (byte)(sentinel.Level + 7);
        destination[32] = (byte)(sentinel.Level + 8);
    }

    private static void AssertPersonaSlots(
        IReadOnlyList<PersonaSlot> actual,
        int expectedCount,
        PersonaSlotSentinel expectedFirst,
        PersonaSlotSentinel expectedSecond)
    {
        Assert.Equal(expectedCount, actual.Count);
        AssertPersonaSlot(actual[0], expectedFirst);
        AssertPersonaSlot(actual[1], expectedSecond);
    }

    private static void AssertPersonaSlot(PersonaSlot actual, PersonaSlotSentinel expected)
    {
        Assert.True(actual.Exists);
        Assert.Equal((byte)(0x80 | expected.Level), actual.Unknown0);
        Assert.Equal(expected.PersonaId, actual.PersonaId);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.ExistsRawByte, actual.ExistsRawByte);
        Assert.Equal(
            new[] { (byte)(expected.Level + 1), (byte)(expected.Level + 2), (byte)(expected.Level + 3) },
            actual.ReservedAfterLevel);
        Assert.Equal(expected.TotalExperience, actual.TotalExperience);
        Assert.Equal(
            Enumerable.Range(0, PersonaSlot.SkillCount).Select(index => (ushort)(expected.FirstSkillId + index)),
            actual.SkillIds);
        Assert.Equal((byte)(expected.Level + 4), actual.Strength);
        Assert.Equal((byte)(expected.Level + 5), actual.Magic);
        Assert.Equal((byte)(expected.Level + 6), actual.Endurance);
        Assert.Equal((byte)(expected.Level + 7), actual.Agility);
        Assert.Equal((byte)(expected.Level + 8), actual.Luck);
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

    private static PersonaSlotEdit CreatePersonaSlotEdit(
        ushort personaId,
        byte level,
        uint totalExperience,
        ushort firstSkillId) =>
        new(
            personaId,
            level,
            totalExperience,
            Enumerable.Range(0, PersonaSlot.SkillCount).Select(index => (ushort)(firstSkillId + index)).ToArray(),
            11,
            22,
            33,
            44,
            55);

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.Message));

    private sealed record UnsupportedSaveEditCommand : SaveEditCommand;

    private sealed record PersonaSlotSentinel(
        ushort PersonaId,
        byte Level,
        uint TotalExperience,
        ushort FirstSkillId,
        byte ExistsRawByte = 1);

    private sealed class FailingWriteCodec : IApplicationSaveCodec
    {
        private readonly SaveDiagnostic diagnostic;

        public FailingWriteCodec(SaveDiagnostic diagnostic)
        {
            this.diagnostic = diagnostic;
        }

        public SaveOpenResult<SaveSnapshot> Open(ReadOnlyMemory<byte> bytes) =>
            P4GSaveCodec.Open(bytes);

        public SaveWriteResult Write(SaveSnapshot snapshot, IEnumerable<SaveFieldPatch> patches) =>
            SaveWriteResult.Failure([diagnostic]);
    }
}
