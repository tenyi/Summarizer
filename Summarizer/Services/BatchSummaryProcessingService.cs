using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.TextSegmentation;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// 批次摘要處理服務實作
/// </summary>
public class BatchSummaryProcessingService : IBatchSummaryProcessingService, IDisposable
{
    private readonly BatchProcessingConfig _config;
    private readonly ISummaryService _summaryService;
    private readonly IBatchProgressNotificationService _notificationService;
    private readonly ICancellationService _cancellationService;
    private readonly ILogger<BatchSummaryProcessingService> _logger;
    
    // 使用 ConcurrentDictionary 來存儲批次處理實例，支援併發訪問
    private readonly ConcurrentDictionary<Guid, BatchSummaryProcessor> _batchProcessors = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _batchCancellationTokens = new();
    private readonly ConcurrentDictionary<Guid, Task> _batchProcessingTasks = new();
    
    private readonly SemaphoreSlim _globalConcurrencyLimit;
    private bool _disposed = false;

    /// <summary>
    /// 建構函式
    /// </summary>
    public BatchSummaryProcessingService(
        IOptions<BatchProcessingConfig> config,
        ISummaryService summaryService,
        IBatchProgressNotificationService notificationService,
        ICancellationService cancellationService,
        ILogger<BatchSummaryProcessingService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 初始化全域併發限制信號量
        _globalConcurrencyLimit = new SemaphoreSlim(_config.MaxConcurrentLimit, _config.MaxConcurrentLimit);
    }

    /// <summary>
    /// 開始批次摘要處理
    /// </summary>
    public Task<Guid> StartBatchProcessingAsync(
        List<SegmentResult> segments, 
        string originalText, 
        string? userId = null, 
        int? concurrentLimit = null,
        CancellationToken cancellationToken = default)
    {
        // 輸入驗證
        if (segments == null || segments.Count == 0)
        {
            throw new ArgumentException("分段結果列表不能為空", nameof(segments));
        }

        if (string.IsNullOrWhiteSpace(originalText))
        {
            throw new ArgumentException("原始文本不能為空", nameof(originalText));
        }

        var batchId = Guid.NewGuid();
        _logger.LogInformation("開始建立批次處理任務，BatchId: {BatchId}，分段數量: {SegmentCount}", 
            batchId, segments.Count);

        try
        {
            // 建立批次處理器
            var batchProcessor = new BatchSummaryProcessor
            {
                BatchId = batchId,
                UserId = userId ?? string.Empty,
                OriginalText = originalText,
                ConcurrentLimit = concurrentLimit ?? _config.DefaultConcurrentLimit,
                Status = BatchProcessingStatus.Queued,
                StartTime = DateTime.UtcNow
            };

            // 建立分段摘要任務
            batchProcessor.Tasks = segments.Select((segment, index) => new SegmentSummaryTask
            {
                SegmentIndex = index,
                SourceSegment = segment,
                Status = SegmentTaskStatus.Pending
            }).ToList();

            // 存儲批次處理器
            _batchProcessors[batchId] = batchProcessor;

            // 建立批次處理上下文
            var context = new BatchProcessingContext
            {
                BatchId = batchId,
                TotalSegments = segments.Count,
                StartTime = DateTime.UtcNow,
                BatchProcessor = batchProcessor  // 設定批次處理器參照
            };

            // 向取消服務註冊批次處理，獲取取消令牌
            var batchCancellationToken = _cancellationService.RegisterBatchProcess(batchId, context);
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, batchCancellationToken);
            _batchCancellationTokens[batchId] = combinedCts;

            // 啟動批次處理任務
            var processingTask = ProcessBatchAsync(batchProcessor, combinedCts.Token);
            _batchProcessingTasks[batchId] = processingTask;

            // 不等待任務完成，立即返回批次 ID
            _logger.LogInformation("批次處理任務已建立並開始執行，BatchId: {BatchId}", batchId);
            
            return Task.FromResult(batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立批次處理任務失敗，BatchId: {BatchId}", batchId);
            
            // 清理資源
            _batchProcessors.TryRemove(batchId, out _);
            _batchCancellationTokens.TryRemove(batchId, out _);
            _batchProcessingTasks.TryRemove(batchId, out _);
            
            throw;
        }
    }

