using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// LLM 輔助合併服務介面
/// 提供基於大語言模型的智能摘要合併功能
/// </summary>
public interface ILLMAssistedMergeService
{
    /// <summary>
    /// 使用 LLM 進行智能摘要合併
    /// </summary>
    /// <param name="summaries">待合併的摘要列表</param>
    /// <param name="strategy">合併策略</param>
    /// <param name="userPreferences">用戶偏好設定</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 合併結果</returns>
    Task<LLMAssistedMergeResult> MergeWithLLMAsync(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy = MergeStrategy.Balanced,
        UserMergePreferences? userPreferences = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 評估 LLM 合併結果的品質
    /// </summary>
    /// <param name="mergeResult">合併結果</param>
    /// <param name="originalSummaries">原始摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>品質評估結果</returns>
    Task<MergeQualityAssessment> AssessMergeQualityAsync(
        LLMAssistedMergeResult mergeResult,
        List<SegmentSummaryTask> originalSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 融合規則式合併與 LLM 合併結果
    /// </summary>
    /// <param name="ruleBasedResult">規則式合併結果</param>
    /// <param name="llmResult">LLM 合併結果</param>
    /// <param name="fusionStrategy">融合策略</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>融合後的合併結果</returns>
    Task<MergeResult> FuseMergeResultsAsync(
        MergeResult ruleBasedResult,
        LLMAssistedMergeResult llmResult,
        FusionStrategy fusionStrategy = FusionStrategy.Intelligent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 對 LLM 合併結果進行後處理
    /// </summary>
    /// <param name="llmResult">LLM 合併結果</param>
    /// <param name="postProcessingOptions">後處理選項</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>後處理後的結果</returns>
    Task<LLMAssistedMergeResult> PostProcessLLMResultAsync(
        LLMAssistedMergeResult llmResult,
        PostProcessingOptions? postProcessingOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成 LLM 合併提示詞
    /// </summary>
    /// <param name="summaries">待合併的摘要列表</param>
    /// <param name="strategy">合併策略</param>
    /// <param name="userPreferences">用戶偏好設定</param>
    /// <returns>生成的提示詞</returns>
    string GenerateMergePrompt(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy,
        UserMergePreferences? userPreferences = null);

    /// <summary>
    /// 驗證 LLM 合併結果的正確性
    /// </summary>
    /// <param name="mergeResult">合併結果</param>
    /// <param name="originalSummaries">原始摘要列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>驗證結果</returns>
    Task<ValidationResult> ValidateLLMMergeResultAsync(
        LLMAssistedMergeResult mergeResult,
        List<SegmentSummaryTask> originalSummaries,
        CancellationToken cancellationToken = default);
}