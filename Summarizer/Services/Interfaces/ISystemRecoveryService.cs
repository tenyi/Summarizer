using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces
{
    /// <summary>
    /// 系統恢復服務介面
    /// 負責處理系統在取消或錯誤後的狀態恢復、資源清理和健康檢查
    /// </summary>
    public interface ISystemRecoveryService
    {
        /// <summary>
        /// 執行系統恢復程序
        /// 包含狀態清理、資源釋放、UI恢復等完整流程
        /// </summary>
        /// <param name="batchId">需要恢復的批次ID</param>
        /// <param name="reason">恢復原因</param>
        /// <returns>恢復結果</returns>
        Task<SystemRecoveryResult> RecoverSystemAsync(Guid batchId, RecoveryReason reason);

        /// <summary>
        /// 清理批次處理狀態
        /// 移除暫存資料、重置處理狀態、清理相關記錄
        /// </summary>
        /// <param name="batchId">批次ID</param>
        /// <returns>清理是否成功</returns>
        Task<bool> CleanupBatchStateAsync(Guid batchId);

        /// <summary>
        /// 釋放系統資源
        /// 包含記憶體清理、連接釋放、暫存檔案清除等
        /// </summary>
        /// <param name="batchId">相關批次ID，null表示全域清理</param>
        /// <returns>釋放是否成功</returns>
        Task<bool> ReleaseResourcesAsync(Guid? batchId = null);

        /// <summary>
        /// 重置UI狀態
        /// 確保前端介面回到正常可用狀態
        /// </summary>
        /// <param name="batchId">批次ID</param>
        /// <returns>重置是否成功</returns>
        Task<bool> ResetUIStateAsync(Guid batchId);

        /// <summary>
        /// 執行系統健康檢查
        /// 驗證各系統組件是否正常運作
        /// </summary>
        /// <returns>健康檢查結果</returns>
        Task<SystemHealthCheckResult> PerformHealthCheckAsync();

        /// <summary>
        /// 執行自我修復
        /// 嘗試自動修復發現的系統問題
        /// </summary>
        /// <param name="healthCheckResult">健康檢查結果</param>
        /// <returns>自我修復結果</returns>
        Task<SelfRepairResult> PerformSelfRepairAsync(SystemHealthCheckResult healthCheckResult);

        /// <summary>
        /// 檢查批次是否需要恢復
        /// 偵測異常終止或未完成的批次處理
        /// </summary>
        /// <param name="batchId">批次ID</param>
        /// <returns>是否需要恢復</returns>
        Task<bool> RequiresRecoveryAsync(Guid batchId);

        /// <summary>
        /// 取得恢復狀態
        /// 回報目前恢復程序的進度和狀態
        /// </summary>
        /// <param name="batchId">批次ID</param>
        /// <returns>恢復狀態</returns>
        Task<RecoveryStatus> GetRecoveryStatusAsync(Guid batchId);
    }

}