//===============================================================
// 檔案：Program.cs
// 此檔案是 Summarizer 應用程式的入口點，負責設定和啟動 ASP.NET Core 應用程式
// 主要功能：
// - 設定 WebApplication 和各種服務
// - 配置 Kestrel 伺服器以支援大型檔案上傳（最大 4GB）
// - 配置身份驗證和授權
// - 設定 HTTP 請求處理管道
// - 整合前端 Vue 應用與後端 API
//===============================================================

// 引用專案所需的命名空間
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Summarizer.Configuration;
using Summarizer.Models;
using Summarizer.Services;
using Summarizer.Services.Interfaces;
using Summarizer.Repositories;
using Summarizer.Repositories.Interfaces;
using Summarizer.Data;
using Summarizer.Middleware;
using Sinotech.Mis.Extensions.Configuration;
using Summarizer;

// 建立 WebApplication 建構器，配置應用程式基礎設定
// 這是 ASP.NET Core 應用程式的起始點，負責初始化和配置整個應用程式
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,  // 傳遞命令列參數給應用程式
    WebRootPath = "ClientApp/dist" // 設定 WebRootPath，在 Production 模式下會使用此目錄作為靜態檔案的根目錄
                                   // 這個目錄包含 Vue 前端應用程式編譯後的靜態檔案
});

// 設定 Kestrel 的最大請求主體大小為 4GB
// Kestrel 是 ASP.NET Core 的內建跨平台 Web 伺服器
// 由於會議助手需要處理大型音訊和視訊檔案，必須增加默認請求大小限制
builder.WebHost.ConfigureKestrel(options =>
{
    // 設定最大請求主體大小為 4GB = 4 * 1024MB * 1024KB * 1024Bytes
    // 這允許使用者上傳大型的音訊/視訊檔案進行處理
    options.Limits.MaxRequestBodySize = 4L * 1024 * 1024 * 1024;
});

// 將服務添加到依賴注入容器中
// AddControllers() 註冊處理 API 請求所需的 MVC 控制器服務
// 這使得應用程式可以處理來自前端的 API 請求
builder.Services.AddControllers();

// 配置表單選項，設定多部分請求內容大小限制
// FormOptions 決定了 HTTP 表單請求的處理方式，包括檔案上傳限制
builder.Services.Configure<FormOptions>(options =>
{
    // 設定多部分表單內容大小限制約為 4GB
    // 確保表單提交（包括檔案上傳）可以處理大型檔案
    // 注意：此值需要與 Kestrel 的 MaxRequestBodySize 保持一致
    options.MultipartBodyLengthLimit = 4194302000; // 設定限制為約 4 GB
});

// 配置 AI 摘要服務設定
builder.Services.Configure<OllamaConfig>(builder.Configuration.GetSection("OllamaApi"));
builder.Services.Configure<OpenAiConfig>(builder.Configuration.GetSection("OpenAi"));

// 註冊 HTTP 客戶端服務
builder.Services.AddHttpClient<IOllamaSummaryService, OllamaSummaryService>();
builder.Services.AddHttpClient<IOpenAiSummaryService, OpenAiSummaryService>();

// 註冊摘要服務
builder.Services.AddScoped<IOllamaSummaryService, OllamaSummaryService>();
builder.Services.AddScoped<IOpenAiSummaryService, OpenAiSummaryService>();

// 註冊 DbContext 服務，配置 SQLite 資料庫
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SummarizerDbContext>(options =>
    options.UseSqlite(connectionString ?? "Data Source=summarizer.db"));

builder.Services.AddScoped<ISummaryRepository, SummaryRepository>();

// 註冊資料庫初始化服務
builder.Services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();

// 從應用程式配置檔中讀取資料庫連接字串
// Configuration 服務會自動從 appsettings.json、環境變數、命令列參數等處獲取配置
string? connStr = builder.Configuration.GetConnectionString("Summarizer");

// 驗證連接字串是否存在，如果不存在則拋出異常中止應用程式啟動
// 這確保了資料庫配置的正確性是應用程式啟動的必要條件
if (string.IsNullOrEmpty(connStr))
{
    throw new Exception("找不到 Summarizer 的連線字串");
}

// 使用 RSA 演算法解密連接字串
// 加密的連接字串增強了應用程式的安全性，避免資料庫憑證明文儲存
connStr = RsaCrypto.RsaDecrypt(connStr);

// 註冊 DbContext 服務，配置使用 SQL Server 資料庫
// FileLogDbContext 是應用程式與資料庫交互的主要介面
// EntityFrameworkCore 提供 ORM 功能，簡化資料庫操作
builder.Services.AddDbContext<UsageLogDbContext>(opt =>
     opt.UseSqlServer(connStr));  // 使用 SQL Server 作為數據庫提供者

