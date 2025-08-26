using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// LLM 輔助合併服務實作
/// 提供基於大語言模型的智能摘要合併功能
/// </summary>
public class LLMAssistedMergeService : ILLMAssistedMergeService
{
    private readonly ILogger<LLMAssistedMergeService> _logger;
    private readonly ISummaryService _summaryService;
    private readonly ITextSimilarityCalculator _similarityCalculator;
    private readonly SummaryMergingConfig _config;
    
    /// <summary>
    /// LLM 合併提示詞模板
    /// </summary>
    private readonly Dictionary<MergeStrategy, string> _promptTemplates = new()
    {
        [MergeStrategy.Concise] = @"
請將以下多個摘要合併成一個簡潔的摘要。要求：
1. 保留最重要的資訊和關鍵要點
2. 移除重複或冗餘的內容
3. 確保邏輯結構清晰
4. 目標長度：{targetLength}個字以內
5. 使用繁體中文

原始摘要：
{summaries}

請提供合併後的摘要：",

        [MergeStrategy.Detailed] = @"
請將以下多個摘要合併成一個詳細完整的摘要。要求：
1. 保留所有重要資訊和細節
2. 整合相關內容，去除重複
3. 維持原文的邏輯結構和論述順序
4. 確保資訊的完整性和準確性
5. 使用繁體中文

原始摘要：
{summaries}

請提供詳細的合併摘要：",

        [MergeStrategy.Structured] = @"
請將以下多個摘要合併成結構化的摘要。要求：
1. 按照主題或類別重新組織內容
2. 使用清晰的標題和段落結構
3. 確保邏輯順序和層次分明
4. 去除重複內容，整合相關資訊
5. 使用繁體中文

原始摘要：
{summaries}

請提供結構化的合併摘要：",

        [MergeStrategy.Balanced] = @"
請將以下多個摘要合併成平衡的摘要。要求：
1. 在簡潔性和完整性之間取得平衡
2. 保留關鍵資訊，適度保留細節
3. 確保內容邏輯清晰，表達流暢
4. 去除重複，整合相關內容
5. 目標長度：{targetLength}個字左右
6. 使用繁體中文

原始摘要：
{summaries}

請提供平衡的合併摘要：",

        [MergeStrategy.Custom] = @"
請根據以下特定要求將多個摘要進行合併：

自訂要求：
{customRequirements}

原始摘要：
{summaries}

請按照上述要求提供合併摘要："
    };

