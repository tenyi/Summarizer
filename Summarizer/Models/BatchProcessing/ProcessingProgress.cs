namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 處理進度資料模型，包含批次處理的完整進度資訊
/// </summary>
public class ProcessingProgress
{
    /// <summary>
    /// 批次處理識別碼
    /// </summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>
    /// 總分段數量
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 目前處理的分段索引（從0開始）
    /// </summary>
    public int CurrentSegment { get; set; }

    /// <summary>
    /// 已完成的分段數量
    /// </summary>
    public int CompletedSegments { get; set; }

    /// <summary>
    /// 失敗的分段數量
    /// </summary>
    public int FailedSegments { get; set; }

    /// <summary>
    /// 目前處理階段
    /// </summary>
    public ProcessingStage CurrentStage { get; set; }

    /// <summary>
    /// 整體進度百分比（0-100）
    /// </summary>
    public double OverallProgress { get; set; }

    /// <summary>
    /// 當前階段進度百分比（0-100）
    /// </summary>
    public double StageProgress { get; set; }

    /// <summary>
    /// 已花費時間（毫秒）
    /// </summary>
    public long ElapsedTimeMs { get; set; }

    /// <summary>
    /// 預估剩餘時間（毫秒，可能為null表示無法預估）
    /// </summary>
    public long? EstimatedRemainingTimeMs { get; set; }

    /// <summary>
    /// 每個分段平均處理時間（毫秒）
    /// </summary>
    public double AverageSegmentTimeMs { get; set; }

    /// <summary>
    /// 目前處理分段的標題（如果有的話）
    /// </summary>
    public string? CurrentSegmentTitle { get; set; }

    /// <summary>
    /// 處理速度統計
    /// </summary>
    public ProcessingSpeed ProcessingSpeed { get; set; } = new();

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 處理開始時間
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 預估完成時間（基於開始時間和預估剩餘時間）
    /// </summary>
    public DateTime? EstimatedCompletionTime => 
        EstimatedRemainingTimeMs.HasValue 
            ? DateTime.UtcNow.AddMilliseconds(EstimatedRemainingTimeMs.Value) 
            : null;

    /// <summary>
    /// 成功率百分比
    /// </summary>
    public double SuccessRate => 
        CompletedSegments + FailedSegments > 0 
            ? (double)CompletedSegments / (CompletedSegments + FailedSegments) * 100 
            : 0;
}