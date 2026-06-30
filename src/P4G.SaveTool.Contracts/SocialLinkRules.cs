namespace P4G.SaveTool.Contracts;

public static class SocialLinkRules
{
    private const byte BlankSocialLinkId = 0;
    private const byte MaximumLegacyAddSocialLinkId = 34;

    public static bool IsSupportedAddLinkId(byte linkId) =>
        linkId is not BlankSocialLinkId && linkId <= MaximumLegacyAddSocialLinkId;
}
