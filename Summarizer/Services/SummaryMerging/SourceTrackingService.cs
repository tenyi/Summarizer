using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// 來源追溯服務實作
/// </summary>
public class SourceTrackingService : ISourceTrackingService
{
    private readonly ILogger<SourceTrackingService> _logger;
    private readonly SummaryMergingConfig _config;
    private readonly ITextSimilarityCalculator _similarityCalculator;
    
    // 簡單的記憶體快取，生產環境應使用 Redis 或類似解決方案
    private readonly Dictionary<Guid, SourceTrackingResult> _trackingCache = new();
    
    public SourceTrackingService(
        ILogger<SourceTrackingService> logger,
        IOptions<SummaryMergingConfig> config,
        ITextSimilarityCalculator similarityCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _similarityCalculator = similarityCalculator ?? throw new ArgumentNullException(nameof(similarityCalculator));
    }

    public async Task<SourceTrackingResult> CreateSourceTrackingAsync(
        MergeResult mergeResult,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始建立來源追溯資訊，合併任務 ID: {MergeJobId}", mergeResult.MergeJobId);

        try
        {
            var trackingResult = new SourceTrackingResult
            {
                MergeJobId = mergeResult.MergeJobId,
                FinalSummary = mergeResult.FinalSummary
            };

            // 建立段落來源對應
            trackingResult.ParagraphMappings = await EstablishContentMappingAsync(
                mergeResult.FinalSummary, inputSummaries, cancellationToken);

            // 計算來源完整性和追溯品質分數
            var qualityMetrics = await CalculateTrackingQualityAsync(trackingResult, inputSummaries);
            trackingResult.SourceIntegrityScore = qualityMetrics.CompletenessScore;
            trackingResult.TraceabilityScore = qualityMetrics.OverallScore;

            // 執行驗證
            var validationResult = await ValidateSourceIntegrityAsync(trackingResult, inputSummaries, cancellationToken);
            trackingResult.ValidationStatus = validationResult.IsValid 
                ? TrackingValidationStatus.Valid 
                : TrackingValidationStatus.Invalid;

            _logger.LogInformation("來源追溯資訊建立完成，品質分數: {Score}", trackingResult.TraceabilityScore);
            return trackingResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立來源追溯資訊時發生錯誤");
            throw;
        }
    }

