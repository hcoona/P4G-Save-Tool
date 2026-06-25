namespace P4G.SaveTool.Presentation;

public sealed record PartyMemberChoiceViewState(byte MemberId, string Name, bool IsUnknown = false);
