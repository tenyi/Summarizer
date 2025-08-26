using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// 內容最佳化服務
/// </summary>
public class ContentOptimizer : IContentOptimizer
{
    private readonly SummaryMergingConfig _config;
    private readonly ISummaryService _summaryService;
    private readonly ILogger<ContentOptimizer> _logger;

    public ContentOptimizer(
        IOptions<SummaryMergingConfig> config,
        ISummaryService summaryService,
        ILogger<ContentOptimizer> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 控制摘要長度並進行最佳化
    /// </summary>
    public async Task<ContentOptimizationResult> OptimizeContentAsync(
        string content,
        OptimizationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = new ContentOptimizationResult
        {
            OriginalContent = content,
            OriginalLength = content.Length,
            TargetLength = parameters.TargetLength,
            OptimizationStartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始內容最佳化，原始長度: {OriginalLength}，目標長度: {TargetLength}",
                result.OriginalLength, result.TargetLength);

            // 1. 計算長度差異和調整策略
            var lengthDifference = result.OriginalLength - result.TargetLength;
            var lengthRatio = (double)result.OriginalLength / result.TargetLength;
            
            result.LengthAdjustmentStrategy = DetermineLengthAdjustmentStrategy(lengthRatio, parameters);

            // 2. 執行內容最佳化
            string optimizedContent = await ExecuteOptimizationAsync(
                content, result.LengthAdjustmentStrategy, parameters, cancellationToken);

            // 3. 後處理和品質檢查
            optimizedContent = PostProcessContent(optimizedContent, parameters);

            // 4. 評估最佳化結果
            var qualityMetrics = EvaluateOptimizationQuality(content, optimizedContent, parameters);

            // 5. 如果品質不佳，進行調整
            if (qualityMetrics.OverallScore < parameters.MinQualityScore)
            {
                _logger.LogWarning("最佳化品質不佳 ({Score})，嘗試調整策略", qualityMetrics.OverallScore);
                optimizedContent = await RefineOptimizationAsync(
                    content, optimizedContent, parameters, cancellationToken);
                qualityMetrics = EvaluateOptimizationQuality(content, optimizedContent, parameters);
            }

            result.OptimizedContent = optimizedContent;
            result.FinalLength = optimizedContent.Length;
            result.CompressionRatio = (double)result.FinalLength / result.OriginalLength;
            result.QualityMetrics = qualityMetrics;
            result.OptimizationEndTime = DateTime.UtcNow;
            result.ProcessingTime = result.OptimizationEndTime - result.OptimizationStartTime;

            _logger.LogInformation("內容最佳化完成，最終長度: {FinalLength}，壓縮比: {CompressionRatio:P2}，品質分數: {QualityScore}",
                result.FinalLength, result.CompressionRatio, qualityMetrics.OverallScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "內容最佳化過程中發生錯誤");
            result.ErrorMessage = ex.Message;
            result.OptimizedContent = content; // 使用原始內容作為後備
            result.FinalLength = result.OriginalLength;
            result.OptimizationEndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 壓縮內容（保留核心資訊）
    /// </summary>
    public async Task<string> CompressContentAsync(
        string content,
        int targetLength,
        CompressionLevel level = CompressionLevel.Balanced,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length <= targetLength)
            return content;

        try
        {
            switch (level)
            {
                case CompressionLevel.Light:
                    return CompressLight(content, targetLength);
                case CompressionLevel.Aggressive:
                    return await CompressAggressiveAsync(content, targetLength, cancellationToken);
                case CompressionLevel.Balanced:
                default:
                    return await CompressBalancedAsync(content, targetLength, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "內容壓縮失敗，返回原始內容");
            return content;
        }
    }

    /// <summary>
    /// 擴展內容（補充重要細節）
    /// </summary>
    public async Task<string> ExpandContentAsync(
        string content,
        int targetLength,
        List<string>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length >= targetLength)
            return content;

        try
        {
            // 1. 分析內容結構
            var contentStructure = AnalyzeContentStructure(content);

            // 2. 識別可以擴展的部分
            var expansionOpportunities = IdentifyExpansionOpportunities(contentStructure, additionalContext);

            // 3. 執行內容擴展
            string expandedContent = await ExecuteContentExpansionAsync(
                content, expansionOpportunities, targetLength, cancellationToken);

            return expandedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "內容擴展失敗，返回原始內容");
            return content;
        }
    }

