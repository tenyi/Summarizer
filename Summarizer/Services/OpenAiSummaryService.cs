using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Exceptions;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// OpenAI API 摘要服務實作
/// </summary>
public class OpenAiSummaryService : IOpenAiSummaryService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiConfig _config;
    private readonly ILogger<OpenAiSummaryService> _logger;
    
    private const string DefaultEndpoint = "https://api.openai.com";
    private const string ChatCompletionsEndpoint = "/v1/chat/completions";
    private const string ModelsEndpoint = "/v1/models";

    public OpenAiSummaryService(
        HttpClient httpClient,
        IOptions<OpenAiConfig> config,
        ILogger<OpenAiSummaryService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        ConfigureHttpClient();
    }
    
    /// <summary>
    /// 配置 HTTP 客戶端
    /// </summary>
    private void ConfigureHttpClient()
    {
        var endpoint = string.IsNullOrEmpty(_config.Endpoint) ? DefaultEndpoint : _config.Endpoint;
        _httpClient.BaseAddress = new Uri(endpoint);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.Timeout);
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
            
        if (!string.IsNullOrEmpty(_config.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _config.OrganizationId);
        }
    }

    /// <summary>
    /// 生成文件摘要
    /// </summary>
    public async Task<string> SummarizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        
        var startTime = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString()[..8];
        
        _logger.LogInformation("開始使用 OpenAI 生成摘要，相關 ID: {CorrelationId}，文本長度: {TextLength}",
            correlationId, text.Length);

        try
        {
            var summary = await ExecuteWithRetryAsync(
                () => GenerateSummaryInternalAsync(text, correlationId, cancellationToken),
                cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("OpenAI 摘要生成完成，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms，摘要長度: {SummaryLength}",
                correlationId, processingTime, summary.Length);

            return summary;
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "OpenAI 摘要生成失敗，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms",
                correlationId, processingTime);
            throw;
        }
    }
    
    /// <summary>
    /// 內部摘要生成邏輯
    /// </summary>
    private async Task<string> GenerateSummaryInternalAsync(
        string text, 
        string correlationId, 
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(text);
        
        var requestPayload = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug("發送 OpenAI API 請求，相關 ID: {CorrelationId}，模型: {Model}",
            correlationId, _config.Model);

        using var response = await _httpClient.PostAsync(ChatCompletionsEndpoint, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = $"OpenAI API 請求失敗，狀態碼: {response.StatusCode}，錯誤內容: {errorContent}";
            
            throw response.StatusCode switch
            {
                System.Net.HttpStatusCode.RequestTimeout => new ApiTimeoutException(errorMessage),
                System.Net.HttpStatusCode.ServiceUnavailable => new ApiServiceUnavailableException(errorMessage),
                System.Net.HttpStatusCode.TooManyRequests => new ApiServiceUnavailableException($"API 請求頻率超限: {errorContent}"),
                System.Net.HttpStatusCode.Unauthorized => new ApiConnectionException($"API 金鑰無效: {errorContent}"),
                _ => new ApiConnectionException(errorMessage)
            };
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractSummaryFromResponse(responseContent, correlationId);
    }
    
    /// <summary>
    /// 建立系統提示詞
    /// </summary>
    private static string BuildSystemPrompt()
    {
        return @"你是一個專業的文件摘要助手。你的任務是為用戶提供的文本生成簡潔、準確、有用的摘要。

摘要要求：
1. 使用繁體中文
2. 摘要長度約為原文的 20-30%
3. 保留文本中的重要資訊和關鍵點
4. 語言簡潔流暢，邏輯清晰
5. 只回傳摘要內容，不要包含額外的說明或評論";
    }
    
    /// <summary>
    /// 建立用戶提示詞
    /// </summary>
    private static string BuildUserPrompt(string text)
    {
        return $"請為以下文本生成摘要：\n\n{text}";
    }
    
    /// <summary>
    /// 從回應中提取摘要內容
    /// </summary>
    private string ExtractSummaryFromResponse(string responseContent, string correlationId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    var summary = content.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(summary))
                    {
                        _logger.LogDebug("成功提取 OpenAI 摘要，相關 ID: {CorrelationId}，長度: {Length}",
                            correlationId, summary.Length);
                        return summary;
                    }
                }
            }
            
            if (root.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = errorElement.TryGetProperty("message", out var msgElement) 
                    ? msgElement.GetString() ?? "未知錯誤" 
                    : "未知錯誤";
                throw new ApiServiceUnavailableException($"OpenAI API 回傳錯誤: {errorMessage}");
            }
            
            throw new ApiResponseParsingException($"無法從 OpenAI 回應中提取摘要內容: {responseContent}");
        }
        catch (JsonException ex)
        {
            throw new ApiResponseParsingException("OpenAI 回應格式不正確，無法解析 JSON", ex);
        }
    }

    /// <summary>
    /// 檢查服務健康狀態
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("檢查 OpenAI 服務健康狀態");
            
            using var response = await _httpClient.GetAsync(ModelsEndpoint, cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;
            
            _logger.LogDebug("OpenAI 健康檢查結果: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI 健康檢查失敗");
            return false;
        }
    }

    /// <summary>
    /// 測試連接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await IsHealthyAsync(cancellationToken);
    }
    
    /// <summary>
    /// 執行重試邏輯
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = _config.RetryDelayMs;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                attempt++;
                var nextDelay = (int)(delay * Math.Pow(2, attempt - 1));
                
                _logger.LogWarning("OpenAI 操作失敗，準備重試 {Attempt}/{MaxRetries}，延遲 {Delay}ms，錯誤: {Error}",
                    attempt, _config.RetryCount, nextDelay, ex.Message);
                
                await Task.Delay(nextDelay, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// 判斷是否應該重試
    /// </summary>
    private bool ShouldRetry(Exception exception, int attemptCount)
    {
        if (attemptCount >= _config.RetryCount)
            return false;

        return exception switch
        {
            ApiTimeoutException => true,
            ApiConnectionException => true,
            HttpRequestException => true,
            TaskCanceledException => false,
            _ => false
        };
    }
}