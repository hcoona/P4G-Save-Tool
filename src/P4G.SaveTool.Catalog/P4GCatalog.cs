using System.Collections.ObjectModel;

namespace P4G.SaveTool.Catalog;

public static partial class P4GCatalog
{
    private static ReadOnlyCollection<TEntry> CreateIndexedEntries<TEntry>(string[] names, Func<int, string, TEntry> factory)
    {
        TEntry[] entries = new TEntry[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            entries[index] = factory(index, names[index]);
        }

        return Array.AsReadOnly(entries);
    }

    private static ReadOnlyCollection<TEntry> CreateSocialLinkEntries<TEntry>((string Name, byte ArcanaId)[] definitions, Func<byte, string, byte, TEntry> factory)
    {
        TEntry[] entries = new TEntry[definitions.Length];
        for (int index = 0; index < definitions.Length; index++)
        {
            (string name, byte arcanaId) = definitions[index];
            entries[index] = factory((byte)index, name, arcanaId);
        }

        return Array.AsReadOnly(entries);
    }

    private static ReadOnlyDictionary<TKey, TValue> CreateLookup<TKey, TValue>(ReadOnlyCollection<TValue> entries, Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        Dictionary<TKey, TValue> lookup = new(entries.Count);
        foreach (TValue entry in entries)
        {
            lookup.Add(keySelector(entry), entry);
        }

        return new ReadOnlyDictionary<TKey, TValue>(lookup);
    }
}
