using Microsoft.AspNetCore.SignalR;
using Summarizer.Hubs;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Collections.Concurrent;

namespace Summarizer.Services;

/// <summary>
/// 進度通知服務實作，負責向客戶端推送即時進度更新
/// </summary>
public class ProgressNotificationService : IProgressNotificationService
{
    private readonly IHubContext<BatchProcessingHub> _hubContext;
    private readonly ILogger<ProgressNotificationService> _logger;
    
    /// <summary>
    /// 批次群組連線計數緩存
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _connectionCountCache = new();
    
    /// <summary>
    /// 最近的進度更新緩存，用於防止重複推送
    /// </summary>
    private readonly ConcurrentDictionary<string, (ProcessingProgress progress, DateTime timestamp)> _recentUpdates = new();
    
    /// <summary>
    /// 防重複推送的時間間隔（毫秒）
    /// </summary>
    private const int DUPLICATE_PREVENTION_INTERVAL = 500;

    public ProgressNotificationService(
        IHubContext<BatchProcessingHub> hubContext,
        ILogger<ProgressNotificationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 啟動清理舊緩存的定時器
        _ = Task.Run(StartCacheCleanupTimer);
    }

    public async Task NotifyProgressUpdateAsync(
        string batchId, 
        ProcessingProgress progress, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (progress == null)
            throw new ArgumentNullException(nameof(progress));

        try
        {
            // 檢查是否需要推送（防止重複推送相同進度）
            if (!ShouldPushProgressUpdate(batchId, progress))
            {
                return;
            }

            var groupName = $"batch_{batchId}";
            
            // 推送進度更新
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ProgressUpdate", progress, cancellationToken);
            
            // 更新緩存
            _recentUpdates[batchId] = (progress, DateTime.UtcNow);
            
            _logger.LogDebug(
                "Progress update sent for batch {BatchId}: {Progress}% at stage {Stage}", 
                batchId, 
                progress.OverallProgress, 
                progress.CurrentStage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to notify progress update for batch {BatchId}", batchId);
            throw;
        }
    }

    public async Task NotifySegmentStatusUpdateAsync(
        string batchId, 
        SegmentStatus segmentStatus, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (segmentStatus == null)
            throw new ArgumentNullException(nameof(segmentStatus));

        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName)
                .SendAsync("SegmentStatusUpdate", segmentStatus, cancellationToken);
            
            _logger.LogDebug(
                "Segment status update sent for batch {BatchId}, segment {SegmentIndex}: {Status}", 
                batchId, 
                segmentStatus.Index, 
                segmentStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to notify segment status update for batch {BatchId}, segment {SegmentIndex}", 
                batchId, segmentStatus.Index);
            throw;
        }
    }

    public async Task NotifyStageChangedAsync(
        string batchId, 
        ProcessingStage newStage, 
        object? stageInfo = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));

        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName)
                .SendAsync("StageChanged", newStage, stageInfo, cancellationToken);
            
            _logger.LogInformation(
                "Stage change notification sent for batch {BatchId}: {NewStage}", 
                batchId, newStage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to notify stage change for batch {BatchId} to stage {NewStage}", 
                batchId, newStage);
            throw;
        }
    }

    public async Task NotifyBatchCompletedAsync(
        string batchId, 
        object result, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName)
                .SendAsync("BatchCompleted", batchId, result, cancellationToken);
            
            // 清理相關緩存
            _recentUpdates.TryRemove(batchId, out _);
            _connectionCountCache.TryRemove(groupName, out _);
            
            _logger.LogInformation(
                "Batch completion notification sent for batch {BatchId}", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to notify batch completion for batch {BatchId}", batchId);
            throw;
        }
    }

    public async Task NotifyBatchFailedAsync(
        string batchId, 
        string error, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(batchId))
            throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));
        
        if (string.IsNullOrEmpty(error))
            throw new ArgumentException("Error message cannot be null or empty", nameof(error));

        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName)
                .SendAsync("BatchFailed", batchId, error, cancellationToken);
            
            // 清理相關緩存
            _recentUpdates.TryRemove(batchId, out _);
            _connectionCountCache.TryRemove(groupName, out _);
            
            _logger.LogWarning(
                "Batch failure notification sent for batch {BatchId}: {Error}", 
                batchId, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to notify batch failure for batch {BatchId}", batchId);
            throw;
        }
    }

    public async Task BroadcastSystemStatusAsync(
        string statusMessage, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(statusMessage))
            throw new ArgumentException("Status message cannot be null or empty", nameof(statusMessage));

        try
        {
            await _hubContext.Clients.All
                .SendAsync("SystemStatusUpdate", statusMessage, DateTime.UtcNow, cancellationToken);
            
            _logger.LogInformation("System status broadcast: {StatusMessage}", statusMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system status: {StatusMessage}", statusMessage);
            throw;
        }
    }

    public async Task NotifyBatchProgressUpdatesAsync(
        IEnumerable<(string batchId, ProcessingProgress progress)> updates, 
        CancellationToken cancellationToken = default)
    {
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));

        var updateList = updates.ToList();
        if (!updateList.Any())
            return;

        try
        {
            // 批量推送以提高效能
            var tasks = updateList
                .GroupBy(u => u.batchId)
                .Select(async group =>
                {
                    var batchId = group.Key;
                    var latestProgress = group.OrderByDescending(u => u.progress.LastUpdated).First().progress;
                    
                    await NotifyProgressUpdateAsync(batchId, latestProgress, cancellationToken);
                });

            await Task.WhenAll(tasks);
            
            _logger.LogDebug(
                "Batch progress updates sent for {BatchCount} batches", 
                updateList.Select(u => u.batchId).Distinct().Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send batch progress updates");
            throw;
        }
    }

    /// <summary>
        /// 取得特定批次組的連線數量
        /// </summary>
        public Task<int> GetBatchGroupConnectionCountAsync(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                throw new ArgumentException("BatchId cannot be null or empty", nameof(batchId));

            try
            {
                var groupName = $"batch_{batchId}";
                
                // 這裡需要實作實際的連線計數邏輯
                // SignalR 本身沒有提供直接獲取群組成員數量的 API
                // 可以通過維護自己的連線追蹤機制或使用 Redis 等外部存儲
                
                // 暫時返回緩存值或預設值
                var count = _connectionCountCache.GetValueOrDefault(groupName, 0);
                return Task.FromResult(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get connection count for batch {BatchId}", batchId);
                return Task.FromResult(0);
            }
        }

    public async Task<bool> HasActiveBatchConnectionsAsync(string batchId)
    {
        try
        {
            var connectionCount = await GetBatchGroupConnectionCountAsync(batchId);
            return connectionCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check active connections for batch {BatchId}", batchId);
            return false;
        }
    }

    /// <summary>
    /// 判斷是否應該推送進度更新（防止重複推送）
    /// </summary>
    private bool ShouldPushProgressUpdate(string batchId, ProcessingProgress progress)
    {
        if (!_recentUpdates.TryGetValue(batchId, out var cachedUpdate))
        {
            return true; // 沒有緩存，應該推送
        }

        var timeSinceLastUpdate = (DateTime.UtcNow - cachedUpdate.timestamp).TotalMilliseconds;
        
        // 如果距離上次推送時間太短，檢查進度是否有顯著變化
        if (timeSinceLastUpdate < DUPLICATE_PREVENTION_INTERVAL)
        {
            var progressDelta = Math.Abs(progress.OverallProgress - cachedUpdate.progress.OverallProgress);
            var stageChanged = progress.CurrentStage != cachedUpdate.progress.CurrentStage;
            
            // 只有在階段變化或進度變化超過 1% 時才推送
            return stageChanged || progressDelta >= 1.0;
        }

        return true; // 時間間隔足夠，應該推送
    }

    /// <summary>
    /// 定期清理過期的緩存資料
    /// </summary>
    private async Task StartCacheCleanupTimer()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(5)); // 每 5 分鐘清理一次
        
        try
        {
            while (await timer.WaitForNextTickAsync())
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-10); // 清理 10 分鐘前的資料
                
                var expiredKeys = _recentUpdates
                    .Where(kvp => kvp.Value.timestamp < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _recentUpdates.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache cleanup timer encountered an error");
        }
    }
}