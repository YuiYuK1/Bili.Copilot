// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Microsoft.Windows.Storage;
using Richasy.BiliKernel.Models.Media;
using System.Text.Json;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 视频章节的本地缓存，按视频和分P独立保存.
/// </summary>
internal static class ChapterCache
{
    private const string FolderName = "Chapters";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 生成章节缓存键.
    /// </summary>
    /// <param name="videoId">视频标识.</param>
    /// <param name="partId">分P标识.</param>
    /// <returns>可用于文件名的缓存键.</returns>
    public static string BuildKey(string? videoId, string? partId)
    {
        var raw = $"{videoId}_{partId}";
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(raw.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    /// <summary>
    /// 保存章节到本地缓存.
    /// </summary>
    /// <param name="key">缓存键.</param>
    /// <param name="chapters">章节列表.</param>
    /// <returns>是否保存成功.</returns>
    public static async Task<bool> SaveAsync(string key, IReadOnlyList<VideoChapter> chapters)
    {
        if (chapters.Count == 0)
        {
            return false;
        }

        try
        {
            var payload = chapters.Select(c => new ChapterCacheEntry
            {
                Title = c.Title,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
            }).ToList();
            var json = JsonSerializer.Serialize(payload, SerializerOptions);
            var path = GetFilePath(key);
            var tempPath = $"{path}.tmp";
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, path, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从本地缓存读取章节.
    /// </summary>
    /// <param name="key">缓存键.</param>
    /// <returns>章节列表；没有缓存或读取失败时为空.</returns>
    public static async Task<List<VideoChapter>> LoadAsync(string key)
    {
        try
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var payload = JsonSerializer.Deserialize<List<ChapterCacheEntry>>(json, SerializerOptions);
            if (payload is null)
            {
                return [];
            }

            return [.. payload.Select(c => new VideoChapter(c.Title, c.StartTime, c.EndTime))];
        }
        catch
        {
            return [];
        }
    }

    private static string GetFilePath(string key)
    {
        var cacheOnDisk = SettingsToolkit.ReadLocalSetting(SettingNames.CacheOnDisk, false);
        var cacheDir = SettingsToolkit.ReadLocalSetting(SettingNames.CacheDir, string.Empty);
        var root = cacheOnDisk && !string.IsNullOrEmpty(cacheDir)
            ? cacheDir
            : ApplicationData.GetDefault().LocalCacheFolder.Path;
        var folder = Path.Combine(root, FolderName);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{key}.json");
    }
}

internal sealed class ChapterCacheEntry
{
    public ChapterCacheEntry()
    {
    }

    public string Title { get; set; } = string.Empty;

    public int StartTime { get; set; }

    public int EndTime { get; set; }
}
