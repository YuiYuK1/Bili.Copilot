// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 空降助手单个类别的行为设置项.
/// </summary>
public sealed partial class SponsorBlockCategorySettingViewModel : ObservableObject
{
    private readonly Action<SponsorBlockCategory, SponsorBlockBehavior> _onBehaviorChanged;

    public SponsorBlockCategorySettingViewModel(
        SponsorBlockCategory category,
        SponsorBlockBehavior behavior,
        string displayName,
        Action<SponsorBlockCategory, SponsorBlockBehavior> onBehaviorChanged)
    {
        Category = category;
        _behavior = behavior;
        DisplayName = displayName;
        _onBehaviorChanged = onBehaviorChanged;
    }

    public SponsorBlockCategory Category { get; }

    public string DisplayName { get; }

    public IReadOnlyList<SponsorBlockBehavior> AvailableBehaviors { get; }
        = Enum.GetValues<SponsorBlockBehavior>();

    [ObservableProperty]
    private SponsorBlockBehavior _behavior;

    partial void OnBehaviorChanged(SponsorBlockBehavior value)
        => _onBehaviorChanged(Category, value);
}
