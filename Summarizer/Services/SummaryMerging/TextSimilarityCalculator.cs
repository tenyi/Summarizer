using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// 文本相似度計算器
/// </summary>
public class TextSimilarityCalculator : ITextSimilarityCalculator
{
    private readonly ILogger<TextSimilarityCalculator> _logger;
    private readonly ISummaryService _summaryService;

    public TextSimilarityCalculator(
        ILogger<TextSimilarityCalculator> logger,
        ISummaryService summaryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
    }

    /// <summary>
    /// 計算兩個文本的相似度
    /// </summary>
    public async Task<double> CalculateSimilarityAsync(
        string text1, 
        string text2, 
        SimilarityType type = SimilarityType.Jaccard,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
        {
            return 0.0;
        }

        try
        {
            return type switch
            {
                SimilarityType.Jaccard => CalculateJaccardSimilarity(text1, text2),
                SimilarityType.Cosine => CalculateCosineSimilarity(text1, text2),
                SimilarityType.Semantic => await CalculateSemanticSimilarityAsync(text1, text2, cancellationToken),
                SimilarityType.EditDistance => CalculateEditDistanceSimilarity(text1, text2),
                SimilarityType.TFIDF => CalculateTFIDFSimilarity(text1, text2),
                _ => CalculateJaccardSimilarity(text1, text2)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "計算文本相似度時發生錯誤，使用預設方法");
            return CalculateJaccardSimilarity(text1, text2);
        }
    }

    /// <summary>
    /// 基於 Jaccard 相似度的快速檢測
    /// </summary>
    public double CalculateJaccardSimilarity(string text1, string text2)
    {
        var words1 = TokenizeText(text1).ToHashSet();
        var words2 = TokenizeText(text2).ToHashSet();

        if (words1.Count == 0 && words2.Count == 0)
            return 1.0;

        if (words1.Count == 0 || words2.Count == 0)
            return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// 計算餘弦相似度
    /// </summary>
    public double CalculateCosineSimilarity(string text1, string text2)
    {
        var words1 = TokenizeText(text1);
        var words2 = TokenizeText(text2);

        var allWords = words1.Union(words2).Distinct().ToList();
        var vector1 = CreateVector(words1, allWords);
        var vector2 = CreateVector(words2, allWords);

        return CalculateCosineSimilarity(vector1, vector2);
    }

    /// <summary>
    /// 基於語義嵌入的深度檢測（模擬實現）
    /// </summary>
    public Task<double> CalculateSemanticSimilarityAsync(string text1, string text2, CancellationToken cancellationToken = default)
    {
        try
        {
            // 由於我們沒有實際的嵌入服務，使用組合相似度作為近似
            var jaccardSim = CalculateJaccardSimilarity(text1, text2);
            var cosineSim = CalculateCosineSimilarity(text1, text2);
            var editSim = CalculateEditDistanceSimilarity(text1, text2);

            // 語義相似度的加權組合
            return Task.FromResult((jaccardSim * 0.4) + (cosineSim * 0.4) + (editSim * 0.2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "語義相似度計算失敗，回退到 Jaccard 相似度");
            return Task.FromResult(CalculateJaccardSimilarity(text1, text2));
        }
    }

    /// <summary>
    /// 計算編輯距離相似度
    /// </summary>
    public double CalculateEditDistanceSimilarity(string text1, string text2)
    {
        var maxLength = Math.Max(text1.Length, text2.Length);
        if (maxLength == 0) return 1.0;

        var editDistance = CalculateLevenshteinDistance(text1, text2);
        return 1.0 - ((double)editDistance / maxLength);
    }

    /// <summary>
    /// 計算 TF-IDF 相似度
    /// </summary>
    public double CalculateTFIDFSimilarity(string text1, string text2)
    {
        var words1 = TokenizeText(text1);
        var words2 = TokenizeText(text2);

        var allWords = words1.Union(words2).Distinct().ToList();
        var documents = new[] { words1, words2 };

        var tfidf1 = CalculateTFIDF(words1, documents, allWords);
        var tfidf2 = CalculateTFIDF(words2, documents, allWords);

        return CalculateCosineSimilarity(tfidf1, tfidf2);
    }

    /// <summary>
    /// 檢測是否為重複內容
    /// </summary>
    public async Task<bool> IsDuplicateContentAsync(
        string text1, 
        string text2, 
        double threshold = 0.8,
        SimilarityType type = SimilarityType.Semantic,
        CancellationToken cancellationToken = default)
    {
        var similarity = await CalculateSimilarityAsync(text1, text2, type, cancellationToken);
        return similarity >= threshold;
    }

    /// <summary>
    /// 文本分詞
    /// </summary>
    private static List<string> TokenizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // 移除標點符號並轉換為小寫
        var cleanText = Regex.Replace(text.ToLowerInvariant(), @"[^\u4e00-\u9fa5a-zA-Z0-9\s]", " ");
        
        // 分詞（簡單的空格分割，實際應用中可以使用更sophisticated的分詞工具）
        return cleanText.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 1) // 過濾單字符詞
                      .ToList();
    }

    /// <summary>
    /// 創建詞頻向量
    /// </summary>
    private static double[] CreateVector(List<string> words, List<string> vocabulary)
    {
        var vector = new double[vocabulary.Count];
        var wordCount = words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < vocabulary.Count; i++)
        {
            vector[i] = wordCount.GetValueOrDefault(vocabulary[i], 0);
        }

        return vector;
    }

    /// <summary>
    /// 計算兩個向量的餘弦相似度
    /// </summary>
    private static double CalculateCosineSimilarity(double[] vector1, double[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("向量長度必須相同");

        var dotProduct = vector1.Zip(vector2, (a, b) => a * b).Sum();
        var magnitude1 = Math.Sqrt(vector1.Sum(x => x * x));
        var magnitude2 = Math.Sqrt(vector2.Sum(x => x * x));

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// 計算Levenshtein編輯距離
    /// </summary>
    private static int CalculateLevenshteinDistance(string text1, string text2)
    {
        var matrix = new int[text1.Length + 1, text2.Length + 1];

        for (int i = 0; i <= text1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= text2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= text1.Length; i++)
        {
            for (int j = 1; j <= text2.Length; j++)
            {
                var cost = text1[i - 1] == text2[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[text1.Length, text2.Length];
    }

    /// <summary>
    /// 計算TF-IDF向量
    /// </summary>
    private static double[] CalculateTFIDF(List<string> document, List<string>[] allDocuments, List<string> vocabulary)
    {
        var tfidfVector = new double[vocabulary.Count];
        var wordCount = document.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < vocabulary.Count; i++)
        {
            var word = vocabulary[i];
            var tf = (double)wordCount.GetValueOrDefault(word, 0) / document.Count;
            
            var docsContaining = allDocuments.Count(doc => doc.Contains(word));
            var idf = docsContaining > 0 ? Math.Log((double)allDocuments.Length / docsContaining) : 0;
            
            tfidfVector[i] = tf * idf;
        }

        return tfidfVector;
    }
}