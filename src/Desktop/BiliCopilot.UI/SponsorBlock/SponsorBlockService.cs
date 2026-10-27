// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段服务.
/// </summary>
public sealed class SponsorBlockService : IDisposable
{
    public const string DefaultBaseUrl = "https://bsbsb.top";

    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly SponsorBlockSegmentCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _requestTimeout;
    private readonly bool _ownsHttpClient;
    private bool _isDisposed;

    public SponsorBlockService(
        HttpClient? httpClient = null,
        SponsorBlockSegmentCache? cache = null,
        TimeProvider? timeProvider = null,
        TimeSpan? requestTimeout = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _cache = cache ?? new SponsorBlockSegmentCache();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public async Task<IReadOnlyList<SponsorBlockSegment>> GetSegmentsAsync(
        string? bvid,
        string? cid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bvid) || string.IsNullOrWhiteSpace(cid))
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGet(bvid, cid, now, out var cached))
        {
            return cached;
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_requestTimeout);

            var url = $"{DefaultBaseUrl}/api/skipSegments?videoID={Uri.EscapeDataString(bvid)}&cid={Uri.EscapeDataString(cid)}";
            using var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent
                || !response.IsSuccessStatusCode)
            {
                return Array.Empty<SponsorBlockSegment>();
            }

            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            var segments = SponsorBlockSegmentParser.Parse(json, cid);
            if (segments.Count > 0)
            {
                _cache.Set(bvid, cid, segments, now);
            }

            return segments;
        }
        catch
        {
            return Array.Empty<SponsorBlockSegment>();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
