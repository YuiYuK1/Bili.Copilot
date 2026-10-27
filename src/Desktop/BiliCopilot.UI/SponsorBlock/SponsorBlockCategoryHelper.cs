// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手类别显示名称.
/// </summary>
public static class SponsorBlockCategoryHelper
{
    public static string GetDisplayName(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => "赞助广告",
            SponsorBlockCategory.SelfPromo => "自我推广",
            SponsorBlockCategory.ExclusiveAccess => "独家内容",
            SponsorBlockCategory.Interaction => "互动提示",
            SponsorBlockCategory.PoiHighlight => "高亮时刻",
            SponsorBlockCategory.Intro => "开场动画",
            SponsorBlockCategory.Outro => "结尾感谢",
            SponsorBlockCategory.Preview => "预告",
            SponsorBlockCategory.Padding => "填充",
            SponsorBlockCategory.Filler => "废话",
            SponsorBlockCategory.MusicOfftopic => "无关音乐",
            _ => "未知片段",
        };
}
