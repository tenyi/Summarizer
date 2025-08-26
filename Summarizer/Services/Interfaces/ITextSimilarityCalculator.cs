using Summarizer.Models.SummaryMerging;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 文本相似度計算器介面
/// </summary>
public interface ITextSimilarityCalculator
{
    /// <summary>
    /// 計算兩個文本的相似度
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <param name="type">相似度計算類型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>相似度分數（0-1之間）</returns>
    Task<double> CalculateSimilarityAsync(
        string text1, 
        string text2, 
        SimilarityType type = SimilarityType.Jaccard,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 基於 Jaccard 相似度的快速檢測
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <returns>Jaccard 相似度分數</returns>
    double CalculateJaccardSimilarity(string text1, string text2);

    /// <summary>
    /// 計算餘弦相似度
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <returns>餘弦相似度分數</returns>
    double CalculateCosineSimilarity(string text1, string text2);

    /// <summary>
    /// 基於語義嵌入的深度檢測
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>語義相似度分數</returns>
    Task<double> CalculateSemanticSimilarityAsync(string text1, string text2, CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算編輯距離相似度
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <returns>編輯距離相似度分數</returns>
    double CalculateEditDistanceSimilarity(string text1, string text2);

    /// <summary>
    /// 計算 TF-IDF 相似度
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <returns>TF-IDF 相似度分數</returns>
    double CalculateTFIDFSimilarity(string text1, string text2);

    /// <summary>
    /// 檢測是否為重複內容
    /// </summary>
    /// <param name="text1">第一個文本</param>
    /// <param name="text2">第二個文本</param>
    /// <param name="threshold">相似度閾值</param>
    /// <param name="type">相似度計算類型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否為重複內容</returns>
    Task<bool> IsDuplicateContentAsync(
        string text1, 
        string text2, 
        double threshold = 0.8,
        SimilarityType type = SimilarityType.Semantic,
        CancellationToken cancellationToken = default);
}