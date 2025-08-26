namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 批次摘要處理器
/// </summary>
public class BatchSummaryProcessor
{
    /// <summary>
    /// 批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 使用者 ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 原始文本內容
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 分段摘要任務列表
    /// </summary>
    public List<SegmentSummaryTask> Tasks { get; set; } = new();

    /// <summary>
    /// 批次處理狀態
    /// </summary>
    public BatchProcessingStatus Status { get; set; } = BatchProcessingStatus.Queued;

    /// <summary>
    /// 開始時間
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成時間
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>
    /// 併發限制數量
    /// </summary>
    public int ConcurrentLimit { get; set; } = 2;

    /// <summary>
    /// 總分段數
    /// </summary>
    public int TotalSegments => Tasks.Count;

    /// <summary>
    /// 已完成分段數
    /// </summary>
    public int CompletedSegments => Tasks.Count(t => t.Status == SegmentTaskStatus.Completed);

    /// <summary>
    /// 失敗分段數
    /// </summary>
    public int FailedSegments => Tasks.Count(t => t.Status == SegmentTaskStatus.Failed);

    /// <summary>
    /// 進度百分比
    /// </summary>
    public double ProgressPercentage => TotalSegments > 0 ? (double)CompletedSegments / TotalSegments * 100 : 0;

    /// <summary>
    /// 已經過時間
    /// </summary>
    public TimeSpan ElapsedTime => CompletedTime?.Subtract(StartTime) ?? DateTime.UtcNow.Subtract(StartTime);

    /// <summary>
    /// 預估剩餘時間
    /// </summary>
    public TimeSpan? EstimatedRemainingTime
    {
        get
        {
            if (CompletedSegments == 0) return null;
            
            var avgTimePerSegment = ElapsedTime.TotalSeconds / CompletedSegments;
            var remainingSegments = TotalSegments - CompletedSegments;
            
            return TimeSpan.FromSeconds(avgTimePerSegment * remainingSegments);
        }
    }

    /// <summary>
    /// 當前處理的分段標題
    /// </summary>
    public string CurrentSegmentTitle 
    {
        get
        {
            var currentTask = Tasks.FirstOrDefault(t => t.Status == SegmentTaskStatus.Processing);
            return currentTask?.SourceSegment.Title ?? string.Empty;
        }
    }

    /// <summary>
    /// 最終合併摘要結果
    /// </summary>
    public string FinalSummary { get; set; } = string.Empty;

    /// <summary>
    /// 批次處理統計資訊
    /// </summary>
    public BatchProcessingStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 批次處理統計資訊
/// </summary>
public class BatchProcessingStatistics
{
    /// <summary>
    /// 總處理時間
    /// </summary>
    public TimeSpan TotalProcessingTime { get; set; }

    /// <summary>
    /// 平均每段處理時間
    /// </summary>
    public TimeSpan AverageSegmentProcessingTime { get; set; }

    /// <summary>
    /// 總重試次數
    /// </summary>
    public int TotalRetries { get; set; }

    /// <summary>
    /// API 呼叫成功率
    /// </summary>
    public double ApiSuccessRate { get; set; }

    /// <summary>
    /// 峰值併發數
    /// </summary>
    public int PeakConcurrency { get; set; }

    /// <summary>
    /// 總 API 呼叫次數
    /// </summary>
    public int TotalApiCalls { get; set; }

    /// <summary>
    /// 成功的 API 呼叫次數
    /// </summary>
    public int SuccessfulApiCalls { get; set; }
}