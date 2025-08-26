using Summarizer.Models.TextSegmentation;

namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 分段摘要任務
/// </summary>
public class SegmentSummaryTask
{
    /// <summary>
    /// 分段索引
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 來源分段資料
    /// </summary>
    public SegmentResult SourceSegment { get; set; } = new();

    /// <summary>
    /// 任務狀態
    /// </summary>
    public SegmentTaskStatus Status { get; set; } = SegmentTaskStatus.Pending;

    /// <summary>
    /// 摘要結果
    /// </summary>
    public string SummaryResult { get; set; } = string.Empty;

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 完成時間
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 處理時間
    /// </summary>
    public TimeSpan? ProcessingTime { get; set; }

    /// <summary>
    /// 最後重試時間
    /// </summary>
    public DateTime? LastRetryTime { get; set; }
}