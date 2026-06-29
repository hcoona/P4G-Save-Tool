using System.Collections.ObjectModel;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Contracts;

public sealed class WorkingSaveState
{
    private const int EquipmentSlotCount = 8;
    private const int SocialStatCount = SocialStatRules.StatCount;
    private const int SocialLinkSlotCount = 23;

    private readonly ReadOnlyCollection<PartyMemberId> partyMembers;
    private readonly ReadOnlyCollection<ushort> equippedWeapons;
    private readonly ReadOnlyCollection<ushort> equippedArmors;
    private readonly ReadOnlyCollection<ushort> equippedAccessories;
    private readonly ReadOnlyCollection<ushort> equippedCostumes;
    private readonly ReadOnlyCollection<ushort> socialStats;
    private readonly ReadOnlyCollection<PersonaSlot> protagonistPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> partyPersonaSlots;
    private readonly ReadOnlyCollection<PersonaSlot> compendiumPersonaSlots;
    private readonly ReadOnlyCollection<InventoryStack> inventoryStacks;
    private readonly ReadOnlyCollection<SocialLinkState> socialLinks;

    public WorkingSaveState(
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<ushort> equippedWeapons,
        IReadOnlyList<ushort> equippedArmors,
        IReadOnlyList<ushort> equippedAccessories,
        IReadOnlyList<ushort> equippedCostumes,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack>? inventoryStacks = null,
        IReadOnlyList<ushort>? socialStats = null,
        IReadOnlyList<SocialLinkState>? socialLinks = null,
        byte mainCharacterLevel = 0,
        uint mainCharacterTotalExperience = 0,
        byte day = 0,
        byte dayPhase = 0,
        byte nextDay = 0,
        byte nextDayPhase = 0)
    {
        ArgumentNullException.ThrowIfNull(names);

        Names = names;
        Yen = yen;
        this.partyMembers = CopyReadOnly(partyMembers, nameof(partyMembers));
        this.equippedWeapons = CopyFixedLength(equippedWeapons, EquipmentSlotCount, nameof(equippedWeapons));
        this.equippedArmors = CopyFixedLength(equippedArmors, EquipmentSlotCount, nameof(equippedArmors));
        this.equippedAccessories = CopyFixedLength(equippedAccessories, EquipmentSlotCount, nameof(equippedAccessories));
        this.equippedCostumes = CopyFixedLength(equippedCostumes, EquipmentSlotCount, nameof(equippedCostumes));
        this.socialStats = CopyFixedLength(socialStats ?? new ushort[SocialStatCount], SocialStatCount, nameof(socialStats));
        this.protagonistPersonaSlots = CopyReadOnly(protagonistPersonaSlots, nameof(protagonistPersonaSlots));
        this.partyPersonaSlots = CopyReadOnly(partyPersonaSlots, nameof(partyPersonaSlots));
        this.compendiumPersonaSlots = CopyReadOnly(compendiumPersonaSlots, nameof(compendiumPersonaSlots));
        this.inventoryStacks = CopyReadOnly(inventoryStacks ?? Array.Empty<InventoryStack>(), nameof(inventoryStacks));
        this.socialLinks = CopyReadOnly(socialLinks ?? Array.Empty<SocialLinkState>(), nameof(socialLinks));
        if (this.socialLinks.Count > SocialLinkSlotCount)
        {
            throw new ArgumentException(
                $"Field must contain at most {SocialLinkSlotCount} values.",
                nameof(socialLinks));
        }
        MainCharacterLevel = mainCharacterLevel;
        MainCharacterTotalExperience = mainCharacterTotalExperience;
        Day = day;
        DayPhase = dayPhase;
        NextDay = nextDay;
        NextDayPhase = nextDayPhase;
    }

    public SaveNames Names { get; }

    public uint Yen { get; }

    public IReadOnlyList<PartyMemberId> PartyMembers => partyMembers;

    public IReadOnlyList<ushort> EquippedWeapons => equippedWeapons;

    public IReadOnlyList<ushort> EquippedArmors => equippedArmors;

    public IReadOnlyList<ushort> EquippedAccessories => equippedAccessories;

    public IReadOnlyList<ushort> EquippedCostumes => equippedCostumes;

