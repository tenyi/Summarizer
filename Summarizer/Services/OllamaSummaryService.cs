using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Exceptions;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// Ollama API 摘要服務實作
/// </summary>
public class OllamaSummaryService : IOllamaSummaryService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaConfig _config;
    private readonly ILogger<OllamaSummaryService> _logger;
    
    private const string GenerateEndpoint = "/api/generate";
    private const string TagsEndpoint = "/api/tags";

    public OllamaSummaryService(
        HttpClient httpClient,
        IOptions<OllamaConfig> config,
        ILogger<OllamaSummaryService> logger)
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
        _httpClient.BaseAddress = new Uri(_config.Endpoint);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.Timeout);
        
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
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
        
        _logger.LogInformation("開始生成摘要，相關 ID: {CorrelationId}，文本長度: {TextLength}",
            correlationId, text.Length);

        try
        {
            var summary = await ExecuteWithRetryAsync(
                () => GenerateSummaryInternalAsync(text, correlationId, cancellationToken),
                cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("摘要生成完成，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms，摘要長度: {SummaryLength}",
                correlationId, processingTime, summary.Length);

            return summary;
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "摘要生成失敗，相關 ID: {CorrelationId}，處理時間: {ProcessingTime}ms",
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
        var prompt = BuildSummaryPrompt(text);
        var requestPayload = new
        {
            model = _config.Model,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9,
                max_tokens = 2000
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug("發送 Ollama API 請求，相關 ID: {CorrelationId}，模型: {Model}",
            correlationId, _config.Model);

        using var response = await _httpClient.PostAsync(GenerateEndpoint, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = $"Ollama API 請求失敗，狀態碼: {response.StatusCode}，錯誤內容: {errorContent}";
            
            throw response.StatusCode switch
            {
                System.Net.HttpStatusCode.RequestTimeout => new ApiTimeoutException(errorMessage),
                System.Net.HttpStatusCode.ServiceUnavailable => new ApiServiceUnavailableException(errorMessage),
                _ => new ApiConnectionException(errorMessage)
            };
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractSummaryFromResponse(responseContent, correlationId);
    }
    
    /// <summary>
    /// 建立摘要提示詞
    /// </summary>
    private static string BuildSummaryPrompt(string text)
    {
        return $@"請為以下文本生成一個簡潔明了的中文摘要：

文本內容：
{text}

要求：
1. 使用繁體中文
2. 摘要長度約為原文的 20-30%
3. 保留重要資訊和關鍵點
4. 語言簡潔流暢
5. 只回傳摘要內容，不要包含額外說明

摘要：";
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
            
            if (root.TryGetProperty("response", out var responseElement))
            {
                var summary = responseElement.GetString()?.Trim();
                if (!string.IsNullOrEmpty(summary))
                {
                    _logger.LogDebug("成功提取摘要，相關 ID: {CorrelationId}，長度: {Length}",
                        correlationId, summary.Length);
                    return summary;
                }
            }
            
            if (root.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = errorElement.GetString() ?? "未知錯誤";
                throw new ApiServiceUnavailableException($"Ollama API 回傳錯誤: {errorMessage}");
            }
            
            throw new ApiResponseParsingException($"無法從回應中提取摘要內容: {responseContent}");
        }
        catch (JsonException ex)
        {
            throw new ApiResponseParsingException("回應格式不正確，無法解析 JSON", ex);
        }
    }

    /// <summary>
    /// 檢查服務健康狀態
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("檢查 Ollama 服務健康狀態");
            
            using var response = await _httpClient.GetAsync(TagsEndpoint, cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;
            
            _logger.LogDebug("Ollama 健康檢查結果: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama 健康檢查失敗");
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
                
                _logger.LogWarning("操作失敗，準備重試 {Attempt}/{MaxRetries}，延遲 {Delay}ms，錯誤: {Error}",
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