    public async Task<string> MarkSegmentSourcesAsync(
        string finalSummary,
        List<MergeSourceMapping> sourceMappings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始標記分段來源資訊");

        try
        {
            return await Task.Run(() =>
            {
                var markedSummary = new StringBuilder(finalSummary);
                var paragraphs = SplitIntoParagraphs(finalSummary);
                
                var offset = 0;
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    var mapping = sourceMappings.FirstOrDefault(m => m.ParagraphIndex == i);
                    if (mapping?.SourceSegmentIndices?.Any() == true)
                    {
                        var sourceMarker = CreateSourceMarker(mapping.SourceSegmentIndices);
                        var insertPosition = offset + paragraphs[i].Length;
                        
                        markedSummary.Insert(insertPosition, sourceMarker);
                        offset += sourceMarker.Length;
                    }
                    offset += paragraphs[i].Length;
                }

                return markedSummary.ToString();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標記分段來源資訊時發生錯誤");
            return finalSummary; // 返回原始摘要作為備用
        }
    }

    public async Task<List<ParagraphSourceMapping>> EstablishContentMappingAsync(
        string finalSummary,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始建立摘要內容與原分段的對應關係");

        try
        {
            var paragraphs = SplitIntoParagraphs(finalSummary);
            var mappings = new List<ParagraphSourceMapping>();
            
            var currentPosition = 0;
            for (int i = 0; i < paragraphs.Count; i++)
            {
                var paragraph = paragraphs[i];
                var mapping = new ParagraphSourceMapping
                {
                    ParagraphIndex = i,
                    Content = paragraph,
                    StartPosition = currentPosition,
                    EndPosition = currentPosition + paragraph.Length
                };

                // 找出與此段落最相關的來源分段
                var sourceReferences = await FindSourceReferencesAsync(paragraph, inputSummaries, cancellationToken);
                mapping.SourceReferences = sourceReferences;
                
                // 計算信心分數
                mapping.ConfidenceScore = sourceReferences.Any() 
                    ? sourceReferences.Average(r => r.SimilarityScore) 
                    : 0.0;

                // 確定合併方法
                mapping.MergeMethod = DetermineMergeMethod(sourceReferences);

                mappings.Add(mapping);
                currentPosition += paragraph.Length + 1; // +1 for paragraph separator
            }

            return mappings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立內容對應關係時發生錯誤");
            return new List<ParagraphSourceMapping>();
        }
    }

    public Task<string> GenerateSourceReferencesAsync(
        SourceTrackingResult trackingResult,
        ReferenceGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ReferenceGenerationOptions();
        _logger.LogInformation("開始生成來源引用，格式: {Format}", options.Format);

        try
        {
            var referencedSummary = new StringBuilder(trackingResult.FinalSummary);
            
            string result = options.Format switch
            {
                ReferenceFormat.InText => GenerateInTextReferences(trackingResult, options),
                ReferenceFormat.Footnote => GenerateFootnoteReferences(trackingResult, options),
                ReferenceFormat.Endnote => GenerateEndnoteReferences(trackingResult, options),
                ReferenceFormat.Tooltip => GenerateTooltipReferences(trackingResult, options),
                ReferenceFormat.Custom => GenerateCustomReferences(trackingResult, options),
                _ => GenerateInTextReferences(trackingResult, options)
            };
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成來源引用時發生錯誤");
            return Task.FromResult(trackingResult.FinalSummary);
        }
    }

    public Task<TraceabilityVisualizationData> CreateVisualizationDataAsync(
        SourceTrackingResult trackingResult,
        VisualizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new VisualizationOptions();
        _logger.LogInformation("開始建立追溯視覺化資料，類型: {Type}", options.Type);

        try
        {
            var visualizationData = new TraceabilityVisualizationData
            {
                TrackingId = trackingResult.TrackingId,
                Options = options
            };

            // 建立節點：最終摘要段落
            var paragraphNodes = CreateParagraphNodes(trackingResult.ParagraphMappings);
            visualizationData.Nodes.AddRange(paragraphNodes);

            // 建立節點：來源分段
            var sourceNodes = CreateSourceNodes(trackingResult.ParagraphMappings);
            visualizationData.Nodes.AddRange(sourceNodes);

            // 建立連接：段落與來源分段的關聯
            var links = CreateVisualizationLinks(trackingResult.ParagraphMappings);
            visualizationData.Links.AddRange(links);

            // 應用佈局演算法計算節點位置
            ApplyLayoutAlgorithm(visualizationData, options.Layout);

            return Task.FromResult(visualizationData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立視覺化資料時發生錯誤");
            throw;
        }
    }

    public async Task<SourceValidationResult> ValidateSourceIntegrityAsync(
        SourceTrackingResult trackingResult,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始驗證來源追溯的完整性和準確性");

        try
        {
            var validationResult = new SourceValidationResult();
            var issues = new List<ValidationIssue>();

            // 檢查覆蓋度：確保所有重要的來源分段都被引用
            await ValidateCoverageAsync(trackingResult, inputSummaries, issues);

            // 檢查準確性：驗證引用的正確性
            await ValidateAccuracyAsync(trackingResult, issues);

            // 檢查完整性：確保沒有斷裂的連結
            await ValidateIntegrityAsync(trackingResult, issues);

            // 檢查一致性：確保引用的一致性
            await ValidateConsistencyAsync(trackingResult, issues);

            validationResult.Issues = issues;
            validationResult.IsValid = !issues.Any(i => i.Severity >= IssueSeverity.Error);

            // 計算各項分數
            validationResult.CoverageScore = CalculateCoverageScore(trackingResult, inputSummaries);
            validationResult.AccuracyScore = CalculateAccuracyScore(issues);
            validationResult.IntegrityScore = CalculateIntegrityScore(issues);

            _logger.LogInformation("來源追溯驗證完成，發現 {IssueCount} 個問題", issues.Count);
            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證來源追溯時發生錯誤");
            throw;
        }
    }

    public Task<TraceabilityQualityMetrics> CalculateQualityScoresAsync(
        SourceTrackingResult trackingResult,
        SourceValidationResult validationResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = new TraceabilityQualityMetrics
            {
                AccuracyScore = validationResult.AccuracyScore,
                CompletenessScore = validationResult.IntegrityScore,
                CoverageScore = validationResult.CoverageScore,
                ReliabilityScore = CalculateReliabilityScore(trackingResult),
                ConsistencyScore = CalculateConsistencyScore(trackingResult)
            };

            // 計算整體品質分數
            metrics.OverallScore = (metrics.AccuracyScore * 0.25 + 
                                  metrics.CompletenessScore * 0.2 + 
                                  metrics.CoverageScore * 0.2 + 
                                  metrics.ReliabilityScore * 0.2 + 
                                  metrics.ConsistencyScore * 0.15);

            // 統計問題類型
            foreach (var issue in validationResult.Issues)
            {
                if (metrics.IssueStats.ContainsKey(issue.IssueType))
                    metrics.IssueStats[issue.IssueType]++;
                else
                    metrics.IssueStats[issue.IssueType] = 1;
            }

            // 生成改善建議
            metrics.ImprovementSuggestions = GenerateImprovementSuggestions(metrics, validationResult);

            return Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "計算品質分數時發生錯誤");
            throw;
        }
    }

