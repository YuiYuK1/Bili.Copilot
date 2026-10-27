// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Richasy.MpvKernel.Player;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 分段式播放进度条.
/// </summary>
/// <remarks>
/// 自绘实现，不依赖 Slider：每个章节是一个独立分段容器，内部依次叠放
/// 轨道 / 缓存 / 已播放，因此缓冲进度天然按章节划分。
/// 轨道在控件内垂直居中，与两侧时间文本对齐；悬浮层用负的 Canvas.Top 绘制在轨道上方.
/// </remarks>
public sealed partial class ChapterSegmentBar : PlayerControlBase
{
    private const double SegmentGap = 2d;
    private const double MinSegmentWidth = 2d;
    private const double TrackHeight = 4d;
    private const double HoverTrackHeight = 8d;
    private const double DragThreshold = 3d;

    /// <summary>
    /// 圆点内层直径，对应原生 Slider 的 SliderInnerThumbWidth/Height = 12.
    /// </summary>
    private const double ThumbSize = 12d;

    /// <summary>
    /// 原生 Slider 的 Thumb 控件尺寸，对应 SliderHorizontalThumbWidth/Height = 18.
    /// </summary>
    private const double ThumbControlSize = 18d;

    /// <summary>
    /// 圆点深色外盘直径：Thumb 模板根 Border 带 Margin=-2，故实际为 18 + 4 = 22.
    /// </summary>
    private const double ThumbOuterSize = ThumbControlSize + 4d;

    /// <summary>
    /// 原生滑块静息状态把内层缩放到 0.86，悬浮时回到 1.0.
    /// </summary>
    private const double ThumbRestScale = 0.86;

    /// <summary>
    /// 原生滑块按下时把内层缩放到 0.71，对应模板 Pressed 状态.
    /// </summary>
    private const double ThumbPressedScale = 0.71;

    /// <summary>
    /// 圆点外框圆角，对应原生 Slider 的 SliderThumbCornerRadius = 10.
    /// </summary>
    private const double ThumbCornerRadius = 10d;

    /// <summary>
    /// 悬浮层淡入时长（毫秒）.
    /// </summary>
    private const int PopupFadeMs = 80;

    /// <summary>
    /// 悬浮层位置更新的最小位移（像素），小于该值不重设坐标.
    /// </summary>
    private const double PopupMoveThreshold = 2d;

    /// <summary>
    /// 拖动时下发给播放器的跳转间隔（毫秒）。视觉填充逐帧跟随，但真实 seek 需节流.
    /// </summary>
    private const int SeekThrottleMs = 90;

    /// <summary>
    /// 判定播放器已抵达跳转目标的容差（秒）.
    /// </summary>
    private const double SeekSettleThreshold = 1.5d;

    /// <summary>
    /// 目标位置与当前播放位置的差值小于该值时不再下发跳转，
    /// 与 <see cref="PlayerViewModel.ChangePositionAsync"/> 的内部判定保持一致.
    /// </summary>
    private const double MinSeekDelta = 2d;

    /// <summary>
    /// 乐观显示位置的最长存活时间（毫秒）。播放器可能因关键帧对齐到不了目标，
    /// 必须超时清除，否则进度条会永久停在跳转点.
    /// </summary>
    private const int PendingSeekTtlMs = 2000;

    /// <summary>
    /// 控件总高：轨道在其中垂直居中，父级按中心对齐时与两侧时间文本齐平.
    /// </summary>
    private const double BarHeight = 34d;

    /// <summary>
    /// 紧凑模式高度：只留轨道自身，供底部迷你进度条使用.
    /// </summary>
    private const double CompactHeight = TrackHeight;

    /// <summary>
    /// 交互热区高度：覆盖整条控件，避免轨道过细时难以命中.
    /// </summary>
    private const double HitAreaHeight = BarHeight;

    /// <summary>
    /// 浮层圆角，对应系统 OverlayCornerRadius（4）.
    /// </summary>
    private const double OverlayCornerRadiusValue = 4d;

