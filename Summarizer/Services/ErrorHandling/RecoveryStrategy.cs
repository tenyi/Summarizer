using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 恢復策略
    /// 適用於系統可以自動恢復到正常狀態的錯誤情況
    /// 包含資源清理、狀態重設、部分結果保存等恢復操作
    /// </summary>
    public class RecoveryStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化恢復策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public RecoveryStrategy(
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
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.Recovery;

        /// <summary>
        /// 執行恢復策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "恢復策略");

            try
            {
                // 檢查是否已取消
                if (IsCancellationRequested(error))
                {
                    return CreateFailureResult("操作已被取消，無法執行恢復", false);
                }

                // 評估恢復可行性
                var recoveryAssessment = await AssessRecoveryFeasibilityAsync(error);
                if (!recoveryAssessment.IsRecoverable)
                {
                    return CreateFailureResult(
                        $"系統評估此錯誤無法自動恢復：{recoveryAssessment.Reason}",
                        true,
                        "建議使用其他錯誤處理策略");
                }

                // 建立恢復計劃
                var recoveryPlan = await CreateRecoveryPlanAsync(error);

                // 發送恢復開始通知
                await SendNotificationAsync(error, "正在執行系統自動恢復程序...");

                // 執行恢復步驟
                var recoveryResult = await ExecuteRecoveryPlanAsync(error, recoveryPlan);

                // 記錄恢復結果
                RecordRecoveryAttempt(error, recoveryResult);

                var result = recoveryResult.Success 
                    ? CreateSuccessResult(
                        $"系統已成功恢復正常狀態：{recoveryResult.Message}",
                        false,
                        "系統已恢復，可以繼續正常操作")
                    : CreateFailureResult(
                        $"自動恢復失敗：{recoveryResult.Message}",
                        true,
                        "需要使用其他錯誤處理策略");

                // 將恢復資訊加入結果資料
                result.Data["RecoveryPlan"] = recoveryPlan;
                result.Data["RecoveryResult"] = recoveryResult;
                result.Data["RecoveryAttemptedAt"] = DateTime.UtcNow;

                LogHandlingComplete(error, result, "恢復策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行恢復策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                return CreateFailureResult(
                    $"恢復策略執行異常: {ex.Message}",
                    true,
                    "系統恢復失敗，建議升級處理或手動介入");
            }
        }

        /// <summary>
        /// 判斷是否適合恢復處理
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合恢復</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查錯誤類型是否適合自動恢復
            return IsRecoverable(error);
        }

        /// <summary>
        /// 評估恢復可行性
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>恢復可行性評估</returns>
        private async Task<RecoveryAssessment> AssessRecoveryFeasibilityAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬評估時間

            var assessment = new RecoveryAssessment();

            // 基於錯誤類型評估
            switch (error.Category)
            {
                case ErrorCategory.Service:
                    assessment.IsRecoverable = true;
                    assessment.Confidence = 0.9;
                    assessment.Reason = "暫時性錯誤通常可以透過清理和重設恢復";
                    break;

                case ErrorCategory.Network:
                    assessment.IsRecoverable = error.Severity != ErrorSeverity.Critical;
                    assessment.Confidence = 0.7;
                    assessment.Reason = assessment.IsRecoverable ? "網路錯誤可嘗試重連恢復" : "網路錯誤過於嚴重";
                    break;

                case ErrorCategory.System:
                    assessment.IsRecoverable = error.Severity == ErrorSeverity.Info || error.Severity == ErrorSeverity.Warning;
                    assessment.Confidence = 0.6;
                    assessment.Reason = assessment.IsRecoverable ? "輕微系統錯誤可嘗試恢復" : "系統錯誤過於嚴重";
                    break;

                case ErrorCategory.Processing:
                    assessment.IsRecoverable = true;
                    assessment.Confidence = 0.8;
                    assessment.Reason = "處理錯誤可透過重新處理恢復";
                    break;

                default:
                    assessment.IsRecoverable = false;
                    assessment.Confidence = 0.0;
                    assessment.Reason = $"錯誤類型 {error.Category} 不適合自動恢復";
                    break;
            }

            // 檢查系統資源狀態
            if (assessment.IsRecoverable)
            {
                var systemHealth = await CheckSystemHealthAsync();
                if (!systemHealth.IsHealthy)
                {
                    assessment.IsRecoverable = false;
                    assessment.Reason = $"系統健康狀態不佳：{systemHealth.Issue}";
                }
            }

            return assessment;
        }

        /// <summary>
        /// 建立恢復計劃
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>恢復計劃</returns>
        private async Task<RecoveryPlan> CreateRecoveryPlanAsync(ProcessingError error)
        {
            await Task.Delay(50); // 模擬計劃建立時間

            var plan = new RecoveryPlan
            {
                ErrorId = error.ErrorId,
                PlanCreatedAt = DateTime.UtcNow,
                Steps = new List<RecoveryStep>()
            };

            // 根據錯誤類型建立恢復步驟
            switch (error.Category)
            {
                case ErrorCategory.Network:
                    plan.Steps.AddRange(CreateTemporaryErrorRecoverySteps(error));
                    break;

                case ErrorCategory.Service:
                    plan.Steps.AddRange(CreateServiceErrorRecoverySteps(error));
                    break;

                case ErrorCategory.System:
                    plan.Steps.AddRange(CreateSystemErrorRecoverySteps(error));
                    break;

                case ErrorCategory.Processing:
                    plan.Steps.AddRange(CreateNetworkErrorRecoverySteps(error));
                    break;

                default:
                    plan.Steps.Add(new RecoveryStep
                    {
                        StepNumber = 1,
                        Description = "執行一般清理程序",
                        Action = RecoveryAction.GeneralCleanup,
                        EstimatedDurationMs = 1000
                    });
                    break;
            }

            // 加入通用的收尾步驟
            plan.Steps.Add(new RecoveryStep
            {
                StepNumber = plan.Steps.Count + 1,
                Description = "驗證系統恢復狀態",
                Action = RecoveryAction.VerifyRecovery,
                EstimatedDurationMs = 500
            });

            return plan;
        }

        /// <summary>
        /// 執行恢復計劃
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="plan">恢復計劃</param>
        /// <returns>恢復結果</returns>
        private async Task<RecoveryResult> ExecuteRecoveryPlanAsync(ProcessingError error, RecoveryPlan plan)
        {
            var result = new RecoveryResult
            {
                ErrorId = error.ErrorId,
                StartedAt = DateTime.UtcNow,
                StepResults = new List<StepResult>()
            };

            try
            {
                foreach (var step in plan.Steps)
                {
                    // 檢查取消狀態
                    if (IsCancellationRequested(error))
                    {
                        result.Success = false;
                        result.Message = "恢復過程中操作被取消";
                        break;
                    }

                    // 執行恢復步驟
                    var stepResult = await ExecuteRecoveryStepAsync(error, step);
                    result.StepResults.Add(stepResult);

                    // 如果步驟失敗，停止恢復
                    if (!stepResult.Success)
                    {
                        result.Success = false;
                        result.Message = $"恢復步驟 {step.StepNumber} 失敗：{stepResult.ErrorMessage}";
                        break;
                    }

                    // 發送進度通知
                    await SendNotificationAsync(error, 
                        $"恢復進度：完成步驟 {step.StepNumber}/{plan.Steps.Count} - {step.Description}");
                }

                // 如果所有步驟都成功
                if (result.StepResults.All(sr => sr.Success))
                {
                    result.Success = true;
                    result.Message = "系統已成功恢復到正常狀態";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"恢復計劃執行異常：{ex.Message}";
                _logger.LogError(ex, "執行恢復計劃時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
            finally
            {
                result.CompletedAt = DateTime.UtcNow;
                result.TotalDurationMs = (int)(result.CompletedAt - result.StartedAt).TotalMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 執行單個恢復步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="step">恢復步驟</param>
        /// <returns>步驟執行結果</returns>
        private async Task<StepResult> ExecuteRecoveryStepAsync(ProcessingError error, RecoveryStep step)
        {
            var stepResult = new StepResult
            {
                StepNumber = step.StepNumber,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 根據恢復動作執行相應操作
                switch (step.Action)
                {
                    case RecoveryAction.SavePartialResults:
                        await SavePartialResultsAsync(error);
                        break;

                    case RecoveryAction.CleanupResources:
                        await CleanupResourcesAsync(error);
                        break;

                    case RecoveryAction.ResetState:
                        await ResetSystemStateAsync(error);
                        break;

                    case RecoveryAction.RestartServices:
                        await RestartServicesAsync(error);
                        break;

                    case RecoveryAction.ReestablishConnections:
                        await ReestablishConnectionsAsync(error);
                        break;

                    case RecoveryAction.VerifyRecovery:
                        await VerifyRecoveryAsync(error);
                        break;

                    case RecoveryAction.GeneralCleanup:
                        await PerformGeneralCleanupAsync(error);
                        break;

                    default:
                        throw new NotSupportedException($"不支援的恢復動作：{step.Action}");
                }

                stepResult.Success = true;
                stepResult.Message = $"步驟 {step.StepNumber} 執行成功";
            }
            catch (Exception ex)
            {
                stepResult.Success = false;
                stepResult.ErrorMessage = ex.Message;
                _logger.LogWarning(ex, "恢復步驟 {StepNumber} 執行失敗：{StepDescription}", 
                    step.StepNumber, step.Description);
            }
            finally
            {
                stepResult.CompletedAt = DateTime.UtcNow;
                stepResult.DurationMs = (int)(stepResult.CompletedAt - stepResult.StartedAt).TotalMilliseconds;
            }

            return stepResult;
        }

        /// <summary>
        /// 檢查錯誤是否可恢復
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否可恢復</returns>
        private static bool IsRecoverable(ProcessingError error)
        {
            // 根據錯誤類型判斷是否可恢復
            return error.Category switch
            {
                ErrorCategory.Service => true,          // 服務錯誤可嘗試恢復（含暫時性錯誤）
                ErrorCategory.System => true,           // 系統錯誤可能可恢復（含配置錯誤）
                ErrorCategory.Network => true,          // 網路錯誤可恢復（含逾時）
                ErrorCategory.Processing => true,       // 處理錯誤可能可恢復
                ErrorCategory.Storage => true,          // 儲存錯誤可嘗試恢復
                ErrorCategory.Validation => false,      // 驗證錯誤需要修正輸入
                ErrorCategory.Authorization => false,   // 授權錯誤需要權限調整
                ErrorCategory.Authentication => false,  // 認證錯誤需要重新認證
                _ => false
            };
        }

        // 建立不同錯誤類型的恢復步驟
        private static List<RecoveryStep> CreateTemporaryErrorRecoverySteps(ProcessingError error)
        {
            return new List<RecoveryStep>
            {
                new() { StepNumber = 1, Description = "保存部分結果", Action = RecoveryAction.SavePartialResults, EstimatedDurationMs = 1000 },
                new() { StepNumber = 2, Description = "清理暫時資源", Action = RecoveryAction.CleanupResources, EstimatedDurationMs = 500 },
                new() { StepNumber = 3, Description = "重設系統狀態", Action = RecoveryAction.ResetState, EstimatedDurationMs = 800 }
            };
        }

        private static List<RecoveryStep> CreateServiceErrorRecoverySteps(ProcessingError error)
        {
            return new List<RecoveryStep>
            {
                new() { StepNumber = 1, Description = "保存當前進度", Action = RecoveryAction.SavePartialResults, EstimatedDurationMs = 1200 },
                new() { StepNumber = 2, Description = "重啟相關服務", Action = RecoveryAction.RestartServices, EstimatedDurationMs = 3000 },
                new() { StepNumber = 3, Description = "清理服務資源", Action = RecoveryAction.CleanupResources, EstimatedDurationMs = 800 }
            };
        }

        private static List<RecoveryStep> CreateSystemErrorRecoverySteps(ProcessingError error)
        {
            return new List<RecoveryStep>
            {
                new() { StepNumber = 1, Description = "保存系統狀態", Action = RecoveryAction.SavePartialResults, EstimatedDurationMs = 1500 },
                new() { StepNumber = 2, Description = "清理系統資源", Action = RecoveryAction.CleanupResources, EstimatedDurationMs = 1000 },
                new() { StepNumber = 3, Description = "重設系統組件", Action = RecoveryAction.ResetState, EstimatedDurationMs = 2000 }
            };
        }

        private static List<RecoveryStep> CreateNetworkErrorRecoverySteps(ProcessingError error)
        {
            return new List<RecoveryStep>
            {
                new() { StepNumber = 1, Description = "重新建立網路連接", Action = RecoveryAction.ReestablishConnections, EstimatedDurationMs = 2000 },
                new() { StepNumber = 2, Description = "清理連接資源", Action = RecoveryAction.CleanupResources, EstimatedDurationMs = 500 }
            };
        }

        // 恢復操作的實際實作
        private async Task SavePartialResultsAsync(ProcessingError error)
        {
            if (error.BatchId.HasValue)
            {
                // 恢復策略記錄恢復嘗試，實際的部分結果保存由取消服務處理
                _logger.LogInformation("恢復策略執行 - 批次 {BatchId} 在 {Timestamp} 嘗試恢復",
                    error.BatchId.Value, DateTime.UtcNow);
            }
        }

        private async Task CleanupResourcesAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬清理時間
            // 實際實作：清理記憶體、關閉檔案控制代碼、釋放連接等
        }

        private async Task ResetSystemStateAsync(ProcessingError error)
        {
            await Task.Delay(200); // 模擬重設時間
            // 實際實作：重設相關系統狀態、清除暫存資料等
        }

        private async Task RestartServicesAsync(ProcessingError error)
        {
            await Task.Delay(500); // 模擬服務重啟時間
            // 實際實作：重啟相關服務或組件
        }

        private async Task ReestablishConnectionsAsync(ProcessingError error)
        {
            await Task.Delay(300); // 模擬重新連接時間
            // 實際實作：重新建立資料庫連接、網路連接等
        }

        private async Task VerifyRecoveryAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬驗證時間
            // 實際實作：驗證系統是否已恢復正常
        }

        private async Task PerformGeneralCleanupAsync(ProcessingError error)
        {
            await Task.Delay(150); // 模擬一般清理時間
            // 實際實作：執行一般性的清理操作
        }

        private async Task<SystemHealth> CheckSystemHealthAsync()
        {
            await Task.Delay(50); // 模擬健康檢查時間
            
            // 實際實作：檢查系統健康狀態
            return new SystemHealth { IsHealthy = true, Issue = null };
        }

        private static void RecordRecoveryAttempt(ProcessingError error, RecoveryResult result)
        {
            error.ErrorContext["RecoveryAttemptedAt"] = DateTime.UtcNow;
            error.ErrorContext["RecoverySuccess"] = result.Success;
            error.ErrorContext["RecoveryMessage"] = result.Message;
            error.ErrorContext["RecoveryDurationMs"] = result.TotalDurationMs;
        }
    }

    // 恢復相關的資料模型
    public class RecoveryAssessment
    {
        public bool IsRecoverable { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class RecoveryPlan
    {
        public Guid ErrorId { get; set; }
        public DateTime PlanCreatedAt { get; set; }
        public List<RecoveryStep> Steps { get; set; } = new();
    }

    public class RecoveryStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public RecoveryAction Action { get; set; }
        public int EstimatedDurationMs { get; set; }
    }

    public class RecoveryResult
    {
        public Guid ErrorId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int TotalDurationMs { get; set; }
        public List<StepResult> StepResults { get; set; } = new();
    }

    public class StepResult
    {
        public int StepNumber { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int DurationMs { get; set; }
    }

    public class SystemHealth
    {
        public bool IsHealthy { get; set; }
        public string? Issue { get; set; }
    }

    public enum RecoveryAction
    {
        SavePartialResults,
        CleanupResources,
        ResetState,
        RestartServices,
        ReestablishConnections,
        VerifyRecovery,
        GeneralCleanup
    }
}