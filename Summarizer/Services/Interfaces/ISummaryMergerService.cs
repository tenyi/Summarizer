using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 摘要合併服務介面
/// </summary>
public interface ISummaryMergerService
{
    /// <summary>
    /// 合併多個分段摘要為最終總結
    /// </summary>
    /// <param name="summaries">分段摘要列表</param>
    /// <param name="strategy">合併策略</param>
    /// <param name="parameters">合併參數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併結果</returns>
    Task<MergeResult> MergeSummariesAsync(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy = MergeStrategy.Balanced,
        MergeParameters? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立摘要合併作業
    /// </summary>
    /// <param name="summaries">分段摘要列表</param>
    /// <param name="strategy">合併策略</param>
    /// <param name="parameters">合併參數</param>
    /// <returns>摘要合併器</returns>
    SummaryMerger CreateMergeJob(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy = MergeStrategy.Balanced,
        MergeParameters? parameters = null);

    /// <summary>
    /// 執行摘要合併作業
    /// </summary>
    /// <param name="merger">摘要合併器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併結果</returns>
    Task<MergeResult> ExecuteMergeJobAsync(
        SummaryMerger merger,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 預覽合併結果（不執行完整合併）
    /// </summary>
    /// <param name="summaries">分段摘要列表</param>
    /// <param name="strategy">合併策略</param>
    /// <param name="parameters">合併參數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併預覽結果</returns>
    Task<MergePreviewResult> PreviewMergeAsync(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy = MergeStrategy.Balanced,
        MergeParameters? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 評估摘要品質
    /// </summary>
    /// <param name="summary">摘要內容</param>
    /// <param name="originalSummaries">原始分段摘要</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>品質評估結果</returns>
    Task<MergeQualityMetrics> AssessQualityAsync(
        string summary,
        List<SegmentSummaryTask> originalSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 選擇最適合的合併策略
    /// </summary>
    /// <param name="summaries">分段摘要列表</param>
    /// <param name="targetLength">目標長度</param>
    /// <returns>建議的合併策略</returns>
    MergeStrategy SelectOptimalStrategy(List<SegmentSummaryTask> summaries, int? targetLength = null);

    /// <summary>
    /// 驗證合併參數
    /// </summary>
    /// <param name="parameters">合併參數</param>
    /// <param name="summaries">分段摘要列表</param>
    /// <returns>驗證結果和修正後的參數</returns>
    (bool IsValid, MergeParameters CorrectedParameters, List<string> Issues) ValidateParameters(
        MergeParameters parameters, 
        List<SegmentSummaryTask> summaries);
}

/// <summary>
/// 合併預覽結果
/// </summary>
public class MergePreviewResult
{
    /// <summary>
    /// 預估最終長度
    /// </summary>
    public int EstimatedLength { get; set; }

    /// <summary>
    /// 預估壓縮比率
    /// </summary>
    public double EstimatedCompressionRatio { get; set; }

    /// <summary>
    /// 預計會被移除的重複內容數量
    /// </summary>
    public int EstimatedDuplicatesRemoved { get; set; }

    /// <summary>
    /// 建議的合併策略
    /// </summary>
    public MergeStrategy RecommendedStrategy { get; set; }

    /// <summary>
    /// 預估品質分數
    /// </summary>
    public double EstimatedQualityScore { get; set; }

    /// <summary>
    /// 預估處理時間
    /// </summary>
    public TimeSpan EstimatedProcessingTime { get; set; }

    /// <summary>
    /// 潛在品質問題
    /// </summary>
    public List<string> PotentialIssues { get; set; } = new();

    /// <summary>
    /// 主要段落預覽（前幾個段落的合併效果）
    /// </summary>
    public List<string> ParagraphPreviews { get; set; } = new();
}