    /// <summary>
    /// 取得批次處理狀態
    /// </summary>
    public async Task<BatchProcessingProgress?> GetBatchProgressAsync(Guid batchId)
    {
        if (!_batchProcessors.TryGetValue(batchId, out var processor))
        {
            _logger.LogWarning("找不到批次處理器，BatchId: {BatchId}", batchId);
            return null;
        }

        return await Task.FromResult(new BatchProcessingProgress
        {
            BatchId = batchId,
            TotalSegments = processor.TotalSegments,
            CompletedSegments = processor.CompletedSegments,
            FailedSegments = processor.FailedSegments,
            ProgressPercentage = processor.ProgressPercentage,
            ElapsedTime = processor.ElapsedTime,
            EstimatedRemainingTime = processor.EstimatedRemainingTime,
            CurrentSegmentTitle = processor.CurrentSegmentTitle,
            Status = processor.Status,
            StatusMessage = GetStatusMessage(processor.Status),
            ProcessingRate = CalculateProcessingRate(processor),
            CurrentConcurrency = processor.ConcurrentLimit
        });
    }

    /// <summary>
    /// 取得批次處理結果
    /// </summary>
    public async Task<BatchSummaryProcessor?> GetBatchResultAsync(Guid batchId)
    {
        if (!_batchProcessors.TryGetValue(batchId, out var processor))
        {
            return null;
        }

        return await Task.FromResult(processor);
    }

    /// <summary>
    /// 暫停批次處理
    /// </summary>
    public async Task<bool> PauseBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (!_batchProcessors.TryGetValue(batchId, out var processor))
        {
            _logger.LogWarning("找不到批次處理器，BatchId: {BatchId}", batchId);
            return false;
        }

        if (processor.Status != BatchProcessingStatus.Processing)
        {
            _logger.LogWarning("批次處理器狀態不允許暫停，BatchId: {BatchId}，當前狀態: {Status}", 
                batchId, processor.Status);
            return false;
        }

        processor.Status = BatchProcessingStatus.Paused;
        _logger.LogInformation("批次處理已暫停，BatchId: {BatchId}", batchId);

        // 發送暫停狀態通知
        await _notificationService.NotifyStatusChangeAsync(batchId, BatchProcessingStatus.Paused, cancellationToken: cancellationToken);

