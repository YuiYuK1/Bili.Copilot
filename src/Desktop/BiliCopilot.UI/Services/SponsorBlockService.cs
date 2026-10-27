// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.SponsorBlock;
using BiliCopilot.UI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiliCopilot.UI.Services;

/// <summary>
/// bsbsb.top（BilibiliSponsorBlock）接口返回的原始片段.
/// </summary>
internal sealed class BsbsbSegmentDto
{
    [JsonPropertyName("segment")]
    public JsonElement? Segment { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("actionType")]
    public string? ActionType { get; set; }

    [JsonPropertyName("UUID")]
    public string? Uuid { get; set; }

    [JsonPropertyName("videoDuration")]
    public double? VideoDuration { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<BsbsbSegmentDto>))]
internal sealed partial class SponsorBlockJsonContext : JsonSerializerContext;

/// <summary>
/// SponsorBlock 片段服务.
/// </summary>
public sealed class SponsorBlockService : ISponsorBlockService
{
    private const string BaseUrl = "https://www.bsbsb.top";
    private const int MaxCacheSize = 200;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly Dictionary<string, IReadOnlyList<SponsorSegment>> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly object _cacheLock = new();
    private readonly ILogger<SponsorBlockService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockService"/> class.
    /// </summary>
    public SponsorBlockService(ILogger<SponsorBlockService> logger)
        => _logger = logger;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SponsorSegment>> GetSegmentsAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return [];
        }

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(videoId, out var cached))
            {
                return cached;
            }
        }

        IReadOnlyList<SponsorSegment> segments;
        try
        {
            var url = $"{BaseUrl}/api/skipSegments?videoID={Uri.EscapeDataString(videoId)}";
            using var response = await HttpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // 该视频没有社区片段，是正常情况.
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("获取 SponsorBlock 片段失败：{VideoId}，HTTP {StatusCode}", videoId, (int)response.StatusCode);
                return [];
            }

            var dtos = await response.Content.ReadFromJsonAsync(SponsorBlockJsonContext.Default.ListBsbsbSegmentDto);
            segments = dtos is null ? [] : MapSegments(dtos);
        }
        catch (Exception ex)
        {
            // 网络不可用或返回格式异常时都按“无片段”处理.
            _logger.LogWarning(ex, "获取 SponsorBlock 片段失败：{VideoId}", videoId);
            return [];
        }

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(videoId, out var cached))
            {
                return cached;
            }

            while (_cache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
            {
                _cache.Remove(_cacheOrder.Dequeue());
            }

            _cache[videoId] = segments;
            _cacheOrder.Enqueue(videoId);
        }

        return segments;
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheOrder.Clear();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BiliCopilot/2.0");
        return client;
    }

    private static IReadOnlyList<SponsorSegment> MapSegments(List<BsbsbSegmentDto> dtos)
    {
        var result = new List<SponsorSegment>(dtos.Count);
        foreach (var dto in dtos)
        {
            if (dto.Segment is not { ValueKind: JsonValueKind.Array } segmentArray
                || segmentArray.GetArrayLength() < 2)
            {
                continue;
            }

            double start;
            double end;
            try
            {
                start = segmentArray[0].GetDouble();
                end = segmentArray[1].GetDouble();
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                continue;
            }

            // 单点片段（poi_highlight 等）没有可跳过的区间.
            if (end <= start)
            {
                continue;
            }

            if (!Enum.TryParse(dto.Category, ignoreCase: true, out SponsorCategory category))
            {
                category = SponsorCategory.Sponsor;
            }

            if (!Enum.TryParse(dto.ActionType, ignoreCase: true, out SponsorActionType action))
            {
                action = SponsorActionType.Skip;
            }

            result.Add(new SponsorSegment
            {
                StartTime = start,
                EndTime = end,
                UUID = dto.Uuid,
                Category = category,
                Action = action,
                Description = dto.Description,
            });
        }

        return result.OrderBy(p => p.StartTime).ToList();
    }
}
