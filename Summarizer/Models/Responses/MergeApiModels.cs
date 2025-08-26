using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Models.Responses;

/// <summary>
/// 合併請求
/// </summary>
public class MergeRequest
{
    /// <summary>
    /// 待合併的摘要任務列表
    /// </summary>
    public List<SegmentSummaryTask>? SummaryTasks { get; set; }

    /// <summary>
    /// 合併策略
    /// </summary>
    public MergeStrategy Strategy { get; set; } = MergeStrategy.Balanced;

    /// <summary>
    /// 合併參數
    /// </summary>
    public MergeParameters? Parameters { get; set; }

    /// <summary>
    /// 是否啟用 LLM 輔助
    /// </summary>
    public bool EnableLLMAssist { get; set; } = false;

    /// <summary>
    /// 用戶合併偏好
    /// </summary>
    public UserMergePreferences? UserPreferences { get; set; }

    /// <summary>
    /// 融合策略（當啟用 LLM 時）
    /// </summary>
    public FusionStrategy FusionStrategy { get; set; } = FusionStrategy.Intelligent;
}

/// <summary>
/// 合併 API 回應
/// </summary>
public class MergeApiResponse : ApiResponse
{
    /// <summary>
    /// 合併結果
    /// </summary>
    public MergeResult? MergeResult { get; set; }

    /// <summary>
    /// 處理時間（毫秒）
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// 驗證錯誤列表
    /// </summary>
    public List<string>? ValidationErrors { get; set; }
}

/// <summary>
/// 合併預覽請求
/// </summary>
public class MergePreviewRequest
{
    /// <summary>
    /// 待合併的摘要任務列表
    /// </summary>
    public List<SegmentSummaryTask>? SummaryTasks { get; set; }

    /// <summary>
    /// 用戶合併偏好
    /// </summary>
    public UserMergePreferences? UserPreferences { get; set; }

    /// <summary>
    /// 預覽長度（字數）
    /// </summary>
    public int? PreviewLength { get; set; } = 200;
}

/// <summary>
/// 合併預覽回應
/// </summary>
public class MergePreviewResponse : ApiResponse
{
    /// <summary>
    /// 預覽摘要
    /// </summary>
    public string PreviewSummary { get; set; } = string.Empty;

    /// <summary>
    /// 推薦的合併策略
    /// </summary>
    public MergeStrategy RecommendedStrategy { get; set; }

    /// <summary>
    /// 推薦策略的原因
    /// </summary>
    public string StrategyReason { get; set; } = string.Empty;

    /// <summary>
    /// 內容特徵分析
    /// </summary>
    public ContentCharacteristics? ContentCharacteristics { get; set; }

    /// <summary>
    /// 預估處理時間
    /// </summary>
    public TimeSpan EstimatedProcessingTime { get; set; }

    /// <summary>
    /// 品質分數
    /// </summary>
    public double QualityScore { get; set; }
}

/// <summary>
/// 合併策略回應
/// </summary>
public class MergeStrategiesResponse : ApiResponse
{
    /// <summary>
    /// 可用的策略選項
    /// </summary>
    public List<StrategyOption> Strategies { get; set; } = new();
}

/// <summary>
/// 策略選項
/// </summary>
public class StrategyOption
{
    /// <summary>
    /// 策略類型
    /// </summary>
    public MergeStrategy Strategy { get; set; }

    /// <summary>
    /// 策略顯示名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 策略描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否為推薦策略
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// 推薦原因
    /// </summary>
    public string? RecommendationReason { get; set; }

    /// <summary>
    /// 推薦信心分數
    /// </summary>
    public double? ConfidenceScore { get; set; }
}

/// <summary>
/// 批次合併請求
/// </summary>
public class BatchMergeRequest
{
    /// <summary>
    /// 批次處理 ID
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// 合併策略
    /// </summary>
    public MergeStrategy? Strategy { get; set; }