    /// <summary>
    /// 透明但可命中的画刷：用于分段容器，使整条热区都能接收指针.
    /// </summary>
    private static readonly Brush HitAreaBackgroundBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x01, 0, 0, 0));

    /// <summary>
    /// 资源缺失时的回退值，正常情况下都会被 Overrides.xaml 中的主题资源覆盖.
    /// </summary>
    private static readonly Brush FallbackPlayedBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFB, 0x72, 0x99));
    private static readonly Brush FallbackTrackBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF));
    private static readonly Brush FallbackCacheBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
    private static readonly Brush FallbackThumbOuterBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B));
    private static readonly Brush FallbackThumbBorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly Brush FallbackPopupBackgroundBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xF0, 0x20, 0x20, 0x20));
    private static readonly Brush FallbackPopupTimeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly Brush FallbackPopupTitleBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top,
        Height = BarHeight,
        Background = HitAreaBackgroundBrush,
    };

    private readonly List<SegmentVisual> _segments = [];
    private readonly List<(double Start, double End)> _cachedRanges = [];

    /// <summary>
    /// 空降片段标记的覆盖层。自身不参与命中测试，由控件按标记矩形统一判定.
    /// </summary>
    private readonly Canvas _markerLayer = new() { IsHitTestVisible = false };

    private readonly Ellipse _thumbInner = new()
    {
        Width = ThumbSize,
        Height = ThumbSize,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
        RenderTransform = new CompositeTransform
        {
            ScaleX = ThumbRestScale,
            ScaleY = ThumbRestScale,
        },
    };

    /// <summary>
    /// 圆点外框：结构与原生 Slider 的 Thumb 模板一致（外框 + 内层椭圆）.
    /// </summary>
    private readonly Border _thumb = new()
    {
        Width = ThumbOuterSize,
        Height = ThumbOuterSize,
        CornerRadius = new CornerRadius(ThumbCornerRadius),
        BorderThickness = new Thickness(1),
        IsHitTestVisible = false,
    };

    private readonly Border _hoverPopup = new()
    {
        // 初始必须隐藏，否则控件加载时会出现一个空白悬浮层.
        Visibility = Visibility.Collapsed,
        CornerRadius = new CornerRadius(OverlayCornerRadiusValue),
        Padding = new Thickness(8, 4, 8, 4),
        IsHitTestVisible = false,
    };

    private readonly StackPanel _hoverStack = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly TextBlock _hoverTime = new() { Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };
    private readonly TextBlock _hoverTitle = new() { Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };

    /// <summary>
    /// <see cref="ShowThumb"/> 的依赖属性：底部迷你进度条不需要圆点.
    /// </summary>
    public static readonly DependencyProperty ShowThumbProperty =
        DependencyProperty.Register(nameof(ShowThumb), typeof(bool), typeof(ChapterSegmentBar), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="Compact"/> 的依赖属性：紧凑模式用于底部迷你进度条.
    /// </summary>
    public static readonly DependencyProperty CompactProperty =
        DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(ChapterSegmentBar), new PropertyMetadata(false, OnCompactChanged));

    /// <summary>
    /// <see cref="IsInteractive"/> 的依赖属性.
    /// </summary>
    /// <remarks>
    /// 迷你进度条仅用于展示：不响应指针、不显示悬浮层、不出现悬浮态.
    /// </remarks>
    public static readonly DependencyProperty IsInteractiveProperty =
        DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(ChapterSegmentBar), new PropertyMetadata(true, OnInteractiveChanged));

    private SegmentVisual? _hovered;
    private bool _isPressed;
    private bool _isDragging;
    private double _pressX;

    /// <summary>
    /// 指针当前是否位于章节边缘热区内.
    /// </summary>
    private bool _isOnEdge;

    /// <summary>
    /// 按下时是否落在边缘热区。为 true 时点击跳转到章节起点而非精确时间.
    /// </summary>
    private bool _pressOnEdge;

    /// <summary>
    /// 边缘热区命中的章节起始时间.
    /// </summary>
    private double _edgeChapterStart;

    /// <summary>
    /// 当前光标形状，避免重复创建 InputSystemCursor.
    /// </summary>
    private InputSystemCursorShape? _cursorShape;
    private Brush? _playedBrush;
    private Brush? _trackBrush;
    private Brush? _cacheBrush;
    private double _lastPopupX = double.NaN;
    private double _lastPopupTime = double.NaN;
    private double _hoverPopupHeight;
    private long _lastSeekTicks;
    private bool _seekInFlight;

    /// <summary>
    /// 当前订阅的播放器实例。播放器是延迟创建的，视图模型设置时可能还是 null，
    /// 因此必须在 Player 属性变化后重新挂接，否则进度条收不到 Position 更新.
    /// </summary>
    private MpvPlayer? _attachedPlayer;

    /// <summary>
    /// 是否存在真实章节。无章节时只有一个整段，不应对悬浮做出放大反应.
    /// </summary>
    private bool _hasChapters;

    /// <summary>
    /// 拖动期间的预览位置，让填充立即跟随光标而不必等待播放器回调.
    /// </summary>
    private double? _dragPosition;

    /// <summary>
    /// 跳转后的目标位置。播放器上报新 Position 之前先用它绘制填充，
    /// 否则跳转瞬间会读到旧 Position 而把进度条弹回原位，看起来像是没跳到.
    /// </summary>
    private double? _pendingSeekPosition;

    /// <summary>
    /// <see cref="_pendingSeekPosition"/> 的失效时刻（<see cref="Environment.TickCount64"/> 毫秒）.
    /// </summary>
    private long _pendingSeekDeadline;

    /// <summary>
    /// 上一帧已绘制的状态，用于避免每帧重复写入 XAML.
    /// </summary>
    private double _lastAppliedPosition = double.NaN;
    private double _lastAppliedWidth = -1d;
    private double _lastAppliedDuration = -1d;

    /// <summary>
    /// 是否已挂上渲染回调.
    /// </summary>
    private bool _renderingHooked;

    /// <summary>
    /// 当前所在章节下标，用于避免每帧重算章节高亮.
    /// </summary>
    private int _currentChapterIndex = -1;

    /// <summary>
    /// 圆点缩放动画，复用以避免每次悬浮重建时间线.
    /// </summary>
    private Storyboard? _thumbScaleStoryboard;
    private DoubleAnimation[]? _thumbScaleAnimations;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterSegmentBar"/> class.
    /// </summary>
    public ChapterSegmentBar()
    {
        ApplyBarHeight();

        // 悬浮层需要越出上边界显示，显式禁用裁剪.
        Clip = null;
        _canvas.Clip = null;
        Content = _canvas;

        _hoverTime.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _hoverStack.Children.Add(_hoverTime);
        _hoverStack.Children.Add(_hoverTitle);
        _hoverPopup.Child = _hoverStack;

        _thumb.Child = _thumbInner;

        // 章节分段容器通过 Insert(0) 加在最底层，因此标记层位于分段之上、
        // 圆点与悬浮层之下.
        _canvas.Children.Add(_markerLayer);
        _canvas.Children.Add(_thumb);
        _canvas.Children.Add(_hoverPopup);

        Loaded += OnLoadedHandler;
        Unloaded += (_, _) =>
        {
            HideHoverPopup();
            StopRenderLoop();
            DetachViewModel(ViewModel);
            DetachPlayer();
        };
        SizeChanged += (_, _) => Rebuild();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerReleased;
        PointerCaptureLost += OnPointerReleased;
        PointerExited += OnPointerExited;
    }

    /// <summary>
    /// 是否使用紧凑高度（轨道贴底，无悬浮层空间）.
    /// </summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    /// <summary>
    /// 是否显示进度圆点.
    /// </summary>
    public bool ShowThumb
    {
        get => (bool)GetValue(ShowThumbProperty);
        set => SetValue(ShowThumbProperty, value);
    }

    /// <summary>
    /// 是否启用指针交互.
    /// </summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    private double Duration => ViewModel?.Player?.Duration ?? 0;

    private double Position => ViewModel?.Player?.Position ?? 0;

    /// <summary>
    /// 当前布局高度：普通模式 34px，紧凑模式仅轨道高度.
    /// </summary>
    private double LayoutHeight => Compact ? CompactHeight : BarHeight;

    private Brush PlayedBrush => _playedBrush ??= ResolveResource("SegmentBarPlayedBrush", FallbackPlayedBrush);

    private Brush TrackResolvedBrush => _trackBrush ??= ResolveResource("SegmentBarTrackBrush", FallbackTrackBrush);

    private Brush CacheResolvedBrush => _cacheBrush ??= ResolveResource("SegmentBarCacheBrush", FallbackCacheBrush);

    private void OnLoadedHandler(object sender, RoutedEventArgs e)
    {
        // Compact 由 XAML 在构造之后设置，这里按最终值再校正一次高度.
        ApplyBarHeight();
        _playedBrush = PlayedBrush;
        _trackBrush = TrackResolvedBrush;
        _cacheBrush = CacheResolvedBrush;
        ApplyThumbBrushes();
        AttachViewModel(ViewModel);
        AttachPlayer();
        Rebuild();
        StartRenderLoop();
    }

    /// <summary>
    /// 挂上渲染回调。单纯依赖 <c>PropertyChanged</c> 会在订阅时机或控件可见性
    /// 变化时漏掉更新（迷你进度条初始为 Collapsed，转为可见时可能既没有尺寸变化
    /// 事件也没有位置事件），因此每帧只比对目标位置，变化时才重绘.
    /// </summary>
    private void StartRenderLoop()
    {
        if (_renderingHooked)
        {
            return;
        }

        _renderingHooked = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRenderLoop()
    {
        if (!_renderingHooked)
        {
            return;
        }

        _renderingHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    /// <summary>
    /// 每帧检查是否需要重绘：位置、宽度或时长任一变化才写 XAML.
    /// </summary>
    private void OnRendering(object? sender, object e)
    {
        var duration = Duration;
        var width = _canvas.ActualWidth;
        if (duration <= 0 || width <= 0)
        {
            return;
        }

        if (_segments.Count == 0)
        {
            // 控件从 Collapsed 变为可见时宽度才确定，此时补建分段.
            Rebuild();
            return;
        }

        var position = ResolveDisplayPosition();
        if (Math.Abs(position - _lastAppliedPosition) < 0.04
            && Math.Abs(width - _lastAppliedWidth) < 0.5
            && Math.Abs(duration - _lastAppliedDuration) < 0.001)
        {
            return;
        }

        ApplyFill(position, duration, width);
    }

    private void ApplyBarHeight()
    {
        Height = LayoutHeight;
        _canvas.Height = LayoutHeight;

        foreach (var segment in _segments)
        {
            // 分段容器以整条热区高度绘制，并靠负的 Top 让轨道落在控件中线，
            // 因此高度变化时必须跟着重新定位，否则紧凑模式下轨道会跑到控件之外.
            Canvas.SetTop(segment.Container, (LayoutHeight - HitAreaHeight) / 2d);
            LayoutTrack(segment, TrackHeight);
        }
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ChapterSegmentBar bar)
        {
            return;
        }

        bar.ApplyBarHeight();
        bar.Rebuild();
    }

    private static void OnInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ChapterSegmentBar bar)
        {
            return;
        }

        // 切到不可交互时立即清掉悬浮态与浮层，避免残留.
        if (e.NewValue is false)
        {
            bar.SetHovered(null);
            bar.UpdateCursor(false);
            bar.HideHoverPopup();
            bar._isPressed = false;
            bar._isDragging = false;
            bar._pressOnEdge = false;
        }
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        // 基类在首次卸载后会移除自己的 Loaded 订阅，因此这里和构造函数的
        // Loaded 处理器都要负责重新挂接视图模型与播放器.
        AttachViewModel(ViewModel);
        AttachPlayer();
        Rebuild();
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(PlayerViewModel? oldValue, PlayerViewModel? newValue)
    {
        DetachViewModel(oldValue);
        DetachPlayer();

        if (newValue is not null)
        {
            AttachViewModel(newValue);
            AttachPlayer();
            Rebuild();
        }
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        StopRenderLoop();
        DetachViewModel(ViewModel);
        DetachPlayer();
    }

    private void AttachViewModel(PlayerViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.ChapterInitialized -= OnChapterInitialized;
        viewModel.ChapterInitialized += OnChapterInitialized;
        viewModel.CacheStateChanged -= OnCacheStateChanged;
        viewModel.CacheStateChanged += OnCacheStateChanged;
        viewModel.SponsorBlockSegmentsChanged -= OnSponsorBlockSegmentsChanged;
        viewModel.SponsorBlockSegmentsChanged += OnSponsorBlockSegmentsChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel(PlayerViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.ChapterInitialized -= OnChapterInitialized;
        viewModel.CacheStateChanged -= OnCacheStateChanged;
        viewModel.SponsorBlockSegmentsChanged -= OnSponsorBlockSegmentsChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void AttachPlayer()
    {
        var player = ViewModel?.Player;
        if (ReferenceEquals(player, _attachedPlayer))
        {
            return;
        }

        DetachPlayer();
        _attachedPlayer = player;
        if (_attachedPlayer is not null)
        {
            _attachedPlayer.PropertyChanged += OnPlayerPropertyChanged;
        }
    }

    private void DetachPlayer()
    {
        if (_attachedPlayer is not null)
        {
            _attachedPlayer.PropertyChanged -= OnPlayerPropertyChanged;
            _attachedPlayer = null;
        }
    }

    private void OnChapterInitialized(object? sender, EventArgs e) => Rebuild();

    private void OnCacheStateChanged(object? sender, Richasy.MpvKernel.Core.Models.MpvCacheStateEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _cachedRanges.Clear();
            foreach (var range in e.SeekableRanges)
            {
                _cachedRanges.Add((range.Start, range.End));
            }

            // 缓冲范围变化才需要重算缓存宽度.
            UpdateCache();
        });
    }

    private void OnPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MpvPlayer.Position))
        {
            // 播放器抵达跳转目标后，放弃乐观显示位置，改回真实位置.
            if (_pendingSeekPosition is double pending && Math.Abs(Position - pending) <= SeekSettleThreshold)
            {
                _pendingSeekPosition = null;
            }

            UpdateFill();

            // 只有跨过章节边界时才需要重新计算高亮，避免每帧遍历章节.
            if (ResolveCurrentChapterIndex() != _currentChapterIndex)
            {
                UpdateCurrentChapter();
            }
        }
        else if (e.PropertyName is nameof(MpvPlayer.Duration))
        {
            // 切换视频后，上一个视频的跳转目标已无意义.
            _pendingSeekPosition = null;

            // 时长在加载完成后才确定；此时重建分段才能得到正确的宽度比例.
            DispatcherQueue.TryEnqueue(Rebuild);
        }
    }

    /// <summary>
    /// 返回当前时间所属的章节下标；无章节或不在任何章节内时返回 -1.
    /// </summary>
    private int ResolveCurrentChapterIndex()
    {
        var chapters = ViewModel?.Chapters;
        if (chapters is null || chapters.Count == 0)
        {
            return -1;
        }

        var position = Position;
        for (var i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            var chapterEnd = chapter.EndPosition > chapter.Position ? chapter.EndPosition : double.MaxValue;
            if (position >= chapter.Position && position < chapterEnd)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 预览跳转（视频区域横向拖拽）期间播放位置不变，
    /// 需依赖 <c>PreviewPosition</c> 的变化驱动重绘.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.Player))
        {
            // Player 是延迟创建的：视图模型先于播放器设置时，这里必须补挂接.
            AttachPlayer();
            DispatcherQueue.TryEnqueue(Rebuild);
        }
        else if (e.PropertyName is nameof(PlayerViewModel.PreviewPosition) or nameof(PlayerViewModel.IsPreviewProgressChanging))
        {
            DispatcherQueue.TryEnqueue(UpdateFill);
        }
    }

    /// <summary>
    /// 同步章节条目的播放状态，供章节列表高亮.
    /// </summary>
    private void UpdateCurrentChapter()
    {
        var chapters = ViewModel?.Chapters;
        if (chapters is null || chapters.Count == 0)
        {
            return;
        }

        var position = Position;
        _currentChapterIndex = ResolveCurrentChapterIndex();
        for (var i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            var isPlaying = i == _currentChapterIndex;
            if (chapter.IsPlaying != isPlaying)
            {
                chapter.IsPlaying = isPlaying;
            }

            var isPlayed = chapter.EndPosition > chapter.Position && position >= chapter.EndPosition;
            if (chapter.IsPlayed != isPlayed)
            {
                chapter.IsPlayed = isPlayed;
            }
        }
    }

    /// <summary>
    /// 圆点三层画刷沿用原生 Slider 的 Thumb 定义，保证观感与系统滑块一致.
    /// </summary>
    private void ApplyThumbBrushes()
    {
        _thumbInner.Fill = ResolveResource("SegmentBarThumbBrush", PlayedBrush);
        _thumb.Background = ResolveResource("SegmentBarThumbOuterBrush", FallbackThumbOuterBrush);
        _thumb.BorderBrush = ResolveResource("SegmentBarThumbBorderBrush", FallbackThumbBorderBrush);
        _hoverPopup.Background = ResolveResource("SegmentBarPopupBackgroundBrush", FallbackPopupBackgroundBrush);
        _hoverTime.Foreground = ResolveResource("SegmentBarPopupTimeBrush", FallbackPopupTimeBrush);
        _hoverTitle.Foreground = ResolveResource("SegmentBarPopupTitleBrush", FallbackPopupTitleBrush);
    }

    private static Brush ResolveResource(string key, Brush fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush)
        {
            return brush;
        }

        return fallback;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds))
        {
            return "00:00";
        }

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? ((int)span.TotalHours).ToString() + ":" + span.Minutes.ToString("D2") + ":" + span.Seconds.ToString("D2")
            : span.Minutes.ToString("D2") + ":" + span.Seconds.ToString("D2");
    }

    private sealed class SegmentVisual(
        Canvas container,
        Rectangle track,
        Rectangle cache,
        Rectangle played)
    {
        public Canvas Container { get; } = container;

        public Rectangle Track { get; } = track;

        public Rectangle Cache { get; } = cache;

        public Rectangle Played { get; } = played;

        public double Start { get; private set; }

        public double End { get; private set; }

        public string? Title { get; private set; }

        /// <summary>
        /// 复用该分段视觉，更新其时间区间与几何位置.
        /// </summary>
        public void Update(double start, double end, string? title, double left, double width)
        {
            Start = start;
            End = end;
            Title = title;

            Container.Width = width;
            Canvas.SetLeft(Container, left);

            Track.Width = width;
        }

        /// <summary>
        /// 分段高度动画，首次使用时构建。动画的是 Height 与圆角半径，
        /// 而不是纵向缩放的 Transform —— 缩放会把圆角压成椭圆，看不出圆角变化.
        /// </summary>
        public Storyboard HeightStoryboard { get; private set; } = new();

        public DoubleAnimation[] HeightAnimations { get; private set; } = [];

        /// <summary>
        /// 为三层的 Height / RadiusX / RadiusY / Canvas.Top 各建动画，复用同一个 Storyboard.
        /// </summary>
        public void EnsureHeightStoryboard()
        {
            if (HeightAnimations.Length > 0)
            {
                return;
            }

            HeightStoryboard = new Storyboard();
            var animations = new List<DoubleAnimation>(12);
            var elements = new FrameworkElement[] { Track, Cache, Played };

            foreach (var element in elements)
            {
                var height = CreateAnimation();
                Storyboard.SetTarget(height, element);
                Storyboard.SetTargetProperty(height, "Height");
                HeightStoryboard.Children.Add(height);
                animations.Add(height);

                foreach (var prop in new[] { "RadiusX", "RadiusY" })
                {
                    var radius = CreateAnimation();
                    Storyboard.SetTarget(radius, element);
                    Storyboard.SetTargetProperty(radius, prop);
                    HeightStoryboard.Children.Add(radius);
                    animations.Add(radius);
                }

                var top = CreateAnimation();
                Storyboard.SetTarget(top, element);
                Storyboard.SetTargetProperty(top, "(Canvas.Top)");
                HeightStoryboard.Children.Add(top);
                animations.Add(top);
            }

            HeightAnimations = [.. animations];
        }

        private static DoubleAnimation CreateAnimation() => new()
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EnableDependentAnimation = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
    }
}
