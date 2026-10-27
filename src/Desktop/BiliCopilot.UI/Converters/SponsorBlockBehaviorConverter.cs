// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

/// <summary>
/// 空降助手片段行为的可读文本转换器.
/// </summary>
internal sealed partial class SponsorBlockBehaviorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is SponsorBlockBehavior behavior
            ? behavior switch
            {
                SponsorBlockBehavior.Skip => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockBehaviorSkip),
                SponsorBlockBehavior.Mute => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockBehaviorMute),
                SponsorBlockBehavior.ShowButton => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockBehaviorShowButton),
                _ => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockBehaviorNone),
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
