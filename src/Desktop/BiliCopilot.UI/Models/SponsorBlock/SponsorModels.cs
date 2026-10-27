// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.SponsorBlock;

/// <summary>
/// SponsorBlock 片段类别.
/// </summary>
public enum SponsorCategory
{
    Sponsor,
    SelfPromo,
    ExclusiveAccess,
    Interaction,

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "SponsorBlock API category name.")]
    POI_Highlight,

    Intro,
    Outro,
    Preview,
    Padding,
    Filler,

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "SponsorBlock API category name.")]
    Music_Offtopic,

    Unpaid,
}

/// <summary>
/// SponsorBlock 片段操作类型.
/// </summary>
public enum SponsorActionType
{
    Skip,
    Mute,
    Full,
    POI,
}

/// <summary>
/// 一个 SponsorBlock 片段.
/// </summary>
public sealed class SponsorSegment
{
    /// <summary>
    /// 开始时间（秒）.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// 结束时间（秒）.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// 视频 ID.
    /// </summary>
    public string? VideoID { get; set; }

    /// <summary>
    /// 片段唯一标识.
    /// </summary>
    public string? UUID { get; set; }

    /// <summary>
    /// 类别.
    /// </summary>
    public SponsorCategory Category { get; set; }

    /// <summary>
    /// 操作类型.
    /// </summary>
    public SponsorActionType Action { get; set; }

    /// <summary>
    /// 片段描述.
    /// </summary>
    public string? Description { get; set; }
}
