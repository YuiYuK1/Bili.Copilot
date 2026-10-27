// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降片段类别与进度条标记颜色资源的映射.
/// </summary>
public static class SponsorBlockCategoryPalette
{
    public static string GetBrushKey(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => "SponsorBlockMarkerSponsorBrush",
            SponsorBlockCategory.SelfPromo => "SponsorBlockMarkerSelfPromoBrush",
            SponsorBlockCategory.ExclusiveAccess => "SponsorBlockMarkerExclusiveAccessBrush",
            SponsorBlockCategory.Interaction => "SponsorBlockMarkerInteractionBrush",
            SponsorBlockCategory.PoiHighlight => "SponsorBlockMarkerPoiHighlightBrush",
            SponsorBlockCategory.Intro => "SponsorBlockMarkerIntroBrush",
            SponsorBlockCategory.Outro => "SponsorBlockMarkerOutroBrush",
            SponsorBlockCategory.Preview => "SponsorBlockMarkerPreviewBrush",
            SponsorBlockCategory.Padding => "SponsorBlockMarkerPaddingBrush",
            SponsorBlockCategory.Filler => "SponsorBlockMarkerFillerBrush",
            SponsorBlockCategory.MusicOfftopic => "SponsorBlockMarkerMusicOfftopicBrush",
            _ => "SponsorBlockMarkerUnknownBrush",
        };

    public static uint GetFallbackColor(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => 0xFF00D800,
            SponsorBlockCategory.SelfPromo => 0xFFFFD400,
            SponsorBlockCategory.ExclusiveAccess => 0xFF00A878,
            SponsorBlockCategory.Interaction => 0xFFCC00FF,
            SponsorBlockCategory.PoiHighlight => 0xFFFF3B8C,
            SponsorBlockCategory.Intro => 0xFF00E5E5,
            SponsorBlockCategory.Outro => 0xFF4C5BFF,
            SponsorBlockCategory.Preview => 0xFF2AA0E0,
            SponsorBlockCategory.Padding => 0xFF9E9E9E,
            SponsorBlockCategory.Filler => 0xFF8A4DFF,
            SponsorBlockCategory.MusicOfftopic => 0xFFFF9F1A,
            _ => 0xFFE53935,
        };
}
