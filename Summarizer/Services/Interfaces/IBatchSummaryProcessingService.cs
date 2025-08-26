using Summarizer.Models.BatchProcessing;
using Summarizer.Models.TextSegmentation;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 批次摘要處理服務介面
/// </summary>
public interface IBatchSummaryProcessingService
{
    /// <summary>
    /// 開始批次摘要處理
    /// </summary>
    /// <param name="segments">分段結果列表</param>
    /// <param name="originalText">原始文本</param>
    /// <param name="userId">使用者 ID</param>
    /// <param name="concurrentLimit">併發限制（可選）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>批次處理 ID</returns>
    Task<Guid> StartBatchProcessingAsync(
        List<SegmentResult> segments, 
        string originalText, 
        string? userId = null, 
        int? concurrentLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得批次處理狀態
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <returns>批次處理進度</returns>
    Task<BatchProcessingProgress?> GetBatchProgressAsync(Guid batchId);

    /// <summary>
    /// 取得批次處理結果
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <returns>批次處理結果</returns>
    Task<BatchSummaryProcessor?> GetBatchResultAsync(Guid batchId);

    /// <summary>
    /// 暫停批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功暫停</returns>
    Task<bool> PauseBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢復批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功恢復</returns>
    Task<bool> ResumeBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功取消</returns>
    Task<bool> CancelBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理已完成的批次處理記錄
    /// </summary>
    /// <param name="olderThanHours">清理多少小時前的記錄</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的記錄數量</returns>
    Task<int> CleanupCompletedBatchesAsync(int olderThanHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的所有批次處理
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="pageIndex">頁面索引</param>
    /// <param name="pageSize">頁面大小</param>
    /// <returns>批次處理列表</returns>
    Task<List<BatchProcessingProgress>> GetUserBatchesAsync(string userId, int pageIndex = 0, int pageSize = 10);
}