using System.Diagnostics.CodeAnalysis;
using P4G.SaveTool.Presentation;

namespace P4G.SaveTool.WinUI;

internal static class XamlBindingPreservation
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SocialStatRankChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CalendarPhaseChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SocialLinkViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SocialLinkChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PartyMemberChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PersonaSlotViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PersonaChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SkillChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(EquipmentCharacterViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InventoryItemChoiceViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InventoryStackViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ItemCategoryViewState))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CompendiumPersonaViewState))]
    internal static void PreserveXamlBindingProperties()
    {
    }
}