    public Task<SourceTrackingResult?> GetTrackingResultAsync(
        Guid trackingId,
        CancellationToken cancellationToken = default)
    {
        var result = _trackingCache.TryGetValue(trackingId, out var trackingResult) ? trackingResult : null;
        return Task.FromResult(result);
    }

    public Task<bool> SaveTrackingResultAsync(
        SourceTrackingResult trackingResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _trackingCache[trackingResult.TrackingId] = trackingResult;
            _logger.LogInformation("來源追溯結果已儲存，ID: {TrackingId}", trackingResult.TrackingId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存追溯結果時發生錯誤");
            return Task.FromResult(false);
        }
    }

    #region 私有輔助方法

    private List<string> SplitIntoParagraphs(string text)
    {
        return text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(p => p.Trim())
                  .Where(p => !string.IsNullOrEmpty(p))
                  .ToList();
    }

    private string CreateSourceMarker(List<int> sourceIndices)
    {
        var indices = string.Join(", ", sourceIndices.Select(i => (i + 1).ToString()));
        return $" [來源: {indices}]";
    }

    private async Task<List<SourceReference>> FindSourceReferencesAsync(
        string paragraph,
        List<SegmentSummaryTask> inputSummaries,
        CancellationToken cancellationToken)
    {
        var references = new List<SourceReference>();
        
        for (int i = 0; i < inputSummaries.Count; i++)
        {
            var summary = inputSummaries[i];
            if (string.IsNullOrEmpty(summary.SummaryResult))
                continue;

            // 計算段落與來源摘要的相似度
            var similarity = await _similarityCalculator.CalculateSimilarityAsync(
                paragraph, summary.SummaryResult, SimilarityType.Semantic, cancellationToken);

            if (similarity >= _config.DuplicateDetection.SimilarityThreshold * 0.6) // 較低的閾值用於來源追溯
            {
                var reference = new SourceReference
                {
                    SegmentIndex = i,
                    SegmentTitle = summary.SourceSegment?.Title ?? $"分段 {i + 1}",
                    ContentExcerpt = CreateExcerpt(summary.SummaryResult, 100),
                    SimilarityScore = similarity,
                    ContributionWeight = CalculateContributionWeight(similarity),
                    ReferenceType = DetermineReferenceType(similarity),
                    OriginalSegmentContent = summary.SourceSegment?.Content ?? "",
                    SummaryContent = summary.SummaryResult
                };

                references.Add(reference);
            }
        }

        // 按相似度排序並限制數量
        return references.OrderByDescending(r => r.SimilarityScore)
                        .Take(_config.MaxReferencesPerParagraph)
                        .ToList();
    }

    private string DetermineMergeMethod(List<SourceReference> references)
    {
        if (!references.Any())
            return "Unknown";

        if (references.Count == 1)
            return "Direct";

        if (references.All(r => r.SimilarityScore > 0.9))
            return "Duplicate";

        return "Merged";
    }

    private string CreateExcerpt(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;

        return content.Substring(0, maxLength - 3) + "...";
    }

    private double CalculateContributionWeight(double similarity)
    {
        // 基於相似度的貢獻度權重計算
        return Math.Pow(similarity, 2); // 平方函數強化高相似度的權重
    }

