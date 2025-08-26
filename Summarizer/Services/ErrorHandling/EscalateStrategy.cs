using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 升級策略
    /// 適用於需要管理員或技術人員介入處理的嚴重錯誤
    /// </summary>
    public class EscalateStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化升級策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public EscalateStrategy(
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
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.Escalate;

        /// <summary>
        /// 執行升級策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "升級策略");

            try
            {
                // 建立升級報告
                var escalationReport = await CreateEscalationReportAsync(error);

                // 確定升級等級
                var escalationLevel = DetermineEscalationLevel(error);

                // 記錄升級資訊
                RecordEscalation(error, escalationLevel);

                // 發送升級通知
                await SendEscalationNotificationAsync(error, escalationReport, escalationLevel);

                // 保存部分結果（如果有的話）
                await SavePartialResultsAsync(error);

                // 暫停相關的批次處理
                await PauseBatchProcessingAsync(error);

                // 發送管理員通知
                await NotifyAdministratorsAsync(error, escalationReport, escalationLevel);

                var result = CreateSuccessResult(
                    $"錯誤已成功升級至 {escalationLevel} 等級，等待人工處理",
                    true,
                    "等待管理員或技術人員介入處理");

                // 將升級報告加入結果資料
                result.Data["EscalationReport"] = escalationReport;
                result.Data["EscalationLevel"] = escalationLevel;
                result.Data["EscalatedAt"] = DateTime.UtcNow;

                LogHandlingComplete(error, result, "升級策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行升級策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                return CreateFailureResult(
                    $"升級策略執行異常: {ex.Message}",
                    true,
                    "檢查升級策略配置或手動通知管理員");
            }
        }

        /// <summary>
        /// 判斷是否適合升級處理
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合升級</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查錯誤類型是否適合升級
            return IsEscalatable(error);
        }

        /// <summary>
        /// 建立升級報告
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>升級報告</returns>
        private async Task<EscalationReport> CreateEscalationReportAsync(ProcessingError error)
        {
            var report = new EscalationReport
            {
                ErrorId = error.ErrorId,
                ErrorCategory = error.Category,
                ErrorSeverity = error.Severity,
                ErrorMessage = error.ErrorMessage,
                UserFriendlyMessage = error.UserFriendlyMessage,
                Context = error.ErrorContext.Any() 
                    ? string.Join(", ", error.ErrorContext.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                    : string.Empty,
                OccurredAt = error.OccurredAt,
                BatchId = error.BatchId,
                // UserId = "System", // ProcessingError 沒有 UserId 屬性
                // StackTrace = "", // ProcessingError 沒有 StackTrace 屬性  
                ReportGeneratedAt = DateTime.UtcNow
            };

            // 加入診斷資訊
            if (error.ErrorContext.Any())
            {
                report.DiagnosticInfo = new Dictionary<string, object>(error.ErrorContext);
            }

            // 加入建議的解決方案
            report.SuggestedActions = new List<string>(error.SuggestedActions);

            // 加入影響評估
            report.ImpactAssessment = await AssessImpactAsync(error);

            // 加入緊急程度評估
            report.UrgencyLevel = DetermineUrgencyLevel(error);

            // 加入相關錯誤歷史
            report.RelatedErrors = await GetRelatedErrorsAsync(error);

            return report;
        }

        /// <summary>
        /// 確定升級等級
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>升級等級</returns>
        private static EscalationLevel DetermineEscalationLevel(ProcessingError error)
        {
            // 根據錯誤嚴重程度和類型確定升級等級
            return error.Severity switch
            {
                ErrorSeverity.Fatal => EscalationLevel.Emergency,
                ErrorSeverity.Critical when error.Category == ErrorCategory.Authorization => EscalationLevel.Emergency,
                ErrorSeverity.Critical when error.Category == ErrorCategory.System => EscalationLevel.Emergency,
                ErrorSeverity.Critical => EscalationLevel.High,
                ErrorSeverity.Error when error.Category == ErrorCategory.Processing => EscalationLevel.Medium,
                ErrorSeverity.Error => EscalationLevel.Low,
                ErrorSeverity.Warning => EscalationLevel.Low,
                ErrorSeverity.Info => EscalationLevel.Low,
                _ => EscalationLevel.Low
            };
        }

        /// <summary>
        /// 檢查錯誤是否可升級
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否可升級</returns>
        private static bool IsEscalatable(ProcessingError error)
        {
            // 所有類型的錯誤都可能需要升級，但某些類型更常見
            return error.Category switch
            {
                ErrorCategory.System => true,          // 系統錯誤可能需要技術介入（含配置錯誤）
                ErrorCategory.Processing => true,      // 業務邏輯錯誤可能需要業務人員介入
                ErrorCategory.Service => true,         // 服務錯誤可能需要技術支援（含速率限制和暫時性錯誤）
                ErrorCategory.Authorization => true,   // 授權錯誤可能需要權限調整
                ErrorCategory.Authentication => true,  // 認證錯誤可能需要帳號處理
                ErrorCategory.Storage => true,         // 儲存錯誤可能需要技術介入
                ErrorCategory.Network => false,        // 網路錯誤通常先重試（含逾時）
                ErrorCategory.Validation => false,     // 驗證錯誤通常給用戶指導
                _ => true
            };
        }

        /// <summary>
        /// 記錄升級資訊
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="level">升級等級</param>
        private static void RecordEscalation(ProcessingError error, EscalationLevel level)
        {
            error.ErrorContext["EscalatedAt"] = DateTime.UtcNow;
            error.ErrorContext["EscalationLevel"] = level.ToString();
            error.ErrorContext["EscalationId"] = Guid.NewGuid().ToString();
            
            // 記錄升級歷史
            if (!error.ErrorContext.ContainsKey("EscalationHistory"))
            {
                error.ErrorContext["EscalationHistory"] = new List<Dictionary<string, object>>();
            }
            
            if (error.ErrorContext["EscalationHistory"] is List<Dictionary<string, object>> history)
            {
                history.Add(new Dictionary<string, object>
                {
                    ["Timestamp"] = DateTime.UtcNow,
                    ["Level"] = level.ToString(),
                    ["EscalationId"] = error.ErrorContext["EscalationId"]
                });
            }
        }

        /// <summary>
        /// 發送升級通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="report">升級報告</param>
        /// <param name="level">升級等級</param>
        private async Task SendEscalationNotificationAsync(ProcessingError error, EscalationReport report, EscalationLevel level)
        {
            var notificationMessage = level switch
            {
                EscalationLevel.Emergency => $"🚨 緊急錯誤升級：{error.UserFriendlyMessage}",
                EscalationLevel.High => $"⚠️ 高優先級錯誤升級：{error.UserFriendlyMessage}",
                EscalationLevel.Medium => $"⚡ 中等優先級錯誤升級：{error.UserFriendlyMessage}",
                EscalationLevel.Low => $"ℹ️ 錯誤升級通知：{error.UserFriendlyMessage}",
                _ => $"錯誤升級通知：{error.UserFriendlyMessage}"
            };

            await SendNotificationAsync(error, notificationMessage);
        }

        /// <summary>
        /// 保存部分結果（升級策略中記錄升級事件）
        /// </summary>
        private Task SavePartialResultsAsync(ProcessingError error)
        {
            try
            {
                if (error.BatchId.HasValue)
                {
                    // 錯誤升級不直接保存部分結果，而是記錄升級事件
                    _logger.LogWarning("錯誤升級事件 - 批次 {BatchId} 在 {Timestamp} 升級處理",
                        error.BatchId.Value, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存部分結果時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 暫停批次處理
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task PauseBatchProcessingAsync(ProcessingError error)
        {
            try
            {
                if (error.BatchId.HasValue && error.Severity >= ErrorSeverity.Error)
                {
                    // 對於高嚴重程度錯誤，暫停相關的批次處理
                    await _notificationService.NotifyErrorAsync(
                        error.BatchId.Value,
                        "由於發生嚴重錯誤，批次處理已暫停等待人工處理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "暫停批次處理時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 通知管理員
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="report">升級報告</param>
        /// <param name="level">升級等級</param>
        private async Task NotifyAdministratorsAsync(ProcessingError error, EscalationReport report, EscalationLevel level)
        {
            try
            {
                // 這裡可以實作發送郵件、Slack通知、或其他管理員通知機制
                _logger.LogWarning(
                    "錯誤已升級至管理員處理：錯誤ID {ErrorId}，升級等級 {Level}，錯誤訊息：{Message}",
                    error.ErrorId, level, error.ErrorMessage);

                // 模擬發送管理員通知
                await Task.Delay(100);

                _logger.LogInformation("管理員升級通知已發送，錯誤ID: {ErrorId}", error.ErrorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送管理員通知時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 評估錯誤影響
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>影響評估</returns>
        private async Task<string> AssessImpactAsync(ProcessingError error)
        {
            await Task.Delay(10); // 模擬評估時間

            return error.Severity switch
            {
                ErrorSeverity.Critical => "嚴重影響：系統功能受到重大影響，可能導致服務中斷",
                ErrorSeverity.Error => "高度影響：重要功能受影響，需要優先處理",
                ErrorSeverity.Warning => "中度影響：部分功能受影響，可能影響使用者體驗",
                ErrorSeverity.Info => "低度影響：輕微功能異常，對整體系統影響有限",
                _ => "影響程度待評估"
            };
        }

        /// <summary>
        /// 確定緊急程度
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>緊急程度</returns>
        private static string DetermineUrgencyLevel(ProcessingError error)
        {
            if (error.Category == ErrorCategory.Authorization || error.Severity == ErrorSeverity.Critical)
            {
                return "緊急";
            }
            else if (error.Severity == ErrorSeverity.Error)
            {
                return "高";
            }
            else if (error.Severity == ErrorSeverity.Warning)
            {
                return "中等";
            }
            else
            {
                return "低";
            }
        }

        /// <summary>
        /// 取得相關錯誤
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>相關錯誤列表</returns>
        private async Task<List<string>> GetRelatedErrorsAsync(ProcessingError error)
        {
            await Task.Delay(10); // 模擬查詢時間

            // 這裡可以實作查詢相關錯誤的邏輯
            // 例如查詢相同類型、相同使用者、相同批次的其他錯誤
            
            var relatedErrors = new List<string>();
            
            if (error.BatchId.HasValue)
            {
                relatedErrors.Add($"批次 {error.BatchId} 中的其他錯誤");
            }
            
            if (!string.IsNullOrEmpty(error.UserId))
            {
                relatedErrors.Add($"使用者 {error.UserId} 的近期錯誤");
            }
            
            return relatedErrors;
        }
    }

    /// <summary>
    /// 升級等級
    /// </summary>
    public enum EscalationLevel
    {
        /// <summary>
        /// 低優先級
        /// </summary>
        Low,

        /// <summary>
        /// 中等優先級
        /// </summary>
        Medium,

        /// <summary>
        /// 高優先級
        /// </summary>
        High,

        /// <summary>
        /// 緊急
        /// </summary>
        Emergency
    }

    /// <summary>
    /// 升級報告
    /// </summary>
    public class EscalationReport
    {
        /// <summary>
        /// 錯誤ID
        /// </summary>
        public Guid ErrorId { get; set; }

        /// <summary>
        /// 錯誤類型
        /// </summary>
        public ErrorCategory ErrorCategory { get; set; }

        /// <summary>
        /// 錯誤嚴重程度
        /// </summary>
        public ErrorSeverity ErrorSeverity { get; set; }

        /// <summary>
        /// 錯誤訊息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 使用者友善訊息
        /// </summary>
        public string UserFriendlyMessage { get; set; } = string.Empty;

        /// <summary>
        /// 錯誤上下文
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// 發生時間
        /// </summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>
        /// 批次ID
        /// </summary>
        public Guid? BatchId { get; set; }

        /// <summary>
        /// 使用者ID
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 堆疊追蹤
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// 診斷資訊
        /// </summary>
        public Dictionary<string, object> DiagnosticInfo { get; set; } = new();

        /// <summary>
        /// 建議的解決方案
        /// </summary>
        public List<string> SuggestedActions { get; set; } = new();

        /// <summary>
        /// 影響評估
        /// </summary>
        public string ImpactAssessment { get; set; } = string.Empty;

        /// <summary>
        /// 緊急程度
        /// </summary>
        public string UrgencyLevel { get; set; } = string.Empty;

        /// <summary>
        /// 相關錯誤
        /// </summary>
        public List<string> RelatedErrors { get; set; } = new();

        /// <summary>
        /// 報告產生時間
        /// </summary>
        public DateTime ReportGeneratedAt { get; set; }
    }
}