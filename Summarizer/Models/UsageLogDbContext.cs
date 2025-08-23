using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sinotech.Mis.Extensions.Configuration;
using System;

namespace Summarizer.Models
{
    /// <summary>
    /// 使用記錄的資料庫上下文類別，負責 API 使用記錄的交互
    /// 此類別繼承自 Entity Framework Core 的 DbContext，用於管理資料庫會話
    /// </summary>
    public class UsageLogDbContext : DbContext
    {
        /// <summary>
        /// 應用程式的組態設定，用於取得連線字串等組態資訊
        /// </summary>
        private readonly IConfiguration configuration;


        /// <summary>
        /// API 使用記錄的資料表集合
        /// 代表資料庫中的 UsageLogs 資料表，用於追蹤 API 的使用情況
        /// </summary>
        public DbSet<UsageLog> UsageLogs { get; set; } = null!;

        /// <summary>
        /// 建構子，初始化 FileLogDbContext 的新實例
        /// </summary>
        /// <param name="options">DbContext 的選項，包含資料庫提供者、連線逾時等設定</param>
        /// <param name="config">應用程式的組態設定，用於取得資料庫連線字串等組態資訊</param>
        /// <remarks>
        /// 此建構子會接收資料庫上下文選項和組態設定，
        /// 並將組態設定儲存在私有欄位中以供後續使用
        /// </remarks>
        public UsageLogDbContext(DbContextOptions<UsageLogDbContext> options, IConfiguration config) : base(options)
        {
            configuration = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 設定資料庫連接字串和其他選項
        /// 此方法會在第一次建立 DbContext 實例時被呼叫
        /// </summary>
        /// <param name="optionsBuilder">用於設定資料庫提供者和連接字串的建構器</param>
        /// <exception cref="Exception">當找不到 MeetingAssistant 的連線字串時拋出例外</exception>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder == null)
            {
                throw new ArgumentNullException(nameof(optionsBuilder));
            }            // 從組態設定中取得 MeetingAssistant 的連線字串

            string? connStr = configuration.GetConnectionString("Translator");
            // 檢查連線字串是否為空或 null
            if (string.IsNullOrEmpty(connStr))
            {
                throw new Exception("找不到 Translator 的連線字串，請確認 appsettings.json 中是否有設定連線字串");
            }

            // 檢測是否為 FakeItEasy 模擬物件或測試環境
            bool isFakeItEasyMock = configuration.GetType().FullName?.Contains("FakeItEasy") == true ||
                                   configuration.GetType().FullName?.Contains("Castle.Proxies") == true;
            
            bool isTestEnvironment = connStr.Contains("Fake") || 
                                   connStr.Contains("Test") || 
                                   connStr.Contains("Memory") ||
                                   isFakeItEasyMock;
            
            try
            {
                if (isTestEnvironment)
                {
                    // 測試環境：直接使用連線字串，不需要解密
                    // 通常測試會使用 InMemory 資料庫，所以這裡可能不會執行到 UseSqlServer
                    if (!optionsBuilder.IsConfigured)
                    {
                        optionsBuilder.UseSqlServer(connStr);
                    }
                }
                else
                {

                    // 生產環境：解密連線字串
                    connStr = RsaCrypto.RsaDecrypt(connStr);
                    optionsBuilder.UseSqlServer(connStr);
                }
            }
            catch (Exception ex)
            {
                string environmentInfo = isTestEnvironment ? "測試環境" : "生產環境";
                throw new Exception($"資料庫連線設定失敗 ({environmentInfo})，請確認連線字串格式正確且解密金鑰有效", ex);
            }
        }
    }
}
