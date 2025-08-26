using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 錯誤處理策略基礎類別
    /// 提供所有策略共用的基礎功能和依賴服務
    /// </summary>
    public abstract class BaseErrorHandlingStrategy : IErrorHandlingStrategy
    {
        /// <summary>
        /// 日誌記錄器
        /// </summary>
        protected readonly ILogger<BaseErrorHandlingStrategy> _logger;

        /// <summary>
        /// 批次處理進度通知服務
        /// </summary>
        protected readonly IBatchProgressNotificationService _notificationService;

        /// <summary>
        /// 取消操作服務
        /// </summary>
        protected readonly ICancellationService _cancellationService;

        /// <summary>
        /// 部分結果處理器
        /// </summary>
        protected readonly IPartialResultHandler _partialResultHandler;

        /// <summary>
        /// 初始化錯誤處理策略基礎類別
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        protected BaseErrorHandlingStrategy(
            ILogger<BaseErrorHandlingStrategy> logger,
            IBatchProgressNotificationService notificationService,
            ICancellationService cancellationService,
            IPartialResultHandler partialResultHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
            _partialResultHandler = partialResultHandler ?? throw new ArgumentNullException(nameof(partialResultHandler));
        }

        /// <summary>
        /// 策略類型（由衍生類別實作）
        /// </summary>
        public abstract ErrorHandlingStrategy StrategyType { get; }

        /// <summary>
        /// 執行錯誤處理策略（由衍生類別實作）
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public abstract Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error);

        /// <summary>
        /// 判斷此策略是否適用於指定的錯誤（預設實作，衍生類別可覆寫）
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適用</returns>
        public virtual bool CanHandle(ProcessingError error)
        {
            return error.HandlingStrategy == StrategyType;
        }

        /// <summary>
        /// 記錄錯誤處理開始的日誌
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="strategyName">策略名稱</param>
        protected virtual void LogHandlingStart(ProcessingError error, string strategyName)
        {
            _logger.LogInformation(
                "開始執行錯誤處理策略 {StrategyName}，錯誤ID: {ErrorId}，錯誤類型: {Category}，嚴重程度: {Severity}",
                strategyName, error.ErrorId, error.Category, error.Severity);
        }

        /// <summary>
        /// 記錄錯誤處理完成的日誌
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="result">處理結果</param>
        /// <param name="strategyName">策略名稱</param>
        protected virtual void LogHandlingComplete(ProcessingError error, ErrorHandlingResult result, string strategyName)
        {
            _logger.LogInformation(
                "錯誤處理策略 {StrategyName} 執行完成，錯誤ID: {ErrorId}，處理結果: {Success}，訊息: {Message}",
                strategyName, error.ErrorId, result.Success, result.Message);
        }

        /// <summary>
        /// 發送錯誤處理通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="message">通知訊息</param>
        protected virtual async Task SendNotificationAsync(ProcessingError error, string message)
        {
            try
            {
                if (error.BatchId.HasValue)
                {
                    await _notificationService.NotifyErrorAsync(
                        error.BatchId.Value,
                        message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "發送錯誤處理通知時發生異常");
            }
        }

        /// <summary>
        /// 檢查取消權杖狀態
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否已取消</returns>
        protected virtual bool IsCancellationRequested(ProcessingError error)
        {
            try
            {
                if (error.BatchId.HasValue)
                {
                    var cancellationToken = _cancellationService.GetCancellationToken(error.BatchId.Value);
                    return cancellationToken?.IsCancellationRequested ?? false;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "檢查取消狀態時發生異常");
                return false;
            }
        }

        /// <summary>
        /// 建立成功的處理結果
        /// </summary>
        /// <param name="message">結果訊息</param>
        /// <param name="requiresFurtherAction">是否需要進一步處理</param>
        /// <param name="nextAction">建議的下一步操作</param>
        /// <returns>成功的處理結果</returns>
        protected static ErrorHandlingResult CreateSuccessResult(string message, bool requiresFurtherAction = false, string? nextAction = null)
        {
            var result = new ErrorHandlingResult
            {
                Success = true,
                Message = message,
                RequiresFurtherAction = requiresFurtherAction,
                NextAction = nextAction
            };
            
            // 記錄處理時間到資料字典中
            result.Data["HandledAt"] = DateTime.UtcNow;
            
            return result;
        }

        /// <summary>
        /// 建立失敗的處理結果
        /// </summary>
        /// <param name="message">錯誤訊息</param>
        /// <param name="requiresFurtherAction">是否需要進一步處理</param>
        /// <param name="nextAction">建議的下一步操作</param>
        /// <returns>失敗的處理結果</returns>
        protected static ErrorHandlingResult CreateFailureResult(string message, bool requiresFurtherAction = true, string? nextAction = null)
        {
            var result = new ErrorHandlingResult
            {
                Success = false,
                Message = message,
                RequiresFurtherAction = requiresFurtherAction,
                NextAction = nextAction
            };
            
            // 記錄處理時間到資料字典中
            result.Data["HandledAt"] = DateTime.UtcNow;
            
            return result;
        }
    }
}