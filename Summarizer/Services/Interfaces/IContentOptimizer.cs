using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 內容最佳化服務介面
/// </summary>
public interface IContentOptimizer
{
    /// <summary>
    /// 控制摘要長度並進行最佳化
    /// </summary>
    /// <param name="content">原始內容</param>
    /// <param name="parameters">最佳化參數</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>內容最佳化結果</returns>
    Task<ContentOptimizationResult> OptimizeContentAsync(
        string content,
        OptimizationParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 壓縮內容（保留核心資訊）
    /// </summary>
    /// <param name="content">原始內容</param>
    /// <param name="targetLength">目標長度</param>
    /// <param name="level">壓縮程度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>壓縮後的內容</returns>
    Task<string> CompressContentAsync(
        string content,
        int targetLength,
        CompressionLevel level = CompressionLevel.Balanced,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 擴展內容（補充重要細節）
    /// </summary>
    /// <param name="content">原始內容</param>
    /// <param name="targetLength">目標長度</param>
    /// <param name="additionalContext">額外上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>擴展後的內容</returns>
    Task<string> ExpandContentAsync(
        string content,
        int targetLength,
        List<string>? additionalContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 平衡長度與品質
    /// </summary>
    /// <param name="content">原始內容</param>
    /// <param name="targetLength">目標長度</param>
    /// <param name="qualityWeight">品質權重（0-1）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>長度品質平衡結果</returns>
    Task<LengthQualityBalance> BalanceLengthAndQualityAsync(
        string content,
        int targetLength,
        double qualityWeight = 0.6,
        CancellationToken cancellationToken = default);
}