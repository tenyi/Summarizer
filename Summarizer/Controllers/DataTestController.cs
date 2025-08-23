//===============================================================
// 檔案：Controllers/DataTestController.cs
// 說明：提供資料驗證與測試端點（僅限開發環境）。
//===============================================================

using Microsoft.AspNetCore.Mvc;
using Summarizer.Repositories.Interfaces;
using Summarizer.Models;

namespace Summarizer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataTestController : ControllerBase
    {
        private readonly ISummaryRepository _repository;
        private readonly ILogger<DataTestController> _logger;
        private readonly IWebHostEnvironment _environment;

        public DataTestController(
            ISummaryRepository repository, 
            ILogger<DataTestController> logger,
            IWebHostEnvironment environment)
        {
            _repository = repository;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// 檢查資料庫健康狀態
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetDatabaseHealthAsync()
        {
            try
            {
                var isHealthy = await _repository.HealthCheckAsync();
                var response = new
                {
                    Success = isHealthy,
                    Status = isHealthy ? "資料庫連線正常" : "資料庫連線失敗",
                    Timestamp = DateTime.UtcNow
                };
                
                return isHealthy ? Ok(response) : StatusCode(503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料庫健康檢查時發生錯誤");
                return StatusCode(500, new { Error = "健康檢查失敗", Message = ex.Message });
            }
        }

        /// <summary>
        /// 取得資料庫基本統計資訊
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetDatabaseStatisticsAsync()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound("此端點僅在開發環境中可用");
            }

            try
            {
                var totalCount = await _repository.GetTotalCountAsync();
                var recentRecords = await _repository.GetRecentAsync(5);
                
                var statistics = new
                {
                    TotalRecords = totalCount,
                    RecentRecordsCount = recentRecords.Count(),
                    LatestRecord = recentRecords.FirstOrDefault()?.CreatedAt,
                    DatabaseStatus = await _repository.HealthCheckAsync() ? "健康" : "異常",
                    Timestamp = DateTime.UtcNow
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得資料庫統計時發生錯誤");
                return StatusCode(500, new { Error = "取得統計資料失敗", Message = ex.Message });
            }
        }

        /// <summary>
        /// 取得最近的摘要記錄（僅限開發環境）
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentRecordsAsync([FromQuery] int count = 10)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound("此端點僅在開發環境中可用");
            }

            if (count < 1 || count > 100)
            {
                return BadRequest("記錄數量必須在 1 到 100 之間");
            }

            try
            {
                var records = await _repository.GetRecentAsync(count);
                
                // 不回傳完整的原始文本，只回傳摘要資訊
                var sanitizedRecords = records.Select(r => new
                {
                    r.Id,
                    OriginalTextPreview = r.OriginalText.Length > 100 ? 
                        r.OriginalText.Substring(0, 100) + "..." : r.OriginalText,
                    SummaryTextPreview = r.SummaryText.Length > 200 ? 
                        r.SummaryText.Substring(0, 200) + "..." : r.SummaryText,
                    r.CreatedAt,
                    r.UserId,
                    r.OriginalLength,
                    r.SummaryLength,
                    r.ProcessingTimeMs,
                    r.ErrorMessage
                });

                return Ok(new { Records = sanitizedRecords, Count = records.Count() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得最近記錄時發生錯誤");
                return StatusCode(500, new { Error = "取得記錄失敗", Message = ex.Message });
            }
        }

        /// <summary>
        /// 建立測試摘要記錄（僅限開發環境）
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> CreateTestRecordAsync()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound("此端點僅在開發環境中可用");
            }

            try
            {
                var testRecord = new SummaryRecord
                {
                    OriginalText = "這是一個測試原始文本，用於驗證資料庫功能是否正常運作。文本包含足夠的內容來測試各種功能。",
                    SummaryText = "這是一個測試摘要，用於驗證資料庫儲存和查詢功能。",
                    CreatedAt = DateTime.UtcNow,
                    UserId = "test-user",
                    ProcessingTimeMs = 1500.0,
                    ErrorMessage = null
                };

                // 設定正確的長度
                testRecord.OriginalLength = testRecord.OriginalText.Length;
                testRecord.SummaryLength = testRecord.SummaryText.Length;

                var createdRecord = await _repository.CreateAsync(testRecord);
                
                _logger.LogInformation("成功建立測試記錄，ID: {RecordId}", createdRecord.Id);
                
                return Created($"/api/datatest/record/{createdRecord.Id}", new 
                { 
                    Message = "測試記錄建立成功", 
                    RecordId = createdRecord.Id,
                    CreatedAt = createdRecord.CreatedAt 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立測試記錄時發生錯誤");
                return StatusCode(500, new { Error = "建立測試記錄失敗", Message = ex.Message });
            }
        }

        /// <summary>
        /// 驗證資料完整性（僅限開發環境）
        /// </summary>
        [HttpGet("integrity")]
        public async Task<IActionResult> ValidateDataIntegrityAsync()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound("此端點僅在開發環境中可用");
            }

            try
            {
                var recentRecords = await _repository.GetRecentAsync(100);
                var issues = new List<string>();

                foreach (var record in recentRecords)
                {
                    // 驗證必要欄位
                    if (string.IsNullOrWhiteSpace(record.OriginalText))
                        issues.Add($"記錄 {record.Id}: 原始文本為空");

                    // 驗證長度一致性
                    if (record.OriginalLength != record.OriginalText.Length)
                        issues.Add($"記錄 {record.Id}: 原始文本長度不一致 (實際: {record.OriginalText.Length}, 記錄: {record.OriginalLength})");

                    if (record.SummaryLength != record.SummaryText.Length)
                        issues.Add($"記錄 {record.Id}: 摘要文本長度不一致 (實際: {record.SummaryText.Length}, 記錄: {record.SummaryLength})");

                    // 驗證時間合理性
                    if (record.CreatedAt > DateTime.UtcNow)
                        issues.Add($"記錄 {record.Id}: 建立時間在未來");

                    if (record.ProcessingTimeMs < 0)
                        issues.Add($"記錄 {record.Id}: 處理時間為負數");
                }

                var result = new
                {
                    TotalRecordsChecked = recentRecords.Count(),
                    IssuesFound = issues.Count,
                    Issues = issues,
                    Status = issues.Count == 0 ? "資料完整性檢查通過" : "發現資料完整性問題",
                    CheckedAt = DateTime.UtcNow
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料完整性檢查時發生錯誤");
                return StatusCode(500, new { Error = "完整性檢查失敗", Message = ex.Message });
            }
        }

        /// <summary>
        /// 取得特定記錄詳細資訊（僅限開發環境）
        /// </summary>
        [HttpGet("record/{id:int}")]
        public async Task<IActionResult> GetRecordByIdAsync(int id)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound("此端點僅在開發環境中可用");
            }

            try
            {
                var record = await _repository.GetByIdAsync(id);
                
                if (record == null)
                {
                    return NotFound(new { Message = $"找不到 ID 為 {id} 的記錄" });
                }

                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢記錄 {RecordId} 時發生錯誤", id);
                return StatusCode(500, new { Error = "查詢記錄失敗", Message = ex.Message });
            }
        }
    }
}