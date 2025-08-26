using Summarizer.Models.BatchProcessing;

namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// 策略推薦結果
/// </summary>
public class StrategyRecommendation
{
    /// <summary>
    /// 推薦的策略
    /// </summary>
    public MergeStrategy RecommendedStrategy { get; set; }

    /// <summary>
    /// 推薦的合併方法
    /// </summary>
    public MergeMethod RecommendedMethod { get; set; }

    /// <summary>
    /// 推薦的參數設定
    /// </summary>
    public MergeParameters Parameters { get; set; } = new();

    /// <summary>
    /// 推薦信心度 (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// 推薦信心分數 (0-1) - 別名
    /// </summary>
    public double ConfidenceScore => Confidence;

    /// <summary>
    /// 推薦原因說明
    /// </summary>
    public List<string> Reasons { get; set; } = new();
    
    /// <summary>
    /// 推薦原因 - 別名
    /// </summary>
    public string Reason => string.Join("; ", Reasons);

    /// <summary>
    /// 替代策略選項
    /// </summary>
    public List<AlternativeStrategy> Alternatives { get; set; } = new();
}

/// <summary>
/// 策略評估結果
/// </summary>
public class StrategyEvaluation
{
    /// <summary>
    /// 策略類型
    /// </summary>
    public MergeStrategy Strategy { get; set; }

    /// <summary>
    /// 適用性分數 (0-1)
    /// </summary>
    public double SuitabilityScore { get; set; }

    /// <summary>
    /// 預估品質分數 (0-1)
    /// </summary>
    public double EstimatedQuality { get; set; }

    /// <summary>
    /// 處理效率評估 (0-1)
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// 優點列表
    /// </summary>
    public List<string> Advantages { get; set; } = new();

    /// <summary>
    /// 缺點列表
    /// </summary>
    public List<string> Disadvantages { get; set; } = new();

    /// <summary>
    /// 適用場景
    /// </summary>
    public List<string> SuitableScenarios { get; set; } = new();
}

/// <summary>
/// 替代策略選項
/// </summary>
public class AlternativeStrategy
{
    /// <summary>
    /// 策略類型
    /// </summary>
    public MergeStrategy Strategy { get; set; }

    /// <summary>
    /// 合併方法
    /// </summary>
    public MergeMethod Method { get; set; }

    /// <summary>
    /// 參數設定
    /// </summary>
    public MergeParameters Parameters { get; set; } = new();

    /// <summary>
    /// 評分 (0-1)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 選擇此策略的原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 使用者合併偏好設定
/// </summary>
public class UserMergePreferences
{
    /// <summary>
    /// 偏好的輸出長度
    /// </summary>
    public OutputLengthPreference LengthPreference { get; set; } = OutputLengthPreference.Medium;

    /// <summary>
    /// 詳細程度偏好
    /// </summary>
    public DetailLevelPreference DetailLevel { get; set; } = DetailLevelPreference.Balanced;

    /// <summary>
    /// 結構化程度偏好
    /// </summary>
    public StructureLevelPreference StructureLevel { get; set; } = StructureLevelPreference.Moderate;

    /// <summary>
    /// 是否保留來源資訊
    /// </summary>
    public bool PreserveSourceInfo { get; set; } = true;

    /// <summary>
    /// 是否優先保留關鍵資訊
    /// </summary>
    public bool PrioritizeKeyInfo { get; set; } = true;

    /// <summary>
    /// 容忍重複的程度
    /// </summary>
    public DuplicateToleranceLevel DuplicateTolerance { get; set; } = DuplicateToleranceLevel.Low;

    /// <summary>
    /// 自訂權重設定
    /// </summary>
    public Dictionary<string, double> CustomWeights { get; set; } = new();

    /// <summary>
    /// 特殊處理偏好
    /// </summary>
    public List<SpecialHandlingPreference> SpecialHandlings { get; set; } = new();

    /// <summary>
    /// 使用者 ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 偏好建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 內容特徵分析結果
/// </summary>
public class ContentCharacteristics
{
    /// <summary>
    /// 內容總數
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 平均長度
    /// </summary>
    public double AverageLength { get; set; }

    /// <summary>
    /// 長度變異度
    /// </summary>
    public double LengthVariance { get; set; }

