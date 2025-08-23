//===============================================================
// 檔案：Repositories/Interfaces/ISummaryRepository.cs
// 說明：定義摘要記錄儲存庫的介面。
//===============================================================

using Summarizer.Models;

namespace Summarizer.Repositories.Interfaces
{
    public interface ISummaryRepository
    {
        Task<SummaryRecord> CreateAsync(SummaryRecord record);
        Task<SummaryRecord?> GetByIdAsync(int id);
        Task<IEnumerable<SummaryRecord>> GetRecentAsync(int count = 10);
        Task<int> GetTotalCountAsync();
        Task<bool> HealthCheckAsync();
    }
}
