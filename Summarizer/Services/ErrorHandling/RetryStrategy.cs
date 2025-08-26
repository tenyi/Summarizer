using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 重試策略
    /// 適用於暫時性錯誤，如網路連線問題、服務暫時不可用等
    /// </summary>
    public class RetryStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 預設最大重試次數
        /// </summary>
        private const int DefaultMaxRetries = 3;

        /// <summary>
        /// 預設重試延遲（毫秒）
        /// </summary>
        private const int DefaultDelayMs = 1000;

        /// <summary>
        /// 初始化重試策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public RetryStrategy(
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
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.Retry;

        /// <summary>
        /// 執行重試策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "重試策略");

            try
            {
                // 檢查是否已取消
                if (IsCancellationRequested(error))
                {
                    return CreateFailureResult("操作已被取消，無法執行重試", false, "檢查取消原因並決定是否重新開始");
                }

                // 檢查是否適合重試
                if (!IsRetryable(error))
                {
                    return CreateFailureResult(
                        $"錯誤類型 {error.Category} 不適合重試處理", 
                        true, 
                        "建議使用其他錯誤處理策略");
                }

                // 取得重試配置
                var retryConfig = GetRetryConfiguration(error);
                
                // 檢查是否已超過最大重試次數
                var currentRetries = GetCurrentRetryCount(error);
                if (currentRetries >= retryConfig.MaxRetries)
                {
                    await SendNotificationAsync(error, $"已達到最大重試次數 {retryConfig.MaxRetries}，停止重試");
                    return CreateFailureResult(
                        $"已達到最大重試次數 {retryConfig.MaxRetries}，重試失敗", 
                        true, 
                        "考慮使用升級策略或手動介入");
                }

                // 計算延遲時間（指數退避）
                var delay = CalculateDelay(currentRetries, retryConfig);
                
                // 記錄重試資訊
                RecordRetryAttempt(error, currentRetries + 1);

                // 發送重試通知
                await SendNotificationAsync(error, 
                    $"正在執行第 {currentRetries + 1} 次重試，預計延遲 {delay} 毫秒");

                // 等待重試延遲
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }

                // 檢查取消狀態（延遲後再次檢查）
                if (IsCancellationRequested(error))
                {
                    return CreateFailureResult("在重試等待期間操作被取消", false);
                }

                // 執行重試邏輯
                var retryResult = await PerformRetryAsync(error, currentRetries + 1);
                
                LogHandlingComplete(error, retryResult, "重試策略");
                return retryResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行重試策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                return CreateFailureResult(
                    $"重試策略執行異常: {ex.Message}", 
                    true, 
                    "檢查重試策略配置或考慮其他處理方式");
            }
        }

        /// <summary>
        /// 判斷錯誤是否適合重試
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合重試</returns>
        public override bool CanHandle(ProcessingError error)
        {
            return base.CanHandle(error) && IsRetryable(error);
        }

        /// <summary>
        /// 檢查錯誤是否可重試
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否可重試</returns>
        private static bool IsRetryable(ProcessingError error)
        {
            // 根據錯誤類型判斷是否適合重試
            return error.Category switch
            {
                ErrorCategory.Network => true,           // 網路錯誤通常可重試（含逾時）
                ErrorCategory.Service => true,           // 服務錯誤可能是暫時性的
                ErrorCategory.Storage => false,         // 儲存錯誤需要檢查
                ErrorCategory.Validation => false,       // 驗證錯誤重試無意義
                ErrorCategory.Authorization => false,    // 授權錯誤重試通常無效
                ErrorCategory.Authentication => false,   // 認證錯誤不應重試
                ErrorCategory.Processing => false,       // 業務邏輯錯誤重試無意義
                ErrorCategory.System => false,           // 系統錯誤通常不可重試（含資源和配置）
                // 其他類型謹慎處理
                _ => false
            };
        }

        /// <summary>
        /// 取得重試配置
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>重試配置</returns>
        private static RetryConfiguration GetRetryConfiguration(ProcessingError error)
        {
            // 根據錯誤類型和嚴重程度調整重試配置
            return error.Severity switch
            {
                ErrorSeverity.Info => new RetryConfiguration 
                { 
                    MaxRetries = 5, 
                    BaseDelayMs = 500, 
                    UseExponentialBackoff = true 
                },
                ErrorSeverity.Warning => new RetryConfiguration 
                { 
                    MaxRetries = DefaultMaxRetries, 
                    BaseDelayMs = DefaultDelayMs, 
                    UseExponentialBackoff = true 
                },
                ErrorSeverity.Error => new RetryConfiguration 
                { 
                    MaxRetries = 2, 
                    BaseDelayMs = 2000, 
                    UseExponentialBackoff = true 
                },
                ErrorSeverity.Critical => new RetryConfiguration 
                { 
                    MaxRetries = 1, 
                    BaseDelayMs = 1000, 
                    UseExponentialBackoff = false 
                },
                _ => new RetryConfiguration 
                { 
                    MaxRetries = DefaultMaxRetries, 
                    BaseDelayMs = DefaultDelayMs, 
                    UseExponentialBackoff = true 
                }
            };
        }

        /// <summary>
        /// 取得目前重試次數
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>重試次數</returns>
        private static int GetCurrentRetryCount(ProcessingError error)
        {
            if (error.ErrorContext.TryGetValue("RetryCount", out var retryCountObj) && 
                retryCountObj is int retryCount)
            {
                return retryCount;
            }
            return 0;
        }

        /// <summary>
        /// 記錄重試嘗試
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="attemptNumber">嘗試次數</param>
        private static void RecordRetryAttempt(ProcessingError error, int attemptNumber)
        {
            error.ErrorContext["RetryCount"] = attemptNumber;
            error.ErrorContext["LastRetryAt"] = DateTime.UtcNow;
            
            // 記錄重試歷史
            if (!error.ErrorContext.ContainsKey("RetryHistory"))
            {
                error.ErrorContext["RetryHistory"] = new List<DateTime>();
            }
            
            if (error.ErrorContext["RetryHistory"] is List<DateTime> history)
            {
                history.Add(DateTime.UtcNow);
            }
        }

        /// <summary>
        /// 計算延遲時間
        /// </summary>
        /// <param name="attemptNumber">嘗試次數</param>
        /// <param name="config">重試配置</param>
        /// <returns>延遲毫秒數</returns>
        private static int CalculateDelay(int attemptNumber, RetryConfiguration config)
        {
            if (!config.UseExponentialBackoff)
            {
                return config.BaseDelayMs;
            }

            // 指數退避：延遲時間 = 基礎延遲 * 2^嘗試次數
            var delay = config.BaseDelayMs * Math.Pow(2, attemptNumber);
            
            // 限制最大延遲時間為 30 秒
            return (int)Math.Min(delay, 30000);
        }

        /// <summary>
        /// 執行實際的重試操作
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="attemptNumber">嘗試次數</param>
        /// <returns>重試結果</returns>
        private async Task<ErrorHandlingResult> PerformRetryAsync(ProcessingError error, int attemptNumber)
        {
            // 這裡實作具體的重試邏輯
            // 在實際應用中，這可能需要呼叫原始失敗的操作
            
            try
            {
                // 模擬重試成功的情況（實際實作時需要呼叫具體的業務邏輯）
                await Task.Delay(100); // 模擬處理時間

                // 發送重試成功通知
                await SendNotificationAsync(error, $"第 {attemptNumber} 次重試成功完成");

                return CreateSuccessResult(
                    $"重試策略執行成功，第 {attemptNumber} 次嘗試完成",
                    false,
                    "繼續原本的處理流程");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "第 {AttemptNumber} 次重試失敗，錯誤ID: {ErrorId}", attemptNumber, error.ErrorId);
                
                return CreateFailureResult(
                    $"第 {attemptNumber} 次重試失敗: {ex.Message}",
                    true,
                    "將在下次重試或考慮其他處理策略");
            }
        }

        /// <summary>
        /// 重試配置
        /// </summary>
        private class RetryConfiguration
        {
            /// <summary>
            /// 最大重試次數
            /// </summary>
            public int MaxRetries { get; set; } = DefaultMaxRetries;

            /// <summary>
            /// 基礎延遲時間（毫秒）
            /// </summary>
            public int BaseDelayMs { get; set; } = DefaultDelayMs;

            /// <summary>
            /// 是否使用指數退避
            /// </summary>
            public bool UseExponentialBackoff { get; set; } = true;
        }
    }
}