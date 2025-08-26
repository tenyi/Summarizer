using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 來源追溯服務介面
/// </summary>
public interface ISourceTrackingService
{
    /// <summary>
    /// 建立來源追溯資訊
    /// </summary>
    /// <param name="mergeResult">合併結果</param>
    /// <param name="inputSummaries">輸入摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>來源追溯結果</returns>
    Task<SourceTrackingResult> CreateSourceTrackingAsync(
        MergeResult mergeResult,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 標記分段來源資訊
    /// </summary>
    /// <param name="finalSummary">最終摘要</param>
    /// <param name="sourceMappings">來源對應關係</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>標記後的摘要內容</returns>
    Task<string> MarkSegmentSourcesAsync(
        string finalSummary,
        List<MergeSourceMapping> sourceMappings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立摘要內容與原分段的對應關係
    /// </summary>
    /// <param name="finalSummary">最終摘要</param>
    /// <param name="inputSummaries">輸入摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>段落來源對應列表</returns>
    Task<List<ParagraphSourceMapping>> EstablishContentMappingAsync(
        string finalSummary,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 自動生成來源引用
    /// </summary>
    /// <param name="trackingResult">追溯結果</param>
    /// <param name="options">引用生成選項</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含引用的摘要內容</returns>
    Task<string> GenerateSourceReferencesAsync(
        SourceTrackingResult trackingResult,
        ReferenceGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立追溯資訊的視覺化顯示資料
    /// </summary>
    /// <param name="trackingResult">追溯結果</param>
    /// <param name="options">視覺化選項</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>視覺化資料</returns>
    Task<TraceabilityVisualizationData> CreateVisualizationDataAsync(
        SourceTrackingResult trackingResult,
        VisualizationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證來源追溯的完整性和準確性
    /// </summary>
    /// <param name="trackingResult">追溯結果</param>
    /// <param name="inputSummaries">原始輸入摘要</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>驗證結果</returns>
    Task<SourceValidationResult> ValidateSourceIntegrityAsync(
        SourceTrackingResult trackingResult,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算追溯資訊的品質分數
    /// </summary>
    /// <param name="trackingResult">追溯結果</param>
    /// <param name="validationResult">驗證結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>品質分數資訊</returns>
    Task<TraceabilityQualityMetrics> CalculateQualityScoresAsync(
        SourceTrackingResult trackingResult,
        SourceValidationResult validationResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得追溯結果
    /// </summary>
    /// <param name="trackingId">追溯 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>追溯結果</returns>
    Task<SourceTrackingResult?> GetTrackingResultAsync(
        Guid trackingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 儲存追溯結果
    /// </summary>
    /// <param name="trackingResult">追溯結果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功儲存</returns>
    Task<bool> SaveTrackingResultAsync(
        SourceTrackingResult trackingResult,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 追溯品質指標
/// </summary>
public class TraceabilityQualityMetrics
{
    /// <summary>
    /// 整體品質分數
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 準確性分數
    /// </summary>
    public double AccuracyScore { get; set; }

    /// <summary>
    /// 完整性分數
    /// </summary>
    public double CompletenessScore { get; set; }

    /// <summary>
    /// 可信度分數
    /// </summary>
    public double ReliabilityScore { get; set; }

    /// <summary>
    /// 涵蓋度分數
    /// </summary>
    public double CoverageScore { get; set; }

    /// <summary>
    /// 一致性分數
    /// </summary>
    public double ConsistencyScore { get; set; }

    /// <summary>
    /// 品質問題統計
    /// </summary>
    public Dictionary<ValidationIssueType, int> IssueStats { get; set; } = new();

    /// <summary>
    /// 改善建議
    /// </summary>
    public List<string> ImprovementSuggestions { get; set; } = new();

    /// <summary>
    /// 計算時間
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}