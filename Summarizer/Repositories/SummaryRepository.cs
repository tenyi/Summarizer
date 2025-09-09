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
    /// <summary>
    /// 摘要記錄儲存庫類別，提供摘要記錄的 CRUD 操作和資料庫健康檢查功能。
    /// 實作了重試機制以處理資料庫操作的暫時性錯誤。
    /// </summary>
    public class SummaryRepository : ISummaryRepository
    {
        private readonly SummarizerDbContext _context;
        private readonly ILogger<SummaryRepository> _logger;

        /// <summary>
        /// 初始化 SummaryRepository 實例。
        /// </summary>
        /// <param name="context">資料庫上下文物件</param>
        /// <param name="logger">日誌記錄器</param>
        public SummaryRepository(SummarizerDbContext context, ILogger<SummaryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 非同步建立新的摘要記錄，並實作重試機制以處理資料庫更新異常。
        /// </summary>
        /// <param name="record">要建立的摘要記錄物件</param>
        /// <returns>建立成功的摘要記錄</returns>
        /// <exception cref="InvalidOperationException">當重試次數用盡仍無法建立記錄時拋出</exception>
        public async Task<SummaryRecord> CreateAsync(SummaryRecord record)
        {
            const int maxRetries = 3; // 最大重試次數
            const int baseDelayMs = 1000; // 基礎延遲時間（毫秒）

            // 實作指數退避重試機制
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

        /// <summary>
        /// 根據 ID 非同步查詢摘要記錄，並實作重試機制以處理查詢異常。
        /// </summary>
        /// <param name="id">摘要記錄的唯一識別碼</param>
        /// <returns>找到的摘要記錄，若不存在則返回 null</returns>
        /// <exception cref="InvalidOperationException">當重試次數用盡仍無法查詢記錄時拋出</exception>
        public async Task<SummaryRecord?> GetByIdAsync(int id)
        {
            const int maxRetries = 3; // 最大重試次數
            const int baseDelayMs = 500; // 基礎延遲時間（毫秒）

            // 實作指數退避重試機制
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

        /// <summary>
        /// 非同步查詢最近建立的摘要記錄，並實作重試機制以處理查詢異常。
        /// </summary>
        /// <param name="count">要查詢的記錄數量，預設為 10</param>
        /// <returns>最近建立的摘要記錄集合</returns>
        /// <exception cref="InvalidOperationException">當重試次數用盡仍無法查詢記錄時拋出</exception>
        public async Task<IEnumerable<SummaryRecord>> GetRecentAsync(int count = 10)
        {
            const int maxRetries = 3; // 最大重試次數
            const int baseDelayMs = 500; // 基礎延遲時間（毫秒）

            // 實作指數退避重試機制
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

        /// <summary>
        /// 非同步查詢最舊的摘要記錄，並實作重試機制以處理查詢異常。
        /// </summary>
        /// <returns>最舊的摘要記錄，若無記錄則返回 null</returns>
        /// <exception cref="InvalidOperationException">當重試次數用盡仍無法查詢記錄時拋出</exception>
        public async Task<SummaryRecord?> GetOldestAsync()
        {
            const int maxRetries = 3; // 最大重試次數
            const int baseDelayMs = 500; // 基礎延遲時間（毫秒）

            // 實作指數退避重試機制
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var result = await _context.SummaryRecords
                        .OrderBy(r => r.CreatedAt)
                        .FirstOrDefaultAsync();
                    
                    _logger.LogDebug("查詢最舊的摘要記錄，結果: {Found}", result != null ? $"ID {result.Id}" : "未找到");
                    return result;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("查詢最舊摘要記錄失敗 (嘗試 {Attempt}/{MaxRetries}): {Error}", 
                        attempt + 1, maxRetries, ex.Message);
                    
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "查詢最舊摘要記錄時發生錯誤");
                    throw;
                }
            }
            
            throw new InvalidOperationException($"經過 {maxRetries} 次重試後仍無法查詢最舊記錄");
        }

        /// <summary>
        /// 非同步查詢摘要記錄的總數量，並實作重試機制以處理查詢異常。
        /// </summary>
        /// <returns>摘要記錄的總數量</returns>
        /// <exception cref="InvalidOperationException">當重試次數用盡仍無法查詢記錄總數時拋出</exception>
        public async Task<int> GetTotalCountAsync()
        {
            const int maxRetries = 3; // 最大重試次數
            const int baseDelayMs = 500; // 基礎延遲時間（毫秒）

            // 實作指數退避重試機制
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

        /// <summary>
        /// 非同步執行資料庫健康檢查，驗證資料庫連接和基本查詢功能。
        /// </summary>
        /// <returns>若資料庫正常運作則返回 true，否則返回 false</returns>
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
