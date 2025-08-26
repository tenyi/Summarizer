namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 批次處理狀態列舉
/// </summary>
public enum BatchProcessingStatus
{
    /// <summary>
    /// 排隊中
    /// </summary>
    Queued,

    /// <summary>
    /// 處理中
    /// </summary>
    Processing,

    /// <summary>
    /// 已暫停
    /// </summary>
    Paused,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 處理失敗
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}

/// <summary>
/// 分段任務狀態列舉
/// </summary>
public enum SegmentTaskStatus
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending,

    /// <summary>
    /// 處理中
    /// </summary>
    Processing,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失敗
    /// </summary>
    Failed,

    /// <summary>
    /// 重試中
    /// </summary>
    Retrying
}