    private SourceReferenceType DetermineReferenceType(double similarity)
    {
        if (similarity > 0.9)
            return SourceReferenceType.Direct;
        else if (similarity > 0.7)
            return SourceReferenceType.Paraphrase;
        else if (similarity > 0.5)
            return SourceReferenceType.Summary;
        else
            return SourceReferenceType.Inferred;
    }

    private string GenerateInTextReferences(SourceTrackingResult trackingResult, ReferenceGenerationOptions options)
    {
        var result = new StringBuilder(trackingResult.FinalSummary);
        var offset = 0;

        foreach (var mapping in trackingResult.ParagraphMappings)
        {
            if (mapping.SourceReferences.Any())
            {
                var validReferences = mapping.SourceReferences
                    .Where(r => r.SimilarityScore >= options.MinConfidenceThreshold)
                    .Take(options.MaxReferencesPerParagraph);

                if (validReferences.Any())
                {
                    var referenceText = CreateInTextReferenceText(validReferences, options);
                    var insertPosition = mapping.EndPosition + offset;
                    
                    result.Insert(insertPosition, referenceText);
                    offset += referenceText.Length;
                }
            }
        }

        return result.ToString();
    }

    private string CreateInTextReferenceText(IEnumerable<SourceReference> references, ReferenceGenerationOptions options)
    {
        var refTexts = references.Select(r => 
        {
            var text = options.ShowSegmentTitles ? r.SegmentTitle : $"分段{r.SegmentIndex + 1}";
            if (options.ShowConfidenceScores)
                text += $"({r.SimilarityScore:F2})";
            return text;
        });

        return $" [{string.Join(", ", refTexts)}]";
    }

    private string GenerateFootnoteReferences(SourceTrackingResult trackingResult, ReferenceGenerationOptions options)
    {
        var result = new StringBuilder(trackingResult.FinalSummary);
        var footnotes = new StringBuilder("\n\n---\n腳註：\n");

        // 實作腳註生成邏輯
        // 這裡簡化實作，實際應用中需要更複雜的邏輯
        return result.ToString() + footnotes.ToString();
    }

    private string GenerateEndnoteReferences(SourceTrackingResult trackingResult, ReferenceGenerationOptions options)
    {
        // 實作尾註生成邏輯
        return trackingResult.FinalSummary + "\n\n參考來源：\n[尾註內容]";
    }

    private string GenerateTooltipReferences(SourceTrackingResult trackingResult, ReferenceGenerationOptions options)
    {
        // 實作工具提示引用邏輯，通常返回包含特殊標記的HTML
        return trackingResult.FinalSummary;
    }

    private string GenerateCustomReferences(SourceTrackingResult trackingResult, ReferenceGenerationOptions options)
    {
        if (string.IsNullOrEmpty(options.CustomFormatTemplate))
            return GenerateInTextReferences(trackingResult, options);

        // 實作自訂格式邏輯
        return trackingResult.FinalSummary;
    }

    private List<VisualizationNode> CreateParagraphNodes(List<ParagraphSourceMapping> mappings)
    {
        var nodes = new List<VisualizationNode>();
        
        foreach (var mapping in mappings)
        {
            var node = new VisualizationNode
            {
                Id = $"paragraph_{mapping.ParagraphIndex}",
                Type = NodeType.FinalParagraph,
                Title = $"段落 {mapping.ParagraphIndex + 1}",
                Content = CreateExcerpt(mapping.Content, 100),
                Size = Math.Min(mapping.Content.Length / 10, 100),
                Color = DetermineNodeColor(mapping.ConfidenceScore),
                Properties = new Dictionary<string, object>
                {
                    ["confidence"] = mapping.ConfidenceScore,
                    ["method"] = mapping.MergeMethod
                }
            };
            nodes.Add(node);
        }

        return nodes;
    }

    private List<VisualizationNode> CreateSourceNodes(List<ParagraphSourceMapping> mappings)
    {
        var nodes = new List<VisualizationNode>();
        var processedSources = new HashSet<int>();

        foreach (var mapping in mappings)
        {
            foreach (var reference in mapping.SourceReferences)
            {
                if (processedSources.Contains(reference.SegmentIndex))
                    continue;

                var node = new VisualizationNode
                {
                    Id = $"source_{reference.SegmentIndex}",
                    Type = NodeType.SourceSegment,
                    Title = reference.SegmentTitle,
                    Content = CreateExcerpt(reference.SummaryContent, 100),
                    Size = Math.Min(reference.SummaryContent.Length / 15, 80),
                    Color = DetermineSourceNodeColor(reference.ReferenceType),
                    Properties = new Dictionary<string, object>
                    {
                        ["type"] = reference.ReferenceType.ToString(),
                        ["weight"] = reference.ContributionWeight
                    }
                };
                
                nodes.Add(node);
                processedSources.Add(reference.SegmentIndex);
            }
        }

        return nodes;
    }

