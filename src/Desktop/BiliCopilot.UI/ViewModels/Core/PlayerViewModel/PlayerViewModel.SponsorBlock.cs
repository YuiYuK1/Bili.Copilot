// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Models.SponsorBlock;
using BiliCopilot.UI.Services.Interfaces;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型的空降助手部分.
/// </summary>
public sealed partial class PlayerViewModel
{
    private const int SponsorCheckIntervalMs = 500;
    private const int SponsorNoticeDurationMs = 2500;

    private IReadOnlyList<SponsorSegment> _sponsorSegments = [];
    private SponsorSegment? _currentSponsorSegment;
    private bool _isSkippingSponsor;
    private long _lastSponsorCheckTicks;
    private DispatcherQueueTimer? _sponsorNoticeTimer;
    private bool _isApplyingSponsorSettings;

    /// <summary>
    /// 当前视频的赞助片段.
    /// </summary>
    public IReadOnlyList<SponsorSegment> SponsorSegments => _sponsorSegments;

    /// <summary>
    /// 是否启用空降助手.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSponsorBlockEnabled { get; set; }

    /// <summary>
    /// 是否自动跳过赞助片段.
    /// </summary>
    [ObservableProperty]
    public partial bool AutoSkipSponsor { get; set; }

    /// <summary>
    /// 是否显示空降助手提示.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSponsorSkipNoticeVisible { get; set; }

    /// <summary>
    /// 空降助手提示文本.
    /// </summary>
    [ObservableProperty]
    public partial string SponsorSkipNoticeText { get; set; }

