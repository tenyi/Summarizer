using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 記錄並忽略策略
    /// 適用於對系統運作影響輕微的錯誤，記錄錯誤資訊後繼續正常運作
    /// 通常用於非關鍵功能的錯誤或可接受的失敗情況
    /// </summary>
    public class LogAndIgnoreStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化記錄並忽略策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public LogAndIgnoreStrategy(
            ILogger<BaseErrorHandlingStrategy> logger,
            IBatchProgressNotificationService notificationService,
            ICancellationService cancellationService,
            IPartialResultHandler partialResultHandler)
            : base(logger, notificationService, cancellationService, partialResultHandler)
        {
        }

        /// <summary>
        /// 策略類型
        /// </summary>
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.LogAndIgnore;

        /// <summary>
        /// 執行記錄並忽略策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "記錄並忽略策略");

            try
            {
                // 檢查錯誤是否真的可以安全忽略
                var safetyCheck = await PerformSafetyCheckAsync(error);
                if (!safetyCheck.IsSafeToIgnore)
                {
                    return CreateFailureResult(
                        $"安全檢查失敗，錯誤不可忽略：{safetyCheck.Reason}",
                        true,
                        "建議使用其他錯誤處理策略");
                }

                // 執行詳細日誌記錄
                await PerformDetailedLoggingAsync(error);

                // 收集統計資訊
                await CollectErrorStatisticsAsync(error);

                // 檢查是否需要發送提醒通知
                var shouldNotify = await ShouldSendNotificationAsync(error);
                if (shouldNotify)
                {
                    await SendIgnoreNotificationAsync(error);
                }

                // 記錄忽略決定
                RecordIgnoreDecision(error);

                // 檢查是否需要監控後續類似錯誤
                await SetupMonitoringIfNeededAsync(error);

                var result = CreateSuccessResult(
                    "錯誤已記錄並安全忽略，系統繼續正常運作",
                    false,
                    "無需額外操作，系統將繼續監控類似錯誤");

                // 將處理資訊加入結果資料
                result.Data["SafetyCheck"] = safetyCheck;
                result.Data["LoggingCompleted"] = true;
                result.Data["StatisticsCollected"] = true;
                result.Data["IgnoredAt"] = DateTime.UtcNow;

                LogHandlingComplete(error, result, "記錄並忽略策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行記錄並忽略策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                
                // 即使策略執行失敗，也要嘗試基本的日誌記錄
                try
                {
                    _logger.LogWarning("原始錯誤（策略失敗時的補救記錄）- ID: {ErrorId}, 類型: {Category}, 訊息: {Message}",
                        error.ErrorId, error.Category, error.ErrorMessage);
                }
                catch
                {
                    // 忽略補救記錄的失敗
                }

                return CreateFailureResult(
                    $"記錄並忽略策略執行異常: {ex.Message}",
                    true,
                    "策略失敗但原錯誤資訊已記錄");
            }
        }

        /// <summary>
        /// 判斷是否適合記錄並忽略處理
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合記錄並忽略</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查錯誤是否適合忽略
            return IsIgnorable(error);
        }

        /// <summary>
        /// 執行安全性檢查
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>安全檢查結果</returns>
        private async Task<SafetyCheckResult> PerformSafetyCheckAsync(ProcessingError error)
        {
            await Task.Delay(50); // 模擬安全檢查時間

            var result = new SafetyCheckResult();

            // 檢查錯誤嚴重程度
            if (error.Severity >= ErrorSeverity.Error)
            {
                result.IsSafeToIgnore = false;
                result.Reason = $"錯誤嚴重程度為 {error.Severity}，不適合忽略";
                result.RiskLevel = RiskLevel.High;
                return result;
            }

            // 檢查錯誤類型
            switch (error.Category)
            {
                case ErrorCategory.Authorization:
                    result.IsSafeToIgnore = false;
                    result.Reason = "安全相關錯誤不可忽略";
                    result.RiskLevel = RiskLevel.High;
                    break;

                case ErrorCategory.System:
                    result.IsSafeToIgnore = error.Severity == ErrorSeverity.Info;
                    result.Reason = result.IsSafeToIgnore ? "低嚴重程度系統錯誤可以忽略" : "系統錯誤風險較高";
                    result.RiskLevel = result.IsSafeToIgnore ? RiskLevel.Low : RiskLevel.Medium;
                    break;

                case ErrorCategory.Processing:
                    result.IsSafeToIgnore = error.Severity == ErrorSeverity.Info;
                    result.Reason = result.IsSafeToIgnore ? "非關鍵業務邏輯錯誤可以忽略" : "業務邏輯錯誤需要注意";
                    result.RiskLevel = result.IsSafeToIgnore ? RiskLevel.Low : RiskLevel.Medium;
                    break;

                case ErrorCategory.Validation:
                case ErrorCategory.Authentication:
                    result.IsSafeToIgnore = false;
                    result.Reason = $"{error.Category} 類型錯誤可能影響功能正確性";
                    result.RiskLevel = RiskLevel.Medium;
                    break;

                default:
                    // 對於其他類型，根據嚴重程度決定
                    result.IsSafeToIgnore = error.Severity <= ErrorSeverity.Warning;
                    result.Reason = result.IsSafeToIgnore ? "錯誤影響程度可接受" : "錯誤可能影響系統運作";
                    result.RiskLevel = error.Severity <= ErrorSeverity.Info ? RiskLevel.Low : RiskLevel.Medium;
                    break;
            }

            // 檢查錯誤頻率
            var frequency = await CheckErrorFrequencyAsync(error);
            if (frequency.IsHighFrequency)
            {
                result.IsSafeToIgnore = false;
                result.Reason = $"錯誤發生頻率過高 ({frequency.Count} 次/小時)，需要關注";
                result.RiskLevel = RiskLevel.High;
            }

            return result;
        }

        /// <summary>
        /// 執行詳細日誌記錄
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task PerformDetailedLoggingAsync(ProcessingError error)
        {
            await Task.Delay(20); // 模擬詳細記錄時間

            // 根據錯誤嚴重程度選擇日誌等級
            var logLevel = error.Severity switch
            {
                ErrorSeverity.Critical => LogLevel.Critical,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Info => LogLevel.Information,
                _ => LogLevel.Information
            };

            // 記錄基本錯誤資訊
            _logger.Log(logLevel,
                "錯誤已忽略處理 - ID: {ErrorId}, 類型: {Category}, 嚴重程度: {Severity}, " +
                "批次: {BatchId}, 上下文: {Context}, 訊息: {Message}",
                error.ErrorId, error.Category, error.Severity,
                error.BatchId, error.ErrorContext, error.ErrorMessage);

            // 記錄錯誤上下文資訊（替代堆疊追蹤）
            if (error.ErrorContext.Any())
            {
                _logger.LogDebug("忽略錯誤的上下文資訊 - ID: {ErrorId}\n{Context}",
                    error.ErrorId, string.Join(", ", error.ErrorContext.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            // 記錄診斷資訊
            if (error.DiagnosticInfo?.Any() == true)
            {
                _logger.LogDebug("忽略錯誤的診斷資訊 - ID: {ErrorId}, 診斷資料: {@DiagnosticInfo}",
                    error.ErrorId, error.DiagnosticInfo);
            }

            // 記錄建議的解決方案（供將來參考）
            if (error.SuggestedActions.Any())
            {
                _logger.LogInformation("忽略錯誤的建議解決方案 - ID: {ErrorId}, 建議: {Suggestions}",
                    error.ErrorId, string.Join("; ", error.SuggestedActions));
            }
        }

        /// <summary>
        /// 收集錯誤統計資訊
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task CollectErrorStatisticsAsync(ProcessingError error)
        {
            await Task.Delay(30); // 模擬統計收集時間

            try
            {
                // 更新錯誤計數器（這裡可以整合實際的監控系統）
                var statisticsKey = $"{error.Category}_{error.Severity}";
                
                // 記錄到元資料中以便追蹤
                if (!error.ErrorContext.ContainsKey("StatisticsCollected"))
                {
                    error.ErrorContext["StatisticsCollected"] = true;
                    error.ErrorContext["StatisticsKey"] = statisticsKey;
                    error.ErrorContext["CollectedAt"] = DateTime.UtcNow;
                }

                _logger.LogDebug("錯誤統計資訊已收集 - 統計鍵: {StatisticsKey}, 錯誤ID: {ErrorId}",
                    statisticsKey, error.ErrorId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集錯誤統計資訊時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 檢查是否需要發送通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否需要通知</returns>
        private async Task<bool> ShouldSendNotificationAsync(ProcessingError error)
        {
            await Task.Delay(10); // 模擬檢查時間

            // 根據錯誤類型和使用者設定決定是否通知
            return error.Severity switch
            {
                ErrorSeverity.Warning => true,  // 中等嚴重程度的錯誤進行通知
                ErrorSeverity.Error => true,    // 高嚴重程度錯誤應該通知（雖然很少會忽略）
                ErrorSeverity.Info => false,    // 低嚴重程度錯誤通常不通知
                _ => false
            };
        }

        /// <summary>
        /// 發送忽略通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task SendIgnoreNotificationAsync(ProcessingError error)
        {
            var message = $"ℹ️ 系統遇到非關鍵錯誤已自動處理：{error.UserFriendlyMessage}";
            await SendNotificationAsync(error, message);
        }

        /// <summary>
        /// 檢查錯誤頻率
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>頻率檢查結果</returns>
        private async Task<FrequencyCheckResult> CheckErrorFrequencyAsync(ProcessingError error)
        {
            await Task.Delay(20); // 模擬頻率檢查時間

            // 這裡可以整合實際的錯誤追蹤系統
            // 目前使用簡化的邏輯
            return new FrequencyCheckResult
            {
                Count = 1, // 假設計數
                IsHighFrequency = false,
                TimeWindow = "1 hour"
            };
        }

        /// <summary>
        /// 設定監控（如果需要）
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task SetupMonitoringIfNeededAsync(ProcessingError error)
        {
            await Task.Delay(15); // 模擬監控設定時間

            // 對於某些類型的錯誤，設定監控以追蹤趨勢
            var needsMonitoring = error.Category switch
            {
                ErrorCategory.System => true,
                ErrorCategory.Service => true,
                ErrorCategory.Network => true,
                _ => false
            };

            if (needsMonitoring)
            {
                error.ErrorContext["MonitoringEnabled"] = true;
                error.ErrorContext["MonitoringSetupAt"] = DateTime.UtcNow;
                
                _logger.LogDebug("已為錯誤類型 {Category} 設定監控，錯誤ID: {ErrorId}",
                    error.Category, error.ErrorId);
            }
        }

        /// <summary>
        /// 檢查錯誤是否可以忽略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否可以忽略</returns>
        private static bool IsIgnorable(ProcessingError error)
        {
            // 嚴重錯誤不可忽略
            if (error.Severity >= ErrorSeverity.Error)
                return false;

            // 根據錯誤類型判斷
            return error.Category switch
            {
                ErrorCategory.Validation => false,      // 驗證錯誤通常不可忽略
                ErrorCategory.Authorization => false,   // 授權錯誤不可忽略
                ErrorCategory.Authentication => false,  // 認證錯誤不可忽略  
                ErrorCategory.Processing => true,       // 業務邏輯錯誤可能可以忽略
                ErrorCategory.System => true,           // 輕微系統錯誤可能可以忽略
                ErrorCategory.Service => true,          // 輕微服務錯誤可能可以忽略
                ErrorCategory.Network => true,          // 網路錯誤可能可以忽略
                ErrorCategory.Storage => false,         // 儲存錯誤不可忽略
                _ => false
            };
        }

        /// <summary>
        /// 記錄忽略決定
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private static void RecordIgnoreDecision(ProcessingError error)
        {
            error.ErrorContext["IgnoredAt"] = DateTime.UtcNow;
            error.ErrorContext["IgnoreStrategy"] = "LogAndIgnore";
            error.ErrorContext["IgnoreReason"] = "錯誤影響輕微，已記錄並繼續運作";
            error.ErrorContext["IgnoreDecisionId"] = Guid.NewGuid().ToString();
        }
    }

    // 相關資料模型
    public class SafetyCheckResult
    {
        public bool IsSafeToIgnore { get; set; }
        public string Reason { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; }
    }

    public class FrequencyCheckResult
    {
        public int Count { get; set; }
        public bool IsHighFrequency { get; set; }
        public string TimeWindow { get; set; } = string.Empty;
    }

    public enum RiskLevel
    {
        /// <summary>
        /// 低風險
        /// </summary>
        Low,

        /// <summary>
        /// 中等風險
        /// </summary>
        Medium,

        /// <summary>
        /// 高風險
        /// </summary>
        High
    }
}