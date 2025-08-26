using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 重複內容檢測器介面
/// </summary>
public interface IDuplicateContentDetector
{
    /// <summary>
    /// 檢測摘要列表中的重複內容
    /// </summary>
    /// <param name="summaries">摘要列表</param>
    /// <param name="parameters">檢測參數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重複內容檢測結果</returns>
    Task<DuplicateDetectionResult> DetectDuplicatesAsync(
        List<SegmentSummaryTask> summaries,
        DuplicateDetectionParameters? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除重複內容並返回最佳版本
    /// </summary>
    /// <param name="summaries">摘要列表</param>
    /// <param name="parameters">檢測參數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>去重後的摘要列表</returns>
    Task<List<SegmentSummaryTask>> RemoveDuplicatesAsync(
        List<SegmentSummaryTask> summaries,
        DuplicateDetectionParameters? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 識別近義詞和改寫內容
    /// </summary>
    /// <param name="summaries">摘要列表</param>
    /// <param name="semanticThreshold">語義相似度閾值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>近義詞組列表</returns>
    Task<List<SynonymGroup>> IdentifySynonymContentAsync(
        List<SegmentSummaryTask> summaries,
        double semanticThreshold = 0.75,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 重複內容檢測結果
/// </summary>
public class DuplicateDetectionResult
{
    /// <summary>
    /// 原始摘要數量
    /// </summary>
    public int OriginalCount { get; set; }

    /// <summary>
    /// 去重後數量
    /// </summary>
    public int FinalCount { get; set; }

    /// <summary>
    /// 移除的重複數量
    /// </summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>
    /// 重複組列表
    /// </summary>
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();

    /// <summary>
    /// 去重後的摘要列表
    /// </summary>
    public List<ProcessedSummaryItem> DeduplicatedSummaries { get; set; } = new();

    /// <summary>
    /// 檢測參數
    /// </summary>
    public DuplicateDetectionParameters DetectionParameters { get; set; } = new();

    /// <summary>
    /// 處理開始時間
    /// </summary>
    public DateTime ProcessingStartTime { get; set; }

    /// <summary>
    /// 處理結束時間
    /// </summary>
    public DateTime ProcessingEndTime { get; set; }

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
/// 重複內容檢測參數
/// </summary>
public class DuplicateDetectionParameters
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

    /// <summary>
    /// 是否啟用模糊匹配
    /// </summary>
    public bool EnableFuzzyMatching { get; set; } = true;

    /// <summary>
    /// 比較的最小長度
    /// </summary>
    public int MinLengthForComparison { get; set; } = 20;

    /// <summary>
    /// 是否保留較長的版本
    /// </summary>
    public bool PreserveLongerVersion { get; set; } = true;

    /// <summary>
    /// 是否考慮標題相似度
    /// </summary>
    public bool ConsiderTitleSimilarity { get; set; } = true;
}

/// <summary>
/// 重複內容組
/// </summary>
public class DuplicateGroup
{
    /// <summary>
    /// 代表項目
    /// </summary>
    public ProcessedSummaryItem Representative { get; set; } = new();

    /// <summary>
    /// 重複項目列表
    /// </summary>
    public List<ProcessedSummaryItem> Duplicates { get; set; } = new();

    /// <summary>
    /// 檢測方法
    /// </summary>
    public DuplicateDetectionMethod DetectionMethod { get; set; }

    /// <summary>
    /// 相似度分數
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// 組內項目總數
    /// </summary>
    public int TotalItems => 1 + Duplicates.Count;
}

/// <summary>
/// 預處理後的摘要項目
/// </summary>
public class ProcessedSummaryItem
{
    /// <summary>
    /// 原始索引
    /// </summary>
    public int OriginalIndex { get; set; }

    /// <summary>
    /// 清理後的內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 原始內容
    /// </summary>
    public string OriginalContent { get; set; } = string.Empty;

    /// <summary>
    /// 標題
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 詞數
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// 關鍵詞
    /// </summary>
    public List<string> KeyPhrases { get; set; } = new();

    /// <summary>
    /// 是否被選中（未被去除）
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// 品質分數
    /// </summary>
    public double QualityScore { get; set; } = 1.0;
}

/// <summary>
/// 近義詞組
/// </summary>
public class SynonymGroup
{
    /// <summary>
    /// 代表摘要
    /// </summary>
    public SegmentSummaryTask Representative { get; set; } = new();

    /// <summary>
    /// 近義詞摘要列表
    /// </summary>
    public List<SegmentSummaryTask> Synonyms { get; set; } = new();

    /// <summary>
    /// 語義相似度分數
    /// </summary>
    public double SemanticSimilarity { get; set; }
}

/// <summary>
/// 重複內容檢測方法
/// </summary>
public enum DuplicateDetectionMethod
{
    /// <summary>
    /// 文本相似度
    /// </summary>
    TextSimilarity = 1,

    /// <summary>
    /// 語義相似度
    /// </summary>
    SemanticSimilarity = 2,

    /// <summary>
    /// 模糊匹配
    /// </summary>
    FuzzyMatching = 3,

    /// <summary>
    /// 混合方法
    /// </summary>
    Hybrid = 4
}