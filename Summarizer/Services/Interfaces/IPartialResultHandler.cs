using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 部分結果處理服務介面
/// 負責處理批次處理取消時的部分結果保存和品質評估
/// </summary>
public interface IPartialResultHandler
{
    /// <summary>
    /// 處理部分結果（主要入口點）
    /// 當批次處理被取消且用戶選擇保存部分結果時調用
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="userId">使用者 ID</param>
    /// <param name="completedSegments">已完成的分段列表</param>
    /// <param name="totalSegments">總分段數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>處理後的部分結果</returns>
    Task<PartialResult> ProcessPartialResultAsync(
        Guid batchId,
        string userId,
        List<SegmentSummaryTask> completedSegments,
        int totalSegments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 收集已完成的分段結果
    /// 從任務列表中提取已成功完成的分段
    /// </summary>
    /// <param name="allTasks">所有任務列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已完成的分段列表</returns>
    Task<List<SegmentSummaryTask>> CollectCompletedSegmentsAsync(
        List<SegmentSummaryTask> allTasks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 評估部分結果的品質
    /// 分析完整性、連貫性和可用性
    /// </summary>
    /// <param name="completedSegments">已完成的分段</param>
    /// <param name="totalSegments">總分段數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>品質評估結果</returns>
    Task<PartialResultQuality> EvaluateResultQualityAsync(
        List<SegmentSummaryTask> completedSegments,
        int totalSegments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成部分摘要
    /// 將已完成的分段合併成一個部分完整的摘要
    /// </summary>
    /// <param name="completedSegments">已完成的分段</param>
    /// <param name="quality">品質評估結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併後的部分摘要</returns>
    Task<string> GeneratePartialSummaryAsync(
        List<SegmentSummaryTask> completedSegments,
        PartialResultQuality quality,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存部分結果
    /// 將部分結果持久化到資料庫
    /// </summary>
    /// <param name="partialResult">要保存的部分結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功保存</returns>
    Task<bool> SavePartialResultAsync(
        PartialResult partialResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 獲取部分結果
    /// 根據 ID 獲取已保存的部分結果
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="userId">使用者 ID（用於權限檢查）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部分結果，如果不存在或無權限則返回 null</returns>
    Task<PartialResult?> GetPartialResultAsync(
        Guid partialResultId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新部分結果狀態
    /// 當用戶做出保存/丟棄決定時更新狀態
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="status">新狀態</param>
    /// <param name="userComment">用戶評論（可選）</param>
    /// <param name="userId">使用者 ID（用於權限檢查）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功更新</returns>
    Task<bool> UpdatePartialResultStatusAsync(
        Guid partialResultId,
        PartialResultStatus status,
        string? userComment,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 獲取用戶的部分結果列表
    /// 獲取特定用戶的所有部分結果
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="status">狀態篩選（可選）</param>
    /// <param name="pageIndex">頁面索引</param>
    /// <param name="pageSize">頁面大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部分結果列表</returns>
    Task<List<PartialResult>> GetUserPartialResultsAsync(
        string userId,
        PartialResultStatus? status = null,
        int pageIndex = 0,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理過期的部分結果
    /// 定期清理過期未決定的部分結果
    /// </summary>
    /// <param name="expireAfterHours">多少小時後過期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的記錄數量</returns>
    Task<int> CleanupExpiredPartialResultsAsync(
        int expireAfterHours = 24,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查是否可以從部分結果繼續處理
    /// 分析部分結果是否有足夠的品質來繼續完成剩餘的處理
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="userId">使用者 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否可以繼續處理</returns>
    Task<bool> CanContinueFromPartialResultAsync(
        Guid partialResultId,
        string userId,
        CancellationToken cancellationToken = default);
}