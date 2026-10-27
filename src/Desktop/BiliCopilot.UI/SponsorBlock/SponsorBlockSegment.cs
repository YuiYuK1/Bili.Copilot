// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手的单个可跳过片段.
/// </summary>
public sealed class SponsorBlockSegment
{
    public SponsorBlockSegment(
        SponsorBlockCategory category,
        double start,
        double end,
        string? uuid = null,
        double? videoDuration = null,
        SponsorBlockActionType actionType = SponsorBlockActionType.Skip)
    {
        Category = category;
        Start = start;
        End = end;
        Uuid = uuid;
        VideoDuration = videoDuration;
        ActionType = actionType;
    }

    public SponsorBlockCategory Category { get; }

    public double Start { get; }

    public double End { get; }

    public string? Uuid { get; }

    public double? VideoDuration { get; }

    public SponsorBlockActionType ActionType { get; }

    public double Duration => End - Start;

    public static bool IsValidRange(double start, double end)
        => !double.IsNaN(start)
            && !double.IsNaN(end)
            && !double.IsInfinity(start)
            && !double.IsInfinity(end)
            && start >= 0
            && end > start;
}