    public LLMAssistedMergeService(
        ILogger<LLMAssistedMergeService> logger,
        ISummaryService summaryService,
        ITextSimilarityCalculator similarityCalculator,
        IOptions<SummaryMergingConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        _similarityCalculator = similarityCalculator ?? throw new ArgumentNullException(nameof(similarityCalculator));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 使用 LLM 進行智能摘要合併
    /// </summary>
    public async Task<LLMAssistedMergeResult> MergeWithLLMAsync(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy = MergeStrategy.Balanced,
        UserMergePreferences? userPreferences = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var mergeJobId = Guid.NewGuid();

        try
        {
            _logger.LogInformation("開始 LLM 輔助合併，工作ID: {MergeJobId}，策略: {Strategy}", mergeJobId, strategy);

            // 驗證輸入
            if (summaries == null || summaries.Count == 0)
            {
                throw new ArgumentException("摘要列表不能為空", nameof(summaries));
            }

            // 生成提示詞
            var prompt = GenerateMergePrompt(summaries, strategy, userPreferences);

            // 準備統計資訊
            var statistics = new LLMMergeStatistics
            {
                OriginalSummaryCount = summaries.Count,
                OriginalTotalWords = summaries.Sum(s => CountWords(s.SummaryResult))
            };

            // 呼叫 LLM API
            var llmResponse = await CallLLMServiceAsync(prompt, cancellationToken);

            // 處理回應
            var processedSummary = await ProcessLLMResponseAsync(llmResponse.Content, cancellationToken);

            // 更新統計資訊
            statistics.MergedWords = CountWords(processedSummary);
            statistics.InformationRetention = await CalculateInformationRetentionAsync(summaries, processedSummary, cancellationToken);
            statistics.LogicalConsistency = await AssessLogicalConsistencyAsync(processedSummary, cancellationToken);
            statistics.LanguageFluency = await AssessLanguageFluencyAsync(processedSummary, cancellationToken);

            // 計算信心分數
            var confidenceScore = await CalculateConfidenceScoreAsync(summaries, processedSummary, llmResponse, cancellationToken);

            stopwatch.Stop();

            var result = new LLMAssistedMergeResult
            {
                MergeJobId = mergeJobId,
                FinalSummary = processedSummary,
                ModelInfo = llmResponse.ModelInfo,
                UsedPrompt = prompt,
                RawResponse = llmResponse.Content,
                Statistics = statistics,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConfidenceScore = confidenceScore,
                RequiresHumanReview = confidenceScore < _config.MinimumConfidenceThreshold
            };

            _logger.LogInformation("LLM 合併完成，工作ID: {MergeJobId}，處理時間: {ProcessingTime}ms", mergeJobId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "LLM 合併過程發生錯誤，工作ID: {MergeJobId}", mergeJobId);

            return new LLMAssistedMergeResult
            {
                MergeJobId = mergeJobId,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RequiresHumanReview = true
            };
        }
    }

    /// <summary>
    /// 評估 LLM 合併結果的品質
    /// </summary>
    public async Task<MergeQualityAssessment> AssessMergeQualityAsync(
        LLMAssistedMergeResult mergeResult,
        List<SegmentSummaryTask> originalSummaries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始品質評估，合併工作ID: {MergeJobId}", mergeResult.MergeJobId);

            var metrics = new QualityMetrics();
            var issues = new List<QualityIssue>();
            var recommendations = new List<string>();

            // 評估內容完整性
            metrics.ContentCompleteness = await AssessContentCompletenessAsync(mergeResult.FinalSummary, originalSummaries, cancellationToken);

            // 評估資訊準確性
            metrics.InformationAccuracy = await AssessInformationAccuracyAsync(mergeResult.FinalSummary, originalSummaries, cancellationToken);

            // 評估語言品質
            metrics.LanguageQuality = await AssessLanguageQualityAsync(mergeResult.FinalSummary, cancellationToken);

            // 評估結構合理性
            metrics.StructuralCoherence = await AssessStructuralCoherenceAsync(mergeResult.FinalSummary, cancellationToken);

            // 評估重複內容
            metrics.DuplicationScore = await AssessDuplicationAsync(mergeResult.FinalSummary, cancellationToken);

            // 評估關鍵資訊保留度
            metrics.KeyInformationRetention = await AssessKeyInformationRetentionAsync(mergeResult.FinalSummary, originalSummaries, cancellationToken);

            // 識別品質問題
            await IdentifyQualityIssuesAsync(mergeResult.FinalSummary, originalSummaries, issues, cancellationToken);

            // 生成建議
            GenerateRecommendations(metrics, issues, recommendations);

            // 計算整體品質分數
            var overallScore = CalculateOverallQualityScore(metrics);
            var passesThreshold = overallScore >= _config.MinimumQualityThreshold;

            return new MergeQualityAssessment
            {
                OverallQualityScore = overallScore,
                Metrics = metrics,
                Issues = issues,
                Recommendations = recommendations,
                PassesQualityThreshold = passesThreshold
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "品質評估過程發生錯誤，合併工作ID: {MergeJobId}", mergeResult.MergeJobId);
            throw;
        }
    }

