namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 分段處理狀態資料模型
/// </summary>
public class SegmentStatus
{
    /// <summary>
    /// 分段索引（從0開始）
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 分段標題或描述
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 分段處理狀態
    /// </summary>
    public SegmentProcessingStatus Status { get; set; }

    /// <summary>
    /// 開始處理時間
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 完成處理時間
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 處理所花費的時間（毫秒）
    /// </summary>
    public long? ProcessingTimeMs { get; set; }

    /// <summary>
    /// 錯誤訊息（如果處理失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 分段內容長度（字符數）
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// 分段在整個文本中的位置（字符偏移）
    /// </summary>
    public int ContentOffset { get; set; }

    /// <summary>
    /// 處理結果的長度（如果已完成）
    /// </summary>
    public int? ResultLength { get; set; }

    /// <summary>
    /// 是否已完成處理（成功或失敗）
    /// </summary>
    public bool IsCompleted => Status == SegmentProcessingStatus.Completed || Status == SegmentProcessingStatus.Failed;

    /// <summary>
    /// 是否正在處理中
    /// </summary>
    public bool IsProcessing => Status == SegmentProcessingStatus.Processing || Status == SegmentProcessingStatus.Retrying;
}