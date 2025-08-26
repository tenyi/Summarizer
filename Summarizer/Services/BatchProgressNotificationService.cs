using Microsoft.AspNetCore.SignalR;
using Summarizer.Hubs;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Text.Json;

namespace Summarizer.Services;

/// <summary>
/// 批次處理進度通知服務實作
/// </summary>
public class BatchProgressNotificationService : IBatchProgressNotificationService
{
    private readonly IHubContext<BatchProcessingHub> _hubContext;
    private readonly ILogger<BatchProgressNotificationService> _logger;

    /// <summary>
    /// 建構函式
    /// </summary>
    public BatchProgressNotificationService(
        IHubContext<BatchProcessingHub> hubContext,
        ILogger<BatchProgressNotificationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 發送批次處理進度更新
    /// </summary>
    public async Task NotifyProgressUpdateAsync(Guid batchId, BatchProcessingProgress progress, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "ProgressUpdate",
                new
                {
                    BatchId = batchId,
                    Progress = progress,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送進度更新通知，BatchId: {BatchId}，進度: {ProgressPercentage}%",
                batchId, progress.ProgressPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送進度更新通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送批次處理狀態變更
    /// </summary>
    public async Task NotifyStatusChangeAsync(Guid batchId, BatchProcessingStatus status, string? message = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "StatusChange",
                new
                {
                    BatchId = batchId,
                    Status = status,
                    StatusMessage = message ?? GetStatusMessage(status),
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送狀態變更通知，BatchId: {BatchId}，狀態: {Status}", batchId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送狀態變更通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送分段處理完成通知
    /// </summary>
    public async Task NotifySegmentCompletedAsync(Guid batchId, int segmentIndex, SegmentSummaryResult segmentResult, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "SegmentCompleted",
                new
                {
                    BatchId = batchId,
                    SegmentIndex = segmentIndex,
                    SegmentResult = segmentResult,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送分段完成通知，BatchId: {BatchId}，分段: {SegmentIndex}", batchId, segmentIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送分段完成通知失敗，BatchId: {BatchId}，分段: {SegmentIndex}", batchId, segmentIndex);
        }
    }

    /// <summary>
    /// 發送批次處理完成通知
    /// </summary>
    public async Task NotifyBatchCompletedAsync(Guid batchId, BatchSummaryProcessor processor, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            // 整理完成結果
            var completedSegments = processor.Tasks
                .Where(t => t.Status == SegmentTaskStatus.Completed)
                .Select(t => new SegmentSummaryResult
                {
                    SegmentIndex = t.SegmentIndex,
                    Title = t.SourceSegment.Title,
                    OriginalContent = t.SourceSegment.Content,
                    Summary = t.SummaryResult ?? string.Empty,
                    Status = t.Status,
                    ProcessingTime = t.ProcessingTime,
                    RetryCount = t.RetryCount,
                    ErrorMessage = t.ErrorMessage
                })
                .ToList();

            await _hubContext.Clients.Group(groupName).SendAsync(
                "BatchCompleted",
                new
                {
                    BatchId = batchId,
                    Status = processor.Status,
                    CompletedSegments = completedSegments,
                    Statistics = processor.Statistics,
                    FinalSummary = processor.FinalSummary,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogInformation("已發送批次完成通知，BatchId: {BatchId}，狀態: {Status}，完成數量: {CompletedCount}",
                batchId, processor.Status, completedSegments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送批次完成通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送錯誤通知
    /// </summary>
    public async Task NotifyErrorAsync(Guid batchId, string error, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "Error",
                new
                {
                    BatchId = batchId,
                    Error = error,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送錯誤通知，BatchId: {BatchId}，錯誤: {Error}", batchId, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送錯誤通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送取消請求通知
    /// </summary>
    public async Task NotifyCancellationRequestedAsync(Guid batchId, CancellationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "CancellationRequested",
                new
                {
                    BatchId = batchId,
                    UserId = request.UserId,
                    Reason = request.Reason,
                    SavePartialResults = request.SavePartialResults,
                    ForceCancel = request.ForceCancel,
                    UserComment = request.UserComment,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送取消請求通知，BatchId: {BatchId}，原因: {Reason}", batchId, request.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送取消請求通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 取得狀態訊息
    /// </summary>
    private static string GetStatusMessage(BatchProcessingStatus status)
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
    /// 發送部分結果已保存通知
    /// </summary>
    public async Task NotifyPartialResultSavedAsync(Guid batchId, Guid partialResultId, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "PartialResultSaved",
                new
                {
                    BatchId = batchId,
                    PartialResultId = partialResultId,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogInformation("已發送部分結果保存通知，BatchId: {BatchId}，PartialResultId: {PartialResultId}",
                batchId, partialResultId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送部分結果保存通知失敗，BatchId: {BatchId}，PartialResultId: {PartialResultId}",
                batchId, partialResultId);
        }
    }

    /// <summary>
    /// 發送恢復完成通知
    /// </summary>
    public async Task NotifyRecoveryCompleted(Guid batchId, bool success, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "RecoveryCompleted",
                new
                {
                    BatchId = batchId,
                    Success = success,
                    Duration = duration.TotalSeconds,
                    Message = success ? "系統恢復成功" : "系統恢復部分失敗",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogInformation("已發送恢復完成通知，BatchId: {BatchId}，成功: {Success}，耗時: {Duration}",
                batchId, success, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送恢復完成通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送UI重置通知
    /// </summary>
    public async Task NotifyUIReset(Guid batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "UIReset",
                new
                {
                    BatchId = batchId,
                    Message = "前端介面已重置",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送UI重置通知，BatchId: {BatchId}", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送UI重置通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送進度重置通知
    /// </summary>
    public async Task NotifyProgressReset(Guid batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "ProgressReset",
                new
                {
                    BatchId = batchId,
                    Message = "處理進度已重置",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送進度重置通知，BatchId: {BatchId}", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送進度重置通知失敗，BatchId: {BatchId}", batchId);
        }
    }

    /// <summary>
    /// 發送UI恢復完成通知
    /// </summary>
    public async Task NotifyUIRecoveryCompleted(Guid batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"batch_{batchId}";
            
            await _hubContext.Clients.Group(groupName).SendAsync(
                "UIRecoveryCompleted",
                new
                {
                    BatchId = batchId,
                    Message = "前端介面恢復完成",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogDebug("已發送UI恢復完成通知，BatchId: {BatchId}", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送UI恢復完成通知失敗，BatchId: {BatchId}", batchId);
        }
    }
}