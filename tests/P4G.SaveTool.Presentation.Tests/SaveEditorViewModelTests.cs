using System.Text.Json;
using System.Xml.Linq;
using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;
using P4G.SaveTool.Presentation;
using Xunit;

namespace P4G.SaveTool.Presentation.Tests;

public sealed class SaveEditorViewModelTests
{
    private static readonly string[] ForbiddenPresentationDependencyIds = ["Application", "SaveFormat"];

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
        Assert.Equal((byte)99, viewModel.MainCharacterLevel);
        Assert.Equal(0x0f0e0d0cu, viewModel.MainCharacterTotalExperience);
        Assert.Equal(5, viewModel.SocialStats.Count);
        Assert.Equal("Average", viewModel.SocialStats[0].RankName);
        Assert.Equal(18, viewModel.Calendar.Day);
        Assert.Equal(3, viewModel.SocialLinks.Count);
        Assert.Equal((byte)1, viewModel.SocialLinks[0].LinkId);
        Assert.Equal((byte)5, viewModel.SocialLinks[0].Level);
        Assert.Equal((byte)3, viewModel.SocialLinks[0].Progress);
        Assert.Equal((byte)2, viewModel.SocialLinks[0].Flag);
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
    public void LevelExperienceProjectionUsesLegacyFormula()
    {
        Assert.Equal(0u, LevelExperienceProjection.CalculateTotalExperienceFromLevel(1));
        Assert.Equal(216u, LevelExperienceProjection.CalculateTotalExperienceFromLevel(5));
        Assert.Equal(687960u, LevelExperienceProjection.CalculateTotalExperienceFromLevel(50));
    }

    [Fact]
    public void ViewStatesExposeNativeAotTemplateTextThroughToString()
    {
        Assert.Equal("Average", new SocialStatRankChoiceViewState(1, "Average").ToString());
        Assert.Equal("Morning", new CalendarPhaseChoiceViewState(0, "Morning").ToString());
        Assert.Equal("Yosuke", new PartyMemberChoiceViewState(2, "Yosuke").ToString());
        Assert.Equal("Blank", new PartyConfigurationChoiceViewState(0, "Blank").ToString());
        Assert.Equal("Izanagi", new PersonaChoiceViewState(1, "Izanagi").ToString());
        Assert.Equal("Cleave", new SkillChoiceViewState(1, "Cleave").ToString());
        Assert.Equal("Weapons", new ItemCategoryViewState(0, "Weapons").ToString());
        Assert.Equal("Long Sword", new InventoryItemChoiceViewState(1, 0, "Long Sword").ToString());
        Assert.Equal("Hero", new EquipmentCharacterViewState(0, "Hero", 1, 2, 3, 4).ToString());
        Assert.Equal("Yosuke [Magician]  Lv 5  Progress 3", new SocialLinkViewState(0, 1, "Yosuke", "Magician", 5, 3, 2).ToString());
        Assert.Equal("Long Sword [Weapons]  Qty 2", new InventoryStackViewState(0, 1, "Long Sword", 0, "Weapons", 2).ToString());
        Assert.Equal("3", new PersonaSlotViewState(3, true, 1, 12, 1234, [0, 0, 0, 0, 0, 0, 0, 0], 1, 2, 3, 4, 5).ToString());
    }

    [Fact]
    public void PartyConfigurationChoicesUseLegacySaveValuesAndPreserveUnknownValues()
    {
        IReadOnlyList<PartyConfigurationChoiceViewState> knownChoices =
            SaveEditorViewModel.GetPartyConfigurationChoices(7, out PartyConfigurationChoiceViewState selectedKnownChoice);

        Assert.Equal((byte)7, selectedKnownChoice.MemberValue);
        Assert.Equal("Naoto Shirogane", selectedKnownChoice.Name);
        Assert.Contains(knownChoices, static choice => choice.MemberValue == 0 && choice.Name == "Blank");
        Assert.Contains(knownChoices, static choice => choice.MemberValue == 2 && choice.Name == "Yosuke Hanamura");
        Assert.Contains(knownChoices, static choice => choice.MemberValue == 8 && choice.Name == "Teddie");

        IReadOnlyList<PartyConfigurationChoiceViewState> unknownChoices =
            SaveEditorViewModel.GetPartyConfigurationChoices(0xfe, out PartyConfigurationChoiceViewState selectedUnknownChoice);

        Assert.True(selectedUnknownChoice.IsUnknown);
        Assert.Equal((byte)0xfe, selectedUnknownChoice.MemberValue);
        Assert.Equal("Unknown (254)", selectedUnknownChoice.Name);
        Assert.Same(selectedUnknownChoice, unknownChoices[^1]);
    }

