namespace P4G.SaveTool.Contracts;

public static class PartyMemberRules
{
    public static bool IsSupportedMemberValue(byte memberValue) =>
        memberValue is 0 or 2 or 3 or 4 or 6 or 7 or 8;

    public static byte NormalizeMemberValue(byte memberValue) =>
        IsSupportedMemberValue(memberValue) ? memberValue : (byte)0;
}
