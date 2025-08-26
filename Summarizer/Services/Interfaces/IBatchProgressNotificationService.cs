using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 批次處理進度通知服務介面
/// </summary>
public interface IBatchProgressNotificationService
{
    /// <summary>
    /// 發送批次處理進度更新
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="progress">進度資訊</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyProgressUpdateAsync(Guid batchId, BatchProcessingProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送批次處理狀態變更
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="status">新狀態</param>
    /// <param name="message">狀態訊息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyStatusChangeAsync(Guid batchId, BatchProcessingStatus status, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送分段處理完成通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="segmentIndex">分段索引</param>
    /// <param name="segmentResult">分段處理結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifySegmentCompletedAsync(Guid batchId, int segmentIndex, SegmentSummaryResult segmentResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送批次處理完成通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="processor">批次處理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyBatchCompletedAsync(Guid batchId, BatchSummaryProcessor processor, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送錯誤通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="error">錯誤訊息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyErrorAsync(Guid batchId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送取消請求通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="request">取消請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyCancellationRequestedAsync(Guid batchId, CancellationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 發送部分結果已保存通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyPartialResultSavedAsync(Guid batchId, Guid partialResultId, CancellationToken cancellationToken = default);
}