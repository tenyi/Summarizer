using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Summarizer.Data;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;
using System.Text;

namespace Summarizer.Services;

/// <summary>
/// 部分結果處理服務實作
/// 負責處理批次處理取消時的部分結果保存和品質評估
/// </summary>
public class PartialResultHandler : IPartialResultHandler
{
    private readonly SummarizerDbContext _dbContext;
    private readonly ISummaryMergerService _summaryMergerService;
    private readonly ILogger<PartialResultHandler> _logger;

    public PartialResultHandler(
        SummarizerDbContext dbContext,
        ISummaryMergerService summaryMergerService,
        ILogger<PartialResultHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _summaryMergerService = summaryMergerService ?? throw new ArgumentNullException(nameof(summaryMergerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 處理部分結果（主要入口點）
    /// </summary>
    public async Task<PartialResult> ProcessPartialResultAsync(
        Guid batchId,
        string userId,
        List<SegmentSummaryTask> completedSegments,
        int totalSegments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始處理部分結果，BatchId: {BatchId}, 已完成分段: {CompletedCount}/{TotalCount}",
                batchId, completedSegments.Count, totalSegments);

            // 創建部分結果物件
            var partialResult = new PartialResult
            {
                BatchId = batchId,
                UserId = userId,
                CompletedSegments = completedSegments,
                TotalSegments = totalSegments,
                CompletionPercentage = (double)completedSegments.Count / totalSegments * 100.0,
                Status = PartialResultStatus.Processing,
                CancellationTime = DateTime.UtcNow,
                ProcessingTime = completedSegments
                    .Where(s => s.ProcessingTime.HasValue)
                    .Aggregate(TimeSpan.Zero, (acc, s) => acc + s.ProcessingTime!.Value)
            };

            // 評估品質
            partialResult.Quality = await EvaluateResultQualityAsync(completedSegments, totalSegments, cancellationToken);

            // 生成部分摘要
            partialResult.PartialSummary = await GeneratePartialSummaryAsync(completedSegments, partialResult.Quality, cancellationToken);

            // 生成原始文本樣本
            partialResult.OriginalTextSample = GenerateOriginalTextSample(completedSegments);

            // 更新狀態為等待用戶決定
            partialResult.Status = PartialResultStatus.PendingUserDecision;

            _logger.LogInformation("部分結果處理完成，品質等級: {Quality}, 完整性分數: {Score:F2}%",
                partialResult.Quality.OverallQuality, partialResult.Quality.CompletenessScore * 100);

            return partialResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理部分結果時發生錯誤，BatchId: {BatchId}", batchId);
            throw;
        }
    }

    /// <summary>
    /// 收集已完成的分段任務
    /// </summary>
        public Task<List<SegmentSummaryTask>> CollectCompletedSegmentsAsync(
            List<SegmentSummaryTask> allTasks,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (allTasks == null || !allTasks.Any())
                {
                    _logger.LogWarning("沒有可用的任務列表");
                    return Task.FromResult(new List<SegmentSummaryTask>());
                }

                // 篩選已完成且有結果的分段
                var completedSegments = allTasks
                    .Where(task => task.Status == SegmentTaskStatus.Completed && 
                                  !string.IsNullOrWhiteSpace(task.SummaryResult))
                    .OrderBy(task => task.SegmentIndex)
                    .ToList();

                _logger.LogInformation("收集到 {Count} 個已完成的分段", completedSegments.Count);

                return Task.FromResult(completedSegments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收集已完成分段時發生錯誤");
                return Task.FromResult(new List<SegmentSummaryTask>());
            }
        }

    /// <summary>
    /// 評估部分結果的品質
    /// </summary>
    public async Task<PartialResultQuality> EvaluateResultQualityAsync(
        List<SegmentSummaryTask> completedSegments,
        int totalSegments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var quality = new PartialResultQuality();

            // 計算完整性分數
            quality.CompletenessScore = (double)completedSegments.Count / totalSegments;

            // 評估內容覆蓋率
            quality.Coverage = EvaluateContentCoverage(completedSegments, totalSegments);

            // 計算語意連貫性分數
            quality.CoherenceScore = await CalculateCoherenceScoreAsync(completedSegments, cancellationToken);

            // 評估邏輯連貫性
            quality.HasLogicalFlow = EvaluateLogicalFlow(completedSegments);

            // 識別遺漏主題
            quality.MissingTopics = IdentifyMissingTopics(completedSegments, totalSegments);

            // 生成品質警告
            quality.QualityWarnings = GenerateQualityWarnings(quality, completedSegments);

            // 確定總體品質等級
            quality.OverallQuality = DetermineOverallQuality(quality);

            // 推薦動作
            quality.RecommendedAction = DetermineRecommendedAction(quality);

            // 生成品質說明
            quality.QualityExplanation = GenerateQualityExplanation(quality);

            return quality;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "評估品質時發生錯誤");
            
            // 返回預設的低品質評估
            return new PartialResultQuality
            {
                CompletenessScore = (double)completedSegments.Count / totalSegments,
                HasLogicalFlow = false,
                CoherenceScore = 0.0,
                OverallQuality = QualityLevel.Poor,
                RecommendedAction = RecommendedAction.ReviewRequired,
                QualityWarnings = { "品質評估過程中發生錯誤" },
                QualityExplanation = "由於系統錯誤，無法完成完整的品質評估。建議謹慎審查結果。"
            };
        }
    }

    /// <summary>
    /// 生成部分摘要
    /// </summary>
    public async Task<string> GeneratePartialSummaryAsync(
        List<SegmentSummaryTask> completedSegments,
        PartialResultQuality quality,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!completedSegments.Any())
            {
                return "由於沒有完成的分段，無法生成摘要。";
            }

            // 使用現有的合併服務來合併分段摘要
            var mergeResult = await _summaryMergerService.MergeSummariesAsync(
                completedSegments, 
                Models.SummaryMerging.MergeStrategy.Balanced, // 使用平衡策略，適合部分內容
                null,
                cancellationToken);

            if (!string.IsNullOrEmpty(mergeResult?.FinalSummary))
            {
                var partialSummary = new StringBuilder();
                
                // 添加部分結果說明
                partialSummary.AppendLine("【部分摘要結果】");
                partialSummary.AppendLine($"完成度：{quality.CompletenessScore:P1} ({completedSegments.Count}/{completedSegments.Count + (int)((1 - quality.CompletenessScore) * completedSegments.Count / quality.CompletenessScore)})");
                partialSummary.AppendLine($"品質等級：{GetQualityLevelText(quality.OverallQuality)}");
                
                if (quality.QualityWarnings.Any())
                {
                    partialSummary.AppendLine("⚠️ 品質提醒：" + string.Join("、", quality.QualityWarnings));
                }
                
                partialSummary.AppendLine();
                partialSummary.AppendLine("【摘要內容】");
                partialSummary.AppendLine(mergeResult.FinalSummary);

                if (quality.MissingTopics.Any())
                {
                    partialSummary.AppendLine();
                    partialSummary.AppendLine("【可能遺漏的主題】");
                    foreach (var topic in quality.MissingTopics)
                    {
                        partialSummary.AppendLine($"• {topic}");
                    }
                }

                return partialSummary.ToString();
            }
            else
            {
                // 降級處理：簡單串接
                return GenerateBasicConcatenatedSummary(completedSegments, quality);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成部分摘要時發生錯誤");
            return GenerateBasicConcatenatedSummary(completedSegments, quality);
        }
    }

    /// <summary>
    /// 保存部分結果
    /// </summary>
    public async Task<bool> SavePartialResultAsync(
        PartialResult partialResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.PartialResults.Add(partialResult);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("部分結果已保存，ID: {PartialResultId}, BatchId: {BatchId}",
                partialResult.PartialResultId, partialResult.BatchId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存部分結果時發生錯誤，ID: {PartialResultId}",
                partialResult.PartialResultId);
            return false;
        }
    }

    /// <summary>
    /// 獲取部分結果
    /// </summary>
    public async Task<PartialResult?> GetPartialResultAsync(
        Guid partialResultId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.PartialResults
                .FirstOrDefaultAsync(pr => pr.PartialResultId == partialResultId && pr.UserId == userId,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取部分結果時發生錯誤，ID: {PartialResultId}", partialResultId);
            return null;
        }
    }

    /// <summary>
    /// 更新部分結果狀態
    /// </summary>
    public async Task<bool> UpdatePartialResultStatusAsync(
        Guid partialResultId,
        PartialResultStatus status,
        string? userComment,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var partialResult = await _dbContext.PartialResults
                .FirstOrDefaultAsync(pr => pr.PartialResultId == partialResultId && pr.UserId == userId,
                    cancellationToken);

            if (partialResult == null)
            {
                _logger.LogWarning("找不到部分結果或沒有權限，ID: {PartialResultId}, UserId: {UserId}",
                    partialResultId, userId);
                return false;
            }

            partialResult.Status = status;
            partialResult.UserAccepted = status == PartialResultStatus.Accepted;
            partialResult.AcceptedTime = DateTime.UtcNow;
            
            if (!string.IsNullOrWhiteSpace(userComment))
            {
                partialResult.UserComment = userComment;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("部分結果狀態已更新，ID: {PartialResultId}, 新狀態: {Status}",
                partialResultId, status);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新部分結果狀態時發生錯誤，ID: {PartialResultId}", partialResultId);
            return false;
        }
    }

    /// <summary>
    /// 獲取用戶的部分結果列表
    /// </summary>
    public async Task<List<PartialResult>> GetUserPartialResultsAsync(
        string userId,
        PartialResultStatus? status = null,
        int pageIndex = 0,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.PartialResults
                .Where(pr => pr.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(pr => pr.Status == status.Value);
            }

            return await query
                .OrderByDescending(pr => pr.CancellationTime)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取用戶部分結果列表時發生錯誤，UserId: {UserId}", userId);
            return new List<PartialResult>();
        }
    }

    /// <summary>
    /// 清理過期的部分結果
    /// </summary>
    public async Task<int> CleanupExpiredPartialResultsAsync(
        int expireAfterHours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var expireTime = DateTime.UtcNow.AddHours(-expireAfterHours);
            
            var expiredResults = await _dbContext.PartialResults
                .Where(pr => pr.Status == PartialResultStatus.PendingUserDecision && 
                            pr.CancellationTime < expireTime)
                .ToListAsync(cancellationToken);

            foreach (var result in expiredResults)
            {
                result.Status = PartialResultStatus.Expired;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("已清理 {Count} 個過期的部分結果", expiredResults.Count);
            
            return expiredResults.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理過期部分結果時發生錯誤");
            return 0;
        }
    }

    /// <summary>
    /// 檢查是否可以從部分結果繼續處理
    /// </summary>
    public async Task<bool> CanContinueFromPartialResultAsync(
        Guid partialResultId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var partialResult = await GetPartialResultAsync(partialResultId, userId, cancellationToken);
            
            if (partialResult == null)
            {
                return false;
            }

            // 檢查品質是否足夠好，可以繼續處理
            return partialResult.Quality.OverallQuality >= QualityLevel.Acceptable &&
                   partialResult.Quality.CompletenessScore >= 0.3; // 至少完成 30%
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查是否可以繼續處理時發生錯誤，ID: {PartialResultId}", partialResultId);
            return false;
        }
    }

    #region 私有輔助方法

    /// <summary>
    /// 評估內容覆蓋率
    /// </summary>
    private ContentCoverage EvaluateContentCoverage(List<SegmentSummaryTask> completedSegments, int totalSegments)
    {
        var coverage = new ContentCoverage();
        
        if (!completedSegments.Any())
        {
            return coverage;
        }

        var segmentIndices = completedSegments.Select(s => s.SegmentIndex).OrderBy(i => i).ToList();
        var firstIndex = segmentIndices.First();
        var lastIndex = segmentIndices.Last();

        // 計算各部分的覆蓋率
        var beginningThird = totalSegments / 3;
        var middleThird = beginningThird * 2;
        
        coverage.BeginningCoverage = (double)segmentIndices.Count(i => i < beginningThird) / beginningThird;
        coverage.MiddleCoverage = (double)segmentIndices.Count(i => i >= beginningThird && i < middleThird) / beginningThird;
        coverage.EndCoverage = (double)segmentIndices.Count(i => i >= middleThird) / (totalSegments - middleThird);

        // 檢查連續覆蓋
        var maxContinuous = 0;
        var currentContinuous = 1;
        
        for (int i = 1; i < segmentIndices.Count; i++)
        {
            if (segmentIndices[i] == segmentIndices[i - 1] + 1)
            {
                currentContinuous++;
            }
            else
            {
                maxContinuous = Math.Max(maxContinuous, currentContinuous);
                currentContinuous = 1;
            }
        }
        
        coverage.MaxContinuousLength = Math.Max(maxContinuous, currentContinuous);
        coverage.HasContinuousCoverage = coverage.MaxContinuousLength >= Math.Min(3, completedSegments.Count);

        // 計算覆蓋間隙
        coverage.CoverageGaps = 0;
        for (int i = 1; i < segmentIndices.Count; i++)
        {
            var gap = segmentIndices[i] - segmentIndices[i - 1] - 1;
            if (gap > 0)
            {
                coverage.CoverageGaps++;
            }
        }

        return coverage;
    }

    /// <summary>
    /// 計算語意連貫性分數
    /// </summary>
    private async Task<double> CalculateCoherenceScoreAsync(List<SegmentSummaryTask> completedSegments, CancellationToken cancellationToken)
    {
        if (completedSegments.Count < 2)
        {
            return completedSegments.Any() ? 0.8 : 0.0; // 單個分段假設有基本連貫性
        }

        try
        {
            // 使用合併服務來評估連貫性
            var previewResult = await _summaryMergerService.PreviewMergeAsync(
                completedSegments, 
                Models.SummaryMerging.MergeStrategy.Balanced, 
                null, 
                cancellationToken);

            // 基於預估品質分數來評估連貫性
            return Math.Max(0.0, Math.Min(1.0, previewResult.EstimatedQualityScore));
        }
        catch
        {
            // 降級處理：基於分段的連續性來估算
            var segmentIndices = completedSegments.Select(s => s.SegmentIndex).OrderBy(i => i).ToList();
            var continuousRatio = 0.0;
            
            for (int i = 1; i < segmentIndices.Count; i++)
            {
                if (segmentIndices[i] == segmentIndices[i - 1] + 1)
                {
                    continuousRatio += 1.0;
                }
            }
            
            return segmentIndices.Count > 1 ? continuousRatio / (segmentIndices.Count - 1) : 0.5;
        }
    }

    /// <summary>
    /// 評估邏輯連貫性
    /// </summary>
    private bool EvaluateLogicalFlow(List<SegmentSummaryTask> completedSegments)
    {
        if (completedSegments.Count < 2)
        {
            return completedSegments.Any();
        }

        var segmentIndices = completedSegments.Select(s => s.SegmentIndex).OrderBy(i => i).ToList();
        
        // 檢查是否有足夠的連續分段來形成邏輯流
        var consecutiveCount = 0;
        for (int i = 1; i < segmentIndices.Count; i++)
        {
            if (segmentIndices[i] == segmentIndices[i - 1] + 1)
            {
                consecutiveCount++;
            }
        }

        // 如果至少有一半的分段是連續的，認為有邏輯流
        return consecutiveCount >= (segmentIndices.Count - 1) * 0.5;
    }

    /// <summary>
    /// 識別遺漏主題
    /// </summary>
    private List<string> IdentifyMissingTopics(List<SegmentSummaryTask> completedSegments, int totalSegments)
    {
        var missingTopics = new List<string>();
        var completedCount = completedSegments.Count;
        var missingCount = totalSegments - completedCount;
        
        if (missingCount == 0)
        {
            return missingTopics;
        }

        var segmentIndices = completedSegments.Select(s => s.SegmentIndex).OrderBy(i => i).ToList();
        
        // 分析遺漏的分段位置
        var missingFromBeginning = segmentIndices.Any() ? segmentIndices.Min() : totalSegments;
        var missingFromEnd = segmentIndices.Any() ? totalSegments - segmentIndices.Max() - 1 : totalSegments;

        if (missingFromBeginning > 0)
        {
            missingTopics.Add($"文件開頭的 {missingFromBeginning} 個分段內容");
        }

        if (missingFromEnd > 0)
        {
            missingTopics.Add($"文件結尾的 {missingFromEnd} 個分段內容");
        }

        // 檢查中間的間隙
        for (int i = 1; i < segmentIndices.Count; i++)
        {
            var gap = segmentIndices[i] - segmentIndices[i - 1] - 1;
            if (gap > 0)
            {
                missingTopics.Add($"分段 {segmentIndices[i - 1] + 1} 到 {segmentIndices[i] - 1} 之間的 {gap} 個分段內容");
            }
        }

        return missingTopics;
    }

    /// <summary>
    /// 生成品質警告
    /// </summary>
    private List<string> GenerateQualityWarnings(PartialResultQuality quality, List<SegmentSummaryTask> completedSegments)
    {
        var warnings = new List<string>();

        if (quality.CompletenessScore < 0.3)
        {
            warnings.Add("完成度過低，可能影響摘要的完整性");
        }

        if (quality.CoherenceScore < 0.5)
        {
            warnings.Add("分段間的連貫性較差，摘要可能不夠流暢");
        }

        if (!quality.Coverage.HasContinuousCoverage)
        {
            warnings.Add("缺乏連續的內容覆蓋，摘要可能有跳躍性");
        }

        if (quality.Coverage.CoverageGaps > 2)
        {
            warnings.Add($"存在 {quality.Coverage.CoverageGaps} 個內容間隙，部分主題可能遺漏");
        }

        if (quality.Coverage.BeginningCoverage < 0.2)
        {
            warnings.Add("開頭部分內容缺失，可能影響背景理解");
        }

        if (quality.Coverage.EndCoverage < 0.2)
        {
            warnings.Add("結尾部分內容缺失，可能遺漏結論或總結");
        }

        return warnings;
    }

    /// <summary>
    /// 確定總體品質等級
    /// </summary>
    private QualityLevel DetermineOverallQuality(PartialResultQuality quality)
    {
        var completeness = quality.CompletenessScore;
        var coherence = quality.CoherenceScore;
        
        // 品質等級主要基於完整性和連貫性
        var averageScore = (completeness * 0.7 + coherence * 0.3); // 完整性權重更高

        return averageScore switch
        {
            >= 0.8 => QualityLevel.Excellent,
            >= 0.6 => QualityLevel.Good,
            >= 0.4 => QualityLevel.Acceptable,
            >= 0.2 => QualityLevel.Poor,
            _ => QualityLevel.Unusable
        };
    }

    /// <summary>
    /// 確定推薦動作
    /// </summary>
    private RecommendedAction DetermineRecommendedAction(PartialResultQuality quality)
    {
        return quality.OverallQuality switch
        {
            QualityLevel.Excellent => RecommendedAction.Recommend,
            QualityLevel.Good => RecommendedAction.Recommend,
            QualityLevel.Acceptable => RecommendedAction.ReviewRequired,
            QualityLevel.Poor => RecommendedAction.ConsiderContinue,
            QualityLevel.Unusable => RecommendedAction.Discard,
            _ => RecommendedAction.ReviewRequired
        };
    }

    /// <summary>
    /// 生成品質說明
    /// </summary>
    private string GenerateQualityExplanation(PartialResultQuality quality)
    {
        var explanation = new StringBuilder();

        explanation.AppendLine($"完整性分數：{quality.CompletenessScore:P1}");
        explanation.AppendLine($"連貫性分數：{quality.CoherenceScore:P1}");
        explanation.AppendLine($"總體品質：{GetQualityLevelText(quality.OverallQuality)}");

        if (quality.QualityWarnings.Any())
        {
            explanation.AppendLine();
            explanation.AppendLine("注意事項：");
            foreach (var warning in quality.QualityWarnings)
            {
                explanation.AppendLine($"• {warning}");
            }
        }

        explanation.AppendLine();
        explanation.Append($"建議動作：{GetRecommendedActionText(quality.RecommendedAction)}");

        return explanation.ToString();
    }

    /// <summary>
    /// 獲取品質等級文字描述
    /// </summary>
    private string GetQualityLevelText(QualityLevel level)
    {
        return level switch
        {
            QualityLevel.Excellent => "優秀",
            QualityLevel.Good => "良好",
            QualityLevel.Acceptable => "可接受",
            QualityLevel.Poor => "較差",
            QualityLevel.Unusable => "不可用",
            _ => "未知"
        };
    }

    /// <summary>
    /// 獲取推薦動作文字描述
    /// </summary>
    private string GetRecommendedActionText(RecommendedAction action)
    {
        return action switch
        {
            RecommendedAction.Recommend => "建議保存此結果",
            RecommendedAction.ReviewRequired => "建議審查後決定",
            RecommendedAction.ConsiderContinue => "考慮繼續處理以改善品質",
            RecommendedAction.Discard => "建議丟棄此結果",
            _ => "需要手動判斷"
        };
    }

    /// <summary>
    /// 生成原始文本樣本
    /// </summary>
    private string GenerateOriginalTextSample(List<SegmentSummaryTask> completedSegments)
    {
        var sample = new StringBuilder();
        var segmentCount = Math.Min(3, completedSegments.Count); // 最多取3個分段作為樣本
        
        for (int i = 0; i < segmentCount; i++)
        {
            var segment = completedSegments[i];
            var sampleText = segment.SourceSegment.Content.Length > 200 
                ? segment.SourceSegment.Content.Substring(0, 200) + "..."
                : segment.SourceSegment.Content;
                
            sample.AppendLine($"分段 {segment.SegmentIndex + 1}：{sampleText}");
            sample.AppendLine();
        }
        
        if (completedSegments.Count > segmentCount)
        {
            sample.AppendLine($"... 以及其他 {completedSegments.Count - segmentCount} 個分段");
        }

        return sample.ToString();
    }

    /// <summary>
    /// 生成基本的串接摘要（降級處理）
    /// </summary>
    private string GenerateBasicConcatenatedSummary(List<SegmentSummaryTask> completedSegments, PartialResultQuality quality)
    {
        var summary = new StringBuilder();
        
        summary.AppendLine("【部分摘要結果 - 基礎合併】");
        summary.AppendLine($"完成度：{quality.CompletenessScore:P1}");
        summary.AppendLine($"品質等級：{GetQualityLevelText(quality.OverallQuality)}");
        summary.AppendLine();
        summary.AppendLine("【摘要內容】");

        var orderedSegments = completedSegments.OrderBy(s => s.SegmentIndex).ToList();
        
        for (int i = 0; i < orderedSegments.Count; i++)
        {
            var segment = orderedSegments[i];
            
            if (i > 0)
            {
                // 檢查是否有間隙
                var previousIndex = orderedSegments[i - 1].SegmentIndex;
                if (segment.SegmentIndex > previousIndex + 1)
                {
                    summary.AppendLine();
                    summary.AppendLine($"[中間跳過了 {segment.SegmentIndex - previousIndex - 1} 個分段]");
                    summary.AppendLine();
                }
            }
            
            summary.AppendLine($"{i + 1}. {segment.SummaryResult}");
            summary.AppendLine();
        }

        if (quality.MissingTopics.Any())
        {
            summary.AppendLine("【可能遺漏的主題】");
            foreach (var topic in quality.MissingTopics)
            {
                summary.AppendLine($"• {topic}");
            }
        }

        return summary.ToString();
    }

    #endregion
}