// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Danmaku;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    /// <summary>
    /// 同时请求的弹幕分片数量。B 站接口对并发敏感，过高会返回空响应.
    /// </summary>
    private const int DanmakuSegmentConcurrency = 3;

    /// <summary>
    /// 单个分片请求的超时时间，避免个别请求挂住导致整个弹幕初始化迟迟不结束.
    /// </summary>
    private static readonly TimeSpan DanmakuSegmentTimeout = TimeSpan.FromSeconds(12);

    [RelayCommand]
    private async Task InitializeDanmakuAsync()
    {
        SettingsToolkit.DeleteLocalSetting(Models.Constants.SettingNames.IsDanmakuEnabled);
        IsDanmakuEnabled = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.IsDanmakuEnabled, true);
        IsDanmakuControlVisible = IsDanmakuEnabled;
        if (!IsDanmakuEnabled)
        {
            return;
        }

        IsDanmakuLoading = true;
        using var semaphore = new SemaphoreSlim(DanmakuSegmentConcurrency, DanmakuSegmentConcurrency);
        var danmakus = new List<DanmakuInformation>();
        try
        {
            if (_snapshot.Type == Models.Constants.BiliMediaType.Video)
            {
                var connector = Connector as VideoConnectorViewModel;
                var aid = connector._view.Information.Identifier.Id;
                var cid = connector._part.Identifier.Id;
                Danmaku.Title = connector._view.Information.Identifier.Title;
                var totalMinutes = Math.Ceiling(TimeSpan.FromSeconds(connector._view.Information.Duration ?? 0).TotalMinutes);
                var tasks = new List<Task>();
                var segmentCount = Convert.ToInt32(Math.Ceiling(totalMinutes / 6.0)); // 6 minutes per segment
                for (var i = 0; i < segmentCount; i++)
                {
                    var index = i + 1;
                    tasks.Add(Task.Run(async () => await GetDanmakuAsync(index, aid, cid)));
                }

                await Task.WhenAll(tasks);
            }
            else if (_snapshot.Type == Models.Constants.BiliMediaType.Pgc)
            {
                var connector = Connector as PgcConnectorViewModel;
                var aid = connector._episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Aid).ToString();
                var cid = connector._episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Cid).ToString();
                var totalMinutes = Math.Ceiling(TimeSpan.FromSeconds(connector._episode.Duration ?? 0).TotalMinutes);
                var tasks = new List<Task>();
                var segmentCount = Convert.ToInt32(Math.Ceiling(totalMinutes / 6.0)); // 6 minutes per segment
                for (var i = 0; i < segmentCount; i++)
                {
                    var index = i + 1;
                    tasks.Add(Task.Run(async () => await GetDanmakuAsync(index, aid, cid)));
                }

                Danmaku.Title = $"{connector.SeasonTitle} - {connector.EpisodeTitle}";
                await Task.WhenAll(tasks);
            }

            Danmaku.DanmakuCountText = danmakus.Count > 0
                ? string.Format(ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.DanmakuCountTemplate), danmakus.Count.ToString("N0"))
                : ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.DanmakuEmpty);
            Danmaku.Initialize(danmakus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize danmaku.");
            IsDanmakuControlVisible = false;
        }
        finally
        {
            IsDanmakuLoading = false;
        }

        async Task GetDanmakuAsync(int index, string aid, string cid)
        {
            await semaphore.WaitAsync();
            try
            {
                var segment = await FetchDanmakuSegmentAsync(index, aid, cid);
                if (segment is { Count: > 0 })
                {
                    lock (danmakus)
                    {
                        danmakus.AddRange(segment);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    [RelayCommand]
    private async Task ReloadDanmakuAsync()
    {
        if (Player is null || Window is null || Window.IsClosed || !Player.IsPlaybackInitialized || Danmaku is null)
        {
            return;
        }

        Danmaku.ClearAll();
        await InitializeDanmakuAsync();
    }

    /// <summary>
    /// 获取单个弹幕分片。先走 gRPC；失败后短暂退避，再退回 Web 接口。
    /// 底层服务自身已对单次请求做了重试，这里只负责切换链路，避免叠加过多请求。
    /// </summary>
    private async Task<IReadOnlyList<DanmakuInformation>?> FetchDanmakuSegmentAsync(int index, string aid, string cid)
    {
        var service = this.Get<IDanmakuService>();

        try
        {
            using var cts = new CancellationTokenSource(DanmakuSegmentTimeout);
            return await service.GetSegmentDanmakusWithGrpcAsync(aid, cid, index, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning("gRPC 获取弹幕分片 {Index} 失败，改用 Web 接口：{Message}", index, ex.Message);
        }

        // gRPC 接口偶尔会返回空响应（尤其是长视频并发的多个分片），
        // Web 接口走的是另一条链路，可作为兜底。先退避一下，避免马上撞上限流.
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        try
        {
            using var cts = new CancellationTokenSource(DanmakuSegmentTimeout);
            return await service.GetSegmentDanmakusWithWebAsync(aid, cid, index, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取弹幕分片失败：{Index}", index);
            return default;
        }
    }
}