    public IReadOnlyList<ushort> SocialStats => socialStats;

    public IReadOnlyList<PersonaSlot> ProtagonistPersonaSlots => protagonistPersonaSlots;

    public IReadOnlyList<PersonaSlot> PartyPersonaSlots => partyPersonaSlots;

    public IReadOnlyList<PersonaSlot> CompendiumPersonaSlots => compendiumPersonaSlots;

    public IReadOnlyList<InventoryStack> InventoryStacks => inventoryStacks;

    public IReadOnlyList<SocialLinkState> SocialLinks => socialLinks;

    public byte MainCharacterLevel { get; }

    public uint MainCharacterTotalExperience { get; }

    public byte Day { get; }

    public byte DayPhase { get; }

    public byte NextDay { get; }

    public byte NextDayPhase { get; }

    public WorkingSaveState WithNames(SaveNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return CreateState(
            names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithYen(uint yen) =>
        CreateState(
            Names,
            yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);

    public WorkingSaveState WithMainCharacterLevel(byte mainCharacterLevel) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            mainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);

    public WorkingSaveState WithMainCharacterTotalExperience(uint mainCharacterTotalExperience) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            mainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);

    public WorkingSaveState WithPartyMember(int slotIndex, PartyMemberId memberId)
    {
        if ((uint)slotIndex >= (uint)partyMembers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Party member slot is out of range.");
        }

        PartyMemberId[] updatedPartyMembers = partyMembers.ToArray();
        updatedPartyMembers[slotIndex] = memberId;
        return CreateState(
            Names,
            Yen,
            updatedPartyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithSocialStat(int statIndex, ushort points)
    {
        if ((uint)statIndex >= (uint)socialStats.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(statIndex), statIndex, "Social stat slot is out of range.");
        }

        if (socialStats[statIndex] == points)
        {
            return this;
        }

        ushort[] updatedSocialStats = socialStats.ToArray();
        updatedSocialStats[statIndex] = points;
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            updatedSocialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithDay(byte day) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            day,
            DayPhase,
            NextDay,
            NextDayPhase);

    public WorkingSaveState WithDayPhase(byte dayPhase) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            dayPhase,
            NextDay,
            NextDayPhase);

