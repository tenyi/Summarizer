using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 進度通知服務介面，負責向客戶端推送即時進度更新
/// </summary>
public interface IProgressNotificationService
{
    /// <summary>
    /// 推送進度更新到指定批次群組
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="progress">進度資料</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyProgressUpdateAsync(string batchId, ProcessingProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送分段狀態更新到指定批次群組
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="segmentStatus">分段狀態</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifySegmentStatusUpdateAsync(string batchId, SegmentStatus segmentStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送階段變更到指定批次群組
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="newStage">新的處理階段</param>
    /// <param name="stageInfo">階段附加資訊</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyStageChangedAsync(string batchId, ProcessingStage newStage, object? stageInfo = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送批次處理完成通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="result">處理結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyBatchCompletedAsync(string batchId, object result, CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送批次處理失敗通知
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="error">錯誤訊息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyBatchFailedAsync(string batchId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 廣播系統狀態更新
    /// </summary>
    /// <param name="statusMessage">狀態訊息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task BroadcastSystemStatusAsync(string statusMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量推送多個進度更新（效能優化）
    /// </summary>
    /// <param name="updates">進度更新列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyBatchProgressUpdatesAsync(IEnumerable<(string batchId, ProcessingProgress progress)> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得批次群組的連線數量
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <returns>連線數量</returns>
    Task<int> GetBatchGroupConnectionCountAsync(string batchId);

    /// <summary>
    /// 檢查批次群組是否有活躍連線
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <returns>是否有活躍連線</returns>
    Task<bool> HasActiveBatchConnectionsAsync(string batchId);
}