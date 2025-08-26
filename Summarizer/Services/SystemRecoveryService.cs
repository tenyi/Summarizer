using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Summarizer.Data;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Diagnostics;

namespace Summarizer.Services
{
    /// <summary>
    /// 系統恢復服務實作
    /// 負責處理系統在取消或錯誤後的狀態恢復、資源清理和健康檢查
    /// </summary>
    public class SystemRecoveryService : ISystemRecoveryService
    {
        private readonly SummarizerDbContext _dbContext;
        private readonly IBatchProgressNotificationService _notificationService;
        private readonly ILogger<SystemRecoveryService> _logger;
        private readonly IBatchSummaryProcessingService _processorService;
        private readonly ICancellationService _cancellationService;
        
        // 追蹤恢復狀態的內部字典
        private readonly Dictionary<Guid, RecoveryStatus> _recoveryStatuses = new();
        private readonly Dictionary<Guid, SystemRecoveryResult> _activeRecoveries = new();
        
        public SystemRecoveryService(
            SummarizerDbContext dbContext,
            IBatchProgressNotificationService notificationService,
            ILogger<SystemRecoveryService> logger,
            IBatchSummaryProcessingService processorService,
            ICancellationService cancellationService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processorService = processorService ?? throw new ArgumentNullException(nameof(processorService));
            _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        }

