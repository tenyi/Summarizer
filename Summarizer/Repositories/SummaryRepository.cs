//===============================================================
// 檔案：Repositories/SummaryRepository.cs
// 說明：實作摘要記錄儲存庫。
//===============================================================

using Microsoft.EntityFrameworkCore;
using Summarizer.Data;
using Summarizer.Models;
using Summarizer.Repositories.Interfaces;

namespace Summarizer.Repositories
{
    public class SummaryRepository : ISummaryRepository
    {
        private readonly SummarizerDbContext _context;
        private readonly ILogger<SummaryRepository> _logger;

        public SummaryRepository(SummarizerDbContext context, ILogger<SummaryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SummaryRecord> CreateAsync(SummaryRecord record)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _context.SummaryRecords.Add(record);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("成功建立摘要記錄，ID: {RecordId}", record.Id);
                    return record;
                }
                catch (DbUpdateException ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("建立摘要記錄失敗 (嘗試 {Attempt}/{MaxRetries}): {Error}", 
                        attempt + 1, maxRetries, ex.Message);
                    
                    // 指數退避延遲
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                    
                    // 重置 DbContext 狀態
                    _context.Entry(record).State = EntityState.Detached;
                    _context.SummaryRecords.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "建立摘要記錄時發生未預期的錯誤");
                    throw;
                }
            }
            
            throw new InvalidOperationException($"經過 {maxRetries} 次重試後仍無法建立摘要記錄");
        }

        public async Task<SummaryRecord?> GetByIdAsync(int id)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 500;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var result = await _context.SummaryRecords.FindAsync(id);
                    _logger.LogDebug("查詢摘要記錄 ID: {RecordId}，結果: {Found}", id, result != null ? "找到" : "未找到");
                    return result;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("查詢摘要記錄失敗 (嘗試 {Attempt}/{MaxRetries}): {Error}", 
                        attempt + 1, maxRetries, ex.Message);
                    
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "查詢摘要記錄 ID: {RecordId} 時發生錯誤", id);
                    throw;
                }
            }
            
            throw new InvalidOperationException($"經過 {maxRetries} 次重試後仍無法查詢記錄");
        }

        public async Task<IEnumerable<SummaryRecord>> GetRecentAsync(int count = 10)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 500;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var results = await _context.SummaryRecords
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(count)
                        .ToListAsync();
                    
                    _logger.LogDebug("查詢最近的 {Count} 筆摘要記錄，實際取得 {ActualCount} 筆", count, results.Count);
                    return results;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("查詢最近摘要記錄失敗 (嘗試 {Attempt}/{MaxRetries}): {Error}", 
                        attempt + 1, maxRetries, ex.Message);
                    
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "查詢最近摘要記錄時發生錯誤");
                    throw;
                }
            }
            
            throw new InvalidOperationException($"經過 {maxRetries} 次重試後仍無法查詢記錄");
        }

        public async Task<int> GetTotalCountAsync()
        {
            const int maxRetries = 3;
            const int baseDelayMs = 500;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var count = await _context.SummaryRecords.CountAsync();
                    _logger.LogDebug("查詢摘要記錄總數: {TotalCount}", count);
                    return count;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("查詢摘要記錄總數失敗 (嘗試 {Attempt}/{MaxRetries}): {Error}", 
                        attempt + 1, maxRetries, ex.Message);
                    
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "查詢摘要記錄總數時發生錯誤");
                    throw;
                }
            }
            
            throw new InvalidOperationException($"經過 {maxRetries} 次重試後仍無法查詢記錄總數");
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                _logger.LogDebug("開始執行資料庫健康檢查");
                
                // 嘗試連接資料庫
                var canConnect = await _context.Database.CanConnectAsync();
                
                if (canConnect)
                {
                    // 如果可以連接，嘗試執行簡單查詢來確認資料庫運作正常
                    var count = await _context.SummaryRecords.CountAsync();
                    _logger.LogDebug("資料庫健康檢查通過，目前有 {Count} 筆摘要記錄", count);
                    return true;
                }
                else
                {
                    _logger.LogWarning("資料庫健康檢查失敗：無法連接到資料庫");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料庫健康檢查時發生錯誤");
                return false;
            }
        }
    }
}