    /// <summary>
    /// 合併參數
    /// </summary>
    public MergeParameters? Parameters { get; set; }

    /// <summary>
    /// 是否啟用 LLM 輔助
    /// </summary>
    public bool EnableLLMAssist { get; set; } = false;

    /// <summary>
    /// 用戶合併偏好
    /// </summary>
    public UserMergePreferences? UserPreferences { get; set; }

    /// <summary>
    /// 融合策略
    /// </summary>
    public FusionStrategy? FusionStrategy { get; set; }
}

/// <summary>
/// 儲存合併結果請求
/// </summary>
public class SaveMergeResultRequest
{
    /// <summary>
    /// 合併工作 ID
    /// </summary>
    public Guid MergeJobId { get; set; }

    /// <summary>
    /// 合併結果
    /// </summary>
    public MergeResult? MergeResult { get; set; }

    /// <summary>
    /// 儲存選項
    /// </summary>
    public SaveOptions? SaveOptions { get; set; }

    /// <summary>
    /// 用戶 ID（可選）
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 標籤（可選）
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 備註（可選）
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 儲存選項
/// </summary>
public class SaveOptions
{
    /// <summary>
    /// 是否包含來源追溯資訊
    /// </summary>
    public bool IncludeSourceTracking { get; set; } = true;

    /// <summary>
    /// 是否包含品質指標
    /// </summary>
    public bool IncludeQualityMetrics { get; set; } = true;

    /// <summary>
    /// 是否包含處理統計
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// 儲存格式
    /// </summary>
    public SaveFormat Format { get; set; } = SaveFormat.Json;

    /// <summary>
    /// 是否壓縮
    /// </summary>
    public bool Compress { get; set; } = false;
}

/// <summary>
/// 儲存格式
/// </summary>
public enum SaveFormat
{
    /// <summary>
    /// JSON 格式
    /// </summary>
    Json,

    /// <summary>
    /// XML 格式
    /// </summary>
    Xml,

    /// <summary>
    /// 純文字格式
    /// </summary>
    PlainText,

    /// <summary>
    /// Markdown 格式
    /// </summary>
    Markdown
}

/// <summary>
/// 儲存合併結果回應
/// </summary>
public class SaveMergeResultResponse : ApiResponse
{
    /// <summary>
    /// 儲存的結果 ID
    /// </summary>
    public Guid? SavedId { get; set; }

    /// <summary>
    /// 儲存路徑或位置
    /// </summary>
    public string? SavedLocation { get; set; }

    /// <summary>
    /// 檔案大小（位元組）
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// 儲存時間
    /// </summary>
    public DateTime? SavedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 檢索合併結果回應
/// </summary>
public class RetrieveMergeResultResponse : ApiResponse
{
    /// <summary>
    /// 合併結果
    /// </summary>
    public MergeResult? MergeResult { get; set; }

    /// <summary>
    /// 儲存的中繼資料
    /// </summary>
    public SavedResultMetadata? Metadata { get; set; }
}

/// <summary>
/// 儲存結果中繼資料
/// </summary>
public class SavedResultMetadata
{
    /// <summary>
    /// 儲存 ID
    /// </summary>
    public Guid SavedId { get; set; }

    /// <summary>
    /// 合併工作 ID
    /// </summary>
    public Guid MergeJobId { get; set; }

    /// <summary>
    /// 用戶 ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 標籤
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 儲存時間
    /// </summary>
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// 最後存取時間
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// 存取次數
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// 檔案大小
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// 儲存格式
    /// </summary>
    public SaveFormat Format { get; set; }

    /// <summary>
    /// 是否已壓縮
    /// </summary>
    public bool IsCompressed { get; set; }
}

/// <summary>
/// 合併請求驗證結果
/// </summary>
internal class MergeRequestValidation
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 錯誤列表
    /// </summary>
    public List<string> Errors { get; set; } = new();
}