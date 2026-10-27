// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手接口返回的原始片段数据.
/// </summary>
internal sealed class SponsorBlockSegmentDto
{
    [JsonPropertyName("segment")]
    public double[]? Segment { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("actionType")]
    public string? ActionType { get; set; }

    [JsonPropertyName("UUID")]
    public string? Uuid { get; set; }

    [JsonPropertyName("cid")]
    public JsonElement? Cid { get; set; }

    [JsonPropertyName("videoDuration")]
    public double? VideoDuration { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<SponsorBlockSegmentDto>))]
internal sealed partial class SponsorBlockJsonContext : JsonSerializerContext;
