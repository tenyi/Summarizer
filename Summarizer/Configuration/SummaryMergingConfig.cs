namespace Summarizer.Configuration;

/// <summary>
/// 摘要合併服務配置
/// </summary>
public class SummaryMergingConfig
{
    /// <summary>
    /// 配置節名稱
    /// </summary>
    public const string SectionName = "SummaryMerging";

    /// <summary>
    /// 預設合併策略
    /// </summary>
    public string DefaultStrategy { get; set; } = "Balanced";

    /// <summary>
    /// 目標長度比例（相對於原文）
    /// </summary>
    public double TargetLengthRatio { get; set; } = 0.6;

    /// <summary>
    /// 品質閾值設定
    /// </summary>
    public QualityThresholdsConfig QualityThresholds { get; set; } = new();

    /// <summary>
    /// 重複內容檢測設定
    /// </summary>
    public DuplicateDetectionConfig DuplicateDetection { get; set; } = new();

    /// <summary>
    /// LLM 輔助設定
    /// </summary>
    public LLMAssistanceConfig LLMAssistance { get; set; } = new();

    /// <summary>
    /// 長度控制設定
    /// </summary>
    public LengthControlConfig LengthControl { get; set; } = new();

    /// <summary>
    /// 每段落最大引用數量
    /// </summary>
    public int MaxReferencesPerParagraph { get; set; } = 3;

    /// <summary>
    /// 最小信心分數閾值
    /// </summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// 最小品質分數閾值
    /// </summary>
    public double MinimumQualityThreshold { get; set; } = 0.75;

    /// <summary>
    /// 最小驗證分數閾值
    /// </summary>
    public double MinimumValidationScore { get; set; } = 0.8;
}

/// <summary>
/// 品質閾值配置
/// </summary>
public class QualityThresholdsConfig
{
    /// <summary>
    /// 最小連貫性分數
    /// </summary>
    public double MinCoherenceScore { get; set; } = 0.7;

    /// <summary>
    /// 最小完整性分數
    /// </summary>
    public double MinCompletenessScore { get; set; } = 0.8;

    /// <summary>
    /// 最小簡潔性分數
    /// </summary>
    public double MinConcisenesScore { get; set; } = 0.6;

    /// <summary>
    /// 最小準確性分數
    /// </summary>
    public double MinAccuracyScore { get; set; } = 0.75;
}

/// <summary>
/// 重複內容檢測配置
/// </summary>
public class DuplicateDetectionConfig
{
    /// <summary>
    /// 相似度閾值
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.8;

    /// <summary>
    /// 是否使用語義相似度
    /// </summary>
    public bool UseSemanticSimilarity { get; set; } = true;

    /// <summary>
    /// 上下文窗口大小
    /// </summary>
    public int ContextWindow { get; set; } = 3;

    /// <summary>
    /// 語義相似度閾值
    /// </summary>
    public double SemanticSimilarityThreshold { get; set; } = 0.75;
}

/// <summary>
/// LLM 輔助配置
/// </summary>
public class LLMAssistanceConfig
{
    /// <summary>
    /// 是否為複雜合併啟用 LLM
    /// </summary>
    public bool EnableForComplexMerges { get; set; } = true;

    /// <summary>
    /// 是否回退到規則式合併
    /// </summary>
    public bool FallbackToRuleBased { get; set; } = true;

    /// <summary>
    /// 最大重試次數
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// 啟用 LLM 的最小分段數量
    /// </summary>
    public int MinSegmentsForLLM { get; set; } = 5;

    /// <summary>
    /// LLM 調用超時（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 長度控制配置
/// </summary>
public class LengthControlConfig
{
    /// <summary>
    /// 最小摘要長度
    /// </summary>
    public int MinLength { get; set; } = 100;

    /// <summary>
    /// 最大摘要長度
    /// </summary>
    public int MaxLength { get; set; } = 2000;

    /// <summary>
    /// 預設目標長度
    /// </summary>
    public int DefaultTargetLength { get; set; } = 800;

    /// <summary>
    /// 長度調整容差（百分比）
    /// </summary>
    public double LengthTolerance { get; set; } = 0.15;
}