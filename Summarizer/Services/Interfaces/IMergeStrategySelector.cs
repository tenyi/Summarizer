using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 合併策略選擇器介面
/// </summary>
public interface IMergeStrategySelector
{
    /// <summary>
    /// 自動選擇最適合的合併策略
    /// </summary>
    /// <param name="summaries">輸入摘要列表</param>
    /// <param name="userPreferences">使用者偏好設定</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>推薦的合併策略和參數</returns>
    Task<StrategyRecommendation> SelectOptimalStrategyAsync(
        List<SegmentSummaryTask> summaries,
        UserMergePreferences? userPreferences = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 評估不同策略的適用性
    /// </summary>
    /// <param name="summaries">輸入摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>各策略的評估結果</returns>
    Task<Dictionary<MergeStrategy, StrategyEvaluation>> EvaluateStrategiesAsync(
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立自訂策略參數
    /// </summary>
    /// <param name="preferences">使用者偏好設定</param>
    /// <param name="summaries">輸入摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>自訂合併參數</returns>
    Task<MergeParameters> CreateCustomParametersAsync(
        UserMergePreferences preferences,
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分析內容特徵以輔助策略選擇
    /// </summary>
    /// <param name="summaries">輸入摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>內容特徵分析結果</returns>
    Task<ContentCharacteristics> AnalyzeContentCharacteristicsAsync(
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據策略執行結果調整未來的推薦
    /// </summary>
    /// <param name="strategy">使用的策略</param>
    /// <param name="result">合併結果</param>
    /// <param name="userFeedback">使用者回饋</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功學習調整</returns>
    Task<bool> LearnFromResultAsync(
        MergeStrategy strategy,
        MergeResult result,
        UserFeedback? userFeedback = null,
        CancellationToken cancellationToken = default);
}

