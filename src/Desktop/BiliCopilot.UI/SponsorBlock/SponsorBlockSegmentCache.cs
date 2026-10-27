// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手片段缓存，以「BV 号 + 分 P cid」为键.
/// </summary>
public sealed class SponsorBlockSegmentCache
{
    private const int DefaultCapacity = 200;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly Queue<string> _insertionOrder = new();
    private readonly object _lock = new();
    private readonly TimeSpan _timeToLive;
    private readonly int _capacity;

    public SponsorBlockSegmentCache(TimeSpan? timeToLive = null, int capacity = DefaultCapacity)
    {
        _timeToLive = timeToLive ?? TimeSpan.FromMinutes(5);
        _capacity = Math.Max(1, capacity);
    }

    public bool TryGet(string bvid, string cid, DateTimeOffset now, out IReadOnlyList<SponsorBlockSegment> segments)
    {
        segments = Array.Empty<SponsorBlockSegment>();
        var key = CreateKey(bvid, cid);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (now - entry.CreatedAt >= _timeToLive)
            {
                _ = _entries.Remove(key);
                return false;
            }

            segments = entry.Segments;
            return true;
        }
    }

    public void Set(string bvid, string cid, IReadOnlyList<SponsorBlockSegment> segments, DateTimeOffset now)
    {
        var key = CreateKey(bvid, cid);
        lock (_lock)
        {
            if (!_entries.ContainsKey(key))
            {
                while (_entries.Count >= _capacity && _insertionOrder.Count > 0)
                {
                    _ = _entries.Remove(_insertionOrder.Dequeue());
                }

                _insertionOrder.Enqueue(key);
            }

            _entries[key] = new CacheEntry(segments, now);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _insertionOrder.Clear();
        }
    }

    private static string CreateKey(string bvid, string cid)
        => $"{bvid}\u001f{cid}";

    private readonly record struct CacheEntry(IReadOnlyList<SponsorBlockSegment> Segments, DateTimeOffset CreatedAt);
}
