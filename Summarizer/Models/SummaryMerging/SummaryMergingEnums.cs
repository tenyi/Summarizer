namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// 合併策略
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// 簡潔式：重點摘取
    /// </summary>
    Concise = 1,

    /// <summary>
    /// 詳細式：保留細節
    /// </summary>
    Detailed = 2,

    /// <summary>
    /// 結構化：分類整理
    /// </summary>
    Structured = 3,

    /// <summary>
    /// 平衡式：長度與品質平衡
    /// </summary>
    Balanced = 4,

    /// <summary>
    /// 自訂式：使用者自訂參數
    /// </summary>
    Custom = 5
}

/// <summary>
/// 合併方法
/// </summary>
public enum MergeMethod
{
    /// <summary>
    /// 規則式合併
    /// </summary>
    RuleBased = 1,

    /// <summary>
    /// 統計式合併
    /// </summary>
    Statistical = 2,

    /// <summary>
    /// LLM 輔助合併
    /// </summary>
    LLMAssisted = 3,

    /// <summary>
    /// 混合式合併
    /// </summary>
    Hybrid = 4
}

/// <summary>
/// 合併狀態
/// </summary>
public enum MergeStatus
{
    /// <summary>
    /// 等待處理
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 處理中
    /// </summary>
    Processing = 2,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 處理失敗
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 5
}

/// <summary>
/// 品質問題類型
/// </summary>
public enum QualityIssueType
{
    /// <summary>
    /// 連貫性問題
    /// </summary>
    Coherence = 1,

    /// <summary>
    /// 完整性問題
    /// </summary>
    Completeness = 2,

    /// <summary>
    /// 簡潔性問題
    /// </summary>
    Conciseness = 3,

    /// <summary>
    /// 準確性問題
    /// </summary>
    Accuracy = 4,

    /// <summary>
    /// 重複內容
    /// </summary>
    Duplication = 5,

    /// <summary>
    /// 結構問題
    /// </summary>
    Structure = 6,

    /// <summary>
    /// 語言表達問題
    /// </summary>
    Expression = 7
}

/// <summary>
/// 品質問題嚴重程度
/// </summary>
public enum QualityIssueSeverity
{
    /// <summary>
    /// 低度：建議修正
    /// </summary>
    Low = 1,

    /// <summary>
    /// 中度：應該修正
    /// </summary>
    Medium = 2,

    /// <summary>
    /// 高度：必須修正
    /// </summary>
    High = 3,

    /// <summary>
    /// 嚴重：影響使用
    /// </summary>
    Critical = 4
}

/// <summary>
/// 文本相似度類型
/// </summary>
public enum SimilarityType
{
    /// <summary>
    /// Jaccard 相似度
    /// </summary>
    Jaccard = 1,

    /// <summary>
    /// 餘弦相似度
    /// </summary>
    Cosine = 2,

    /// <summary>
    /// 語義相似度
    /// </summary>
    Semantic = 3,

    /// <summary>
    /// 編輯距離
    /// </summary>
    EditDistance = 4,

    /// <summary>
    /// TF-IDF 相似度
    /// </summary>
    TFIDF = 5
}