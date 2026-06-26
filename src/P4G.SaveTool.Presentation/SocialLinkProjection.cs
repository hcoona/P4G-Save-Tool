using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;

namespace P4G.SaveTool.Presentation;

internal static class SocialLinkProjection
{
    private static readonly ReadOnlyCollection<SocialLinkChoiceViewState> choices = CreateChoices();

    internal static ReadOnlyCollection<SocialLinkViewState> ProjectSocialLinks(IReadOnlyList<SocialLinkState> socialLinks)
    {
        ArgumentNullException.ThrowIfNull(socialLinks);

        SocialLinkViewState[] items = new SocialLinkViewState[socialLinks.Count];
        for (int index = 0; index < socialLinks.Count; index++)
        {
            items[index] = ProjectSocialLink(index, socialLinks[index]);
        }

        return Array.AsReadOnly(items);
    }

    internal static IReadOnlyList<SocialLinkChoiceViewState> GetChoices(byte currentLinkId, out SocialLinkChoiceViewState selectedChoice)
    {
        if (currentLinkId == 0)
        {
            selectedChoice = choices[0];
            return choices;
        }

        if (choices.FirstOrDefault(choice => choice.LinkId == currentLinkId) is SocialLinkChoiceViewState choice)
        {
            selectedChoice = choice;
            return choices;
        }

        selectedChoice = new SocialLinkChoiceViewState(currentLinkId, $"Unknown ({currentLinkId})", string.Empty, false, true);
        List<SocialLinkChoiceViewState> extendedChoices = new(choices.Count + 1);
        extendedChoices.AddRange(choices);
        extendedChoices.Add(selectedChoice);
        return Array.AsReadOnly(extendedChoices.ToArray());
    }

    private static SocialLinkViewState ProjectSocialLink(int slotIndex, SocialLinkState socialLink)
    {
        if (P4GCatalog.SocialLinksById.TryGetValue(socialLink.LinkId, out SocialLinkCatalogEntry entry))
        {
            string arcanaName = P4GCatalog.ArcanaById.TryGetValue(entry.ArcanaId, out ArcanaCatalogEntry arcana)
                ? arcana.Name
                : string.Empty;

            return new SocialLinkViewState(
                slotIndex,
                socialLink.LinkId,
                entry.Name,
                arcanaName,
                socialLink.Level,
                socialLink.Progress,
                socialLink.Flag);
        }

        return new SocialLinkViewState(
            slotIndex,
            socialLink.LinkId,
            $"Unknown ({socialLink.LinkId})",
            string.Empty,
            socialLink.Level,
            socialLink.Progress,
            socialLink.Flag,
            true);
    }

    private static ReadOnlyCollection<SocialLinkChoiceViewState> CreateChoices() =>
        Array.AsReadOnly(P4GCatalog.SocialLinks
            .Select(static link =>
            {
                string arcanaName = P4GCatalog.ArcanaById.TryGetValue(link.ArcanaId, out ArcanaCatalogEntry arcana)
                    ? arcana.Name
                    : string.Empty;
                return new SocialLinkChoiceViewState(link.Id, link.Name, arcanaName, link.Id == 0);
            })
            .ToArray());
}
