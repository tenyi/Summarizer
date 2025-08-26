using Summarizer.Models.BatchProcessing;

namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// 摘要合併器
/// </summary>
public class SummaryMerger
{
    /// <summary>
    /// 合併作業 ID
    /// </summary>
    public Guid MergeJobId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 輸入摘要列表
    /// </summary>
    public List<SegmentSummaryTask> InputSummaries { get; set; } = new();

    /// <summary>
    /// 合併策略
    /// </summary>
    public MergeStrategy Strategy { get; set; } = MergeStrategy.Balanced;

    /// <summary>
    /// 合併參數
    /// </summary>
    public MergeParameters Parameters { get; set; } = new();

    /// <summary>
    /// 合併結果
    /// </summary>
    public MergeResult? Result { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 處理狀態
    /// </summary>
    public MergeStatus Status { get; set; } = MergeStatus.Pending;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 合併結果
/// </summary>
public class MergeResult
{
    /// <summary>
    /// 合併任務 ID
    /// </summary>
    public Guid MergeJobId { get; set; }

    /// <summary>
    /// 最終摘要
    /// </summary>
    public string FinalSummary { get; set; } = string.Empty;

    /// <summary>
    /// 來源映射關係
    /// </summary>
    public List<MergeSourceMapping> SourceMappings { get; set; } = new();

    /// <summary>
    /// 品質評估指標
    /// </summary>
    public MergeQualityMetrics QualityMetrics { get; set; } = new();

    /// <summary>
    /// 合併統計資訊
    /// </summary>
    public MergeStatistics Statistics { get; set; } = new();

    /// <summary>
    /// 應用的合併策略
    /// </summary>
    public MergeStrategy AppliedStrategy { get; set; }

    /// <summary>
    /// 應用的合併方法
    /// </summary>
    public MergeMethod AppliedMethod { get; set; }

    /// <summary>
    /// 處理時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// 完成時間
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// 來源映射關係
/// </summary>
public class MergeSourceMapping
{
    /// <summary>
    /// 段落索引
    /// </summary>
    public int ParagraphIndex { get; set; }

    /// <summary>
    /// 段落內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 來源分段索引列表
    /// </summary>
    public List<int> SourceSegmentIndices { get; set; } = new();

    /// <summary>
    /// 信心分數
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// 使用的合併方法
    /// </summary>
    public MergeMethod Method { get; set; }

    /// <summary>
    /// 重要性分數
    /// </summary>
    public double ImportanceScore { get; set; }

    /// <summary>
    /// 是否為關鍵資訊
    /// </summary>
    public bool IsKeyInformation { get; set; }
}

/// <summary>
/// 合併參數
/// </summary>
public class MergeParameters
{
    /// <summary>
    /// 目標總結長度
    /// </summary>
    public int TargetLength { get; set; } = 800;

    /// <summary>
    /// 相似度閾值
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.8;

    /// <summary>
    /// 是否保留結構
    /// </summary>
    public bool PreserveStructure { get; set; } = true;

    /// <summary>
    /// 是否啟用 LLM 輔助
    /// </summary>
    public bool EnableLLMAssist { get; set; } = true;

    /// <summary>
    /// 重要性閾值
    /// </summary>
    public double ImportanceThreshold { get; set; } = 0.6;

    /// <summary>
    /// 是否生成來源引用
    /// </summary>
    public bool GenerateSourceReferences { get; set; } = true;

    /// <summary>
    /// 是否去除重複內容
    /// </summary>
    public bool RemoveDuplicates { get; set; } = true;

    /// <summary>
    /// 使用者自訂偏好
    /// </summary>
    public Dictionary<string, object> CustomPreferences { get; set; } = new();
}

/// <summary>
/// 品質評估指標
/// </summary>
public class MergeQualityMetrics
{
    /// <summary>
    /// 連貫性分數
    /// </summary>
    public double CoherenceScore { get; set; }

    /// <summary>
    /// 完整性分數
    /// </summary>
    public double CompletenessScore { get; set; }

    /// <summary>
    /// 簡潔性分數
    /// </summary>
    public double ConcisenesScore { get; set; }

    /// <summary>
    /// 準確性分數
    /// </summary>
    public double AccuracyScore { get; set; }

    /// <summary>
    /// 整體品質分數
    /// </summary>
    public double OverallQuality { get; set; }

    /// <summary>
    /// 品質問題列表
    /// </summary>
    public List<QualityIssue> Issues { get; set; } = new();

    /// <summary>
    /// 評估時間
    /// </summary>
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 品質問題
/// </summary>
public class QualityIssue
{
    /// <summary>
    /// 問題類型
    /// </summary>
    public QualityIssueType Type { get; set; }

    /// <summary>
    /// 問題描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 嚴重程度
    /// </summary>
    public QualityIssueSeverity Severity { get; set; }

    /// <summary>
    /// 位置資訊
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 建議修正方式
    /// </summary>
    public string? Suggestion { get; set; }
}

/// <summary>
/// 合併統計資訊
/// </summary>
public class MergeStatistics
{
    /// <summary>
    /// 輸入分段數量
    /// </summary>
    public int InputSegmentCount { get; set; }

    /// <summary>
    /// 輸出段落數量
    /// </summary>
    public int OutputParagraphCount { get; set; }

    /// <summary>
    /// 原始總長度
    /// </summary>
    public int OriginalLength { get; set; }

    /// <summary>
    /// 最終總長度
    /// </summary>
    public int FinalLength { get; set; }

    /// <summary>
    /// 壓縮比率
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// 移除的重複內容數量
    /// </summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>
    /// 關鍵詞密度
    /// </summary>
    public Dictionary<string, double> KeywordDensity { get; set; } = new();

    /// <summary>
    /// 處理時間統計
    /// </summary>
    public Dictionary<string, TimeSpan> ProcessingTimes { get; set; } = new();

    /// <summary>
    /// 保留的關鍵資訊數量
    /// </summary>
    public int KeyInformationRetained { get; set; }
}