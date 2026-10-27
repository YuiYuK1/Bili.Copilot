// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手行为控制器.
/// </summary>
public sealed class SponsorBlockController
{
    public const double StartToleranceSeconds = 0.5;
    public const double MinimumRemainingSeconds = 0.5;
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(8);

    private readonly Func<SponsorBlockCategory, SponsorBlockBehavior> _behaviorResolver;
    private readonly TimeSpan _promptTimeout;
    private readonly HashSet<string> _handledSegments = new(StringComparer.Ordinal);
    private SponsorBlockSegment? _activeMuteSegment;
    private SponsorBlockSegment? _activePromptSegment;
    private DateTimeOffset _promptShownAt;
    private bool _muteApplied;
    private bool _muteSuppressed;
    private bool _muteRestoreState;
    private bool _hasChecked;
    private DateTimeOffset _lastCheckAt;

    public SponsorBlockController(
        Func<SponsorBlockCategory, SponsorBlockBehavior>? behaviorResolver = null,
        TimeSpan? promptTimeout = null)
    {
        _behaviorResolver = behaviorResolver ?? SponsorBlockBehaviorDefaults.GetDefault;
        _promptTimeout = promptTimeout ?? PromptTimeout;
        if (_promptTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(promptTimeout));
        }
    }

    public SponsorBlockSegment? ActiveSegment { get; private set; }

    public SponsorBlockSegment? PromptSegment => _activePromptSegment;

    public bool IsMuteApplied => _muteApplied;

    public bool IsPromptActive => _activePromptSegment is not null;

    public SponsorBlockDecision Evaluate(
        IReadOnlyList<SponsorBlockSegment>? segments,
        double position,
        double duration,
        bool isMuted,
        DateTimeOffset now)
    {
        if (segments is null || segments.Count == 0
            || !IsFinite(position) || position < 0)
        {
            return default;
        }

        if (_hasChecked && now - _lastCheckAt < MinimumCheckInterval)
        {
            return default;
        }

        _hasChecked = true;
        _lastCheckAt = now;

        var effectiveDuration = IsFinite(duration) && duration > 0
            ? duration
            : double.PositiveInfinity;
        var segmentAtPosition = FindSegmentAt(segments, position);
        ActiveSegment = segmentAtPosition;

        if (_activePromptSegment is not null)
        {
            if (position >= _activePromptSegment.End
                || position < _activePromptSegment.Start - StartToleranceSeconds
                || now - _promptShownAt >= _promptTimeout)
            {
                return CompletePrompt(_activePromptSegment);
            }

            return default;
        }

        if (_activeMuteSegment is not null)
        {
            var leftMuteSegment = position >= _activeMuteSegment.End
                || position < _activeMuteSegment.Start - StartToleranceSeconds;
            if (!leftMuteSegment)
            {
                if (!_muteSuppressed && !_muteApplied)
                {
                    _muteApplied = true;
                    _muteRestoreState = isMuted;
                    ActiveSegment = _activeMuteSegment;
                    return new SponsorBlockDecision(
                        SponsorBlockActionKind.Mute,
                        _activeMuteSegment,
                        0,
                        true);
                }

                return default;
            }

            var restoreState = _muteApplied ? _muteRestoreState : (bool?)null;
            var muteSegment = _activeMuteSegment;
            ClearMute();
            ActiveSegment = segmentAtPosition;
            if (restoreState.HasValue)
            {
                return new SponsorBlockDecision(
                    SponsorBlockActionKind.RestoreMute,
                    muteSegment,
                    0,
                    restoreState.Value);
            }
        }

        if (segmentAtPosition is null)
        {
            return default;
        }

        foreach (var candidate in segments)
        {
            if (candidate is null
                || !SponsorBlockSegment.IsValidRange(candidate.Start, candidate.End)
                || position < candidate.Start - StartToleranceSeconds
                || position >= candidate.End)
            {
                continue;
            }

            var key = CreateKey(candidate);
            if (_handledSegments.Contains(key))
            {
                continue;
            }

            switch (_behaviorResolver(candidate.Category))
            {
                case SponsorBlockBehavior.Skip:
                    {
                        var target = Math.Min(candidate.End, effectiveDuration);
                        if (target - position < MinimumRemainingSeconds)
                        {
                            continue;
                        }

                        _ = _handledSegments.Add(key);
                        ActiveSegment = candidate;
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.Skip,
                            candidate,
                            target);
                    }

                case SponsorBlockBehavior.Mute:
                    {
                        _activeMuteSegment = candidate;
                        _muteApplied = true;
                        _muteSuppressed = false;
                        _muteRestoreState = isMuted;
                        ActiveSegment = candidate;
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.Mute,
                            candidate,
                            0,
                            true);
                    }

                case SponsorBlockBehavior.ShowButton:
                    {
                        _activePromptSegment = candidate;
                        _promptShownAt = now;
                        ActiveSegment = candidate;
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.ShowButton,
                            candidate);
                    }

                default:
                    continue;
            }
        }

        return default;
    }

    public SponsorBlockDecision AcceptActivePrompt(double duration)
    {
        var segment = _activePromptSegment;
        if (segment is null)
        {
            return default;
        }

        ClearPrompt(segment);
        var target = IsFinite(duration) && duration > 0
            ? Math.Min(segment.End, duration)
            : segment.End;
        return new SponsorBlockDecision(SponsorBlockActionKind.Skip, segment, target);
    }

    public SponsorBlockDecision DismissActivePrompt()
    {
        var segment = _activePromptSegment;
        if (segment is null)
        {
            return default;
        }

        ClearPrompt(segment);
        return new SponsorBlockDecision(SponsorBlockActionKind.HideButton, segment);
    }

    public void NotifyUserMuteChanged()
    {
        if (_activeMuteSegment is null)
        {
            return;
        }

        _muteApplied = false;
        _muteSuppressed = true;
    }

    public SponsorBlockDecision Reset()
    {
        var restoreState = _muteApplied ? _muteRestoreState : (bool?)null;
        var prompt = _activePromptSegment;

        _handledSegments.Clear();
        _activeMuteSegment = null;
        _activePromptSegment = null;
        _promptShownAt = default;
        _muteApplied = false;
        _muteSuppressed = false;
        _muteRestoreState = false;
        _hasChecked = false;
        _lastCheckAt = default;
        ActiveSegment = null;

        if (restoreState.HasValue)
        {
            return new SponsorBlockDecision(
                SponsorBlockActionKind.RestoreMute,
                null,
                0,
                restoreState.Value);
        }

        return prompt is null
            ? default
            : new SponsorBlockDecision(SponsorBlockActionKind.HideButton, prompt);
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static SponsorBlockSegment? FindSegmentAt(
        IReadOnlyList<SponsorBlockSegment> segments,
        double position)
    {
        foreach (var segment in segments)
        {
            if (segment is not null
                && SponsorBlockSegment.IsValidRange(segment.Start, segment.End)
                && position >= segment.Start - StartToleranceSeconds
                && position < segment.End)
            {
                return segment;
            }
        }

        return null;
    }

    private static string CreateKey(SponsorBlockSegment segment)
        => string.IsNullOrWhiteSpace(segment.Uuid)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{segment.Category}:{segment.Start:F3}:{segment.End:F3}")
            : segment.Uuid!;

    private SponsorBlockDecision CompletePrompt(SponsorBlockSegment segment)
    {
        ClearPrompt(segment);
        return new SponsorBlockDecision(SponsorBlockActionKind.HideButton, segment);
    }

    private void ClearPrompt(SponsorBlockSegment segment)
    {
        _ = _handledSegments.Add(CreateKey(segment));
        _activePromptSegment = null;
        _promptShownAt = default;
    }

    private void ClearMute()
    {
        _activeMuteSegment = null;
        _muteApplied = false;
        _muteSuppressed = false;
        _muteRestoreState = false;
    }
}
