// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型的空降助手部分.
/// </summary>
public sealed partial class PlayerViewModel
{
    private SponsorBlockController? _sponsorBlockController;
    private IReadOnlyList<SponsorBlockSegment> _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
    private CancellationTokenSource? _sponsorBlockLoadSource;
    private int _sponsorBlockGeneration;
    private bool _isSponsorBlockSeeking;
    private bool _isApplyingSponsorBlockSettings;

    /// <summary>
    /// 当前分 P 的空降助手片段列表，供进度条标记等界面复用.
    /// </summary>
    public IReadOnlyList<SponsorBlockSegment> SponsorBlockSegments => _sponsorBlockSegments;

    /// <summary>
    /// 当前播放位置所在的空降助手片段.
    /// </summary>
    public SponsorBlockSegment? ActiveSponsorBlockSegment => SponsorBlockController.ActiveSegment;

    private SponsorBlockController SponsorBlockController
        => _sponsorBlockController ??= new SponsorBlockController(ResolveSponsorBlockBehavior);

    /// <summary>
    /// 当前分 P 的空降片段列表已更新.
    /// </summary>
    public event EventHandler? SponsorBlockSegmentsChanged;

    /// <summary>
    /// 请求显示空降助手跳过提示.
    /// </summary>
    public event EventHandler? RequestShowSponsorBlockTip;

    /// <summary>
    /// 请求隐藏空降助手跳过提示.
    /// </summary>
    public event EventHandler? RequestHideSponsorBlockTip;

    /// <summary>
    /// 是否启用空降助手.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSponsorBlockEnabled { get; set; }

    /// <summary>
    /// 是否自动跳过。关闭后 Skip 行为退化为显示跳过按钮.
    /// </summary>
    [ObservableProperty]
    public partial bool AutoSkipSponsor { get; set; }

    /// <summary>
    /// 空降助手跳过提示文本.
    /// </summary>
    [ObservableProperty]
    public partial string? SponsorBlockTipText { get; set; }

    private SponsorBlockBehavior ResolveSponsorBlockBehavior(SponsorBlockCategory category)
    {
        var behavior = SponsorBlockBehaviorSettings.Read(category);
        if (!AutoSkipSponsor && behavior == SponsorBlockBehavior.Skip)
        {
            return SponsorBlockBehavior.ShowButton;
        }

        return behavior;
    }

    [RelayCommand]
    private async Task AcceptSponsorBlockPromptAsync()
    {
        var decision = SponsorBlockController.AcceptActivePrompt(Player?.Duration ?? 0);
        if (decision.IsActionable)
        {
            HideSponsorBlockTip();
            await ApplySponsorBlockDecisionAsync(decision, _sponsorBlockGeneration);
        }
    }

    [RelayCommand]
    private void DismissSponsorBlockPrompt()
    {
        var decision = SponsorBlockController.DismissActivePrompt();
        if (decision.Kind == SponsorBlockActionKind.HideButton)
        {
            HideSponsorBlockTip();
        }
    }

    private void ResetSponsorBlock()
    {
        _sponsorBlockGeneration++;
        var previousSource = _sponsorBlockLoadSource;
        _sponsorBlockLoadSource = null;
        if (previousSource is not null)
        {
            previousSource.Cancel();
            previousSource.Dispose();
        }

        _sponsorBlockSegments = Array.Empty<SponsorBlockSegment>();
        NotifySponsorBlockSegmentsChanged();
        var resetDecision = SponsorBlockController.Reset();
        HideSponsorBlockTip();
        _isSponsorBlockSeeking = false;

        if (resetDecision is { Kind: SponsorBlockActionKind.RestoreMute, MuteState: bool restoreState })
        {
            _ = ApplySponsorBlockMuteAsync(restoreState);
        }
    }

    private void StartSponsorBlockLoading()
    {
        ResetSponsorBlock();
        ApplySponsorBlockSettings();

        if (!IsSponsorBlockEnabled || Connector is not VideoConnectorViewModel videoConnector)
        {
            return;
        }

        var bvid = videoConnector.BvId;
        var cid = videoConnector._part?.Identifier.Id;
        if (string.IsNullOrWhiteSpace(bvid) || string.IsNullOrWhiteSpace(cid))
        {
            return;
        }

        var generation = _sponsorBlockGeneration;
        var source = new CancellationTokenSource();
        _sponsorBlockLoadSource = source;
        _ = LoadSponsorBlockSegmentsAsync(bvid, cid, generation, source.Token);
    }

    private void CheckSponsorBlock(double position)
    {
        SyncSponsorBlockSettings();

        if (_sponsorBlockSegments.Count == 0
            || _isSponsorBlockSeeking
            || IsProgressChanging
            || Player?.IsPlaybackInitialized != true)
        {
            return;
        }

        if (!IsSponsorBlockEnabled)
        {
            if (SponsorBlockController.IsMuteApplied || SponsorBlockController.IsPromptActive)
            {
                var resetDecision = SponsorBlockController.Reset();
                HideSponsorBlockTip();
                if (resetDecision is { Kind: SponsorBlockActionKind.RestoreMute, MuteState: bool restoreState })
                {
                    _ = ApplySponsorBlockMuteAsync(restoreState);
                }
            }

            return;
        }

        var decision = SponsorBlockController.Evaluate(
            _sponsorBlockSegments,
            position,
            Player.Duration,
            IsMute,
            DateTimeOffset.UtcNow);

        if (decision.IsActionable)
        {
            _ = ApplySponsorBlockDecisionAsync(decision, _sponsorBlockGeneration);
        }
    }