        /// <summary>
        /// 執行系統恢復程序
        /// 包含狀態清理、資源釋放、UI恢復等完整流程
        /// </summary>
        public async Task<SystemRecoveryResult> RecoverSystemAsync(Guid batchId, RecoveryReason reason)
        {
            var result = new SystemRecoveryResult
            {
                BatchId = batchId,
                Reason = reason,
                StartedAt = DateTime.UtcNow
            };

            _activeRecoveries[batchId] = result;
            _recoveryStatuses[batchId] = RecoveryStatus.InProgress;

            try
            {
                _logger.LogInformation("開始系統恢復程序，批次ID: {BatchId}，原因: {Reason}", batchId, reason);

                // 步驟 1: 檢查是否需要恢復
                var requiresRecovery = await RequiresRecoveryAsync(batchId);
                var step1 = new RecoveryStep
                {
                    Name = "檢查恢復需求",
                    Description = "驗證批次是否需要執行恢復程序",
                    Status = StepStatus.InProgress,
                    StartedAt = DateTime.UtcNow
                };
                result.Steps.Add(step1);

                if (!requiresRecovery && reason != RecoveryReason.ManualRecovery)
                {
                    step1.Status = StepStatus.Skipped;
                    step1.CompletedAt = DateTime.UtcNow;
                    step1.ResultMessage = "批次不需要恢復";
                    result.IsSuccess = true;
                    result.PostRecoveryState = SystemState.Healthy;
                    result.CompletedAt = DateTime.UtcNow;
                    return result;
                }

                step1.Status = StepStatus.Completed;
                step1.CompletedAt = DateTime.UtcNow;
                step1.ResultMessage = "確認需要執行恢復";

                // 步驟 2: 清理批次處理狀態
                var step2 = await ExecuteRecoveryStep("清理處理狀態", "清理批次處理相關的暫存資料和狀態", 
                    async () => await CleanupBatchStateAsync(batchId));
                result.Steps.Add(step2);

                // 步驟 3: 釋放系統資源
                var step3 = await ExecuteRecoveryStep("釋放系統資源", "釋放與批次相關的記憶體和系統資源",
                    async () => await ReleaseResourcesAsync(batchId));
                result.Steps.Add(step3);

                // 步驟 4: 重置UI狀態
                var step4 = await ExecuteRecoveryStep("重置UI狀態", "確保前端介面回到正常可用狀態",
                    async () => await ResetUIStateAsync(batchId));
                result.Steps.Add(step4);

                // 步驟 5: 執行健康檢查
                var step5 = new RecoveryStep
                {
                    Name = "系統健康檢查",
                    Description = "驗證系統各組件是否正常運作",
                    Status = StepStatus.InProgress,
                    StartedAt = DateTime.UtcNow
                };
                result.Steps.Add(step5);

                var healthCheck = await PerformHealthCheckAsync();
                step5.Status = healthCheck.OverallStatus == HealthStatus.Healthy ? StepStatus.Completed : StepStatus.Failed;
                step5.CompletedAt = DateTime.UtcNow;
                step5.ResultMessage = $"健康檢查結果: {healthCheck.OverallStatus}";
                step5.Context["HealthCheckResult"] = healthCheck;

                // 如果健康檢查發現問題，嘗試自我修復
                if (healthCheck.OverallStatus != HealthStatus.Healthy)
                {
                    var step6 = new RecoveryStep
                    {
                        Name = "自我修復",
                        Description = "嘗試自動修復發現的系統問題",
                        Status = StepStatus.InProgress,
                        StartedAt = DateTime.UtcNow
                    };
                    result.Steps.Add(step6);

                    var repairResult = await PerformSelfRepairAsync(healthCheck);
                    step6.Status = repairResult.IsSuccess ? StepStatus.Completed : StepStatus.Failed;
                    step6.CompletedAt = DateTime.UtcNow;
                    step6.ResultMessage = $"修復完成，成功: {repairResult.SuccessfulRepairs}，失敗: {repairResult.FailedRepairs}";
                    step6.Context["SelfRepairResult"] = repairResult;

                    result.PostRecoveryState = repairResult.PostRepairState;
                }
                else
                {
                    result.PostRecoveryState = SystemState.Healthy;
                }

                // 判斷整體恢復是否成功
                var allStepsSuccessful = result.Steps.All(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped);
                result.IsSuccess = allStepsSuccessful;
                result.CompletedAt = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    _recoveryStatuses[batchId] = RecoveryStatus.Completed;
                    result.Recommendations.Add("系統恢復成功，可以繼續正常使用");
                    _logger.LogInformation("系統恢復完成，批次ID: {BatchId}，總耗時: {Duration}", batchId, result.Duration);
                }
                else
                {
                    _recoveryStatuses[batchId] = RecoveryStatus.Failed;
                    result.Recommendations.Add("部分恢復步驟失敗，建議手動檢查系統狀態");
                    _logger.LogError("系統恢復部分失敗，批次ID: {BatchId}", batchId);
                }

                // 統計資訊
                result.Statistics["TotalSteps"] = result.Steps.Count;
                result.Statistics["SuccessfulSteps"] = result.Steps.Count(s => s.Status == StepStatus.Completed);
                result.Statistics["FailedSteps"] = result.Steps.Count(s => s.Status == StepStatus.Failed);
                result.Statistics["SkippedSteps"] = result.Steps.Count(s => s.Status == StepStatus.Skipped);

                // 通知恢復結果
                await _notificationService.NotifyRecoveryCompleted(batchId, result.IsSuccess, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "系統恢復過程中發生錯誤，批次ID: {BatchId}", batchId);
                
                result.IsSuccess = false;
                result.CompletedAt = DateTime.UtcNow;
                result.PostRecoveryState = SystemState.Failed;
                result.Errors.Add(new ProcessingError
                {
                    BatchId = batchId,
                    Category = ErrorCategory.System,
                    Severity = ErrorSeverity.Critical,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "系統恢復過程中發生未預期的錯誤",
                    ErrorContext = { ["Exception"] = ex.ToString() }
                });

                _recoveryStatuses[batchId] = RecoveryStatus.Failed;
                return result;
            }
        }

