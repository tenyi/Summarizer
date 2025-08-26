namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 取消操作請求模型
/// </summary>
public class CancellationRequest
{
    /// <summary>
    /// 批次處理識別碼
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 取消原因
    /// </summary>
    public CancellationReason Reason { get; set; }

    /// <summary>
    /// 是否保存部分結果
    /// </summary>
    public bool SavePartialResults { get; set; }

    /// <summary>
    /// 取消請求時間
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 使用者註解
    /// </summary>
    public string UserComment { get; set; } = string.Empty;

    /// <summary>
    /// 強制取消（不等待優雅關閉）
    /// </summary>
    public bool ForceCancel { get; set; }
}

/// <summary>
/// 取消原因枚舉
/// </summary>
public enum CancellationReason
{
    /// <summary>
    /// 使用者主動取消
    /// </summary>
    UserRequested,

    /// <summary>
    /// 系統超時
    /// </summary>
    SystemTimeout,

    /// <summary>
    /// 資源耗盡
    /// </summary>
    ResourceExhausted,

    /// <summary>
    /// 服務不可用
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// 品質不達標
    /// </summary>
    QualityThreshold,

    /// <summary>
    /// 應用程式關閉
    /// </summary>
    ApplicationShutdown
}

/// <summary>
/// 取消操作結果
/// </summary>
public class CancellationResult
{
    /// <summary>
    /// 取消是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 結果訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 是否已保存部分結果
    /// </summary>
    public bool PartialResultsSaved { get; set; }

    /// <summary>
    /// 實際停止時間
    /// </summary>
    public DateTime ActualStopTime { get; set; }

    /// <summary>
    /// 優雅關閉耗時（毫秒）
    /// </summary>
    public long GracefulShutdownDurationMs { get; set; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static CancellationResult CreateSuccess() => new()
    {
        Success = true,
        Message = "取消操作成功完成",
        ActualStopTime = DateTime.UtcNow
    };

    /// <summary>
    /// 建立未找到結果
    /// </summary>
    public static CancellationResult CreateNotFound() => new()
    {
        Success = false,
        Message = "找不到指定的批次處理"
    };

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static CancellationResult CreateFailed(string message) => new()
    {
        Success = false,
        Message = message
    };
}