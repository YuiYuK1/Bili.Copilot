// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 分段式播放进度条：指针交互与悬浮反馈.
/// </summary>
public sealed partial class ChapterSegmentBar
{
    private async void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsInteractive || Duration <= 0 || _canvas.ActualWidth <= 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(_canvas);
        var x = point.Position.X;

        // 空降标记优先于普通按位置跳转。目标是片段终点（裁剪到视频时长）.
        var marker = HitTestMarker(x, point.Position.Y);
        if (marker is SponsorBlockMarker hit
            && TryGetSponsorSegment(hit.SegmentIndex, out var sponsorSegment))
        {
            e.Handled = true;
            await SeekAsync(Math.Min(sponsorSegment.End, Duration));
            return;
        }

        var segment = HitTest(x);
        _isPressed = true;
        _isDragging = false;
        _pressX = x;

        // 有章节时，轨道上下边缘代表“跳到本章节起点”；无章节时
        // IsOnVerticalEdge 恒为 false，整条高度都用于精确跳转.
        _pressOnEdge = segment is not null && IsOnVerticalEdge(point.Position.Y);
        if (_pressOnEdge)
        {
            _edgeChapterStart = segment!.Start;
        }

        CapturePointer(e.Pointer);
        e.Handled = true;

        UpdateThumbScale();

        // 按下即跳转：不依赖 PointerReleased，避免释放事件被吞
        // （或先触发 PointerExited）时点击无效、需要再点一次.
        if (_pressOnEdge)
        {
            await SeekAsync(_edgeChapterStart);
        }
        else
        {
            await SeekToXAsync(_pressX);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!IsInteractive)
        {
            return;
        }

        var point = e.GetCurrentPoint(_canvas);
        var x = point.Position.X;

        if (_isPressed)
        {
            if (!_isDragging && Math.Abs(x - _pressX) >= DragThreshold)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                UpdateHoverPopup(x);
                SetDragPosition(x);
                e.Handled = true;
            }

