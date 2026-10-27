// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手类别解析器.
/// </summary>
public static class SponsorBlockCategoryParser
{
    public static SponsorBlockCategory Parse(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "sponsor" => SponsorBlockCategory.Sponsor,
            "selfpromo" => SponsorBlockCategory.SelfPromo,
            "exclusive_access" => SponsorBlockCategory.ExclusiveAccess,
            "interaction" => SponsorBlockCategory.Interaction,
            "poi_highlight" => SponsorBlockCategory.PoiHighlight,
            "intro" => SponsorBlockCategory.Intro,
            "outro" => SponsorBlockCategory.Outro,
            "preview" => SponsorBlockCategory.Preview,
            "padding" => SponsorBlockCategory.Padding,
            "filler" => SponsorBlockCategory.Filler,
            "music_offtopic" => SponsorBlockCategory.MusicOfftopic,
            _ => SponsorBlockCategory.Unknown,
        };
}