    /// <summary>
    /// 融合規則式合併與 LLM 合併結果
    /// </summary>
    public async Task<MergeResult> FuseMergeResultsAsync(
        MergeResult ruleBasedResult,
        LLMAssistedMergeResult llmResult,
        FusionStrategy fusionStrategy = FusionStrategy.Intelligent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始融合合併結果，策略: {Strategy}", fusionStrategy);

            return fusionStrategy switch
            {
                FusionStrategy.PreferLLM => await ConvertLLMResultToMergeResult(llmResult, cancellationToken),
                FusionStrategy.PreferRuleBased => ruleBasedResult,
                FusionStrategy.Intelligent => await IntelligentFusionAsync(ruleBasedResult, llmResult, cancellationToken),
                FusionStrategy.WeightedFusion => await WeightedFusionAsync(ruleBasedResult, llmResult, cancellationToken),
                FusionStrategy.StagedFusion => await StagedFusionAsync(ruleBasedResult, llmResult, cancellationToken),
                _ => await IntelligentFusionAsync(ruleBasedResult, llmResult, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "融合合併結果時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 對 LLM 合併結果進行後處理
    /// </summary>
    public async Task<LLMAssistedMergeResult> PostProcessLLMResultAsync(
        LLMAssistedMergeResult llmResult,
        PostProcessingOptions? postProcessingOptions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始後處理 LLM 合併結果，工作ID: {MergeJobId}", llmResult.MergeJobId);

            var options = postProcessingOptions ?? new PostProcessingOptions();
            var processedSummary = llmResult.FinalSummary;

            // 語言檢查
            if (options.EnableLanguageCheck)
            {
                processedSummary = await PerformLanguageCheckAsync(processedSummary, cancellationToken);
            }

            // 格式標準化
            if (options.EnableFormatNormalization)
            {
                processedSummary = await NormalizeFormatAsync(processedSummary, cancellationToken);
            }

            // 重複內容清理
            if (options.EnableDuplicationCleaning)
            {
                processedSummary = await CleanDuplicatedContentAsync(processedSummary, cancellationToken);
            }

            // 長度調整
            if (options.EnableLengthAdjustment && options.TargetLengthLimit.HasValue)
            {
                processedSummary = await AdjustLengthAsync(processedSummary, options.TargetLengthLimit.Value, cancellationToken);
            }

            // 執行自訂規則
            foreach (var rule in options.CustomRules)
            {
                processedSummary = await ApplyCustomRuleAsync(processedSummary, rule, cancellationToken);
            }

            // 建立後處理後的結果
            var result = new LLMAssistedMergeResult
            {
                MergeJobId = llmResult.MergeJobId,
                FinalSummary = processedSummary,
                ModelInfo = llmResult.ModelInfo,
                UsedPrompt = llmResult.UsedPrompt,
                RawResponse = llmResult.RawResponse,
                Statistics = llmResult.Statistics,
                ProcessingTimeMs = llmResult.ProcessingTimeMs,
                ConfidenceScore = llmResult.ConfidenceScore,
                CreatedAt = llmResult.CreatedAt,
                RequiresHumanReview = llmResult.RequiresHumanReview
            };

            // 更新統計資訊
            result.Statistics.MergedWords = CountWords(processedSummary);

            _logger.LogInformation("後處理完成，工作ID: {MergeJobId}", llmResult.MergeJobId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "後處理過程發生錯誤，工作ID: {MergeJobId}", llmResult.MergeJobId);
            throw;
        }
    }

    /// <summary>
    /// 生成 LLM 合併提示詞
    /// </summary>
    public string GenerateMergePrompt(
        List<SegmentSummaryTask> summaries,
        MergeStrategy strategy,
        UserMergePreferences? userPreferences = null)
    {
        try
        {
            if (!_promptTemplates.TryGetValue(strategy, out var template))
            {
                template = _promptTemplates[MergeStrategy.Balanced];
                _logger.LogWarning("未找到策略 {Strategy} 的提示詞模板，使用預設模板", strategy);
            }

            // 準備摘要內容
            var summaryBuilder = new StringBuilder();
            for (int i = 0; i < summaries.Count; i++)
            {
                summaryBuilder.AppendLine($"摘要 {i + 1}:");
                summaryBuilder.AppendLine(summaries[i].SummaryResult);
                summaryBuilder.AppendLine();
            }

            // 替換模板變量
            var prompt = template
                .Replace("{summaries}", summaryBuilder.ToString())
                .Replace("{targetLength}", CalculateTargetLength(summaries, userPreferences).ToString());

            // 如果是自訂策略，添加自訂要求
            if (strategy == MergeStrategy.Custom && userPreferences?.CustomWeights.Any() == true)
            {
                var customRequirements = userPreferences.CustomWeights
                    .Select(kv => $"{kv.Key}: {kv.Value}")
                    .ToList();
                prompt = prompt.Replace("{customRequirements}", string.Join("\n", customRequirements));
            }

            // 添加用戶偏好設定
            if (userPreferences != null)
            {
                prompt = AppendUserPreferences(prompt, userPreferences);
            }

            return prompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成合併提示詞時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 驗證 LLM 合併結果的正確性
    /// </summary>
    public async Task<ValidationResult> ValidateLLMMergeResultAsync(
        LLMAssistedMergeResult mergeResult,
        List<SegmentSummaryTask> originalSummaries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始驗證 LLM 合併結果，工作ID: {MergeJobId}", mergeResult.MergeJobId);

            var validationResult = new ValidationResult { IsValid = true };
            var validationScore = 0.0;
            var scoreComponents = 0;

            // 基本格式驗證
            await ValidateBasicFormatAsync(mergeResult.FinalSummary, validationResult, cancellationToken);

            // 內容完整性驗證
            var completenessScore = await ValidateContentCompletenessAsync(mergeResult.FinalSummary, originalSummaries, validationResult, cancellationToken);
            validationScore += completenessScore;
            scoreComponents++;

            // 邏輯一致性驗證
            var consistencyScore = await ValidateLogicalConsistencyAsync(mergeResult.FinalSummary, validationResult, cancellationToken);
            validationScore += consistencyScore;
            scoreComponents++;

            // 語言品質驗證
            var languageScore = await ValidateLanguageQualityAsync(mergeResult.FinalSummary, validationResult, cancellationToken);
            validationScore += languageScore;
            scoreComponents++;

            // 長度合理性驗證
            var lengthScore = ValidateLengthReasonableness(mergeResult.FinalSummary, originalSummaries, validationResult);
            validationScore += lengthScore;
            scoreComponents++;

            // 計算最終驗證分數
            validationResult.ValidationScore = scoreComponents > 0 ? validationScore / scoreComponents : 0.0;
            validationResult.IsValid = validationResult.Errors.Count == 0 && validationResult.ValidationScore >= _config.MinimumValidationScore;

            _logger.LogInformation("驗證完成，工作ID: {MergeJobId}，驗證分數: {Score}，是否通過: {IsValid}", 
                mergeResult.MergeJobId, validationResult.ValidationScore, validationResult.IsValid);

            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證過程發生錯誤，工作ID: {MergeJobId}", mergeResult.MergeJobId);
            throw;
        }
    }

    #region 私有輔助方法

    /// <summary>
    /// 呼叫 LLM 服務 API
    /// </summary>
    private async Task<LLMResponse> CallLLMServiceAsync(string prompt, CancellationToken cancellationToken)
    {
        // 這裡使用現有的 ISummaryService 來呼叫 LLM
        // 實際實作中會根據配置調用 OpenAI 或 Ollama API
        var summaryContent = await _summaryService.SummarizeAsync(prompt, cancellationToken);

        return new LLMResponse
        {
            Content = summaryContent,
            ModelInfo = new LLMModelInfo
            {
                ModelName = "Default", // 從配置中獲取實際模型名稱
                Provider = "OpenAI/Ollama", // 從配置中確定
                Parameters = new Dictionary<string, object>()
            }
        };
    }

    /// <summary>
    /// 處理 LLM 回應
    /// </summary>
    private async Task<string> ProcessLLMResponseAsync(string rawResponse, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // 模擬異步處理
        
        // 清理回應格式
        var processed = rawResponse
            .Trim()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        // 移除多餘的空行
        processed = Regex.Replace(processed, @"\n{3,}", "\n\n");

        return processed;
    }

    /// <summary>
    /// 計算詞數
    /// </summary>
    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // 中文字符數 + 英文單詞數
        var chineseChars = Regex.Matches(text, @"[\u4e00-\u9fa5]").Count;
        var englishWords = Regex.Matches(text, @"\b[a-zA-Z]+\b").Count;

        return chineseChars + englishWords;
    }

    /// <summary>
    /// 計算資訊保留度
    /// </summary>
    private async Task<double> CalculateInformationRetentionAsync(
        List<SegmentSummaryTask> originalSummaries,
        string mergedSummary,
        CancellationToken cancellationToken)
    {
        // 使用文字相似度計算資訊保留度
        var totalSimilarity = 0.0;
        foreach (var summary in originalSummaries)
        {
            var similarity = await _similarityCalculator.CalculateSimilarityAsync(
                summary.SummaryResult, mergedSummary, SimilarityType.Semantic, cancellationToken);
            totalSimilarity += similarity;
        }

        return originalSummaries.Count > 0 ? totalSimilarity / originalSummaries.Count : 0.0;
    }

    /// <summary>
    /// 評估邏輯一致性
    /// </summary>
    private async Task<double> AssessLogicalConsistencyAsync(string summary, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // 模擬異步處理
        
        // 簡單的邏輯一致性評估
        // 實際實作中可以使用更複雜的 NLP 方法
        var sentences = summary.Split(new[] { '.', '。', '!', '！', '?', '？' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length <= 1) return 1.0;

        // 檢查前後句子的連貫性
        var consistencyScore = 0.8; // 基礎分數

        // 檢查是否有矛盾詞彙
        var contradictionIndicators = new[] { "但是", "然而", "相反", "不過", "卻", "而" };
        var contradictionCount = contradictionIndicators.Sum(indicator => 
            Regex.Matches(summary, indicator).Count);

        // 適度的轉折是正常的，過多可能表示邏輯混亂
        if (contradictionCount > sentences.Length * 0.3)
        {
            consistencyScore -= 0.2;
        }

        return Math.Max(0.0, Math.Min(1.0, consistencyScore));
    }

    /// <summary>
    /// 評估語言流暢度
    /// </summary>
    private async Task<double> AssessLanguageFluencyAsync(string summary, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // 模擬異步處理
        
        // 簡單的語言流暢度評估
        var fluencyScore = 1.0;

        // 檢查重複詞彙
        var words = Regex.Split(summary, @"\W+")
            .Where(w => !string.IsNullOrEmpty(w) && w.Length > 1)
            .GroupBy(w => w.ToLower())
            .ToDictionary(g => g.Key, g => g.Count());

        var totalWords = words.Values.Sum();
        var uniqueWords = words.Count;

        if (totalWords > 0)
        {
            var repetitionRatio = 1.0 - (double)uniqueWords / totalWords;
            if (repetitionRatio > 0.3) // 重複率過高
            {
                fluencyScore -= 0.2;
            }
        }

        // 檢查句子長度分布
        var sentences = summary.Split(new[] { '.', '。', '!', '！', '?', '？' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length > 0)
        {
            var avgSentenceLength = sentences.Average(s => s.Length);
            if (avgSentenceLength > 200 || avgSentenceLength < 10) // 句子過長或過短
            {
                fluencyScore -= 0.1;
            }
        }

        return Math.Max(0.0, Math.Min(1.0, fluencyScore));
    }

    /// <summary>
    /// 計算信心分數
    /// </summary>
    private async Task<double> CalculateConfidenceScoreAsync(
        List<SegmentSummaryTask> originalSummaries,
        string mergedSummary,
        LLMResponse llmResponse,
        CancellationToken cancellationToken)
    {
        // 綜合多個因素計算信心分數
        var factors = new List<double>();

        // 因素1: 資訊保留度
        var informationRetention = await CalculateInformationRetentionAsync(originalSummaries, mergedSummary, cancellationToken);
        factors.Add(informationRetention);

        // 因素2: 語言品質
        var languageQuality = await AssessLanguageFluencyAsync(mergedSummary, cancellationToken);
        factors.Add(languageQuality);

        // 因素3: 長度合理性
        var originalTotalLength = originalSummaries.Sum(s => s.SummaryResult.Length);
        var mergedLength = mergedSummary.Length;
        var lengthRatio = originalTotalLength > 0 ? (double)mergedLength / originalTotalLength : 0;
        var lengthScore = lengthRatio switch
        {
            < 0.2 => 0.6, // 壓縮過度
            >= 0.2 and < 0.8 => 1.0, // 合理範圍
            >= 0.8 => 0.8, // 壓縮不足
            _ => 0.5 // 其他情況（如NaN等異常值）
        };
        factors.Add(lengthScore);

        return factors.Average();
    }

    /// <summary>
    /// 計算目標長度
    /// </summary>
    private static int CalculateTargetLength(List<SegmentSummaryTask> summaries, UserMergePreferences? preferences)
    {
        var originalTotalWords = summaries.Sum(s => CountWords(s.SummaryResult));
        
        // 根據輸出長度偏好調整
        var ratio = preferences?.LengthPreference switch
        {
            OutputLengthPreference.VeryShort => 0.3,
            OutputLengthPreference.Short => 0.5,
            OutputLengthPreference.Medium => 0.7,
            OutputLengthPreference.Long => 0.9,
            OutputLengthPreference.VeryLong => 1.0,
            _ => 0.6
        };

        return (int)(originalTotalWords * ratio);
    }

    /// <summary>
    /// 添加用戶偏好設定到提示詞
    /// </summary>
    private static string AppendUserPreferences(string prompt, UserMergePreferences preferences)
    {
        var additionalRequirements = new List<string>();

        if (preferences.DetailLevel != DetailLevelPreference.Balanced)
        {
            additionalRequirements.Add($"詳細程度: {GetDetailLevelDescription(preferences.DetailLevel)}");
        }

        if (preferences.StructureLevel != StructureLevelPreference.Moderate)
        {
            additionalRequirements.Add($"結構要求: {GetStructureLevelDescription(preferences.StructureLevel)}");
        }

        if (preferences.DuplicateTolerance != DuplicateToleranceLevel.Low)
        {
            additionalRequirements.Add($"重複容忍度: {GetDuplicateToleranceDescription(preferences.DuplicateTolerance)}");
        }

        if (additionalRequirements.Any())
        {
            prompt += "\n\n額外要求：\n" + string.Join("\n", additionalRequirements);
        }

        return prompt;
    }

    private static string GetDetailLevelDescription(DetailLevelPreference level)
    {
        return level switch
        {
            DetailLevelPreference.HighlySimplified => "只保留最核心的資訊",
            DetailLevelPreference.Simplified => "保留基本資訊和要點",
            DetailLevelPreference.Balanced => "保留適度的細節和說明",
            DetailLevelPreference.Detailed => "保留詳細資訊和完整描述",
            DetailLevelPreference.VeryDetailed => "保留所有重要資訊和細節",
            _ => "自動判斷適當的詳細程度"
        };
    }

    private static string GetStructureLevelDescription(StructureLevelPreference level)
    {
        return level switch
        {
            StructureLevelPreference.Unstructured => "使用平鋪式結構，不分段落",
            StructureLevelPreference.LightlyStructured => "使用基本的段落分隔",
            StructureLevelPreference.Moderate => "使用清晰的標題和段落結構",
            StructureLevelPreference.HighlyStructured => "使用層次化的結構組織",
            StructureLevelPreference.FullyStructured => "使用完整的結構化格式",
            _ => "自動選擇適當的結構"
        };
    }

    private static string GetDuplicateToleranceDescription(DuplicateToleranceLevel level)
    {
        return level switch
        {
            DuplicateToleranceLevel.None => "嚴格去除所有重複內容",
            DuplicateToleranceLevel.Low => "去除明顯的重複內容",
            DuplicateToleranceLevel.Medium => "僅去除完全重複的內容",
            DuplicateToleranceLevel.High => "允許適度的內容重複",
            DuplicateToleranceLevel.Permissive => "允許大量內容重複",
            _ => "自動判斷重複內容處理方式"
        };
    }

    #endregion

    #region 品質評估相關方法

    private async Task<double> AssessContentCompletenessAsync(string mergedSummary, List<SegmentSummaryTask> originalSummaries, CancellationToken cancellationToken)
    {
        // 使用語義相似度評估內容完整性
        var completenessScores = new List<double>();

        foreach (var summary in originalSummaries)
        {
            var similarity = await _similarityCalculator.CalculateSimilarityAsync(
                summary.SummaryResult, mergedSummary, SimilarityType.Semantic, cancellationToken);
            completenessScores.Add(similarity);
        }

        return completenessScores.Any() ? completenessScores.Average() : 0.0;
    }

    private async Task<double> AssessInformationAccuracyAsync(string mergedSummary, List<SegmentSummaryTask> originalSummaries, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 簡化的準確性評估
        // 實際實作中可以使用更複雜的事實檢查方法
        return 0.85; // 預設準確性分數
    }

    private async Task<double> AssessLanguageQualityAsync(string mergedSummary, CancellationToken cancellationToken)
    {
        return await AssessLanguageFluencyAsync(mergedSummary, cancellationToken);
    }

    private async Task<double> AssessStructuralCoherenceAsync(string mergedSummary, CancellationToken cancellationToken)
    {
        return await AssessLogicalConsistencyAsync(mergedSummary, cancellationToken);
    }

    private async Task<double> AssessDuplicationAsync(string mergedSummary, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // 檢測重複句子
        var sentences = mergedSummary.Split(new[] { '.', '。', '!', '！', '?', '？' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (sentences.Count <= 1) return 0.0;

        var duplicates = 0;
        for (int i = 0; i < sentences.Count - 1; i++)
        {
            for (int j = i + 1; j < sentences.Count; j++)
            {
                var similarity = _similarityCalculator.CalculateJaccardSimilarity(sentences[i], sentences[j]);
                if (similarity > 0.8) // 高相似度被視為重複
                {
                    duplicates++;
                }
            }
        }

        return duplicates > 0 ? (double)duplicates / sentences.Count : 0.0;
    }

    private async Task<double> AssessKeyInformationRetentionAsync(string mergedSummary, List<SegmentSummaryTask> originalSummaries, CancellationToken cancellationToken)
    {
        return await CalculateInformationRetentionAsync(originalSummaries, mergedSummary, cancellationToken);
    }

    private async Task IdentifyQualityIssuesAsync(string mergedSummary, List<SegmentSummaryTask> originalSummaries, List<QualityIssue> issues, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // 檢查長度異常
        var originalTotalLength = originalSummaries.Sum(s => s.SummaryResult.Length);
        var mergedLength = mergedSummary.Length;
        
        if (mergedLength < originalTotalLength * 0.1)
        {
            issues.Add(new QualityIssue
            {
                Type = QualityIssueType.Completeness,
                Severity = QualityIssueSeverity.High,
                Description = "合併後的摘要可能過於簡短，可能遺失重要資訊"
            });
        }

        if (mergedLength > originalTotalLength * 1.2)
        {
            issues.Add(new QualityIssue
            {
                Type = QualityIssueType.Conciseness,
                Severity = QualityIssueSeverity.Low,
                Description = "合併後的摘要長度未有效壓縮"
            });
        }

        // 檢查空內容
        if (string.IsNullOrWhiteSpace(mergedSummary))
        {
            issues.Add(new QualityIssue
            {
                Type = QualityIssueType.Completeness,
                Severity = QualityIssueSeverity.Critical,
                Description = "合併結果為空或僅包含空白字符"
            });
        }
    }

    private static void GenerateRecommendations(QualityMetrics metrics, List<QualityIssue> issues, List<string> recommendations)
    {
        if (metrics.ContentCompleteness < 0.7)
        {
            recommendations.Add("建議檢查合併邏輯，確保重要資訊得到保留");
        }

        if (metrics.LanguageQuality < 0.8)
        {
            recommendations.Add("建議進行語言後處理，提升表達品質");
        }

        if (metrics.DuplicationScore > 0.3)
        {
            recommendations.Add("建議加強重複內容檢測和清理");
        }

        if (issues.Any(i => i.Severity == QualityIssueSeverity.Critical))
        {
            recommendations.Add("發現嚴重問題，建議人工審查");
        }
    }

    private static double CalculateOverallQualityScore(QualityMetrics metrics)
    {
        // 加權平均計算整體品質分數
        var weights = new Dictionary<string, double>
        {
            [nameof(metrics.ContentCompleteness)] = 0.25,
            [nameof(metrics.InformationAccuracy)] = 0.2,
            [nameof(metrics.LanguageQuality)] = 0.2,
            [nameof(metrics.StructuralCoherence)] = 0.15,
            [nameof(metrics.KeyInformationRetention)] = 0.15,
            [nameof(metrics.DuplicationScore)] = 0.05 // 重複分數越低越好，權重較小
        };

        var score = 
            metrics.ContentCompleteness * weights[nameof(metrics.ContentCompleteness)] +
            metrics.InformationAccuracy * weights[nameof(metrics.InformationAccuracy)] +
            metrics.LanguageQuality * weights[nameof(metrics.LanguageQuality)] +
            metrics.StructuralCoherence * weights[nameof(metrics.StructuralCoherence)] +
            metrics.KeyInformationRetention * weights[nameof(metrics.KeyInformationRetention)] +
            (1.0 - metrics.DuplicationScore) * weights[nameof(metrics.DuplicationScore)]; // 重複分數反向計算

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    #endregion

    #region 融合相關方法

    private async Task<MergeResult> ConvertLLMResultToMergeResult(LLMAssistedMergeResult llmResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        return new MergeResult
        {
            MergeJobId = llmResult.MergeJobId,
            FinalSummary = llmResult.FinalSummary,
            Statistics = new MergeStatistics
            {
                InputSegmentCount = llmResult.Statistics.OriginalSummaryCount,
                OriginalLength = llmResult.Statistics.OriginalTotalWords,
                FinalLength = llmResult.Statistics.MergedWords,
                CompressionRatio = llmResult.Statistics.CompressionRatio
            },
            AppliedStrategy = MergeStrategy.Custom, // 標記為 LLM 生成
            QualityMetrics = new MergeQualityMetrics
            {
                OverallQuality = llmResult.ConfidenceScore
            },
            ProcessingTime = TimeSpan.FromMilliseconds(llmResult.ProcessingTimeMs),
            CompletedAt = llmResult.CreatedAt
        };
    }

    private async Task<MergeResult> IntelligentFusionAsync(MergeResult ruleBasedResult, LLMAssistedMergeResult llmResult, CancellationToken cancellationToken)
    {
        // 基於品質評估選擇最佳結果
        var llmQuality = await AssessMergeQualityAsync(llmResult, new List<SegmentSummaryTask>(), cancellationToken);
        
        if (llmQuality.OverallQualityScore > ruleBasedResult.QualityMetrics.OverallQuality)
        {
            return await ConvertLLMResultToMergeResult(llmResult, cancellationToken);
        }
        else
        {
            return ruleBasedResult;
        }
    }

    private async Task<MergeResult> WeightedFusionAsync(MergeResult ruleBasedResult, LLMAssistedMergeResult llmResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 簡化實作：選擇品質更高的結果
        // 實際實作中可以進行更複雜的內容融合
        return llmResult.ConfidenceScore > ruleBasedResult.QualityMetrics.OverallQuality 
            ? await ConvertLLMResultToMergeResult(llmResult, cancellationToken)
            : ruleBasedResult;
    }

    private async Task<MergeResult> StagedFusionAsync(MergeResult ruleBasedResult, LLMAssistedMergeResult llmResult, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 分階段融合：結構來自規則式，內容來自 LLM
        // 這裡簡化為選擇更好的結果
        return await IntelligentFusionAsync(ruleBasedResult, llmResult, cancellationToken);
    }

    #endregion

    #region 後處理相關方法

    private async Task<string> PerformLanguageCheckAsync(string text, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 簡單的語言檢查和修正
        // 移除多餘標點符號
        var processed = Regex.Replace(text, @"[。！？]{2,}", "。");
        processed = Regex.Replace(processed, @"[,.;:]{2,}", "，");
        
        return processed;
    }

    private async Task<string> NormalizeFormatAsync(string text, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 格式標準化
        var processed = text
            .Replace("  ", " ") // 移除多餘空格
            .Replace("\n\n\n", "\n\n") // 標準化行距
            .Trim();
        
        return processed;
    }

    private async Task<string> CleanDuplicatedContentAsync(string text, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 移除重複句子
        var sentences = text.Split(new[] { '。', '！', '？' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
        
        return string.Join("。", sentences) + (sentences.Any() ? "。" : "");
    }

    private async Task<string> AdjustLengthAsync(string text, int targetLength, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        var currentLength = CountWords(text);
        
        if (currentLength <= targetLength)
        {
            return text;
        }
        
        // 簡單的截取方式，實際中應該使用更智能的方式
        var ratio = (double)targetLength / currentLength;
        var sentences = text.Split(new[] { '。', '！', '？' }, StringSplitOptions.RemoveEmptyEntries);
        var targetSentenceCount = Math.Max(1, (int)(sentences.Length * ratio));
        
        return string.Join("。", sentences.Take(targetSentenceCount)) + "。";
    }

    private async Task<string> ApplyCustomRuleAsync(string text, string rule, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        // 簡化實作：假設規則是正規表達式替換
        // 格式：rule = "pattern|replacement"
        var parts = rule.Split('|');
        if (parts.Length == 2)
        {
            try
            {
                return Regex.Replace(text, parts[0], parts[1]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自訂規則執行失敗: {Rule}", rule);
            }
        }
        
        return text;
    }

    #endregion

    #region 驗證相關方法

    private async Task ValidateBasicFormatAsync(string summary, ValidationResult result, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(summary))
        {
            result.Errors.Add(new ValidationError
            {
                ErrorType = "EmptyContent",
                Message = "摘要內容為空",
                Severity = QualityIssueSeverity.Critical
            });
            result.IsValid = false;
        }
    }

    private async Task<double> ValidateContentCompletenessAsync(string mergedSummary, List<SegmentSummaryTask> originalSummaries, ValidationResult result, CancellationToken cancellationToken)
    {
        var completeness = await AssessContentCompletenessAsync(mergedSummary, originalSummaries, cancellationToken);
        
        if (completeness < 0.5)
        {
            result.Warnings.Add(new ValidationWarning
            {
                WarningType = "LowCompleteness",
                Message = "內容完整性較低，可能遺失重要資訊"
            });
        }

        return completeness;
    }

    private async Task<double> ValidateLogicalConsistencyAsync(string summary, ValidationResult result, CancellationToken cancellationToken)
    {
        var consistency = await AssessLogicalConsistencyAsync(summary, cancellationToken);

        if (consistency < 0.6)
        {
            result.Warnings.Add(new ValidationWarning
            {
                WarningType = "LogicalInconsistency",
                Message = "邏輯一致性較低，建議檢查內容連貫性"
            });
        }

        return consistency;
    }

    private async Task<double> ValidateLanguageQualityAsync(string summary, ValidationResult result, CancellationToken cancellationToken)
    {
        var quality = await AssessLanguageQualityAsync(summary, cancellationToken);

        if (quality < 0.7)
        {
            result.Warnings.Add(new ValidationWarning
            {
                WarningType = "LanguageQuality",
                Message = "語言品質有待改善"
            });
        }

        return quality;
    }

    private double ValidateLengthReasonableness(string mergedSummary, List<SegmentSummaryTask> originalSummaries, ValidationResult result)
    {
        var originalTotalWords = originalSummaries.Sum(s => CountWords(s.SummaryResult));
        var mergedWords = CountWords(mergedSummary);
        
        if (originalTotalWords == 0) return 1.0;

        var ratio = (double)mergedWords / originalTotalWords;

        if (ratio < 0.1)
        {
            result.Errors.Add(new ValidationError
            {
                ErrorType = "TooShort",
                Message = "合併結果過於簡短",
                Severity = QualityIssueSeverity.High
            });
            return 0.3;
        }

        if (ratio > 1.5)
        {
            result.Warnings.Add(new ValidationWarning
            {
                WarningType = "TooLong",
                Message = "合併結果未有效壓縮"
            });
            return 0.7;
        }

        return 1.0;
    }

    #endregion
}

/// <summary>
/// LLM 回應包裝類別
/// </summary>
internal class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public LLMModelInfo ModelInfo { get; set; } = new();
}