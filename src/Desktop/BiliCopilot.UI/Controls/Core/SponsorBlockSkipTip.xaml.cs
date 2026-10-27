// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 空降助手的跳过提示. 显示时不抢占键盘焦点，也不拦截播放器操作.
/// </summary>
public sealed partial class SponsorBlockSkipTip : PlayerControlBase
{
    private readonly DispatcherQueueTimer _timer;
    private DateTimeOffset? _visibleTime;

    public SponsorBlockSkipTip()
    {
        InitializeComponent();
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += OnTimerTick;
    }

    public void Show()
    {
        ProgressRect.Value = 0;
        RootPanel.Visibility = Visibility.Visible;
        _visibleTime = DateTimeOffset.Now;
        _timer.Start();
    }

    public void Hide()
    {
        ProgressRect.Value = 0;
        RootPanel.Visibility = Visibility.Collapsed;
        _visibleTime = null;
        _timer.Stop();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_visibleTime.HasValue)
        {
            return;
        }

        var elapsed = (DateTimeOffset.Now - _visibleTime.Value).TotalSeconds;
        if (elapsed >= SponsorBlockController.PromptTimeout.TotalSeconds)
        {
            ViewModel.DismissSponsorBlockPromptCommand.Execute(default);
            Hide();
            return;
        }

        ProgressRect.Value = elapsed;
    }
}
