//===============================================================
// 檔案：Repositories/Interfaces/ISummaryRepository.cs
// 說明：定義摘要記錄儲存庫的介面。
//===============================================================

using Summarizer.Models;

namespace Summarizer.Repositories.Interfaces
{
    public interface ISummaryRepository
    {
        /// <summary>
        /// 建立新的摘要記錄。
        /// </summary>
        /// <param name="record">摘要記錄物件</param>
        /// <returns>建立的摘要記錄</returns>
        Task<SummaryRecord> CreateAsync(SummaryRecord record);

        /// <summary>
        /// 根據 ID 取得摘要記錄。
        /// </summary>
        /// <param name="id">摘要記錄的 ID</param>
        /// <returns>摘要記錄，如果不存在則為 null</returns>
        Task<SummaryRecord?> GetByIdAsync(int id);

        /// <summary>
        /// 取得最近的摘要記錄。
        /// </summary>
        /// <param name="count">要取得的記錄數量，預設為 10</param>
        /// <returns>摘要記錄的集合</returns>
        Task<IEnumerable<SummaryRecord>> GetRecentAsync(int count = 10);

        /// <summary>
        /// 取得最舊的摘要記錄。
        /// </summary>
        /// <returns>最舊的摘要記錄，如果不存在則為 null</returns>
        Task<SummaryRecord?> GetOldestAsync();

        /// <summary>
        /// 取得總記錄數量。
        /// </summary>
        /// <returns>總記錄數量</returns>
        Task<int> GetTotalCountAsync();

        /// <summary>
        /// 執行健康檢查。
        /// </summary>
        /// <returns>如果健康則為 true，否則為 false</returns>
        Task<bool> HealthCheckAsync();
    }
}
