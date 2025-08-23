using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Summarizer.Exceptions;
using Summarizer.Models.Requests;
using Summarizer.Models.Responses;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<SummarizeController> _logger;
    private readonly ISummaryRepository _summaryRepository;

    public SummarizeController(
        IOllamaSummaryService ollamaService,
        IOpenAiSummaryService openAiService,
        IConfiguration configuration,
        ILogger<SummarizeController> logger,
        ISummaryRepository summaryRepository)
    {
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
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
}