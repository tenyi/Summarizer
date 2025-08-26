namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// 內容最佳化結果
/// </summary>
public class ContentOptimizationResult
{
    /// <summary>
    /// 原始內容
    /// </summary>
    public string OriginalContent { get; set; } = string.Empty;

    /// <summary>
    /// 最佳化後內容
    /// </summary>
    public string OptimizedContent { get; set; } = string.Empty;

    /// <summary>
    /// 原始長度
    /// </summary>
    public int OriginalLength { get; set; }

    /// <summary>
    /// 最終長度
    /// </summary>
    public int FinalLength { get; set; }

    /// <summary>
    /// 目標長度
    /// </summary>
    public int TargetLength { get; set; }

    /// <summary>
    /// 壓縮比率
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// 長度調整策略
    /// </summary>
    public LengthAdjustmentStrategy LengthAdjustmentStrategy { get; set; }

    /// <summary>
    /// 品質評估指標
    /// </summary>
    public OptimizationQualityMetrics QualityMetrics { get; set; } = new();

    /// <summary>
    /// 最佳化開始時間
    /// </summary>
    public DateTime OptimizationStartTime { get; set; }

    /// <summary>
    /// 最佳化結束時間
    /// </summary>
    public DateTime OptimizationEndTime { get; set; }

    /// <summary>
    /// 處理時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 最佳化參數
/// </summary>
public class OptimizationParameters
{
    /// <summary>
    /// 目標長度
    /// </summary>
    public int TargetLength { get; set; } = 800;

    /// <summary>
    /// 長度容差（百分比）
    /// </summary>
    public double LengthTolerance { get; set; } = 0.15;

    /// <summary>
    /// 最小品質分數
    /// </summary>
    public double MinQualityScore { get; set; } = 0.6;

    /// <summary>
    /// 是否保留格式
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;

    /// <summary>
    /// 是否保持關鍵資訊
    /// </summary>
    public bool PreserveKeyInformation { get; set; } = true;

    /// <summary>
    /// 優先保留的內容類型
    /// </summary>
    public List<ContentType> PriorityContentTypes { get; set; } = new();

    /// <summary>
    /// 自訂最佳化偏好
    /// </summary>
    public Dictionary<string, object> CustomPreferences { get; set; } = new();
}

/// <summary>
/// 最佳化品質評估指標
/// </summary>
public class OptimizationQualityMetrics
{
    /// <summary>
    /// 內容保持度（0-1）
    /// </summary>
    public double ContentRetention { get; set; }

    /// <summary>
    /// 流暢度（0-1）
    /// </summary>
    public double Fluency { get; set; }

    /// <summary>
    /// 連貫性（0-1）
    /// </summary>
    public double Coherence { get; set; }

    /// <summary>
    /// 長度達標度（0-1）
    /// </summary>
    public double LengthAccuracy { get; set; }

    /// <summary>
    /// 整體品質分數（0-1）
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 品質問題列表
    /// </summary>
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// 長度品質平衡結果
/// </summary>
public class LengthQualityBalance
{
    /// <summary>
    /// 原始內容
    /// </summary>
    public string OriginalContent { get; set; } = string.Empty;

    /// <summary>
    /// 平衡後內容
    /// </summary>
    public string BalancedContent { get; set; } = string.Empty;

    /// <summary>
    /// 原始長度
    /// </summary>
    public int OriginalLength { get; set; }

    /// <summary>
    /// 最終長度
    /// </summary>
    public int FinalLength { get; set; }

    /// <summary>
    /// 目標長度
    /// </summary>
    public int TargetLength { get; set; }

    /// <summary>
    /// 品質權重
    /// </summary>
    public double QualityWeight { get; set; }

    /// <summary>
    /// 品質分數
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// 長度分數
    /// </summary>
    public double LengthScore { get; set; }

    /// <summary>
    /// 平衡分數
    /// </summary>
    public double BalanceScore { get; set; }

    /// <summary>
    /// 候選版本列表
    /// </summary>
    public List<LengthCandidate> Candidates { get; set; } = new();

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 長度候選版本
/// </summary>
public class LengthCandidate
{
    /// <summary>
    /// 內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 長度
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// 品質分數
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// 長度分數
    /// </summary>
    public double LengthScore { get; set; }

    /// <summary>
    /// 平衡分數
    /// </summary>
    public double BalanceScore { get; set; }

    /// <summary>
    /// 生成方法
    /// </summary>
    public string GenerationMethod { get; set; } = string.Empty;
}

/// <summary>
/// 長度調整策略
/// </summary>
public enum LengthAdjustmentStrategy
{
    /// <summary>
    /// 僅最佳化，不調整長度
    /// </summary>
    OptimizeOnly = 1,

    /// <summary>
    /// 中度壓縮
    /// </summary>
    ModerateCompression = 2,

    /// <summary>
    /// 激進壓縮
    /// </summary>
    AggressiveCompression = 3,

    /// <summary>
    /// 中度擴展
    /// </summary>
    ModerateExpansion = 4,

    /// <summary>
    /// 顯著擴展
    /// </summary>
    SignificantExpansion = 5
}

/// <summary>
/// 壓縮程度
/// </summary>
public enum CompressionLevel
{
    /// <summary>
    /// 輕度壓縮
    /// </summary>
    Light = 1,

    /// <summary>
    /// 平衡壓縮
    /// </summary>
    Balanced = 2,

    /// <summary>
    /// 激進壓縮
    /// </summary>
    Aggressive = 3
}

/// <summary>
/// 內容類型
/// </summary>
public enum ContentType
{
    /// <summary>
    /// 主要觀點
    /// </summary>
    MainPoints = 1,

    /// <summary>
    /// 關鍵細節
    /// </summary>
    KeyDetails = 2,

    /// <summary>
    /// 支撐資料
    /// </summary>
    SupportingData = 3,

    /// <summary>
    /// 範例說明
    /// </summary>
    Examples = 4,

    /// <summary>
    /// 結論總結
    /// </summary>
    Conclusions = 5,

    /// <summary>
    /// 引用資料
    /// </summary>
    Citations = 6
}