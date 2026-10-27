// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Microsoft.Windows.Storage;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 视频章节的本地缓存，按视频和分P独立保存.
/// </summary>
internal static class ChapterCache
{
    private const string FolderName = "Chapters";

    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    /// <summary>
    /// 同一进程内串行化读写。两个播放窗口同时打开同一视频时，
    /// 并发写同一个缓存文件会互相占用临时文件，导致保存失败.
    /// </summary>
    private static readonly SemaphoreSlim StorageLock = new(1, 1);

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
    /// 保存章节到本地缓存。优先写入用户指定的缓存目录，失败后回退到应用缓存目录.
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

        string json;
        try
        {
            var payload = chapters.Select(c => new ChapterCacheEntry
            {
                Title = c.Title,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
            }).ToList();
            json = System.Text.Json.JsonSerializer.Serialize(payload, SerializerOptions);
        }
        catch
        {
            return false;
        }

        await StorageLock.WaitAsync();
        try
        {
            foreach (var root in GetRootFolders())
            {
                string? tempPath = null;
                try
                {
                    var folder = Path.Combine(root, FolderName);
                    Directory.CreateDirectory(folder);
                    var path = Path.Combine(folder, $"{key}.json");
                    tempPath = Path.Combine(folder, $"{key}.{Guid.NewGuid():N}.tmp");
                    await File.WriteAllTextAsync(tempPath, json, Utf8NoBom);
                    if (await TryMoveAsync(tempPath, path))
                    {
                        tempPath = null;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存章节缓存失败: {ex.Message}");
                }
                finally
                {
                    if (tempPath is not null)
                    {
                        TryDelete(tempPath);
                    }
                }
            }

            return false;
        }
        finally
        {
            StorageLock.Release();
        }
    }

    /// <summary>
    /// 从本地缓存读取章节。会依次尝试用户缓存目录与应用缓存目录.
    /// </summary>
    /// <param name="key">缓存键.</param>
    /// <returns>章节列表；没有缓存或读取失败时为空.</returns>
    public static async Task<List<VideoChapter>> LoadAsync(string key)
    {
        await StorageLock.WaitAsync();
        try
        {
            foreach (var root in GetRootFolders())
            {
                try
                {
                    var path = Path.Combine(root, FolderName, $"{key}.json");
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
                    var payload = System.Text.Json.JsonSerializer.Deserialize<List<ChapterCacheEntry>>(json, SerializerOptions);
                    if (payload is null)
                    {
                        continue;
                    }

                    return [.. payload.Select(c => new VideoChapter(c.Title, c.StartTime, c.EndTime))];
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"读取章节缓存失败: {ex.Message}");
                }
            }

            return [];
        }
        finally
        {
            StorageLock.Release();
        }
    }

    private static async Task<bool> TryMoveAsync(string source, string destination)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Move(source, destination, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                {
                    System.Diagnostics.Debug.WriteLine($"写入章节缓存失败: {ex.Message}");
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(40 * (attempt + 1)));
            }
        }

        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 返回候选缓存根目录：用户指定目录优先，应用缓存目录兜底.
    /// </summary>
    private static List<string> GetRootFolders()
    {
        var roots = new List<string>();

        try
        {
            var cacheOnDisk = SettingsToolkit.ReadLocalSetting(SettingNames.CacheOnDisk, false);
            var cacheDir = SettingsToolkit.ReadLocalSetting(SettingNames.CacheDir, string.Empty);
            if (cacheOnDisk && !string.IsNullOrWhiteSpace(cacheDir))
            {
                AddRoot(roots, cacheDir);
            }
        }
        catch
        {
        }

        try
        {
            AddRoot(roots, ApplicationData.GetDefault().LocalCacheFolder.Path);
        }
        catch
        {
        }

        return roots;
    }

    private static void AddRoot(List<string> roots, string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!string.IsNullOrWhiteSpace(fullPath)
                && !roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(fullPath);
            }
        }
        catch
        {
        }
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
