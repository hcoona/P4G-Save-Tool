using System.Collections.ObjectModel;

namespace P4G.SaveTool.Catalog;

public static partial class P4GCatalog
{
    private static readonly string[] ArcanaNames =
    [
        "",
        "Fool",
        "Magician",
        "Priestess",
        "Empress",
        "Emperor",
        "Hierophant",
        "Lovers",
        "Chariot",
        "Justice",
        "Hermit",
        "Fortune",
        "Strength",
        "Hanged Man",
        "Death",
        "Temperance",
        "Devil",
        "Tower",
        "Star",
        "Moon",
        "Sun",
        "Judgement",
        "Aeon",
        "???",
        "World",
        "Jester",
        "Hunger",
        "Aeon",
        "Outsider",
        "1D",
        "1E",
        "1F",
    ];
    public static ReadOnlyCollection<ArcanaCatalogEntry> Arcana { get; } = CreateIndexedEntries(ArcanaNames, static (id, name) => new ArcanaCatalogEntry((byte)id, name));
    public static ReadOnlyDictionary<byte, ArcanaCatalogEntry> ArcanaById { get; } = CreateLookup(Arcana, static entry => entry.Id);

    private static readonly string[] CalendarPhasesNames =
    [
        "Early Morning",
        "Morning",
        "Lunchtime",
        "Afternoon",
        "After School",
        "Evening",
    ];
    public static ReadOnlyCollection<CalendarPhaseCatalogEntry> CalendarPhases { get; } = CreateIndexedEntries(CalendarPhasesNames, static (id, name) => new CalendarPhaseCatalogEntry((byte)id, name));
    public static ReadOnlyDictionary<byte, CalendarPhaseCatalogEntry> CalendarPhasesById { get; } = CreateLookup(CalendarPhases, static entry => entry.Id);

    private static readonly string[] PartyMembersNames =
    [
        "Yu Narukami",
        "Yosuke Hanamura",
        "Chie Satonaka",
        "Yukiko Amagi",
        "Rise Kujikawa",
        "Kanji Tatsumi",
        "Naoto Shirogane",
        "Teddie",
    ];
    public static ReadOnlyCollection<PartyMemberCatalogEntry> PartyMembers { get; } = CreateIndexedEntries(PartyMembersNames, static (id, name) => new PartyMemberCatalogEntry((byte)id, name));
    public static ReadOnlyDictionary<byte, PartyMemberCatalogEntry> PartyMembersById { get; } = CreateLookup(PartyMembers, static entry => entry.Id);

    private static readonly string[] ItemCategoryNames =
    [
        "Weapons",
        "Armor",
        "Accessories",
        "Consumables",
        "Materials",
        "Skill Cards",
        "Books",
        "Veggies",
        "Minerals",
        "Social Link",
        "Shelf",
        "Costumes",
        "Bugs",
        "Fish",
        "Other",
    ];
    public static ReadOnlyCollection<ItemCategoryCatalogEntry> ItemCategories { get; } = CreateIndexedEntries(ItemCategoryNames, static (id, name) => new ItemCategoryCatalogEntry((ItemCategoryId)id, name));
    public static ReadOnlyDictionary<ItemCategoryId, ItemCategoryCatalogEntry> ItemCategoriesById { get; } = CreateLookup(ItemCategories, static entry => entry.Id);

    private static readonly (string Name, byte ArcanaId)[] SocialLinkDefinitions =
    [
        ("Blank", 0),
        ("Investigation Team", 1),
        ("Nanako", 9),
        ("Rise", 7),
        ("Rise (GF)", 7),
        ("Yukiko", 3),
        ("Yukiko (GF)", 3),
        ("Yosuke", 2),
        ("Dojima", 6),
        ("Sayoko", 16),
        ("Kanji", 5),
        ("Chie", 8),
        ("Chie (GF)", 3),
        ("Fox", 10),
        ("Naoto", 11),
        ("Naoto (GF)", 11),
        ("Fellow Athletes (Kou)", 12),
        ("Fellow Athletes (Daisuke)", 12),
        ("Naoki", 13),
        ("Hisano", 14),
        ("Margaret", 4),
        ("Ai", 19),
        ("Ai (GF)", 19),
        ("Shu", 17),
        ("Teddie", 18),
        ("Yumi", 20),
        ("Yumi (GF)", 20),
        ("Ayane", 20),
        ("Ayane (GF)", 20),
        ("Eri", 15),
        ("The Seekers of Truth", 21),
        ("Adachi", 25),
        ("Adachi (Hunger)", 26),
        ("Marie", 22),
        ("Marie (GF)", 22),
    ];
    public static ReadOnlyCollection<SocialLinkCatalogEntry> SocialLinks { get; } = CreateSocialLinkEntries(SocialLinkDefinitions, static (id, name, arcanaId) => new SocialLinkCatalogEntry(id, name, arcanaId));
    public static ReadOnlyDictionary<byte, SocialLinkCatalogEntry> SocialLinksById { get; } = CreateLookup(SocialLinks, static entry => entry.Id);
}