    private List<VisualizationLink> CreateVisualizationLinks(List<ParagraphSourceMapping> mappings)
    {
        var links = new List<VisualizationLink>();
        
        foreach (var mapping in mappings)
        {
            foreach (var reference in mapping.SourceReferences)
            {
                var link = new VisualizationLink
                {
                    Id = $"link_{mapping.ParagraphIndex}_{reference.SegmentIndex}",
                    SourceId = $"source_{reference.SegmentIndex}",
                    TargetId = $"paragraph_{mapping.ParagraphIndex}",
                    Strength = reference.SimilarityScore,
                    Type = DetermineLinkType(reference.ReferenceType),
                    Label = $"{reference.SimilarityScore:F2}",
                    Color = DetermineLinkColor(reference.SimilarityScore),
                    Width = Math.Max(reference.SimilarityScore * 5, 1)
                };
                links.Add(link);
            }
        }

        return links;
    }

    private void ApplyLayoutAlgorithm(TraceabilityVisualizationData data, LayoutAlgorithm algorithm)
    {
        // 實作不同佈局演算法
        switch (algorithm)
        {
            case LayoutAlgorithm.ForceDirected:
                ApplyForceDirectedLayout(data);
                break;
            case LayoutAlgorithm.Hierarchical:
                ApplyHierarchicalLayout(data);
                break;
            case LayoutAlgorithm.Circular:
                ApplyCircularLayout(data);
                break;
            default:
                ApplyForceDirectedLayout(data);
                break;
        }
    }

    private void ApplyForceDirectedLayout(TraceabilityVisualizationData data)
    {
        // 簡化的力導向佈局演算法
        var random = new Random();
        foreach (var node in data.Nodes)
        {
            node.Position.X = random.NextDouble() * 800;
            node.Position.Y = random.NextDouble() * 600;
        }
    }

    private void ApplyHierarchicalLayout(TraceabilityVisualizationData data)
    {
        // 階層佈局：來源節點在上方，段落節點在下方
        var sourceNodes = data.Nodes.Where(n => n.Type == NodeType.SourceSegment).ToList();
        var paragraphNodes = data.Nodes.Where(n => n.Type == NodeType.FinalParagraph).ToList();

        for (int i = 0; i < sourceNodes.Count; i++)
        {
            sourceNodes[i].Position.X = (i + 1) * (800.0 / (sourceNodes.Count + 1));
            sourceNodes[i].Position.Y = 100;
        }

        for (int i = 0; i < paragraphNodes.Count; i++)
        {
            paragraphNodes[i].Position.X = (i + 1) * (800.0 / (paragraphNodes.Count + 1));
            paragraphNodes[i].Position.Y = 400;
        }
    }

    private void ApplyCircularLayout(TraceabilityVisualizationData data)
    {
        // 圓形佈局
        var centerX = 400.0;
        var centerY = 300.0;
        var radius = 200.0;

        for (int i = 0; i < data.Nodes.Count; i++)
        {
            var angle = 2 * Math.PI * i / data.Nodes.Count;
            data.Nodes[i].Position.X = centerX + radius * Math.Cos(angle);
            data.Nodes[i].Position.Y = centerY + radius * Math.Sin(angle);
        }
    }

    private string DetermineNodeColor(double confidenceScore)
    {
        if (confidenceScore >= 0.8) return "#4CAF50"; // 綠色 - 高信心度
        if (confidenceScore >= 0.6) return "#FF9800"; // 橙色 - 中等信心度
        return "#F44336"; // 紅色 - 低信心度
    }

    private string DetermineSourceNodeColor(SourceReferenceType type)
    {
        return type switch
        {
            SourceReferenceType.Direct => "#2196F3",      // 藍色
            SourceReferenceType.Paraphrase => "#9C27B0",  // 紫色
            SourceReferenceType.Summary => "#607D8B",     // 藍灰色
            SourceReferenceType.Merged => "#795548",      // 棕色
            SourceReferenceType.Inferred => "#9E9E9E",    // 灰色
            _ => "#9E9E9E"
        };
    }

