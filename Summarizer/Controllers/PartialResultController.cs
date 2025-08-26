using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Security.Claims;

namespace Summarizer.Controllers;

/// <summary>
/// 部分結果處理控制器
/// 提供部分結果的處理、保存和管理功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PartialResultController : ControllerBase
{
    private readonly IPartialResultHandler _partialResultHandler;
    private readonly ICancellationService _cancellationService;
    private readonly IBatchSummaryProcessingService _batchProcessingService;
    private readonly ILogger<PartialResultController> _logger;

    public PartialResultController(
        IPartialResultHandler partialResultHandler,
        ICancellationService cancellationService,
        IBatchSummaryProcessingService batchProcessingService,
        ILogger<PartialResultController> logger)
    {
        _partialResultHandler = partialResultHandler ?? throw new ArgumentNullException(nameof(partialResultHandler));
        _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        _batchProcessingService = batchProcessingService ?? throw new ArgumentNullException(nameof(batchProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 處理部分結果
    /// 當批次處理被取消且用戶選擇保存部分結果時調用
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>處理後的部分結果</returns>
    [HttpPost("process/{batchId:guid}")]
    public async Task<IActionResult> ProcessPartialResultAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法識別當前使用者");
            }

            _logger.LogInformation("開始處理部分結果，BatchId: {BatchId}, UserId: {UserId}", batchId, userId);

            // 取得批次處理結果
            var batchResult = await _batchProcessingService.GetBatchResultAsync(batchId);
            if (batchResult?.Tasks == null || !batchResult.Tasks.Any())
            {
                _logger.LogWarning("找不到批次處理任務，BatchId: {BatchId}", batchId);
                return NotFound("找不到對應的批次處理");
            }

            // 收集已完成的分段
            var completedSegments = await _partialResultHandler.CollectCompletedSegmentsAsync(batchResult.Tasks, cancellationToken);
            
            if (!completedSegments.Any())
            {
                _logger.LogWarning("沒有找到已完成的分段，BatchId: {BatchId}", batchId);
                return BadRequest("沒有已完成的分段可以處理");
            }

            // 處理部分結果
            var partialResult = await _partialResultHandler.ProcessPartialResultAsync(
                batchId,
                userId,
                completedSegments,
                batchResult.TotalSegments,
                cancellationToken);

            // 保存部分結果
            var saved = await _partialResultHandler.SavePartialResultAsync(partialResult, cancellationToken);
            if (!saved)
            {
                _logger.LogError("保存部分結果失敗，BatchId: {BatchId}", batchId);
                return StatusCode(500, "保存部分結果失敗");
            }

            _logger.LogInformation("部分結果處理完成，PartialResultId: {PartialResultId}", partialResult.PartialResultId);

            return Ok(partialResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理部分結果時發生錯誤，BatchId: {BatchId}", batchId);
            return StatusCode(500, "處理部分結果時發生內部錯誤");
        }
    }

    /// <summary>
    /// 保存或更新部分結果狀態
    /// 當用戶對部分結果做出決定時調用
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="request">更新請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作結果</returns>
    [HttpPost("save/{partialResultId:guid}")]
    public async Task<IActionResult> SavePartialResultAsync(
        Guid partialResultId,
        [FromBody] UpdatePartialResultRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法識別當前使用者");
            }

            _logger.LogInformation("更新部分結果狀態，PartialResultId: {PartialResultId}, Status: {Status}, UserId: {UserId}",
                partialResultId, request.Status, userId);

            var success = await _partialResultHandler.UpdatePartialResultStatusAsync(
                partialResultId,
                request.Status,
                request.UserComment,
                userId,
                cancellationToken);

            if (!success)
            {
                _logger.LogWarning("更新部分結果狀態失敗，可能是權限不足或結果不存在，PartialResultId: {PartialResultId}",
                    partialResultId);
                return NotFound("找不到對應的部分結果或沒有權限");
            }

            return Ok(new { success = true, message = "部分結果狀態已更新" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新部分結果狀態時發生錯誤，PartialResultId: {PartialResultId}", partialResultId);
            return StatusCode(500, "更新部分結果狀態時發生內部錯誤");
        }
    }

    /// <summary>
    /// 獲取部分結果詳細資訊
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部分結果詳細資訊</returns>
    [HttpGet("{partialResultId:guid}")]
    public async Task<IActionResult> GetPartialResultAsync(
        Guid partialResultId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法識別當前使用者");
            }

            var partialResult = await _partialResultHandler.GetPartialResultAsync(partialResultId, userId, cancellationToken);
            
            if (partialResult == null)
            {
                return NotFound("找不到對應的部分結果或沒有權限");
            }

            return Ok(partialResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取部分結果時發生錯誤，PartialResultId: {PartialResultId}", partialResultId);
            return StatusCode(500, "獲取部分結果時發生內部錯誤");
        }
    }

    /// <summary>
    /// 獲取當前使用者的部分結果列表
    /// </summary>
    /// <param name="status">狀態篩選</param>
    /// <param name="pageIndex">頁面索引</param>
    /// <param name="pageSize">頁面大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>部分結果列表</returns>
    [HttpGet("list")]
    public async Task<IActionResult> GetUserPartialResultsAsync(
        [FromQuery] PartialResultStatus? status = null,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法識別當前使用者");
            }

            // 限制頁面大小
            pageSize = Math.Min(Math.Max(pageSize, 1), 50);

            var results = await _partialResultHandler.GetUserPartialResultsAsync(
                userId, 
                status, 
                pageIndex, 
                pageSize, 
                cancellationToken);

            return Ok(new
            {
                results,
                pageIndex,
                pageSize,
                hasMore = results.Count == pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取部分結果列表時發生錯誤");
            return StatusCode(500, "獲取部分結果列表時發生內部錯誤");
        }
    }

    /// <summary>
    /// 檢查是否可以從部分結果繼續處理
    /// </summary>
    /// <param name="partialResultId">部分結果 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否可以繼續處理</returns>
    [HttpGet("{partialResultId:guid}/can-continue")]
    public async Task<IActionResult> CanContinueFromPartialResultAsync(
        Guid partialResultId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("無法識別當前使用者");
            }

            var canContinue = await _partialResultHandler.CanContinueFromPartialResultAsync(
                partialResultId, 
                userId, 
                cancellationToken);

            return Ok(new { canContinue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查是否可以繼續處理時發生錯誤，PartialResultId: {PartialResultId}", partialResultId);
            return StatusCode(500, "檢查是否可以繼續處理時發生內部錯誤");
        }
    }

    /// <summary>
    /// 清理過期的部分結果
    /// 這個端點通常由系統管理員或定時任務調用
    /// </summary>
    /// <param name="expireAfterHours">多少小時後過期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理的記錄數量</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupExpiredPartialResultsAsync(
        [FromQuery] int expireAfterHours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 限制過期時間範圍
            expireAfterHours = Math.Min(Math.Max(expireAfterHours, 1), 168); // 最少1小時，最多7天

            var cleanedCount = await _partialResultHandler.CleanupExpiredPartialResultsAsync(
                expireAfterHours, 
                cancellationToken);

            _logger.LogInformation("清理過期部分結果完成，清理數量: {Count}", cleanedCount);

            return Ok(new { cleanedCount, expireAfterHours });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理過期部分結果時發生錯誤");
            return StatusCode(500, "清理過期部分結果時發生內部錯誤");
        }
    }

    /// <summary>
    /// 獲取當前使用者 ID
    /// </summary>
    /// <returns>使用者 ID</returns>
    private string GetCurrentUserId()
    {
        return User?.Identity?.Name ?? User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
    }
}

/// <summary>
/// 更新部分結果請求模型
/// </summary>
public class UpdatePartialResultRequest
{
    /// <summary>
    /// 新的狀態
    /// </summary>
    public PartialResultStatus Status { get; set; }

    /// <summary>
    /// 使用者評論
    /// </summary>
    public string? UserComment { get; set; }
}