    [Fact]
    public void OpenSaveProjectsInventoryEntriesAndCatalogSelectors()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1, 2),
                    new InventoryStack(257, 3),
                    new InventoryStack(1184, 4),
                    new InventoryStack(820, 5),
                    new InventoryStack(821, 6),
                    new InventoryStack(822, 7),
                    new InventoryStack(823, 8),
                ])),
                []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Collection(
            viewModel.InventoryEntries,
            static entry =>
            {
                Assert.Equal((ushort)1, entry.ItemId);
                Assert.Equal("Weapons", entry.CategoryName);
                Assert.NotEqual("Unknown item (1)", entry.ItemName);
                Assert.Equal((byte)2, entry.Quantity);
            },
            static entry =>
            {
                Assert.Equal((ushort)257, entry.ItemId);
                Assert.Equal("Armor", entry.CategoryName);
                Assert.Equal((byte)3, entry.Quantity);
            },
            static entry =>
            {
                Assert.Equal((ushort)1184, entry.ItemId);
                Assert.Equal("Social Link", entry.CategoryName);
                Assert.Equal((byte)4, entry.Quantity);
            },
            static entry =>
            {
                Assert.Equal((ushort)820, entry.ItemId);
                Assert.Equal("Arc Magatama", entry.ItemName);
                Assert.Equal("Other", entry.CategoryName);
            },
            static entry =>
            {
                Assert.Equal((ushort)821, entry.ItemId);
                Assert.Equal("Amethyst", entry.ItemName);
                Assert.Equal("Other", entry.CategoryName);
            },
            static entry =>
            {
                Assert.Equal((ushort)822, entry.ItemId);
                Assert.Equal("Aquamarine", entry.ItemName);
                Assert.Equal("Other", entry.CategoryName);
            },
            static entry =>
            {
                Assert.Equal((ushort)823, entry.ItemId);
                Assert.Equal("Emerald", entry.ItemName);
                Assert.Equal("Other", entry.CategoryName);
            });
        Assert.Contains(SaveEditorViewModel.InventoryCategories, static category => category.Name == "Weapons");
        Assert.Contains(SaveEditorViewModel.InventoryCategories, static category => category.Name == "Armor");
        Assert.Contains(SaveEditorViewModel.InventoryCategories, static category => category.Name == "Other");

        IReadOnlyList<InventoryItemChoiceViewState> weapons = SaveEditorViewModel.GetInventoryItemsForCategory((byte)ItemCategoryId.Weapons);
        Assert.True(weapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, weapons[0].ItemId);
        Assert.Equal((ushort)1, weapons[1].ItemId);
        Assert.Contains(weapons, static item => item.ItemId == 2305);
        Assert.Contains(weapons, static item => item.ItemId == 2432);
        Assert.Contains(weapons, static item => item.ItemId == 2434);
        Assert.Contains(weapons, static item => item.ItemId == 2440);
        Assert.DoesNotContain(weapons, static item => item.ItemId == 2388);
        Assert.DoesNotContain(weapons, static item => item.ItemId == 2433);

        IReadOnlyList<InventoryItemChoiceViewState> other = SaveEditorViewModel.GetInventoryItemsForCategory((byte)ItemCategoryId.Other);
        InventoryItemChoiceViewState otherPlaceholder = Assert.Single(other);
        Assert.True(otherPlaceholder.IsPlaceholder);
        Assert.Equal((ushort)1024, otherPlaceholder.ItemId);
        AssertReadOnlyListDoesNotAllowMutation(
            viewModel.InventoryEntries,
            new InventoryStackViewState(0, 999, "Test", 0, "Weapons", 1));
    }

    [Fact]
    public void OpenSaveProjectsOtherInventoryEntriesAndAllowsInventoryEdits()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1025, 7),
                ])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        InventoryStackViewState entry = Assert.Single(viewModel.InventoryEntries);
        Assert.Equal((ushort)1025, entry.ItemId);
        Assert.Equal("Other", entry.CategoryName);
        Assert.NotEqual("Blank", entry.ItemName);
        Assert.EndsWith(" [Other]", entry.DisplayName);
        IReadOnlyList<InventoryItemChoiceViewState> otherChoices = SaveEditorViewModel.GetInventoryItemsForCategory((byte)ItemCategoryId.Other);
        Assert.NotEmpty(otherChoices);
        Assert.True(otherChoices[0].IsPlaceholder);
        Assert.Equal((ushort)1024, otherChoices[0].ItemId);
        Assert.DoesNotContain(otherChoices, static item => item.ItemId == 1025);

        SaveEditorOperationResult updateResult = viewModel.SetInventoryItemQuantity(1025, 9);
        SaveEditorOperationResult removeResult = viewModel.RemoveInventoryItem(1025);

        Assert.True(updateResult.Succeeded, FormatDiagnostics(updateResult.Diagnostics));
        Assert.True(removeResult.Succeeded, FormatDiagnostics(removeResult.Diagnostics));
    }

    [Fact]
    public void ShelfPickerUsesLegacyOrdering()
    {
        IReadOnlyList<InventoryItemChoiceViewState> shelf = SaveEditorViewModel.GetInventoryItemsForCategory((byte)ItemCategoryId.Shelf);

        Assert.True(shelf[0].IsPlaceholder);
        Assert.Equal((ushort)1024, shelf[0].ItemId);
        Assert.Equal(
            new ushort[] { 2056, 2057, 2058, 2059, 2060, 1234 },
            shelf.Skip(1).Take(6).Select(static item => item.ItemId).ToArray());
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)256)]
    [InlineData((ushort)1024)]
    [InlineData((ushort)1792)]
    public void PlaceholderInventoryItemsAreHiddenFromInventoryEntries(ushort itemId)
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(itemId, 1),
                ])),
                []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Empty(viewModel.InventoryEntries);
    }

    [Fact]
    public void CostumePickerIncludesLegacyDefaultClothingItemAsPlaceholder()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        IReadOnlyList<InventoryItemChoiceViewState> costumes = SaveEditorViewModel.GetInventoryItemsForCategory((byte)ItemCategoryId.Costumes);

        Assert.NotEmpty(costumes);
        InventoryItemChoiceViewState legacyCostumeBlank = Assert.Single(costumes, static item => item.ItemId == 1792);
        Assert.True(legacyCostumeBlank.IsPlaceholder);
        Assert.DoesNotContain(costumes, static item => item.ItemId == 1792 && !item.IsPlaceholder);
        Assert.Contains(costumes, static item => item.ItemId == 2040);
    }

    [Fact]
    public void OpenSaveProjectsEquipmentCharactersAndSelectors()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(7, viewModel.EquipmentCharacters.Count);
        EquipmentCharacterViewState protagonist = viewModel.EquipmentCharacters[0];
        Assert.Equal((byte)0, protagonist.CharacterId);
        Assert.Equal("Yu Sato", protagonist.Name);
        Assert.Equal((ushort)1, protagonist.WeaponItemId);
        Assert.Equal((ushort)256, protagonist.ArmorItemId);
        Assert.Equal((ushort)512, protagonist.AccessoryItemId);
        Assert.Equal((ushort)1792, protagonist.CostumeItemId);
        EquipmentCharacterViewState firstPartyMember = viewModel.EquipmentCharacters[1];
        Assert.Equal((byte)1, firstPartyMember.CharacterId);
        Assert.Equal("Yosuke Hanamura", firstPartyMember.Name);
        Assert.Equal((ushort)39, firstPartyMember.WeaponItemId);
        Assert.Equal((ushort)266, firstPartyMember.ArmorItemId);
        Assert.DoesNotContain(viewModel.EquipmentCharacters, static character => character.CharacterId == 4);

        IReadOnlyList<InventoryItemChoiceViewState> protagonistWeapons = SaveEditorViewModel.GetWeaponChoices(protagonist.CharacterId);
        Assert.True(protagonistWeapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, protagonistWeapons[0].ItemId);
        Assert.Contains(protagonistWeapons, static item => item.ItemId == 1);
        Assert.Contains(protagonistWeapons, static item => item.ItemId == 2305);
        Assert.Contains(protagonistWeapons, static item => item.ItemId == 2434);
        Assert.DoesNotContain(protagonistWeapons, static item => item.ItemId == 39);
        Assert.DoesNotContain(protagonistWeapons, static item => item.ItemId == 2435);

        IReadOnlyList<InventoryItemChoiceViewState> yosukeWeapons = SaveEditorViewModel.GetWeaponChoices(firstPartyMember.CharacterId);
        Assert.True(yosukeWeapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, yosukeWeapons[0].ItemId);
        Assert.Contains(yosukeWeapons, static item => item.ItemId == 39);
        Assert.Contains(yosukeWeapons, static item => item.ItemId == 2326);
        Assert.Contains(yosukeWeapons, static item => item.ItemId == 2435);
        Assert.DoesNotContain(yosukeWeapons, static item => item.ItemId == 1);
        Assert.DoesNotContain(yosukeWeapons, static item => item.ItemId == 2434);

        IReadOnlyList<InventoryItemChoiceViewState> chieWeapons = SaveEditorViewModel.GetWeaponChoices(2);
        Assert.True(chieWeapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, chieWeapons[0].ItemId);
        Assert.Contains(chieWeapons, static item => item.ItemId == 112);
        Assert.Contains(chieWeapons, static item => item.ItemId == 142);
        Assert.Contains(chieWeapons, static item => item.ItemId == 2367);
        Assert.Contains(chieWeapons, static item => item.ItemId == 2375);
        Assert.Contains(chieWeapons, static item => item.ItemId == 2436);
        Assert.DoesNotContain(chieWeapons, static item => item.ItemId == 77);
        Assert.DoesNotContain(chieWeapons, static item => item.ItemId == 2437);

        IReadOnlyList<InventoryItemChoiceViewState> yukikoWeapons = SaveEditorViewModel.GetWeaponChoices(3);
        Assert.True(yukikoWeapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, yukikoWeapons[0].ItemId);
        Assert.Contains(yukikoWeapons, static item => item.ItemId == 77);
        Assert.Contains(yukikoWeapons, static item => item.ItemId == 104);
        Assert.Contains(yukikoWeapons, static item => item.ItemId == 2345);
        Assert.Contains(yukikoWeapons, static item => item.ItemId == 2355);
        Assert.Contains(yukikoWeapons, static item => item.ItemId == 2437);
        Assert.DoesNotContain(yukikoWeapons, static item => item.ItemId == 112);
        Assert.DoesNotContain(yukikoWeapons, static item => item.ItemId == 2436);

        IReadOnlyList<InventoryItemChoiceViewState> kanjiWeapons = SaveEditorViewModel.GetWeaponChoices(5);
        Assert.True(kanjiWeapons[0].IsPlaceholder);
        Assert.Equal((ushort)0, kanjiWeapons[0].ItemId);
        Assert.Contains(kanjiWeapons, static item => item.ItemId == 150);
        Assert.Contains(kanjiWeapons, static item => item.ItemId == 2385);
        Assert.Contains(kanjiWeapons, static item => item.ItemId == 2389);
        Assert.Contains(kanjiWeapons, static item => item.ItemId == 2396);
        Assert.Contains(kanjiWeapons, static item => item.ItemId == 2438);
        Assert.DoesNotContain(kanjiWeapons, static item => item.ItemId == 2388);

        Assert.Contains(SaveEditorViewModel.GetArmorChoices(), static item => item.ItemId == 334);
        Assert.Contains(SaveEditorViewModel.GetArmorChoices(), static item => item.ItemId == 264);
        Assert.Contains(SaveEditorViewModel.GetAccessoryChoices(), static item => item.ItemId == 754);
        Assert.Contains(SaveEditorViewModel.GetCostumeChoices(), static item => item.ItemId == 2040);
    }

    [Fact]
    public void OpenSaveProjectsPersonaMemberAndChoiceSelectors()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.NotEmpty(viewModel.PartyMemberChoices);
        Assert.Collection(
            viewModel.PartyMemberChoices.Take(2),
            static member =>
            {
                Assert.Equal((byte)0, member.MemberId);
                Assert.Equal("Yu Sato", member.Name);
                Assert.False(member.IsUnknown);
            },
            static member =>
            {
                Assert.Equal((byte)1, member.MemberId);
                Assert.Equal("Yosuke Hanamura", member.Name);
                Assert.False(member.IsUnknown);
            });
        Assert.Contains(viewModel.PartyMemberChoices, static member => member.MemberId == 4 && member.Name == "Rise Kujikawa");

        ushort knownPersonaId = P4GCatalog.Personas[1].Id;
        IReadOnlyList<PersonaChoiceViewState> personaChoices = SaveEditorViewModel.GetPersonaChoices(knownPersonaId, out PersonaChoiceViewState selectedPersona);
        Assert.Contains(personaChoices, static choice => choice.PersonaId == 0 && choice.Name == "Blank" && !choice.IsUnknown);
        Assert.DoesNotContain(personaChoices, static choice => choice.PersonaId == 43);
        Assert.DoesNotContain(personaChoices, static choice => choice.PersonaId == 52);
        Assert.Contains(personaChoices, static choice => choice.PersonaId == P4GCatalog.Personas[1].Id);
        Assert.Equal(knownPersonaId, selectedPersona.PersonaId);
        Assert.False(selectedPersona.IsUnknown);

        IReadOnlyList<PersonaChoiceViewState> unknownPersonaChoices = SaveEditorViewModel.GetPersonaChoices(0xDEAD, out PersonaChoiceViewState unknownPersona);
        Assert.True(unknownPersona.IsUnknown);
        Assert.Equal((ushort)0xDEAD, unknownPersona.PersonaId);
        Assert.Contains(unknownPersonaChoices, static choice => choice.PersonaId == 0xDEAD && choice.IsUnknown);

        ushort knownSkillId = P4GCatalog.Skills[1].Id;
        IReadOnlyList<SkillChoiceViewState> skillChoices = SaveEditorViewModel.GetSkillChoices(0xBEEF, out SkillChoiceViewState unknownSkill);
        Assert.DoesNotContain(skillChoices, static choice => choice.SkillId == 255);
        Assert.DoesNotContain(skillChoices, static choice => choice.SkillId == 301);
        Assert.True(unknownSkill.IsUnknown);
        Assert.Equal((ushort)0xBEEF, unknownSkill.SkillId);
        Assert.Contains(skillChoices, static choice => choice.SkillId == 0xBEEF && choice.IsUnknown);

        IReadOnlyList<SkillChoiceViewState> knownSkillChoices = SaveEditorViewModel.GetSkillChoices(knownSkillId, out SkillChoiceViewState selectedSkill);
        Assert.Equal(knownSkillId, selectedSkill.SkillId);
        Assert.False(selectedSkill.IsUnknown);
        Assert.Contains(knownSkillChoices, choice => choice.SkillId == knownSkillId);
    }

    [Fact]
    public void OpenSaveProjectsSocialStatsAndCalendarSelectors()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(5, viewModel.SocialStats.Count);
        Assert.Equal(0, viewModel.SocialStats[0].StatIndex);
        Assert.Equal("Courage", viewModel.SocialStats[0].Name);
        Assert.Equal(15, viewModel.SocialStats[0].Points);
        Assert.Equal(1, viewModel.SocialStats[0].Rank);
        Assert.Equal("Average", viewModel.SocialStats[0].RankName);
        Assert.Equal(1, viewModel.SocialStats[1].StatIndex);
        Assert.Equal("Knowledge", viewModel.SocialStats[1].Name);
        Assert.Equal(30, viewModel.SocialStats[1].Points);
        Assert.Equal(2, viewModel.SocialStats[1].Rank);
        Assert.Equal("Informed", viewModel.SocialStats[1].RankName);
        Assert.Equal(2, viewModel.SocialStats[2].StatIndex);
        Assert.Equal("Diligence", viewModel.SocialStats[2].Name);
        Assert.Equal(80, viewModel.SocialStats[2].Points);
        Assert.Equal(4, viewModel.SocialStats[2].Rank);
        Assert.Equal("Thorough", viewModel.SocialStats[2].RankName);
        Assert.Equal(3, viewModel.SocialStats[3].StatIndex);
        Assert.Equal("Understanding", viewModel.SocialStats[3].Name);
        Assert.Equal(140, viewModel.SocialStats[3].Points);
        Assert.Equal(5, viewModel.SocialStats[3].Rank);
        Assert.Equal("Saintly", viewModel.SocialStats[3].RankName);
        Assert.Equal(18, viewModel.Calendar.Day);
        Assert.Equal(4, viewModel.Calendar.DayPhaseId);
        Assert.Equal(19, viewModel.Calendar.NextDay);
        Assert.Equal(5, viewModel.Calendar.NextDayPhaseId);

        IReadOnlyList<SocialStatRankChoiceViewState> courageChoices = SaveEditorViewModel.GetSocialStatChoices(
            0,
            viewModel.SocialStats[0].Points,
            out SocialStatRankChoiceViewState selectedCourageRank);
        Assert.Equal(1, selectedCourageRank.Rank);
        Assert.Same(courageChoices[0], selectedCourageRank);

        IReadOnlyList<SocialStatRankChoiceViewState> diligenceChoices = SaveEditorViewModel.GetSocialStatChoices(
            2,
            viewModel.SocialStats[2].Points,
            out SocialStatRankChoiceViewState selectedDiligenceRank);
        Assert.Equal(4, selectedDiligenceRank.Rank);
        Assert.Equal("Thorough", selectedDiligenceRank.Name);
        Assert.Same(diligenceChoices[3], selectedDiligenceRank);

        IReadOnlyList<SocialStatRankChoiceViewState> understandingChoices = SaveEditorViewModel.GetSocialStatChoices(
            3,
            viewModel.SocialStats[3].Points,
            out SocialStatRankChoiceViewState selectedUnderstandingRank);
        Assert.Equal(5, selectedUnderstandingRank.Rank);
        Assert.Equal("Saintly", selectedUnderstandingRank.Name);
        Assert.Same(understandingChoices[4], selectedUnderstandingRank);

        CalendarPhaseChoiceViewState[] calendarChoices = [.. SaveEditorViewModel.GetCalendarPhaseChoices(
            viewModel.Calendar.DayPhaseId,
            out CalendarPhaseChoiceViewState selectedPhase)];
        Assert.Equal(4, selectedPhase.PhaseId);
        Assert.Same(calendarChoices[4], selectedPhase);
    }

    [Fact]
    public void OpenSaveProjectsBlankSocialLinkChoiceWhenCurrentLinkIdIsZero()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        IReadOnlyList<SocialLinkChoiceViewState> linkChoices = SaveEditorViewModel.GetSocialLinkChoices(0, out SocialLinkChoiceViewState selectedChoice);

        Assert.Equal(P4GCatalog.SocialLinks.Count, linkChoices.Count);
        Assert.Same(linkChoices[0], selectedChoice);
        Assert.Equal((byte)0, selectedChoice.LinkId);
        Assert.True(selectedChoice.IsPlaceholder);
        Assert.False(selectedChoice.IsUnknown);
        Assert.Contains(linkChoices, static choice => choice.LinkId == 0 && choice.IsPlaceholder);
        Assert.DoesNotContain(linkChoices, static choice => choice.IsUnknown);
    }

    [Fact]
    public void OpenSaveProjectsUnknownSocialLinkProjectionAndSelectorChoice()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(socialLinks:
                [
                    new SocialLinkState(1, 5, 3, 2),
                    new SocialLinkState(99, 2, 4, 1),
                ])),
                []),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        Assert.True(viewModel.SocialLinks[1].IsUnknown);
        Assert.Equal((byte)99, viewModel.SocialLinks[1].LinkId);
        Assert.Equal("Unknown (99)", viewModel.SocialLinks[1].Name);
        Assert.Equal("Unknown (99)", viewModel.SocialLinks[1].DisplayName);

        IReadOnlyList<SocialLinkChoiceViewState> linkChoices = SaveEditorViewModel.GetSocialLinkChoices(99, out SocialLinkChoiceViewState selectedChoice);

        Assert.Equal(P4GCatalog.SocialLinks.Count + 1, linkChoices.Count);
        Assert.Same(linkChoices[^1], selectedChoice);
        Assert.Equal((byte)99, selectedChoice.LinkId);
        Assert.True(selectedChoice.IsUnknown);
        Assert.Equal("Unknown (99)", selectedChoice.Name);
        Assert.Contains(linkChoices, static choice => choice.LinkId == 99 && choice.IsUnknown);
    }

    [Fact]
    public void NameEditsRefreshEquipmentCharactersProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        IReadOnlyList<EquipmentCharacterViewState> initialEquipmentCharacters = viewModel.EquipmentCharacters;

        SaveEditorOperationResult result = viewModel.SetNames("Dojima", "Nanako");

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.NotSame(initialEquipmentCharacters, viewModel.EquipmentCharacters);
        Assert.Equal("Nanako Dojima", viewModel.EquipmentCharacters[0].Name);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetSaveNamesEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void SocialStatAndCalendarEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult courageResult = viewModel.SetSocialStatRank(0, 5);
        SaveEditorOperationResult dayPhaseResult = viewModel.SetDayPhase(5);
        SaveEditorOperationResult nextDayResult = viewModel.SetNextDay(21);
        SaveEditorOperationResult nextPhaseResult = viewModel.SetNextDayPhase(1);

        Assert.True(courageResult.Succeeded, FormatDiagnostics(courageResult.Diagnostics));
        Assert.True(dayPhaseResult.Succeeded, FormatDiagnostics(dayPhaseResult.Diagnostics));
        Assert.True(nextDayResult.Succeeded, FormatDiagnostics(nextDayResult.Diagnostics));
        Assert.True(nextPhaseResult.Succeeded, FormatDiagnostics(nextPhaseResult.Diagnostics));
        Assert.Equal(5, viewModel.SocialStats[0].Rank);
        Assert.Equal(140, viewModel.SocialStats[0].Points);
        Assert.Equal(5, viewModel.Calendar.DayPhaseId);
        Assert.Equal(21, viewModel.Calendar.NextDay);
        Assert.Equal(1, viewModel.Calendar.NextDayPhaseId);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetSocialStatRankEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetDayPhaseEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetNextDayEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetNextDayPhaseEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void SocialStatEditMethodsApplyRankToPointsForIndicesOneTwoAndThree()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult knowledgeResult = viewModel.SetSocialStatRank(1, 5);
        SaveEditorOperationResult diligenceResult = viewModel.SetSocialStatRank(2, 1);
        SaveEditorOperationResult understandingResult = viewModel.SetSocialStatRank(3, 4);

        Assert.True(knowledgeResult.Succeeded, FormatDiagnostics(knowledgeResult.Diagnostics));
        Assert.True(diligenceResult.Succeeded, FormatDiagnostics(diligenceResult.Diagnostics));
        Assert.True(understandingResult.Succeeded, FormatDiagnostics(understandingResult.Diagnostics));
        Assert.Equal(240, viewModel.SocialStats.Single(stat => stat.StatIndex == 1).Points);
        Assert.Equal(5, viewModel.SocialStats.Single(stat => stat.StatIndex == 1).Rank);
        Assert.Equal(15, viewModel.SocialStats.Single(stat => stat.StatIndex == 2).Points);
        Assert.Equal(1, viewModel.SocialStats.Single(stat => stat.StatIndex == 2).Rank);
        Assert.Equal(80, viewModel.SocialStats.Single(stat => stat.StatIndex == 3).Points);
        Assert.Equal(4, viewModel.SocialStats.Single(stat => stat.StatIndex == 3).Rank);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits =>
            {
                SetSocialStatRankEdit edit = Assert.IsType<SetSocialStatRankEdit>(Assert.Single(edits));
                Assert.Equal(1, edit.StatIndex);
                Assert.Equal(5, edit.Rank);
            },
            static edits =>
            {
                SetSocialStatRankEdit edit = Assert.IsType<SetSocialStatRankEdit>(Assert.Single(edits));
                Assert.Equal(2, edit.StatIndex);
                Assert.Equal(1, edit.Rank);
            },
            static edits =>
            {
                SetSocialStatRankEdit edit = Assert.IsType<SetSocialStatRankEdit>(Assert.Single(edits));
                Assert.Equal(3, edit.StatIndex);
                Assert.Equal(4, edit.Rank);
            });
    }

    [Fact]
    public void UnchangedSocialStatRankSelectionPreservesMidRankPointsAndSkipsEdits()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(socialStats: [18, 30, 80, 140, 85])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetSocialStatRank(0, 2);
        SaveEditorOperationResult dayPhaseResult = viewModel.SetDayPhase(4);
        SaveEditorOperationResult nextDayPhaseResult = viewModel.SetNextDayPhase(5);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.True(dayPhaseResult.Succeeded, FormatDiagnostics(dayPhaseResult.Diagnostics));
        Assert.True(nextDayPhaseResult.Succeeded, FormatDiagnostics(nextDayPhaseResult.Diagnostics));
        Assert.Equal(18, viewModel.SocialStats[0].Points);
        Assert.Equal(2, viewModel.SocialStats[0].Rank);
        Assert.Equal(4, viewModel.Calendar.DayPhaseId);
        Assert.Equal(5, viewModel.Calendar.NextDayPhaseId);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void UnchangedUnknownCalendarPhasesViaDirectSettersPreserveDiagnosticsAndSkipEdits()
    {
        SaveDiagnostic warning = new(DiagnosticSeverity.Warning, "WARN", "Opened with a warning.", "Open");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(dayPhase: 200, nextDayPhase: 201)),
                [warning]),
            ApplyEditsHandler = static (_, _) => throw new InvalidOperationException("Unchanged unknown calendar phases should not apply edits."),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        IReadOnlyList<CalendarPhaseChoiceViewState> dayPhaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(
            viewModel.Calendar.DayPhaseId,
            out CalendarPhaseChoiceViewState selectedDayPhase);
        IReadOnlyList<CalendarPhaseChoiceViewState> nextDayPhaseChoices = SaveEditorViewModel.GetCalendarPhaseChoices(
            viewModel.Calendar.NextDayPhaseId,
            out CalendarPhaseChoiceViewState selectedNextDayPhase);

        Assert.True(selectedDayPhase.IsUnknown);
        Assert.Equal(200, selectedDayPhase.PhaseId);
        Assert.Contains(dayPhaseChoices, static choice => choice.PhaseId == 200 && choice.IsUnknown);
        Assert.True(selectedNextDayPhase.IsUnknown);
        Assert.Equal(201, selectedNextDayPhase.PhaseId);
        Assert.Contains(nextDayPhaseChoices, static choice => choice.PhaseId == 201 && choice.IsUnknown);

        SaveEditorOperationResult dayPhaseResult = viewModel.SetDayPhase(200);
        SaveEditorOperationResult nextDayPhaseResult = viewModel.SetNextDayPhase(201);

        Assert.True(dayPhaseResult.Succeeded, FormatDiagnostics(dayPhaseResult.Diagnostics));
        Assert.True(nextDayPhaseResult.Succeeded, FormatDiagnostics(nextDayPhaseResult.Diagnostics));
        Assert.Empty(dayPhaseResult.Diagnostics);
        Assert.Empty(nextDayPhaseResult.Diagnostics);
        Assert.Equal(200, viewModel.Calendar.DayPhaseId);
        Assert.Equal(201, viewModel.Calendar.NextDayPhaseId);
        Assert.Equal(new[] { warning }, viewModel.Diagnostics);
        Assert.False(viewModel.HasErrors);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void PlaceholderInventoryItemsCannotBeModified()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult setResult = viewModel.SetInventoryItemQuantity(1792, 1);
        SaveEditorOperationResult removeResult = viewModel.RemoveInventoryItem(1792);

        Assert.False(setResult.Succeeded);
        Assert.False(removeResult.Succeeded);
        Assert.Single(setResult.Diagnostics, diagnostic => diagnostic.Code == "P4GPRES008");
        Assert.Single(removeResult.Diagnostics, diagnostic => diagnostic.Code == "P4GPRES008");
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void PlaceholderInventoryStacksAreHiddenFromProjectedEntries()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1792, 1),
                    new InventoryStack(257, 2),
                    new InventoryStack(1024, 3),
                ])),
                []),
        };
        SaveEditorViewModel viewModel = new(service);

        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        InventoryStackViewState entry = Assert.Single(viewModel.InventoryEntries);
        Assert.Equal((ushort)257, entry.ItemId);
        Assert.Equal((byte)2, entry.Quantity);
        Assert.Equal(1, entry.SlotIndex);
    }

    [Fact]
    public void EquipmentEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        IReadOnlyList<EquipmentCharacterViewState> initialEquipmentCharacters = viewModel.EquipmentCharacters;

        SaveEditorOperationResult weaponResult = viewModel.SetEquippedWeapon(0, 2434);
        SaveEditorOperationResult armorResult = viewModel.SetEquippedArmor(1, 271);
        SaveEditorOperationResult accessoryResult = viewModel.SetEquippedAccessory(2, 687);
        SaveEditorOperationResult costumeResult = viewModel.SetEquippedCostume(3, 1792);

        Assert.True(weaponResult.Succeeded, FormatDiagnostics(weaponResult.Diagnostics));
        Assert.True(armorResult.Succeeded, FormatDiagnostics(armorResult.Diagnostics));
        Assert.True(accessoryResult.Succeeded, FormatDiagnostics(accessoryResult.Diagnostics));
        Assert.True(costumeResult.Succeeded, FormatDiagnostics(costumeResult.Diagnostics));
        Assert.NotSame(initialEquipmentCharacters, viewModel.EquipmentCharacters);
        Assert.Equal((ushort)2434, viewModel.EquipmentCharacters[0].WeaponItemId);
        Assert.Equal((ushort)271, viewModel.EquipmentCharacters[1].ArmorItemId);
        Assert.Equal((ushort)687, viewModel.EquipmentCharacters[2].AccessoryItemId);
        Assert.Equal((ushort)1792, viewModel.EquipmentCharacters[3].CostumeItemId);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetEquippedWeaponEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetEquippedArmorEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetEquippedAccessoryEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetEquippedCostumeEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void PersonaEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        PersonaSlotEdit protagonistEdit = CreatePersonaSlotEdit(0x0102, 88, 0x11111111, 0x1401);
        PersonaSlotEdit partyEdit = CreatePersonaSlotEdit(0x0203, 66, 0x22222222, 0x1501);

        SaveEditorOperationResult protagonistResult = viewModel.SetProtagonistPersonaSlot(0, protagonistEdit);
        SaveEditorOperationResult partyResult = viewModel.SetPartyPersonaSlot(0, partyEdit);

        Assert.True(protagonistResult.Succeeded, FormatDiagnostics(protagonistResult.Diagnostics));
        Assert.True(partyResult.Succeeded, FormatDiagnostics(partyResult.Diagnostics));
        Assert.Equal((ushort)0x0102, viewModel.ProtagonistPersonaSlots[0].PersonaId);
        Assert.Equal((ushort)0x0203, viewModel.PartyPersonaSlots[0].PersonaId);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetProtagonistPersonaSlotEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetPartyPersonaSlotEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void PersonaEditMethodsRejectBlankPersonaIdAndSurfaceDiagnostics()
    {
        WorkingSaveState blankPersonaState = CreateState().WithProtagonistPersonaSlot(
            0,
            new PersonaSlot(false, 0, 0, 0, [0, 0, 0], 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0));
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(blankPersonaState), []),
            ApplyEditsHandler = static (_, _) => new SaveEditResult<WorkingSave>(
                null,
                [new SaveDiagnostic(DiagnosticSeverity.Error, "P4GAPP009", "Persona slot edit must specify a persona id.", "Persona")]),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetProtagonistPersonaSlot(0, CreatePersonaSlotEdit(0, 88, 0x11111111, 0x1401));

        Assert.False(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Equal(new[] { result.Diagnostics.Single() }, viewModel.Diagnostics);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.ProtagonistPersonaSlots[0].Exists);
        Assert.Equal((ushort)0, viewModel.ProtagonistPersonaSlots[0].PersonaId);
        Assert.Equal("P4GAPP009", result.Diagnostics.Single().Code);
        Assert.Equal("Persona", result.Diagnostics.Single().Target);
    }

    [Fact]
    public void CompendiumEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        WorkingSaveState compendiumState = CreateState(compendiumPersonaSlots:
        [
            CreateBlankPersonaSlot(),
            CreatePersonaSlot(0x0404, 12, 0x04040404, 0x4401),
        ]);
        FakeSaveApplicationService service = new()
        {
            OpenHandler = _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(compendiumState), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult setResult = viewModel.SetCompendiumPersonaSlot(0, CreatePersonaSlotEdit(0x0505, 90, 0x05050505, 0x5501));
        SaveEditorOperationResult clearResult = viewModel.ClearCompendiumPersonaSlot(1);

        Assert.True(setResult.Succeeded, FormatDiagnostics(setResult.Diagnostics));
        Assert.True(clearResult.Succeeded, FormatDiagnostics(clearResult.Diagnostics));
        Assert.Equal((ushort)0x0505, viewModel.CompendiumPersonaSlots[0].PersonaId);
        Assert.Equal((byte)90, viewModel.CompendiumPersonaSlots[0].Level);
        Assert.False(viewModel.CompendiumPersonaSlots[1].Exists);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits =>
            {
                SetCompendiumPersonaSlotEdit edit = Assert.IsType<SetCompendiumPersonaSlotEdit>(Assert.Single(edits));
                Assert.Equal(0, edit.SlotIndex);
                Assert.Equal((ushort)0x0505, edit.PersonaSlot.PersonaId);
            },
            static edits =>
            {
                ClearCompendiumPersonaSlotEdit edit = Assert.IsType<ClearCompendiumPersonaSlotEdit>(Assert.Single(edits));
                Assert.Equal(1, edit.SlotIndex);
            });
    }

    [Fact]
    public void CompendiumEditMethodsSkipNoOpEditsAndPreserveDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState(compendiumPersonaSlots:
            [
                CreatePersonaSlot(0x0303, 22, 0x03030303, 0x3301),
                CreateBlankPersonaSlot(),
                CreateBlankPersonaSlot(),
            ])), []),
            ApplyEditsHandler = static (_, _) => throw new InvalidOperationException("Unchanged compendium edits should not apply."),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetCompendiumPersonaSlot(0, CreatePersonaSlotEdit(0x0303, 22, 0x03030303, 0x3301));
        SaveEditorOperationResult clearResult = viewModel.ClearCompendiumPersonaSlot(2);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.True(clearResult.Succeeded, FormatDiagnostics(clearResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void ClearCompendiumPersonaSlotsSkipsAllBlankSlotsWithoutChangingDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState(compendiumPersonaSlots:
            [
                CreateBlankPersonaSlot(),
                CreateBlankPersonaSlot(),
                CreateBlankPersonaSlot(),
            ])), []),
            ApplyEditsHandler = static (_, _) => throw new InvalidOperationException("Blank compendium slots should not apply."),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.ClearCompendiumPersonaSlots();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.Empty(viewModel.Diagnostics);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void ClearCompendiumPersonaSlotsAppliesEditAndRefreshesProjectionForNonBlankSlots()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState(compendiumPersonaSlots:
            [
                CreatePersonaSlot(0x0404, 12, 0x04040404, 0x4401),
                CreateBlankPersonaSlot(),
                CreatePersonaSlot(0x0505, 34, 0x05050505, 0x5501),
            ])), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.ClearCompendiumPersonaSlots();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.True(viewModel.IsDirty);
        Assert.All(viewModel.CompendiumPersonaSlots, static slot => Assert.False(slot.Exists));
        Assert.Collection(
            service.AppliedEdits,
            static edits =>
            {
                ClearCompendiumPersonaSlotsEdit edit = Assert.IsType<ClearCompendiumPersonaSlotsEdit>(Assert.Single(edits));
                Assert.NotNull(edit);
            });
    }

    [Fact]
    public void CompendiumEditMethodsRejectBlankPersonaIdAndSurfaceDiagnostics()
    {
        SaveDiagnostic error = new(DiagnosticSeverity.Error, "P4GAPP009", "Persona slot edit must specify a persona id.", "Persona");
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = (_, edits) =>
            {
                SetCompendiumPersonaSlotEdit edit = Assert.IsType<SetCompendiumPersonaSlotEdit>(Assert.Single(edits));
                return edit.PersonaSlot.PersonaId == 0
                    ? new SaveEditResult<WorkingSave>(null, [error])
                    : throw new InvalidOperationException("Unexpected compendium edit.");
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetCompendiumPersonaSlot(
            0,
            new PersonaSlotEdit(0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 0, 0));

        Assert.False(result.Succeeded);
        Assert.Equal(new[] { error }, result.Diagnostics);
        Assert.Equal(new[] { error }, viewModel.Diagnostics);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("P4GAPP009", result.Diagnostics.Single().Code);
        Assert.Equal("Persona", result.Diagnostics.Single().Target);
    }

    [Fact]
    public void SocialLinkEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult addResult = viewModel.AddSocialLink(12);
        SaveEditorOperationResult levelResult = viewModel.SetSocialLinkLevel(0, 6);
        SaveEditorOperationResult progressResult = viewModel.SetSocialLinkProgress(1, 4);
        SaveEditorOperationResult flagResult = viewModel.SetSocialLinkFlag(2, 7);
        SaveEditorOperationResult removeResult = viewModel.RemoveSocialLink(3);

        Assert.True(addResult.Succeeded, FormatDiagnostics(addResult.Diagnostics));
        Assert.True(levelResult.Succeeded, FormatDiagnostics(levelResult.Diagnostics));
        Assert.True(progressResult.Succeeded, FormatDiagnostics(progressResult.Diagnostics));
        Assert.True(flagResult.Succeeded, FormatDiagnostics(flagResult.Diagnostics));
        Assert.True(removeResult.Succeeded, FormatDiagnostics(removeResult.Diagnostics));
        Assert.Equal((byte)1, viewModel.SocialLinks[0].LinkId);
        Assert.Equal((byte)6, viewModel.SocialLinks[0].Level);
        Assert.Equal((byte)3, viewModel.SocialLinks[0].Progress);
        Assert.Equal((byte)2, viewModel.SocialLinks[0].Flag);
        Assert.Equal((byte)8, viewModel.SocialLinks[1].LinkId);
        Assert.Equal((byte)2, viewModel.SocialLinks[1].Level);
        Assert.Equal((byte)4, viewModel.SocialLinks[1].Progress);
        Assert.Equal((byte)0, viewModel.SocialLinks[1].Flag);
        Assert.Equal((byte)10, viewModel.SocialLinks[2].LinkId);
        Assert.Equal((byte)1, viewModel.SocialLinks[2].Level);
        Assert.Equal((byte)0, viewModel.SocialLinks[2].Progress);
        Assert.Equal((byte)7, viewModel.SocialLinks[2].Flag);
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<AddSocialLinkEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetSocialLinkLevelEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetSocialLinkProgressEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetSocialLinkFlagEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<RemoveSocialLinkEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void SocialLinkAddMethodSurfacesDiagnosticsForInvalidIds()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (_, edits) =>
            {
                AddSocialLinkEdit addSocialLink = Assert.IsType<AddSocialLinkEdit>(Assert.Single(edits));
                return addSocialLink.LinkId switch
                {
                    1 => new SaveEditResult<WorkingSave>(
                        null,
                        [new SaveDiagnostic(DiagnosticSeverity.Error, "P4GAPP017", "Social link edit targets a duplicate link id.", "SocialLinks")]),
                    _ => throw new InvalidOperationException("Unexpected social link id."),
                };
            },
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult zeroResult = viewModel.AddSocialLink(0);
        SaveEditorOperationResult unknownResult = viewModel.AddSocialLink(99);
        SaveEditorOperationResult duplicateResult = viewModel.AddSocialLink(1);

        Assert.False(zeroResult.Succeeded);
        Assert.False(unknownResult.Succeeded);
        Assert.False(duplicateResult.Succeeded);
        Assert.Equal("P4GAPP016", zeroResult.Diagnostics.Single().Code);
        Assert.Equal("P4GAPP016", unknownResult.Diagnostics.Single().Code);
        Assert.Equal("P4GAPP017", duplicateResult.Diagnostics.Single().Code);
        IReadOnlyList<SaveEditCommand> appliedEdits = Assert.Single(service.AppliedEdits);
        AddSocialLinkEdit appliedAdd = Assert.IsType<AddSocialLinkEdit>(Assert.Single(appliedEdits));
        Assert.Equal((byte)1, appliedAdd.LinkId);
    }

    [Fact]
    public void SocialLinkEditMethodsSkipNoOpEditsAndPreserveDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
            ApplyEditsHandler = static (_, _) => throw new InvalidOperationException("Unchanged social link edits should not apply."),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult levelResult = viewModel.SetSocialLinkLevel(0, 5);
        SaveEditorOperationResult progressResult = viewModel.SetSocialLinkProgress(1, 1);
        SaveEditorOperationResult flagResult = viewModel.SetSocialLinkFlag(2, 1);

        Assert.True(levelResult.Succeeded, FormatDiagnostics(levelResult.Diagnostics));
        Assert.True(progressResult.Succeeded, FormatDiagnostics(progressResult.Diagnostics));
        Assert.True(flagResult.Succeeded, FormatDiagnostics(flagResult.Diagnostics));
        Assert.False(viewModel.IsDirty);
        Assert.Empty(service.AppliedEdits);
    }

    [Fact]
    public void InventoryEditMethodsApplyCommandsRefreshProjectionAndTrackDirtyState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1, 2),
                    new InventoryStack(257, 3),
                ])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult updateResult = viewModel.SetInventoryItemQuantity(257, 9);
        SaveEditorOperationResult removeResult = viewModel.RemoveInventoryItem(1);

        Assert.True(updateResult.Succeeded, FormatDiagnostics(updateResult.Diagnostics));
        Assert.True(removeResult.Succeeded, FormatDiagnostics(removeResult.Diagnostics));
        Assert.Collection(
            viewModel.InventoryEntries,
            static entry =>
            {
                Assert.Equal((ushort)257, entry.ItemId);
                Assert.Equal((byte)9, entry.Quantity);
            });
        Assert.True(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<SetInventoryItemQuantityEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<RemoveInventoryItemEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void InventoryQuantityZeroRemainsVisibleInProjectionUntilReload()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks: [new InventoryStack(257, 3)])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult result = viewModel.SetInventoryItemQuantity(257, 0);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        InventoryStackViewState entry = Assert.Single(viewModel.InventoryEntries);
        Assert.Equal((ushort)257, entry.ItemId);
        Assert.Equal((byte)0, entry.Quantity);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void InventoryEditsThatRestoreEquivalentSetClearDirtyStateEvenWhenOrderChanges()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1, 2),
                    new InventoryStack(257, 3),
                ])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);

        SaveEditorOperationResult removeResult = viewModel.RemoveInventoryItem(1);
        SaveEditorOperationResult restoreResult = viewModel.SetInventoryItemQuantity(1, 2);

        Assert.True(removeResult.Succeeded, FormatDiagnostics(removeResult.Diagnostics));
        Assert.True(restoreResult.Succeeded, FormatDiagnostics(restoreResult.Diagnostics));
        Assert.Collection(
            viewModel.InventoryEntries,
            static entry =>
            {
                Assert.Equal((ushort)257, entry.ItemId);
                Assert.Equal((byte)3, entry.Quantity);
            },
            static entry =>
            {
                Assert.Equal((ushort)1, entry.ItemId);
                Assert.Equal((byte)2, entry.Quantity);
            });
        Assert.False(viewModel.IsDirty);
        Assert.Collection(
            service.AppliedEdits,
            static edits => Assert.IsType<RemoveInventoryItemEdit>(Assert.Single(edits)),
            static edits => Assert.IsType<SetInventoryItemQuantityEdit>(Assert.Single(edits)));
    }

    [Fact]
    public void NonInventoryEditsDoNotRefreshInventoryEntriesProjection()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1, 2),
                ])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        changedProperties.Clear();
        IReadOnlyList<InventoryStackViewState> initialInventoryEntries = viewModel.InventoryEntries;

        SaveEditorOperationResult result = viewModel.SetNames("Dojima", "Nanako");

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.Same(initialInventoryEntries, viewModel.InventoryEntries);
        Assert.DoesNotContain(nameof(SaveEditorViewModel.InventoryEntries), changedProperties);
    }

    [Fact]
    public void InventoryEditsRefreshInventoryEntriesProjection()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(
                new FakeWorkingSave(CreateState(inventoryStacks:
                [
                    new InventoryStack(1, 2),
                ])),
                []),
            ApplyEditsHandler = static (save, edits) => ApplyCommands(save, edits),
        };
        SaveEditorViewModel viewModel = new(service);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        changedProperties.Clear();
        IReadOnlyList<InventoryStackViewState> initialInventoryEntries = viewModel.InventoryEntries;

        SaveEditorOperationResult result = viewModel.SetInventoryItemQuantity(1, 9);

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.NotSame(initialInventoryEntries, viewModel.InventoryEntries);
        Assert.Contains(nameof(SaveEditorViewModel.InventoryEntries), changedProperties);
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
    public void CreateBlankSaveLoadsDefaultStateAndCanWrite()
    {
        FakeSaveApplicationService service = new()
        {
            CreateBlankSaveHandler = static () => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState("", "", 0, mainCharacterLevel: 0)), []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult result = viewModel.CreateBlankSave();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.FamilyName);
        Assert.Equal(string.Empty, viewModel.GivenName);
        Assert.Equal(0u, viewModel.Yen);
        Assert.Equal((byte)0, viewModel.MainCharacterLevel);
        Assert.Equal(1, service.CreateBlankSaveCalls);
        Assert.Empty(viewModel.Diagnostics);
    }

    [Fact]
    public void CreateBlankSaveNoOpsWhenSaveAlreadyExists()
    {
        FakeSaveApplicationService service = new()
        {
            CreateBlankSaveHandler = static () => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState("", "", 0, mainCharacterLevel: 0)), []),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult firstResult = viewModel.CreateBlankSave();
        string familyName = viewModel.FamilyName;
        string givenName = viewModel.GivenName;
        uint yen = viewModel.Yen;
        bool canWrite = viewModel.CanWrite;
        bool isDirty = viewModel.IsDirty;
        IReadOnlyList<SaveDiagnostic> diagnostics = viewModel.Diagnostics;

        SaveEditorOperationResult secondResult = viewModel.CreateBlankSave();

        Assert.True(firstResult.Succeeded, FormatDiagnostics(firstResult.Diagnostics));
        Assert.True(secondResult.Succeeded, FormatDiagnostics(secondResult.Diagnostics));
        Assert.True(viewModel.HasSave);
        Assert.Equal(1, service.CreateBlankSaveCalls);
        Assert.Equal(familyName, viewModel.FamilyName);
        Assert.Equal(givenName, viewModel.GivenName);
        Assert.Equal(yen, viewModel.Yen);
        Assert.Equal(canWrite, viewModel.CanWrite);
        Assert.Equal(isDirty, viewModel.IsDirty);
        Assert.Same(diagnostics, viewModel.Diagnostics);
    }

    [Fact]
    public void CreateBlankSaveCanBePersistedSuccessfully()
    {
        FakeSaveApplicationService service = new()
        {
            CreateBlankSaveHandler = static () => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState("", "", 0, mainCharacterLevel: 0)), []),
            WriteHandler = _ => SaveWriteResult.Success([0x1, 0x2, 0x3]),
        };
        SaveEditorViewModel viewModel = new(service);

        SaveEditorOperationResult createResult = viewModel.CreateBlankSave();
        SaveEditorWriteResult writeResult = viewModel.WriteSave();
        SaveEditorWriteToken writeToken = AssertOperationToken(writeResult);
        SaveEditorOperationResult acknowledgeResult = viewModel.AcknowledgeSaved(writeToken);

        Assert.True(createResult.Succeeded, FormatDiagnostics(createResult.Diagnostics));
        Assert.True(writeResult.Succeeded, FormatDiagnostics(writeResult.Diagnostics));
        Assert.True(acknowledgeResult.Succeeded, FormatDiagnostics(acknowledgeResult.Diagnostics));
        Assert.Equal(new byte[] { 0x1, 0x2, 0x3 }, writeResult.Bytes);
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, service.CreateBlankSaveCalls);
        Assert.Single(service.WrittenSaves);
        Assert.Empty(viewModel.Diagnostics);
    }

    [Fact]
    public void ClearSaveResetsBlankSaveStateToNoOpenSave()
    {
        FakeSaveApplicationService service = new()
        {
            CreateBlankSaveHandler = static () => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState("", "", 0, mainCharacterLevel: 0)), []),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.CreateBlankSave();

        SaveEditorOperationResult result = viewModel.ClearSave();

        Assert.True(result.Succeeded, FormatDiagnostics(result.Diagnostics));
        AssertNoOpenSaveState(viewModel);
        Assert.Equal(1, service.CreateBlankSaveCalls);
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
            static edit =>
            {
                SetSaveNamesEdit setNames = Assert.IsType<SetSaveNamesEdit>(edit);
                Assert.Equal("Dojima", setNames.FamilyName);
                Assert.Equal("Nanako", setNames.GivenName);
            },
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
            ("SetSocialStatRank", static viewModel => viewModel.SetSocialStatRank(0, 5)),
            ("SetDay", static viewModel => viewModel.SetDay(18)),
            ("SetDayPhase", static viewModel => viewModel.SetDayPhase(4)),
            ("SetNextDay", static viewModel => viewModel.SetNextDay(19)),
            ("SetNextDayPhase", static viewModel => viewModel.SetNextDayPhase(5)),
            ("SetPartyMember", static viewModel => viewModel.SetPartyMember(1, new PartyMemberId(0x07))),
            ("AddSocialLink", static viewModel => viewModel.AddSocialLink(12)),
            ("RemoveSocialLink", static viewModel => viewModel.RemoveSocialLink(0)),
            ("SetSocialLinkLevel", static viewModel => viewModel.SetSocialLinkLevel(0, 5)),
            ("SetSocialLinkProgress", static viewModel => viewModel.SetSocialLinkProgress(0, 3)),
            ("SetSocialLinkFlag", static viewModel => viewModel.SetSocialLinkFlag(0, 2)),
            ("SetInventoryItemQuantity", static viewModel => viewModel.SetInventoryItemQuantity(257, 9)),
            ("RemoveInventoryItem", static viewModel => viewModel.RemoveInventoryItem(257)),
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
            Assert.Equal((byte)0, viewModel.MainCharacterLevel);
            Assert.Equal(0u, viewModel.MainCharacterTotalExperience);
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
    public void AcknowledgeSavedWithOpenSaveButNoPendingSerializedSaveReturnsDiagnosticAndKeepsState()
    {
        FakeSaveApplicationService service = new()
        {
            OpenHandler = static _ => new SaveOpenResult<WorkingSave>(new FakeWorkingSave(CreateState()), []),
        };
        SaveEditorViewModel viewModel = new(service);
        viewModel.OpenSave(ReadOnlyMemory<byte>.Empty);
        int openInputsBeforeAcknowledge = service.OpenInputs.Count;
        int appliedEditsBeforeAcknowledge = service.AppliedEdits.Count;
        int writtenSavesBeforeAcknowledge = service.WrittenSaves.Count;

        SaveEditorOperationResult result = viewModel.AcknowledgeSaved(default);

        Assert.False(result.Succeeded);
        SaveDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("P4GPRES003", diagnostic.Code);
        Assert.Equal(new[] { diagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.HasErrors);
        Assert.Equal(openInputsBeforeAcknowledge, service.OpenInputs.Count);
        Assert.Equal(appliedEditsBeforeAcknowledge, service.AppliedEdits.Count);
        Assert.Equal(writtenSavesBeforeAcknowledge, service.WrittenSaves.Count);
    }

    [Fact]
    public void ReportSaveFailedFallsBackToGenericDiagnosticWhenDiagnosticsMissing()
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
        int writesBeforeFirstFailure = service.WrittenSaves.Count;
        int appliedEditsBeforeFirstFailure = service.AppliedEdits.Count;

        SaveEditorOperationResult nullDiagnosticsResult = viewModel.ReportSaveFailed(firstToken, null);

        Assert.False(nullDiagnosticsResult.Succeeded);
        SaveDiagnostic nullDiagnostic = Assert.Single(nullDiagnosticsResult.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, nullDiagnostic.Severity);
        Assert.Equal("P4GPRES006", nullDiagnostic.Code);
        Assert.Equal(new[] { nullDiagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.HasErrors);
        Assert.Equal(writesBeforeFirstFailure, service.WrittenSaves.Count);
        Assert.Equal(appliedEditsBeforeFirstFailure, service.AppliedEdits.Count);

        viewModel.SetYen(42);
        SaveEditorWriteToken secondToken = AssertOperationToken(viewModel.WriteSave());
        Assert.False(viewModel.CanWrite);
        int writesBeforeSecondFailure = service.WrittenSaves.Count;
        int appliedEditsBeforeSecondFailure = service.AppliedEdits.Count;

        SaveEditorOperationResult emptyDiagnosticsResult = viewModel.ReportSaveFailed(secondToken, []);

        Assert.False(emptyDiagnosticsResult.Succeeded);
        SaveDiagnostic emptyDiagnostic = Assert.Single(emptyDiagnosticsResult.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, emptyDiagnostic.Severity);
        Assert.Equal("P4GPRES006", emptyDiagnostic.Code);
        Assert.Equal(new[] { emptyDiagnostic }, viewModel.Diagnostics);
        Assert.True(viewModel.HasSave);
        Assert.True(viewModel.CanWrite);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.HasErrors);
        Assert.Equal(writesBeforeSecondFailure, service.WrittenSaves.Count);
        Assert.Equal(appliedEditsBeforeSecondFailure, service.AppliedEdits.Count);
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
    public void PresentationAssemblyDoesNotReferenceApplicationOrSaveFormat()
    {
        HashSet<string?> referencedAssemblies = typeof(SaveEditorViewModel)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("P4G.SaveTool.Contracts", referencedAssemblies);
        Assert.DoesNotContain("P4G.SaveTool.Application", referencedAssemblies);
        Assert.DoesNotContain("P4G.SaveTool.SaveFormat", referencedAssemblies);
    }

    [Fact]
    public void PresentationProjectReferencesCatalogWithoutApplicationOrSaveFormat()
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

        Assert.Contains(
            references,
            static reference => reference.Contains("P4G.SaveTool.Catalog", StringComparison.OrdinalIgnoreCase));

        foreach (string forbiddenReference in new[]
        {
            "P4G.SaveTool.Application",
            "P4G.SaveTool.SaveFormat",
        })
        {
            Assert.DoesNotContain(
                references,
                reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PresentationResolvedLockFileGraphDoesNotContainApplicationOrSaveFormat()
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
                SetSaveNamesEdit setNames => state.WithNames(new SaveNames(setNames.FamilyName, setNames.GivenName)),
                SetYenEdit setYen => state.WithYen(setYen.Yen),
                SetPartyMemberEdit setPartyMember => state.WithPartyMember(setPartyMember.SlotIndex, new PartyMemberId(setPartyMember.MemberValue)),
                SetSocialStatRankEdit setSocialStatRank => state.WithSocialStat(
                    setSocialStatRank.StatIndex,
                    SocialStatRules.RankToPoints(setSocialStatRank.StatIndex, setSocialStatRank.Rank)),
                SetDayEdit setDay => state.WithDay((byte)setDay.Day),
                SetDayPhaseEdit setDayPhase => state.WithDayPhase((byte)setDayPhase.PhaseId),
                SetNextDayEdit setNextDay => state.WithNextDay((byte)setNextDay.Day),
                SetNextDayPhaseEdit setNextDayPhase => state.WithNextDayPhase((byte)setNextDayPhase.PhaseId),
                SetEquippedWeaponEdit setEquippedWeapon => state.WithEquippedWeapon(setEquippedWeapon.CharacterId, setEquippedWeapon.ItemId),
                SetEquippedArmorEdit setEquippedArmor => state.WithEquippedArmor(setEquippedArmor.CharacterId, setEquippedArmor.ItemId),
                SetEquippedAccessoryEdit setEquippedAccessory => state.WithEquippedAccessory(setEquippedAccessory.CharacterId, setEquippedAccessory.ItemId),
                SetEquippedCostumeEdit setEquippedCostume => state.WithEquippedCostume(setEquippedCostume.CharacterId, setEquippedCostume.ItemId),
                AddSocialLinkEdit addSocialLink => state.WithSocialLinkAdded(new SocialLinkState(addSocialLink.LinkId, 1, 0, 0)),
                RemoveSocialLinkEdit removeSocialLink => state.WithSocialLinkRemoved(removeSocialLink.SlotIndex),
                SetSocialLinkLevelEdit setSocialLinkLevel => state.WithSocialLink(
                    setSocialLinkLevel.SlotIndex,
                    state.SocialLinks[setSocialLinkLevel.SlotIndex] with { Level = setSocialLinkLevel.Level }),
                SetSocialLinkProgressEdit setSocialLinkProgress => state.WithSocialLink(
                    setSocialLinkProgress.SlotIndex,
                    state.SocialLinks[setSocialLinkProgress.SlotIndex] with { Progress = setSocialLinkProgress.Progress }),
                SetSocialLinkFlagEdit setSocialLinkFlag => state.WithSocialLink(
                    setSocialLinkFlag.SlotIndex,
                    state.SocialLinks[setSocialLinkFlag.SlotIndex] with { Flag = setSocialLinkFlag.Flag }),
                SetProtagonistPersonaSlotEdit setProtagonistPersonaSlot => state.WithProtagonistPersonaSlot(setProtagonistPersonaSlot.SlotIndex, BuildPersonaSlot(setProtagonistPersonaSlot.PersonaSlot, state.ProtagonistPersonaSlots[setProtagonistPersonaSlot.SlotIndex])),
                SetPartyPersonaSlotEdit setPartyPersonaSlot => state.WithPartyPersonaSlot(setPartyPersonaSlot.SlotIndex, BuildPersonaSlot(setPartyPersonaSlot.PersonaSlot, state.PartyPersonaSlots[setPartyPersonaSlot.SlotIndex])),
                SetCompendiumPersonaSlotEdit setCompendiumPersonaSlot => state.WithCompendiumPersonaSlot(setCompendiumPersonaSlot.SlotIndex, BuildPersonaSlot(setCompendiumPersonaSlot.PersonaSlot, state.CompendiumPersonaSlots[setCompendiumPersonaSlot.SlotIndex])),
                ClearCompendiumPersonaSlotEdit clearCompendiumPersonaSlot => state.WithCompendiumPersonaSlot(clearCompendiumPersonaSlot.SlotIndex, CreateBlankPersonaSlot()),
                ClearCompendiumPersonaSlotsEdit => ClearCompendiumPersonaSlots(state),
                SetInventoryItemQuantityEdit setInventoryItemQuantity => state.WithInventoryItemQuantity(setInventoryItemQuantity.ItemId, setInventoryItemQuantity.Quantity),
                RemoveInventoryItemEdit removeInventoryItem => state.WithInventoryItemRemoved(removeInventoryItem.ItemId),
                _ => state,
            };
        }

        return new SaveEditResult<WorkingSave>(new FakeWorkingSave(state), []);
    }

    private static WorkingSaveState CreateState(
        string familyName = "Sato",
        string givenName = "Yu",
        uint yen = 123456u,
        IReadOnlyList<ushort>? equippedWeapons = null,
        IReadOnlyList<ushort>? equippedArmors = null,
        IReadOnlyList<ushort>? equippedAccessories = null,
        IReadOnlyList<ushort>? equippedCostumes = null,
        IReadOnlyList<PersonaSlot>? compendiumPersonaSlots = null,
        IReadOnlyList<InventoryStack>? inventoryStacks = null,
        IReadOnlyList<ushort>? socialStats = null,
        IReadOnlyList<SocialLinkState>? socialLinks = null,
        byte mainCharacterLevel = 99,
        uint mainCharacterTotalExperience = 0x0f0e0d0c,
        byte day = 18,
        byte dayPhase = 4,
        byte nextDay = 19,
        byte nextDayPhase = 5) =>
        new(
            new SaveNames(familyName, givenName),
            yen,
            [new PartyMemberId(0x01), new PartyMemberId(0xfe), new PartyMemberId(0x80)],
            equippedWeapons ?? [1, 39, 112, 150, 183, 217, 2305, 2434],
            equippedArmors ?? [256, 266, 287, 293, 307, 315, 328, 334],
            equippedAccessories ?? [512, 615, 685, 687, 754, 512, 615, 754],
            equippedCostumes ?? [1792, 2040, 1792, 2040, 1792, 2040, 1792, 2040],
            [CreatePersonaSlot(0x0101, 77, 0x01010101, 0x1101)],
            [CreatePersonaSlot(0x0202, 44, 0x02020202, 0x2201)],
            compendiumPersonaSlots ?? [CreatePersonaSlot(0x0303, 22, 0x03030303, 0x3301)],
            inventoryStacks ?? [],
            socialStats ?? [15, 30, 80, 140, 85],
            socialLinks ?? [new SocialLinkState(1, 5, 3, 2), new SocialLinkState(8, 2, 1, 0), new SocialLinkState(10, 1, 0, 1)],
            mainCharacterLevel,
            mainCharacterTotalExperience,
            day,
            dayPhase,
            nextDay,
            nextDayPhase);

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

    private static PersonaSlot BuildPersonaSlot(PersonaSlotEdit edit, PersonaSlot currentSlot) =>
        new(
            currentSlot.ExistsRawByte,
            currentSlot.Unknown0,
            edit.PersonaId,
            edit.PersonaId != 0 && edit.Level == 0 ? (byte)1 : edit.Level,
            currentSlot.ReservedAfterLevel,
            edit.TotalExperience,
            edit.SkillIds,
            edit.Strength,
            edit.Magic,
            edit.Endurance,
            edit.Agility,
            edit.Luck);

    private static WorkingSaveState ClearCompendiumPersonaSlots(WorkingSaveState state)
    {
        for (int slotIndex = 0; slotIndex < state.CompendiumPersonaSlots.Count; slotIndex++)
        {
            state = state.WithCompendiumPersonaSlot(slotIndex, CreateBlankPersonaSlot());
        }

        return state;
    }

    private static PersonaSlot CreateBlankPersonaSlot() =>
        new(
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

    private static string FormatDiagnostics(IReadOnlyList<SaveDiagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(static diagnostic =>
                $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Target}: {diagnostic.Message}"));

    private static void AssertPartyMemberEdit(SaveEditCommand edit, int slotIndex, byte memberValue)
    {
        SetPartyMemberEdit partyMemberEdit = Assert.IsType<SetPartyMemberEdit>(edit);
        Assert.Equal(slotIndex, partyMemberEdit.SlotIndex);
        Assert.Equal(memberValue, partyMemberEdit.MemberValue);
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

    private static void AssertNoOpenSaveState(SaveEditorViewModel viewModel)
    {
        Assert.False(viewModel.HasSave);
        Assert.False(viewModel.CanWrite);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.FamilyName);
        Assert.Equal(string.Empty, viewModel.GivenName);
        Assert.Equal(0u, viewModel.Yen);
        Assert.Empty(viewModel.PartyMembers);
        Assert.Empty(viewModel.PartyMemberChoices);
        Assert.Empty(viewModel.ProtagonistPersonaSlots);
        Assert.Empty(viewModel.PartyPersonaSlots);
        Assert.Empty(viewModel.CompendiumPersonaSlots);
        Assert.Empty(viewModel.InventoryEntries);
        Assert.Empty(viewModel.EquipmentCharacters);
        Assert.Empty(viewModel.SocialStats);
        Assert.Empty(viewModel.SocialLinks);
        Assert.Equal(new CalendarViewState(0, 0, 0, 0), viewModel.Calendar);
        Assert.Empty(viewModel.Diagnostics);
        Assert.False(viewModel.HasDiagnostics);
        Assert.False(viewModel.HasErrors);
    }

    private sealed class FakeWorkingSave(WorkingSaveState state) : WorkingSave(state);

    private sealed class FakeSaveApplicationService : ISaveApplicationService
    {
        public Func<ReadOnlyMemory<byte>, SaveOpenResult<WorkingSave>>? OpenHandler { get; init; }

        public Func<SaveOpenResult<WorkingSave>>? CreateBlankSaveHandler { get; init; }

        public Func<WorkingSave, IEnumerable<SaveEditCommand>, SaveEditResult<WorkingSave>>? ApplyEditsHandler { get; init; }

        public Func<WorkingSave, SaveWriteResult>? WriteHandler { get; init; }

        public List<byte[]> OpenInputs { get; } = [];

        public int CreateBlankSaveCalls { get; private set; }

        public List<SaveEditCommand[]> AppliedEdits { get; } = [];

        public List<WorkingSave> WrittenSaves { get; } = [];

        public SaveOpenResult<WorkingSave> Open(ReadOnlyMemory<byte> bytes)
        {
            OpenInputs.Add(bytes.ToArray());
            return OpenHandler is null
                ? new SaveOpenResult<WorkingSave>(null, [new SaveDiagnostic(DiagnosticSeverity.Error, "FAKE001", "No open handler.", "Fake")])
                : OpenHandler(bytes);
        }

        public SaveOpenResult<WorkingSave> CreateBlankSave()
        {
            CreateBlankSaveCalls++;
            return CreateBlankSaveHandler is null
                ? new SaveOpenResult<WorkingSave>(null, [new SaveDiagnostic(DiagnosticSeverity.Error, "FAKE003", "No blank-save handler.", "Fake")])
                : CreateBlankSaveHandler();
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
