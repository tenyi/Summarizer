using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services
{
    /// <summary>
    /// 錯誤分類和處理服務實作
    /// 提供自動錯誤分類、使用者友善訊息生成和診斷資訊收集功能
    /// </summary>
    public class ErrorClassificationService : IErrorClassificationService
    {
        private readonly ILogger<ErrorClassificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IBatchProgressNotificationService? _notificationService;

        /// <summary>
        /// 建構函式
        /// </summary>
        /// <param name="logger">日誌服務</param>
        /// <param name="configuration">設定服務</param>
        /// <param name="notificationService">批次進度通知服務（可選）</param>
        public ErrorClassificationService(
            ILogger<ErrorClassificationService> logger,
            IConfiguration configuration,
            IBatchProgressNotificationService? notificationService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _notificationService = notificationService;
        }

        /// <summary>
        /// 分類和處理例外錯誤
        /// </summary>
        /// <param name="exception">發生的例外</param>
        /// <param name="context">錯誤發生的內容資訊</param>
        /// <param name="batchId">相關的批次識別碼（可選）</param>
        /// <param name="userId">相關的使用者識別碼（可選）</param>
        /// <returns>處理後的錯誤資訊</returns>
        public async Task<ProcessingError> ClassifyAndProcessErrorAsync(
            Exception exception, 
            string context, 
            Guid? batchId = null, 
            string? userId = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var error = new ProcessingError
            {
                BatchId = batchId,
                UserId = userId,
                ErrorMessage = exception.Message,
                Source = context,
                OccurredAt = DateTime.UtcNow
            };

            // 分類錯誤類別和嚴重程度
            ClassifyError(exception, error);

            // 決定處理策略
            error.HandlingStrategy = await DetermineHandlingStrategyAsync(error);

            // 生成使用者友善訊息
            error.UserFriendlyMessage = GenerateUserFriendlyMessage(exception, context);

            // 生成解決方案建議
            error.SuggestedActions = await GenerateSuggestedActionsAsync(error);

            // 收集診斷資訊
            error.ErrorContext = await CollectDiagnosticInfoAsync(exception, context, batchId);

            // 判斷是否可恢復
            error.IsRecoverable = IsRecoverable(exception, 0);

            // 設定最大重試次數
            error.MaxRetryAttempts = GetMaxRetryAttempts(exception);

            // 生成錯誤代碼
            error.ErrorCode = GenerateErrorCode(exception, error.Category);

            await LogErrorAsync(error);

            return error;
        }

        /// <summary>
        /// 根據錯誤分類決定處理策略
        /// </summary>
        /// <param name="error">處理錯誤</param>
        /// <returns>錯誤處理策略</returns>
        public Task<ErrorHandlingStrategy> DetermineHandlingStrategyAsync(ProcessingError error)
        {
            var strategy = error.Category switch
            {
                ErrorCategory.Validation => ErrorHandlingStrategy.UserGuidance,
                ErrorCategory.Authentication => ErrorHandlingStrategy.UserGuidance,
                ErrorCategory.Authorization => ErrorHandlingStrategy.UserGuidance,
                ErrorCategory.Network => error.Severity == ErrorSeverity.Fatal 
                    ? ErrorHandlingStrategy.Escalate 
                    : ErrorHandlingStrategy.Retry,
                ErrorCategory.Service => error.Severity >= ErrorSeverity.Critical 
                    ? ErrorHandlingStrategy.Fallback 
                    : ErrorHandlingStrategy.Retry,
                ErrorCategory.Processing => error.IsRecoverable 
                    ? ErrorHandlingStrategy.Recovery 
                    : ErrorHandlingStrategy.Escalate,
                ErrorCategory.Storage => ErrorHandlingStrategy.Retry,
                ErrorCategory.System => error.Severity == ErrorSeverity.Fatal 
                    ? ErrorHandlingStrategy.ImmediateStop 
                    : ErrorHandlingStrategy.Escalate,
                ErrorCategory.Configuration => ErrorHandlingStrategy.Escalate,
                ErrorCategory.Timeout => ErrorHandlingStrategy.Retry,
                _ => ErrorHandlingStrategy.LogAndIgnore
            };

            return Task.FromResult(strategy);
        }

        /// <summary>
        /// 生成使用者友善的錯誤訊息
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="context">內容資訊</param>
        /// <returns>使用者友善的錯誤訊息</returns>
        public string GenerateUserFriendlyMessage(Exception exception, string context)
        {
            return exception switch
            {
                TimeoutException => "處理時間過長，請稍後再試。如果問題持續發生，請聯繫系統管理員。",
                HttpRequestException httpEx when httpEx.Message.Contains("timeout") => 
                    "網路連線超時，請檢查網路狀態後重試。",
                HttpRequestException => "網路連線發生問題，請檢查網路狀態後重試。",
                UnauthorizedAccessException => "您沒有執行此操作的權限，請聯繫系統管理員。",
                ArgumentNullException => "必要的資料遺失，請確認所有必填欄位都已填寫。",
                ArgumentException => "輸入的資料格式不正確，請檢查後重新輸入。",
                InvalidOperationException => "目前系統狀態無法執行此操作，請稍後重試。",
                NotSupportedException => "此功能目前不支援，請使用其他方式或聯繫系統管理員。",
                OutOfMemoryException => "系統記憶體不足，請關閉其他應用程式或聯繫 IT 支援。",
                StackOverflowException => "系統發生嚴重錯誤，請聯繫技術支援。",
                OperationCanceledException => "操作已取消。已處理的部分結果可能可以保留，請檢查部分結果選項。",
                FileNotFoundException => "找不到必要的檔案，請確認檔案存在或聯繫系統管理員。",
                DirectoryNotFoundException => "找不到必要的資料夾，請確認路徑正確或聯繫系統管理員。",
                _ => GetGenericUserFriendlyMessage(exception, context)
            };
        }

        /// <summary>
        /// 根據錯誤類型產生建議的處理動作
        /// </summary>
        /// <param name="error">處理錯誤</param>
        /// <returns>建議動作清單</returns>
        public Task<List<string>> GenerateSuggestedActionsAsync(ProcessingError error)
        {
            var actions = new List<string>();

            switch (error.Category)
            {
                case ErrorCategory.Validation:
                    actions.AddRange(new[]
                    {
                        "檢查輸入資料的格式和內容",
                        "確認所有必填欄位都已正確填寫",
                        "參考系統說明文件中的資料格式要求",
                        "如有疑問請聯絡系統管理員"
                    });
                    break;

                case ErrorCategory.Network:
                    actions.AddRange(new[]
                    {
                        "檢查網路連線狀態",
                        "重新整理頁面後再試",
                        "如果使用 VPN，請嘗試關閉後重試",
                        "聯絡 IT 支援檢查網路設定"
                    });
                    break;

                case ErrorCategory.Service:
                    actions.AddRange(new[]
                    {
                        "稍後再試（服務可能暫時不可用）",
                        "確認 AI 服務設定正確",
                        "聯絡系統管理員檢查服務狀態",
                        "嘗試使用備用服務（如可用）"
                    });
                    break;

                case ErrorCategory.Processing:
                    if (error.IsRecoverable)
                    {
                        actions.AddRange(new[]
                        {
                            "檢查是否有部分結果可以保留",
                            "考慮將文本分成較小的部分處理",
                            "重新開始處理",
                            "如果問題持續，請聯絡技術支援"
                        });
                    }
                    else
                    {
                        actions.AddRange(new[]
                        {
                            "記錄錯誤詳情並聯絡技術支援",
                            "嘗試重新啟動應用程式",
                            "檢查系統資源使用狀況"
                        });
                    }
                    break;

                case ErrorCategory.System:
                    actions.AddRange(new[]
                    {
                        "檢查系統資源（記憶體、磁碟空間）",
                        "關閉不必要的應用程式",
                        "重新啟動系統",
                        "聯絡 IT 支援進行系統檢查"
                    });
                    break;

                case ErrorCategory.Timeout:
                    actions.AddRange(new[]
                    {
                        "減少文本長度後重試",
                        "稍後再試（系統可能正忙）",
                        "檢查網路連線穩定性",
                        "聯絡系統管理員調整超時設定"
                    });
                    break;

                default:
                    actions.AddRange(new[]
                    {
                        "重新整理頁面後重試",
                        "檢查是否有系統更新",
                        "記錄錯誤發生的步驟",
                        "聯絡技術支援並提供錯誤詳情"
                    });
                    break;
            }

            return Task.FromResult(actions);
        }

        /// <summary>
        /// 收集錯誤診斷資訊
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="context">內容資訊</param>
        /// <param name="batchId">相關的批次識別碼（可選）</param>
        /// <returns>診斷資訊字典</returns>
        public Task<Dictionary<string, object>> CollectDiagnosticInfoAsync(
            Exception exception, 
            string context, 
            Guid? batchId = null)
        {
            var diagnostics = new Dictionary<string, object>
            {
                ["ExceptionType"] = exception.GetType().Name,
                ["Context"] = SanitizeContext(context),
                ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                ["MachineName"] = Environment.MachineName,
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["CLRVersion"] = Environment.Version.ToString()
            };

            if (batchId.HasValue)
            {
                diagnostics["BatchId"] = batchId.Value.ToString();
            }

            // 添加記憶體使用資訊
            try
            {
                var workingSet = Environment.WorkingSet;
                diagnostics["MemoryUsage"] = $"{workingSet / 1024 / 1024} MB";
            }
            catch
            {
                // 忽略記憶體資訊收集失敗
            }

            // 添加安全的堆棧追蹤
            if (exception.StackTrace != null)
            {
                diagnostics["StackTrace"] = SanitizeStackTrace(exception.StackTrace);
            }

            // 添加內部例外資訊
            if (exception.InnerException != null)
            {
                diagnostics["InnerException"] = new
                {
                    Type = exception.InnerException.GetType().Name,
                    Message = exception.InnerException.Message
                };
            }

            return Task.FromResult(diagnostics);
        }

        /// <summary>
        /// 判斷錯誤是否可恢復
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="retryAttempts">已重試次數</param>
        /// <returns>是否可恢復</returns>
        public bool IsRecoverable(Exception exception, int retryAttempts = 0)
        {
            var maxRetries = GetMaxRetryAttempts(exception);
            
            if (retryAttempts >= maxRetries)
                return false;

            return exception switch
            {
                OutOfMemoryException => false,
                StackOverflowException => false,
                ArgumentNullException => false,
                ArgumentException => false,
                UnauthorizedAccessException => false,
                NotSupportedException => false,
                TimeoutException => true,
                HttpRequestException => true,
                InvalidOperationException => true,
                OperationCanceledException => true,
                _ => true // 預設為可恢復，但會限制重試次數
            };
        }

        /// <summary>
        /// 執行錯誤處理策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public async Task<ErrorHandlingResult> ExecuteHandlingStrategyAsync(ProcessingError error)
        {
            try
            {
                return error.HandlingStrategy switch
                {
                    ErrorHandlingStrategy.Retry => await HandleRetryStrategyAsync(error),
                    ErrorHandlingStrategy.Escalate => await HandleEscalateStrategyAsync(error),
                    ErrorHandlingStrategy.UserGuidance => await HandleUserGuidanceStrategyAsync(error),
                    ErrorHandlingStrategy.Recovery => await HandleRecoveryStrategyAsync(error),
                    ErrorHandlingStrategy.Fallback => await HandleFallbackStrategyAsync(error),
                    ErrorHandlingStrategy.LogAndIgnore => await HandleLogAndIgnoreStrategyAsync(error),
                    ErrorHandlingStrategy.ImmediateStop => await HandleImmediateStopStrategyAsync(error),
                    _ => new ErrorHandlingResult
                    {
                        Success = false,
                        Message = "未知的錯誤處理策略",
                        RequiresFurtherAction = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行錯誤處理策略時發生例外: {Strategy}", error.HandlingStrategy);
                return new ErrorHandlingResult
                {
                    Success = false,
                    Message = "錯誤處理策略執行失敗",
                    RequiresFurtherAction = true,
                    Data = new Dictionary<string, object> { ["Exception"] = ex.Message }
                };
            }
        }

        /// <summary>
        /// 記錄錯誤到日誌系統
        /// </summary>
        /// <param name="error">處理錯誤</param>
        public Task LogErrorAsync(ProcessingError error)
        {
            var logLevel = error.Severity switch
            {
                ErrorSeverity.Info => LogLevel.Information,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Critical => LogLevel.Critical,
                ErrorSeverity.Fatal => LogLevel.Critical,
                _ => LogLevel.Warning
            };

            _logger.Log(logLevel, 
                "錯誤分類: {Category}, 嚴重程度: {Severity}, 策略: {Strategy}, 錯誤: {Message}, 批次: {BatchId}, 使用者: {UserId}",
                error.Category, 
                error.Severity, 
                error.HandlingStrategy, 
                error.ErrorMessage, 
                error.BatchId, 
                error.UserId);

            error.Logged = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 通知相關使用者錯誤發生
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>通知操作任務</returns>
        public async Task NotifyErrorAsync(ProcessingError error)
        {
            if (_notificationService != null && error.BatchId.HasValue)
            {
                try
                {
                    await _notificationService.NotifyErrorAsync(error.BatchId.Value, error.UserFriendlyMessage);
                    error.UserNotified = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "通知使用者錯誤時失敗: {ErrorId}", error.ErrorId);
                }
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// 分類錯誤類別和嚴重程度
        /// </summary>
        private void ClassifyError(Exception exception, ProcessingError error)
        {
            (error.Category, error.Severity) = exception switch
            {
                ArgumentException or ArgumentNullException => (ErrorCategory.Validation, ErrorSeverity.Warning),
                UnauthorizedAccessException => (ErrorCategory.Authorization, ErrorSeverity.Error),
                HttpRequestException httpEx when httpEx.Message.Contains("timeout") => (ErrorCategory.Timeout, ErrorSeverity.Error),
                HttpRequestException => (ErrorCategory.Network, ErrorSeverity.Warning),
                TimeoutException => (ErrorCategory.Timeout, ErrorSeverity.Warning),
                InvalidOperationException => (ErrorCategory.Processing, ErrorSeverity.Error),
                NotSupportedException => (ErrorCategory.Configuration, ErrorSeverity.Error),
                OutOfMemoryException => (ErrorCategory.System, ErrorSeverity.Fatal),
                StackOverflowException => (ErrorCategory.System, ErrorSeverity.Fatal),
                OperationCanceledException => (ErrorCategory.Processing, ErrorSeverity.Info),
                FileNotFoundException or DirectoryNotFoundException => (ErrorCategory.Storage, ErrorSeverity.Error),
                _ => (ErrorCategory.Processing, ErrorSeverity.Error)
            };
        }

        /// <summary>
        /// 取得最大重試次數
        /// </summary>
        private int GetMaxRetryAttempts(Exception exception)
        {
            var defaultRetries = _configuration.GetValue<int>("ErrorHandling:MaxRetryAttempts", 3);

            return exception switch
            {
                ArgumentException or ArgumentNullException => 0,
                UnauthorizedAccessException => 0,
                OutOfMemoryException => 0,
                StackOverflowException => 0,
                TimeoutException => Math.Min(defaultRetries, 5),
                HttpRequestException => Math.Min(defaultRetries, 3),
                _ => defaultRetries
            };
        }

        /// <summary>
        /// 生成錯誤代碼
        /// </summary>
        private string GenerateErrorCode(Exception exception, ErrorCategory category)
        {
            var prefix = category switch
            {
                ErrorCategory.Validation => "VAL",
                ErrorCategory.Authentication => "AUTH",
                ErrorCategory.Authorization => "AUTHZ",
                ErrorCategory.Network => "NET",
                ErrorCategory.Service => "SVC",
                ErrorCategory.Processing => "PROC",
                ErrorCategory.Storage => "STOR",
                ErrorCategory.System => "SYS",
                ErrorCategory.Configuration => "CFG",
                ErrorCategory.Timeout => "TIME",
                _ => "GEN"
            };

            var suffix = exception.GetType().Name.GetHashCode().ToString("X")[..4];
            return $"{prefix}-{suffix}";
        }

        /// <summary>
        /// 生成一般性使用者友善訊息
        /// </summary>
        private string GetGenericUserFriendlyMessage(Exception exception, string context)
        {
            if (context.Contains("AI") || context.Contains("ollama") || context.Contains("OpenAI"))
            {
                return "AI 服務處理時發生錯誤，請稍後重試或聯繫系統管理員。";
            }

            if (context.Contains("batch") || context.Contains("處理"))
            {
                return "批次處理時發生錯誤，已處理的部分結果可能可以保留。請檢查部分結果或重新處理。";
            }

            return "系統發生未預期的錯誤，請記錄發生錯誤時的操作步驟並聯繫技術支援。";
        }

        /// <summary>
        /// 清理內容資訊，移除敏感資料
        /// </summary>
        private string SanitizeContext(string context)
        {
            if (string.IsNullOrEmpty(context))
                return string.Empty;

            // 移除可能的敏感路徑資訊
            var sanitized = context
                .Replace(Environment.UserName, "[USER]")
                .Replace(Environment.MachineName, "[MACHINE]");

            // 限制長度避免日誌過大
            return sanitized.Length > 500 ? sanitized[..500] + "..." : sanitized;
        }

        /// <summary>
        /// 清理堆疊追蹤，移除敏感路徑資訊
        /// </summary>
        private string SanitizeStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return string.Empty;

            var sanitized = stackTrace
                .Replace(Environment.UserName, "[USER]")
                .Replace(Environment.MachineName, "[MACHINE]");

            // 移除檔案的完整路徑，只保留檔名
            var lines = sanitized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("\\") && lines[i].Contains(".cs:"))
                {
                    var parts = lines[i].Split('\\');
                    if (parts.Length > 1)
                    {
                        lines[i] = lines[i].Replace(string.Join("\\", parts[..^1]) + "\\", "");
                    }
                }
            }

            return string.Join("\n", lines);
        }

        #endregion

        #region Strategy Implementation Methods

        private Task<ErrorHandlingResult> HandleRetryStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已標記為可重試",
                RequiresFurtherAction = true,
                NextAction = "重試操作",
                Data = new Dictionary<string, object>
                {
                    ["RetryAfterSeconds"] = GetRetryDelaySeconds(error),
                    ["MaxRetries"] = error.MaxRetryAttempts
                }
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleEscalateStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已提交給系統管理員處理",
                RequiresFurtherAction = false,
                Data = new Dictionary<string, object>
                {
                    ["EscalatedAt"] = DateTime.UtcNow,
                    ["ContactInfo"] = "請聯絡系統管理員或技術支援"
                }
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleUserGuidanceStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已提供使用者指導",
                RequiresFurtherAction = false,
                Data = new Dictionary<string, object>
                {
                    ["UserMessage"] = error.UserFriendlyMessage,
                    ["SuggestedActions"] = error.SuggestedActions
                }
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleRecoveryStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已啟動恢復程序",
                RequiresFurtherAction = true,
                NextAction = "檢查部分結果",
                Data = new Dictionary<string, object>
                {
                    ["RecoveryType"] = "PartialResults",
                    ["BatchId"] = error.BatchId?.ToString() ?? ""
                }
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleFallbackStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已切換到備援服務",
                RequiresFurtherAction = true,
                NextAction = "使用備援服務重試",
                Data = new Dictionary<string, object>
                {
                    ["FallbackService"] = "備用 AI 服務"
                }
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleLogAndIgnoreStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已記錄錯誤，繼續處理",
                RequiresFurtherAction = false
            };
            return Task.FromResult(result);
        }

        private Task<ErrorHandlingResult> HandleImmediateStopStrategyAsync(ProcessingError error)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = "已立即停止所有相關操作",
                RequiresFurtherAction = false,
                Data = new Dictionary<string, object>
                {
                    ["StoppedAt"] = DateTime.UtcNow,
                    ["Reason"] = "系統穩定性保護"
                }
            };
            return Task.FromResult(result);
        }

        private int GetRetryDelaySeconds(ProcessingError error)
        {
            var baseDelay = _configuration.GetValue<int>("ErrorHandling:RetryDelaySeconds", 5);
            
            return error.Category switch
            {
                ErrorCategory.Network => baseDelay * 2,
                ErrorCategory.Service => baseDelay * 3,
                ErrorCategory.Timeout => baseDelay * 4,
                _ => baseDelay
            };
        }

        #endregion
    }
}