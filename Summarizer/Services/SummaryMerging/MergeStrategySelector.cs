using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;
using Summarizer.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Summarizer.Services.SummaryMerging;

/// <summary>
/// 合併策略選擇器實作
/// </summary>
public class MergeStrategySelector : IMergeStrategySelector
{
    private readonly ILogger<MergeStrategySelector> _logger;
    private readonly SummaryMergingConfig _config;
    private readonly ITextSimilarityCalculator _similarityCalculator;
    
    // 簡單的學習記憶體存儲（生產環境應使用資料庫）
    private readonly Dictionary<string, StrategyPerformance> _strategyPerformanceHistory = new();

    public MergeStrategySelector(
        ILogger<MergeStrategySelector> logger,
        IOptions<SummaryMergingConfig> config,
        ITextSimilarityCalculator similarityCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _similarityCalculator = similarityCalculator ?? throw new ArgumentNullException(nameof(similarityCalculator));
    }

    public async Task<StrategyRecommendation> SelectOptimalStrategyAsync(
        List<SegmentSummaryTask> summaries,
        UserMergePreferences? userPreferences = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始自動選擇合併策略，摘要數量: {Count}", summaries.Count);

        try
        {
            // 分析內容特徵
            var characteristics = await AnalyzeContentCharacteristicsAsync(summaries, cancellationToken);
            
            // 評估各種策略
            var evaluations = await EvaluateStrategiesAsync(summaries, cancellationToken);
            
            // 結合使用者偏好進行策略選擇
            var recommendation = SelectBasedOnAnalysis(characteristics, evaluations, userPreferences);
            
            _logger.LogInformation("策略選擇完成，推薦策略: {Strategy}，信心度: {Confidence:F2}", 
                recommendation.RecommendedStrategy, recommendation.Confidence);
            
            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自動選擇合併策略時發生錯誤");
            return GetFallbackRecommendation(userPreferences);
        }
    }

    public async Task<Dictionary<MergeStrategy, StrategyEvaluation>> EvaluateStrategiesAsync(
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default)
    {
        var evaluations = new Dictionary<MergeStrategy, StrategyEvaluation>();
        var characteristics = await AnalyzeContentCharacteristicsAsync(summaries, cancellationToken);

        // 評估簡潔式合併
        evaluations[MergeStrategy.Concise] = EvaluateConciseStrategy(characteristics, summaries);
        
        // 評估詳細式合併
        evaluations[MergeStrategy.Detailed] = EvaluateDetailedStrategy(characteristics, summaries);
        
        // 評估結構化合併
        evaluations[MergeStrategy.Structured] = EvaluateStructuredStrategy(characteristics, summaries);
        
        // 評估平衡式合併
        evaluations[MergeStrategy.Balanced] = EvaluateBalancedStrategy(characteristics, summaries);
        
        // 評估自訂合併
        evaluations[MergeStrategy.Custom] = EvaluateCustomStrategy(characteristics, summaries);

        return evaluations;
    }

    public async Task<MergeParameters> CreateCustomParametersAsync(
        UserMergePreferences preferences,
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default)
    {
        var characteristics = await AnalyzeContentCharacteristicsAsync(summaries, cancellationToken);
        
        var parameters = new MergeParameters();
        
        // 根據使用者偏好調整目標長度
        parameters.TargetLength = CalculateTargetLength(preferences.LengthPreference, characteristics);
        
        // 根據重複容忍度調整相似度閾值
        parameters.SimilarityThreshold = CalculateSimilarityThreshold(preferences.DuplicateTolerance);
        
        // 根據詳細程度設定重要性閾值
        parameters.ImportanceThreshold = CalculateImportanceThreshold(preferences.DetailLevel);
        
        // 設定結構保留選項
        parameters.PreserveStructure = preferences.StructureLevel >= StructureLevelPreference.Moderate;
        
        // 設定來源引用
        parameters.GenerateSourceReferences = preferences.PreserveSourceInfo;
        
        // 應用自訂權重
        ApplyCustomWeights(parameters, preferences.CustomWeights);
        
        return parameters;
    }

