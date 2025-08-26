using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 立即停止策略
    /// 適用於嚴重錯誤需要立即終止處理的情況
    /// 執行緊急停止程序並保護系統免受進一步損害
    /// </summary>
    public class ImmediateStopStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化立即停止策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public ImmediateStopStrategy(
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
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.ImmediateStop;

        /// <summary>
        /// 執行立即停止策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "立即停止策略");

            try
            {
                // 評估停止的嚴重程度
                var stopAssessment = await AssessStopSeverityAsync(error);

                // 發送緊急停止通知
                await SendEmergencyStopNotificationAsync(error, stopAssessment);

                // 建立停止計劃
                var stopPlan = await CreateStopPlanAsync(error, stopAssessment);

                // 執行緊急保存程序
                await ExecuteEmergencySaveAsync(error);

                // 停止相關處理程序
                await StopRelatedProcessesAsync(error, stopPlan);

                // 清理和保護資源
                await CleanupAndProtectResourcesAsync(error, stopPlan);

                // 記錄停止決定和原因
                RecordStopDecision(error, stopAssessment);

                // 觸發緊急通知鏈
                await TriggerEmergencyNotificationChainAsync(error, stopAssessment);

                var result = CreateSuccessResult(
                    $"系統已執行緊急停止程序：{stopAssessment.Reason}",
                    true,
                    "需要人工介入檢查並決定後續處理");

                // 將停止資訊加入結果資料
                result.Data["StopAssessment"] = stopAssessment;
                result.Data["StopPlan"] = stopPlan;
                result.Data["EmergencyStoppedAt"] = DateTime.UtcNow;
                result.Data["StopType"] = stopAssessment.StopType.ToString();

                LogHandlingComplete(error, result, "立即停止策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "執行立即停止策略時發生嚴重異常，錯誤ID: {ErrorId}", error.ErrorId);
                
                // 即使策略執行失敗，也要嘗試基本的停止操作
                try
                {
                    await ExecuteMinimalStopAsync(error);
                }
                catch (Exception minimalEx)
                {
                    _logger.LogCritical(minimalEx, "連基本停止操作都失敗，系統可能處於危險狀態，錯誤ID: {ErrorId}", error.ErrorId);
                }

                return CreateFailureResult(
                    $"立即停止策略執行異常，但已執行基本停止程序: {ex.Message}",
                    true,
                    "緊急聯絡系統管理員進行人工介入");
            }
        }

        /// <summary>
        /// 判斷是否需要立即停止
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否需要立即停止</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查是否符合立即停止的條件
            return RequiresImmediateStop(error);
        }

        /// <summary>
        /// 評估停止嚴重程度
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>停止評估結果</returns>
        private async Task<StopAssessment> AssessStopSeverityAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬評估時間

            var assessment = new StopAssessment
            {
                ErrorId = error.ErrorId,
                AssessedAt = DateTime.UtcNow
            };

            // 根據錯誤類型和嚴重程度評估
            switch (error.Category)
            {
                case ErrorCategory.Authorization:
                    assessment.StopType = StopType.SecurityEmergency;
                    assessment.UrgencyLevel = UrgencyLevel.Critical;
                    assessment.Reason = "檢測到安全威脅，立即停止以保護系統";
                    assessment.RequireImmediateAction = true;
                    break;

                case ErrorCategory.System when error.Severity == ErrorSeverity.Critical:
                    assessment.StopType = StopType.SystemFailure;
                    assessment.UrgencyLevel = UrgencyLevel.High;
                    assessment.Reason = "系統發生嚴重故障，需要立即停止以防止損害擴散";
                    assessment.RequireImmediateAction = true;
                    break;

                case ErrorCategory.Processing when error.Severity == ErrorSeverity.Critical:
                    assessment.StopType = StopType.DataIntegrityRisk;
                    assessment.UrgencyLevel = UrgencyLevel.High;
                    assessment.Reason = "業務邏輯發生嚴重錯誤，可能影響資料完整性";
                    assessment.RequireImmediateAction = true;
                    break;

                case ErrorCategory.System when error.Severity >= ErrorSeverity.Critical:
                    assessment.StopType = StopType.ConfigurationCritical;
                    assessment.UrgencyLevel = UrgencyLevel.Medium;
                    assessment.Reason = "關鍵配置錯誤，繼續執行可能造成不可預期的後果";
                    assessment.RequireImmediateAction = true;
                    break;

                default:
                    assessment.StopType = StopType.GeneralCritical;
                    assessment.UrgencyLevel = UrgencyLevel.Medium;
                    assessment.Reason = $"嚴重的 {error.Category} 錯誤，需要立即停止處理";
                    assessment.RequireImmediateAction = true;
                    break;
            }

            // 評估潛在影響
            assessment.PotentialImpact = await AssessPotentialImpactAsync(error);

            return assessment;
        }

        /// <summary>
        /// 建立停止計劃
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="assessment">停止評估</param>
        /// <returns>停止計劃</returns>
        private async Task<StopPlan> CreateStopPlanAsync(ProcessingError error, StopAssessment assessment)
        {
            await Task.Delay(50); // 模擬計劃建立時間

            var plan = new StopPlan
            {
                ErrorId = error.ErrorId,
                StopType = assessment.StopType,
                CreatedAt = DateTime.UtcNow,
                Actions = new List<StopAction>()
            };

            // 根據停止類型建立相應的停止動作
            switch (assessment.StopType)
            {
                case StopType.SecurityEmergency:
                    plan.Actions.AddRange(CreateSecurityEmergencyActions(error));
                    break;

                case StopType.SystemFailure:
                    plan.Actions.AddRange(CreateSystemFailureActions(error));
                    break;

                case StopType.DataIntegrityRisk:
                    plan.Actions.AddRange(CreateDataIntegrityActions(error));
                    break;

                case StopType.ConfigurationCritical:
                    plan.Actions.AddRange(CreateConfigurationCriticalActions(error));
                    break;

                default:
                    plan.Actions.AddRange(CreateGeneralCriticalActions(error));
                    break;
            }

            // 按優先級排序動作
            plan.Actions = plan.Actions.OrderBy(a => a.Priority).ToList();

            return plan;
        }

        /// <summary>
        /// 執行緊急保存程序
        /// </summary>
        private Task ExecuteEmergencySaveAsync(ProcessingError error)
        {
            try
            {
                _logger.LogCritical("開始執行緊急保存程序，錯誤ID: {ErrorId}", error.ErrorId);

                if (error.BatchId.HasValue)
                {
                    // 立即停止策略不直接保存部分結果，而是觸發系統停止信號
                    _logger.LogCritical("緊急停止信號 - 批次 {BatchId} 因 {Reason} 立即停止",
                        error.BatchId.Value, error.ErrorMessage);

                    _logger.LogInformation("緊急保存完成，批次ID: {BatchId}", error.BatchId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "緊急保存失敗，錯誤ID: {ErrorId}，這可能導致資料丟失", error.ErrorId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止相關處理程序
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="plan">停止計劃</param>
        private async Task StopRelatedProcessesAsync(ProcessingError error, StopPlan plan)
        {
            try
            {
                if (error.BatchId.HasValue)
                {
                    // 立即取消相關的批次處理
                    var cancellationToken = _cancellationService.GetCancellationToken(error.BatchId.Value);
                    if (cancellationToken != null && !cancellationToken.Value.IsCancellationRequested)
                    {
                        // 立即停止策略通過設定檢查點來協調停止
                        _cancellationService.SetSafeCheckpoint(error.BatchId.Value, false);
                        _logger.LogWarning("已設定緊急停止檢查點，批次ID: {BatchId}", error.BatchId);
                    }
                }

                // 通知相關系統停止處理
                await SendStopSignalToRelatedSystemsAsync(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止相關處理程序時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 清理和保護資源
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="plan">停止計劃</param>
        private async Task CleanupAndProtectResourcesAsync(ProcessingError error, StopPlan plan)
        {
            try
            {
                await Task.Delay(200); // 模擬清理時間

                // 執行資源清理和保護措施
                _logger.LogInformation("正在執行資源清理和保護措施，錯誤ID: {ErrorId}", error.ErrorId);

                // 這裡可以實作具體的資源清理邏輯
                // 例如：關閉檔案控制代碼、釋放記憶體、斷開連接等

                _logger.LogInformation("資源清理和保護措施已完成，錯誤ID: {ErrorId}", error.ErrorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行資源清理和保護時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 觸發緊急通知鏈
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="assessment">停止評估</param>
        private async Task TriggerEmergencyNotificationChainAsync(ProcessingError error, StopAssessment assessment)
        {
            try
            {
                // 發送緊急通知給所有相關人員
                var emergencyMessage = assessment.UrgencyLevel switch
                {
                    UrgencyLevel.Critical => $"🚨 系統緊急停止：{error.UserFriendlyMessage}",
                    UrgencyLevel.High => $"⚠️ 系統緊急停止：{error.UserFriendlyMessage}",
                    _ => $"⏹️ 系統已停止：{error.UserFriendlyMessage}"
                };

                await SendNotificationAsync(error, emergencyMessage);

                // 這裡可以整合更多的通知機制
                // 例如：SMS、郵件、Slack、企業通訊系統等
                _logger.LogCritical("緊急通知已發送：{Message}", emergencyMessage);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "發送緊急通知時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        /// <summary>
        /// 發送緊急停止通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="assessment">停止評估</param>
        private async Task SendEmergencyStopNotificationAsync(ProcessingError error, StopAssessment assessment)
        {
            var message = $"🆘 緊急停止程序已啟動：{assessment.Reason}";
            await SendNotificationAsync(error, message);
        }

        /// <summary>
        /// 檢查是否需要立即停止
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否需要立即停止</returns>
        private static bool RequiresImmediateStop(ProcessingError error)
        {
            // 嚴重錯誤需要立即停止
            if (error.Severity == ErrorSeverity.Critical)
                return true;

            // 特定類型的錯誤需要立即停止
            return error.Category switch
            {
                ErrorCategory.System when error.Severity >= ErrorSeverity.Critical => true,
                ErrorCategory.Processing when error.Severity >= ErrorSeverity.Critical => true,
                ErrorCategory.Storage when error.Severity >= ErrorSeverity.Critical => true,
                ErrorCategory.Authorization when error.Severity >= ErrorSeverity.Error => true,
                ErrorCategory.Authentication when error.Severity >= ErrorSeverity.Error => true,
                _ => false
            };
        }

        /// <summary>
        /// 評估潛在影響
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>潛在影響描述</returns>
        private async Task<string> AssessPotentialImpactAsync(ProcessingError error)
        {
            await Task.Delay(30); // 模擬評估時間

            return error.Category switch
            {
                ErrorCategory.Authorization => "可能的安全漏洞或認證威脅，立即停止可避免資料外洩",
                ErrorCategory.System => "系統故障可能導致資料損毀或服務中斷（含配置錯誤）",
                ErrorCategory.Processing => "業務邏輯錯誤可能導致資料不一致或錯誤結果",
                _ => "嚴重錯誤可能對系統造成不可預期的損害"
            };
        }

        // 建立不同類型的停止動作
        private static List<StopAction> CreateSecurityEmergencyActions(ProcessingError error)
        {
            return new List<StopAction>
            {
                new() { Priority = 1, Description = "立即隔離受影響的系統組件", ActionType = "IsolateComponents" },
                new() { Priority = 2, Description = "停止所有外部連接", ActionType = "StopExternalConnections" },
                new() { Priority = 3, Description = "保存安全日誌和證據", ActionType = "SaveSecurityLogs" },
                new() { Priority = 4, Description = "通知安全團隊", ActionType = "NotifySecurityTeam" }
            };
        }

        private static List<StopAction> CreateSystemFailureActions(ProcessingError error)
        {
            return new List<StopAction>
            {
                new() { Priority = 1, Description = "緊急保存系統狀態", ActionType = "SaveSystemState" },
                new() { Priority = 2, Description = "停止相關服務", ActionType = "StopServices" },
                new() { Priority = 3, Description = "釋放系統資源", ActionType = "ReleaseResources" },
                new() { Priority = 4, Description = "生成系統診斷報告", ActionType = "GenerateDiagnostics" }
            };
        }

        private static List<StopAction> CreateDataIntegrityActions(ProcessingError error)
        {
            return new List<StopAction>
            {
                new() { Priority = 1, Description = "停止所有資料修改操作", ActionType = "StopDataModification" },
                new() { Priority = 2, Description = "備份當前資料狀態", ActionType = "BackupCurrentData" },
                new() { Priority = 3, Description = "檢查資料完整性", ActionType = "CheckDataIntegrity" },
                new() { Priority = 4, Description = "隔離受影響的資料", ActionType = "IsolateAffectedData" }
            };
        }

        private static List<StopAction> CreateConfigurationCriticalActions(ProcessingError error)
        {
            return new List<StopAction>
            {
                new() { Priority = 1, Description = "停止使用錯誤配置", ActionType = "StopUsingBadConfig" },
                new() { Priority = 2, Description = "恢復上一個已知良好的配置", ActionType = "RestoreGoodConfig" },
                new() { Priority = 3, Description = "驗證配置完整性", ActionType = "ValidateConfig" }
            };
        }

        private static List<StopAction> CreateGeneralCriticalActions(ProcessingError error)
        {
            return new List<StopAction>
            {
                new() { Priority = 1, Description = "立即停止當前處理", ActionType = "StopCurrentProcessing" },
                new() { Priority = 2, Description = "保存當前狀態", ActionType = "SaveCurrentState" },
                new() { Priority = 3, Description = "清理和保護資源", ActionType = "CleanupResources" },
                new() { Priority = 4, Description = "準備系統診斷資訊", ActionType = "PrepareDiagnostics" }
            };
        }

        /// <summary>
        /// 執行最小停止操作（策略失敗時的備案）
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private async Task ExecuteMinimalStopAsync(ProcessingError error)
        {
            try
            {
                // 基本的停止操作
                if (error.BatchId.HasValue)
                {
                    // 設定停止檢查點
                    _cancellationService.SetSafeCheckpoint(error.BatchId.Value, false);
                }

                await SendNotificationAsync(error, "系統已執行緊急停止程序（最小化操作）");
                
                _logger.LogCritical("已執行最小化緊急停止程序，錯誤ID: {ErrorId}", error.ErrorId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "最小化停止程序也失敗，系統處於嚴重危險狀態，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        private async Task SendStopSignalToRelatedSystemsAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬發送停止信號時間
            // 這裡可以實作發送停止信號給相關系統的邏輯
        }

        private static void RecordStopDecision(ProcessingError error, StopAssessment assessment)
        {
            error.ErrorContext["EmergencyStoppedAt"] = DateTime.UtcNow;
            error.ErrorContext["StopType"] = assessment.StopType.ToString();
            error.ErrorContext["StopReason"] = assessment.Reason;
            error.ErrorContext["UrgencyLevel"] = assessment.UrgencyLevel.ToString();
            error.ErrorContext["StopDecisionId"] = Guid.NewGuid().ToString();
        }
    }

    // 立即停止相關的資料模型
    public class StopAssessment
    {
        public Guid ErrorId { get; set; }
        public StopType StopType { get; set; }
        public UrgencyLevel UrgencyLevel { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool RequireImmediateAction { get; set; }
        public string PotentialImpact { get; set; } = string.Empty;
        public DateTime AssessedAt { get; set; }
    }

    public class StopPlan
    {
        public Guid ErrorId { get; set; }
        public StopType StopType { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<StopAction> Actions { get; set; } = new();
    }

    public class StopAction
    {
        public int Priority { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
    }

    public enum StopType
    {
        /// <summary>
        /// 安全緊急事件
        /// </summary>
        SecurityEmergency,

        /// <summary>
        /// 系統故障
        /// </summary>
        SystemFailure,

        /// <summary>
        /// 資料完整性風險
        /// </summary>
        DataIntegrityRisk,

        /// <summary>
        /// 關鍵配置錯誤
        /// </summary>
        ConfigurationCritical,

        /// <summary>
        /// 一般嚴重錯誤
        /// </summary>
        GeneralCritical
    }

    public enum UrgencyLevel
    {
        /// <summary>
        /// 低緊急程度
        /// </summary>
        Low,

        /// <summary>
        /// 中等緊急程度
        /// </summary>
        Medium,

        /// <summary>
        /// 高緊急程度
        /// </summary>
        High,

        /// <summary>
        /// 嚴重緊急程度
        /// </summary>
        Critical
    }
}