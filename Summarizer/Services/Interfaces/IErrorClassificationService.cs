using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces
{
    /// <summary>
    /// 錯誤分類和處理服務介面
    /// 提供錯誤自動分類、處理策略決定和診斷資訊收集功能
    /// </summary>
    public interface IErrorClassificationService
    {
        /// <summary>
        /// 分類和處理例外錯誤
        /// </summary>
        /// <param name="exception">發生的例外</param>
        /// <param name="context">錯誤發生的內容資訊</param>
        /// <param name="batchId">相關的批次識別碼（可選）</param>
        /// <param name="userId">相關的使用者識別碼（可選）</param>
        /// <returns>處理後的錯誤資訊</returns>
        Task<ProcessingError> ClassifyAndProcessErrorAsync(
            Exception exception, 
            string context, 
            Guid? batchId = null, 
            string? userId = null);

        /// <summary>
        /// 根據錯誤分類決定處理策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>建議的處理策略</returns>
        Task<ErrorHandlingStrategy> DetermineHandlingStrategyAsync(ProcessingError error);

        /// <summary>
        /// 生成使用者友善的錯誤訊息
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="context">內容資訊</param>
        /// <returns>使用者友善的錯誤訊息</returns>
        string GenerateUserFriendlyMessage(Exception exception, string context);

        /// <summary>
        /// 生成錯誤解決方案建議
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>解決方案建議列表</returns>
        Task<List<string>> GenerateSuggestedActionsAsync(ProcessingError error);

        /// <summary>
        /// 收集錯誤診斷資訊
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="context">內容資訊</param>
        /// <param name="batchId">相關的批次識別碼（可選）</param>
        /// <returns>診斷資訊</returns>
        Task<Dictionary<string, object>> CollectDiagnosticInfoAsync(
            Exception exception, 
            string context, 
            Guid? batchId = null);

        /// <summary>
        /// 判斷錯誤是否可恢復
        /// </summary>
        /// <param name="exception">例外資訊</param>
        /// <param name="retryAttempts">已重試次數</param>
        /// <returns>是否可恢復</returns>
        bool IsRecoverable(Exception exception, int retryAttempts = 0);

        /// <summary>
        /// 執行錯誤處理策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        Task<ErrorHandlingResult> ExecuteHandlingStrategyAsync(ProcessingError error);

        /// <summary>
        /// 記錄錯誤到系統日誌
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>記錄操作任務</returns>
        Task LogErrorAsync(ProcessingError error);

        /// <summary>
        /// 通知相關使用者錯誤發生
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>通知操作任務</returns>
        Task NotifyErrorAsync(ProcessingError error);
    }

    /// <summary>
    /// 錯誤處理結果
    /// </summary>
    public class ErrorHandlingResult
    {
        /// <summary>
        /// 處理是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 處理結果訊息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 是否需要進一步處理
        /// </summary>
        public bool RequiresFurtherAction { get; set; }

        /// <summary>
        /// 建議的下一步操作
        /// </summary>
        public string? NextAction { get; set; }

        /// <summary>
        /// 處理產生的額外資料
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }
}