    public WorkingSaveState WithNextDay(byte nextDay) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            nextDay,
            NextDayPhase);

    public WorkingSaveState WithNextDayPhase(byte nextDayPhase) =>
        CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            nextDayPhase);

    public WorkingSaveState WithProtagonistPersonaSlot(int slotIndex, PersonaSlot personaSlot) =>
        WithPersonaSlot(protagonistPersonaSlots, slotIndex, personaSlot, nameof(slotIndex), static (state, slots) => CreateState(
            state.Names,
            state.Yen,
            state.partyMembers,
            state.equippedWeapons,
            state.equippedArmors,
            state.equippedAccessories,
            state.equippedCostumes,
            state.socialStats,
            slots,
            state.partyPersonaSlots,
            state.compendiumPersonaSlots,
            state.inventoryStacks,
            state.socialLinks,
            state.MainCharacterLevel,
            state.MainCharacterTotalExperience,
            state.Day,
            state.DayPhase,
            state.NextDay,
            state.NextDayPhase));

    public WorkingSaveState WithPartyPersonaSlot(int slotIndex, PersonaSlot personaSlot) =>
        WithPersonaSlot(partyPersonaSlots, slotIndex, personaSlot, nameof(slotIndex), static (state, slots) => CreateState(
            state.Names,
            state.Yen,
            state.partyMembers,
            state.equippedWeapons,
            state.equippedArmors,
            state.equippedAccessories,
            state.equippedCostumes,
            state.socialStats,
            state.protagonistPersonaSlots,
            slots,
            state.compendiumPersonaSlots,
            state.inventoryStacks,
            state.socialLinks,
            state.MainCharacterLevel,
            state.MainCharacterTotalExperience,
            state.Day,
            state.DayPhase,
            state.NextDay,
            state.NextDayPhase));

    public WorkingSaveState WithCompendiumPersonaSlot(int slotIndex, PersonaSlot personaSlot) =>
        WithPersonaSlot(compendiumPersonaSlots, slotIndex, personaSlot, nameof(slotIndex), static (state, slots) => CreateState(
            state.Names,
            state.Yen,
            state.partyMembers,
            state.equippedWeapons,
            state.equippedArmors,
            state.equippedAccessories,
            state.equippedCostumes,
            state.socialStats,
            state.protagonistPersonaSlots,
            state.partyPersonaSlots,
            slots,
            state.inventoryStacks,
            state.socialLinks,
            state.MainCharacterLevel,
            state.MainCharacterTotalExperience,
            state.Day,
            state.DayPhase,
            state.NextDay,
            state.NextDayPhase));

    public WorkingSaveState WithEquippedWeapon(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Weapon);

    public WorkingSaveState WithEquippedArmor(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Armor);

    public WorkingSaveState WithEquippedAccessory(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Accessory);

    public WorkingSaveState WithEquippedCostume(int characterId, ushort itemId) =>
        WithEquipment(characterId, itemId, EquipmentKind.Costume);

    public WorkingSaveState WithInventoryItemQuantity(ushort itemId, byte quantity)
    {
        int itemIndex = FindInventoryItemIndex(itemId);
        InventoryStack updatedStack = new(itemId, quantity);
        if (itemIndex >= 0)
        {
            if (inventoryStacks[itemIndex].Equals(updatedStack))
            {
                return this;
            }

            List<InventoryStack> updatedInventory = inventoryStacks.ToList();
            updatedInventory[itemIndex] = updatedStack;
            return CreateState(
                Names,
                Yen,
                partyMembers,
                equippedWeapons,
                equippedArmors,
                equippedAccessories,
                equippedCostumes,
                socialStats,
                protagonistPersonaSlots,
                partyPersonaSlots,
                compendiumPersonaSlots,
                updatedInventory,
                socialLinks,
                MainCharacterLevel,
                MainCharacterTotalExperience,
                Day,
                DayPhase,
                NextDay,
                NextDayPhase);
        }

        List<InventoryStack> insertedInventory = inventoryStacks.ToList();
        insertedInventory.Add(updatedStack);
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            insertedInventory,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithInventoryItemRemoved(ushort itemId)
    {
        int itemIndex = FindInventoryItemIndex(itemId);
        return itemIndex < 0 ? this : WithInventoryItemRemoved(itemIndex);
    }

    public WorkingSaveState WithSocialLink(int slotIndex, SocialLinkState socialLink)
    {
        if ((uint)slotIndex >= (uint)socialLinks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Social link slot is out of range.");
        }

        if (socialLinks[slotIndex].Equals(socialLink))
        {
            return this;
        }

        SocialLinkState[] updatedSocialLinks = socialLinks.ToArray();
        updatedSocialLinks[slotIndex] = socialLink;
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            updatedSocialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithSocialLinkAdded(SocialLinkState socialLink)
    {
        if (socialLink.LinkId == 0 || socialLinks.Any(existing => existing.LinkId == socialLink.LinkId))
        {
            return this;
        }

        List<SocialLinkState> updatedSocialLinks = socialLinks.ToList();
        updatedSocialLinks.Add(socialLink);
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            updatedSocialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    public WorkingSaveState WithSocialLinkRemoved(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)socialLinks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Social link slot is out of range.");
        }

        List<SocialLinkState> updatedSocialLinks = socialLinks.ToList();
        updatedSocialLinks.RemoveAt(slotIndex);
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            updatedSocialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    private static ReadOnlyCollection<T> CopyReadOnly<T>(IReadOnlyCollection<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        return Array.AsReadOnly(values.ToArray());
    }

    private static ReadOnlyCollection<T> CopyFixedLength<T>(IReadOnlyCollection<T> values, int expectedLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count != expectedLength)
        {
            throw new ArgumentException(
                $"Field must contain exactly {expectedLength} values.",
                parameterName);
        }

        return Array.AsReadOnly(values.ToArray());
    }

    private WorkingSaveState WithInventoryItemRemoved(int itemIndex)
    {
        List<InventoryStack> updatedInventory = inventoryStacks.ToList();
        updatedInventory.RemoveAt(itemIndex);
        return CreateState(
            Names,
            Yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            updatedInventory,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    private WorkingSaveState WithPersonaSlot(
        ReadOnlyCollection<PersonaSlot> slots,
        int slotIndex,
        PersonaSlot personaSlot,
        string parameterName,
        Func<WorkingSaveState, ReadOnlyCollection<PersonaSlot>, WorkingSaveState> createState)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(personaSlot);

        if ((uint)slotIndex >= (uint)slots.Count)
        {
            throw new ArgumentOutOfRangeException(parameterName, slotIndex, "Persona slot is out of range.");
        }

        if (slots[slotIndex].Equals(personaSlot))
        {
            return this;
        }

        PersonaSlot[] updatedSlots = slots.ToArray();
        updatedSlots[slotIndex] = personaSlot;
        return createState(this, Array.AsReadOnly(updatedSlots));
    }

    private WorkingSaveState WithEquipment(int characterId, ushort itemId, EquipmentKind kind)
    {
        if ((uint)characterId >= (uint)equippedWeapons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(characterId), characterId, "Equipment character slot is out of range.");
        }

        bool isUnchanged = kind switch
        {
            EquipmentKind.Weapon => equippedWeapons[characterId] == itemId,
            EquipmentKind.Armor => equippedArmors[characterId] == itemId,
            EquipmentKind.Accessory => equippedAccessories[characterId] == itemId,
            EquipmentKind.Costume => equippedCostumes[characterId] == itemId,
            _ => false,
        };
        if (isUnchanged)
        {
            return this;
        }

        ushort[] updatedWeapons = equippedWeapons.ToArray();
        ushort[] updatedArmors = equippedArmors.ToArray();
        ushort[] updatedAccessories = equippedAccessories.ToArray();
        ushort[] updatedCostumes = equippedCostumes.ToArray();
        switch (kind)
        {
            case EquipmentKind.Weapon:
                updatedWeapons[characterId] = itemId;
                break;
            case EquipmentKind.Armor:
                updatedArmors[characterId] = itemId;
                break;
            case EquipmentKind.Accessory:
                updatedAccessories[characterId] = itemId;
                break;
            case EquipmentKind.Costume:
                updatedCostumes[characterId] = itemId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported equipment kind.");
        }

        return CreateState(
            Names,
            Yen,
            partyMembers,
            updatedWeapons,
            updatedArmors,
            updatedAccessories,
            updatedCostumes,
            socialStats,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialLinks,
            MainCharacterLevel,
            MainCharacterTotalExperience,
            Day,
            DayPhase,
            NextDay,
            NextDayPhase);
    }

    private static WorkingSaveState CreateState(
        SaveNames names,
        uint yen,
        IReadOnlyList<PartyMemberId> partyMembers,
        IReadOnlyList<ushort> equippedWeapons,
        IReadOnlyList<ushort> equippedArmors,
        IReadOnlyList<ushort> equippedAccessories,
        IReadOnlyList<ushort> equippedCostumes,
        IReadOnlyList<ushort> socialStats,
        IReadOnlyList<PersonaSlot> protagonistPersonaSlots,
        IReadOnlyList<PersonaSlot> partyPersonaSlots,
        IReadOnlyList<PersonaSlot> compendiumPersonaSlots,
        IReadOnlyList<InventoryStack> inventoryStacks,
        IReadOnlyList<SocialLinkState> socialLinks,
        byte mainCharacterLevel,
        uint mainCharacterTotalExperience,
        byte day,
        byte dayPhase,
        byte nextDay,
        byte nextDayPhase) =>
        new(
            names,
            yen,
            partyMembers,
            equippedWeapons,
            equippedArmors,
            equippedAccessories,
            equippedCostumes,
            protagonistPersonaSlots,
            partyPersonaSlots,
            compendiumPersonaSlots,
            inventoryStacks,
            socialStats,
            socialLinks,
            mainCharacterLevel,
            mainCharacterTotalExperience,
            day,
            dayPhase,
            nextDay,
            nextDayPhase);

    private enum EquipmentKind
    {
        Weapon,
        Armor,
        Accessory,
        Costume,
    }

    private int FindInventoryItemIndex(ushort itemId)
    {
        for (int index = 0; index < inventoryStacks.Count; index++)
        {
            if (inventoryStacks[index].ItemId == itemId)
            {
                return index;
            }
        }

        return -1;
    }
}
