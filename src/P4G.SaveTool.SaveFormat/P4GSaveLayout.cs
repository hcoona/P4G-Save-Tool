using System.Collections.ObjectModel;

namespace P4G.SaveTool.SaveFormat;

public sealed class P4GSaveLayout
{
    private static readonly P4GSaveLayout GoldenVitaFixed = new(
        P4GSaveLayoutKind.P4GGoldenVitaFixed,
        new SaveFieldDescriptor("FamilyNameJString", 16, SaveStringCodec.EncodedNameByteLength),
        new SaveFieldDescriptor("GivenNameJString", 34, SaveStringCodec.EncodedNameByteLength),
        new SaveFieldDescriptor("Yen", 88, sizeof(uint)),
        new SaveFieldDescriptor("PartyMembers", 92, 5),
        new SaveFieldDescriptor("FamilyNamePString", 100, SaveStringCodec.EncodedNameByteLength),
        new SaveFieldDescriptor("GivenNamePString", 118, SaveStringCodec.EncodedNameByteLength),
        new SaveFieldDescriptor("Inventory", 136, 2559),
        new SaveFieldDescriptor("ProtagonistEquipment", 3360, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot1", 3492, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot2", 3624, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot3", 3756, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot4", 3888, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot5", 4020, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot6", 4152, 8),
        new SaveFieldDescriptor("PartyEquipmentSlot7", 4284, 8),
        new PersonaBlockDescriptor("ProtagonistPersonaSlots", 2700, 12, 48),
        new SaveFieldDescriptor("MainCharacterLevel", 3290, sizeof(byte)),
        new SaveFieldDescriptor("SocialStats", 3336, 10),
        new SaveFieldDescriptor("MainCharacterTotalExperience", 3348, sizeof(uint)),
        new PersonaBlockDescriptor("PartyPersonaSlots", 3492, 7, 132, 8),
        new SaveFieldDescriptor("Calendar", 6484, 11),
        new SaveFieldDescriptor("SocialLinks", 6512, 368),
        new PersonaBlockDescriptor("CompendiumPersonaSlots", 9688, 249, 48));

    private readonly ReadOnlyCollection<SaveFieldDescriptor> fieldRegions;
    private readonly ReadOnlyCollection<PersonaBlockDescriptor> personaBlocks;

    private P4GSaveLayout(
        P4GSaveLayoutKind kind,
        SaveFieldDescriptor familyNameJString,
        SaveFieldDescriptor givenNameJString,
        SaveFieldDescriptor yen,
        SaveFieldDescriptor partyMembers,
        SaveFieldDescriptor familyNamePString,
        SaveFieldDescriptor givenNamePString,
        SaveFieldDescriptor inventory,
        SaveFieldDescriptor protagonistEquipment,
        SaveFieldDescriptor partyEquipmentSlot1,
        SaveFieldDescriptor partyEquipmentSlot2,
        SaveFieldDescriptor partyEquipmentSlot3,
        SaveFieldDescriptor partyEquipmentSlot4,
        SaveFieldDescriptor partyEquipmentSlot5,
        SaveFieldDescriptor partyEquipmentSlot6,
        SaveFieldDescriptor partyEquipmentSlot7,
        PersonaBlockDescriptor protagonistPersonaSlots,
        SaveFieldDescriptor mainCharacterLevel,
        SaveFieldDescriptor socialStats,
        SaveFieldDescriptor mainCharacterTotalExperience,
        PersonaBlockDescriptor partyPersonaSlots,
        SaveFieldDescriptor calendar,
        SaveFieldDescriptor socialLinks,
        PersonaBlockDescriptor compendiumPersonaSlots)
    {
        Kind = kind;
        FamilyNameJString = familyNameJString;
        GivenNameJString = givenNameJString;
        Yen = yen;
        PartyMembers = partyMembers;
        FamilyNamePString = familyNamePString;
        GivenNamePString = givenNamePString;
        Inventory = inventory;
        ProtagonistEquipment = protagonistEquipment;
        PartyEquipmentSlots = Array.AsReadOnly(new[]
        {
            partyEquipmentSlot1,
            partyEquipmentSlot2,
            partyEquipmentSlot3,
            partyEquipmentSlot4,
            partyEquipmentSlot5,
            partyEquipmentSlot6,
            partyEquipmentSlot7,
        });
        ProtagonistPersonaSlots = protagonistPersonaSlots;
        MainCharacterLevel = mainCharacterLevel;
        SocialStats = socialStats;
        MainCharacterTotalExperience = mainCharacterTotalExperience;
        PartyPersonaSlots = partyPersonaSlots;
        Calendar = calendar;
        SocialLinks = socialLinks;
        CompendiumPersonaSlots = compendiumPersonaSlots;

        fieldRegions = Array.AsReadOnly(new[]
        {
            FamilyNameJString,
            GivenNameJString,
            Yen,
            PartyMembers,
            FamilyNamePString,
            GivenNamePString,
            Inventory,
            ProtagonistEquipment,
            partyEquipmentSlot1,
            partyEquipmentSlot2,
            partyEquipmentSlot3,
            partyEquipmentSlot4,
            partyEquipmentSlot5,
            partyEquipmentSlot6,
            partyEquipmentSlot7,
            MainCharacterLevel,
            SocialStats,
            MainCharacterTotalExperience,
            Calendar,
            SocialLinks,
        });
        personaBlocks = Array.AsReadOnly(new[]
        {
            ProtagonistPersonaSlots,
            PartyPersonaSlots,
            CompendiumPersonaSlots,
        });
        MinimumLength = Math.Max(
            fieldRegions.Max(static region => region.EndOffset),
            personaBlocks.Max(static block => block.EndOffset));
    }

    public P4GSaveLayoutKind Kind { get; }

    public SaveFieldDescriptor FamilyNameJString { get; }

    public SaveFieldDescriptor GivenNameJString { get; }

    public SaveFieldDescriptor Yen { get; }

    public SaveFieldDescriptor PartyMembers { get; }

    public SaveFieldDescriptor FamilyNamePString { get; }

    public SaveFieldDescriptor GivenNamePString { get; }

    public SaveFieldDescriptor Inventory { get; }

    public SaveFieldDescriptor ProtagonistEquipment { get; }

    public IReadOnlyList<SaveFieldDescriptor> PartyEquipmentSlots { get; }

    public PersonaBlockDescriptor ProtagonistPersonaSlots { get; }

    public SaveFieldDescriptor MainCharacterLevel { get; }

    public SaveFieldDescriptor SocialStats { get; }

    public SaveFieldDescriptor MainCharacterTotalExperience { get; }

    public PersonaBlockDescriptor PartyPersonaSlots { get; }

    public SaveFieldDescriptor Calendar { get; }

    public SaveFieldDescriptor SocialLinks { get; }

    public PersonaBlockDescriptor CompendiumPersonaSlots { get; }

    public int MinimumLength { get; }

    public IReadOnlyList<SaveFieldDescriptor> FieldRegions => fieldRegions;

    public IReadOnlyList<PersonaBlockDescriptor> PersonaBlocks => personaBlocks;

    public static P4GSaveLayout For(P4GSaveLayoutKind kind) =>
        kind switch
        {
            P4GSaveLayoutKind.P4GGoldenVitaFixed => GoldenVitaFixed,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save layout kind."),
        };
}
