using Summarizer.Models.BatchProcessing;

namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 部分結果資料模型
/// 當批次處理被取消時，保存已完成分段的結果
/// </summary>
public class PartialResult
{
    /// <summary>
    /// 部分結果唯一識別碼
    /// </summary>
    public Guid PartialResultId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 關聯的批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 使用者 ID（用於權限檢查）
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 已完成的分段列表
    /// </summary>
    public List<SegmentSummaryTask> CompletedSegments { get; set; } = new();

    /// <summary>
    /// 總分段數
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 完成百分比
    /// </summary>
    public double CompletionPercentage { get; set; }

    /// <summary>
    /// 合併後的部分摘要
    /// </summary>
    public string PartialSummary { get; set; } = string.Empty;

    /// <summary>
    /// 品質評估結果
    /// </summary>
    public PartialResultQuality Quality { get; set; } = new();

    /// <summary>
    /// 取消操作發生的時間
    /// </summary>
    public DateTime CancellationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 使用者是否接受這個部分結果
    /// </summary>
    public bool UserAccepted { get; set; }

    /// <summary>
    /// 使用者做決定的時間
    /// </summary>
    public DateTime? AcceptedTime { get; set; }

    /// <summary>
    /// 部分結果的處理狀態
    /// </summary>
    public PartialResultStatus Status { get; set; } = PartialResultStatus.PendingUserDecision;

    /// <summary>
    /// 原始文本片段（供參考）
    /// </summary>
    public string OriginalTextSample { get; set; } = string.Empty;

    /// <summary>
    /// 處理這個部分結果花費的總時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// 用戶對部分結果的評論或註記
    /// </summary>
    public string UserComment { get; set; } = string.Empty;

    /// <summary>
    /// 是否已完成（已接受或已拒絕）
    /// </summary>
    public bool IsFinalized => Status == PartialResultStatus.Accepted || Status == PartialResultStatus.Rejected || Status == PartialResultStatus.Expired;
}

/// <summary>
/// 部分結果的處理狀態
/// </summary>
public enum PartialResultStatus
{
    /// <summary>
    /// 等待用戶決定
    /// </summary>
    PendingUserDecision,

    /// <summary>
    /// 已接受並保存
    /// </summary>
    Accepted,

    /// <summary>
    /// 已拒絕
    /// </summary>
    Rejected,

    /// <summary>
    /// 已過期（超過決定期限）
    /// </summary>
    Expired,

    /// <summary>
    /// 處理中（正在生成部分摘要）
    /// </summary>
    Processing,

    /// <summary>
    /// 處理失敗
    /// </summary>
    Failed
}