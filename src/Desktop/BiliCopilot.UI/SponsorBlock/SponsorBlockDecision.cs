// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手控制器对当前播放进度给出的决策.
/// </summary>
public readonly record struct SponsorBlockDecision(
    SponsorBlockActionKind Kind,
    SponsorBlockSegment? Segment = null,
    double TargetPosition = 0,
    bool? MuteState = null)
{
    public bool IsActionable => Kind != SponsorBlockActionKind.None;
}
