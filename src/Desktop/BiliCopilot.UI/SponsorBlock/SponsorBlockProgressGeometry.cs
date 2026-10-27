// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 进度条上的时间区间（秒）.
/// </summary>
public readonly record struct SponsorBlockTimeRange(double Start, double End);

/// <summary>
/// 空降片段在进度条上的一段绘制矩形.
/// </summary>
public readonly record struct SponsorBlockMarker(
    int SegmentIndex,
    SponsorBlockCategory Category,
    double Left,
    double Width);

/// <summary>
/// 空降片段到进度条像素位置的映射.
/// </summary>
public static class SponsorBlockProgressGeometry
{
    public const double SegmentGap = 2d;
    public const double MinSegmentWidth = 2d;

    public static IReadOnlyList<SponsorBlockMarker> Map(
        IReadOnlyList<SponsorBlockSegment>? segments,
        IReadOnlyList<SponsorBlockTimeRange>? chapters,
        double duration,
        double width)
    {
        var markers = new List<SponsorBlockMarker>();
        if (segments is null || segments.Count == 0
            || !IsFinite(duration) || duration <= 0
            || !IsFinite(width) || width <= 0)
        {
            return markers;
        }

        var spans = ResolveSpans(chapters, duration);
        var layout = ResolveLayout(spans, duration, width);

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (segment is null || !IsFinite(segment.Start) || !IsFinite(segment.End))
            {
                continue;
            }

            var segmentStart = Math.Clamp(segment.Start, 0, duration);
            var segmentEnd = Math.Clamp(segment.End, 0, duration);
            if (segmentEnd <= segmentStart)
            {
                continue;
            }

            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                var from = Math.Max(segmentStart, span.Start);
                var to = Math.Min(segmentEnd, span.End);
                if (to <= from)
                {
                    continue;
                }

                var spanLength = span.End - span.Start;
                if (spanLength <= 0)
                {
                    continue;
                }

                var (left, spanWidth) = layout[i];
                var fromRatio = (from - span.Start) / spanLength;
                var toRatio = (to - span.Start) / spanLength;
                var markerWidth = (toRatio - fromRatio) * spanWidth;
                if (markerWidth <= 0)
                {
                    continue;
                }

                markers.Add(new SponsorBlockMarker(
                    index,
                    segment.Category,
                    left + (fromRatio * spanWidth),
                    markerWidth));
            }
        }

        return markers;
    }

    private static List<SponsorBlockTimeRange> ResolveSpans(
        IReadOnlyList<SponsorBlockTimeRange>? chapters,
        double duration)
    {
        var spans = new List<SponsorBlockTimeRange>();
        if (chapters is not null)
        {
            foreach (var chapter in chapters)
            {
                if (!IsFinite(chapter.Start) || !IsFinite(chapter.End))
                {
                    continue;
                }

                var start = Math.Clamp(chapter.Start, 0, duration);
                var end = Math.Clamp(chapter.End, 0, duration);
                if (end > start)
                {
                    spans.Add(new SponsorBlockTimeRange(start, end));
                }
            }
        }

        if (spans.Count == 0)
        {
            spans.Add(new SponsorBlockTimeRange(0, duration));
        }

        return spans;
    }

    private static (double Left, double Width)[] ResolveLayout(
        List<SponsorBlockTimeRange> spans,
        double duration,
        double width)
    {
        var layout = new (double Left, double Width)[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var isFirst = i == 0;
            var isLast = i == spans.Count - 1;
            var left = (span.Start / duration * width) + (isFirst ? 0d : SegmentGap / 2d);
            var right = (span.End / duration * width) - (isLast ? 0d : SegmentGap / 2d);
            layout[i] = (left, Math.Max(right - left, MinSegmentWidth));
        }

        return layout;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);
}
