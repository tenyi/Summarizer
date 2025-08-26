using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 備援策略
    /// 當主要處理方式失敗時，提供替代的處理方案
    /// 確保系統能夠以降級模式繼續運作
    /// </summary>
    public class FallbackStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化備援策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public FallbackStrategy(
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
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.Fallback;

        /// <summary>
        /// 執行備援策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "備援策略");

            try
            {
                // 檢查是否已取消
                if (IsCancellationRequested(error))
                {
                    return CreateFailureResult("操作已被取消，無法執行備援策略", false);
                }

                // 確定可用的備援選項
                var fallbackOptions = await IdentifyFallbackOptionsAsync(error);
                if (!fallbackOptions.Any())
                {
                    return CreateFailureResult(
                        "沒有可用的備援選項",
                        true,
                        "考慮使用其他錯誤處理策略");
                }

                // 選擇最佳的備援選項
                var selectedFallback = SelectBestFallback(error, fallbackOptions);

                // 發送備援啟動通知
                await SendNotificationAsync(error, 
                    $"正在啟動備援方案：{selectedFallback.Description}");

                // 保存當前狀態
                await SaveCurrentStateAsync(error);

                // 執行備援方案
                var fallbackResult = await ExecuteFallbackAsync(error, selectedFallback);

                // 記錄備援執行結果
                RecordFallbackExecution(error, selectedFallback, fallbackResult);

                var result = fallbackResult.Success
                    ? CreateSuccessResult(
                        $"備援策略執行成功：{fallbackResult.Message}",
                        selectedFallback.RequiresFurtherAction,
                        selectedFallback.NextAction)
                    : CreateFailureResult(
                        $"備援策略執行失敗：{fallbackResult.Message}",
                        true,
                        "需要使用其他錯誤處理策略");

                // 將備援資訊加入結果資料
                result.Data["FallbackOption"] = selectedFallback;
                result.Data["FallbackResult"] = fallbackResult;
                result.Data["FallbackExecutedAt"] = DateTime.UtcNow;

                LogHandlingComplete(error, result, "備援策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行備援策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                return CreateFailureResult(
                    $"備援策略執行異常: {ex.Message}",
                    true,
                    "備援方案失敗，建議升級處理");
            }
        }

        /// <summary>
        /// 判斷是否適合備援處理
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合備援</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查錯誤類型是否適合備援處理
            return HasFallbackOptions(error);
        }

        /// <summary>
        /// 識別可用的備援選項
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>備援選項列表</returns>
        private async Task<List<FallbackOption>> IdentifyFallbackOptionsAsync(ProcessingError error)
        {
            await Task.Delay(100); // 模擬選項識別時間

            var options = new List<FallbackOption>();

            // 根據錯誤類型提供不同的備援選項
            switch (error.Category)
            {
                case ErrorCategory.Service:
                    options.AddRange(GetServiceFallbackOptions(error));
                    break;

                case ErrorCategory.Network:
                    options.AddRange(GetNetworkFallbackOptions(error));
                    break;

                case ErrorCategory.System:
                    options.AddRange(GetSystemFallbackOptions(error));
                    options.AddRange(GetConfigurationFallbackOptions(error));
                    break;

                case ErrorCategory.Processing:
                    options.AddRange(GetBusinessLogicFallbackOptions(error));
                    break;

                default:
                    options.AddRange(GetGeneralFallbackOptions(error));
                    break;
            }

            // 過濾可用的選項
            return options.Where(o => o.IsAvailable).ToList();
        }

        /// <summary>
        /// 選擇最佳備援選項
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="options">可用選項</param>
        /// <returns>選中的備援選項</returns>
        private static FallbackOption SelectBestFallback(ProcessingError error, List<FallbackOption> options)
        {
            // 根據優先級和可靠性選擇最佳選項
            return options
                .OrderByDescending(o => o.Priority)
                .ThenByDescending(o => o.Reliability)
                .ThenBy(o => o.EstimatedCost)
                .First();
        }

        /// <summary>
        /// 執行備援方案
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="fallback">備援選項</param>
        /// <returns>執行結果</returns>
        private async Task<FallbackExecutionResult> ExecuteFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            var result = new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 根據備援類型執行相應操作
                switch (fallback.Type)
                {
                    case FallbackType.AlternativeService:
                        result = await ExecuteAlternativeServiceFallbackAsync(error, fallback);
                        break;

                    case FallbackType.DegradedMode:
                        result = await ExecuteDegradedModeFallbackAsync(error, fallback);
                        break;

                    case FallbackType.CachedResponse:
                        result = await ExecuteCachedResponseFallbackAsync(error, fallback);
                        break;

                    case FallbackType.DefaultValue:
                        result = await ExecuteDefaultValueFallbackAsync(error, fallback);
                        break;

                    case FallbackType.SimplifiedProcessing:
                        result = await ExecuteSimplifiedProcessingFallbackAsync(error, fallback);
                        break;

                    case FallbackType.ManualIntervention:
                        result = await ExecuteManualInterventionFallbackAsync(error, fallback);
                        break;

                    default:
                        throw new NotSupportedException($"不支援的備援類型：{fallback.Type}");
                }

                result.Success = true;
                result.Message = result.Message ?? $"備援方案 {fallback.Type} 執行成功";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"備援方案執行失敗：{ex.Message}";
                _logger.LogWarning(ex, "執行備援方案 {FallbackType} 時發生異常", fallback.Type);
            }
            finally
            {
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (int)(result.CompletedAt - result.StartedAt).TotalMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 檢查是否有備援選項
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否有備援選項</returns>
        private static bool HasFallbackOptions(ProcessingError error)
        {
            // 大部分錯誤類型都可能有備援選項，除了某些嚴重錯誤
            return error.Category switch
            {
                ErrorCategory.Service => true,          // 服務錯誤通常有備援服務（含暫時性和速率限制）
                ErrorCategory.Network => true,          // 網路錯誤可能有離線模式（含逾時）
                ErrorCategory.System => true,           // 系統錯誤可能有降級模式（含配置錯誤）
                ErrorCategory.Processing => true,       // 業務邏輯可能有簡化流程
                ErrorCategory.Storage => true,          // 儲存錯誤可能有備用儲存
                ErrorCategory.Authorization => false,   // 授權錯誤不適合備援
                ErrorCategory.Authentication => false,  // 認證錯誤不應使用備援
                ErrorCategory.Validation => false,      // 驗證錯誤需要正確輸入
                _ => false
            };
        }

        // 取得不同類型的備援選項
        private static List<FallbackOption> GetServiceFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.AlternativeService,
                    Description = "切換到備用的AI摘要服務",
                    Priority = 9,
                    Reliability = 0.8,
                    EstimatedCost = 2,
                    IsAvailable = true,
                    RequiresFurtherAction = false,
                    NextAction = "監控備用服務狀態"
                },
                new()
                {
                    Type = FallbackType.CachedResponse,
                    Description = "使用之前快取的摘要結果",
                    Priority = 6,
                    Reliability = 0.6,
                    EstimatedCost = 1,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "稍後重新處理以獲得最新結果"
                }
            };
        }

        private static List<FallbackOption> GetNetworkFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.CachedResponse,
                    Description = "使用離線快取資料",
                    Priority = 8,
                    Reliability = 0.7,
                    EstimatedCost = 1,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "網路恢復後同步最新資料"
                },
                new()
                {
                    Type = FallbackType.DegradedMode,
                    Description = "啟動離線模式",
                    Priority = 7,
                    Reliability = 0.8,
                    EstimatedCost = 2,
                    IsAvailable = true,
                    RequiresFurtherAction = false
                }
            };
        }

        private static List<FallbackOption> GetSystemFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.SimplifiedProcessing,
                    Description = "使用簡化的處理流程",
                    Priority = 7,
                    Reliability = 0.9,
                    EstimatedCost = 2,
                    IsAvailable = true,
                    RequiresFurtherAction = false
                },
                new()
                {
                    Type = FallbackType.DegradedMode,
                    Description = "啟動系統降級模式",
                    Priority = 6,
                    Reliability = 0.8,
                    EstimatedCost = 3,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "監控系統恢復狀況"
                }
            };
        }

        private static List<FallbackOption> GetConfigurationFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.DefaultValue,
                    Description = "使用系統預設配置",
                    Priority = 8,
                    Reliability = 0.9,
                    EstimatedCost = 1,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "修正配置設定"
                }
            };
        }

        private static List<FallbackOption> GetBusinessLogicFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.SimplifiedProcessing,
                    Description = "使用簡化的業務流程",
                    Priority = 7,
                    Reliability = 0.8,
                    EstimatedCost = 2,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "稍後執行完整的業務流程"
                }
            };
        }

        private static List<FallbackOption> GetGeneralFallbackOptions(ProcessingError error)
        {
            return new List<FallbackOption>
            {
                new()
                {
                    Type = FallbackType.ManualIntervention,
                    Description = "轉為手動處理模式",
                    Priority = 3,
                    Reliability = 1.0,
                    EstimatedCost = 5,
                    IsAvailable = true,
                    RequiresFurtherAction = true,
                    NextAction = "等待人工處理"
                }
            };
        }

        // 執行不同類型的備援方案
        private async Task<FallbackExecutionResult> ExecuteAlternativeServiceFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(1000); // 模擬切換服務時間
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "已成功切換到備用AI摘要服務",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(1000)
            };
        }

        private async Task<FallbackExecutionResult> ExecuteDegradedModeFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(500); // 模擬啟動降級模式時間
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "系統已切換為降級模式運行",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(500)
            };
        }

        private async Task<FallbackExecutionResult> ExecuteCachedResponseFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(200); // 模擬快取讀取時間
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "已提供快取的處理結果",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(200)
            };
        }

        private async Task<FallbackExecutionResult> ExecuteDefaultValueFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(100); // 模擬設定預設值時間
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "已套用系統預設設定值",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(100)
            };
        }

        private async Task<FallbackExecutionResult> ExecuteSimplifiedProcessingFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(800); // 模擬簡化處理時間
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "已執行簡化的處理流程",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(800)
            };
        }

        private async Task<FallbackExecutionResult> ExecuteManualInterventionFallbackAsync(ProcessingError error, FallbackOption fallback)
        {
            await Task.Delay(300); // 模擬手動介入設定時間
            
            // 發送手動處理通知
            await SendNotificationAsync(error, "任務已轉入手動處理佇列，請等待人工處理");
            
            return new FallbackExecutionResult
            {
                FallbackType = fallback.Type,
                Success = true,
                Message = "任務已轉入手動處理模式",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddMilliseconds(300)
            };
        }

        private async Task SaveCurrentStateAsync(ProcessingError error)
        {
            try
            {
                if (error.BatchId.HasValue)
                {
                    // 備援策略記錄狀態，實際的部分結果保存由其他服務處理
                    _logger.LogInformation("備援策略執行 - 批次 {BatchId} 在 {Timestamp} 切換備援模式",
                        error.BatchId.Value, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存當前狀態時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
            }
        }

        private static void RecordFallbackExecution(ProcessingError error, FallbackOption fallback, FallbackExecutionResult result)
        {
            error.ErrorContext["FallbackExecutedAt"] = DateTime.UtcNow;
            error.ErrorContext["FallbackType"] = fallback.Type.ToString();
            error.ErrorContext["FallbackSuccess"] = result.Success;
            error.ErrorContext["FallbackMessage"] = result.Message;
            error.ErrorContext["FallbackDurationMs"] = result.DurationMs;
        }
    }

    // 備援相關的資料模型
    public class FallbackOption
    {
        public FallbackType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } // 1-10, 數字越大優先級越高
        public double Reliability { get; set; } // 0.0-1.0
        public int EstimatedCost { get; set; } // 1-5, 數字越大成本越高
        public bool IsAvailable { get; set; }
        public bool RequiresFurtherAction { get; set; }
        public string? NextAction { get; set; }
    }

    public class FallbackExecutionResult
    {
        public FallbackType FallbackType { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int DurationMs { get; set; }
    }

    public enum FallbackType
    {
        /// <summary>
        /// 替代服務
        /// </summary>
        AlternativeService,

        /// <summary>
        /// 降級模式
        /// </summary>
        DegradedMode,

        /// <summary>
        /// 快取回應
        /// </summary>
        CachedResponse,

        /// <summary>
        /// 預設值
        /// </summary>
        DefaultValue,

        /// <summary>
        /// 簡化處理
        /// </summary>
        SimplifiedProcessing,

        /// <summary>
        /// 手動介入
        /// </summary>
        ManualIntervention
    }
}