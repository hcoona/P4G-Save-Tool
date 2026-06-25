namespace P4G.SaveTool.Catalog;

public enum ItemCategoryId : byte
{
    Weapons,
    Armor,
    Accessories,
    Consumables,
    Materials,
    SkillCards,
    Books,
    Veggies,
    Minerals,
    SocialLink,
    Shelf,
    Costumes,
    Bugs,
    Fish,
    Other,
}

public readonly record struct ArcanaCatalogEntry(byte Id, string Name);
public readonly record struct CalendarPhaseCatalogEntry(byte Id, string Name);
public readonly record struct PartyMemberCatalogEntry(byte Id, string Name);
public readonly record struct PersonaCatalogEntry(ushort Id, string Name);
public readonly record struct SkillCatalogEntry(ushort Id, string Name);
public readonly record struct SocialLinkCatalogEntry(byte Id, string Name, byte ArcanaId);
public readonly record struct ItemCategoryCatalogEntry(ItemCategoryId Id, string Name);
public readonly record struct ItemCatalogEntry(ushort Id, string Name);
