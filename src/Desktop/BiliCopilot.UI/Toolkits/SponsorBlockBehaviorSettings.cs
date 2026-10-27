// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手按类别配置的行为设置.
/// </summary>
internal static class SponsorBlockBehaviorSettings
{
    private static readonly SponsorBlockCategory[] _categories =
    [
        SponsorBlockCategory.Sponsor,
        SponsorBlockCategory.SelfPromo,
        SponsorBlockCategory.ExclusiveAccess,
        SponsorBlockCategory.Interaction,
        SponsorBlockCategory.PoiHighlight,
        SponsorBlockCategory.Intro,
        SponsorBlockCategory.Outro,
        SponsorBlockCategory.Preview,
        SponsorBlockCategory.Padding,
        SponsorBlockCategory.Filler,
        SponsorBlockCategory.MusicOfftopic,
        SponsorBlockCategory.Unknown,
    ];

    public static IReadOnlyList<SponsorBlockCategory> Categories => _categories;

    public static SponsorBlockBehavior Read(SponsorBlockCategory category)
    {
        var defaultValue = SponsorBlockBehaviorDefaults.GetDefault(category);
        try
        {
            var value = SettingsToolkit.ReadLocalSetting(GetSettingName(category), defaultValue);
            return Enum.IsDefined(value) ? value : defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    public static void Write(SponsorBlockCategory category, SponsorBlockBehavior behavior)
        => SettingsToolkit.WriteLocalSetting(GetSettingName(category), behavior);

    private static SettingNames GetSettingName(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => SettingNames.SponsorBlockBehaviorSponsor,
            SponsorBlockCategory.SelfPromo => SettingNames.SponsorBlockBehaviorSelfPromo,
            SponsorBlockCategory.ExclusiveAccess => SettingNames.SponsorBlockBehaviorExclusiveAccess,
            SponsorBlockCategory.Interaction => SettingNames.SponsorBlockBehaviorInteraction,
            SponsorBlockCategory.PoiHighlight => SettingNames.SponsorBlockBehaviorPoiHighlight,
            SponsorBlockCategory.Intro => SettingNames.SponsorBlockBehaviorIntro,
            SponsorBlockCategory.Outro => SettingNames.SponsorBlockBehaviorOutro,
            SponsorBlockCategory.Preview => SettingNames.SponsorBlockBehaviorPreview,
            SponsorBlockCategory.Padding => SettingNames.SponsorBlockBehaviorPadding,
            SponsorBlockCategory.Filler => SettingNames.SponsorBlockBehaviorFiller,
            SponsorBlockCategory.MusicOfftopic => SettingNames.SponsorBlockBehaviorMusicOfftopic,
            _ => SettingNames.SponsorBlockBehaviorUnknown,
        };
}