    private async Task LoadSponsorSegmentsAsync()
    {
        ApplySponsorSettings();
        ClearSponsorState();

        if (!IsSponsorBlockEnabled)
        {
            return;
        }

        if (Connector is not VideoConnectorViewModel videoConnector
            || string.IsNullOrEmpty(videoConnector.BvId))
        {
            return;
        }

        try
        {
            var service = this.Get<ISponsorBlockService>();
            var segments = await service.GetSegmentsAsync(videoConnector.BvId!);
            if (!IsSponsorBlockEnabled)
            {
                return;
            }

            _sponsorSegments = segments;
            OnPropertyChanged(nameof(SponsorSegments));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "加载空降助手片段失败：{VideoId}", videoConnector.BvId);
        }
    }

    /// <summary>
    /// 检查当前位置是否需要跳过。检查频率限制在每秒两次，避免跟随 Position 的高频事件做无谓计算.
    /// </summary>
    private async Task CheckAndSkipSponsorAsync(double position)
    {
        SyncSponsorSettingsFromStorage();

        if (!IsSponsorBlockEnabled || _sponsorSegments.Count == 0 || _isSkippingSponsor)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastSponsorCheckTicks < SponsorCheckIntervalMs)
        {
            return;
        }

        _lastSponsorCheckTicks = now;

        var segment = FindCurrentSponsorSegment(position);
        if (segment is null)
        {
            if (_currentSponsorSegment is not null && position >= _currentSponsorSegment.EndTime)
            {
                _currentSponsorSegment = null;
            }

            return;
        }

        if (ReferenceEquals(segment, _currentSponsorSegment))
        {
            return;
        }

        _currentSponsorSegment = segment;
        var categoryName = GetSponsorCategoryName(segment.Category);

        if (!AutoSkipSponsor)
        {
            ShowSponsorSkipNotice(string.Format(ResourceToolkit.GetLocalizedString(StringNames.SponsorDetected), categoryName));
            return;
        }

        var duration = Player.Duration;
        var target = Math.Min(segment.EndTime, Math.Max(duration - 0.1, position));
        if (duration <= 0 || target - position < 2)
        {
            // 片段太短，播放器会忽略过近的跳转，不做无效 seek.
            ShowSponsorSkipNotice(string.Format(ResourceToolkit.GetLocalizedString(StringNames.SponsorDetected), categoryName));
            return;
        }

        _isSkippingSponsor = true;
        try
        {
            await ChangePositionAsync(target);
            ShowSponsorSkipNotice(string.Format(ResourceToolkit.GetLocalizedString(StringNames.SponsorSkipped), categoryName));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "空降助手跳转失败");
        }
        finally
        {
            _isSkippingSponsor = false;
        }
    }

    private SponsorSegment? FindCurrentSponsorSegment(double position)
    {
        var left = 0;
        var right = _sponsorSegments.Count - 1;
        while (left <= right)
        {
            var mid = left + ((right - left) / 2);
            var segment = _sponsorSegments[mid];
            if (position < segment.StartTime - 0.5)
            {
                right = mid - 1;
            }
            else if (position >= segment.EndTime)
            {
                left = mid + 1;
            }
            else
            {
                return segment;
            }
        }

        return default;
    }

    private void ApplySponsorSettings()
    {
        var enabled = SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);
        var autoSkip = SettingsToolkit.ReadLocalSetting(SettingNames.AutoSkipSponsor, true);

        _isApplyingSponsorSettings = true;
        try
        {
            IsSponsorBlockEnabled = enabled;
            AutoSkipSponsor = autoSkip;
        }
        finally
        {
            _isApplyingSponsorSettings = false;
        }
    }

    private void SyncSponsorSettingsFromStorage()
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

    private void ClearSponsorState()
    {
        _sponsorSegments = [];
        _currentSponsorSegment = null;
        _lastSponsorCheckTicks = 0;
        OnPropertyChanged(nameof(SponsorSegments));
        IsSponsorSkipNoticeVisible = false;
    }

    private void ShowSponsorSkipNotice(string message)
    {
        SponsorSkipNoticeText = message;
        IsSponsorSkipNoticeVisible = true;

        if (_sponsorNoticeTimer is null)
        {
            _sponsorNoticeTimer = queue.CreateTimer();
            _sponsorNoticeTimer.Interval = TimeSpan.FromMilliseconds(SponsorNoticeDurationMs);
            _sponsorNoticeTimer.Tick += OnSponsorNoticeTimerTick;
        }

        _sponsorNoticeTimer.Stop();
        _sponsorNoticeTimer.Start();
    }

    private void OnSponsorNoticeTimerTick(DispatcherQueueTimer sender, object args)
    {
        IsSponsorSkipNoticeVisible = false;
        sender.Stop();
    }

    private void DisposeSponsorBlock()
    {
        if (_sponsorNoticeTimer is not null)
        {
            _sponsorNoticeTimer.Tick -= OnSponsorNoticeTimerTick;
            _sponsorNoticeTimer.Stop();
            _sponsorNoticeTimer = null;
        }

        _sponsorSegments = [];
        _currentSponsorSegment = null;
    }

    partial void OnIsSponsorBlockEnabledChanged(bool value)
    {
        if (_isApplyingSponsorSettings)
        {
            return;
        }

        SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockEnabled, value);
        if (value)
        {
            _ = LoadSponsorSegmentsAsync();
        }
        else
        {
            ClearSponsorState();
        }
    }

    partial void OnAutoSkipSponsorChanged(bool value)
    {
        if (_isApplyingSponsorSettings)
        {
            return;
        }

        SettingsToolkit.WriteLocalSetting(SettingNames.AutoSkipSponsor, value);
    }

    private static string GetSponsorCategoryName(SponsorCategory category)
        => category switch
        {
            SponsorCategory.Sponsor => "赞助广告",
            SponsorCategory.SelfPromo => "自我推广",
            SponsorCategory.ExclusiveAccess => "独家内容",
            SponsorCategory.Interaction => "互动提示",
            SponsorCategory.POI_Highlight => "高亮时刻",
            SponsorCategory.Intro => "开场动画",
            SponsorCategory.Outro => "结尾感谢",
            SponsorCategory.Preview => "预告",
            SponsorCategory.Padding => "填充",
            SponsorCategory.Filler => "废话",
            SponsorCategory.Music_Offtopic => "无关音乐",
            SponsorCategory.Unpaid => "不可跳过",
            _ => "赞助片段",
        };
}
