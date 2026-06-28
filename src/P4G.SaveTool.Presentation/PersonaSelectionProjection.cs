using System.Collections.ObjectModel;
using System.Linq;
using P4G.SaveTool.Catalog;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Presentation;

internal static class PersonaSelectionProjection
{
    private static readonly (ushort Start, ushort Length)[] legacyPersonaRanges =
    [
        (1, 42),
        (44, 8),
        (53, 127),
        (182, 32),
        (224, 26),
    ];
    private static readonly (ushort Start, ushort Length)[] legacySkillRanges =
    [
        (0, 255),
        (259, 42),
        (349, 46),
        (440, 13),
        (472, 151),
    ];
    private static readonly ReadOnlyCollection<PersonaChoiceViewState> personas = CreatePersonaChoices();
    private static readonly ReadOnlyCollection<SkillChoiceViewState> skills = CreateSkillChoices();

    internal static IReadOnlyList<PartyMemberChoiceViewState> ProjectPartyMemberChoices(WorkingSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        List<PartyMemberChoiceViewState> choices = [];
        choices.Add(new PartyMemberChoiceViewState(0, FormatProtagonistName(state.Names)));
        foreach (PartyMemberCatalogEntry member in P4GCatalog.PartyMembers.Skip(1))
        {
            choices.Add(new PartyMemberChoiceViewState(member.Id, member.Name));
        }

        return Array.AsReadOnly(choices.ToArray());
    }

    internal static IReadOnlyList<PersonaChoiceViewState> GetPersonaChoices(ushort currentPersonaId, out PersonaChoiceViewState selectedChoice) =>
        ResolveChoices(personas, currentPersonaId, CreateUnknownPersonaChoice, out selectedChoice);

    internal static IReadOnlyList<SkillChoiceViewState> GetSkillChoices(ushort currentSkillId, out SkillChoiceViewState selectedChoice) =>
        ResolveChoices(skills, currentSkillId, CreateUnknownSkillChoice, out selectedChoice);

    private static ReadOnlyCollection<PersonaChoiceViewState> CreatePersonaChoices()
    {
        List<PersonaChoiceViewState> choices = [new PersonaChoiceViewState(0, "Blank")];
        AppendPersonaChoices(choices);
        return Array.AsReadOnly(choices.ToArray());
    }

    private static ReadOnlyCollection<SkillChoiceViewState> CreateSkillChoices()
    {
        List<SkillChoiceViewState> choices = [];
        AppendSkillChoices(choices);
        return Array.AsReadOnly(choices.ToArray());
    }

    private static void AppendPersonaChoices(List<PersonaChoiceViewState> choices)
    {
        foreach ((ushort Start, ushort Length) in legacyPersonaRanges)
        {
            int end = Start + Length;
            for (ushort personaId = Start; personaId < end; personaId++)
            {
                PersonaCatalogEntry persona = P4GCatalog.PersonasById[personaId];
                choices.Add(new PersonaChoiceViewState(persona.Id, persona.Name));
            }
        }
    }

    private static void AppendSkillChoices(List<SkillChoiceViewState> choices)
    {
        foreach ((ushort Start, ushort Length) in legacySkillRanges)
        {
            int end = Start + Length;
            for (ushort skillId = Start; skillId < end; skillId++)
            {
                SkillCatalogEntry skill = P4GCatalog.SkillsById[skillId];
                choices.Add(new SkillChoiceViewState(skill.Id, skill.Name));
            }
        }
    }

    private static ReadOnlyCollection<TChoice> ResolveChoices<TChoice>(
        ReadOnlyCollection<TChoice> choices,
        ushort currentId,
        Func<ushort, TChoice> createUnknownChoice,
        out TChoice selectedChoice)
        where TChoice : notnull
    {
        foreach (TChoice choice in choices)
        {
            if (choice is PersonaChoiceViewState personaChoice && personaChoice.PersonaId == currentId)
            {
                selectedChoice = choice;
                return choices;
            }

            if (choice is SkillChoiceViewState skillChoice && skillChoice.SkillId == currentId)
            {
                selectedChoice = choice;
                return choices;
            }
        }

        selectedChoice = createUnknownChoice(currentId);
        List<TChoice> projectedChoices = new(choices.Count + 1);
        projectedChoices.AddRange(choices);
        projectedChoices.Add(selectedChoice);
        return Array.AsReadOnly(projectedChoices.ToArray());
    }

    private static PersonaChoiceViewState CreateUnknownPersonaChoice(ushort personaId) =>
        new(personaId, $"Unknown persona ({personaId})", true);

    private static SkillChoiceViewState CreateUnknownSkillChoice(ushort skillId) =>
        new(skillId, $"Unknown skill ({skillId})", true);

    private static string FormatProtagonistName(SaveNames names) =>
        string.IsNullOrWhiteSpace(names.GivenName) && string.IsNullOrWhiteSpace(names.FamilyName)
            ? "Protagonist"
            : $"{names.GivenName} {names.FamilyName}".Trim();
}
