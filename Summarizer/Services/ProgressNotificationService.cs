using Microsoft.AspNetCore.SignalR;
using Summarizer.Hubs;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Collections.Concurrent;

namespace Summarizer.Services;

/// <summary>
/// 進度通知服務實作類別
/// 
/// 此服務負責處理批次處理進度的即時通知功能，主要通過 SignalR 向連接的客戶端推送：
/// - 處理進度更新通知
/// - 處理階段變更通知
/// - 批次完成/失敗通知
/// - 系統狀態廣播
/// - 區段狀態更新
/// 
/// 主要特性：
/// - 防重複推送機制：避免在短時間內推送相同或相似的進度更新
/// - 連線管理：追蹤各批次群組的活躍連線數量
/// - 效能優化：批量推送多個進度更新以提高效率
/// - 記憶體管理：自動清理過期的快取資料
/// - 錯誤處理：完善的異常處理和日誌記錄
/// 
/// 設計模式：
/// - 實現 IProgressNotificationService 介面
/// - 使用依賴注入模式注入 IHubContext 和 ILogger
/// - 使用 ConcurrentDictionary 確保執行緒安全
/// - 使用非同步編程模式處理所有通知操作
/// </summary>
public class ProgressNotificationService : IProgressNotificationService
{
    /// <summary>
    /// SignalR Hub 上下文，用於向客戶端推送訊息
    /// 
    /// 此上下文提供以下功能：
    /// - 向特定群組推送訊息
    /// - 向所有連接的客戶端廣播訊息
    /// - 管理客戶端連線和群組
    /// </summary>
    private readonly IHubContext<BatchProcessingHub> _hubContext;
    
    /// <summary>
    /// 日誌記錄器，用於記錄服務操作和錯誤資訊
    /// 
    /// 記錄等級包括：
    /// - Debug：詳細的調試資訊
    /// - Information：一般操作資訊
    /// - Warning：警告訊息
    /// - Error：錯誤訊息
    /// </summary>
    private readonly ILogger<ProgressNotificationService> _logger;
    
    /// <summary>
    /// 批次群組連線計數快取
    /// 
    /// Key: 群組名稱 (格式: "batch_{batchId}")
    /// Value: 該群組的活躍連線數量
    /// 
    /// 使用場景：
    /// - 檢查批次是否還有活躍的監聽者
    /// - 優化推送策略（無連線時可跳過推送）
    /// - 統計和監控用途
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _connectionCountCache = new();
    
    /// <summary>
    /// 最近的進度更新快取，用於防止重複推送
    /// 
    /// Key: 批次 ID
    /// Value: Tuple 包含 (ProcessingProgress 物件, 時間戳記)
    /// 
    /// 快取策略：
    /// - 儲存最近一次推送的進度資訊
    /// - 比較新舊進度決定是否需要推送
    /// - 定期清理過期的快取資料
    /// </summary>
    private readonly ConcurrentDictionary<string, (ProcessingProgress progress, DateTime timestamp)> _recentUpdates = new();
    
    /// <summary>
    /// 防重複推送的時間間隔（毫秒）
    /// 
    /// 設定值: 500 毫秒
    /// 
    /// 作用：
    /// - 在此時間間隔內，只在進度變化顯著時才推送
    /// - 避免過於頻繁的推送影響效能
    /// - 平衡即時性和系統負載
    /// </summary>
    private const int DUPLICATE_PREVENTION_INTERVAL = 500;

    /// <summary>
    /// 初始化進度通知服務
    /// </summary>
    /// <param name="hubContext">
    /// SignalR Hub 上下文，用於推送訊息到客戶端
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <param name="logger">
    /// 日誌記錄器，用於記錄服務操作和錯誤
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// 當 hubContext 或 logger 為 null 時拋出
    /// </exception>
    /// <remarks>
    /// 建構函式會自動啟動快取清理定時器，在背景執行緒中運行
    /// 定時器每 5 分鐘清理一次過期的快取資料
    /// </remarks>
    public ProgressNotificationService(
        IHubContext<BatchProcessingHub> hubContext,
        ILogger<ProgressNotificationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 啟動清理舊快取的定時器（在背景執行緒中運行）
        // 此定時器負責定期清理過期的進度更新快取，避免記憶體洩漏
        _ = Task.Run(StartCacheCleanupTimer);
    }