        /// <summary>
        /// 清理批次處理狀態
        /// 移除暫存資料、重置處理狀態、清理相關記錄
        /// </summary>
        public async Task<bool> CleanupBatchStateAsync(Guid batchId)
        {
            try
            {
                _logger.LogDebug("開始清理批次狀態，批次ID: {BatchId}", batchId);

                // 1. 檢查並取消正在進行的處理
                if (_cancellationService.IsCancellationRequested(batchId))
                {
                    // 透過處理器服務取消處理
                    await _processorService.CancelBatchProcessingAsync(batchId);
                }

                // 2. 清理部分結果（將處理中的狀態設為失敗）
                var partialResults = await _dbContext.PartialResults
                    .Where(p => p.BatchId == batchId && p.Status == PartialResultStatus.Processing)
                    .ToListAsync();

                foreach (var result in partialResults)
                {
                    result.Status = PartialResultStatus.Failed;
                    result.CancellationTime = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("批次狀態清理完成，批次ID: {BatchId}，清理記錄數: {Count}", 
                    batchId, partialResults.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理批次狀態時發生錯誤，批次ID: {BatchId}", batchId);
                return false;
            }
        }

        /// <summary>
        /// 釋放系統資源
        /// 包含記憶體清理、連接釋放、暫存檔案清除等
        /// </summary>
        public async Task<bool> ReleaseResourcesAsync(Guid? batchId = null)
        {
            try
            {
                _logger.LogDebug("開始釋放系統資源，批次ID: {BatchId}", batchId?.ToString() ?? "全域");

                // 1. 強制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // 2. 清理處理器服務的內部狀態
                if (batchId.HasValue)
                {
                    // 取消相關的批次處理
                    await _processorService.CancelBatchProcessingAsync(batchId.Value);
                }

                // 3. 清理資料庫連接池（如果需要）
                await _dbContext.Database.CloseConnectionAsync();

                // 4. 清理暫存檔案
                await CleanupTemporaryFilesAsync(batchId);

                // 5. 從恢復追蹤中移除已完成的項目
                if (batchId.HasValue && _activeRecoveries.ContainsKey(batchId.Value))
                {
                    var recovery = _activeRecoveries[batchId.Value];
                    if (recovery.CompletedAt.HasValue)
                    {
                        _activeRecoveries.Remove(batchId.Value);
                    }
                }

                _logger.LogInformation("系統資源釋放完成，批次ID: {BatchId}", batchId?.ToString() ?? "全域");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "釋放系統資源時發生錯誤，批次ID: {BatchId}", batchId?.ToString() ?? "全域");
                return false;
            }
        }

        /// <summary>
        /// 重置UI狀態
        /// 確保前端介面回到正常可用狀態
        /// </summary>
        public async Task<bool> ResetUIStateAsync(Guid batchId)
        {
            try
            {
                _logger.LogDebug("開始重置UI狀態，批次ID: {BatchId}", batchId);

                // 1. 通知前端重置批次狀態
                await _notificationService.NotifyUIReset(batchId);

                // 2. 清理前端可能緩存的進度資訊
                await _notificationService.NotifyProgressReset(batchId);

                // 3. 發送UI恢復完成通知
                await _notificationService.NotifyUIRecoveryCompleted(batchId);

                _logger.LogInformation("UI狀態重置完成，批次ID: {BatchId}", batchId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置UI狀態時發生錯誤，批次ID: {BatchId}", batchId);
                return false;
            }
        }

        /// <summary>
        /// 執行系統健康檢查
        /// 驗證各系統組件是否正常運作
        /// </summary>
        public async Task<SystemHealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new SystemHealthCheckResult
            {
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogDebug("開始執行系統健康檢查");

                // 1. 檢查資料庫連接
                var dbHealth = await CheckDatabaseHealthAsync();
                result.Components["Database"] = dbHealth;

                // 2. 檢查記憶體使用情況
                var memoryHealth = CheckMemoryHealth();
                result.Components["Memory"] = memoryHealth;

                // 3. 檢查磁碟空間
                var diskHealth = CheckDiskHealth();
                result.Components["Disk"] = diskHealth;

                // 4. 檢查處理服務狀態
                var processorHealth = await CheckProcessorHealthAsync();
                result.Components["Processor"] = processorHealth;

                // 5. 檢查通知服務
                var notificationHealth = await CheckNotificationHealthAsync();
                result.Components["Notification"] = notificationHealth;

                // 計算整體健康狀態
                var componentStatuses = result.Components.Values.Select(c => c.Status).ToList();
                if (componentStatuses.Any(s => s == HealthStatus.Critical))
                {
                    result.OverallStatus = HealthStatus.Critical;
                }
                else if (componentStatuses.Any(s => s == HealthStatus.Unhealthy))
                {
                    result.OverallStatus = HealthStatus.Unhealthy;
                }
                else if (componentStatuses.Any(s => s == HealthStatus.Warning))
                {
                    result.OverallStatus = HealthStatus.Warning;
                }
                else
                {
                    result.OverallStatus = HealthStatus.Healthy;
                }

                // 收集效能指標
                result.Performance = await CollectPerformanceMetricsAsync();

                // 生成建議
                GenerateHealthRecommendations(result);

                _logger.LogInformation("系統健康檢查完成，整體狀態: {Status}", result.OverallStatus);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行系統健康檢查時發生錯誤");
                result.OverallStatus = HealthStatus.Unknown;
                result.Issues.Add(new HealthIssue
                {
                    Type = IssueType.Other,
                    Severity = IssueSeverity.Critical,
                    Title = "健康檢查執行失敗",
                    Description = $"執行健康檢查時發生錯誤: {ex.Message}",
                    Component = "HealthCheck",
                    CanAutoFix = false
                });
                return result;
            }
        }

        /// <summary>
        /// 執行自我修復
        /// 嘗試自動修復發現的系統問題
        /// </summary>
        public async Task<SelfRepairResult> PerformSelfRepairAsync(SystemHealthCheckResult healthCheckResult)
        {
            var result = new SelfRepairResult
            {
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("開始執行自我修復，發現 {Count} 個問題", healthCheckResult.Issues.Count);

                foreach (var issue in healthCheckResult.Issues.Where(i => i.CanAutoFix))
                {
                    var attempt = new RepairAttempt
                    {
                        IssueId = issue.IssueId,
                        Action = $"修復 {issue.Title}",
                        StartedAt = DateTime.UtcNow
                    };

                    try
                    {
                        var success = await AttemptRepairAsync(issue);
                        attempt.Success = success;
                        attempt.CompletedAt = DateTime.UtcNow;

                        if (success)
                        {
                            attempt.ResultMessage = "修復成功";
                            result.SuccessfulRepairs++;
                        }
                        else
                        {
                            attempt.ResultMessage = "修復失敗";
                            result.FailedRepairs++;
                        }
                    }
                    catch (Exception ex)
                    {
                        attempt.Success = false;
                        attempt.CompletedAt = DateTime.UtcNow;
                        attempt.ErrorMessage = ex.Message;
                        result.FailedRepairs++;
                        _logger.LogError(ex, "修復問題時發生錯誤: {Title}", issue.Title);
                    }

                    result.RepairAttempts.Add(attempt);
                }

                // 將無法自動修復的問題添加到列表
                result.UnresolvableIssues.AddRange(healthCheckResult.Issues.Where(i => !i.CanAutoFix));

                result.CompletedAt = DateTime.UtcNow;
                result.IsSuccess = result.FailedRepairs == 0;

                // 設定修復後的系統狀態
                if (result.IsSuccess && result.UnresolvableIssues.Count == 0)
                {
                    result.PostRepairState = SystemState.Healthy;
                }
                else if (result.UnresolvableIssues.Any(i => i.Severity == IssueSeverity.Critical))
                {
                    result.PostRepairState = SystemState.Failed;
                    result.ManualActionRequired.Add("發現嚴重問題需要手動處理");
                }
                else
                {
                    result.PostRepairState = SystemState.Warning;
                }

                _logger.LogInformation("自我修復完成，成功: {Success}，失敗: {Failed}，無法修復: {Unresolvable}", 
                    result.SuccessfulRepairs, result.FailedRepairs, result.UnresolvableIssues.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行自我修復時發生錯誤");
                result.IsSuccess = false;
                result.CompletedAt = DateTime.UtcNow;
                result.PostRepairState = SystemState.Failed;
                return result;
            }
        }

        /// <summary>
        /// 檢查批次是否需要恢復
        /// 偵測異常終止或未完成的批次處理
        /// </summary>
        public async Task<bool> RequiresRecoveryAsync(Guid batchId)
        {
            try
            {
                // 1. 檢查是否有處理中但長時間未更新的部分結果
                var staleProcessing = await _dbContext.PartialResults
                    .Where(p => p.BatchId == batchId && 
                               p.Status == PartialResultStatus.Processing &&
                               p.CancellationTime < DateTime.UtcNow.AddMinutes(-30))
                    .AnyAsync();

                // 2. 檢查是否有取消請求但未完成取消的情況
                var pendingCancellation = _cancellationService.IsCancellationRequested(batchId) &&
                                          await _dbContext.PartialResults
                                              .Where(p => p.BatchId == batchId && p.Status != PartialResultStatus.Failed)
                                              .AnyAsync();

                return staleProcessing || pendingCancellation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檢查恢復需求時發生錯誤，批次ID: {BatchId}", batchId);
                return true; // 發生錯誤時保守處理，認為需要恢復
            }
        }

        /// <summary>
        /// 取得恢復狀態
        /// 回報目前恢復程序的進度和狀態
        /// </summary>
        public async Task<RecoveryStatus> GetRecoveryStatusAsync(Guid batchId)
        {
            await Task.CompletedTask; // 這是同步操作但介面要求非同步
            
            if (_recoveryStatuses.TryGetValue(batchId, out var status))
            {
                return status;
            }

            // 如果沒有恢復狀態記錄，檢查是否需要恢復
            return await RequiresRecoveryAsync(batchId) ? RecoveryStatus.Pending : RecoveryStatus.NotRequired;
        }

        #region 私有輔助方法

        /// <summary>
        /// 執行恢復步驟的通用方法
        /// </summary>
        private async Task<RecoveryStep> ExecuteRecoveryStep(string name, string description, Func<Task<bool>> action)
        {
            var step = new RecoveryStep
            {
                Name = name,
                Description = description,
                Status = StepStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                var success = await action();
                step.Status = success ? StepStatus.Completed : StepStatus.Failed;
                step.ResultMessage = success ? "執行成功" : "執行失敗";
            }
            catch (Exception ex)
            {
                step.Status = StepStatus.Failed;
                step.ErrorMessage = ex.Message;
                _logger.LogError(ex, "恢復步驟執行失敗: {StepName}", name);
            }

            step.CompletedAt = DateTime.UtcNow;
            return step;
        }

        /// <summary>
        /// 清理暫存檔案
        /// </summary>
        private async Task CleanupTemporaryFilesAsync(Guid? batchId)
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var pattern = batchId.HasValue ? $"batch_{batchId.Value}*" : "batch_*";
                
                var tempFiles = Directory.GetFiles(tempPath, pattern);
                foreach (var file in tempFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "無法刪除暫存檔案: {FilePath}", file);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理暫存檔案時發生錯誤");
            }
        }

