// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 分段式播放进度条：空降片段标记.
/// </summary>
public sealed partial class ChapterSegmentBar
{
    private const double MarkerHeight = TrackHeight / 2d;
    private const double MarkerHitPadding = 4d;

    private readonly List<Rectangle> _markerVisuals = [];
    private readonly Dictionary<SponsorBlockCategory, Brush> _markerBrushes = [];
    private List<SponsorBlockMarker> _markers = [];
    private SponsorBlockMarker? _hoveredMarker;

    private void RebuildMarkers()
    {
        var width = _canvas.ActualWidth;
        _markerLayer.Width = width;
        _markerLayer.Height = LayoutHeight;

        _markers = [.. SponsorBlockProgressGeometry.Map(
            ViewModel?.SponsorBlockSegments,
            ResolveChapterRanges(),
            Duration,
            width)];

        _hoveredMarker = null;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        while (_markerVisuals.Count > _markers.Count)
        {
            var last = _markerVisuals[^1];
            _markerLayer.Children.Remove(last);
            _markerVisuals.RemoveAt(_markerVisuals.Count - 1);
        }

        while (_markerVisuals.Count < _markers.Count)
        {
            _markerVisuals.Add(CreateMarkerVisual());
        }

        var top = (LayoutHeight - TrackHeight) / 2d;
        for (var i = 0; i < _markers.Count; i++)
        {
            var marker = _markers[i];
            var visual = _markerVisuals[i];
            visual.Fill = ResolveMarkerBrush(marker.Category);
            visual.Width = marker.Width;
            Canvas.SetLeft(visual, marker.Left);
            Canvas.SetTop(visual, top);
        }
    }

    private void ClearMarkers()
    {
        if (_markers.Count == 0 && _markerVisuals.Count == 0)
        {
            return;
        }

        _markers = [];
        _hoveredMarker = null;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        foreach (var visual in _markerVisuals)
        {
            _markerLayer.Children.Remove(visual);
        }

        _markerVisuals.Clear();
    }

    private Rectangle CreateMarkerVisual()
    {
        var visual = new Rectangle
        {
            Height = MarkerHeight,
            RadiusX = MarkerHeight / 2d,
            RadiusY = MarkerHeight / 2d,
        };

        _markerLayer.Children.Add(visual);
        return visual;
    }

    private List<SponsorBlockTimeRange>? ResolveChapterRanges()
    {
        var chapters = ViewModel?.Chapters;
        if (chapters is null || chapters.Count == 0)
        {
            return null;
        }

        var duration = Duration;
        var ranges = new List<SponsorBlockTimeRange>(chapters.Count);
        foreach (var chapter in chapters)
        {
            var end = chapter.EndPosition > chapter.Position ? chapter.EndPosition : duration;
            ranges.Add(new SponsorBlockTimeRange(chapter.Position, end));
        }

        return ranges;
    }

    private Brush ResolveMarkerBrush(SponsorBlockCategory category)
    {
        if (_markerBrushes.TryGetValue(category, out var brush))
        {
            return brush;
        }

        var argb = SponsorBlockCategoryPalette.GetFallbackColor(category);
        var fallback = new SolidColorBrush(Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
        brush = ResolveResource(SponsorBlockCategoryPalette.GetBrushKey(category), fallback);
        _markerBrushes[category] = brush;
        return brush;
    }

    private SponsorBlockMarker? HitTestMarker(double x, double y)
    {
        if (!IsInteractive || _markers.Count == 0)
        {
            return null;
        }

        var trackTop = (LayoutHeight - TrackHeight) / 2d;
        var trackBottom = trackTop + TrackHeight;
        if (y < trackTop - MarkerHitPadding || y > trackBottom + MarkerHitPadding)
        {
            return null;
        }

        foreach (var marker in _markers)
        {
            if (x >= marker.Left && x <= marker.Left + marker.Width)
            {
                return marker;
            }
        }

        return null;
    }

    private void SetHoveredMarker(SponsorBlockMarker? marker)
    {
        if (_hoveredMarker == marker)
        {
            return;
        }

        _hoveredMarker = marker;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        if (marker is not SponsorBlockMarker value
            || !TryGetSponsorSegment(value.SegmentIndex, out var segment))
        {
            return;
        }

        _hoverTitle.Text = SponsorBlockCategoryHelper.GetDisplayName(value.Category);
        _hoverTitle.Visibility = Visibility.Visible;
        _hoverTime.Text = string.Format(
            ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockMarkerTimeRange),
            FormatTime(segment.Start),
            FormatTime(segment.End));
    }

    private bool TryGetSponsorSegment(int index, out SponsorBlockSegment segment)
    {
        var segments = ViewModel?.SponsorBlockSegments;
        if (segments is not null && index >= 0 && index < segments.Count)
        {
            segment = segments[index];
            return true;
        }

        segment = null!;
        return false;
    }

    private void OnSponsorBlockSegmentsChanged(object? sender, EventArgs e) => RebuildMarkers();
}