    /// <summary>
    /// 平衡長度與品質
    /// </summary>
    public async Task<LengthQualityBalance> BalanceLengthAndQualityAsync(
        string content,
        int targetLength,
        double qualityWeight = 0.6,
        CancellationToken cancellationToken = default)
    {
        var result = new LengthQualityBalance
        {
            OriginalContent = content,
            OriginalLength = content.Length,
            TargetLength = targetLength,
            QualityWeight = qualityWeight
        };

        try
        {
            // 1. 生成不同長度的候選版本
            var candidates = await GenerateLengthCandidatesAsync(content, targetLength, cancellationToken);

            // 2. 評估每個候選版本的品質
            foreach (var candidate in candidates)
            {
                candidate.QualityScore = EvaluateContentQuality(content, candidate.Content);
                candidate.LengthScore = CalculateLengthScore(candidate.Content.Length, targetLength);
                candidate.BalanceScore = (candidate.QualityScore * qualityWeight) + 
                                       (candidate.LengthScore * (1 - qualityWeight));
            }

            // 3. 選擇最佳平衡的版本
            var bestCandidate = candidates.OrderByDescending(c => c.BalanceScore).First();

            result.BalancedContent = bestCandidate.Content;
            result.FinalLength = bestCandidate.Content.Length;
            result.QualityScore = bestCandidate.QualityScore;
            result.LengthScore = bestCandidate.LengthScore;
            result.BalanceScore = bestCandidate.BalanceScore;
            result.Candidates = candidates;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "長度品質平衡失敗");
            result.BalancedContent = content;
            result.FinalLength = content.Length;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    #region 私有輔助方法

    /// <summary>
    /// 決定長度調整策略
    /// </summary>
    private LengthAdjustmentStrategy DetermineLengthAdjustmentStrategy(
        double lengthRatio, 
        OptimizationParameters parameters)
    {
        var tolerance = _config.LengthControl.LengthTolerance;

        if (lengthRatio >= 1 + tolerance)
        {
            // 需要壓縮
            return lengthRatio > 1.5 ? LengthAdjustmentStrategy.AggressiveCompression : 
                   LengthAdjustmentStrategy.ModerateCompression;
        }
        else if (lengthRatio <= 1 - tolerance)
        {
            // 需要擴展
            return lengthRatio < 0.7 ? LengthAdjustmentStrategy.SignificantExpansion : 
                   LengthAdjustmentStrategy.ModerateExpansion;
        }
        else
        {
            // 在可接受範圍內，進行輕微最佳化
            return LengthAdjustmentStrategy.OptimizeOnly;
        }
    }

    /// <summary>
    /// 執行內容最佳化
    /// </summary>
    private async Task<string> ExecuteOptimizationAsync(
        string content,
        LengthAdjustmentStrategy strategy,
        OptimizationParameters parameters,
        CancellationToken cancellationToken)
    {
        switch (strategy)
        {
            case LengthAdjustmentStrategy.AggressiveCompression:
                return await CompressContentAsync(content, parameters.TargetLength, CompressionLevel.Aggressive, cancellationToken);
            
            case LengthAdjustmentStrategy.ModerateCompression:
                return await CompressContentAsync(content, parameters.TargetLength, CompressionLevel.Balanced, cancellationToken);
            
            case LengthAdjustmentStrategy.SignificantExpansion:
            case LengthAdjustmentStrategy.ModerateExpansion:
                return await ExpandContentAsync(content, parameters.TargetLength, null, cancellationToken);
            
            case LengthAdjustmentStrategy.OptimizeOnly:
            default:
                return OptimizeTextFlow(content);
        }
    }

    /// <summary>
    /// 輕度壓縮
    /// </summary>
    private string CompressLight(string content, int targetLength)
    {
        var sentences = SplitIntoSentences(content);
        var result = new StringBuilder();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var cleanSentence = sentence.Trim();
            if (currentLength + cleanSentence.Length <= targetLength)
            {
                result.Append(cleanSentence);
                if (!cleanSentence.EndsWith(".") && !cleanSentence.EndsWith("。"))
                {
                    result.Append("。");
                }
                result.Append(" ");
                currentLength = result.Length;
            }
            else
            {
                break;
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// 平衡壓縮
    /// </summary>
    private async Task<string> CompressBalancedAsync(string content, int targetLength, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var sentences = SplitIntoSentences(content);
            var importantSentences = SelectImportantSentences(sentences, (int)(sentences.Count * 0.7));
            
            var compressedContent = string.Join(" ", importantSentences);
            
            if (compressedContent.Length > targetLength)
            {
                // 進一步壓縮
                return TruncateToLength(compressedContent, targetLength);
            }
            
            return compressedContent;
        }, cancellationToken);
    }

    /// <summary>
    /// 激進壓縮
    /// </summary>
    private async Task<string> CompressAggressiveAsync(string content, int targetLength, CancellationToken cancellationToken)
    {
        try
        {
            // 使用 LLM 進行智能壓縮
            var compressionPrompt = $"請將以下內容壓縮到約 {targetLength} 字，保留最重要的資訊：\n\n{content}";
            var compressed = await _summaryService.SummarizeAsync(compressionPrompt, cancellationToken);
            
            if (compressed.Length <= targetLength * 1.1) // 允許10%的容差
            {
                return compressed;
            }
            
            // 如果還是太長，進行截斷
            return TruncateToLength(compressed, targetLength);
        }
        catch
        {
            // 回退到規則式壓縮
            return CompressLight(content, targetLength);
        }
    }

    /// <summary>
    /// 後處理內容
    /// </summary>
    private string PostProcessContent(string content, OptimizationParameters parameters)
    {
        // 1. 清理多餘的空白
        content = Regex.Replace(content, @"\s+", " ");
        
        // 2. 修正標點符號
        content = FixPunctuation(content);
        
        // 3. 確保句子完整性
        content = EnsureSentenceCompleteness(content);
        
        // 4. 格式化段落
        if (parameters.PreserveFormatting)
        {
            content = FormatParagraphs(content);
        }
        
        return content.Trim();
    }

    /// <summary>
    /// 評估最佳化品質
    /// </summary>
    private OptimizationQualityMetrics EvaluateOptimizationQuality(
        string original,
        string optimized,
        OptimizationParameters parameters)
    {
        var metrics = new OptimizationQualityMetrics();

        // 內容保持度
        metrics.ContentRetention = CalculateContentRetention(original, optimized);
        
        // 流暢度
        metrics.Fluency = EvaluateFluency(optimized);
        
        // 連貫性
        metrics.Coherence = EvaluateCoherence(optimized);
        
        // 長度達標度
        metrics.LengthAccuracy = CalculateLengthAccuracy(optimized.Length, parameters.TargetLength, parameters.LengthTolerance);
        
        // 整體分數
        metrics.OverallScore = (metrics.ContentRetention * 0.3) + 
                              (metrics.Fluency * 0.25) + 
                              (metrics.Coherence * 0.25) + 
                              (metrics.LengthAccuracy * 0.2);

        return metrics;
    }

    /// <summary>
    /// 精煉最佳化結果
    /// </summary>
    private async Task<string> RefineOptimizationAsync(
        string original,
        string optimized,
        OptimizationParameters parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var refinementPrompt = $"請改善以下摘要的品質，使其更流暢連貫，目標長度約 {parameters.TargetLength} 字：\n\n{optimized}";
            return await _summaryService.SummarizeAsync(refinementPrompt, cancellationToken);
        }
        catch
        {
            return optimized; // 如果失敗，返回原始最佳化結果
        }
    }

