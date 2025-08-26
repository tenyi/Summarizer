using System.ComponentModel.DataAnnotations;
using Summarizer.Models.TextSegmentation;

namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 批次摘要請求
/// </summary>
public class BatchSummaryRequest
{
    /// <summary>
    /// 分段結果列表
    /// </summary>
    [Required(ErrorMessage = "分段結果不能為空")]
    public List<SegmentResult> Segments { get; set; } = new();

    /// <summary>
    /// 原始文本內容
    /// </summary>
    [Required(ErrorMessage = "原始文本不能為空")]
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 併發限制數量（可選，使用預設值）
    /// </summary>
    [Range(1, 10, ErrorMessage = "併發數必須在 1 到 10 之間")]
    public int? ConcurrentLimit { get; set; }

    /// <summary>
    /// 是否啟用即時進度更新
    /// </summary>
    public bool EnableRealtimeUpdates { get; set; } = true;

    /// <summary>
    /// 批次處理優先級
    /// </summary>
    public BatchPriority Priority { get; set; } = BatchPriority.Normal;
}

/// <summary>
/// 批次摘要回應
/// </summary>
public class BatchSummaryResponse
{
    /// <summary>
    /// 批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 處理成功標誌
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息（如果處理失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 錯誤代碼（如果處理失敗）
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 初始處理狀態
    /// </summary>
    public BatchProcessingStatus Status { get; set; }

    /// <summary>
    /// 總分段數
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 預估處理時間（分鐘）
    /// </summary>
    public double EstimatedProcessingTimeMinutes { get; set; }

    /// <summary>
    /// 處理開始時間
    /// </summary>
    public DateTime StartTime { get; set; }
}

/// <summary>
/// 批次處理狀態查詢回應
/// </summary>
public class BatchStatusResponse
{
    /// <summary>
    /// 批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 處理成功標誌
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 批次處理進度
    /// </summary>
    public BatchProcessingProgress? Progress { get; set; }

    /// <summary>
    /// 分段摘要結果（僅在完成時提供）
    /// </summary>
    public List<SegmentSummaryResult>? SegmentSummaries { get; set; }

    /// <summary>
    /// 最終合併摘要（僅在完成時提供）
    /// </summary>
    public string? FinalSummary { get; set; }
}

/// <summary>
/// 分段摘要結果
/// </summary>
public class SegmentSummaryResult
{
    /// <summary>
    /// 分段索引
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 分段標題
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 原始內容
    /// </summary>
    public string OriginalContent { get; set; } = string.Empty;

    /// <summary>
    /// 摘要內容
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 處理狀態
    /// </summary>
    public SegmentTaskStatus Status { get; set; }

    /// <summary>
    /// 處理時間
    /// </summary>
    public TimeSpan? ProcessingTime { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 錯誤訊息（如果失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 批次處理優先級
/// </summary>
public enum BatchPriority
{
    /// <summary>
    /// 低優先級
    /// </summary>
    Low = 1,

    /// <summary>
    /// 一般優先級
    /// </summary>
    Normal = 2,

    /// <summary>
    /// 高優先級
    /// </summary>
    High = 3,

    /// <summary>
    /// 緊急優先級
    /// </summary>
    Urgent = 4
}