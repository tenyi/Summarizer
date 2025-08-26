using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 取消操作服務介面
/// </summary>
public interface ICancellationService : IDisposable
{
    /// <summary>
    /// 註冊批次處理的取消令牌
    /// </summary>
    /// <param name="batchId">批次識別碼</param>
    /// <param name="context">批次處理上下文</param>
    /// <returns>取消令牌</returns>
    CancellationToken RegisterBatchProcess(Guid batchId, BatchProcessingContext context);

    /// <summary>
    /// 請求取消批次處理
    /// </summary>
    /// <param name="request">取消請求</param>
    /// <returns>取消結果</returns>
    Task<CancellationResult> RequestCancellationAsync(CancellationRequest request);

    /// <summary>
    /// 檢查是否已請求取消
    /// </summary>
    /// <param name="batchId">批次識別碼</param>
    /// <returns>是否已請求取消</returns>
    bool IsCancellationRequested(Guid batchId);

    /// <summary>
    /// 獲取取消令牌
    /// </summary>
    /// <param name="batchId">批次識別碼</param>
    /// <returns>取消令牌，如果不存在則返回 null</returns>
    CancellationToken? GetCancellationToken(Guid batchId);

    /// <summary>
    /// 設定安全檢查點
    /// </summary>
    /// <param name="batchId">批次識別碼</param>
    /// <param name="isAtCheckpoint">是否在檢查點</param>
    void SetSafeCheckpoint(Guid batchId, bool isAtCheckpoint = true);
}