    #endregion

    #region 待實現的方法存根

    private List<string> SplitIntoSentences(string content)
    {
        return Regex.Split(content, @"[.!?。！？]")
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .Select(s => s.Trim())
                   .ToList();
    }

    private List<string> SelectImportantSentences(List<string> sentences, int count)
    {
        // TODO: 實現句子重要性評估
        return sentences.Take(count).ToList();
    }

    private string TruncateToLength(string content, int targetLength)
    {
        if (content.Length <= targetLength)
            return content;

        var truncated = content.Substring(0, targetLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        return lastSpace > 0 ? truncated.Substring(0, lastSpace) + "..." : truncated + "...";
    }

    private string OptimizeTextFlow(string content)
    {
        // TODO: 實現文本流暢度最佳化
        return content;
    }

    private ContentStructure AnalyzeContentStructure(string content)
    {
        // TODO: 實現內容結構分析
        return new ContentStructure { MainPoints = new List<string>(), Details = new List<string>() };
    }

    private List<ExpansionOpportunity> IdentifyExpansionOpportunities(ContentStructure structure, List<string>? context)
    {
        // TODO: 實現擴展機會識別
        return new List<ExpansionOpportunity>();
    }

    private async Task<string> ExecuteContentExpansionAsync(string content, List<ExpansionOpportunity> opportunities, int targetLength, CancellationToken cancellationToken)
    {
        // TODO: 實現內容擴展
        await Task.CompletedTask;
        return content;
    }

    private async Task<List<LengthCandidate>> GenerateLengthCandidatesAsync(string content, int targetLength, CancellationToken cancellationToken)
    {
        // TODO: 實現長度候選版本生成
        await Task.CompletedTask;
        return new List<LengthCandidate>
        {
            new() { Content = content, Length = content.Length }
        };
    }

    private double EvaluateContentQuality(string original, string candidate)
    {
        // TODO: 實現內容品質評估
        return 0.7;
    }

    private double CalculateLengthScore(int actualLength, int targetLength)
    {
        var difference = Math.Abs(actualLength - targetLength);
        var ratio = (double)difference / targetLength;
        return Math.Max(0, 1 - ratio);
    }

    private double CalculateContentRetention(string original, string optimized)
    {
        // TODO: 實現內容保持度計算
        return 0.8;
    }

    private double EvaluateFluency(string content)
    {
        // TODO: 實現流暢度評估
        return 0.7;
    }

    private double EvaluateCoherence(string content)
    {
        // TODO: 實現連貫性評估
        return 0.75;
    }

    private double CalculateLengthAccuracy(int actualLength, int targetLength, double tolerance)
    {
        var difference = Math.Abs(actualLength - targetLength);
        var allowedDifference = targetLength * tolerance;
        
        if (difference <= allowedDifference)
            return 1.0;
        
        return Math.Max(0, 1 - (difference - allowedDifference) / targetLength);
    }

    private string FixPunctuation(string content)
    {
        // TODO: 實現標點符號修正
        return content;
    }

    private string EnsureSentenceCompleteness(string content)
    {
        // TODO: 實現句子完整性確保
        return content;
    }

    private string FormatParagraphs(string content)
    {
        // TODO: 實現段落格式化
        return content;
    }

    #endregion

    #region 輔助資料類別

    private class ContentStructure
    {
        public List<string> MainPoints { get; set; } = new();
        public List<string> Details { get; set; } = new();
    }

    private class ExpansionOpportunity
    {
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    #endregion
}