// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.Json;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手接口响应解析器.
/// </summary>
public static class SponsorBlockSegmentParser
{
    public static IReadOnlyList<SponsorBlockSegment> Parse(string? json, string? expectedCid = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        List<SponsorBlockSegmentDto>? items;
        try
        {
            items = JsonSerializer.Deserialize(json, SponsorBlockJsonContext.Default.ListSponsorBlockSegmentDto);
        }
        catch (JsonException)
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        if (items is null || items.Count == 0)
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        var segments = new List<SponsorBlockSegment>(items.Count);
        foreach (var item in items)
        {
            if (item?.Segment is not { Length: >= 2 } range)
            {
                continue;
            }

            var start = range[0];
            var end = range[1];
            if (!SponsorBlockSegment.IsValidRange(start, end)
                || !TryParseActionType(item.ActionType, out var actionType)
                || !MatchesCid(item.Cid, expectedCid))
            {
                continue;
            }

            segments.Add(new SponsorBlockSegment(
                SponsorBlockCategoryParser.Parse(item.Category),
                start,
                end,
                item.Uuid,
                item.VideoDuration,
                actionType));
        }

        segments.Sort(static (left, right) =>
        {
            var result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.End.CompareTo(right.End);
        });

        return segments;
    }

    private static bool TryParseActionType(string? actionType, out SponsorBlockActionType result)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            result = SponsorBlockActionType.Skip;
            return true;
        }

        switch (actionType.Trim().ToLowerInvariant())
        {
            case "skip":
                result = SponsorBlockActionType.Skip;
                return true;
            case "mute":
                result = SponsorBlockActionType.Mute;
                return true;
            default:
                // poi / full 等动作不在自动处理范围内.
                result = default;
                return false;
        }
    }

    private static bool MatchesCid(JsonElement? cid, string? expectedCid)
    {
        if (string.IsNullOrWhiteSpace(expectedCid) || cid is null)
        {
            return true;
        }

        var value = cid.Value.ValueKind switch
        {
            JsonValueKind.String => cid.Value.GetString(),
            JsonValueKind.Number => cid.Value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrEmpty(value)
            || string.Equals(value, expectedCid, StringComparison.Ordinal);
    }
}
