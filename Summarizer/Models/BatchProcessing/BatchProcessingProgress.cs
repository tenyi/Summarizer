namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 批次處理進度資訊
/// </summary>
public class BatchProcessingProgress
{
    /// <summary>
    /// 批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 總分段數
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 已完成分段數
    /// </summary>
    public int CompletedSegments { get; set; }

    /// <summary>
    /// 失敗分段數
    /// </summary>
    public int FailedSegments { get; set; }

    /// <summary>
    /// 進度百分比
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// 已經過時間
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 預估剩餘時間
    /// </summary>
    public TimeSpan? EstimatedRemainingTime { get; set; }

    /// <summary>
    /// 當前處理的分段標題
    /// </summary>
    public string CurrentSegmentTitle { get; set; } = string.Empty;

    /// <summary>
    /// 批次處理狀態
    /// </summary>
    public BatchProcessingStatus Status { get; set; }

    /// <summary>
    /// 狀態訊息
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 處理速度（分段/分鐘）
    /// </summary>
    public double ProcessingRate { get; set; }

    /// <summary>
    /// 當前併發數
    /// </summary>
    public int CurrentConcurrency { get; set; }
}