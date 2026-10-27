// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.SponsorBlock;

namespace BiliCopilot.UI.Services.Interfaces;

/// <summary>
/// SponsorBlock 服务.
/// </summary>
public interface ISponsorBlockService
{
    /// <summary>
    /// 获取视频的赞助片段（带本地缓存）.
    /// </summary>
    /// <param name="videoId">视频 BV 号.</param>
    /// <returns>按开始时间升序排列的片段列表.</returns>
    Task<IReadOnlyList<SponsorSegment>> GetSegmentsAsync(string videoId);

    /// <summary>
    /// 清除全部缓存.
    /// </summary>
    void ClearCache();
}