    /// <summary>
    /// 主題多樣性 (0-1)
    /// </summary>
    public double TopicDiversity { get; set; }

    /// <summary>
    /// 內容重疊度 (0-1)
    /// </summary>
    public double ContentOverlap { get; set; }

    /// <summary>
    /// 結構化程度 (0-1)
    /// </summary>
    public double StructureLevel { get; set; }

    /// <summary>
    /// 複雜性評估 (0-1)
    /// </summary>
    public double ComplexityScore { get; set; }

    /// <summary>
    /// 主要主題清單
    /// </summary>
    public List<string> MainTopics { get; set; } = new();

    /// <summary>
    /// 內容類型分佈
    /// </summary>
    public Dictionary<string, double> ContentTypeDistribution { get; set; } = new();

    /// <summary>
    /// 語言特徵
    /// </summary>
    public Dictionary<string, double> LanguageFeatures { get; set; } = new();
}

/// <summary>
/// 使用者回饋
/// </summary>
public class UserFeedback
{
    /// <summary>
    /// 整體滿意度 (1-5)
    /// </summary>
    public int OverallSatisfaction { get; set; }

    /// <summary>
    /// 長度評價 (1-5)
    /// </summary>
    public int LengthRating { get; set; }

    /// <summary>
    /// 內容品質評價 (1-5)
    /// </summary>
    public int QualityRating { get; set; }

    /// <summary>
    /// 結構評價 (1-5)
    /// </summary>
    public int StructureRating { get; set; }

    /// <summary>
    /// 具體意見
    /// </summary>
    public string Comments { get; set; } = string.Empty;

    /// <summary>
    /// 偏好的改進建議
    /// </summary>
    public List<ImprovementSuggestion> Suggestions { get; set; } = new();

    /// <summary>
    /// 回饋時間
    /// </summary>
    public DateTime FeedbackTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 特殊處理偏好
/// </summary>
public class SpecialHandlingPreference
{
    /// <summary>
    /// 處理類型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 處理參數
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 改進建議
/// </summary>
public class ImprovementSuggestion
{
    /// <summary>
    /// 建議類型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 建議描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 優先級 (1-5)
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// 輸出長度偏好
/// </summary>
public enum OutputLengthPreference
{
    /// <summary>
    /// 極短
    /// </summary>
    VeryShort = 1,

    /// <summary>
    /// 短
    /// </summary>
    Short = 2,

    /// <summary>
    /// 中等
    /// </summary>
    Medium = 3,

    /// <summary>
    /// 長
    /// </summary>
    Long = 4,

    /// <summary>
    /// 極長
    /// </summary>
    VeryLong = 5
}

/// <summary>
/// 詳細程度偏好
/// </summary>
public enum DetailLevelPreference
{
    /// <summary>
    /// 高度簡化
    /// </summary>
    HighlySimplified = 1,

    /// <summary>
    /// 簡化
    /// </summary>
    Simplified = 2,

    /// <summary>
    /// 平衡
    /// </summary>
    Balanced = 3,

    /// <summary>
    /// 詳細
    /// </summary>
    Detailed = 4,

    /// <summary>
    /// 非常詳細
    /// </summary>
    VeryDetailed = 5
}

/// <summary>
/// 結構化程度偏好
/// </summary>
public enum StructureLevelPreference
{
    /// <summary>
    /// 無結構
    /// </summary>
    Unstructured = 1,

    /// <summary>
    /// 輕微結構化
    /// </summary>
    LightlyStructured = 2,

    /// <summary>
    /// 適度結構化
    /// </summary>
    Moderate = 3,

    /// <summary>
    /// 高度結構化
    /// </summary>
    HighlyStructured = 4,

    /// <summary>
    /// 完全結構化
    /// </summary>
    FullyStructured = 5
}

/// <summary>
/// 重複容忍程度
/// </summary>
public enum DuplicateToleranceLevel
{
    /// <summary>
    /// 零容忍
    /// </summary>
    None = 1,

    /// <summary>
    /// 低容忍
    /// </summary>
    Low = 2,

    /// <summary>
    /// 中等容忍
    /// </summary>
    Medium = 3,

    /// <summary>
    /// 高容忍
    /// </summary>
    High = 4,

    /// <summary>
    /// 允許重複
    /// </summary>
    Permissive = 5
}