    /// <summary>
    /// 通知批次處理進度更新
    /// 
    /// 向指定批次的所有連接客戶端推送處理進度更新訊息。
    /// 此方法會檢查是否需要推送（防重複推送機制），並且會更新內部快取。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="progress">
    /// 處理進度資訊物件，包含整體進度、目前階段等資訊
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// 當 progress 為 null 時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 推送邏輯：
    /// 1. 驗證輸入參數
    /// 2. 檢查是否需要推送（防重複機制）
    /// 3. 構造群組名稱 (batch_{batchId})
    /// 4. 通過 SignalR 推送訊息到指定群組
    /// 5. 更新快取記錄
    /// 6. 記錄調試日誌
    /// 
    /// 防重複推送機制：
    /// - 在 500 毫秒內只推送顯著的進度變化
    /// - 階段變更時一定推送
    /// - 進度變化超過 1% 時推送
    /// </remarks>
    public async Task NotifyProgressUpdateAsync(
        string batchId, 
        ProcessingProgress progress, 
        CancellationToken cancellationToken = default)
    {
        // 驗證輸入參數的有效性
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (progress == null)
            throw new ArgumentNullException(nameof(progress));

        try
        {
            // 檢查是否需要推送進度更新（防止重複推送相同進度）
            // 如果不需要推送，直接返回以節省資源
            if (!ShouldPushProgressUpdate(batchId, progress))
            {
                return;
            }

            // 構造 SignalR 群組名稱，格式為 "batch_{batchId}"
            // 這樣可以將訊息推送給關注特定批次的所有客戶端
            var groupName = $"batch_{batchId}";
            
            // 通過 SignalR Hub 向指定群組推送進度更新訊息
            // "ProgressUpdate" 是客戶端需要監聽的事件名稱
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ProgressUpdate", progress, cancellationToken);
            
            // 更新內部快取，記錄此次推送的進度和時間戳
            // 用於後續的防重複推送檢查
            _recentUpdates[batchId] = (progress, DateTime.UtcNow);
            
            // 記錄調試日誌，包含批次 ID、進度百分比和目前階段
            _logger.LogDebug(
                "Progress update sent for batch {BatchId}: {Progress}% at stage {Stage}", 
                batchId, 
                progress.OverallProgress, 
                progress.CurrentStage);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌，包含批次 ID 和異常詳細資訊
            _logger.LogError(ex, 
                "Failed to notify progress update for batch {BatchId}", batchId);
            // 重新拋出異常，讓呼叫者知道推送失敗
            throw;
        }
    }