        return await Task.FromResult(true);
    }

    /// <summary>
    /// 恢復批次處理
    /// </summary>
    public async Task<bool> ResumeBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (!_batchProcessors.TryGetValue(batchId, out var processor))
        {
            _logger.LogWarning("找不到批次處理器，BatchId: {BatchId}", batchId);
            return false;
        }

        if (processor.Status != BatchProcessingStatus.Paused)
        {
            _logger.LogWarning("批次處理器狀態不允許恢復，BatchId: {BatchId}，當前狀態: {Status}", 
                batchId, processor.Status);
            return false;
        }

        processor.Status = BatchProcessingStatus.Processing;
        _logger.LogInformation("批次處理已恢復，BatchId: {BatchId}", batchId);

        // 發送恢復狀態通知
        await _notificationService.NotifyStatusChangeAsync(batchId, BatchProcessingStatus.Processing, cancellationToken: cancellationToken);

        return await Task.FromResult(true);
    }

    /// <summary>
    /// 取消批次處理
    /// </summary>
    public async Task<bool> CancelBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (!_batchProcessors.TryGetValue(batchId, out var processor))
        {
            _logger.LogWarning("找不到批次處理器，BatchId: {BatchId}", batchId);
            return false;
        }

        // 設定狀態為取消
        processor.Status = BatchProcessingStatus.Cancelled;
        processor.CompletedTime = DateTime.UtcNow;

        // 取消處理任務
        if (_batchCancellationTokens.TryGetValue(batchId, out var cts))
        {
            cts.Cancel();
        }

        _logger.LogInformation("批次處理已取消，BatchId: {BatchId}", batchId);
        return await Task.FromResult(true);
    }

    /// <summary>
    /// 清理已完成的批次處理記錄
    /// </summary>
    public async Task<int> CleanupCompletedBatchesAsync(int olderThanHours = 24, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-olderThanHours);
        var cleanedCount = 0;

        var batchesToRemove = _batchProcessors
            .Where(kvp => 
                kvp.Value.CompletedTime.HasValue && 
                kvp.Value.CompletedTime < cutoffTime &&
                (kvp.Value.Status == BatchProcessingStatus.Completed || 
                 kvp.Value.Status == BatchProcessingStatus.Failed ||
                 kvp.Value.Status == BatchProcessingStatus.Cancelled))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var batchId in batchesToRemove)
        {
            if (_batchProcessors.TryRemove(batchId, out _))
            {
                _batchCancellationTokens.TryRemove(batchId, out _);
                _batchProcessingTasks.TryRemove(batchId, out _);
                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
        {
            _logger.LogInformation("清理了 {Count} 個已完成的批次處理記錄", cleanedCount);
        }

        return await Task.FromResult(cleanedCount);
    }

    /// <summary>
    /// 取得使用者的所有批次處理
    /// </summary>
    public async Task<List<BatchProcessingProgress>> GetUserBatchesAsync(string userId, int pageIndex = 0, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<BatchProcessingProgress>();
        }

        var userBatches = _batchProcessors.Values
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartTime)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(processor => new BatchProcessingProgress
            {
                BatchId = processor.BatchId,
                TotalSegments = processor.TotalSegments,
                CompletedSegments = processor.CompletedSegments,
                FailedSegments = processor.FailedSegments,
                ProgressPercentage = processor.ProgressPercentage,
                ElapsedTime = processor.ElapsedTime,
                EstimatedRemainingTime = processor.EstimatedRemainingTime,
                CurrentSegmentTitle = processor.CurrentSegmentTitle,
                Status = processor.Status,
                StatusMessage = GetStatusMessage(processor.Status),
                ProcessingRate = CalculateProcessingRate(processor),
                CurrentConcurrency = processor.ConcurrentLimit
            })
            .ToList();

        return await Task.FromResult(userBatches);
    }

    /// <summary>
    /// 批次處理核心邏輯
    /// </summary>
    private async Task ProcessBatchAsync(BatchSummaryProcessor processor, CancellationToken cancellationToken)
    {
        processor.Status = BatchProcessingStatus.Processing;
        _logger.LogInformation("開始執行批次處理，BatchId: {BatchId}，併發限制: {ConcurrentLimit}", 
            processor.BatchId, processor.ConcurrentLimit);

        // 發送狀態變更通知
        await _notificationService.NotifyStatusChangeAsync(processor.BatchId, processor.Status, cancellationToken: cancellationToken);

        try
        {
            // 使用信號量控制併發數量
            var concurrencyLimiter = new SemaphoreSlim(processor.ConcurrentLimit, processor.ConcurrentLimit);
            var processingTasks = new List<Task>();

            // 為每個分段建立處理任務
            foreach (var task in processor.Tasks)
            {
                var processingTask = ProcessSegmentTaskAsync(processor, task, concurrencyLimiter, cancellationToken);
                processingTasks.Add(processingTask);
            }

            // 等待所有任務完成
            await Task.WhenAll(processingTasks);

            // 計算統計資訊
            CalculateStatistics(processor);

            // 設定完成狀態
            processor.Status = processor.FailedSegments > 0 ? BatchProcessingStatus.Failed : BatchProcessingStatus.Completed;
            processor.CompletedTime = DateTime.UtcNow;

            _logger.LogInformation("批次處理完成，BatchId: {BatchId}，狀態: {Status}，完成: {Completed}/{Total}", 
                processor.BatchId, processor.Status, processor.CompletedSegments, processor.TotalSegments);

            // 發送批次完成通知
            await _notificationService.NotifyBatchCompletedAsync(processor.BatchId, processor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            processor.Status = BatchProcessingStatus.Cancelled;
            processor.CompletedTime = DateTime.UtcNow;
            _logger.LogInformation("批次處理被取消，BatchId: {BatchId}", processor.BatchId);

            // 發送取消狀態通知
            await _notificationService.NotifyStatusChangeAsync(processor.BatchId, processor.Status, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            processor.Status = BatchProcessingStatus.Failed;
            processor.CompletedTime = DateTime.UtcNow;
            _logger.LogError(ex, "批次處理發生錯誤，BatchId: {BatchId}", processor.BatchId);

            // 發送錯誤通知
            await _notificationService.NotifyErrorAsync(processor.BatchId, ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// 處理單個分段任務
    /// </summary>
    private async Task ProcessSegmentTaskAsync(
        BatchSummaryProcessor processor,
        SegmentSummaryTask task,
        SemaphoreSlim concurrencyLimiter,
        CancellationToken cancellationToken)
    {
        // 等待併發許可
        await concurrencyLimiter.WaitAsync(cancellationToken);
        
        try
        {
            await ProcessSingleSegmentWithRetryAsync(processor, task, cancellationToken);
        }
        finally
        {
            concurrencyLimiter.Release();
        }
    }

    /// <summary>
    /// 處理單個分段（包含重試機制）
    /// </summary>
    private async Task ProcessSingleSegmentWithRetryAsync(
        BatchSummaryProcessor processor,
        SegmentSummaryTask task,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        task.StartTime = DateTime.UtcNow;
        task.Status = SegmentTaskStatus.Processing;

        for (int attempt = 0; attempt <= _config.RetryPolicy.MaxRetries; attempt++)
        {
            try
            {
                // 檢查批次處理是否被暫停或取消
                if (processor.Status == BatchProcessingStatus.Paused)
                {
                    // 等待恢復或取消
                    while (processor.Status == BatchProcessingStatus.Paused && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 設定安全檢查點（準備開始處理）
                _cancellationService.SetSafeCheckpoint(processor.BatchId, true);

                // 呼叫摘要服務
                var summary = await _summaryService.SummarizeAsync(task.SourceSegment.Content, cancellationToken);

                // 設定安全檢查點（處理完成）
                _cancellationService.SetSafeCheckpoint(processor.BatchId, true);
                
                // 任務成功完成
                task.SummaryResult = summary;
                task.Status = SegmentTaskStatus.Completed;
                task.CompletedTime = DateTime.UtcNow;
                task.ProcessingTime = stopwatch.Elapsed;

                _logger.LogDebug("分段摘要完成，BatchId: {BatchId}，SegmentIndex: {Index}，嘗試次數: {Attempt}", 
                    processor.BatchId, task.SegmentIndex, attempt + 1);

                // 發送分段完成通知
                var segmentResult = new SegmentSummaryResult
                {
                    SegmentIndex = task.SegmentIndex,
                    Title = task.SourceSegment.Title,
                    OriginalContent = task.SourceSegment.Content,
                    Summary = summary,
                    Status = task.Status,
                    ProcessingTime = task.ProcessingTime,
                    RetryCount = attempt,
                    ErrorMessage = null
                };
                await _notificationService.NotifySegmentCompletedAsync(processor.BatchId, task.SegmentIndex, segmentResult, cancellationToken);

                // 發送整體進度更新
                var progress = new BatchProcessingProgress
                {
                    BatchId = processor.BatchId,
                    TotalSegments = processor.TotalSegments,
                    CompletedSegments = processor.CompletedSegments,
                    FailedSegments = processor.FailedSegments,
                    ProgressPercentage = processor.ProgressPercentage,
                    ElapsedTime = processor.ElapsedTime,
                    EstimatedRemainingTime = processor.EstimatedRemainingTime,
                    CurrentSegmentTitle = processor.CurrentSegmentTitle,
                    Status = processor.Status,
                    StatusMessage = GetStatusMessage(processor.Status),
                    ProcessingRate = CalculateProcessingRate(processor),
                    CurrentConcurrency = processor.ConcurrentLimit
                };
                await _notificationService.NotifyProgressUpdateAsync(processor.BatchId, progress, cancellationToken);
                
                return;
            }
            catch (OperationCanceledException)
            {
                task.Status = SegmentTaskStatus.Failed;
                task.ErrorMessage = "處理被取消";
                throw;
            }
            catch (Exception ex)
            {
                task.RetryCount = attempt;
                task.LastRetryTime = DateTime.UtcNow;
                task.ErrorMessage = ex.Message;

                _logger.LogWarning(ex, "分段摘要失敗，BatchId: {BatchId}，SegmentIndex: {Index}，嘗試: {Attempt}/{MaxAttempts}", 
                    processor.BatchId, task.SegmentIndex, attempt + 1, _config.RetryPolicy.MaxRetries + 1);

                if (attempt < _config.RetryPolicy.MaxRetries)
                {
                    // 計算重試延遲（指數退避）
                    var delay = TimeSpan.FromSeconds(_config.RetryPolicy.BaseDelaySeconds * Math.Pow(_config.RetryPolicy.BackoffMultiplier, attempt));
                    task.Status = SegmentTaskStatus.Retrying;
                    
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    // 達到最大重試次數，標記為失敗
                    task.Status = SegmentTaskStatus.Failed;
                    task.CompletedTime = DateTime.UtcNow;
                    task.ProcessingTime = stopwatch.Elapsed;
                    
                    _logger.LogError(ex, "分段摘要最終失敗，BatchId: {BatchId}，SegmentIndex: {Index}", 
                        processor.BatchId, task.SegmentIndex);
                }
            }
        }
    }

    /// <summary>
    /// 計算處理統計資訊
    /// </summary>
    private void CalculateStatistics(BatchSummaryProcessor processor)
    {
        var completedTasks = processor.Tasks.Where(t => t.Status == SegmentTaskStatus.Completed).ToList();
        
        processor.Statistics.TotalProcessingTime = processor.ElapsedTime;
        processor.Statistics.AverageSegmentProcessingTime = completedTasks.Count > 0 
            ? TimeSpan.FromTicks((long)completedTasks.Average(t => t.ProcessingTime?.Ticks ?? 0))
            : TimeSpan.Zero;
        processor.Statistics.TotalRetries = processor.Tasks.Sum(t => t.RetryCount);
        processor.Statistics.TotalApiCalls = processor.Tasks.Sum(t => t.RetryCount + 1);
        processor.Statistics.SuccessfulApiCalls = completedTasks.Count;
        processor.Statistics.ApiSuccessRate = processor.Statistics.TotalApiCalls > 0 
            ? (double)processor.Statistics.SuccessfulApiCalls / processor.Statistics.TotalApiCalls * 100 
            : 0;
        processor.Statistics.PeakConcurrency = processor.ConcurrentLimit;
    }

    /// <summary>
    /// 取得狀態訊息
    /// </summary>
    private string GetStatusMessage(BatchProcessingStatus status)
    {
        return status switch
        {
            BatchProcessingStatus.Queued => "等待處理中",
            BatchProcessingStatus.Processing => "正在處理",
            BatchProcessingStatus.Paused => "已暫停",
            BatchProcessingStatus.Completed => "處理完成",
            BatchProcessingStatus.Failed => "處理失敗",
            BatchProcessingStatus.Cancelled => "已取消",
            _ => "未知狀態"
        };
    }

    /// <summary>
    /// 計算處理速度
    /// </summary>
    private double CalculateProcessingRate(BatchSummaryProcessor processor)
    {
        if (processor.CompletedSegments == 0) return 0;
        
        var elapsedMinutes = processor.ElapsedTime.TotalMinutes;
        return elapsedMinutes > 0 ? processor.CompletedSegments / elapsedMinutes : 0;
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // 取消所有處理中的任務
            foreach (var cts in _batchCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _globalConcurrencyLimit.Dispose();
            _disposed = true;
        }
    }
}