    public async Task<ContentCharacteristics> AnalyzeContentCharacteristicsAsync(
        List<SegmentSummaryTask> summaries,
        CancellationToken cancellationToken = default)
    {
        var validSummaries = summaries.Where(s => !string.IsNullOrEmpty(s.SummaryResult)).ToList();
        
        if (!validSummaries.Any())
        {
            return new ContentCharacteristics { TotalSegments = 0 };
        }

        var characteristics = new ContentCharacteristics
        {
            TotalSegments = validSummaries.Count
        };

        // 計算長度統計
        var lengths = validSummaries.Select(s => s.SummaryResult.Length).ToList();
        characteristics.AverageLength = lengths.Average();
        characteristics.LengthVariance = CalculateVariance(lengths);

        // 分析主題多樣性
        characteristics.TopicDiversity = await CalculateTopicDiversityAsync(validSummaries, cancellationToken);

        // 計算內容重疊度
        characteristics.ContentOverlap = await CalculateContentOverlapAsync(validSummaries, cancellationToken);

        // 評估結構化程度
        characteristics.StructureLevel = CalculateStructureLevel(validSummaries);

        // 評估複雜性
        characteristics.ComplexityScore = CalculateComplexityScore(validSummaries, characteristics);

        // 提取主要主題
        characteristics.MainTopics = ExtractMainTopics(validSummaries);

        // 分析內容類型分佈
        characteristics.ContentTypeDistribution = AnalyzeContentTypeDistribution(validSummaries);

        // 分析語言特徵
        characteristics.LanguageFeatures = AnalyzeLanguageFeatures(validSummaries);

        return characteristics;
    }

