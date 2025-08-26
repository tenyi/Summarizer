namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 分段處理狀態枚舉
/// </summary>
public enum SegmentProcessingStatus
{
    /// <summary>
    /// 等待處理 - 分段已建立但尚未開始處理
    /// </summary>
    Pending,

    /// <summary>
    /// 處理中 - 分段正在進行AI處理
    /// </summary>
    Processing,

    /// <summary>
    /// 已完成 - 分段處理成功完成
    /// </summary>
    Completed,

    /// <summary>
    /// 處理失敗 - 分段處理遇到錯誤
    /// </summary>
    Failed,

    /// <summary>
    /// 重試中 - 分段處理失敗後正在重試
    /// </summary>
    Retrying
}