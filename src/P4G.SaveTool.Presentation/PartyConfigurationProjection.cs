using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Catalog;

namespace P4G.SaveTool.Presentation;

internal static class PartyConfigurationProjection
{
    private static readonly (byte MemberValue, byte? CatalogId, string FallbackName)[] legacyPartyChoices =
    [
        (0, null, "Blank"),
        (2, 1, "Yosuke"),
        (3, 2, "Chie"),
        (4, 3, "Yukiko"),
        (6, 5, "Kanji"),
        (7, 6, "Naoto"),
        (8, 7, "Teddie"),
    ];

    private static readonly ReadOnlyCollection<PartyConfigurationChoiceViewState> choices = CreateChoices();

    internal static IReadOnlyList<PartyConfigurationChoiceViewState> GetChoices(
        byte currentMemberValue,
        out PartyConfigurationChoiceViewState selectedChoice)
    {
        PartyConfigurationChoiceViewState? knownChoice = choices.FirstOrDefault(choice => choice.MemberValue == currentMemberValue);
        if (knownChoice is not null)
        {
            selectedChoice = knownChoice;
            return choices;
        }

        selectedChoice = new PartyConfigurationChoiceViewState(
            currentMemberValue,
            $"Unknown ({currentMemberValue})",
            true);
        return Array.AsReadOnly(choices.Concat([selectedChoice]).ToArray());
    }

    private static ReadOnlyCollection<PartyConfigurationChoiceViewState> CreateChoices()
    {
        List<PartyConfigurationChoiceViewState> result = [];
        foreach ((byte memberValue, byte? catalogId, string fallbackName) in legacyPartyChoices)
        {
            string name = catalogId.HasValue && P4GCatalog.PartyMembersById.TryGetValue(catalogId.Value, out PartyMemberCatalogEntry member)
                ? member.Name
                : fallbackName;
            result.Add(new PartyConfigurationChoiceViewState(memberValue, name));
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