            return;
        }

        var markerHit = HitTestMarker(x, point.Position.Y);
        if (markerHit != _hoveredMarker)
        {
            SetHoveredMarker(markerHit);
        }

        var hovered = HitTest(x);
        if (hovered?.Start != _hovered?.Start)
        {
            SetHovered(hovered);
        }

        // 标记可点击，用与章节边缘一致的手形光标提示.
        UpdateCursor(markerHit is not null || IsOnVerticalEdge(point.Position.Y));
        UpdateHoverPopup(x);
    }

    private async void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var wasPressed = _isPressed;
        var wasDragging = _isDragging;
        var dragTarget = _dragPosition;
        _isPressed = false;
        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
        UpdateThumbScale();

        if (wasPressed && wasDragging && dragTarget is double target)
        {
            // 拖动结束再补一次精确落点，弥补节流期间的偏差.
            // 单击的跳转已在按下时完成，这里不再重复，避免一次点击触发两次 seek.
            await SeekAsync(target);
        }

        _pressOnEdge = false;
        _dragPosition = null;
        UpdateFill();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetHovered(null);
        UpdateCursor(false);
        HideHoverPopup();

        if (_isPressed)
        {
            _isPressed = false;
            _isDragging = false;
        }

        UpdateThumbScale();
    }

    /// <summary>
    /// 拖动中：视觉填充立即跟手，真实跳转按 <see cref="SeekThrottleMs"/> 节流，
    /// 否则每次指针移动都触发一次 seek 会明显卡顿.
    /// </summary>
    private void SetDragPosition(double x)
    {
        var duration = Duration;
        var width = _canvas.ActualWidth;
        if (duration <= 0 || width <= 0)
        {
            return;
        }

        var target = Math.Clamp(x / width, 0, 1) * duration;
        _dragPosition = target;
        UpdateFill();

        var now = Environment.TickCount64;
        if (_seekInFlight || now - _lastSeekTicks < SeekThrottleMs)
        {
            return;
        }

        _lastSeekTicks = now;
        _ = PushSeekAsync(target);
    }

    private async Task PushSeekAsync(double target)
    {
        _seekInFlight = true;
        try
        {
            await SeekAsync(target);
        }
        finally
        {
            _seekInFlight = false;
        }
    }

    private void UpdateHoverPopup(double x)
    {
        var duration = Duration;
        var width = _canvas.ActualWidth;
        if (duration <= 0 || width <= 0)
        {
            HideHoverPopup();
            return;
        }

        // 先写入文本再显示，否则可见瞬间内容为空，会出现空白浮层.
        // 悬浮在空降标记上时文本已在 SetHoveredMarker 中写入，此处不覆盖.
        if (_hoveredMarker is null)
        {
            UpdatePopupText(x, duration, width);
        }

        if (_hoverPopup.Visibility == Visibility.Visible && Math.Abs(x - _lastPopupX) < PopupMoveThreshold)
        {
            return;
        }

        _lastPopupX = x;

        const double EstimatedPopupWidth = 150d;
        var popupWidth = _hoverPopup.ActualWidth > 1 ? _hoverPopup.ActualWidth : EstimatedPopupWidth;
        Canvas.SetLeft(_hoverPopup, Math.Clamp(x - (popupWidth / 2), 0, Math.Max(width - popupWidth, 0)));

        // 浮层画在轨道正上方。控件高度只够放轨道，故用负的 Top 越出上边界；
        // Canvas 默认不裁剪子元素，因此既不遮挡轨道，也不改变布局与对齐.
        if (_hoverPopupHeight <= 0)
        {
            _hoverPopup.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var measured = _hoverPopup.DesiredSize.Height;
            _hoverPopupHeight = measured > 1 ? measured : 26d;
        }

        var trackTop = (LayoutHeight - TrackHeight) / 2d;
        Canvas.SetTop(_hoverPopup, trackTop - _hoverPopupHeight - 6d);

        if (_hoverPopup.Visibility != Visibility.Visible)
        {
            _hoverPopup.Visibility = Visibility.Visible;
            AnimatePopupOpacity(0d, 1d);
        }
    }

    /// <summary>
    /// 悬浮层淡入，避免硬切造成的突兀.
    /// </summary>
    private void AnimatePopupOpacity(double from, double to)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(PopupFadeMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        Storyboard.SetTarget(animation, _hoverPopup);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void UpdatePopupText(double x, double duration, double width)
    {
        var target = Math.Clamp(x / width, 0, 1) * duration;

        // 时间精确到秒才重设文本；NaN 表示无有效缓存，必须重设.
        if (!double.IsNaN(_lastPopupTime) && Math.Abs(target - _lastPopupTime) < 1d)
        {
            return;
        }

        _lastPopupTime = target;
        var segment = HitTest(x);
        _hoverTime.Text = FormatTime(target);
        _hoverTitle.Text = segment?.Title ?? string.Empty;
        _hoverTitle.Visibility = string.IsNullOrEmpty(segment?.Title) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideHoverPopup()
    {
        if (_hoverPopup.Visibility != Visibility.Collapsed)
        {
            _hoverPopup.Visibility = Visibility.Collapsed;
        }

        _lastPopupX = double.NaN;
        _lastPopupTime = double.NaN;
        _hoveredMarker = null;
    }

    private void SetHovered(SegmentVisual? segment)
    {
        // 无章节时整条只有一个分段，放大它没有意义，反而像进度条在闪动.
        if (_hasChapters)
        {
            if (_hovered is not null)
            {
                AnimateSegment(_hovered, TrackHeight);
            }

            _hovered = segment;

            if (_hovered is not null)
            {
                AnimateSegment(_hovered, HoverTrackHeight);
            }
        }
        else
        {
            _hovered = segment;
        }

        UpdateThumbScale();
    }

    /// <summary>
    /// 圆点缩放：按下 <see cref="ThumbPressedScale"/>、悬浮 1.0、静息 <see cref="ThumbRestScale"/>，
    /// 三态取值与原生 Slider 模板一致.
    /// </summary>
    private void UpdateThumbScale()
    {
        var target = _isPressed
            ? ThumbPressedScale
            : _hovered is not null
                ? 1d
                : ThumbRestScale;

        AnimateThumbScale(target);
    }

    /// <summary>
    /// 缩放圆点内层。Transform 动画不参与布局，因此无需 EnableDependentAnimation.
    /// </summary>
    private void AnimateThumbScale(double target)
    {
        // 复用同一个 Storyboard 与其动画，只改目标值，避免每次悬浮都重新构建时间线.
        if (_thumbScaleStoryboard is null)
        {
            _thumbScaleStoryboard = new Storyboard();
            _thumbScaleAnimations = new DoubleAnimation[2];

            for (var i = 0; i < 2; i++)
            {
                var prop = i == 0 ? "ScaleX" : "ScaleY";
                var animation = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(120)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };

                Storyboard.SetTarget(animation, _thumbInner);
                Storyboard.SetTargetProperty(animation, "(UIElement.RenderTransform).(CompositeTransform." + prop + ")");
                _thumbScaleStoryboard.Children.Add(animation);
                _thumbScaleAnimations[i] = animation;
            }
        }

        foreach (var animation in _thumbScaleAnimations!)
        {
            animation.To = target;
        }

        _thumbScaleStoryboard.Begin();
    }

    /// <summary>
    /// 让分段向上下同时变粗。动画 Height 与圆角半径，使胶囊端点始终保持正圆；
    /// 用纵向缩放虽然更省性能，但会把圆角压扁，视觉上圆角不会变化.
    /// </summary>
    private static void AnimateSegment(SegmentVisual segment, double targetHeight)
    {
        var radius = targetHeight / 2d;
        var top = (HitAreaHeight - targetHeight) / 2d;

        // 每个分段自持一个 Storyboard，首次使用时构建，之后只更新目标值.
        segment.EnsureHeightStoryboard();

        var animations = segment.HeightAnimations;
        for (var i = 0; i < 3; i++)
        {
            // 每层 3 个动画：Height、RadiusX、RadiusY、Canvas.Top 共 4 个.
            var offset = i * 4;
            animations[offset].To = targetHeight;
            animations[offset + 1].To = radius;
            animations[offset + 2].To = radius;
            animations[offset + 3].To = top;
        }

        segment.HeightStoryboard.Begin();
    }

    private SegmentVisual? HitTest(double x)
    {
        foreach (var segment in _segments)
        {
            var left = Canvas.GetLeft(segment.Container);
            if (x >= left && x <= left + segment.Container.Width + SegmentGap)
            {
                return segment;
            }
        }

        return null;
    }

    private async Task SeekToXAsync(double x)
    {
        var duration = Duration;
        var width = _canvas.ActualWidth;
        if (ViewModel is null || duration <= 0 || width <= 0)
        {
            return;
        }

        await SeekAsync(Math.Clamp(x / width, 0, 1) * duration);
    }

    private async Task SeekAsync(double target)
    {
        if (ViewModel is null || Duration <= 0)
        {
            return;
        }

        var clamped = Math.Clamp(target, 0, Duration);

        // 与播放器内部判定保持一致：目标离当前位置太近时它根本不会跳转，
        // 此时若仍记录乐观位置，进度条就会停在原处骗人.
        if (Math.Abs(Position - clamped) < MinSeekDelta)
        {
            _pendingSeekPosition = null;
            UpdateFill();
            return;
        }

        // 先记录目标位置并刷新填充，让点击立即有视觉反馈；
        // 播放器回报新 Position 后，由 OnPlayerPropertyChanged 收敛掉该值.
        _pendingSeekPosition = clamped;
        _pendingSeekDeadline = Environment.TickCount64 + PendingSeekTtlMs;
        UpdateFill();

        // PreviewPosition 属于播放器手势预览的语义，不用于传递进度条拖动位置.
        await ViewModel.ChangePositionAsync(clamped);
        UpdateFill();
    }
}