    private async Task LoadSponsorBlockSegmentsAsync(string bvid, string cid, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var segments = await this.Get<SponsorBlockService>().GetSegmentsAsync(bvid, cid, cancellationToken);
            if (generation != _sponsorBlockGeneration)
            {
                return;
            }

            _sponsorBlockSegments = segments;
            NotifySponsorBlockSegmentsChanged();
            logger.LogDebug("空降助手已加载 {Count} 个片段", segments.Count);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "加载空降助手片段失败");
        }
    }

    private async Task ApplySponsorBlockDecisionAsync(SponsorBlockDecision decision, int generation)
    {
        if (generation != _sponsorBlockGeneration)
        {
            return;
        }

        switch (decision.Kind)
        {
            case SponsorBlockActionKind.Skip when decision.Segment is not null:
                await SkipSponsorSegmentAsync(decision.Segment, decision.TargetPosition, generation);
                break;
            case SponsorBlockActionKind.Mute
                when decision.MuteState is bool muteState && SponsorBlockController.IsMuteApplied:
                await ApplySponsorBlockMuteAsync(muteState);
                break;
            case SponsorBlockActionKind.RestoreMute:
                if (decision.MuteState is bool restoreState)
                {
                    await ApplySponsorBlockMuteAsync(restoreState);
                }

                break;
            case SponsorBlockActionKind.ShowButton when decision.Segment is not null:
                SponsorBlockTipText = string.Format(
                    ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockPromptTemplate),
                    SponsorBlockCategoryHelper.GetDisplayName(decision.Segment.Category));
                RequestShowSponsorBlockTip?.Invoke(this, EventArgs.Empty);
                break;
            case SponsorBlockActionKind.HideButton:
                HideSponsorBlockTip();
                break;
        }
    }

    private async Task ApplySponsorBlockMuteAsync(bool isMuted)
    {
        if (Client is null)
        {
            return;
        }

        try
        {
            IsMute = isMuted;
            await Client.SetMuteAsync(isMuted);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "空降助手静音状态切换失败");
        }
    }

    private void HideSponsorBlockTip()
    {
        SponsorBlockTipText = null;
        RequestHideSponsorBlockTip?.Invoke(this, EventArgs.Empty);
    }

    private async Task SkipSponsorSegmentAsync(SponsorBlockSegment segment, double targetPosition, int generation)
    {
        if (Client is null || generation != _sponsorBlockGeneration)
        {
            return;
        }

        _isSponsorBlockSeeking = true;
        try
        {
            await Client.SetCurrentPositionAsync(targetPosition);

            var categoryName = SponsorBlockCategoryHelper.GetDisplayName(segment.Category);
            var message = string.Format(
                ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockSkippedTemplate),
                categoryName);
            Window?.ShowTip(message, InfoType.Success);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "空降助手跳转失败");
        }
        finally
        {
            if (generation == _sponsorBlockGeneration)
            {
                _isSponsorBlockSeeking = false;
            }
        }
    }

    private void NotifySponsorBlockSegmentsChanged()
        => queue.TryEnqueue(() => SponsorBlockSegmentsChanged?.Invoke(this, EventArgs.Empty));

    private void ApplySponsorBlockSettings()
    {
        _isApplyingSponsorBlockSettings = true;
        try
        {
            IsSponsorBlockEnabled = SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);
            AutoSkipSponsor = SettingsToolkit.ReadLocalSetting(SettingNames.AutoSkipSponsor, true);
        }
        finally
        {
            _isApplyingSponsorBlockSettings = false;
        }
    }

    private void SyncSponsorBlockSettings()
    {
        var enabled = SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);
        var autoSkip = SettingsToolkit.ReadLocalSetting(SettingNames.AutoSkipSponsor, true);
        if (enabled != IsSponsorBlockEnabled)
        {
            IsSponsorBlockEnabled = enabled;
        }

        if (autoSkip != AutoSkipSponsor)
        {
            AutoSkipSponsor = autoSkip;
        }
    }

    partial void OnIsSponsorBlockEnabledChanged(bool value)
    {
        if (_isApplyingSponsorBlockSettings)
        {
            return;
        }

        SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockEnabled, value);
        if (value && Player?.IsPlaybackInitialized == true)
        {
            StartSponsorBlockLoading();
        }
        else if (!value)
        {
            ResetSponsorBlock();
        }
    }

    partial void OnAutoSkipSponsorChanged(bool value)
    {
        if (_isApplyingSponsorBlockSettings)
        {
            return;
        }

        SettingsToolkit.WriteLocalSetting(SettingNames.AutoSkipSponsor, value);
    }
}
