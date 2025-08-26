//===============================================================
// 檔案：Middleware/ErrorHandlerMiddleware.cs
// 說明：提供全域的錯誤處理機制，整合錯誤分類和處理策略。
//===============================================================

using System.Net;
using System.Text.Json;
using Summarizer.Exceptions;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Middleware
{
    /// <summary>
    /// 錯誤處理中介軟體，提供統一的錯誤處理和分類機制
    /// </summary>
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlerMiddleware> _logger;
        private readonly IErrorClassificationService _errorClassificationService;
        private readonly IBatchProgressNotificationService _notificationService;

        public ErrorHandlerMiddleware(
            RequestDelegate next, 
            ILogger<ErrorHandlerMiddleware> logger,
            IErrorClassificationService errorClassificationService,
            IBatchProgressNotificationService notificationService)
        {
            _next = next;
            _logger = logger;
            _errorClassificationService = errorClassificationService;
            _notificationService = notificationService;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception error)
            {
                await HandleExceptionAsync(context, error);
            }
        }

        /// <summary>
        /// 處理例外狀況的統一入口
        /// </summary>
        /// <param name="context">HTTP 上下文</param>
        /// <param name="error">例外狀況</param>
        private async Task HandleExceptionAsync(HttpContext context, Exception error)
        {
            var correlationId = context.TraceIdentifier;
            var requestPath = context.Request.Path.Value ?? "";
            
            try
            {
                // 使用錯誤分類服務分析錯誤
                var processedError = await _errorClassificationService.ClassifyAndProcessErrorAsync(error, $"HTTP請求: {requestPath}");
                
                // 記錄錯誤日誌
                LogError(error, processedError, correlationId, requestPath);

                // 設定回應
                var response = context.Response;
                response.ContentType = "application/json";
                response.StatusCode = GetHttpStatusCode(error, processedError.Severity);

                // 建立錯誤回應
                var errorResponse = CreateErrorResponse(processedError, correlationId);
                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                await response.WriteAsync(jsonResponse);

                // 發送錯誤通知（如果可以提取 BatchId）
                await NotifyErrorAsync(context, processedError);
            }
            catch (Exception middlewareError)
            {
                // 中介軟體本身發生錯誤時的備用處理
                _logger.LogCritical(middlewareError, 
                    "錯誤處理中介軟體發生嚴重錯誤，相關 ID: {CorrelationId}，原始錯誤: {OriginalError}", 
                    correlationId, error?.Message);

                await HandleFallbackError(context, error, correlationId);
            }
        }

        /// <summary>
        /// 記錄錯誤日誌
        /// </summary>
        private void LogError(Exception error, ProcessingError processedError, string correlationId, string requestPath)
        {
            var logLevel = processedError.Severity switch
            {
                ErrorSeverity.Fatal => LogLevel.Critical,
                ErrorSeverity.Critical => LogLevel.Error,
                ErrorSeverity.Error => LogLevel.Warning,
                ErrorSeverity.Warning => LogLevel.Information,
                ErrorSeverity.Info => LogLevel.Debug,
                _ => LogLevel.Warning
            };

            _logger.Log(logLevel, error,
                "處理請求時發生 {Severity} 級別錯誤，相關 ID: {CorrelationId}，路徑: {RequestPath}，錯誤類別: {ErrorCategory}，可恢復: {IsRecoverable}",
                processedError.Severity, correlationId, requestPath, processedError.Category, processedError.IsRecoverable);
        }

        /// <summary>
        /// 根據錯誤類型和嚴重程度決定 HTTP 狀態碼
        /// </summary>
        private int GetHttpStatusCode(Exception error, ErrorSeverity severity)
        {
            return error switch
            {
                // 特定例外類型的狀態碼
                ApiTimeoutException => (int)HttpStatusCode.RequestTimeout,
                ApiServiceUnavailableException => (int)HttpStatusCode.ServiceUnavailable,
                ApiConnectionException => (int)HttpStatusCode.BadGateway,
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                NotSupportedException => (int)HttpStatusCode.NotImplemented,
                InvalidOperationException => (int)HttpStatusCode.Conflict,
                TimeoutException => (int)HttpStatusCode.RequestTimeout,
                HttpRequestException => (int)HttpStatusCode.BadGateway,
                
                // 根據嚴重程度決定狀態碼
                _ => severity switch
                {
                    ErrorSeverity.Fatal => (int)HttpStatusCode.InternalServerError,
                    ErrorSeverity.Critical => (int)HttpStatusCode.InternalServerError,
                    ErrorSeverity.Error => (int)HttpStatusCode.BadRequest,
                    ErrorSeverity.Warning => (int)HttpStatusCode.BadRequest,
                    ErrorSeverity.Info => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                }
            };
        }

        /// <summary>
        /// 建立標準化的錯誤回應
        /// </summary>
        private object CreateErrorResponse(ProcessingError processedError, string correlationId)
        {
            return new
            {
                Success = false,
                Error = processedError.UserFriendlyMessage,
                ErrorCode = processedError.ErrorCode ?? "UNHANDLED_ERROR",
                ErrorId = processedError.ErrorId,
                Severity = processedError.Severity.ToString(),
                IsRecoverable = processedError.IsRecoverable,
                SuggestedActions = processedError.SuggestedActions,
                Timestamp = processedError.OccurredAt,
                CorrelationId = correlationId,
                TechnicalDetails = new
                {
                    ErrorMessage = processedError.ErrorMessage,
                    Category = processedError.Category.ToString(),
                    Context = processedError.ErrorContext,
                    RetryCount = processedError.RetryAttempts
                }
            };
        }

        /// <summary>
        /// 發送錯誤通知
        /// </summary>
        private async Task NotifyErrorAsync(HttpContext context, ProcessingError processedError)
        {
            try
            {
                // 嘗試從請求路徑或表單資料中提取 BatchId
                var batchId = ExtractBatchIdFromContext(context);
                if (batchId.HasValue)
                {
                    await _notificationService.NotifyErrorAsync(batchId.Value, processedError.UserFriendlyMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "發送錯誤通知時失敗，ErrorId: {ErrorId}", processedError.ErrorId);
            }
        }

        /// <summary>
        /// 從 HTTP 上下文中提取 BatchId
        /// </summary>
        private Guid? ExtractBatchIdFromContext(HttpContext context)
        {
            try
            {
                // 從路徑參數中提取
                var path = context.Request.Path.Value ?? "";
                var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                for (int i = 0; i < pathSegments.Length - 1; i++)
                {
                    if (pathSegments[i].Equals("batch", StringComparison.OrdinalIgnoreCase) ||
                        pathSegments[i].Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
                        pathSegments[i].Equals("recovery", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Guid.TryParse(pathSegments[i + 1], out var batchId))
                        {
                            return batchId;
                        }
                    }
                }

                // 從查詢參數中提取
                if (context.Request.Query.TryGetValue("batchId", out var queryBatchId))
                {
                    if (Guid.TryParse(queryBatchId.FirstOrDefault(), out var batchId))
                    {
                        return batchId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "提取 BatchId 時發生錯誤");
            }

            return null;
        }

        /// <summary>
        /// 備用錯誤處理（當主要錯誤處理失敗時使用）
        /// </summary>
        private async Task HandleFallbackError(HttpContext context, Exception originalError, string correlationId)
        {
            try
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var fallbackResponse = new
                {
                    Success = false,
                    Error = "系統發生內部錯誤",
                    ErrorCode = "INTERNAL_ERROR",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(fallbackResponse);
                await context.Response.WriteAsync(json);
            }
            catch (Exception fallbackError)
            {
                _logger.LogCritical(fallbackError, 
                    "備用錯誤處理也失敗了，相關 ID: {CorrelationId}，原始錯誤: {OriginalError}", 
                    correlationId, originalError?.Message);
                
                // 最後的備用方案：直接寫入簡單的錯誤訊息
                try
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Internal Server Error");
                }
                catch
                {
                    // 如果連這都失敗，就沒有其他辦法了
                }
            }
        }
    }
}
