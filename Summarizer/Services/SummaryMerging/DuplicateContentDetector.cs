using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// 重複內容檢測服務
/// </summary>
public class DuplicateContentDetector : IDuplicateContentDetector
{
    private readonly SummaryMergingConfig _config;
    private readonly ITextSimilarityCalculator _similarityCalculator;
    private readonly ILogger<DuplicateContentDetector> _logger;

    public DuplicateContentDetector(
        IOptions<SummaryMergingConfig> config,
        ITextSimilarityCalculator similarityCalculator,
        ILogger<DuplicateContentDetector> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _similarityCalculator = similarityCalculator ?? throw new ArgumentNullException(nameof(similarityCalculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 檢測摘要列表中的重複內容
    /// </summary>
    public async Task<DuplicateDetectionResult> DetectDuplicatesAsync(
        List<SegmentSummaryTask> summaries,
        DuplicateDetectionParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= CreateDefaultParameters();
        
        var result = new DuplicateDetectionResult
        {
            OriginalCount = summaries.Count,
            DetectionParameters = parameters,
            ProcessingStartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始檢測重複內容，摘要數量: {Count}，相似度閾值: {Threshold}",
                summaries.Count, parameters.SimilarityThreshold);

            // 1. 預處理摘要內容
            var processedSummaries = PreprocessSummaries(summaries);

            // 2. 執行快速重複檢測
            var quickDuplicates = await PerformQuickDuplicateDetectionAsync(processedSummaries, parameters, cancellationToken);

            // 3. 執行深度語義檢測（如果啟用）
            List<DuplicateGroup> semanticDuplicates = new();
            if (parameters.UseSemanticSimilarity)
            {
                semanticDuplicates = await PerformSemanticDuplicateDetectionAsync(
                    processedSummaries, parameters, cancellationToken);
            }

            // 4. 合併檢測結果
            var allDuplicates = MergeDuplicateResults(quickDuplicates, semanticDuplicates);

            // 5. 選擇最佳版本
            var deduplicatedSummaries = SelectBestVersions(processedSummaries, allDuplicates, parameters);

            // 6. 生成統計資訊
            result.DuplicateGroups = allDuplicates;
            result.DeduplicatedSummaries = deduplicatedSummaries;
            result.FinalCount = deduplicatedSummaries.Count;
            result.DuplicatesRemoved = result.OriginalCount - result.FinalCount;
            result.ProcessingEndTime = DateTime.UtcNow;
            result.ProcessingTime = result.ProcessingEndTime - result.ProcessingStartTime;

            _logger.LogInformation("重複內容檢測完成，原始數量: {Original}，去重後數量: {Final}，移除: {Removed}",
                result.OriginalCount, result.FinalCount, result.DuplicatesRemoved);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重複內容檢測過程中發生錯誤");
            result.ErrorMessage = ex.Message;
            result.ProcessingEndTime = DateTime.UtcNow;
            
            // 返回原始列表作為後備
            result.DeduplicatedSummaries = summaries.Select((s, index) => new ProcessedSummaryItem
            {
                OriginalIndex = index,
                Content = s.SummaryResult ?? string.Empty,
                Title = s.SourceSegment?.Title ?? $"分段 {index + 1}",
                WordCount = CountWords(s.SummaryResult ?? string.Empty),
                IsSelected = true
            }).ToList();
            
            return result;
        }
    }

    /// <summary>
    /// 移除重複內容並返回最佳版本
    /// </summary>
    public async Task<List<SegmentSummaryTask>> RemoveDuplicatesAsync(
        List<SegmentSummaryTask> summaries,
        DuplicateDetectionParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var detectionResult = await DetectDuplicatesAsync(summaries, parameters, cancellationToken);
        
        var result = new List<SegmentSummaryTask>();
        
        foreach (var processed in detectionResult.DeduplicatedSummaries.Where(s => s.IsSelected))
        {
            if (processed.OriginalIndex < summaries.Count)
            {
                result.Add(summaries[processed.OriginalIndex]);
            }
        }

        return result.OrderBy(s => s.SegmentIndex).ToList();
    }

    /// <summary>
    /// 識別近義詞和改寫內容
    /// </summary>
    public async Task<List<SynonymGroup>> IdentifySynonymContentAsync(
        List<SegmentSummaryTask> summaries,
        double semanticThreshold = 0.75,
        CancellationToken cancellationToken = default)
    {
        var synonymGroups = new List<SynonymGroup>();
        
        try
        {
            for (int i = 0; i < summaries.Count - 1; i++)
            {
                if (string.IsNullOrWhiteSpace(summaries[i].SummaryResult))
                    continue;

                var currentGroup = new SynonymGroup
                {
                    Representative = summaries[i],
                    Synonyms = new List<SegmentSummaryTask>()
                };

                for (int j = i + 1; j < summaries.Count; j++)
                {
                    if (string.IsNullOrWhiteSpace(summaries[j].SummaryResult))
                        continue;

                    // 使用語義相似度檢測近義詞
                    var similarity = await _similarityCalculator.CalculateSemanticSimilarityAsync(
                        summaries[i].SummaryResult, summaries[j].SummaryResult, cancellationToken);

                    if (similarity >= semanticThreshold)
                    {
                        currentGroup.Synonyms.Add(summaries[j]);
                    }
                }

                if (currentGroup.Synonyms.Count > 0)
                {
                    synonymGroups.Add(currentGroup);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "識別近義詞內容時發生錯誤");
        }

        return synonymGroups;
    }

    #region 私有輔助方法

    /// <summary>
    /// 建立預設檢測參數
    /// </summary>
    private DuplicateDetectionParameters CreateDefaultParameters()
    {
        return new DuplicateDetectionParameters
        {
            SimilarityThreshold = _config.DuplicateDetection.SimilarityThreshold,
            UseSemanticSimilarity = _config.DuplicateDetection.UseSemanticSimilarity,
            ContextWindow = _config.DuplicateDetection.ContextWindow,
            SemanticSimilarityThreshold = _config.DuplicateDetection.SemanticSimilarityThreshold,
            EnableFuzzyMatching = true,
            MinLengthForComparison = 20,
            PreserveLongerVersion = true,
            ConsiderTitleSimilarity = true
        };
    }

    /// <summary>
    /// 預處理摘要內容
    /// </summary>
    private List<ProcessedSummaryItem> PreprocessSummaries(List<SegmentSummaryTask> summaries)
    {
        var processed = new List<ProcessedSummaryItem>();

        for (int i = 0; i < summaries.Count; i++)
        {
            var summary = summaries[i];
            if (string.IsNullOrWhiteSpace(summary.SummaryResult))
                continue;

            var item = new ProcessedSummaryItem
            {
                OriginalIndex = i,
                Content = CleanText(summary.SummaryResult),
                OriginalContent = summary.SummaryResult,
                Title = summary.SourceSegment?.Title ?? $"分段 {i + 1}",
                WordCount = CountWords(summary.SummaryResult),
                KeyPhrases = ExtractKeyPhrases(summary.SummaryResult),
                IsSelected = true
            };

            processed.Add(item);
        }

        return processed;
    }

    /// <summary>
    /// 執行快速重複檢測
    /// </summary>
    private async Task<List<DuplicateGroup>> PerformQuickDuplicateDetectionAsync(
        List<ProcessedSummaryItem> summaries,
        DuplicateDetectionParameters parameters,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // 避免 CS1998 警告
        var duplicateGroups = new List<DuplicateGroup>();
        var processed = new HashSet<int>();

        for (int i = 0; i < summaries.Count - 1; i++)
        {
            if (processed.Contains(i))
                continue;

            var currentGroup = new DuplicateGroup
            {
                Representative = summaries[i],
                Duplicates = new List<ProcessedSummaryItem>(),
                DetectionMethod = DuplicateDetectionMethod.TextSimilarity
            };

            for (int j = i + 1; j < summaries.Count; j++)
            {
                if (processed.Contains(j))
                    continue;

                // 使用 Jaccard 相似度進行快速檢測
                var similarity = _similarityCalculator.CalculateJaccardSimilarity(
                    summaries[i].Content, summaries[j].Content);

                if (similarity >= parameters.SimilarityThreshold)
                {
                    currentGroup.Duplicates.Add(summaries[j]);
                    processed.Add(j);
                }
            }

            if (currentGroup.Duplicates.Count > 0)
            {
                duplicateGroups.Add(currentGroup);
                processed.Add(i);
            }
        }

        return duplicateGroups;
    }

    /// <summary>
    /// 執行語義重複檢測
    /// </summary>
    private async Task<List<DuplicateGroup>> PerformSemanticDuplicateDetectionAsync(
        List<ProcessedSummaryItem> summaries,
        DuplicateDetectionParameters parameters,
        CancellationToken cancellationToken)
    {
        var duplicateGroups = new List<DuplicateGroup>();
        var processed = new HashSet<int>();

        for (int i = 0; i < summaries.Count - 1; i++)
        {
            if (processed.Contains(i))
                continue;

            var currentGroup = new DuplicateGroup
            {
                Representative = summaries[i],
                Duplicates = new List<ProcessedSummaryItem>(),
                DetectionMethod = DuplicateDetectionMethod.SemanticSimilarity
            };

            for (int j = i + 1; j < summaries.Count; j++)
            {
                if (processed.Contains(j))
                    continue;

                try
                {
                    // 使用語義相似度檢測
                    var similarity = await _similarityCalculator.CalculateSemanticSimilarityAsync(
                        summaries[i].Content, summaries[j].Content, cancellationToken);

                    if (similarity >= parameters.SemanticSimilarityThreshold)
                    {
                        currentGroup.Duplicates.Add(summaries[j]);
                        processed.Add(j);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "語義相似度計算失敗，跳過比較項目 {i} 和 {j}", i, j);
                }
            }

            if (currentGroup.Duplicates.Count > 0)
            {
                duplicateGroups.Add(currentGroup);
                processed.Add(i);
            }
        }

        return duplicateGroups;
    }

    /// <summary>
    /// 合併重複檢測結果
    /// </summary>
    private List<DuplicateGroup> MergeDuplicateResults(
        List<DuplicateGroup> quickDuplicates,
        List<DuplicateGroup> semanticDuplicates)
    {
        var mergedGroups = new List<DuplicateGroup>(quickDuplicates);

        // 將語義重複組合併到結果中（避免重複）
        foreach (var semanticGroup in semanticDuplicates)
        {
            var existingGroup = mergedGroups.FirstOrDefault(g =>
                g.Representative.OriginalIndex == semanticGroup.Representative.OriginalIndex ||
                g.Duplicates.Any(d => d.OriginalIndex == semanticGroup.Representative.OriginalIndex));

            if (existingGroup == null)
            {
                mergedGroups.Add(semanticGroup);
            }
            else
            {
                // 合併重複項目
                foreach (var duplicate in semanticGroup.Duplicates)
                {
                    if (!existingGroup.Duplicates.Any(d => d.OriginalIndex == duplicate.OriginalIndex))
                    {
                        existingGroup.Duplicates.Add(duplicate);
                    }
                }
            }
        }

        return mergedGroups;
    }

    /// <summary>
    /// 選擇最佳版本
    /// </summary>
    private List<ProcessedSummaryItem> SelectBestVersions(
        List<ProcessedSummaryItem> summaries,
        List<DuplicateGroup> duplicateGroups,
        DuplicateDetectionParameters parameters)
    {
        var result = summaries.ToList();
        var toRemove = new HashSet<int>();

        foreach (var group in duplicateGroups)
        {
            // 在重複組中選擇最佳版本
            var bestVersion = SelectBestFromGroup(group, parameters);
            
            // 標記其他版本為移除
            var allInGroup = new List<ProcessedSummaryItem> { group.Representative };
            allInGroup.AddRange(group.Duplicates);

            foreach (var item in allInGroup)
            {
                if (item.OriginalIndex != bestVersion.OriginalIndex)
                {
                    toRemove.Add(item.OriginalIndex);
                }
            }
        }

        // 移除被標記的項目
        return result.Where(s => !toRemove.Contains(s.OriginalIndex)).ToList();
    }

    /// <summary>
    /// 從重複組中選擇最佳版本
    /// </summary>
    private ProcessedSummaryItem SelectBestFromGroup(DuplicateGroup group, DuplicateDetectionParameters parameters)
    {
        var candidates = new List<ProcessedSummaryItem> { group.Representative };
        candidates.AddRange(group.Duplicates);

        // 優先選擇較長的版本（如果設定為保留較長版本）
        if (parameters.PreserveLongerVersion)
        {
            return candidates.OrderByDescending(c => c.WordCount).First();
        }

        // 選擇關鍵詞最豐富的版本
        return candidates.OrderByDescending(c => c.KeyPhrases.Count).First();
    }

    /// <summary>
    /// 清理文本
    /// </summary>
    private string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 移除額外的空白字符
        text = Regex.Replace(text, @"\s+", " ");
        
        // 統一標點符號
        text = text.Replace("，", ",").Replace("。", ".");
        
        return text.Trim();
    }

    /// <summary>
    /// 計算詞數
    /// </summary>
    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new char[] { ' ', '\t', '\n', '\r', '，', '。', '、' }, 
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// 提取關鍵詞
    /// </summary>
    private List<string> ExtractKeyPhrases(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // 簡單的關鍵詞提取（實際應用中可以使用更sophisticated的方法）
        var words = Regex.Matches(text, @"[\u4e00-\u9fa5]{2,}|[a-zA-Z]{3,}")
            .Cast<Match>()
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length > 2)
            .GroupBy(w => w)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(10)
            .ToList();

        return words;
    }

    #endregion
}