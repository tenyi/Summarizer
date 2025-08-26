namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 進度更新事件參數
/// </summary>
public class ProgressUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 批次處理識別碼
    /// </summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>
    /// 更新後的進度資料
    /// </summary>
    public ProcessingProgress Progress { get; set; } = new();

    /// <summary>
    /// 觸發更新的原因
    /// </summary>
    public ProgressUpdateReason UpdateReason { get; set; }

    /// <summary>
    /// 更新時間戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 相關的分段資訊（如果適用）
    /// </summary>
    public SegmentStatus? RelatedSegment { get; set; }

    /// <summary>
    /// 額外的上下文資訊
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// 進度更新原因枚舉
/// </summary>
public enum ProgressUpdateReason
{
    /// <summary>
    /// 初始化開始
    /// </summary>
    InitializationStarted,

    /// <summary>
    /// 分段完成
    /// </summary>
    SegmentCompleted,

    /// <summary>
    /// 分段開始處理
    /// </summary>
    SegmentStarted,

    /// <summary>
    /// 分段失敗
    /// </summary>
    SegmentFailed,

    /// <summary>
    /// 階段變更
    /// </summary>
    StageChanged,

    /// <summary>
    /// 批次處理完成
    /// </summary>
    BatchCompleted,

    /// <summary>
    /// 批次處理失敗
    /// </summary>
    BatchFailed,

    /// <summary>
    /// 定期進度更新
    /// </summary>
    PeriodicUpdate,

    /// <summary>
    /// 時間預估更新
    /// </summary>
    TimeEstimationUpdate,

    /// <summary>
    /// 重試開始
    /// </summary>
    RetryStarted
}