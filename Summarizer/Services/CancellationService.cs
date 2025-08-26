using System.Collections.Concurrent;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// 取消操作服務
/// </summary>
public class CancellationService : ICancellationService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens;
    private readonly ConcurrentDictionary<Guid, BatchProcessingContext> _activeProcesses;
    private readonly IBatchProgressNotificationService _notificationService;
    private readonly IPartialResultHandler _partialResultHandler;
    private readonly ILogger<CancellationService> _logger;

    public CancellationService(
        IBatchProgressNotificationService notificationService,
        IPartialResultHandler partialResultHandler,
        ILogger<CancellationService> logger)
    {
        _cancellationTokens = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        _activeProcesses = new ConcurrentDictionary<Guid, BatchProcessingContext>();
        _notificationService = notificationService;
        _partialResultHandler = partialResultHandler;
        _logger = logger;
    }

    /// <summary>
    /// 註冊批次處理的取消令牌
    /// </summary>
    public CancellationToken RegisterBatchProcess(Guid batchId, BatchProcessingContext context)
    {
        var cts = new CancellationTokenSource();
        _cancellationTokens[batchId] = cts;
        _activeProcesses[batchId] = context;

        _logger.LogDebug("已註冊批次處理取消令牌: {BatchId}", batchId);
        return cts.Token;
    }

    /// <summary>
    /// 請求取消批次處理
    /// </summary>
    public async Task<CancellationResult> RequestCancellationAsync(CancellationRequest request)
    {
        try
        {
            _logger.LogInformation("收到取消請求: BatchId={BatchId}, UserId={UserId}, Reason={Reason}", 
                request.BatchId, request.UserId, request.Reason);

            // 檢查批次是否存在
            if (!_cancellationTokens.TryGetValue(request.BatchId, out var cts))
            {
                _logger.LogWarning("找不到批次處理: {BatchId}", request.BatchId);
                return CancellationResult.CreateNotFound();
            }

            if (!_activeProcesses.TryGetValue(request.BatchId, out var context))
            {
                _logger.LogWarning("找不到批次處理上下文: {BatchId}", request.BatchId);
                return CancellationResult.CreateNotFound();
            }

            // 更新取消狀態
            context.IsCancellationRequested = true;
            context.CancellationRequest = request;
            context.CancellationRequestTime = DateTime.UtcNow;

            // 發送即時通知
            await _notificationService.NotifyCancellationRequestedAsync(request.BatchId, request);

            // 根據取消類型執行對應操作
            if (request.ForceCancel)
            {
                return await ForceCancel(request.BatchId, cts, context);
            }
            else
            {
                return await GracefulCancel(request.BatchId, cts, context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消操作發生錯誤: {BatchId}", request.BatchId);
            return CancellationResult.CreateFailed($"取消操作失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 優雅取消
    /// </summary>
    private async Task<CancellationResult> GracefulCancel(Guid batchId, CancellationTokenSource cts, BatchProcessingContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // 標記取消但不立即停止
            cts.Cancel();
            
            // 等待處理到達安全檢查點
            var timeout = TimeSpan.FromSeconds(30); // 30 秒超時
            var maxWaitTime = DateTime.UtcNow.Add(timeout);
            
            while (DateTime.UtcNow < maxWaitTime && !context.IsAtSafeCheckpoint)
            {
                await Task.Delay(100); // 每 100ms 檢查一次
            }

            stopwatch.Stop();

            // 處理部分結果（如果需要）
            var partialResultSaved = false;
            if (context.CancellationRequest?.SavePartialResults == true)
            {
                partialResultSaved = await ProcessPartialResultAsync(batchId, context);
            }

            var result = new CancellationResult
            {
                Success = true,
                Message = context.IsAtSafeCheckpoint ? "優雅取消成功" : "取消成功（超時）",
                ActualStopTime = DateTime.UtcNow,
                GracefulShutdownDurationMs = stopwatch.ElapsedMilliseconds,
                PartialResultsSaved = partialResultSaved
            };

            // 記錄取消審核日誌
            await LogCancellationAudit(batchId, context, result);

            // 清理資源
            CleanupBatchProcess(batchId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "優雅取消失敗: {BatchId}", batchId);
            return CancellationResult.CreateFailed($"優雅取消失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 強制取消
    /// </summary>
    private async Task<CancellationResult> ForceCancel(Guid batchId, CancellationTokenSource cts, BatchProcessingContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // 立即取消
            cts.Cancel();
            
            // 強制中斷處理
            context.ForceTerminate = true;
            
            stopwatch.Stop();

            var result = new CancellationResult
            {
                Success = true,
                Message = "強制取消成功",
                ActualStopTime = DateTime.UtcNow,
                GracefulShutdownDurationMs = stopwatch.ElapsedMilliseconds,
                PartialResultsSaved = false // 強制取消不保存部分結果
            };

            // 記錄取消審核日誌
            await LogCancellationAudit(batchId, context, result);

            // 清理資源
            CleanupBatchProcess(batchId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "強制取消失敗: {BatchId}", batchId);
            return CancellationResult.CreateFailed($"強制取消失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 檢查是否已請求取消
    /// </summary>
    public bool IsCancellationRequested(Guid batchId)
    {
        return _cancellationTokens.TryGetValue(batchId, out var cts) && cts.Token.IsCancellationRequested;
    }

    /// <summary>
    /// 獲取取消令牌
    /// </summary>
    public CancellationToken? GetCancellationToken(Guid batchId)
    {
        return _cancellationTokens.TryGetValue(batchId, out var cts) ? cts.Token : null;
    }

    /// <summary>
    /// 設定安全檢查點
    /// </summary>
    public void SetSafeCheckpoint(Guid batchId, bool isAtCheckpoint = true)
    {
        if (_activeProcesses.TryGetValue(batchId, out var context))
        {
            context.IsAtSafeCheckpoint = isAtCheckpoint;
            _logger.LogDebug("批次處理檢查點狀態更新: BatchId={BatchId}, IsAtCheckpoint={IsAtCheckpoint}", 
                batchId, isAtCheckpoint);
        }
    }

    /// <summary>
    /// 記錄取消審核日誌
    /// </summary>
    private async Task LogCancellationAudit(Guid batchId, BatchProcessingContext context, CancellationResult result)
    {
        try
        {
            var auditLog = new CancellationAuditLog
            {
                BatchId = batchId,
                UserId = context.CancellationRequest?.UserId ?? "System",
                CancellationReason = context.CancellationRequest?.Reason ?? CancellationReason.SystemTimeout,
                RequestTime = context.CancellationRequestTime ?? DateTime.UtcNow,
                CompletionTime = result.ActualStopTime,
                Success = result.Success,
                Message = result.Message,
                PartialResultsSaved = result.PartialResultsSaved,
                GracefulShutdownDurationMs = result.GracefulShutdownDurationMs,
                UserComment = context.CancellationRequest?.UserComment ?? string.Empty
            };

            _logger.LogInformation("取消操作審核日誌: {@AuditLog}", auditLog);
            
            // 這裡可以將審核日誌寫入資料庫或其他持久化儲存
            // await _auditRepository.SaveCancellationAuditAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄取消審核日誌失敗: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 清理批次處理資源
    /// </summary>
    private void CleanupBatchProcess(Guid batchId)
    {
        if (_cancellationTokens.TryRemove(batchId, out var cts))
        {
            cts.Dispose();
        }
        
        _activeProcesses.TryRemove(batchId, out _);
        
        _logger.LogDebug("已清理批次處理資源: {BatchId}", batchId);
    }

    /// <summary>
    /// 處理部分結果
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="context">批次處理上下文</param>
    /// <returns>是否成功保存部分結果</returns>
    private async Task<bool> ProcessPartialResultAsync(Guid batchId, BatchProcessingContext context)
    {
        try
        {
            _logger.LogInformation("開始處理部分結果，BatchId: {BatchId}", batchId);

            // 取得任務列表
            if (context.BatchProcessor?.Tasks == null || !context.BatchProcessor.Tasks.Any())
            {
                _logger.LogWarning("批次處理器中沒有任務資料，BatchId: {BatchId}", batchId);
                return false;
            }

            // 收集已完成的分段
            var completedSegments = await _partialResultHandler.CollectCompletedSegmentsAsync(context.BatchProcessor.Tasks);
            
            if (!completedSegments.Any())
            {
                _logger.LogWarning("沒有已完成的分段可以處理，BatchId: {BatchId}", batchId);
                return false;
            }

            // 處理部分結果
            var partialResult = await _partialResultHandler.ProcessPartialResultAsync(
                batchId,
                context.CancellationRequest?.UserId ?? "Unknown",
                completedSegments,
                context.TotalSegments,
                CancellationToken.None);

            // 保存部分結果
            var saved = await _partialResultHandler.SavePartialResultAsync(partialResult);
            
            if (saved)
            {
                _logger.LogInformation("部分結果已成功保存，PartialResultId: {PartialResultId}", 
                    partialResult.PartialResultId);
                
                // 通知部分結果已保存
                await _notificationService.NotifyPartialResultSavedAsync(batchId, partialResult.PartialResultId);
            }
            else
            {
                _logger.LogError("保存部分結果失敗，BatchId: {BatchId}", batchId);
            }

            return saved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理部分結果時發生錯誤，BatchId: {BatchId}", batchId);
            return false;
        }
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        foreach (var cts in _cancellationTokens.Values)
        {
            cts?.Dispose();
        }
        
        _cancellationTokens.Clear();
        _activeProcesses.Clear();
    }
}

/// <summary>
/// 批次處理上下文
/// </summary>
public class BatchProcessingContext
{
    /// <summary>
    /// 批次識別碼
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 是否已請求取消
    /// </summary>
    public bool IsCancellationRequested { get; set; }

    /// <summary>
    /// 取消請求
    /// </summary>
    public CancellationRequest? CancellationRequest { get; set; }

    /// <summary>
    /// 取消請求時間
    /// </summary>
    public DateTime? CancellationRequestTime { get; set; }

    /// <summary>
    /// 是否在安全檢查點
    /// </summary>
    public bool IsAtSafeCheckpoint { get; set; }

    /// <summary>
    /// 是否強制終止
    /// </summary>
    public bool ForceTerminate { get; set; }

    /// <summary>
    /// 處理開始時間
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 當前處理階段
    /// </summary>
    public ProcessingStage CurrentStage { get; set; } = ProcessingStage.Initializing;

    /// <summary>
    /// 批次處理器實例（用於存取任務列表）
    /// </summary>
    public BatchSummaryProcessor? BatchProcessor { get; set; }

    /// <summary>
    /// 已完成的分段數量
    /// </summary>
    public int CompletedSegments { get; set; }

    /// <summary>
    /// 總分段數量
    /// </summary>
    public int TotalSegments { get; set; }
}

/// <summary>
/// 取消審核日誌
/// </summary>
public class CancellationAuditLog
{
    /// <summary>
    /// 批次識別碼
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 取消原因
    /// </summary>
    public CancellationReason CancellationReason { get; set; }

    /// <summary>
    /// 請求時間
    /// </summary>
    public DateTime RequestTime { get; set; }

    /// <summary>
    /// 完成時間
    /// </summary>
    public DateTime CompletionTime { get; set; }

    /// <summary>
    /// 是否成功
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
    /// 優雅關閉耗時（毫秒）
    /// </summary>
    public long GracefulShutdownDurationMs { get; set; }

    /// <summary>
    /// 使用者註解
    /// </summary>
    public string UserComment { get; set; } = string.Empty;
}