    /// <summary>
    /// 通知區段狀態更新
    /// 
    /// 向指定批次的所有連接客戶端推送特定區段的處理狀態更新訊息。
    /// 用於追蹤批次處理中各個區段的詳細狀態變化。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="segmentStatus">
    /// 區段狀態資訊物件，包含區段索引、狀態等資訊
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// 當 segmentStatus 為 null 時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 推送流程：
    /// 1. 驗證輸入參數
    /// 2. 構造群組名稱
    /// 3. 推送 "SegmentStatusUpdate" 事件到指定群組
    /// 4. 記錄調試日誌
    /// 
    /// 使用場景：
    /// - 區段處理開始時
    /// - 區段處理完成時
    /// - 區段處理失敗時
    /// - 區段狀態發生變化時
    /// </remarks>
    public async Task NotifySegmentStatusUpdateAsync(
        string batchId, 
        SegmentStatus segmentStatus, 
        CancellationToken cancellationToken = default)
    {
        // 驗證輸入參數
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (segmentStatus == null)
            throw new ArgumentNullException(nameof(segmentStatus));

        try
        {
            // 構造 SignalR 群組名稱
            var groupName = $"batch_{batchId}";
            
            // 推送區段狀態更新訊息
            // "SegmentStatusUpdate" 是客戶端監聽的事件名稱
            await _hubContext.Clients.Group(groupName)
                .SendAsync("SegmentStatusUpdate", segmentStatus, cancellationToken);
            
            // 記錄調試日誌，包含批次 ID、區段索引和狀態
            _logger.LogDebug(
                "Segment status update sent for batch {BatchId}, segment {SegmentIndex}: {Status}", 
                batchId, 
                segmentStatus.Index, 
                segmentStatus.Status);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, 
                "Failed to notify segment status update for batch {BatchId}, segment {SegmentIndex}", 
                batchId, segmentStatus.Index);
            throw;
        }
    }

    /// <summary>
    /// 通知處理階段變更
    /// 
    /// 向指定批次的所有連接客戶端推送處理階段變更通知。
    /// 當批次處理從一個階段轉移到另一個階段時呼叫此方法。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="newStage">
    /// 新的處理階段列舉值
    /// 表示批次處理目前處於哪個階段
    /// </param>
    /// <param name="stageInfo">
    /// 可選的階段附加資訊物件
    /// 可以包含階段特定的詳細資料，如錯誤資訊、統計資料等
    /// 預設值為 null
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 推送流程：
    /// 1. 驗證批次 ID 參數
    /// 2. 構造群組名稱
    /// 3. 推送 "StageChanged" 事件，包含新階段和附加資訊
    /// 4. 記錄資訊日誌
    /// 
    /// 常見的處理階段包括：
    /// - 初始化階段
    /// - 文字分割階段
    /// - 摘要生成階段
    /// - 結果合併階段
    /// - 完成階段
    /// 
    /// stageInfo 的使用場景：
    /// - 階段開始時：包含預計處理的項目數量
    /// - 階段結束時：包含處理統計資訊
    /// - 階段失敗時：包含錯誤詳細資訊
    /// </remarks>
    public async Task NotifyStageChangedAsync(
        string batchId, 
        ProcessingStage newStage, 
        object? stageInfo = null, 
        CancellationToken cancellationToken = default)
    {
        // 驗證批次 ID 參數
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));

        try
        {
            // 構造 SignalR 群組名稱
            var groupName = $"batch_{batchId}";
            
            // 推送階段變更通知
            // "StageChanged" 是客戶端監聽的事件名稱
            // 同時推送新階段和可選的階段附加資訊
            await _hubContext.Clients.Group(groupName)
                .SendAsync("StageChanged", newStage, stageInfo, cancellationToken);
            
            // 記錄資訊日誌，包含批次 ID 和新階段
            _logger.LogInformation(
                "Stage change notification sent for batch {BatchId}: {NewStage}", 
                batchId, newStage);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, 
                "Failed to notify stage change for batch {BatchId} to stage {NewStage}", 
                batchId, newStage);
            throw;
        }
    }

    /// <summary>
    /// 通知批次處理完成
    /// 
    /// 向指定批次的所有連接客戶端推送批次處理成功完成的通知。
    /// 此方法會在推送完成通知後清理相關的快取資料。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="result">
    /// 批次處理的結果物件
    /// 包含處理結果的詳細資料，如摘要內容、統計資訊等
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// 當 result 為 null 時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 處理流程：
    /// 1. 驗證輸入參數
    /// 2. 構造群組名稱
    /// 3. 推送 "BatchCompleted" 事件，包含批次 ID 和結果
    /// 4. 清理相關快取資料（進度更新快取和連線計數快取）
    /// 5. 記錄資訊日誌
    /// 
    /// 快取清理的原因：
    /// - 批次已完成，不再需要追蹤進度更新
    /// - 釋放記憶體資源
    /// - 避免快取資料過期後影響新批次的處理
    /// 
    /// result 物件通常包含：
    /// - 處理的摘要結果
    /// - 處理統計資訊（處理時間、項目數量等）
    /// - 任何需要返回給客戶端的資料
    /// </remarks>
    public async Task NotifyBatchCompletedAsync(
        string batchId, 
        object result, 
        CancellationToken cancellationToken = default)
    {
        // 驗證輸入參數
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        try
        {
            // 構造 SignalR 群組名稱
            var groupName = $"batch_{batchId}";
            
            // 推送批次完成通知
            // "BatchCompleted" 是客戶端監聽的事件名稱
            await _hubContext.Clients.Group(groupName)
                .SendAsync("BatchCompleted", batchId, result, cancellationToken);
            
            // 清理相關快取資料，因為批次已完成
            // 清理進度更新快取，避免記憶體洩漏
            _recentUpdates.TryRemove(batchId, out _);
            // 清理連線計數快取
            _connectionCountCache.TryRemove(groupName, out _);
            
            // 記錄資訊日誌
            _logger.LogInformation(
                "Batch completion notification sent for batch {BatchId}", batchId);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, 
                "Failed to notify batch completion for batch {BatchId}", batchId);
            throw;
        }
    }

    /// <summary>
    /// 通知批次處理失敗
    /// 
    /// 向指定批次的所有連接客戶端推送批次處理失敗的通知。
    /// 此方法會在推送失敗通知後清理相關的快取資料。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="error">
    /// 錯誤訊息字串，描述失敗的原因
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 或 error 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 處理流程：
    /// 1. 驗證輸入參數（批次 ID 和錯誤訊息）
    /// 2. 構造群組名稱
    /// 3. 推送 "BatchFailed" 事件，包含批次 ID 和錯誤訊息
    /// 4. 清理相關快取資料（進度更新快取和連線計數快取）
    /// 5. 記錄警告日誌
    /// 
    /// 快取清理的原因：
    /// - 批次已失敗，不再需要追蹤進度更新
    /// - 釋放記憶體資源
    /// - 避免快取資料影響新批次的處理
    /// 
    /// 錯誤訊息的格式建議：
    /// - 簡潔明瞭的錯誤描述
    /// - 包含足夠的上下文資訊
    /// - 避免暴露敏感的系統資訊
    /// 
    /// 使用場景：
    /// - 批次處理中發生未預期的異常
    /// - 外部服務調用失敗
    /// - 資料驗證失敗
    /// - 資源不足或超時
    /// </remarks>
    public async Task NotifyBatchFailedAsync(
        string batchId, 
        string error, 
        CancellationToken cancellationToken = default)
    {
        // 驗證輸入參數
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (string.IsNullOrEmpty(error))
            throw new ArgumentException("Error message cannot be null or empty", nameof(error));

        try
        {
            // 構造 SignalR 群組名稱
            var groupName = $"batch_{batchId}";
            
            // 推送批次失敗通知
            // "BatchFailed" 是客戶端監聽的事件名稱
            await _hubContext.Clients.Group(groupName)
                .SendAsync("BatchFailed", batchId, error, cancellationToken);
            
            // 清理相關快取資料，因為批次已失敗
            // 清理進度更新快取
            _recentUpdates.TryRemove(batchId, out _);
            // 清理連線計數快取
            _connectionCountCache.TryRemove(groupName, out _);
            
            // 記錄警告日誌，包含批次 ID 和錯誤訊息
            _logger.LogWarning(
                "Batch failure notification sent for batch {BatchId}: {Error}", 
                batchId, error);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, 
                "Failed to notify batch failure for batch {BatchId}", batchId);
            throw;
        }
    }

    /// <summary>
    /// 廣播系統狀態更新
    /// 
    /// 向所有連接的客戶端廣播系統狀態訊息。
    /// 此方法用於向所有用戶通報系統層級的重要狀態變化。
    /// </summary>
    /// <param name="statusMessage">
    /// 系統狀態訊息字串
    /// 描述系統目前的狀態或重要通知
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示廣播操作的完成
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 statusMessage 為 null 或空字串時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當廣播操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 廣播流程：
    /// 1. 驗證狀態訊息參數
    /// 2. 使用 _hubContext.Clients.All 向所有連接的客戶端推送
    /// 3. 推送 "SystemStatusUpdate" 事件，包含狀態訊息和時間戳
    /// 4. 記錄資訊日誌
    /// 
    /// 使用場景：
    /// - 系統維護通知
    /// - 服務重啟通知
    /// - 系統負載警告
    /// - 緊急系統狀態通報
    /// - 版本更新通知
    /// 
    /// 訊息格式建議：
    /// - 簡潔明瞭的狀態描述
    /// - 包含時間戳記以便追蹤
    /// - 避免過於頻繁的廣播以免影響用戶體驗
    /// 
    /// 注意事項：
    /// - 此方法會向所有連接的客戶端推送訊息
    /// - 適用於系統層級的重要通知
    /// - 不適用於特定批次的狀態更新
    /// </remarks>
    public async Task BroadcastSystemStatusAsync(
        string statusMessage, 
        CancellationToken cancellationToken = default)
    {
        // 驗證狀態訊息參數
        if (string.IsNullOrEmpty(statusMessage))
            throw new ArgumentException("Status message cannot be null or empty", nameof(statusMessage));

        try
        {
            // 向所有連接的客戶端廣播系統狀態更新
            // 使用 _hubContext.Clients.All 推送給所有用戶
            // "SystemStatusUpdate" 是客戶端監聽的事件名稱
            await _hubContext.Clients.All
                .SendAsync("SystemStatusUpdate", statusMessage, DateTime.UtcNow, cancellationToken);
            
            // 記錄資訊日誌
            _logger.LogInformation("System status broadcast: {StatusMessage}", statusMessage);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, "Failed to broadcast system status: {StatusMessage}", statusMessage);
            throw;
        }
    }

    /// <summary>
    /// 批量通知批次進度更新
    /// 
    /// 批量處理多個批次的進度更新，提高推送效率。
    /// 此方法會將更新按批次分組，並為每個批次推送最新的進度資訊。
    /// </summary>
    /// <param name="updates">
    /// 進度更新集合，每個項目包含批次 ID 和對應的進度資訊
    /// 格式：IEnumerable&lt;(string batchId, ProcessingProgress progress)&gt;
    /// 不能為 null，否則會拋出 ArgumentNullException
    /// </param>
    /// <param name="cancellationToken">
    /// 取消權杖，用於取消非同步操作
    /// 預設值為 CancellationToken.None
    /// </param>
    /// <returns>
    /// 非同步任務，表示批量推送操作的完成
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// 當 updates 為 null 時拋出
    /// </exception>
    /// <exception cref="Exception">
    /// 當批量推送操作失敗時，會記錄錯誤日誌並重新拋出異常
    /// </exception>
    /// <remarks>
    /// 處理流程：
    /// 1. 驗證輸入參數
    /// 2. 將更新列表轉換為可列舉集合
    /// 3. 檢查是否有更新需要處理
    /// 4. 按批次 ID 分組更新
    /// 5. 為每個批次選擇最新的進度更新
    /// 6. 並行執行所有推送任務
    /// 7. 記錄調試日誌
    /// 
    /// 效能優化策略：
    /// - 批量處理減少網路往返次數
    /// - 並行推送提高處理速度
    /// - 按批次分組避免重複推送
    /// - 選擇最新進度避免過時資料
    /// 
    /// 分組邏輯：
    /// - 使用 LINQ GroupBy 按批次 ID 分組
    /// - 對於每個批次，選擇 LastUpdated 最新的進度
    /// - 確保每個批次只推送一次最新的狀態
    /// 
    /// 使用場景：
    /// - 批次處理服務需要推送多個批次的狀態更新
    /// - 定時狀態同步任務
    /// - 系統重啟後的狀態恢復推送
    /// </remarks>
    public async Task NotifyBatchProgressUpdatesAsync(
        IEnumerable<(string batchId, ProcessingProgress progress)> updates, 
        CancellationToken cancellationToken = default)
    {
        // 驗證輸入參數
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));

        // 將 IEnumerable 轉換為 List 以便重複使用
        var updateList = updates.ToList();
        
        // 如果沒有更新需要處理，直接返回
        if (!updateList.Any())
            return;

        try
        {
            // 批量推送以提高效能
            // 1. 按批次 ID 分組
            // 2. 對於每個批次，選擇最新（LastUpdated 最大）的進度
            // 3. 為每個批次調用 NotifyProgressUpdateAsync
            var tasks = updateList
                .GroupBy(u => u.batchId)  // 按批次 ID 分組
                .Select(async group =>
                {
                    var batchId = group.Key;
                    // 選擇該批次最新的進度更新
                    var latestProgress = group.OrderByDescending(u => u.progress.LastUpdated).First().progress;
                    
                    // 調用單個進度更新方法（會自動處理防重複推送邏輯）
                    await NotifyProgressUpdateAsync(batchId, latestProgress, cancellationToken);
                });

            // 並行執行所有推送任務
            await Task.WhenAll(tasks);
            
            // 記錄調試日誌，顯示處理的批次數量
            _logger.LogDebug(
                "Batch progress updates sent for {BatchCount} batches", 
                updateList.Select(u => u.batchId).Distinct().Count());
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌並重新拋出異常
            _logger.LogError(ex, "Failed to send batch progress updates");
            throw;
        }
    }

    /// <summary>
    /// 取得特定批次群組的連線數量
    /// 
    /// 獲取指定批次目前有多少活躍的客戶端連線。
    /// 此方法用於監控和統計目的，目前實現是基於內部快取。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <returns>
    /// 任務物件，結果為該批次群組的活躍連線數量
    /// 如果發生錯誤，返回 0
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時拋出
    /// </exception>
    /// <remarks>
    /// 目前實現說明：
    /// - 基於內部 _connectionCountCache 快取返回資料
    /// - 如果快取中沒有資料，返回預設值 0
    /// - 這是一個簡化的實現
    /// 
    /// 未來改進方向：
    /// - 整合 SignalR 的實際連線追蹤機制
    /// - 使用 Redis 等外部儲存來共享連線狀態
    /// - 實現分散式環境下的連線計數
    /// - 添加連線超時和清理機制
    /// 
    /// SignalR 限制：
    /// - SignalR 核心 API 沒有直接提供群組成員計數功能
    /// - 需要通過自定義的連線管理機制來實現
    /// - 可以通過 IUserIdProvider 或自定義中介軟體來追蹤
    /// 
    /// 使用場景：
    /// - 檢查批次是否還有活躍的監聽者
    /// - 統計系統使用情況
    /// - 決定是否需要推送通知
    /// - 資源使用優化
    /// </remarks>
    public Task<int> GetBatchGroupConnectionCountAsync(string batchId)
        {
        // 驗證批次 ID 參數
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));

        try
        {
            // 構造 SignalR 群組名稱
            var groupName = $"batch_{batchId}";
            
            // 注意：這裡需要實作實際的連線計數邏輯
            // SignalR 本身沒有提供直接獲取群組成員數量的 API
            // 可以通過以下方式實現：
            // 1. 維護自己的連線追蹤機制（在 Hub 中記錄連線/離線事件）
            // 2. 使用 Redis 等外部存儲來共享連線狀態
            // 3. 實現分散式環境下的連線計數同步
            
            // 暫時返回快取值或預設值
            // 從連線計數快取中獲取該群組的連線數量
            var count = _connectionCountCache.GetValueOrDefault(groupName, 0);
            return Task.FromResult(count);
        }
        catch (Exception ex)
        {
            // 記錄錯誤日誌，返回預設值 0
            _logger.LogError(ex, "Failed to get connection count for batch {BatchId}", batchId);
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// 檢查批次是否還有活躍的連線
    /// 
    /// 判斷指定批次目前是否還有客戶端保持連線。
    /// 此方法基於連線計數來判斷活躍狀態。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼
    /// 不能為 null 或空字串，否則會拋出 ArgumentException
    /// </param>
    /// <returns>
    /// 任務物件，結果為布林值：
    /// true - 該批次有活躍連線
    /// false - 該批次沒有活躍連線或發生錯誤
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 當 batchId 為 null 或空字串時，在內部方法中拋出
    /// </exception>
    /// <remarks>
    /// 判斷邏輯：
    /// 1. 呼叫 GetBatchGroupConnectionCountAsync 獲取連線數量
    /// 2. 如果連線數量大於 0，返回 true
    /// 3. 如果連線數量為 0 或發生錯誤，返回 false
    /// 
    /// 使用場景：
    /// - 決定是否需要推送進度更新（無連線時可跳過）
    /// - 清理資源時檢查是否還有監聽者
    /// - 統計和監控系統活躍批次
    /// - 優化系統資源使用
    /// 
    /// 注意事項：
    /// - 此方法的準確性取決於 GetBatchGroupConnectionCountAsync 的實現
    /// - 在連線追蹤不準確的情況下，可能會返回錯誤結果
    /// - 建議在重要決策前結合其他檢查機制
    /// 
    /// 效能考慮：
    /// - 此方法會呼叫非同步的連線計數查詢
    /// - 在高頻率呼叫場景下應考慮快取結果
    /// - 可以考慮添加連線狀態的本地快取
    /// </remarks>
    public async Task<bool> HasActiveBatchConnectionsAsync(string batchId)
    {
        try
        {
            // 獲取批次群組的連線數量
            var connectionCount = await GetBatchGroupConnectionCountAsync(batchId);
            
            // 如果連線數量大於 0，表示有活躍連線
            return connectionCount > 0;
        }
        catch (Exception ex)
        {
            // 如果發生錯誤，記錄日誌並返回 false（假設沒有活躍連線）
            _logger.LogError(ex, "Failed to check active connections for batch {BatchId}", batchId);
            return false;
        }
    }

    /// <summary>
    /// 判斷是否應該推送進度更新（防止重複推送）
    /// 
    /// 根據時間間隔和進度變化幅度決定是否需要推送新的進度更新。
    /// 此方法實現了防重複推送的智慧邏輯，避免過於頻繁的通知。
    /// </summary>
    /// <param name="batchId">
    /// 批次處理的唯一識別碼，用於查找快取的進度記錄
    /// </param>
    /// <param name="progress">
    /// 新的進度資訊物件，包含目前的處理狀態
    /// </param>
    /// <returns>
    /// 布林值，指示是否應該推送此進度更新：
    /// true - 應該推送
    /// false - 不應該推送（避免重複）
    /// </returns>
    /// <remarks>
    /// 判斷邏輯：
    /// 
    /// 1. 檢查快取中是否存在該批次的進度記錄
    ///    - 如果沒有快取記錄，說明是首次推送，應該推送
    /// 
    /// 2. 如果有快取記錄，計算距離上次推送的時間間隔
    ///    - 如果時間間隔小於防重複閾值（500毫秒），進行進度變化檢查
    ///    - 如果時間間隔足夠大，直接推送
    /// 
    /// 3. 進度變化檢查（在短時間間隔內）：
    ///    - 檢查處理階段是否發生變化（階段變化時一定推送）
    ///    - 檢查整體進度百分比變化是否超過 1%
    ///    - 只有在階段變化或進度變化顯著時才推送
    /// 
    /// 設計理念：
    /// - 平衡即時性和系統效能
    /// - 避免過於頻繁的推送影響用戶體驗
    /// - 確保重要狀態變化能夠及時通知
    /// - 節省網路頻寬和伺服器資源
    /// 
    /// 效能優化：
    /// - 使用 ConcurrentDictionary 確保執行緒安全
    /// - 快取最近的推送記錄
    /// - 定期清理過期的快取資料
    /// </remarks>
    private bool ShouldPushProgressUpdate(string batchId, ProcessingProgress progress)
    {
        // 檢查快取中是否存在該批次的進度記錄
        if (!_recentUpdates.TryGetValue(batchId, out var cachedUpdate))
        {
            // 沒有快取記錄，說明是首次推送，應該推送
            return true;
        }

        // 計算距離上次推送的時間間隔（毫秒）
        var timeSinceLastUpdate = (DateTime.UtcNow - cachedUpdate.timestamp).TotalMilliseconds;
        
        // 如果距離上次推送時間太短，檢查進度是否有顯著變化
        if (timeSinceLastUpdate < DUPLICATE_PREVENTION_INTERVAL)
        {
            // 計算進度變化幅度（百分比差異）
            var progressDelta = Math.Abs(progress.OverallProgress - cachedUpdate.progress.OverallProgress);
            
            // 檢查處理階段是否發生變化
            var stageChanged = progress.CurrentStage != cachedUpdate.progress.CurrentStage;
            
            // 只有在階段變化或進度變化超過 1% 時才推送
            // 這樣可以避免過於頻繁的推送，同時確保重要變化能夠及時通知
            return stageChanged || progressDelta >= 1.0;
        }

        // 時間間隔足夠，應該推送
        return true;
    }

    /// <summary>
    /// 定期清理過期的快取資料
    /// 
    /// 在背景執行緒中運行定時器，每 5 分鐘清理一次過期的進度更新快取。
    /// 此方法用於防止記憶體洩漏和維持快取資料的時效性。
    /// </summary>
    /// <returns>
    /// 非同步任務，表示清理定時器的運行
    /// 此任務會持續運行直到應用程式結束
    /// </returns>
    /// <remarks>
    /// 清理邏輯：
    /// 1. 使用 PeriodicTimer 建立每 5 分鐘觸發一次的定時器
    /// 2. 在每次觸發時，掃描所有快取項目
    /// 3. 識別超過 10 分鐘未更新的項目
    /// 4. 從快取中移除過期的項目
    /// 5. 記錄清理統計資訊
    /// 
    /// 設計參數：
    /// - 清理間隔：5 分鐘（平衡效能和記憶體使用）
    /// - 過期時間：10 分鐘（確保快取資料不會過時太久）
    /// - 背景執行：使用 Task.Run 避免阻塞主執行緒
    /// 
    /// 清理策略：
    /// - 基於時間戳記的過期檢查
    /// - 批量移除過期項目
    /// - 安全的並發操作（使用 ConcurrentDictionary）
    /// - 詳細的日誌記錄
    /// 
    /// 異常處理：
    /// - 捕獲並記錄清理過程中的異常
    /// - 不會因為清理失敗而影響主要功能
    /// - 確保定時器持續運行
    /// 
    /// 效能考慮：
    /// - 清理操作是非同步的，不會阻塞推送操作
    /// - 只在有過期項目時記錄日誌
    /// - 使用高效的 LINQ 查詢進行過期檢查
    /// 
    /// 記憶體管理：
    /// - 防止快取無限增長
    /// - 釋放已完成批次的記憶體資源
    /// - 維持系統的長期穩定運行
    /// </remarks>
    private async Task StartCacheCleanupTimer()
    {
        // 建立定期定時器，每 5 分鐘觸發一次清理操作
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        
        try
        {
            // 持續監聽定時器觸發事件
            while (await timer.WaitForNextTickAsync())
            {
                // 計算過期時間點：10 分鐘前的資料
                var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
                
                // 找出所有過期的快取項目
                // 使用 LINQ 查詢過期的鍵值對
                var expiredKeys = _recentUpdates
                    .Where(kvp => kvp.Value.timestamp < cutoffTime)  // 過期檢查
                    .Select(kvp => kvp.Key)  // 只取鍵
                    .ToList();  // 轉為列表以避免列舉期間的修改

                // 移除所有過期的快取項目
                foreach (var key in expiredKeys)
                {
                    _recentUpdates.TryRemove(key, out _);
                }

                // 如果有清理項目，記錄調試日誌
                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
                }
            }
        }
        catch (Exception ex)
        {
            // 記錄清理過程中的異常
            // 不會因為清理失敗而影響主要功能
            _logger.LogError(ex, "Cache cleanup timer encountered an error");
        }
    }
}