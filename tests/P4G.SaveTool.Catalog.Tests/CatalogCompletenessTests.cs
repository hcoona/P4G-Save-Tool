using System.Security.Cryptography;
using System.Text;
using P4G.SaveTool.Catalog;
using Xunit;

namespace P4G.SaveTool.Catalog.Tests;

public sealed class CatalogCompletenessTests
{
    [Fact]
    public void ArcanaCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.Arcana,
            P4GCatalog.ArcanaById,
            "fec94de27915b2c830f3993707b4f230114fa8115fbf77b22fdf2c4bddb06dbc",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void CalendarPhaseCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.CalendarPhases,
            P4GCatalog.CalendarPhasesById,
            "42d47b349e286ecf85e8ec7d6c97cd5aaa16c55d1deccb4543e67216b88dbeb6",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void PartyMemberCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.PartyMembers,
            P4GCatalog.PartyMembersById,
            "91389c644b75e852126832fad4f4ae3d2212ce159f4a66c9513f14da1280f621",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void ItemCategoryCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.ItemCategories,
            P4GCatalog.ItemCategoriesById,
            "deb4031c6087e164c866c708caa9623826f1c9371a57f805c6bf0f9bebcf6259",
            static entry => $"{(byte)entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void SocialLinkCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.SocialLinks,
            P4GCatalog.SocialLinksById,
            "3fea31d93d4f5f2048b28816928c1efebbb0cf9f476dfe0cd30c0643300333ff",
            static entry => $"{entry.Id}|{entry.Name}|{entry.ArcanaId}",
            static entry => entry.Id);
    }

    [Fact]
    public void PersonaCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.Personas,
            P4GCatalog.PersonasById,
            "26689c5c5606fdfe5c7783f63dbf29d66b42304ae79aea4a9d2ecbdd37182082",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void SkillCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.Skills,
            P4GCatalog.SkillsById,
            "a328d002484df3205ee3b1f24000d9cd2a28fd8a2d6725e301214c5e8f5d7419",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void ItemCatalogMatchesExpectedSnapshot()
    {
        AssertCatalogSnapshot(
            P4GCatalog.Items,
            P4GCatalog.ItemsById,
            "1397b76ce5f3b491b7d9931be66bbcd6604a2aba90d1f2ac64dfb8c8b23d6fd0",
            static entry => $"{entry.Id}|{entry.Name}",
            static entry => entry.Id);
    }

    [Fact]
    public void CatalogCollectionsAreReadOnly()
    {
        AssertReadOnly(P4GCatalog.Arcana);
        AssertReadOnly(P4GCatalog.CalendarPhases);
        AssertReadOnly(P4GCatalog.PartyMembers);
        AssertReadOnly(P4GCatalog.ItemCategories);
        AssertReadOnly(P4GCatalog.SocialLinks);
        AssertReadOnly(P4GCatalog.Personas);
        AssertReadOnly(P4GCatalog.Skills);
        AssertReadOnly(P4GCatalog.Items);
    }

    private static void AssertCatalogSnapshot<TEntry, TKey>(
        IReadOnlyList<TEntry> entries,
        IReadOnlyDictionary<TKey, TEntry> lookup,
        string expectedHash,
        Func<TEntry, string> serializeEntry,
        Func<TEntry, TKey> keySelector)
        where TKey : notnull
    {
        Assert.Equal(expectedHash, ComputeCatalogHash(entries, serializeEntry));
        Assert.Equal(entries.Count, lookup.Count);

        foreach (TEntry entry in entries)
        {
            TKey key = keySelector(entry);
            Assert.True(lookup.ContainsKey(key), $"Missing lookup entry for key {key}.");
            TEntry mapped = lookup[key];
            Assert.Equal(entry, mapped);
        }
    }

    private static string ComputeCatalogHash<TEntry>(IEnumerable<TEntry> entries, Func<TEntry, string> serializeEntry)
    {
        string payload = string.Join('\n', entries.Select(serializeEntry));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> collection)
    {
        Assert.False(collection.GetType().IsArray, $"Collection exposes mutable array type {collection.GetType()}.");
        if (collection is IList<T> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.NotEmpty(list);
            Assert.Throws<NotSupportedException>(() => list[0] = list[0]);
            Assert.Throws<NotSupportedException>(() => list.Add(list[0]));
        }
    }
}