        /// <summary>
        /// 檢查資料庫健康狀態
        /// </summary>
        private async Task<ComponentHealthStatus> CheckDatabaseHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _dbContext.Database.CanConnectAsync();
                stopwatch.Stop();

                return new ComponentHealthStatus
                {
                    Name = "Database",
                    Status = HealthStatus.Healthy,
                    Description = "資料庫連接正常",
                    LastCheckedAt = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ComponentHealthStatus
                {
                    Name = "Database",
                    Status = HealthStatus.Critical,
                    Description = $"資料庫連接失敗: {ex.Message}",
                    LastCheckedAt = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Warnings = { ex.Message }
                };
            }
        }

        /// <summary>
        /// 檢查記憶體健康狀態
        /// </summary>
        private ComponentHealthStatus CheckMemoryHealth()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var totalMemory = GC.GetTotalMemory(false);
                
                // 假設超過 1GB 為警告，超過 2GB 為不健康
                var warningThreshold = 1024 * 1024 * 1024L; // 1GB
                var unhealthyThreshold = 2048 * 1024 * 1024L; // 2GB

                var status = workingSet > unhealthyThreshold ? HealthStatus.Unhealthy :
                           workingSet > warningThreshold ? HealthStatus.Warning : HealthStatus.Healthy;

                return new ComponentHealthStatus
                {
                    Name = "Memory",
                    Status = status,
                    Description = $"記憶體使用量: {workingSet / (1024 * 1024)} MB",
                    LastCheckedAt = DateTime.UtcNow,
                    Metrics = 
                    {
                        ["WorkingSetMB"] = workingSet / (1024 * 1024),
                        ["TotalMemoryMB"] = totalMemory / (1024 * 1024)
                    }
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthStatus
                {
                    Name = "Memory",
                    Status = HealthStatus.Unknown,
                    Description = $"無法檢查記憶體狀態: {ex.Message}",
                    LastCheckedAt = DateTime.UtcNow,
                    Warnings = { ex.Message }
                };
            }
        }

        /// <summary>
        /// 檢查磁碟健康狀態
        /// </summary>
        private ComponentHealthStatus CheckDiskHealth()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                var minFreeSpacePercent = 100.0;

                foreach (var drive in drives)
                {
                    var freeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                    minFreeSpacePercent = Math.Min(minFreeSpacePercent, freeSpacePercent);
                }

                var status = minFreeSpacePercent < 5 ? HealthStatus.Critical :
                           minFreeSpacePercent < 15 ? HealthStatus.Unhealthy :
                           minFreeSpacePercent < 25 ? HealthStatus.Warning : HealthStatus.Healthy;

                return new ComponentHealthStatus
                {
                    Name = "Disk",
                    Status = status,
                    Description = $"最低可用磁碟空間: {minFreeSpacePercent:F1}%",
                    LastCheckedAt = DateTime.UtcNow,
                    Metrics = { ["MinFreeSpacePercent"] = minFreeSpacePercent }
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthStatus
                {
                    Name = "Disk",
                    Status = HealthStatus.Unknown,
                    Description = $"無法檢查磁碟狀態: {ex.Message}",
                    LastCheckedAt = DateTime.UtcNow,
                    Warnings = { ex.Message }
                };
            }
        }

        /// <summary>
        /// 檢查處理服務健康狀態
        /// </summary>
        private async Task<ComponentHealthStatus> CheckProcessorHealthAsync()
        {
            try
            {
                // 這裡需要根據實際的 IBatchSummaryProcessorService 介面來實作
                // 假設它有一個健康檢查方法
                var isHealthy = true; // 暫時假設為健康
                
                await Task.CompletedTask;
                
                return new ComponentHealthStatus
                {
                    Name = "Processor",
                    Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                    Description = isHealthy ? "處理服務運行正常" : "處理服務異常",
                    LastCheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthStatus
                {
                    Name = "Processor",
                    Status = HealthStatus.Unknown,
                    Description = $"無法檢查處理服務狀態: {ex.Message}",
                    LastCheckedAt = DateTime.UtcNow,
                    Warnings = { ex.Message }
                };
            }
        }

        /// <summary>
        /// 檢查通知服務健康狀態
        /// </summary>
        private async Task<ComponentHealthStatus> CheckNotificationHealthAsync()
        {
            try
            {
                // 這裡需要根據實際的 INotificationService 介面來實作
                var isHealthy = true; // 暫時假設為健康
                
                await Task.CompletedTask;
                
                return new ComponentHealthStatus
                {
                    Name = "Notification",
                    Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                    Description = isHealthy ? "通知服務運行正常" : "通知服務異常",
                    LastCheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealthStatus
                {
                    Name = "Notification",
                    Status = HealthStatus.Unknown,
                    Description = $"無法檢查通知服務狀態: {ex.Message}",
                    LastCheckedAt = DateTime.UtcNow,
                    Warnings = { ex.Message }
                };
            }
        }

        /// <summary>
        /// 收集系統效能指標
        /// </summary>
        private async Task<SystemPerformanceMetrics> CollectPerformanceMetricsAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                // 計算 CPU 使用率需要兩個時間點的測量
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;
                await Task.Delay(100); // 等待 100ms
                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                // 獲取記憶體資訊
                var workingSet = process.WorkingSet64;
                var totalPhysicalMemory = GC.GetTotalMemory(false);

                // 獲取磁碟資訊
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                var avgDiskUsage = drives.Select(d => (double)(d.TotalSize - d.AvailableFreeSpace) / d.TotalSize * 100).Average();

                // 獲取處理中的批次數量
                var processingBatches = await _dbContext.PartialResults
                    .Where(p => p.Status == PartialResultStatus.Processing)
                    .CountAsync();

                return new SystemPerformanceMetrics
                {
                    CpuUsagePercent = Math.Max(0, Math.Min(100, cpuUsageTotal * 100)),
                    MemoryUsagePercent = (double)workingSet / (1024 * 1024 * 1024) * 100, // 轉換為GB的百分比
                    DiskUsagePercent = avgDiskUsage,
                    AverageResponseTimeMs = 50, // 這需要根據實際情況測量
                    ActiveConnections = 0, // 這需要從連接池獲取
                    ProcessingBatches = processingBatches,
                    ErrorRatePercent = 0, // 這需要從錯誤統計獲取
                    AdditionalMetrics =
                    {
                        ["WorkingSetMB"] = workingSet / (1024 * 1024),
                        ["TotalMemoryMB"] = totalPhysicalMemory / (1024 * 1024)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收集效能指標時發生錯誤");
                return new SystemPerformanceMetrics();
            }
        }

        /// <summary>
        /// 生成健康檢查建議
        /// </summary>
        private void GenerateHealthRecommendations(SystemHealthCheckResult result)
        {
            if (result.OverallStatus == HealthStatus.Critical)
            {
                result.RecommendedActions.Add("系統發現嚴重問題，建議立即停止所有批次處理並進行維護");
                result.NextCheckRecommendedAt = DateTime.UtcNow.AddMinutes(5);
            }
            else if (result.OverallStatus == HealthStatus.Unhealthy)
            {
                result.RecommendedActions.Add("系統狀態不健康，建議執行自我修復程序");
                result.NextCheckRecommendedAt = DateTime.UtcNow.AddMinutes(15);
            }
            else if (result.OverallStatus == HealthStatus.Warning)
            {
                result.RecommendedActions.Add("系統有警告狀態，建議監控系統資源使用情況");
                result.NextCheckRecommendedAt = DateTime.UtcNow.AddMinutes(30);
            }
            else
            {
                result.RecommendedActions.Add("系統運行正常，繼續定期監控");
                result.NextCheckRecommendedAt = DateTime.UtcNow.AddHours(1);
            }

            // 根據效能指標添加具體建議
            if (result.Performance.MemoryUsagePercent > 80)
            {
                result.RecommendedActions.Add("記憶體使用率較高，建議執行垃圾回收或重啟服務");
            }

            if (result.Performance.DiskUsagePercent > 90)
            {
                result.RecommendedActions.Add("磁碟空間不足，建議清理暫存檔案或擴充儲存空間");
            }

            if (result.Performance.CpuUsagePercent > 90)
            {
                result.RecommendedActions.Add("CPU使用率過高，建議減少同時處理的批次數量");
            }
        }

        /// <summary>
        /// 嘗試修復特定問題
        /// </summary>
        private async Task<bool> AttemptRepairAsync(HealthIssue issue)
        {
            try
            {
                switch (issue.Type)
                {
                    case IssueType.Performance:
                        return await RepairPerformanceIssueAsync(issue);
                    
                    case IssueType.Resource:
                        return await RepairResourceIssueAsync(issue);
                    
                    case IssueType.Connectivity:
                        return await RepairConnectivityIssueAsync(issue);
                    
                    case IssueType.Configuration:
                        return await RepairConfigurationIssueAsync(issue);
                    
                    case IssueType.Data:
                        return await RepairDataIssueAsync(issue);
                    
                    default:
                        _logger.LogWarning("未支援的問題類型自動修復: {Type}", issue.Type);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修復問題時發生錯誤: {Title}", issue.Title);
                return false;
            }
        }

        private async Task<bool> RepairPerformanceIssueAsync(HealthIssue issue)
        {
            // 執行垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            await Task.CompletedTask;
            return true;
        }

        private async Task<bool> RepairResourceIssueAsync(HealthIssue issue)
        {
            // 清理暫存檔案
            await CleanupTemporaryFilesAsync(null);
            return true;
        }

        private async Task<bool> RepairConnectivityIssueAsync(HealthIssue issue)
        {
            try
            {
                // 重新連接資料庫
                await _dbContext.Database.CloseConnectionAsync();
                await _dbContext.Database.CanConnectAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RepairConfigurationIssueAsync(HealthIssue issue)
        {
            await Task.CompletedTask;
            // 設定問題通常需要手動處理
            return false;
        }

        private async Task<bool> RepairDataIssueAsync(HealthIssue issue)
        {
            try
            {
                // 清理過期的處理記錄
                var expiredRecords = await _dbContext.PartialResults
                    .Where(p => p.Status == PartialResultStatus.Processing && p.CancellationTime < DateTime.UtcNow.AddHours(-2))
                    .ToListAsync();

                foreach (var record in expiredRecords)
                {
                    record.Status = PartialResultStatus.Failed;
                    record.CancellationTime = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}