    private LinkType DetermineLinkType(SourceReferenceType referenceType)
    {
        return referenceType switch
        {
            SourceReferenceType.Direct => LinkType.DirectSource,
            SourceReferenceType.Paraphrase => LinkType.IndirectSource,
            SourceReferenceType.Summary => LinkType.IndirectSource,
            SourceReferenceType.Merged => LinkType.MergedSource,
            SourceReferenceType.Inferred => LinkType.IndirectSource,
            _ => LinkType.IndirectSource
        };
    }

    private string DetermineLinkColor(double similarityScore)
    {
        if (similarityScore >= 0.8) return "#4CAF50"; // 綠色 - 高相似度
        if (similarityScore >= 0.6) return "#FF9800"; // 橙色 - 中等相似度
        return "#F44336"; // 紅色 - 低相似度
    }

    private Task ValidateCoverageAsync(SourceTrackingResult trackingResult, List<SegmentSummaryTask> inputSummaries, List<ValidationIssue> issues)
    {
        var referencedSegments = trackingResult.ParagraphMappings
            .SelectMany(m => m.SourceReferences.Select(r => r.SegmentIndex))
            .Distinct()
            .ToHashSet();

        for (int i = 0; i < inputSummaries.Count; i++)
        {
            if (!referencedSegments.Contains(i) && !string.IsNullOrEmpty(inputSummaries[i].SummaryResult))
            {
                issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.InsufficientCoverage,
                    Description = $"分段 {i + 1} 未被任何最終摘要段落引用",
                    Severity = IssueSeverity.Warning,
                    SourceSegmentIndex = i,
                    SuggestedAction = "檢查該分段是否包含重要資訊需要納入最終摘要"
                });
            }
        }
        
        return Task.CompletedTask;
    }

    private Task ValidateAccuracyAsync(SourceTrackingResult trackingResult, List<ValidationIssue> issues)
    {
        foreach (var mapping in trackingResult.ParagraphMappings)
        {
            var lowConfidenceReferences = mapping.SourceReferences
                .Where(r => r.SimilarityScore < _config.DuplicateDetection.SimilarityThreshold * 0.5)
                .ToList();

            foreach (var reference in lowConfidenceReferences)
            {
                issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.LowConfidence,
                    Description = $"段落 {mapping.ParagraphIndex + 1} 對分段 {reference.SegmentIndex + 1} 的引用信心度過低 ({reference.SimilarityScore:F2})",
                    Severity = reference.SimilarityScore < 0.3 ? IssueSeverity.Error : IssueSeverity.Warning,
                    ParagraphIndex = mapping.ParagraphIndex,
                    SourceSegmentIndex = reference.SegmentIndex,
                    SuggestedAction = "重新評估此引用關係或提高相似度閾值"
                });
            }
        }
        
        return Task.CompletedTask;
    }

    private Task ValidateIntegrityAsync(SourceTrackingResult trackingResult, List<ValidationIssue> issues)
    {
        // 檢查是否有無效的來源引用
        foreach (var mapping in trackingResult.ParagraphMappings)
        {
            foreach (var reference in mapping.SourceReferences)
            {
                if (string.IsNullOrEmpty(reference.SummaryContent))
                {
                    issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.BrokenLink,
                        Description = $"段落 {mapping.ParagraphIndex + 1} 引用的分段 {reference.SegmentIndex + 1} 內容為空",
                        Severity = IssueSeverity.Error,
                        ParagraphIndex = mapping.ParagraphIndex,
                        SourceSegmentIndex = reference.SegmentIndex,
                        SuggestedAction = "檢查來源分段資料的完整性"
                    });
                }
            }
        }
        
        return Task.CompletedTask;
    }

    private Task ValidateConsistencyAsync(SourceTrackingResult trackingResult, List<ValidationIssue> issues)
    {
        // 檢查重複引用
        var allReferences = trackingResult.ParagraphMappings
            .SelectMany(m => m.SourceReferences.Select(r => new { ParagraphIndex = m.ParagraphIndex, Reference = r }))
            .GroupBy(x => x.Reference.SegmentIndex)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in allReferences)
        {
            var segmentIndex = group.Key;
            var references = group.Select(x => x.ParagraphIndex).ToList();
            
            if (references.Count > 3) // 如果被過多段落引用，可能表示重複
            {
                issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.DuplicateReference,
                    Description = $"分段 {segmentIndex + 1} 被多個段落 ({string.Join(", ", references.Select(r => r + 1))}) 重複引用",
                    Severity = IssueSeverity.Info,
                    SourceSegmentIndex = segmentIndex,
                    SuggestedAction = "檢查是否存在內容重複或可以合併的段落"
                });
            }
        }
        
        return Task.CompletedTask;
    }

    private double CalculateCoverageScore(SourceTrackingResult trackingResult, List<SegmentSummaryTask> inputSummaries)
    {
        var referencedSegments = trackingResult.ParagraphMappings
            .SelectMany(m => m.SourceReferences.Select(r => r.SegmentIndex))
            .Distinct()
            .Count();

        var totalSegments = inputSummaries.Count(s => !string.IsNullOrEmpty(s.SummaryResult));
        
        return totalSegments > 0 ? (double)referencedSegments / totalSegments : 0.0;
    }

    private double CalculateAccuracyScore(List<ValidationIssue> issues)
    {
        var totalIssues = issues.Count;
        var errorIssues = issues.Count(i => i.Severity >= IssueSeverity.Error);
        
        if (totalIssues == 0) return 1.0;
        
        return Math.Max(0.0, 1.0 - (errorIssues * 0.5 + (totalIssues - errorIssues) * 0.2) / totalIssues);
    }

    private double CalculateIntegrityScore(List<ValidationIssue> issues)
    {
        var integrityIssues = issues.Count(i => i.IssueType == ValidationIssueType.BrokenLink || 
                                              i.IssueType == ValidationIssueType.MissingSource);
        
        return Math.Max(0.0, 1.0 - integrityIssues * 0.3);
    }

    private double CalculateReliabilityScore(SourceTrackingResult trackingResult)
    {
        var allConfidenceScores = trackingResult.ParagraphMappings
            .SelectMany(m => m.SourceReferences.Select(r => r.SimilarityScore))
            .ToList();

        return allConfidenceScores.Any() ? allConfidenceScores.Average() : 0.0;
    }

    private double CalculateConsistencyScore(SourceTrackingResult trackingResult)
    {
        // 基於引用分佈的均勻性計算一致性分數
        var referenceDistribution = trackingResult.ParagraphMappings
            .SelectMany(m => m.SourceReferences.Select(r => r.SegmentIndex))
            .GroupBy(i => i)
            .Select(g => g.Count())
            .ToList();

        if (!referenceDistribution.Any()) return 0.0;

        var mean = referenceDistribution.Average();
        var variance = referenceDistribution.Average(x => Math.Pow(x - mean, 2));
        
        // 變異數越小，一致性越高
        return Math.Max(0.0, 1.0 - variance / (mean + 1));
    }

    private List<string> GenerateImprovementSuggestions(TraceabilityQualityMetrics metrics, SourceValidationResult validationResult)
    {
        var suggestions = new List<string>();

        if (metrics.CoverageScore < 0.8)
        {
            suggestions.Add("提高來源覆蓋率：確保所有重要的原始分段都被適當引用");
        }

        if (metrics.AccuracyScore < 0.7)
        {
            suggestions.Add("改善引用準確性：檢查並修正低信心度的來源引用");
        }

        if (metrics.ReliabilityScore < 0.6)
        {
            suggestions.Add("提升整體可信度：使用更精確的相似度計算算法");
        }

        if (metrics.ConsistencyScore < 0.7)
        {
            suggestions.Add("增強一致性：平衡各段落的來源引用分佈");
        }

        return suggestions;
    }

    private async Task<TraceabilityQualityMetrics> CalculateTrackingQualityAsync(
        SourceTrackingResult trackingResult, 
        List<SegmentSummaryTask> inputSummaries)
    {
        // 這是一個簡化版本，實際上會呼叫 CalculateQualityScoresAsync
        var dummyValidationResult = new SourceValidationResult
        {
            AccuracyScore = 0.8,
            IntegrityScore = 0.85,
            CoverageScore = CalculateCoverageScore(trackingResult, inputSummaries)
        };

        return await CalculateQualityScoresAsync(trackingResult, dummyValidationResult);
    }

    #endregion
}