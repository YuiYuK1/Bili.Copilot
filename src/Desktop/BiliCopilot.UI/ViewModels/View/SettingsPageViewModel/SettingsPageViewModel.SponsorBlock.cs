// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 设置页面视图模型的空降助手部分.
/// </summary>
public sealed partial class SettingsPageViewModel
{
    /// <summary>
    /// 空降助手按类别配置的行为.
    /// </summary>
    public ObservableCollection<SponsorBlockCategorySettingViewModel> SponsorBlockCategorySettings { get; } = [];

    private void InitializeSponsorBlockSettings()
    {
        if (SponsorBlockCategorySettings.Count > 0)
        {
            return;
        }

        foreach (var category in SponsorBlockBehaviorSettings.Categories)
        {
            SponsorBlockCategorySettings.Add(new SponsorBlockCategorySettingViewModel(
                category,
                SponsorBlockBehaviorSettings.Read(category),
                SponsorBlockCategoryHelper.GetDisplayName(category),
                SponsorBlockBehaviorSettings.Write));
        }
    }
}
