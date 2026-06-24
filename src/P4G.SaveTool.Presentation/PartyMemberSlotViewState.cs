using P4G.SaveTool.Domain;

namespace P4G.SaveTool.Presentation;

public sealed record PartyMemberSlotViewState(int SlotIndex, PartyMemberId MemberId)
{
    public byte MemberValue => MemberId.Value;
}