    public async Task<bool> LearnFromResultAsync(
        MergeStrategy strategy,
        MergeResult result,
        UserFeedback? userFeedback = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // 避免 CS1998 警告
        try
        {
            var strategyKey = strategy.ToString();
            
            if (!_strategyPerformanceHistory.ContainsKey(strategyKey))
            {
                _strategyPerformanceHistory[strategyKey] = new StrategyPerformance();
            }

            var performance = _strategyPerformanceHistory[strategyKey];
            performance.UsageCount++;
            
            // 基於品質指標計算性能分數
            var qualityScore = result.QualityMetrics?.OverallQuality ?? 0.5;
            performance.AverageQualityScore = 
                (performance.AverageQualityScore * (performance.UsageCount - 1) + qualityScore) / performance.UsageCount;

            // 記錄處理時間
            performance.AverageProcessingTime = 
                (performance.AverageProcessingTime * (performance.UsageCount - 1) + result.ProcessingTime.TotalMilliseconds) 
                / performance.UsageCount;

            // 如果有使用者回饋，記錄滿意度
            if (userFeedback != null)
            {
                performance.UserSatisfactionCount++;
                var satisfactionScore = userFeedback.OverallSatisfaction / 5.0;
                performance.AverageUserSatisfaction = 
                    (performance.AverageUserSatisfaction * (performance.UserSatisfactionCount - 1) + satisfactionScore) 
                    / performance.UserSatisfactionCount;
            }

            _logger.LogInformation("策略學習記錄更新：{Strategy}，品質分數：{Quality:F2}，滿意度：{Satisfaction:F2}", 
                strategy, performance.AverageQualityScore, performance.AverageUserSatisfaction);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "學習記錄更新失敗");
            return false;
        }
    }

    #region 私有方法

    private StrategyRecommendation SelectBasedOnAnalysis(
        ContentCharacteristics characteristics,
        Dictionary<MergeStrategy, StrategyEvaluation> evaluations,
        UserMergePreferences? userPreferences)
    {
        var scores = new Dictionary<MergeStrategy, double>();
        
        // 基礎適用性分數
        foreach (var evaluation in evaluations)
        {
            scores[evaluation.Key] = evaluation.Value.SuitabilityScore * 0.4 +
                                   evaluation.Value.EstimatedQuality * 0.4 +
                                   evaluation.Value.EfficiencyScore * 0.2;
        }

        // 根據內容特徵調整分數
        AdjustScoresBasedOnCharacteristics(scores, characteristics);

        // 根據使用者偏好調整分數
        if (userPreferences != null)
        {
            AdjustScoresBasedOnPreferences(scores, userPreferences);
        }

        // 根據歷史性能調整分數
        AdjustScoresBasedOnHistory(scores);

        // 選擇最高分的策略
        var bestStrategy = scores.OrderByDescending(kvp => kvp.Value).First();
        
        var recommendation = new StrategyRecommendation
        {
            RecommendedStrategy = bestStrategy.Key,
            RecommendedMethod = DetermineOptimalMethod(bestStrategy.Key, characteristics),
            Confidence = Math.Min(bestStrategy.Value, 1.0),
            Reasons = GenerateRecommendationReasons(bestStrategy.Key, characteristics, userPreferences)
        };

        // 建立參數設定
        recommendation.Parameters = userPreferences != null
            ? CreateCustomParametersAsync(userPreferences, new List<SegmentSummaryTask>()).Result
            : CreateDefaultParameters(bestStrategy.Key);

        // 新增替代選項
        recommendation.Alternatives = CreateAlternatives(scores, bestStrategy.Key);

        return recommendation;
    }

    private StrategyEvaluation EvaluateConciseStrategy(ContentCharacteristics characteristics, List<SegmentSummaryTask> summaries)
    {
        var evaluation = new StrategyEvaluation
        {
            Strategy = MergeStrategy.Concise
        };

        // 簡潔式合併適合內容重疊度高、需要快速摘要的場景
        evaluation.SuitabilityScore = 
            (characteristics.ContentOverlap * 0.4 +
             (1.0 - characteristics.ComplexityScore) * 0.3 +
             (1.0 - characteristics.TopicDiversity) * 0.3);

        evaluation.EstimatedQuality = 0.7; // 簡潔但可能遺失細節
        evaluation.EfficiencyScore = 0.9; // 處理效率高

        evaluation.Advantages.AddRange(new[]
        {
            "處理速度快",
            "結果簡潔明瞭",
            "適合快速瀏覽",
            "減少資訊過載"
        });

        evaluation.Disadvantages.AddRange(new[]
        {
            "可能遺失重要細節",
            "不適合複雜主題",
            "上下文可能不完整"
        });

        evaluation.SuitableScenarios.AddRange(new[]
        {
            "快速概覽需求",
            "內容重複度高",
            "時間壓力大",
            "簡報摘要"
        });

        return evaluation;
    }

    private StrategyEvaluation EvaluateDetailedStrategy(ContentCharacteristics characteristics, List<SegmentSummaryTask> summaries)
    {
        var evaluation = new StrategyEvaluation
        {
            Strategy = MergeStrategy.Detailed
        };

        // 詳細式合併適合內容多樣、需要保留完整資訊的場景
        evaluation.SuitabilityScore = 
            (characteristics.TopicDiversity * 0.4 +
             characteristics.ComplexityScore * 0.3 +
             (1.0 - characteristics.ContentOverlap) * 0.3);

        evaluation.EstimatedQuality = 0.9; // 保留完整資訊
        evaluation.EfficiencyScore = 0.6; // 處理時間較長

        evaluation.Advantages.AddRange(new[]
        {
            "保留完整資訊",
            "適合深度分析",
            "上下文完整",
            "細節豐富"
        });

        evaluation.Disadvantages.AddRange(new[]
        {
            "結果較冗長",
            "處理時間長",
            "可能包含重複內容",
            "資訊密度低"
        });

        evaluation.SuitableScenarios.AddRange(new[]
        {
            "研究報告",
            "詳細分析",
            "學術用途",
            "完整記錄"
        });

        return evaluation;
    }

    private StrategyEvaluation EvaluateStructuredStrategy(ContentCharacteristics characteristics, List<SegmentSummaryTask> summaries)
    {
        var evaluation = new StrategyEvaluation
        {
            Strategy = MergeStrategy.Structured
        };

        // 結構化合併適合主題多樣、需要分類整理的場景
        evaluation.SuitabilityScore = 
            (characteristics.TopicDiversity * 0.5 +
             characteristics.StructureLevel * 0.3 +
             (characteristics.TotalSegments > 5 ? 1.0 : 0.5) * 0.2);

        evaluation.EstimatedQuality = 0.8; // 組織良好但可能機械化
        evaluation.EfficiencyScore = 0.7; // 需要分類處理

        evaluation.Advantages.AddRange(new[]
        {
            "組織結構清晰",
            "易於查找資訊",
            "邏輯性強",
            "適合分類瀏覽"
        });

        evaluation.Disadvantages.AddRange(new[]
        {
            "可能顯得機械化",
            "需要預處理時間",
            "分類可能不準確",
            "缺乏流暢性"
        });

        evaluation.SuitableScenarios.AddRange(new[]
        {
            "多主題內容",
            "參考文檔",
            "分類整理",
            "知識管理"
        });

        return evaluation;
    }

    private StrategyEvaluation EvaluateBalancedStrategy(ContentCharacteristics characteristics, List<SegmentSummaryTask> summaries)
    {
        var evaluation = new StrategyEvaluation
        {
            Strategy = MergeStrategy.Balanced
        };

        // 平衡式合併是通用選擇，適應性強
        evaluation.SuitabilityScore = 0.8; // 通常是較安全的選擇
        evaluation.EstimatedQuality = 0.8; // 平衡品質
        evaluation.EfficiencyScore = 0.8; // 平衡效率

        evaluation.Advantages.AddRange(new[]
        {
            "平衡長度與品質",
            "適應性強",
            "通用性好",
            "風險較低"
        });

        evaluation.Disadvantages.AddRange(new[]
        {
            "可能不是最優選擇",
            "缺乏特色",
            "中庸表現"
        });

        evaluation.SuitableScenarios.AddRange(new[]
        {
            "一般用途",
            "不確定需求",
            "混合內容",
            "初次使用"
        });

        return evaluation;
    }

    private StrategyEvaluation EvaluateCustomStrategy(ContentCharacteristics characteristics, List<SegmentSummaryTask> summaries)
    {
        var evaluation = new StrategyEvaluation
        {
            Strategy = MergeStrategy.Custom
        };

        // 自訂策略需要使用者明確偏好
        evaluation.SuitabilityScore = 0.9; // 理論上最符合需求
        evaluation.EstimatedQuality = 0.95; // 客製化品質
        evaluation.EfficiencyScore = 0.5; // 需要額外配置時間

        evaluation.Advantages.AddRange(new[]
        {
            "完全客製化",
            "精確符合需求",
            "靈活性最高",
            "可微調控制"
        });

        evaluation.Disadvantages.AddRange(new[]
        {
            "需要明確偏好設定",
            "配置複雜",
            "調試時間長",
            "需要經驗"
        });

        evaluation.SuitableScenarios.AddRange(new[]
        {
            "特殊需求",
            "專業用途",
            "有明確偏好",
            "重複使用場景"
        });

        return evaluation;
    }

    private void AdjustScoresBasedOnCharacteristics(Dictionary<MergeStrategy, double> scores, ContentCharacteristics characteristics)
    {
        // 內容重疊度高時，偏向簡潔式
        if (characteristics.ContentOverlap > 0.7)
        {
            scores[MergeStrategy.Concise] *= 1.3;
        }

        // 主題多樣性高時，偏向結構化
        if (characteristics.TopicDiversity > 0.7)
        {
            scores[MergeStrategy.Structured] *= 1.2;
        }

        // 複雜性高時，偏向詳細式
        if (characteristics.ComplexityScore > 0.7)
        {
            scores[MergeStrategy.Detailed] *= 1.2;
        }

        // 分段數量少時，簡潔式效果可能不佳
        if (characteristics.TotalSegments <= 3)
        {
            scores[MergeStrategy.Concise] *= 0.8;
        }
    }

    private void AdjustScoresBasedOnPreferences(Dictionary<MergeStrategy, double> scores, UserMergePreferences preferences)
    {
        // 長度偏好調整
        switch (preferences.LengthPreference)
        {
            case OutputLengthPreference.VeryShort:
            case OutputLengthPreference.Short:
                scores[MergeStrategy.Concise] *= 1.5;
                scores[MergeStrategy.Detailed] *= 0.5;
                break;
            case OutputLengthPreference.Long:
            case OutputLengthPreference.VeryLong:
                scores[MergeStrategy.Detailed] *= 1.5;
                scores[MergeStrategy.Concise] *= 0.5;
                break;
        }

        // 結構化程度偏好調整
        if (preferences.StructureLevel >= StructureLevelPreference.HighlyStructured)
        {
            scores[MergeStrategy.Structured] *= 1.4;
        }

        // 詳細程度偏好調整
        switch (preferences.DetailLevel)
        {
            case DetailLevelPreference.HighlySimplified:
            case DetailLevelPreference.Simplified:
                scores[MergeStrategy.Concise] *= 1.3;
                break;
            case DetailLevelPreference.Detailed:
            case DetailLevelPreference.VeryDetailed:
                scores[MergeStrategy.Detailed] *= 1.3;
                break;
        }
    }

    private void AdjustScoresBasedOnHistory(Dictionary<MergeStrategy, double> scores)
    {
        foreach (var score in scores.ToList())
        {
            var strategyKey = score.Key.ToString();
            if (_strategyPerformanceHistory.ContainsKey(strategyKey))
            {
                var performance = _strategyPerformanceHistory[strategyKey];
                var historyBonus = (performance.AverageQualityScore + performance.AverageUserSatisfaction) / 2.0;
                scores[score.Key] *= (1.0 + historyBonus * 0.2); // 最多 20% 的歷史加成
            }
        }
    }

    private MergeMethod DetermineOptimalMethod(MergeStrategy strategy, ContentCharacteristics characteristics)
    {
        // 基於內容特徵和策略選擇最佳合併方法
        if (characteristics.ComplexityScore > 0.8 && characteristics.TopicDiversity > 0.7)
        {
            return MergeMethod.LLMAssisted; // 複雜內容使用 LLM 輔助
        }

        if (characteristics.TotalSegments > 10)
        {
            return MergeMethod.Hybrid; // 大量內容使用混合方法
        }

        return MergeMethod.RuleBased; // 一般情況使用規則式
    }

    private List<string> GenerateRecommendationReasons(
        MergeStrategy strategy, 
        ContentCharacteristics characteristics, 
        UserMergePreferences? preferences)
    {
        var reasons = new List<string>();

        switch (strategy)
        {
            case MergeStrategy.Concise:
                if (characteristics.ContentOverlap > 0.6)
                    reasons.Add("內容重複度高，適合簡潔式合併以去除冗餘");
                if (preferences?.LengthPreference <= OutputLengthPreference.Short)
                    reasons.Add("使用者偏好較短的輸出長度");
                break;

            case MergeStrategy.Detailed:
                if (characteristics.ComplexityScore > 0.7)
                    reasons.Add("內容複雜度高，需要保留完整資訊");
                if (preferences?.DetailLevel >= DetailLevelPreference.Detailed)
                    reasons.Add("使用者偏好詳細的內容呈現");
                break;

            case MergeStrategy.Structured:
                if (characteristics.TopicDiversity > 0.7)
                    reasons.Add("主題多樣性高，適合結構化組織");
                if (preferences?.StructureLevel >= StructureLevelPreference.HighlyStructured)
                    reasons.Add("使用者偏好高度結構化的輸出");
                break;

            case MergeStrategy.Balanced:
                reasons.Add("內容特徵均衡，選擇通用性強的平衡式策略");
                break;

            case MergeStrategy.Custom:
                if (preferences != null)
                    reasons.Add("根據使用者明確的自訂偏好設定");
                break;
        }

        if (!reasons.Any())
        {
            reasons.Add("基於內容分析的最佳匹配策略");
        }

        return reasons;
    }

    private MergeParameters CreateDefaultParameters(MergeStrategy strategy)
    {
        var parameters = new MergeParameters();
        
        switch (strategy)
        {
            case MergeStrategy.Concise:
                parameters.TargetLength = 400;
                parameters.ImportanceThreshold = 0.8;
                break;
            case MergeStrategy.Detailed:
                parameters.TargetLength = 1200;
                parameters.ImportanceThreshold = 0.4;
                break;
            case MergeStrategy.Structured:
                parameters.PreserveStructure = true;
                parameters.TargetLength = 800;
                break;
            case MergeStrategy.Balanced:
                parameters.TargetLength = 800;
                parameters.ImportanceThreshold = 0.6;
                break;
        }

        return parameters;
    }

    private List<AlternativeStrategy> CreateAlternatives(Dictionary<MergeStrategy, double> scores, MergeStrategy selectedStrategy)
    {
        return scores
            .Where(kvp => kvp.Key != selectedStrategy)
            .OrderByDescending(kvp => kvp.Value)
            .Take(2)
            .Select(kvp => new AlternativeStrategy
            {
                Strategy = kvp.Key,
                Method = MergeMethod.RuleBased,
                Score = kvp.Value,
                Reason = $"備選方案，評分: {kvp.Value:F2}"
            })
            .ToList();
    }

    private StrategyRecommendation GetFallbackRecommendation(UserMergePreferences? preferences)
    {
        return new StrategyRecommendation
        {
            RecommendedStrategy = MergeStrategy.Balanced,
            RecommendedMethod = MergeMethod.RuleBased,
            Parameters = new MergeParameters(),
            Confidence = 0.6,
            Reasons = new List<string> { "自動選擇失敗，使用預設平衡式策略" }
        };
    }

    private double CalculateVariance(List<int> values)
    {
        var average = values.Average();
        return values.Average(v => Math.Pow(v - average, 2));
    }

    private Task<double> CalculateTopicDiversityAsync(List<SegmentSummaryTask> summaries, CancellationToken cancellationToken)
    {
        // 簡化實作：基於內容長度和關鍵詞多樣性
        var allWords = new HashSet<string>();
        var totalWords = 0;

        foreach (var summary in summaries)
        {
            var words = summary.SummaryResult.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            totalWords += words.Length;
            foreach (var word in words)
            {
                if (word.Length > 3) // 過濾短詞
                {
                    allWords.Add(word.ToLower());
                }
            }
        }

        var result = totalWords > 0 ? (double)allWords.Count / totalWords : 0.0;
        return Task.FromResult(result);
    }

    private async Task<double> CalculateContentOverlapAsync(List<SegmentSummaryTask> summaries, CancellationToken cancellationToken)
    {
        if (summaries.Count <= 1) return 0.0;

        var similarities = new List<double>();
        
        for (int i = 0; i < summaries.Count - 1; i++)
        {
            for (int j = i + 1; j < summaries.Count; j++)
            {
                var similarity = await _similarityCalculator.CalculateSimilarityAsync(
                    summaries[i].SummaryResult,
                    summaries[j].SummaryResult,
                    SimilarityType.Jaccard,
                    cancellationToken);
                similarities.Add(similarity);
            }
        }

        return similarities.Any() ? similarities.Average() : 0.0;
    }

    private double CalculateStructureLevel(List<SegmentSummaryTask> summaries)
    {
        var structureIndicators = 0;
        var totalSummaries = summaries.Count;

        foreach (var summary in summaries)
        {
            var content = summary.SummaryResult;
            
            // 檢查結構化指標
            if (Regex.IsMatch(content, @"^\d+\.|\-\s|•\s")) structureIndicators++; // 列表項目
            if (Regex.IsMatch(content, @"#{1,6}\s")) structureIndicators++; // 標題
            if (content.Contains("\n\n")) structureIndicators++; // 段落分隔
        }

        return totalSummaries > 0 ? (double)structureIndicators / (totalSummaries * 3) : 0.0;
    }

    private double CalculateComplexityScore(List<SegmentSummaryTask> summaries, ContentCharacteristics characteristics)
    {
        var complexityFactors = new List<double>
        {
            characteristics.TopicDiversity,
            characteristics.LengthVariance / Math.Max(characteristics.AverageLength, 1),
            Math.Min(characteristics.TotalSegments / 10.0, 1.0) // 分段數量因子
        };

        return complexityFactors.Average();
    }

    private List<string> ExtractMainTopics(List<SegmentSummaryTask> summaries)
    {
        // 簡化實作：提取最常見的關鍵詞作為主題
        var wordFrequency = new Dictionary<string, int>();
        
        foreach (var summary in summaries)
        {
            var words = summary.SummaryResult.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var cleanWord = word.ToLower().Trim('.', ',', '!', '?', ';', ':');
                if (cleanWord.Length > 4) // 過濾短詞和停用詞
                {
                    wordFrequency[cleanWord] = wordFrequency.GetValueOrDefault(cleanWord, 0) + 1;
                }
            }
        }

        return wordFrequency
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private Dictionary<string, double> AnalyzeContentTypeDistribution(List<SegmentSummaryTask> summaries)
    {
        var distribution = new Dictionary<string, double>
        {
            ["描述性內容"] = 0.0,
            ["分析性內容"] = 0.0,
            ["列舉性內容"] = 0.0,
            ["結論性內容"] = 0.0
        };

        foreach (var summary in summaries)
        {
            var content = summary.SummaryResult;
            
            // 簡化的內容類型判斷
            if (Regex.IsMatch(content, @"\d+\.|\-\s|•\s|首先|其次|最後"))
                distribution["列舉性內容"] += 1.0;
            else if (content.Contains("總之") || content.Contains("因此") || content.Contains("結論"))
                distribution["結論性內容"] += 1.0;
            else if (content.Contains("分析") || content.Contains("比較") || content.Contains("評估"))
                distribution["分析性內容"] += 1.0;
            else
                distribution["描述性內容"] += 1.0;
        }

        var total = distribution.Values.Sum();
        if (total > 0)
        {
            foreach (var key in distribution.Keys.ToList())
            {
                distribution[key] /= total;
            }
        }

        return distribution;
    }

    private Dictionary<string, double> AnalyzeLanguageFeatures(List<SegmentSummaryTask> summaries)
    {
        var features = new Dictionary<string, double>
        {
            ["平均句長"] = 0.0,
            ["專業詞彙比例"] = 0.0,
            ["連接詞使用頻率"] = 0.0
        };

        var allSentences = new List<string>();
        var allWords = new List<string>();

        foreach (var summary in summaries)
        {
            var sentences = summary.SummaryResult.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            allSentences.AddRange(sentences.Select(s => s.Trim()));
            
            var words = summary.SummaryResult.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            allWords.AddRange(words);
        }

        // 計算平均句長
        if (allSentences.Any())
        {
            features["平均句長"] = allSentences.Average(s => s.Length);
        }

        // 簡化的專業詞彙檢測（基於詞長）
        var longWords = allWords.Count(w => w.Length > 6);
        features["專業詞彙比例"] = allWords.Any() ? (double)longWords / allWords.Count : 0.0;

        // 連接詞檢測
        var connectors = new[] { "因此", "然而", "同時", "此外", "另外", "而且", "但是" };
        var connectorCount = allWords.Count(w => connectors.Contains(w));
        features["連接詞使用頻率"] = allWords.Any() ? (double)connectorCount / allWords.Count : 0.0;

        return features;
    }

    private int CalculateTargetLength(OutputLengthPreference preference, ContentCharacteristics characteristics)
    {
        var baseLengths = new Dictionary<OutputLengthPreference, int>
        {
            [OutputLengthPreference.VeryShort] = 200,
            [OutputLengthPreference.Short] = 400,
            [OutputLengthPreference.Medium] = 800,
            [OutputLengthPreference.Long] = 1200,
            [OutputLengthPreference.VeryLong] = 1600
        };

        var baseLength = baseLengths[preference];
        
        // 根據內容特徵調整
        var adjustmentFactor = 1.0 + (characteristics.ComplexityScore - 0.5) * 0.3;
        
        return (int)(baseLength * adjustmentFactor);
    }

    private double CalculateSimilarityThreshold(DuplicateToleranceLevel tolerance)
    {
        return tolerance switch
        {
            DuplicateToleranceLevel.None => 0.95,
            DuplicateToleranceLevel.Low => 0.8,
            DuplicateToleranceLevel.Medium => 0.65,
            DuplicateToleranceLevel.High => 0.5,
            DuplicateToleranceLevel.Permissive => 0.3,
            _ => 0.8
        };
    }

    private double CalculateImportanceThreshold(DetailLevelPreference detailLevel)
    {
        return detailLevel switch
        {
            DetailLevelPreference.HighlySimplified => 0.9,
            DetailLevelPreference.Simplified => 0.75,
            DetailLevelPreference.Balanced => 0.6,
            DetailLevelPreference.Detailed => 0.4,
            DetailLevelPreference.VeryDetailed => 0.2,
            _ => 0.6
        };
    }

    private void ApplyCustomWeights(MergeParameters parameters, Dictionary<string, double> customWeights)
    {
        foreach (var weight in customWeights)
        {
            switch (weight.Key.ToLower())
            {
                case "targetlength":
                    parameters.TargetLength = (int)(parameters.TargetLength * weight.Value);
                    break;
                case "similaritythreshold":
                    parameters.SimilarityThreshold = Math.Min(1.0, parameters.SimilarityThreshold * weight.Value);
                    break;
                case "importancethreshold":
                    parameters.ImportanceThreshold = Math.Min(1.0, parameters.ImportanceThreshold * weight.Value);
                    break;
            }
        }
    }

    #endregion
}

/// <summary>
/// 策略性能記錄（用於學習）
/// </summary>
internal class StrategyPerformance
{
    public int UsageCount { get; set; } = 0;
    public double AverageQualityScore { get; set; } = 0.0;
    public double AverageProcessingTime { get; set; } = 0.0;
    public int UserSatisfactionCount { get; set; } = 0;
    public double AverageUserSatisfaction { get; set; } = 0.0;
}