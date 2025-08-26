namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 部分結果品質評估資料模型
/// 用於評估不完整摘要的品質和可用性
/// </summary>
public class PartialResultQuality
{
    /// <summary>
    /// 完整性分數（0.0 到 1.0）
    /// 基於已完成分段與總分段的比例，以及內容覆蓋範圍
    /// </summary>
    public double CompletenessScore { get; set; }

    /// <summary>
    /// 是否具有邏輯連貫性
    /// 檢查分段之間的語意連接和流暢性
    /// </summary>
    public bool HasLogicalFlow { get; set; }

    /// <summary>
    /// 語意連貫性分數（0.0 到 1.0）
    /// 衡量已完成分段之間的語意關聯性
    /// </summary>
    public double CoherenceScore { get; set; }

    /// <summary>
    /// 識別出的遺漏主題列表
    /// 分析哪些主要內容可能因未處理的分段而遺漏
    /// </summary>
    public List<string> MissingTopics { get; set; } = new();

    /// <summary>
    /// 品質警告列表
    /// 識別潛在的品質問題，如：不完整、跳躍性、重複內容等
    /// </summary>
    public List<string> QualityWarnings { get; set; } = new();

    /// <summary>
    /// 總體品質等級
    /// </summary>
    public QualityLevel OverallQuality { get; set; } = QualityLevel.Unknown;

    /// <summary>
    /// 推薦動作
    /// 基於品質評估結果推薦用戶應該採取的動作
    /// </summary>
    public RecommendedAction RecommendedAction { get; set; } = RecommendedAction.ReviewRequired;

    /// <summary>
    /// 內容覆蓋率
    /// 已處理內容在原始文本中的分佈情況
    /// </summary>
    public ContentCoverage Coverage { get; set; } = new();

    /// <summary>
    /// 品質評估的詳細說明
    /// </summary>
    public string QualityExplanation { get; set; } = string.Empty;

    /// <summary>
    /// 評估時間
    /// </summary>
    public DateTime AssessmentTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 品質等級枚舉
/// </summary>
public enum QualityLevel
{
    /// <summary>
    /// 未知（尚未評估）
    /// </summary>
    Unknown,

    /// <summary>
    /// 優秀（>= 80% 完整性，高連貫性）
    /// </summary>
    Excellent,

    /// <summary>
    /// 良好（60-79% 完整性，中等連貫性）
    /// </summary>
    Good,

    /// <summary>
    /// 可接受（40-59% 完整性，基本連貫性）
    /// </summary>
    Acceptable,

    /// <summary>
    /// 較差（20-39% 完整性，低連貫性）
    /// </summary>
    Poor,

    /// <summary>
    /// 不可用（< 20% 完整性，無連貫性）
    /// </summary>
    Unusable
}

/// <summary>
/// 推薦動作枚舉
/// </summary>
public enum RecommendedAction
{
    /// <summary>
    /// 建議保存（品質良好）
    /// </summary>
    Recommend,

    /// <summary>
    /// 需要審查（品質中等）
    /// </summary>
    ReviewRequired,

    /// <summary>
    /// 建議丟棄（品質較差）
    /// </summary>
    Discard,

    /// <summary>
    /// 考慮繼續處理（可以恢復處理）
    /// </summary>
    ConsiderContinue
}

/// <summary>
/// 內容覆蓋率資訊
/// </summary>
public class ContentCoverage
{
    /// <summary>
    /// 文本開頭的覆蓋範圍（0.0 到 1.0）
    /// </summary>
    public double BeginningCoverage { get; set; }

    /// <summary>
    /// 文本中間的覆蓋範圍（0.0 到 1.0）
    /// </summary>
    public double MiddleCoverage { get; set; }

    /// <summary>
    /// 文本結尾的覆蓋範圍（0.0 到 1.0）
    /// </summary>
    public double EndCoverage { get; set; }

    /// <summary>
    /// 是否有連續的覆蓋範圍（避免大段落跳躍）
    /// </summary>
    public bool HasContinuousCoverage { get; set; }

    /// <summary>
    /// 最大連續覆蓋範圍的長度（以分段數計算）
    /// </summary>
    public int MaxContinuousLength { get; set; }

    /// <summary>
    /// 覆蓋間隙的數量（未處理分段形成的空隙）
    /// </summary>
    public int CoverageGaps { get; set; }
}