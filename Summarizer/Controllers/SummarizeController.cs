using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Summarizer.Exceptions;
using Summarizer.Models.Requests;
using Summarizer.Models.Responses;
using Summarizer.Models.TextSegmentation;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using Summarizer.Repositories.Interfaces;
using System.Diagnostics;

namespace Summarizer.Controllers;

/// <summary>
/// 摘要功能控制器，處理文件摘要請求
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SummarizeController : ControllerBase
{
    private readonly IOllamaSummaryService _ollamaService;
    private readonly IOpenAiSummaryService _openAiService;
    private readonly ITextSegmentationService _segmentationService;
    private readonly IBatchSummaryProcessingService _batchProcessingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SummarizeController> _logger;
    private readonly ISummaryRepository _summaryRepository;

    public SummarizeController(
        IOllamaSummaryService ollamaService,
        IOpenAiSummaryService openAiService,
        ITextSegmentationService segmentationService,
        IBatchSummaryProcessingService batchProcessingService,
        IConfiguration configuration,
        ILogger<SummarizeController> logger,
        ISummaryRepository summaryRepository)
    {
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
        _batchProcessingService = batchProcessingService ?? throw new ArgumentNullException(nameof(batchProcessingService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
    }

    /// <summary>
    /// 執行文件摘要
    /// </summary>
    /// <param name="request">摘要請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>摘要結果</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status408RequestTimeout)]
        [ProducesResponseType<SummarizeResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SummarizeAsync(
        [FromBody] SummarizeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            _logger.LogWarning("摘要請求驗證失敗: {Errors}", errors);

            return BadRequest(new SummarizeResponse
            {
                Success = false,
                Error = errors,
                ErrorCode = "VALIDATION_ERROR"
            });
        }

        var correlationId = HttpContext.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("收到摘要請求，相關 ID: {CorrelationId}，文本長度: {TextLength}",
            correlationId, request.Text.Length);

        try
        {
            var summaryService = GetSummaryService();
            var summary = await summaryService.SummarizeAsync(request.Text, cancellationToken);

            stopwatch.Stop();
            var response = new SummarizeResponse
            {
                Success = true,
                Summary = summary,
                OriginalLength = request.Text.Length,
                SummaryLength = summary.Length,
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };

            _logger.LogInformation("摘要生成成功，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms",
                correlationId, response.ProcessingTimeMs);

            try
            {
                var summaryRecord = new Models.SummaryRecord
                {
                    OriginalText = request.Text,
                    SummaryText = summary,
                    CreatedAt = DateTime.UtcNow,
                    OriginalLength = request.Text.Length,
                    SummaryLength = summary.Length,
                    ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
                await _summaryRepository.CreateAsync(summaryRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存摘要記錄失敗，相關 ID: {CorrelationId}", correlationId);
                // Do not block the response to the user
            }

            return Ok(response);
        }
        catch (ApiTimeoutException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "摘要請求超時，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status408RequestTimeout, new SummarizeResponse
            {
                Success = false,
                Error = "處理時間過長，請稍後重試",
                ErrorCode = "TIMEOUT_ERROR",
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (ApiServiceUnavailableException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "AI 服務不可用，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SummarizeResponse
            {
                Success = false,
                Error = "服務暫時不可用，請稍後重試",
                ErrorCode = "SERVICE_UNAVAILABLE",
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (ApiConnectionException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "AI 服務連接失敗，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SummarizeResponse
            {
                Success = false,
                Error = "無法連接到 AI 服務，請稍後重試",
                ErrorCode = "CONNECTION_ERROR",
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "摘要生成時發生未預期的錯誤，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new SummarizeResponse
            {
                Success = false,
                Error = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR",
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
    }

    /// <summary>
    /// 檢查 AI 服務健康狀態
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>健康檢查結果</returns>
    [HttpGet("health")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogDebug("執行 AI 服務健康檢查，相關 ID: {CorrelationId}", correlationId);
        
        try
        {
            var summaryService = GetSummaryService();
            var isHealthy = await summaryService.IsHealthyAsync(cancellationToken);
            
            if (isHealthy)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { status = "healthy", provider = GetCurrentProvider() }
                });
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiResponse<object>
                {
                    Success = false,
                    Error = "AI 服務不可用",
                    ErrorCode = "SERVICE_UNHEALTHY"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查時發生錯誤，相關 ID: {CorrelationId}", correlationId);
            
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiResponse<object>
            {
                Success = false,
                Error = "健康檢查失敗",
                ErrorCode = "HEALTH_CHECK_ERROR"
            });
        }
    }
    
    /// <summary>
    /// 處理文字檔上傳並執行摘要
    /// </summary>
    /// <param name="file">上傳的文字檔案</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>摘要結果</returns>
    [HttpPost("upload")]
    [AllowAnonymous]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType<SummarizeResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadAndSummarizeAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("收到檔案上傳摘要請求，相關 ID: {CorrelationId}，檔案名稱: {FileName}",
            correlationId, file?.FileName);

        // 驗證檔案
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("檔案上傳摘要請求驗證失敗：檔案為空或不存在");
            return BadRequest(new SummarizeResponse
            {
                Success = false,
                Error = "請選擇一個有效的文字檔案",
                ErrorCode = "INVALID_FILE"
            });
        }

        // 檢查檔案大小（限制為 10MB）
        const long maxFileSize = 10 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            _logger.LogWarning("檔案上傳摘要請求驗證失敗：檔案大小超過限制 {FileSize}", file.Length);
            return BadRequest(new SummarizeResponse
            {
                Success = false,
                Error = "檔案大小不能超過 10MB",
                ErrorCode = "FILE_TOO_LARGE"
            });
        }

        // 檢查檔案類型
        var allowedContentTypes = new[] { "text/plain", "application/octet-stream" };
        var allowedExtensions = new[] { ".txt", ".md", ".rtf" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedContentTypes.Contains(file.ContentType) && !allowedExtensions.Contains(fileExtension))
        {
            _logger.LogWarning("檔案上傳摘要請求驗證失敗：不支援的檔案類型 {ContentType}, {Extension}", 
                file.ContentType, fileExtension);
            return BadRequest(new SummarizeResponse
            {
                Success = false,
                Error = "僅支援文字檔案格式（.txt, .md, .rtf）",
                ErrorCode = "INVALID_FILE_TYPE"
            });
        }

        try
        {
            // 讀取檔案內容
            string text;
            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                text = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("檔案上傳摘要請求驗證失敗：檔案內容為空");
                return BadRequest(new SummarizeResponse
                {
                    Success = false,
                    Error = "檔案內容為空",
                    ErrorCode = "EMPTY_FILE"
                });
            }

            _logger.LogInformation("成功讀取檔案內容，相關 ID: {CorrelationId}，內容長度: {TextLength}",
                correlationId, text.Length);

            // 建立摘要請求並處理
            var request = new SummarizeRequest { Text = text };
            var stopwatch = Stopwatch.StartNew();

            var summaryService = GetSummaryService();
            var summary = await summaryService.SummarizeAsync(request.Text, cancellationToken);

            stopwatch.Stop();
            var response = new SummarizeResponse
            {
                Success = true,
                Summary = summary,
                OriginalLength = request.Text.Length,
                SummaryLength = summary.Length,
                ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };

            _logger.LogInformation("檔案摘要生成成功，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms",
                correlationId, response.ProcessingTimeMs);

            // 儲存摘要記錄
            try
            {
                var summaryRecord = new Models.SummaryRecord
                {
                    OriginalText = request.Text,
                    SummaryText = summary,
                    CreatedAt = DateTime.UtcNow,
                    OriginalLength = request.Text.Length,
                    SummaryLength = summary.Length,
                    ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
                await _summaryRepository.CreateAsync(summaryRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存檔案摘要記錄失敗，相關 ID: {CorrelationId}", correlationId);
            }

            return Ok(response);
        }
        catch (ApiTimeoutException ex)
        {
            _logger.LogWarning(ex, "檔案摘要請求超時，相關 ID: {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status408RequestTimeout, new SummarizeResponse
            {
                Success = false,
                Error = "處理時間過長，請稍後重試",
                ErrorCode = "TIMEOUT_ERROR"
            });
        }
        catch (ApiServiceUnavailableException ex)
        {
            _logger.LogError(ex, "檔案摘要時 AI 服務不可用，相關 ID: {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SummarizeResponse
            {
                Success = false,
                Error = "服務暫時不可用，請稍後重試",
                ErrorCode = "SERVICE_UNAVAILABLE"
            });
        }
        catch (ApiConnectionException ex)
        {
            _logger.LogError(ex, "檔案摘要時 AI 服務連接失敗，相關 ID: {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new SummarizeResponse
            {
                Success = false,
                Error = "無法連接到 AI 服務，請稍後重試",
                ErrorCode = "CONNECTION_ERROR"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檔案摘要生成時發生未預期的錯誤，相關 ID: {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new SummarizeResponse
            {
                Success = false,
                Error = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// 根據配置選擇摘要服務
    /// </summary>
    /// <returns>選定的摘要服務</returns>
    private ISummaryService GetSummaryService()
    {
        var provider = _configuration.GetValue<string>("AiProvider")?.ToLowerInvariant();
        
        return provider switch
        {
            "openai" => _openAiService,
            "ollama" => _ollamaService,
            _ => _ollamaService // 預設使用 Ollama
        };
    }
    
    /// <summary>
    /// 取得當前使用的 AI 提供者
    /// </summary>
    /// <returns>提供者名稱</returns>
    private string GetCurrentProvider()
    {
        var provider = _configuration.GetValue<string>("AiProvider")?.ToLowerInvariant();
        return provider ?? "ollama";
    }

    /// <summary>
    /// 檢查文本是否需要分段處理
    /// </summary>
    /// <param name="request">分段檢查請求</param>
    /// <returns>分段檢查結果</returns>
    [HttpPost("check-segmentation")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<bool>>(StatusCodes.Status400BadRequest)]
    public IActionResult CheckSegmentationNeeded([FromBody] SegmentationCheckRequest request)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogDebug("檢查文本分段需求，相關 ID: {CorrelationId}", correlationId);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Error = "文本內容不能為空",
                    ErrorCode = "EMPTY_TEXT"
                });
            }

            var needsSegmentation = _segmentationService.ShouldSegmentText(request.Text);

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = needsSegmentation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查文本分段需求時發生錯誤，相關 ID: {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<bool>
            {
                Success = false,
                Error = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// 執行文本分段
    /// </summary>
    /// <param name="request">分段請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段結果</returns>
    [HttpPost("segment")]
    [AllowAnonymous]
    [ProducesResponseType<SegmentationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SegmentationResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SegmentationResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SegmentTextAsync([FromBody] SegmentationRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            _logger.LogWarning("文本分段請求驗證失敗: {Errors}", errors);

            return BadRequest(new SegmentationResponse
            {
                Success = false,
                ErrorMessage = errors,
                ErrorCode = "VALIDATION_ERROR"
            });
        }

        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("收到文本分段請求，相關 ID: {CorrelationId}，文本長度: {TextLength}",
            correlationId, request.Text.Length);

        try
        {
            var response = await _segmentationService.SegmentTextAsync(request, cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation("文本分段成功，相關 ID: {CorrelationId}，分段數量: {SegmentCount}，處理時間: {ProcessingTime}ms",
                    correlationId, response.TotalSegments, response.ProcessingTimeMs);
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("文本分段失敗，相關 ID: {CorrelationId}，錯誤: {Error}",
                    correlationId, response.ErrorMessage);
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本分段時發生未預期的錯誤，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new SegmentationResponse
            {
                Success = false,
                ErrorMessage = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// 預覽文本分段結果（僅返回分段標題和長度統計）
    /// </summary>
    /// <param name="request">分段請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段預覽結果</returns>
    [HttpPost("segment/preview")]
    [AllowAnonymous]
    [ProducesResponseType<SegmentationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SegmentationPreviewResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SegmentationPreviewResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PreviewSegmentationAsync([FromBody] SegmentationRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            _logger.LogWarning("文本分段預覽請求驗證失敗: {Errors}", errors);

            return BadRequest(new SegmentationPreviewResponse
            {
                Success = false,
                ErrorMessage = errors,
                ErrorCode = "VALIDATION_ERROR"
            });
        }

        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("收到文本分段預覽請求，相關 ID: {CorrelationId}，文本長度: {TextLength}",
            correlationId, request.Text.Length);

        try
        {
            var response = await _segmentationService.SegmentTextAsync(request, cancellationToken);

            var previewResponse = new SegmentationPreviewResponse
            {
                Success = response.Success,
                TotalSegments = response.TotalSegments,
                OriginalLength = response.OriginalLength,
                ProcessingTimeMs = response.ProcessingTimeMs,
                SegmentationMethod = response.SegmentationMethod,
                AverageSegmentLength = response.AverageSegmentLength,
                ErrorMessage = response.ErrorMessage,
                ErrorCode = response.ErrorCode
            };

            if (response.Success && response.Segments != null)
            {
                previewResponse.SegmentPreviews = response.Segments.Select(s => new SegmentPreview
                {
                    SegmentIndex = s.SegmentIndex,
                    Title = s.Title,
                    CharacterCount = s.CharacterCount,
                    Type = s.Type
                }).ToList();
            }

            if (response.Success)
            {
                _logger.LogInformation("文本分段預覽成功，相關 ID: {CorrelationId}，分段數量: {SegmentCount}",
                    correlationId, previewResponse.TotalSegments);
                return Ok(previewResponse);
            }
            else
            {
                _logger.LogWarning("文本分段預覽失敗，相關 ID: {CorrelationId}，錯誤: {Error}",
                    correlationId, previewResponse.ErrorMessage);
                return BadRequest(previewResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本分段預覽時發生未預期的錯誤，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new SegmentationPreviewResponse
            {
                Success = false,
                ErrorMessage = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// 開始批次摘要處理
    /// </summary>
    /// <param name="request">批次摘要請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>批次處理 ID 和初始狀態</returns>
    [HttpPost("batch")]
    [AllowAnonymous]
    [ProducesResponseType<BatchSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSummaryResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BatchSummaryResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartBatchSummaryAsync([FromBody] BatchSummaryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            _logger.LogWarning("批次摘要請求驗證失敗: {Errors}", errors);

            return BadRequest(new BatchSummaryResponse
            {
                Success = false,
                ErrorMessage = errors,
                ErrorCode = "VALIDATION_ERROR"
            });
        }

        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("收到批次摘要請求，相關 ID: {CorrelationId}，分段數量: {SegmentCount}",
            correlationId, request.Segments.Count);

        try
        {
            var batchId = await _batchProcessingService.StartBatchProcessingAsync(
                request.Segments, 
                request.OriginalText, 
                request.UserId, 
                request.ConcurrentLimit,
                cancellationToken);

            var response = new BatchSummaryResponse
            {
                BatchId = batchId,
                Success = true,
                Status = BatchProcessingStatus.Queued,
                TotalSegments = request.Segments.Count,
                EstimatedProcessingTimeMinutes = EstimateProcessingTime(request.Segments.Count),
                StartTime = DateTime.UtcNow
            };

            _logger.LogInformation("批次摘要處理已開始，相關 ID: {CorrelationId}，BatchId: {BatchId}",
                correlationId, batchId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開始批次摘要處理時發生錯誤，相關 ID: {CorrelationId}", correlationId);

            return StatusCode(StatusCodes.Status500InternalServerError, new BatchSummaryResponse
            {
                Success = false,
                ErrorMessage = "系統內部錯誤",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// 查詢批次處理狀態
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <returns>批次處理狀態</returns>
    [HttpGet("batch/{batchId}/status")]
    [AllowAnonymous]
    [ProducesResponseType<BatchStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchStatusResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<BatchStatusResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBatchStatusAsync(Guid batchId)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogDebug("查詢批次處理狀態，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

        try
        {
            var progress = await _batchProcessingService.GetBatchProgressAsync(batchId);
            if (progress == null)
            {
                return NotFound(new BatchStatusResponse
                {
                    BatchId = batchId,
                    Success = false,
                    ErrorMessage = "找不到指定的批次處理"
                });
            }

            var response = new BatchStatusResponse
            {
                BatchId = batchId,
                Success = true,
                Progress = progress
            };

            // 如果處理完成，提供結果
            if (progress.Status == BatchProcessingStatus.Completed || progress.Status == BatchProcessingStatus.Failed)
            {
                var batchResult = await _batchProcessingService.GetBatchResultAsync(batchId);
                if (batchResult != null)
                {
                    response.SegmentSummaries = batchResult.Tasks
                        .Select(t => new SegmentSummaryResult
                        {
                            SegmentIndex = t.SegmentIndex,
                            Title = t.SourceSegment.Title,
                            OriginalContent = t.SourceSegment.Content,
                            Summary = t.SummaryResult,
                            Status = t.Status,
                            ProcessingTime = t.ProcessingTime,
                            RetryCount = t.RetryCount,
                            ErrorMessage = t.ErrorMessage
                        })
                        .ToList();

                    response.FinalSummary = batchResult.FinalSummary;
                }
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢批次處理狀態時發生錯誤，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

            return StatusCode(StatusCodes.Status500InternalServerError, new BatchStatusResponse
            {
                BatchId = batchId,
                Success = false,
                ErrorMessage = "系統內部錯誤"
            });
        }
    }

    /// <summary>
    /// 暫停批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作結果</returns>
    [HttpPost("batch/{batchId}/pause")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PauseBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("請求暫停批次處理，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

        try
        {
            var success = await _batchProcessingService.PauseBatchProcessingAsync(batchId, cancellationToken);
            if (!success)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "無法暫停批次處理，可能處理狀態不允許暫停"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { message = "批次處理已暫停" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暫停批次處理時發生錯誤，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
            {
                Success = false,
                Error = "系統內部錯誤"
            });
        }
    }

    /// <summary>
    /// 恢復批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作結果</returns>
    [HttpPost("batch/{batchId}/resume")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResumeBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("請求恢復批次處理，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

        try
        {
            var success = await _batchProcessingService.ResumeBatchProcessingAsync(batchId, cancellationToken);
            if (!success)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "無法恢復批次處理，可能處理狀態不允許恢復"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { message = "批次處理已恢復" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢復批次處理時發生錯誤，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
            {
                Success = false,
                Error = "系統內部錯誤"
            });
        }
    }

    /// <summary>
    /// 取消批次處理
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作結果</returns>
    [HttpPost("batch/{batchId}/cancel")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBatchProcessingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogInformation("請求取消批次處理，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

        try
        {
            var success = await _batchProcessingService.CancelBatchProcessingAsync(batchId, cancellationToken);
            if (!success)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "找不到指定的批次處理"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { message = "批次處理已取消" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消批次處理時發生錯誤，相關 ID: {CorrelationId}，BatchId: {BatchId}", correlationId, batchId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
            {
                Success = false,
                Error = "系統內部錯誤"
            });
        }
    }

    /// <summary>
    /// 取得使用者的批次處理列表
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="pageIndex">頁面索引</param>
    /// <param name="pageSize">頁面大小</param>
    /// <returns>批次處理列表</returns>
    [HttpGet("batch/user/{userId}")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<List<BatchProcessingProgress>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<List<BatchProcessingProgress>>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserBatchesAsync(string userId, [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<List<BatchProcessingProgress>>
            {
                Success = false,
                Error = "使用者 ID 不能為空"
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ApiResponse<List<BatchProcessingProgress>>
            {
                Success = false,
                Error = "頁面大小必須在 1 到 100 之間"
            });
        }

        try
        {
            var batches = await _batchProcessingService.GetUserBatchesAsync(userId, pageIndex, pageSize);

            return Ok(new ApiResponse<List<BatchProcessingProgress>>
            {
                Success = true,
                Data = batches
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者批次處理列表時發生錯誤，UserId: {UserId}", userId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<List<BatchProcessingProgress>>
            {
                Success = false,
                Error = "系統內部錯誤"
            });
        }
    }

    /// <summary>
    /// 估算處理時間（分鐘）
    /// </summary>
    private double EstimateProcessingTime(int segmentCount)
    {
        // 基於經驗值：平均每段需要 30 秒處理時間
        const double averageSecondsPerSegment = 30.0;
        return (segmentCount * averageSecondsPerSegment) / 60.0; // 轉換為分鐘
    }
}