if (OperatingSystemDetector.IsWindows())
{
    // 如果作業系統是 Windows，則新增 Windows 驗證服務。
    // 新增驗證服務，並設定預設的驗證方案為 NegotiateDefaults.AuthenticationScheme。
    // AddNegotiate() 會加入 Negotiate (通常是 Kerberos 或 NTLM) 驗證處理常式。


    // 配置 Windows 身份驗證
    // 使用 Negotiate 身份驗證方案，它支援 NTLM 和 Kerberos 協議
    // 這種身份驗證方式適合於企業內部網環境，允許用戶使用 Windows 帳戶無縫登入
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

    // 設定授權政策
    // 授權決定已驗證身份的用戶可以訪問哪些資源
    builder.Services.AddAuthorization(options =>
    {
        // 設置備用政策為默認政策
        // 這意味著如果端點沒有明確指定授權要求，將使用默認政策
        // 這樣可以確保所有端點都受到基本的授權保護
        options.FallbackPolicy = options.DefaultPolicy;
    });
}

// 配置 Swagger/OpenAPI 文件生成
// Swagger 提供 API 探索和測試介面，簡化 API 開發與測試
// 詳細資訊請參考: https://aka.ms/aspnetcore/swashbuckle

// 添加 API 探索服務
// 這會自動發現應用程式中的 API 端點並生成 OpenAPI 文件
builder.Services.AddEndpointsApiExplorer();

// 添加 Swagger 生成器
// 這會生成 Swagger UI 介面，讓開發人員可以直接在瀏覽器中瀏覽和測試 API
builder.Services.AddSwaggerGen();

// 配置單頁應用程式(SPA)靜態檔案服務
// 在生產環境中，Vue 前端應用編譯後的檔案將從指定目錄提供服務
builder.Services.AddSpaStaticFiles(configuration =>
{
    // 設定 SPA 靜態檔案的根目錄路徑
    // 這裡指向的是 Vue 應用程式經由 Vite 構建後產生的輸出目錄
    // 這樣後端服務器就能直接提供前端應用的靜態資源
    configuration.RootPath = "ClientApp/dist";
});

// 構建 WebApplication 實例
// 通過 Build() 方法完成服務註冊，並建立應用程式實例
// 這標誌著設定階段的結束和中間件配置階段的開始
var app = builder.Build();

// 配置 HTTP 請求處理管道
// 中間件的註冊順序決定了請求處理的順序
// 請求會按順序通過每個中間件，響應則按相反順序通過

// 在開發環境中啟用 Swagger UI
if (app.Environment.IsDevelopment())
{
    // 啟用 Swagger 端點，生成 API 規範文件
    // 這會在 /swagger/v1/swagger.json 提供 OpenAPI 規範
    app.UseSwagger();

    // 啟用 Swagger UI 介面
    // 這會在 /swagger 提供互動式 API 文檔
    // 開發人員可以使用此介面瀏覽和測試 API
    app.UseSwaggerUI();
}

// 啟用 HTTPS 重新導向中間件
// 如果使用者嘗試使用 HTTP 訪問應用，會被自動重定向到 HTTPS
// 這增強了應用程式的安全性，確保所有通訊都是加密的
app.UseHttpsRedirection();

// 使用全域錯誤處理中介軟體
app.UseMiddleware<ErrorHandlerMiddleware>();

// 啟用靜態檔案中間件
// 這使得 wwwroot 目錄下的檔案（如圖片、CSS、JavaScript）可以被直接訪問
// 靜態檔案會繞過 MVC 管道，直接由檔案系統提供
app.UseStaticFiles();

// 啟用 SPA 靜態檔案中間件
// 這使得 ClientApp/dist 目錄下的檔案（Vue 應用的編譯輸出）可以被直接訪問
// 這個中間件專門為單頁應用程式設計，處理前端應用所需的靜態資源
app.UseSpaStaticFiles();

// 啟用路由中間件
// 路由中間件負責解析請求 URL 並將其匹配到對應的端點
// 這是 ASP.NET Core 請求處理管道的核心部分
app.UseRouting();

// 啟用身份驗證中間件
// 這會處理身份驗證令牌（如 Windows 身份驗證憑證）的接收與驗證
// 在請求處理過程中確定用戶的身份
app.UseAuthentication();

// 啟用授權中間件
// 授權中間件確定已驗證的用戶是否有權訪問特定資源
// 它會檢查控制器或動作上的授權屬性和政策
app.UseAuthorization();

// 設定控制器端點路由
// 這會將 HTTP 請求映射到相應的控制器動作
// 所有 API 控制器都會被自動註冊並可被訪問
// 明確使用 UseRouting() 和 UseEndpoints() 的組合，而不是單獨使用 MapControllers()
#pragma warning disable ASP0014 // Suggest using top level route registrations
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
#pragma warning restore ASP0014 // Suggest using top level route registrations

// 配置單頁應用程式 (SPA) 處理
// SPA 中間件用於處理前端路由和後端 API 的整合
app.UseSpa(spa =>
{
    // 在開發環境中使用 Vite 開發伺服器
    // Vite 是一個現代化的前端構建工具，提供快速的熱模組替換(HMR)和優化的開發體驗
    if (app.Environment.IsDevelopment())
    {
        // 設定 Vite 開發伺服器，指向前端專案的根目錄
        // 這使得後端可以代理前端開發伺服器的請求，實現無縫整合
        spa.UseViteDevelopmentServer(sourcePath: "ClientApp");
        // 在生產環境中，會使用前面配置的 SpaStaticFiles 中間件提供靜態文件
    }
});

// 啟動應用程式，開始接收 HTTP 請求
// 這是應用程式的最後一步，會阻塞直到應用程式關閉
app.Run();
