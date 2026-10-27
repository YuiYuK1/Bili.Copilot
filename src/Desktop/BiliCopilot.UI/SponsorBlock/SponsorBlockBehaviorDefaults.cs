// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手各类片段的默认行为.
/// </summary>
public static class SponsorBlockBehaviorDefaults
{
    public static SponsorBlockBehavior GetDefault(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.SelfPromo => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.ExclusiveAccess => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Intro => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Outro => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Interaction => SponsorBlockBehavior.ShowButton,
            SponsorBlockCategory.PoiHighlight => SponsorBlockBehavior.ShowButton,
            _ => SponsorBlockBehavior.None,
        };

    public static IReadOnlyDictionary<SponsorBlockCategory, SponsorBlockBehavior> CreateMap()
    {
        var map = new Dictionary<SponsorBlockCategory, SponsorBlockBehavior>();
        foreach (var category in Enum.GetValues<SponsorBlockCategory>())
        {
            map[category] = GetDefault(category);
        }

        return map;
    }
}
