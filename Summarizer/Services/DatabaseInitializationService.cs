//===============================================================
// 檔案：Services/DatabaseInitializationService.cs
// 說明：負責資料庫初始化和種子資料建立的服務。
//===============================================================

using Microsoft.EntityFrameworkCore;
using Summarizer.Data;
using Summarizer.Models;

namespace Summarizer.Services
{
    public interface IDatabaseInitializationService
    {
        Task InitializeDatabaseAsync();
        Task SeedDataAsync();
        Task<bool> IsDatabaseInitializedAsync();
    }

    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly SummarizerDbContext _context;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            SummarizerDbContext context,
            ILogger<DatabaseInitializationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 初始化資料庫，確保資料庫和資料表都已建立
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("開始初始化資料庫");

                // 確保資料庫已建立
                await _context.Database.EnsureCreatedAsync();

                // 或者使用 Migration（如果有設定）
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("執行待處理的資料庫遷移：{Migrations}", 
                        string.Join(", ", pendingMigrations));
                    await _context.Database.MigrateAsync();
                }

                _logger.LogInformation("資料庫初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "資料庫初始化失敗");
                throw;
            }
        }

        /// <summary>
        /// 建立種子資料（僅在開發環境或資料庫為空時）
        /// </summary>
        public async Task SeedDataAsync()
        {
            try
            {
                // 檢查是否已有資料
                var existingRecordCount = await _context.SummaryRecords.CountAsync();
                
                if (existingRecordCount > 0)
                {
                    _logger.LogInformation("資料庫已有 {Count} 筆記錄，跳過種子資料建立", existingRecordCount);
                    return;
                }

                _logger.LogInformation("開始建立種子資料");

                var seedRecords = new[]
                {
                    new SummaryRecord
                    {
                        OriginalText = "人工智慧（AI）是電腦科學的一個分支，旨在創建能夠執行通常需要人類智慧的任務的系統。這包括學習、推理、問題解決、感知和語言理解等能力。機器學習是AI的一個重要子領域，它使系統能夠自動學習並從經驗中改進，而無需明確編程。深度學習作為機器學習的一個子集，使用神經網路來模擬人腦的工作方式。",
                        SummaryText = "人工智慧是電腦科學分支，創建執行人類智慧任務的系統，包括機器學習和深度學習技術。",
                        CreatedAt = DateTime.UtcNow.AddDays(-7),
                        UserId = "system",
                        OriginalLength = 124,
                        SummaryLength = 41,
                        ProcessingTimeMs = 2300.5
                    },
                    new SummaryRecord
                    {
                        OriginalText = "雲端運算是一種提供可擴展的運算服務（包括伺服器、儲存、資料庫、網路、軟體、分析和智慧）的模型，這些服務透過網路（通常是網際網路）提供。使用者可以按需存取這些資源，而無需直接管理底層的實體基礎設施。主要的雲端服務模型包括基礎設施即服務（IaaS）、平台即服務（PaaS）和軟體即服務（SaaS）。",
                        SummaryText = "雲端運算透過網路提供可擴展的運算服務，包括 IaaS、PaaS 和 SaaS 三種主要服務模型。",
                        CreatedAt = DateTime.UtcNow.AddDays(-5),
                        UserId = "system",
                        OriginalLength = 158,
                        SummaryLength = 45,
                        ProcessingTimeMs = 1890.2
                    },
                    new SummaryRecord
                    {
                        OriginalText = "區塊鏈是一種分散式帳本技術，它維護一個不斷增長的記錄列表，這些記錄被稱為區塊，每個區塊都包含前一個區塊的加密雜湊、時間戳記和交易資料。這種設計使得區塊鏈本質上是抗修改的，一旦資訊被記錄在區塊中，就很難更改。區塊鏈最著名的應用是作為比特幣等加密貨幣的底層技術，但它的應用範圍遠不止於此。",
                        SummaryText = "區塊鏈是分散式帳本技術，具有抗修改特性，最初用於比特幣但應用範圍廣泛。",
                        CreatedAt = DateTime.UtcNow.AddDays(-3),
                        UserId = "system",
                        OriginalLength = 146,
                        SummaryLength = 38,
                        ProcessingTimeMs = 3150.7
                    },
                    new SummaryRecord
                    {
                        OriginalText = "物聯網（IoT）是指日常物品透過網際網路連接並能夠發送和接收資料的概念。這些裝置包括智慧手機、家電、汽車、建築物等。IoT 的目標是讓這些裝置能夠相互通信並與人類互動，創造更智慧、更有效率的環境。然而，IoT 也帶來了安全和隱私方面的挑戰，因為大量的個人和敏感資料在網路上傳輸。",
                        SummaryText = "物聯網讓日常物品透過網際網路連接交換資料，創造智慧環境但也帶來安全隱私挑戰。",
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        UserId = "system",
                        OriginalLength = 132,
                        SummaryLength = 42,
                        ProcessingTimeMs = 2650.3
                    },
                    new SummaryRecord
                    {
                        OriginalText = "資訊安全是保護數位資訊免受未經授權存取、使用、披露、破壞、修改或銷毀的實務。它涵蓋了網路安全、應用程式安全、資訊保證等多個領域。現代資訊安全面臨著各種威脅，包括惡意軟體、釣魚攻擊、資料洩露和進階持續威脅（APT）。有效的資訊安全策略需要技術控制、程序控制和人員意識訓練的結合。",
                        SummaryText = "資訊安全保護數位資訊免受各種威脅，需要結合技術、程序和人員訓練的綜合策略。",
                        CreatedAt = DateTime.UtcNow.AddHours(-6),
                        UserId = "system",
                        OriginalLength = 138,
                        SummaryLength = 40,
                        ProcessingTimeMs = 2890.1
                    }
                };

                // 計算實際長度
                foreach (var record in seedRecords)
                {
                    record.OriginalLength = record.OriginalText.Length;
                    record.SummaryLength = record.SummaryText.Length;
                }

                _context.SummaryRecords.AddRange(seedRecords);
                await _context.SaveChangesAsync();

                _logger.LogInformation("成功建立 {Count} 筆種子資料", seedRecords.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立種子資料失敗");
                throw;
            }
        }

        /// <summary>
        /// 檢查資料庫是否已正確初始化
        /// </summary>
        public async Task<bool> IsDatabaseInitializedAsync()
        {
            try
            {
                // 嘗試連接資料庫並檢查資料表是否存在
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return false;
                }

                // 檢查主要資料表是否存在
                var tableExists = await _context.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SummaryRecords'")
                    .FirstOrDefaultAsync() > 0;

                return tableExists;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "檢查資料庫初始化狀態時發生錯誤");
                return false;
            }
        }
    }
}